[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-profession-choice-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-profession-choice",
    [int] $StartupTimeoutSeconds = 150,
    [int[]] $ProfessionChoiceIds = (0..29),
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
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet $Url 30
            if ($snapshot.schema_version -eq "snapshot.v1" -and $snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for full snapshot."
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-profession-choice"
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
    $snapshot = Wait-Snapshot $snapshotUrl $StartupTimeoutSeconds
    Write-Json (Join-Path $runDirectory "initial-snapshot.json") $snapshot

    $rows = @()
    foreach ($choiceId in $ProfessionChoiceIds) {
        $fixtureRequest = New-Request $snapshot "debug.setup_level_up_profession" "$RunId.fixture.$choiceId"
        $fixtureRequest.profession_choice_id = $choiceId
        $fixture = Invoke-JsonPost $executeUrl $fixtureRequest
        Write-Json (Join-Path $runDirectory ("fixture-{0:D2}.json" -f $choiceId)) $fixture
        if ($fixture.status -ne "applied") { throw "Profession fixture $choiceId failed: $(@($fixture.block_reasons) -join ',')" }

        $ready = Wait-Snapshot $snapshotUrl 30
        $menuState = $ready.state.menus.menu_specific_state.value
        $offeredIds = @($menuState.profession_choices | ForEach-Object { [int]$_.profession_id })
        if ($ready.state.menus.active_menu.value.type -ne "LevelUpMenu" -or $choiceId -notin $offeredIds) {
            throw "Transparent profession menu mismatch for $choiceId; offered=$($offeredIds -join ',')"
        }
        if (@($menuState.profession_choices | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.title) -or @($_.description_lines).Count -eq 0 }).Count -gt 0) {
            throw "Transparent title or description missing for choice $choiceId"
        }

        $maxHealthBefore = [int]$ready.state.player.max_health.value
        $executeRequest = New-Request $ready "executor.close_menu" "$RunId.choose.$choiceId"
        $executeRequest.profession_choice_id = $choiceId
        $executeRequest.profession_choice_source = "runtime_all_vanilla_professions_matrix"
        $result = Invoke-JsonPost $executeUrl $executeRequest
        $after = Wait-Snapshot $snapshotUrl 30
        $skill = [math]::Floor($choiceId / 6)
        $level = if (($choiceId % 6) -lt 2) { 5 } else { 10 }
        $professionIds = @($after.state.player.skills_detail.value.profession_ids | ForEach-Object { [int]$_ })
        $pendingLevel = @($after.state.player.skills_detail.value.new_levels | Where-Object { [int]$_.skill_index -eq $skill -and [int]$_.level -eq $level }).Count -gt 0
        $expectedHealthDelta = if ($choiceId -eq 24) { 15 } elseif ($choiceId -eq 27) { 25 } else { 0 }
        $maxHealthAfter = [int]$after.state.player.max_health.value
        $passed = $result.status -eq "applied" -and
            $result.primitive_verification_status -eq "verified" -and
            $choiceId -in $professionIds -and
            -not $pendingLevel -and
            -not $after.state.menus.active_menu.value.is_open -and
            $maxHealthAfter -eq ($maxHealthBefore + $expectedHealthDelta)
        $row = [ordered]@{
            profession_choice_id = $choiceId
            skill_index = $skill
            level = $level
            offered_ids = $offeredIds
            status = if ($passed) { "passed" } else { "failed" }
            execution_status = $result.status
            verification = $result.primitive_verification_status
            persistent_profession_present = $choiceId -in $professionIds
            pending_level_removed = -not $pendingLevel
            menu_closed = -not $after.state.menus.active_menu.value.is_open
            max_health_before = $maxHealthBefore
            max_health_after = $maxHealthAfter
            expected_health_delta = $expectedHealthDelta
        }
        $rows += $row
        Write-Json (Join-Path $runDirectory ("case-{0:D2}.json" -f $choiceId)) ([ordered]@{ fixture = $fixture; ready_menu = $menuState; result = $result; after_skills = $after.state.player.skills_detail; summary = $row })
        if (-not $passed) { throw "Profession choice $choiceId failed runtime verification." }
        $snapshot = $after
    }

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_profession_choice_smoke.v1"
        run_id = $RunId
        status = if (@($rows | Where-Object { $_.status -ne "passed" }).Count -eq 0 -and $rows.Count -eq $ProfessionChoiceIds.Count) { "passed" } else { "failed" }
        passed_cases = @($rows | Where-Object { $_.status -eq "passed" }).Count
        total_cases = $rows.Count
        loaded_mod_allowlist = $loadedModAllowlist
        smoke_mods_path = $smokeModsPath
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
