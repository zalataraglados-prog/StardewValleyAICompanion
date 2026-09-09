[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "",
    [string]$RunId = ("runtime-friendship-day-transition-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Wait-FullSnapshot {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [Nullable[int]]$ExpectedTotalDays = $null
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 10
            $snapshot = $response.Content | ConvertFrom-Json
            $progress = $snapshot.state.npcs.grandpa_friendship_progress
            $totalDays = [int]$snapshot.state.time.total_days.value
            $time = [int]$snapshot.state.time.time.value
            $lastStatus = "save=$($snapshot.save_id.status);progress=$($progress.status);days=$totalDays;time=$time"
            $expectedDayMatches = -not $ExpectedTotalDays.HasValue -or `
                $totalDays -eq $ExpectedTotalDays.Value
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $progress.status -in @("available", "derived") -and
                $progress.value.projection_status -eq "complete_live_native_iteration" -and
                $progress.value.day_transition_inputs_status -eq "complete_live_native_fields" -and
                $expectedDayMatches) {
                return [pscustomobject]@{
                    Raw = $response.Content
                    Parsed = $snapshot
                }
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for a full friendship snapshot. Last status: $lastStatus"
}

function New-ExecutionRequest {
    param(
        [string]$OptionId,
        [string]$QueueItemId,
        [string]$StateHash,
        [string]$IsolationPath
    )

    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-friendship-day-transition-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $IsolationPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-Execution {
    param([string]$Url, $Request, [int]$TimeoutSeconds)

    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Request | ConvertTo-Json -Depth 32) -TimeoutSec $TimeoutSeconds
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesRoot = Join-Path $RuntimeRoot "saves"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=true"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $sourceSavesRoot -PathType Container)) {
    throw "Isolated source saves directory not found: $sourceSavesRoot"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $sourceSave = Get-ChildItem -LiteralPath $sourceSavesRoot -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $sourceSave) {
        throw "No isolated source save exists under $sourceSavesRoot"
    }
    $SaveSlot = $sourceSave.Name
}
$sourceSavePath = Join-Path $sourceSavesRoot $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Requested isolated source save slot not found: $sourceSavePath"
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach to an existing runtime."
    }
}
$existingGame = Get-Process -Name @("StardewModdingAPI", "Stardew Valley") `
    -ErrorAction SilentlyContinue
if ($null -ne $existingGame) {
    throw "A Stardew process is already running. Refusing to attach or stop it."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-friendship-day-transition-smoke\" + $RunId)
$clonedSavesRoot = Join-Path $runDirectory "isolated-saves"
$clonedSavePath = Join-Path $clonedSavesRoot $SaveSlot
$beforePath = Join-Path $runDirectory "before-full-snapshot.json"
$afterPath = Join-Path $runDirectory "after-full-snapshot.json"
$preparePath = Join-Path $runDirectory "prepare-sleep-result.json"
$sleepPath = Join-Path $runDirectory "sleep-result.json"
$auditPath = Join-Path $runDirectory "friendship-day-transition-audit.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $clonedSavesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $clonedSavePath -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}
& dotnet build $bootstrapProject --no-restore --nologo `
    "-p:GamePath=$gameDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Goal-conditioned bootstrap build failed with exit code $LASTEXITCODE."
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $clonedSavesRoot
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $clonedSavesRoot
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $prepare = Invoke-Execution -Url $executorUrl -TimeoutSeconds 60 -Request `
        (New-ExecutionRequest "debug.prepare_partnership_sleep" "$RunId.prepare" `
            ([string]$initial.Parsed.state_hash) $clonedSavesRoot)
    Write-Utf8NoBom -Path $preparePath -Content ($prepare | ConvertTo-Json -Depth 32)
    if ($prepare.status -ne "applied" -or $prepare.primitive_verification_status -ne "verified") {
        throw "Native sleep preparation failed: $(@($prepare.block_reasons) -join ',')"
    }

    $before = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 30
    Write-Utf8NoBom -Path $beforePath -Content $before.Raw
    $beforeDays = [int]$before.Parsed.state.time.total_days.value
    $sleep = Invoke-Execution -Url $executorUrl -TimeoutSeconds 180 -Request `
        (New-ExecutionRequest "executor.sleep" "$RunId.sleep" `
            ([string]$before.Parsed.state_hash) $clonedSavesRoot)
    Write-Utf8NoBom -Path $sleepPath -Content ($sleep | ConvertTo-Json -Depth 32)
    $sleepBlockReasons = @($sleep.block_reasons)
    $expectedPostSleepDialogueBoundary = $sleep.status -eq "blocked" -and
        $sleepBlockReasons.Count -eq 1 -and
        [string]$sleepBlockReasons[0] -like "post_sleep_dialogue_unsafe:*" -and
        [string]$sleep.observed_effect -like "*total_days=$($beforeDays + 1)*"
    $sleepVerified = $sleep.status -eq "applied" -and
        $sleep.primitive_verification_status -eq "verified"
    if (-not $sleepVerified -and -not $expectedPostSleepDialogueBoundary) {
        throw "Native sleep failed: $(@($sleep.block_reasons) -join ',')"
    }

    $after = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 90 `
        -ExpectedTotalDays ($beforeDays + 1)
    Write-Utf8NoBom -Path $afterPath -Content $after.Raw

    & dotnet run --project $bootstrapProject --no-build -- `
        audit-friendship-day-transition --before $beforePath --after $afterPath --output $auditPath
    if ($LASTEXITCODE -ne 0) {
        throw "Friendship day-transition audit failed with exit code $LASTEXITCODE."
    }
    $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
    $passed = $audit.status -eq "pass" -and
        [int]$audit.verifiedNpcCount -gt 0 -and
        [int]$audit.mismatchCount -eq 0
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_friendship_day_transition_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        source_save_path = $sourceSavePath
        cloned_save_path = $clonedSavePath
        total_days_before = [int]$audit.beforeTotalDays
        total_days_after = [int]$audit.afterTotalDays
        native_population_rows_before = [int]$audit.nativePopulationRowsBefore
        native_population_rows_after = [int]$audit.nativePopulationRowsAfter
        unique_npc_count = [int]$audit.uniqueNpcCount
        verified_npc_count = [int]$audit.verifiedNpcCount
        verified_friendship_row_count = [int]$audit.verifiedFriendshipRowCount
        mismatch_count = [int]$audit.mismatchCount
        sleep_executor_status = [string]$sleep.status
        sleep_executor_verification_status = [string]$sleep.primitive_verification_status
        accepted_post_sleep_dialogue_boundary = $expectedPostSleepDialogueBoundary
        before_snapshot_path = $beforePath
        after_snapshot_path = $afterPath
        audit_path = $auditPath
        smoke_mods_path = $smokeModsPath
    }
    Write-Utf8NoBom -Path $summaryPath -Content ($summary | ConvertTo-Json -Depth 16)
    $summary | ConvertTo-Json -Depth 16
    if (-not $passed) {
        throw "Runtime friendship day-transition smoke failed: $runDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
