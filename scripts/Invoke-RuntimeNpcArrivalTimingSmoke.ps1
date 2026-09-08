[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "",
    [string]$RunId = ("runtime-npc-arrival-timing-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Wait-FullSnapshot {
    param([string]$Url, [int]$TimeoutSeconds, [switch]$RequireArrivalFixture)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 10
            $snapshot = $response.Content | ConvertFrom-Json
            $catalog = $snapshot.state.npcs.schedule_catalog
            $schedules = $snapshot.state.npcs.schedules
            $maru = @($schedules.value | Where-Object { $_.name -eq "Maru" }) |
                Select-Object -First 1
            $lastStatus = "catalog=$($catalog.status);schedules=$($schedules.status);" +
                "day=$($catalog.value.current_selection_context.day_of_month);maru=$($maru.schedule_key)"
            $worldReady = $snapshot.save_id.status -in @("available", "derived") -and
                $catalog.status -in @("available", "derived") -and
                $schedules.status -in @("available", "derived")
            $fixtureReady = -not $RequireArrivalFixture -or
                ([int]$catalog.value.current_selection_context.day_of_month -eq 16 -and
                 [int]$catalog.value.current_selection_context.game_time -eq 600 -and
                 [string]$maru.schedule_key -eq "DesertFestival_2" -and
                 @($maru.entries | Where-Object {
                     $_.target_location_name -eq "Desert" -and
                     [int]$_.target_tile_x -eq 40 -and
                     [int]$_.target_tile_y -eq 41 -and
                     $null -ne $_.adjacent_route_pixel_distance
                 }).Count -eq 1)
            if ($worldReady -and $fixtureReady) {
                return [pscustomobject]@{ Raw = $response.Content; Parsed = $snapshot }
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for NPC arrival timing snapshot. Last status: $lastStatus"
}

function New-FixtureRequest([string]$StateHash, [string]$IsolationPath) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-npc-arrival-timing-smoke"
        queue_item_id = "$RunId.fixture"
        before_state_hash = $StateHash
        option_id = "debug.setup_schedule_arrival_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $IsolationPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
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
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $sourceSave = Get-ChildItem -LiteralPath $sourceSavesRoot -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $sourceSave) { throw "No isolated source save exists under $sourceSavesRoot" }
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
if ($null -ne (Get-Process -Name @("StardewModdingAPI", "Stardew Valley") -ErrorAction SilentlyContinue)) {
    throw "A Stardew process is already running. Refusing to attach or stop it."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-npc-arrival-timing-smoke\" + $RunId)
$clonedSavesRoot = Join-Path $runDirectory "isolated-saves"
$clonedSavePath = Join-Path $clonedSavesRoot $SaveSlot
$fixturePath = Join-Path $runDirectory "fixture-result.json"
$snapshotPath = Join-Path $runDirectory "arrival-full-snapshot.json"
$auditPath = Join-Path $runDirectory "current-schedule-arrival-audit.json"
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
& dotnet build $bootstrapProject --no-restore --nologo "-p:GamePath=$gameDirectory"
if ($LASTEXITCODE -ne 0) { throw "Goal-conditioned bootstrap build failed: $LASTEXITCODE" }

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH")
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
    $before = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $fixture = Invoke-RestMethod -Method Post -Uri $executorUrl `
        -ContentType "application/json; charset=utf-8" `
        -Body ((New-FixtureRequest ([string]$before.Parsed.state_hash) $clonedSavesRoot) |
            ConvertTo-Json -Depth 32) -TimeoutSec 120
    Write-Utf8NoBom $fixturePath ($fixture | ConvertTo-Json -Depth 32)
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Schedule arrival fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $capture = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 60 -RequireArrivalFixture
    Write-Utf8NoBom $snapshotPath $capture.Raw
    & dotnet run --project $bootstrapProject --no-build -- `
        audit-current-schedules --snapshot $snapshotPath --output $auditPath
    if ($LASTEXITCODE -ne 0) { throw "Current schedule arrival audit failed: $LASTEXITCODE" }
    $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
    $maru = @($audit.rows | Where-Object { $_.npcName -eq "Maru" }) | Select-Object -First 1
    $passed = $audit.status -eq "pass" -and
        [int]$audit.mismatchCount -eq 0 -and
        [int]$audit.verifiedTravelTimeEntryCount -eq
            (@($audit.rows | ForEach-Object { [int]$_.verifiedEntryCount }) |
                Measure-Object -Sum).Sum -and
        [int]$audit.verifiedArrivalTimeEntryCount -gt 0 -and
        $maru.status -eq "pass" -and
        [int]$maru.verifiedArrivalTimeEntryCount -eq 1
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_npc_arrival_timing_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        source_save_path = $sourceSavePath
        cloned_save_path = $clonedSavePath
        loaded_schedule_count = [int]$audit.loadedScheduleCount
        verified_schedule_count = [int]$audit.verifiedScheduleCount
        fail_closed_schedule_count = [int]$audit.failClosedScheduleCount
        mismatch_count = [int]$audit.mismatchCount
        verified_travel_time_entry_count = [int]$audit.verifiedTravelTimeEntryCount
        verified_arrival_time_entry_count = [int]$audit.verifiedArrivalTimeEntryCount
        maru_schedule_key = [string]$maru.nativeScheduleKey
        maru_arrival_time_entry_count = [int]$maru.verifiedArrivalTimeEntryCount
        fixture_path = $fixturePath
        snapshot_path = $snapshotPath
        audit_path = $auditPath
        smoke_mods_path = $smokeModsPath
    }
    Write-Utf8NoBom $summaryPath ($summary | ConvertTo-Json -Depth 16)
    $summary | ConvertTo-Json -Depth 16
    if (-not $passed) { throw "Runtime NPC arrival timing smoke failed: $runDirectory" }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
