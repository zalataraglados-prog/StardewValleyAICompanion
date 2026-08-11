[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-special-order-acceptance-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-special-order-acceptance",
    [int] $StartupTimeoutSeconds = 150,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 120) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 48) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $last = "save_status=$($snapshot.save_id.status)"
        } catch {
            $last = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for snapshot: $last"
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-special-order-acceptance"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) { throw "SMAPI executable not found: $smapiExecutable" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$loadedModAllowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in $loadedModAllowlist) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $windowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $gameProcess = Start-Process -FilePath $smapiExecutable -WorkingDirectory $gameDirectory -WindowStyle $windowStyle -PassThru
    $initial = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds

    $fixture = Invoke-JsonPost $executeUrl (New-Request $initial "debug.setup_special_order_acceptance" "$RunId.fixture")
    Write-Json (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Special-order fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $ready = Wait-Snapshot $snapshotUrl 60
    Write-Json (Join-Path $runDirectory "ready-snapshot.json") $ready
    $board = @($ready.state.quests.special_order_boards.value | Where-Object { $_.board_type -eq "" }) | Select-Object -First 1
    if ($null -eq $board -or -not $board.unlocked -or $board.accepted_this_cycle) {
        throw "Transparent ordinary special-order board is not ready: $(@($board.blocked_diagnostics) -join ',')"
    }

    $interactRequest = New-Request $ready "executor.interact" "$RunId.interact"
    $interactRequest.target_tile_x = [int]$board.action_tile_x
    $interactRequest.target_tile_y = [int]$board.action_tile_y
    $interactRequest.interaction_kind = "map_action"
    $interactRequest.expected_action_type = "SpecialOrders"
    $interact = Invoke-JsonPost $executeUrl $interactRequest
    Write-Json (Join-Path $runDirectory "interact-result.json") $interact
    if ($interact.status -ne "applied") { throw "Special-order board interaction failed: $(@($interact.block_reasons) -join ',')" }

    $menuSnapshot = Wait-Snapshot $snapshotUrl 30
    $menuBoard = @($menuSnapshot.state.quests.special_order_boards.value | Where-Object { $_.board_type -eq "" -and $_.menu_open }) | Select-Object -First 1
    $offer = @($menuBoard.offers) | Select-Object -First 1
    if ($null -eq $offer) { throw "Native SpecialOrdersBoard exposed no offer." }

    $acceptRequest = New-Request $menuSnapshot "executor.accept_special_order" "$RunId.accept"
    $acceptRequest.quest_candidate_id = "special_order_offer:$($offer.offer_fingerprint)"
    $acceptRequest.quest_family = "special_order"
    $acceptRequest.quest_key = [string]$offer.order.quest_key
    $acceptRequest.quest_interaction_kind = "accept_special_order"
    $acceptRequest.quest_offer_fingerprint = [string]$offer.offer_fingerprint
    $acceptRequest.quest_offer_title = [string]$offer.order.quest_name
    $acceptRequest.special_order_board_type = ""
    $acceptRequest.special_order_selection_index = [int]$offer.selection_index
    $acceptRequest.special_order_selection_side = [string]$offer.selection_side
    $acceptRequest.special_order_generation_seed = [int]$offer.order.generation_seed
    $acceptRequest.special_order_due_date = [int]$offer.order.due_date
    $acceptRequest.special_order_duration = [string]$offer.order.duration
    $accept = Invoke-JsonPost $executeUrl $acceptRequest
    Write-Json (Join-Path $runDirectory "accept-result.json") $accept

    $after = Wait-Snapshot $snapshotUrl 30
    Write-Json (Join-Path $runDirectory "after-snapshot.json") $after
    $acceptedOrder = @($after.state.quests.special_orders.value | Where-Object {
        $_.quest_key -eq $offer.order.quest_key -and $_.generation_seed -eq $offer.order.generation_seed
    }) | Select-Object -First 1
    $typeAccepted = @($after.state.quests.accepted_special_order_types.value) -contains ""
    $passed = $accept.status -eq "applied" -and
        $accept.primitive_verification_status -eq "verified" -and
        $null -ne $acceptedOrder -and $typeAccepted
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_special_order_acceptance_smoke.v1"
        run_id = $RunId
        status = if ($passed) { "passed" } else { "failed" }
        fixture_status = $fixture.status
        interact_status = $interact.status
        accept_status = $accept.status
        accept_verification = $accept.primitive_verification_status
        accepted_quest_key = [string]$offer.order.quest_key
        accepted_generation_seed = [int]$offer.order.generation_seed
        matching_order_present_after = $null -ne $acceptedOrder
        order_type_accepted_after = $typeAccepted
        loaded_mod_allowlist = $loadedModAllowlist
        smoke_mods_path = $smokeModsPath
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 5
    if (-not $passed) { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
