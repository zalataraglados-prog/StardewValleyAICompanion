param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-cookout-kit-placement-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $TargetTileX = 64,
    [int] $TargetTileY = 15,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-Post([string] $Url, $Body) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec 120
}
function Wait-World([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $Url -TimeoutSec 15
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.player.cookout_kit_placement.status -in @("available", "derived") -and
                $snapshot.state.player.cooking.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for cookout placement snapshot. Last error: $last"
}
function New-Request($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-cookout-kit-placement"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = $TargetTileX
        target_tile_y = $TargetTileY
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) { throw "SMAPI executable not found: $smapi" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save exists under $savesPath" }
    $SaveSlot = $slot.Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Cookout placement smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-cookout-kit-placement\" + $RunId)
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $gameDirectory "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$game = $null
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
    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru

    $initial = Wait-World $snapshotUrl $StartupTimeoutSeconds
    $fixtureRequest = New-Request $initial "debug.setup_cookout_kit_placement_target" "$RunId.fixture"
    $fixture = Invoke-Post $executeUrl $fixtureRequest
    Write-Json (Join-Path $artifactDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Cookout placement fixture failed: $(@($fixture.block_reasons) -join ',')"
    }
    $TargetTileX = [int]$fixture.target_tile_x
    $TargetTileY = [int]$fixture.target_tile_y

    Start-Sleep -Milliseconds 750
    $before = Wait-World $snapshotUrl 60
    $placement = $before.state.player.cookout_kit_placement.value
    $kit = @($placement.rows | Where-Object {
        [string]$_.qualified_item_id -eq "(O)926" -and [int]$_.stack -gt 0
    }) | Select-Object -First 1
    $location = @($kit.locations | Where-Object {
        [string]$_.location_id -eq [string]$before.state.player.location_id.value -and
        [string]$_.placement_probe_status -eq "native_legal_tiles_available"
    }) | Select-Object -First 1
    $range = @($location.static_legal_tile_ranges | Where-Object {
        [int]$_.y -eq $TargetTileY -and [int]$_.start_x -le $TargetTileX -and [int]$_.end_x -ge $TargetTileX
    }) | Select-Object -First 1
    if ($null -eq $kit -or $null -eq $range) { throw "Transparent Cookout Kit placement projection is incomplete." }
    Write-Json (Join-Path $artifactDirectory "snapshot-before.json") $before

    $applyRequest = New-Request $before "executor.place_cookout_kit" "$RunId.place"
    $applyRequest["location_id"] = [string]$before.state.player.location_id.value
    $applyRequest["inventory_slot_index"] = [int]$kit.inventory_slot_index
    $applyRequest["qualified_item_id"] = "(O)926"
    $applyRequest["native_contract"] = [string]$placement.native_runtime_contract
    $applyRequest["max_movement_tiles"] = 512
    $result = Invoke-Post $executeUrl $applyRequest
    Write-Json (Join-Path $artifactDirectory "placement-result.json") $result

    Start-Sleep -Milliseconds 750
    $after = Wait-World $snapshotUrl 60
    Write-Json (Join-Path $artifactDirectory "snapshot-after.json") $after
    $cookoutSource = @($after.state.player.cooking.value.rows | Where-Object {
        [string]$_.cooking_source_kind -eq "cookout_kit" -and
        [int]$_.interaction_tile_x -eq $TargetTileX -and [int]$_.interaction_tile_y -eq $TargetTileY
    }) | Select-Object -First 1
    $passed = $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified" -and
        $null -ne $cookoutSource -and [string]$cookoutSource.native_contract -like "Torch.checkForAction*"
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_cookout_kit_placement_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        target_tile = "$TargetTileX,$TargetTileY"
        execution_status = [string]$result.status
        verification_status = [string]$result.primitive_verification_status
        verification_reasons = @($result.primitive_verification_reasons)
        block_reasons = @($result.block_reasons)
        transparent_cooking_handoff_verified = $null -ne $cookoutSource
        transparent_cooking_source_id = [string]$cookoutSource.cooking_source_id
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) { throw "Runtime Cookout Kit placement smoke failed: $artifactDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
    }
}
