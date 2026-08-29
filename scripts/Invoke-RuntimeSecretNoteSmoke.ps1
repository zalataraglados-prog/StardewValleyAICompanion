param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-secret-note-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-secret-note-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") { return $response }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            if ($snapshot.save_id.status -in @("available", "derived")) { return $snapshot }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for a world snapshot"
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) { throw "No isolated save slots found under $savesPath" }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path (Join-Path $ProjectRoot $OutputDirectory) $RunId
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    $sourceMod = Join-Path (Join-Path $runtimeGameDir "Mods") $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod | Out-Null
    Copy-Item -Path (Join-Path $sourceMod "*") -Destination $targetMod -Recurse -Force
}

$previousEnv = @{}
foreach ($name in @("STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")) {
    $previousEnv[$name] = [Environment]::GetEnvironmentVariable($name)
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $runtimeGameDir -WindowStyle Hidden -PassThru
    $health = Wait-JsonHealth -Url "http://127.0.0.1:8767/health" -TimeoutSeconds 30
    $snapshot = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $cases = @(
        [ordered]@{ fixture_target_id = 0; selected_note_id = 0; qualified_item_id = "(O)79"; expected_quest_id = "" },
        [ordered]@{ fixture_target_id = 10; selected_note_id = 10; qualified_item_id = "(O)79"; expected_quest_id = "30" },
        [ordered]@{ fixture_target_id = 23; selected_note_id = 23; qualified_item_id = "(O)79"; expected_quest_id = "29" },
        [ordered]@{ fixture_target_id = 1001; selected_note_id = 1001; qualified_item_id = "(O)842"; expected_quest_id = "" }
    )
    $results = @()
    foreach ($case in $cases) {
        $caseName = if ([int]$case.fixture_target_id -eq 0) { "note-seeded-multiple" } else { "note-$($case.selected_note_id)" }
        $setup = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-secret-note-smoke"
            queue_item_id = "$caseName.setup"; before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_secret_note_fixture"; execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
            qualified_item_id = [string]$case.qualified_item_id; secret_note_fixture_target_id = [int]$case.fixture_target_id
        }
        $setupResult = Invoke-JsonPost -Url $executorUrl -Body $setup
        if ($setupResult.status -ne "applied") { throw "$caseName fixture failed: $(@($setupResult.block_reasons) -join ',')" }
        Start-Sleep -Milliseconds 250
        $before = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $context = $before.state.player.secret_note_candidates.value
        $note = @($context.rows | Where-Object {
            [bool]$_.available -and ([int]$case.selected_note_id -eq 0 -or [int]$_.selected_note_id -eq [int]$case.selected_note_id)
        })
        if ($note.Count -ne 1) { throw "$caseName expected one exact available projection, got $($note.Count)" }
        $note = $note[0]
        $read = [ordered]@{
            schema_version = "training_execution_request.v1"; run_id = $RunId; queue_id = "runtime-secret-note-smoke"
            queue_item_id = "$caseName.read"; before_state_hash = $before.state_hash
            option_id = "executor.read_secret_note"; execution_mode = "training_singleplayer"; actor = "training_farmer.main"
            save_isolation_path = $savesPath; request_nonce = [guid]::NewGuid().ToString("N"); created_at = [DateTimeOffset]::UtcNow.ToString("O")
            slot_index = [int]$note.slot_index; item_id = [string]$note.item_id; qualified_item_id = [string]$note.qualified_item_id
            secret_note_runtime_type = [string]$note.runtime_type; secret_note_stack_before = [int]$note.stack_before; secret_note_stack_after = [int]$note.stack_after
            secret_note_is_journal = [bool]$note.is_journal; secret_note_journal_index = [int]$note.journal_index
            secret_note_total_count = [int]$note.total_note_count; secret_note_unseen_ids_native_order_json = [string]$note.unseen_note_ids_native_order_json
            secret_note_unseen_count = [int]$note.unseen_note_count; secret_note_selection_kind = [string]$note.selection_kind
            secret_note_selected_id = [int]$note.selected_note_id; secret_note_content_sha256 = [string]$note.selected_note_content_sha256
            secret_note_display_kind = [string]$note.display_kind; secret_note_expected_image = [int]$note.expected_secret_note_image
            secret_note_expected_which_bg = [int]$note.expected_which_bg; secret_note_expected_quest_id = [string]$note.expected_quest_id
            secret_note_expected_quest_present_before = [bool]$note.expected_quest_present_before
            secret_note_expected_quest_present_after = [bool]$note.expected_quest_present_after
            secret_note_projection_fingerprint = [string]$context.projection_fingerprint; secret_note_native_contract = [string]$note.native_contract
        }
        $readResult = Invoke-JsonPost -Url $executorUrl -Body $read
        Start-Sleep -Milliseconds 250
        $after = Wait-WorldSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $actualSelectedNoteId = [int]$note.selected_note_id
        $seen = @($after.state.player.secret_note_candidates.value.seen_note_ids) -contains $actualSelectedNoteId
        $menu = $after.state.menus.menu_specific_state.value
        $passed = $readResult.status -eq "applied" -and $readResult.primitive_verification_status -eq "verified" -and $seen -and $menu.kind -eq "letter_viewer"
        $results += [pscustomobject][ordered]@{
            selected_note_id = $actualSelectedNoteId; qualified_item_id = [string]$case.qualified_item_id
            expected_quest_id = [string]$note.expected_quest_id; status = $readResult.status
            verification = $readResult.primitive_verification_status; note_seen = $seen
            menu_kind = $menu.kind; result = if ($passed) { "passed" } else { "failed" }
        }
        Write-JsonFile -Path (Join-Path $runDirectory "$caseName-before.json") -Value $before
        Write-JsonFile -Path (Join-Path $runDirectory "$caseName-read.json") -Value $readResult
        Write-JsonFile -Path (Join-Path $runDirectory "$caseName-after.json") -Value $after
        $snapshot = $after
    }
    $summary = [ordered]@{
        status = if (@($results | Where-Object { $_.result -ne "passed" }).Count -eq 0) { "passed" } else { "failed" }
        run_id = $RunId; save_slot = $SaveSlot; loaded_mod_allowlist = @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")
        cases = $results; executor_health = $health; smapi_process_id = $process.Id
    }
    Write-JsonFile -Path (Join-Path $runDirectory "summary.json") -Value $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") { throw "Runtime secret-note smoke failed. See $runDirectory" }
}
finally {
    foreach ($key in $previousEnv.Keys) { Set-Item -Path "env:$key" -Value $previousEnv[$key] }
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
