[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-mail-processing-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-mail-processing",
    [int] $StartupTimeoutSeconds = 150,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for full snapshot."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-mail-processing"
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

$cases = @(
    [ordered]@{ mail_id = "Robin"; expected = "attachment" },
    [ordered]@{ mail_id = "quest10"; expected = "money" },
    [ordered]@{ mail_id = "RarecrowSociety"; expected = "crafting_recipe" },
    [ordered]@{ mail_id = "winter_21_1"; expected = "quest" },
    [ordered]@{ mail_id = "CF_Fish"; expected = "stardrop" }
)
$environmentNames = @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
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
    $snapshot = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $snapshot

    $rows = @()
    foreach ($case in $cases) {
        $mailId = [string]$case.mail_id
        $fixtureRequest = New-Request $snapshot "debug.setup_mail" "$RunId.fixture.$mailId"
        $fixtureRequest.target_runtime_identity = $mailId
        $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
        Write-Json (Join-Path $runDirectory ("fixture-$mailId.json")) $fixture
        if ($fixture.status -ne "applied") { throw "Mail fixture $mailId failed: $(@($fixture.block_reasons) -join ',')" }

        $readyDeadline = (Get-Date).AddSeconds(30)
        do {
            $ready = Wait-Snapshot $snapshotUrl 30
            $mailbox = $ready.state.quests.mailbox_processing.value
            $atStand = $ready.state.player.location_id.value -eq $mailbox.mailbox_location_id -and
                [int]$ready.state.player.tile_x.value -eq [int]$mailbox.stand_tile_x -and
                [int]$ready.state.player.tile_y.value -eq [int]$mailbox.stand_tile_y
            if ($mailbox.pending_mail_id -eq $mailId -and $mailbox.status -eq "ready" -and $atStand) { break }
            Start-Sleep -Milliseconds 250
        } while ((Get-Date) -lt $readyDeadline)
        if ($mailbox.pending_mail_id -ne $mailId -or $mailbox.status -ne "ready") {
            throw "Transparent mailbox mismatch for ${mailId}: status=$($mailbox.status), reasons=$(@($mailbox.blocked_diagnostics) -join ',')"
        }
        if (-not $atStand) { throw "Player did not settle at owned mailbox stand for $mailId" }
        $moneyBefore = [int]$ready.state.player.money.value
        $staminaBefore = [int]$ready.state.player.max_stamina.value
        $recipesBefore = @($ready.state.world_progress.crafting_recipes.value.psobject.Properties.Name).Count

        $openRequest = New-Request $ready "executor.interact" "$RunId.open.$mailId"
        $openRequest.target_tile_x = [int]$mailbox.mailbox_action_tile_x
        $openRequest.target_tile_y = [int]$mailbox.mailbox_action_tile_y
        $openRequest.interaction_kind = "map_action"
        $openRequest.expected_action_type = "Mailbox"
        $openRequest.target_runtime_type = "Mailbox"
        $openRequest.target_runtime_identity = $mailId
        $open = Invoke-JsonPost $executeUrl $openRequest
        if ($open.status -ne "applied") { throw "Mail open $mailId failed: $(@($open.block_reasons) -join ',')" }

        $letterSnapshot = Wait-Snapshot $snapshotUrl 30
        $letter = $letterSnapshot.state.menus.menu_specific_state.value
        if ($letterSnapshot.state.menus.active_menu.value.type -ne "LetterViewerMenu" -or $letter.mail_title -ne $mailId) {
            throw "LetterViewer identity mismatch for $mailId"
        }
        $moneyAfterOpen = [int]$letterSnapshot.state.player.money.value
        $recipesAfterOpen = @($letterSnapshot.state.world_progress.crafting_recipes.value.psobject.Properties.Name).Count

        $closeRequest = New-Request $letterSnapshot "executor.close_menu" "$RunId.close.$mailId"
        $closeRequest.target_runtime_type = "LetterViewerMenu"
        $closeRequest.target_runtime_identity = $mailId
        $closeRequest.expected_output_items_json = (ConvertTo-Json -InputObject @($letter.attachments) -Depth 32 -Compress)
        $closeRequest.quest_id = if ($null -eq $letter.quest_id) { "" } else { [string]$letter.quest_id }
        $closeRequest.quest_key = if ($null -eq $letter.special_order_id) { "" } else { [string]$letter.special_order_id }
        $close = Invoke-JsonPost $executeUrl $closeRequest 180
        $after = Wait-Snapshot $snapshotUrl 30
        $questAccepted = @($after.state.quests.active_quests.value | Where-Object { [string]$_.id -eq "113" }).Count -gt 0
        $staminaFact = @($close.changed_facts | Where-Object path -eq "player.max_stamina" | Select-Object -First 1)
        $observedStaminaDelta = if ($staminaFact.Count -eq 1) { [int]$staminaFact[0].after - [int]$staminaFact[0].before } else { 0 }
        $caseEffect = switch ([string]$case.expected) {
            "attachment" {
                @($close.primitive_verification_reasons | Where-Object {
                    $_ -match '^attachment_clicks=[1-9][0-9]*$'
                }).Count -eq 1
            }
            "money" { $moneyAfterOpen -gt $moneyBefore }
            "crafting_recipe" { $recipesAfterOpen -gt $recipesBefore }
            "quest" { $questAccepted }
            "stardrop" { $observedStaminaDelta -eq 34 }
            default { $false }
        }
        $passed = $open.status -eq "applied" -and $close.status -eq "applied" -and
            $close.primitive_verification_status -eq "verified" -and
            -not [bool]$after.state.menus.active_menu.value.is_open -and $caseEffect
        $row = [ordered]@{
            mail_id = $mailId
            expected_effect = [string]$case.expected
            status = if ($passed) { "passed" } else { "failed" }
            open_status = $open.status
            close_status = $close.status
            close_verification = $close.primitive_verification_status
            money_delta_on_open = $moneyAfterOpen - $moneyBefore
            crafting_recipe_count_delta_on_open = $recipesAfterOpen - $recipesBefore
            quest_113_accepted = $questAccepted
            max_stamina_delta = $observedStaminaDelta
            final_menu_open = [bool]$after.state.menus.active_menu.value.is_open
        }
        $rows += $row
        Write-Json (Join-Path $runDirectory ("case-$mailId.json")) ([ordered]@{ fixture = $fixture; mailbox = $mailbox; open = $open; letter = $letter; close = $close; summary = $row })
        if (-not $passed) { throw "Mail case $mailId failed runtime verification." }
        $snapshot = $after
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_mail_processing_smoke.v1"
        run_id = $RunId
        status = if (@($rows | Where-Object status -ne "passed").Count -eq 0 -and $rows.Count -eq $cases.Count) { "passed" } else { "failed" }
        passed_cases = @($rows | Where-Object status -eq "passed").Count
        total_cases = $rows.Count
        loaded_mod_allowlist = $loadedModAllowlist
        cases = $rows
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 8
    if ($summary.status -ne "passed") { exit 2 }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name]) }
    if (-not $KeepGameRunning -and $null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
