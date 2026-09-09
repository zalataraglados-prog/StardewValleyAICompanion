[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "",
    [string]$RunId = ("runtime-npc-schedule-projection-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$StartupTimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Wait-DayStartSnapshot {
    param([string]$Url, [int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -Headers @{ Accept = "application/json" } -TimeoutSec 5
            $snapshot = $response.Content | ConvertFrom-Json
            $catalog = $snapshot.state.npcs.schedule_catalog
            $schedules = $snapshot.state.npcs.schedules
            $lastStatus = "catalog=$($catalog.status);schedules=$($schedules.status);time=$($snapshot.state.time.time.value)"
            if ($catalog.status -in @("available", "derived") -and
                $schedules.status -in @("available", "derived") -and
                [int]$snapshot.state.time.time.value -eq 600 -and
                $catalog.value.current_selection_context.day_start_capture -eq $true) {
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
    throw "Timed out waiting for a 06:00 full-profile schedule snapshot. Last status: $lastStatus"
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$bootstrapProject = Join-Path $ProjectRoot "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=true"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves directory not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $save = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $save) {
        throw "No isolated save slot exists under $savesPath"
    }
    $SaveSlot = $save.Name
}
$savePath = Join-Path $savesPath $SaveSlot
if (-not (Test-Path -LiteralPath $savePath -PathType Container)) {
    throw "Requested isolated save slot not found: $savePath"
}
if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort 8765 -ErrorAction SilentlyContinue)) {
    throw "Port 8765 is already listening. Refusing to attach to an existing bridge."
}
$existingSmapi = Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue
if ($null -ne $existingSmapi) {
    throw "StardewModdingAPI is already running. Refusing to attach or stop an unrelated game."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-npc-schedule-projection-smoke\" + $RunId)
$snapshotPath = Join-Path $runDirectory "day-start-full-snapshot.json"
$auditPath = Join-Path $runDirectory "current-schedule-audit.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}
& dotnet build $bootstrapProject --no-restore --nologo
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
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $capture = Wait-DayStartSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    [System.IO.File]::WriteAllText(
        $snapshotPath,
        $capture.Raw,
        [System.Text.UTF8Encoding]::new($false))

    & dotnet run --project $bootstrapProject --no-build -- audit-current-schedules `
        --snapshot $snapshotPath --output $auditPath
    if ($LASTEXITCODE -ne 0) {
        throw "Current native schedule projection audit failed with exit code $LASTEXITCODE."
    }
    $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
    $passed = $audit.status -eq "pass" -and
        [int]$audit.verifiedScheduleCount -gt 0 -and
        [int]$audit.mismatchCount -eq 0
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_npc_schedule_projection_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_slot = $SaveSlot
        snapshot_profile = "full"
        game_time = [int]$audit.gameTime
        loaded_schedule_count = [int]$audit.loadedScheduleCount
        verified_schedule_count = [int]$audit.verifiedScheduleCount
        fail_closed_schedule_count = [int]$audit.failClosedScheduleCount
        mismatch_count = [int]$audit.mismatchCount
        snapshot_path = $snapshotPath
        audit_path = $auditPath
        smoke_mods_path = $smokeModsPath
    }
    $summary | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    $summary | ConvertTo-Json -Depth 16
    if (-not $passed) {
        throw "Runtime NPC schedule projection smoke failed: $runDirectory"
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
