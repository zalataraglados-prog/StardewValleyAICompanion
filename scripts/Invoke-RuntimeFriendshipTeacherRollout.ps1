[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-friendship-teacher-rollout-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $RouteTimingCalibration =
        "artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [ValidateRange(1, 7)]
    [int] $MaxDayTransitions = 1,
    [ValidateRange(1, 16)]
    [int] $MaxObjectivesPerDay = 2,
    [ValidateRange(1, 16)]
    [int] $MaxNoProgressDays = 3,
    [ValidateRange(1, 65535)]
    [int] $BackendPort = 5159,
    [ValidateRange(30, 600)]
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] $Value
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
    $Value | ConvertTo-Json -Depth 64 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-SaveFingerprint {
    param([Parameter(Mandatory = $true)] [string] $Path)

    $resolvedRoot = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    $rows = Get-ChildItem -LiteralPath $Path -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
            if (-not $fullPath.StartsWith(
                    $resolvedRoot,
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "Save fingerprint escaped its source root: $fullPath"
            }
            $relativePath = $fullPath.Substring($resolvedRoot.Length)
            $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
            $relativePath + "|" + $hash
        }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Wait-JsonEndpoint {
    param(
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds,
        [scriptblock] $Accept = { param($Value) $null -ne $Value }
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 15
            if (& $Accept $value) {
                return $value
            }
            $lastError = "endpoint response did not satisfy admission predicate"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Assert-PortFree {
    param([int[]] $Ports)

    foreach ($port in $Ports) {
        if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port `
                -ErrorAction SilentlyContinue)) {
            throw "Port $port is already listening; refusing to attach to an existing process."
        }
    }
}

function Invoke-Build {
    param([string] $Project, [string] $GamePath = "")

    $arguments = @("build", $Project, "-c", "Release", "--nologo")
    if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
        $arguments += "-p:GamePath=$GamePath"
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE for $Project."
    }
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesRoot = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=social_future&fresh=true"
$loopSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=social&fresh=true"
$executorUrl = "http://127.0.0.1:8767"
$backendUrl = "http://127.0.0.1:$BackendPort"
$calibrationPath = if ([System.IO.Path]::IsPathRooted($RouteTimingCalibration)) {
    [System.IO.Path]::GetFullPath($RouteTimingCalibration)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $RouteTimingCalibration))
}

foreach ($requiredPath in @(
        $ProjectRoot,
        $RuntimeRoot,
        $gameDirectory,
        $sourceSavesRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) {
        throw "Required directory is missing: $requiredPath"
    }
}
foreach ($requiredFile in @($smapiExecutable, $calibrationPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file is missing: $requiredFile"
    }
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $sourceSave = Get-ChildItem -LiteralPath $sourceSavesRoot -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $sourceSave) {
        throw "No isolated source save exists under $sourceSavesRoot."
    }
    $SaveSlot = $sourceSave.Name
}
$sourceSavePath = Join-Path $sourceSavesRoot $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Requested source save slot is missing: $sourceSavePath"
}

Assert-PortFree -Ports @(8765, 8767, $BackendPort)
$existingGame = Get-Process -Name @("StardewModdingAPI", "Stardew Valley") `
    -ErrorAction SilentlyContinue
if ($null -ne $existingGame) {
    throw "A Stardew process is already running; refusing to attach to or stop it."
}

$runDirectory = Join-Path $ProjectRoot `
    ("artifacts\runtime-friendship-teacher-rollout\" + $RunId)
$clonedSavesRoot = Join-Path $runDirectory "isolated-saves"
$clonedSavePath = Join-Path $clonedSavesRoot $SaveSlot
$coordinatorOutput = Join-Path $runDirectory "coordinator"
$trainingOutput = Join-Path $runDirectory "runtime-output"
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
$launcherSummaryPath = Join-Path $runDirectory "launcher-summary.json"
if ((Test-Path -LiteralPath $runDirectory) -or
    (Test-Path -LiteralPath $smokeModsPath)) {
    throw "RunId already exists; refusing to overwrite prior evidence: $RunId"
}

$sourceFingerprintBefore = Get-SaveFingerprint -Path $sourceSavePath
New-Item -ItemType Directory -Force -Path $clonedSavesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
New-Item -ItemType Directory -Force -Path $coordinatorOutput | Out-Null
New-Item -ItemType Directory -Force -Path $trainingOutput | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $clonedSavePath -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @(
        "StardewAI.TransparentBridge",
        "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}

$backendProject = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\StardewAI.Backend.csproj"
$liveLoopProject = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"
$coordinatorProject = Join-Path $ProjectRoot `
    "tools\StardewAI.FriendshipTeacherRollout\StardewAI.FriendshipTeacherRollout.csproj"
Invoke-Build -Project $backendProject
Invoke-Build -Project $liveLoopProject
Invoke-Build -Project $coordinatorProject

$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$liveLoopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$coordinatorDll = Join-Path $ProjectRoot `
    "tools\StardewAI.FriendshipTeacherRollout\bin\Release\net8.0\StardewAI.FriendshipTeacherRollout.dll"
foreach ($requiredFile in @($backendDll, $liveLoopDll, $coordinatorDll)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Built runtime assembly is missing: $requiredFile"
    }
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
    "SMAPI_MODS_PATH",
    "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name, "Process")
}

$gameProcess = $null
$backendProcess = $null
$launcherError = $null
try {
    $env:STARDEWAI_TEST_SAVES = $clonedSavesRoot
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $clonedSavesRoot
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutput
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $runDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $runDirectory "backend.stderr.log") `
        -PassThru
    Wait-JsonEndpoint -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    Wait-JsonEndpoint -Url "$executorUrl/health" `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    $initialSnapshot = Wait-JsonEndpoint -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds -Accept {
            param($snapshot)
            $snapshot.save_id.status -in @("available", "derived") -and
                $snapshot.state.time.total_days.status -in @("available", "derived") -and
                $snapshot.state.npcs.grandpa_friendship_progress.status -in
                    @("available", "derived") -and
                $snapshot.state.npcs.grandpa_friendship_progress.value.projection_status -eq
                    "complete_live_native_iteration"
        }
    $initialTotalDays = [int]$initialSnapshot.state.time.total_days.value
    $deadlineTotalDaysExclusive = $initialTotalDays + $MaxDayTransitions + 1

    $coordinatorArguments = @(
        $coordinatorDll,
        "--project-root", $ProjectRoot,
        "--output-root", $coordinatorOutput,
        "--run-id", $RunId,
        "--backend-url", $backendUrl,
        "--snapshot-url", $snapshotUrl,
        "--loop-snapshot-url", $loopSnapshotUrl,
        "--executor-url", $executorUrl,
        "--save-isolation-path", $clonedSavesRoot,
        "--save-slot", $SaveSlot,
        "--calibration", $calibrationPath,
        "--live-training-loop-dll", $liveLoopDll,
        "--deadline-total-days-exclusive", $deadlineTotalDaysExclusive,
        "--max-day-transitions", $MaxDayTransitions,
        "--max-objectives-per-day", $MaxObjectivesPerDay,
        "--max-no-progress-days", $MaxNoProgressDays
    )
    & dotnet @coordinatorArguments `
        1> (Join-Path $runDirectory "coordinator.stdout.log") `
        2> (Join-Path $runDirectory "coordinator.stderr.log")
    $coordinatorExitCode = $LASTEXITCODE
    $coordinatorSummaryPath = Join-Path $coordinatorOutput "summary.json"
    if (-not (Test-Path -LiteralPath $coordinatorSummaryPath -PathType Leaf)) {
        throw "Friendship teacher coordinator did not write summary.json."
    }
    $coordinatorSummary = Get-Content -LiteralPath $coordinatorSummaryPath -Raw |
        ConvertFrom-Json
    if ($coordinatorExitCode -ne 0 -or
        $coordinatorSummary.status -notin @(
            "bounded_evidence_complete",
            "goal_satisfied")) {
        throw "Friendship teacher coordinator was blocked; inspect $coordinatorSummaryPath."
    }

    $sourceFingerprintAfter = Get-SaveFingerprint -Path $sourceSavePath
    $sourceSaveUntouched = $sourceFingerprintAfter -eq $sourceFingerprintBefore
    if (-not $sourceSaveUntouched) {
        throw "The persistent source-save tree changed during the isolated rollout."
    }
    $launcherSummary = [ordered]@{
        schema_version = "stardewai.runtime_friendship_teacher_rollout_launcher.v1"
        status = "passed"
        run_id = $RunId
        admission_scope = "bounded_teacher_rollout_not_formal_training"
        deadline_mode = "bounded_calibration_not_grandpa_deadline_proof"
        initial_total_days = $initialTotalDays
        deadline_total_days_exclusive = $deadlineTotalDaysExclusive
        source_save_path = $sourceSavePath
        source_save_tree_sha256_before = $sourceFingerprintBefore
        source_save_tree_sha256_after = $sourceFingerprintAfter
        source_save_untouched = $sourceSaveUntouched
        cloned_save_path = $clonedSavePath
        snapshot_profile = "social_future"
        action_loop_snapshot_profile = "social"
        snapshot_force_fresh = $true
        formal_training_started = $false
        max_day_transitions = $MaxDayTransitions
        max_objectives_per_day = $MaxObjectivesPerDay
        coordinator_status = [string]$coordinatorSummary.status
        objective_count = [int]$coordinatorSummary.objectiveCount
        day_transition_count = [int]$coordinatorSummary.dayTransitionCount
        coordinator_summary_path = $coordinatorSummaryPath
        smoke_mods_path = $smokeModsPath
        backend_process_id = $backendProcess.Id
        game_process_id = $gameProcess.Id
    }
    Write-JsonFile -Path $launcherSummaryPath -Value $launcherSummary
    $launcherSummary | ConvertTo-Json -Depth 64
}
catch {
    $launcherError = $_
    $sourceFingerprintAfter = Get-SaveFingerprint -Path $sourceSavePath
    Write-JsonFile -Path (Join-Path $runDirectory "error-summary.json") -Value `
        ([ordered]@{
            status = "error"
            run_id = $RunId
            error = $_.Exception.Message
            error_type = $_.Exception.GetType().Name
            source_save_tree_sha256_before = $sourceFingerprintBefore
            source_save_tree_sha256_after = $sourceFingerprintAfter
            source_save_untouched =
                ($sourceFingerprintAfter -eq $sourceFingerprintBefore)
            formal_training_started = $false
            timestamp = [DateTimeOffset]::UtcNow.ToString("O")
        })
}
finally {
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $backendProcess -and -not $backendProcess.HasExited) {
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
}

if ($null -ne $launcherError) {
    throw $launcherError
}
