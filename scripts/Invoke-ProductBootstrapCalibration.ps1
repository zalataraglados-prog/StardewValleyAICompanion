param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory = $true)]
    [string] $SavesPath,
    [Parameter(Mandatory = $true)]
    [string] $SaveSlot,
    [string] $OutputRoot = "E:\StardewAITraining\bootstrap-runs",
    [string] $RunId = ("product-bootstrap-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $MaxAttempts = 8,
    [ValidateRange(1, [int]::MaxValue)]
    [int] $RequiredVerifiedActions = 1,
    [ValidateRange(1, 128)]
    [int] $SaveBoundaryMaxAttempts = 16,
    [ValidateSet("stop", "rolling")]
    [string] $ArtifactRetentionMode = "stop",
    [ValidateRange(1, 1000000)]
    [int] $MaxPersistedIterations = 64,
    [ValidateRange(1, 64)]
    [int] $MaxUnverifiedExecutionStreak = 8,
    [ValidateRange(1, 64)]
    [int] $MaxQueueNoProgressStreak = 8,
    [ValidateRange(1024, [int]::MaxValue)]
    [int] $MinFreeSpaceMb = 8192,
    [switch] $CaptureGoldenWalnutFreshSaveEvidence,
    [switch] $RequireGoldenWalnutSourceScheduleReady,
    [string] $ResumeCropTeacherTransitionLedger = "",
    [switch] $ContinueAfterCropTeacherRun,
    [string] $StageOneCollectionRequirementInventory = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\authoritative-requirement-inventory-v1.json"),
    [string] $StageOneCollectionAcquisitionLowering = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\acquisition-route-option-lowering-v1.json"),
    [string] $MasterAnglerStageOneWindowIndex = (Join-Path `
        $ProjectRoot `
        "experiments\local-data\output\master-angler-stage-one-window-index-v1.json"),
    [string] $RouteTimingCalibration = `
        "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

if ($RequiredVerifiedActions -gt $MaxAttempts) {
    throw "RequiredVerifiedActions must not exceed MaxAttempts."
}
if ($RequireGoldenWalnutSourceScheduleReady -and
    -not $CaptureGoldenWalnutFreshSaveEvidence) {
    throw "RequireGoldenWalnutSourceScheduleReady requires CaptureGoldenWalnutFreshSaveEvidence."
}
$resumeCropTeacher = -not [string]::IsNullOrWhiteSpace(
    $ResumeCropTeacherTransitionLedger)
if ($resumeCropTeacher) {
    if ($CaptureGoldenWalnutFreshSaveEvidence -or
        $RequireGoldenWalnutSourceScheduleReady) {
        throw "Crop Teacher resume cannot be combined with fresh-save capture."
    }
    if (-not (Test-Path -LiteralPath `
            $ResumeCropTeacherTransitionLedger -PathType Leaf)) {
        throw "Crop Teacher transition ledger was not found: $ResumeCropTeacherTransitionLedger"
    }
    $ResumeCropTeacherTransitionLedger =
        [IO.Path]::GetFullPath($ResumeCropTeacherTransitionLedger)
}
if ($ContinueAfterCropTeacherRun -and -not $resumeCropTeacher) {
    throw "ContinueAfterCropTeacherRun requires ResumeCropTeacherTransitionLedger."
}
if ($RequireGoldenWalnutSourceScheduleReady -or
    $ContinueAfterCropTeacherRun) {
    foreach ($path in @(
            $StageOneCollectionRequirementInventory,
            $StageOneCollectionAcquisitionLowering,
            $MasterAnglerStageOneWindowIndex,
            $RouteTimingCalibration)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required Stage 1 Teacher authority artifact was not found: $path"
        }
    }
}

function Write-Json([string] $Path, $Value) {
    $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Wait-Json([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 15
            if ($null -ne $value) { return $value }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $last"
}

function Wait-World([string] $SnapshotUrl, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $SnapshotUrl -TimeoutSec 30
            $snapshot = $response.Content | ConvertFrom-Json
            if ($snapshot.state.player.location_id.status -in @("available", "derived") -and
                $snapshot.state.identity.save_id.status -in @("available", "derived")) {
                return [string] $response.Content
            }
        } catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for the isolated bootstrap save."
}

$savesFullPath = [IO.Path]::GetFullPath($SavesPath)
$saveDirectory = Join-Path $savesFullPath $SaveSlot
$saveFile = Join-Path $saveDirectory $SaveSlot
if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
    throw "Save slot file was not found: $saveFile"
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable was not found: $smapi"
}

$backendUrl = "http://127.0.0.1:$BackendPort"
$productUrl = "http://127.0.0.1:$ProductPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
foreach ($port in @(8765, 8767, $BackendPort, $ProductPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Bootstrap calibration requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $RunId
$journalRoot = Join-Path $artifactDirectory "product-journal"
$trainingRoot = Join-Path $artifactDirectory "training"
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null

$binaryRoot = Join-Path $artifactDirectory "binaries"
$releaseEntrypoints = @(
    [pscustomobject]@{
        Project = Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"
        OutputDirectory = Join-Path $binaryRoot "backend"
        ArtifactsDirectory = Join-Path $binaryRoot "backend-build"
        DllName = "StardewAI.Backend.dll"
    },
    [pscustomobject]@{
        Project = Join-Path $ProjectRoot "tools\StardewAI.ProductExecutor\StardewAI.ProductExecutor.csproj"
        OutputDirectory = Join-Path $binaryRoot "product-executor"
        ArtifactsDirectory = Join-Path $binaryRoot "product-executor-build"
        DllName = "StardewAI.ProductExecutor.dll"
    },
    [pscustomobject]@{
        Project = Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"
        OutputDirectory = Join-Path $binaryRoot "live-training-loop"
        ArtifactsDirectory = Join-Path $binaryRoot "live-training-loop-build"
        DllName = "StardewAI.LiveTrainingLoop.dll"
    }
)
foreach ($entry in $releaseEntrypoints) {
    $publishLog = Join-Path $artifactDirectory (
        "publish-" + [IO.Path]::GetFileNameWithoutExtension($entry.Project) + ".log")
    $publishOutput = & dotnet publish $entry.Project -c Release --nologo `
        --artifacts-path $entry.ArtifactsDirectory --output $entry.OutputDirectory `
        "-p:GamePath=$gameDirectory" 2>&1
    $publishOutput | Set-Content -LiteralPath $publishLog -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Required isolated Release publish failed for $($entry.Project) with exit code $LASTEXITCODE. See $publishLog."
    }
}

$backendDll = Join-Path $releaseEntrypoints[0].OutputDirectory $releaseEntrypoints[0].DllName
$productDll = Join-Path $releaseEntrypoints[1].OutputDirectory $releaseEntrypoints[1].DllName
$loopDll = Join-Path $releaseEntrypoints[2].OutputDirectory $releaseEntrypoints[2].DllName
foreach ($path in @($backendDll, $productDll, $loopDll)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Release binary was not found: $path"
    }
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR", "STARDEWAI_PRODUCT_EXECUTOR_URL",
    "STARDEWAI_NATIVE_EXECUTOR_URL", "STARDEWAI_BRIDGE_SNAPSHOT_URL",
    "STARDEWAI_PRODUCT_JOURNAL_ROOT", "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT",
    "STARDEWAI_PRODUCT_RUN_ID", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$game = $null
$backend = $null
$product = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesFullPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesFullPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"

    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @($backendDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = "http://127.0.0.1:8767"
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = $journalRoot
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $savesFullPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $RunId
    $product = Start-Process dotnet -ArgumentList @($productDll) -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "product.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "product.stderr.log") -PassThru
    $productHealth = Wait-Json "$productUrl/health" 60
    if ($productHealth.status -ne "ready" -or [int]$productHealth.product_executor_count -le 0) {
        throw "Product executor health contract failed."
    }

    $game = Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "http://127.0.0.1:8767/health" $StartupTimeoutSeconds | Out-Null
    $initialJson = Wait-World $snapshotUrl $StartupTimeoutSeconds
    [IO.File]::WriteAllText(
        (Join-Path $artifactDirectory "initial-snapshot.json"),
        $initialJson,
        [Text.UTF8Encoding]::new($false))
    $initial = $initialJson | ConvertFrom-Json

    $arguments = @(
        $loopDll,
        "--root", $trainingRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $productUrl,
        "--use-product-executor",
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $savesFullPath,
        "--goal", "goal.grandpa_max_score_year3",
        "--max-attempts", $MaxAttempts,
        "--required-verified-actions", $RequiredVerifiedActions,
        "--skip-training",
        "--use-daily-plan",
        "--daily-plan-max-candidates", 4,
        "--continue-after-blocked-queue-items",
        "--execution-snapshot-profile", "full",
        "--artifact-retention-mode", $ArtifactRetentionMode,
        "--max-persisted-iterations", $MaxPersistedIterations,
        "--max-unverified-execution-streak", $MaxUnverifiedExecutionStreak,
        "--max-queue-no-progress-streak", $MaxQueueNoProgressStreak,
        "--min-free-space-mb", $MinFreeSpaceMb,
        "--sleep-ms", 1000,
        "--after-snapshot-wait-ms", 500
    )
    if ($CaptureGoldenWalnutFreshSaveEvidence) {
        $arguments += @(
            "--snapshot-artifact-mode", "content_addressed_gzip",
            "--capture-golden-walnut-fresh-save-evidence",
            "--require-native-save-boundary",
            "--save-slot", $SaveSlot,
            "--save-boundary-max-attempts", $SaveBoundaryMaxAttempts
        )
    }
    if ($RequireGoldenWalnutSourceScheduleReady) {
        $arguments += "--require-golden-walnut-source-schedule-ready"
    }
    if ($RequireGoldenWalnutSourceScheduleReady -or
        $ContinueAfterCropTeacherRun) {
        $arguments += @(
            "--stage-one-collection-requirement-inventory",
            $StageOneCollectionRequirementInventory,
            "--stage-one-collection-acquisition-lowering",
            $StageOneCollectionAcquisitionLowering,
            "--master-angler-stage-one-window-index",
            $MasterAnglerStageOneWindowIndex,
            "--route-timing-calibration",
            $RouteTimingCalibration
        )
    }
    if ($resumeCropTeacher) {
        $arguments += @(
            "--resume-crop-teacher-transition-ledger",
            $ResumeCropTeacherTransitionLedger
        )
    }
    if ($ContinueAfterCropTeacherRun) {
        $arguments += "--continue-after-crop-teacher-run"
    }
    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $artifactDirectory "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Product bootstrap LiveTrainingLoop failed with exit code $LASTEXITCODE."
    }

    $trajectoryPath = Join-Path $trainingRoot "datasets\policy-decision-trajectories.jsonl"
    $trajectoryExists = Test-Path -LiteralPath $trajectoryPath -PathType Leaf
    if (-not $trajectoryExists -and
        -not $CaptureGoldenWalnutFreshSaveEvidence -and
        -not $resumeCropTeacher) {
        throw "Bootstrap calibration produced no policy trajectory file."
    }
    $trajectories = if ($trajectoryExists) {
        @(Get-Content -LiteralPath $trajectoryPath |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_ | ConvertFrom-Json })
    }
    else {
        @()
    }
    $accepted = @($trajectories | Where-Object {
        $_.schema_version -eq "policy_decision_trajectory.v2" -and
        $_.versions.executor -eq "product_executor.v1" -and
        $_.outcome.status -eq "applied" -and
        $_.outcome.success -eq $true -and
        $_.outcome.after_snapshot_fresh -eq $true -and
        @($_.candidates | Where-Object { $_.available -and $_.admitted_for_policy }).Count -ge 2
    })
    if ($accepted.Count -lt 1 -and
        -not $CaptureGoldenWalnutFreshSaveEvidence -and
        -not $resumeCropTeacher) {
        throw "Bootstrap calibration produced no Product trajectory with at least two admitted candidates."
    }

    $runDirectory = Join-Path (Join-Path $trainingRoot "runs") $RunId
    $loopReportPath = Join-Path $runDirectory "live-training-loop-report.json"
    if (-not (Test-Path -LiteralPath $loopReportPath -PathType Leaf)) {
        throw "Bootstrap calibration produced no LiveTrainingLoop report: $loopReportPath"
    }
    $loopReport = Get-Content -LiteralPath $loopReportPath -Raw |
        ConvertFrom-Json
    if ($CaptureGoldenWalnutFreshSaveEvidence -and
        (-not [bool]$loopReport.golden_walnut_fresh_save_evidence_capture_enabled -or
         [int]$loopReport.required_verified_actions -ne $RequiredVerifiedActions -or
         [int]$loopReport.verified_actions -lt $RequiredVerifiedActions -or
         [int]$loopReport.golden_walnut_fresh_save_episode_count -lt $RequiredVerifiedActions -or
         -not [bool]$loopReport.native_save_boundary_required -or
         -not [bool]$loopReport.native_save_boundary_verified -or
         [string]::IsNullOrWhiteSpace(
             [string]$loopReport.golden_walnut_fresh_save_episode_manifest_path) -or
         -not (Test-Path -LiteralPath (
             [string]$loopReport.golden_walnut_fresh_save_episode_manifest_path) `
             -PathType Leaf))) {
        throw "Golden Walnut fresh-save evidence capture produced no durable episode manifest."
    }
    if ($RequireGoldenWalnutSourceScheduleReady -and
        (-not [bool]$loopReport.golden_walnut_source_milestone_ready -or
         -not [bool]$loopReport.golden_walnut_source_schedule_verified -or
         [string]::IsNullOrWhiteSpace(
             [string]$loopReport.golden_walnut_source_schedule_path) -or
         -not (Test-Path -LiteralPath (
             [string]$loopReport.golden_walnut_source_schedule_path) `
             -PathType Leaf))) {
        throw "Golden Walnut long-run milestone did not produce a verified source schedule."
    }
    if ($resumeCropTeacher -and
        (-not [bool]$loopReport.crop_teacher_run_completed -or
         [string]::IsNullOrWhiteSpace(
             [string]$loopReport.crop_teacher_transition_ledger_path) -or
         -not (Test-Path -LiteralPath (
             [string]$loopReport.crop_teacher_transition_ledger_path) `
             -PathType Leaf) -or
         [IO.Path]::GetFullPath(
             [string]$loopReport.crop_teacher_transition_ledger_path) -eq
            $ResumeCropTeacherTransitionLedger)) {
        throw "Crop Teacher resume did not complete its bound native run."
    }

    $summary = [ordered]@{
        schema_version = "stardewai.product_bootstrap_calibration.v1"
        status = "passed"
        run_id = $RunId
        save_id = [string]$initial.state.identity.save_id.value
        year = [int]$initial.state.time.year.value
        season = [string]$initial.state.time.season.value
        day = [int]$initial.state.time.day.value
        trajectory_path = $trajectoryPath
        trajectory_count = $trajectories.Count
        accepted_multi_candidate_count = $accepted.Count
        selected_option_id = if ($accepted.Count -gt 0) {
            [string]$accepted[0].selection.option_id
        } else { "" }
        admitted_candidate_count = if ($accepted.Count -gt 0) {
            @($accepted[0].candidates | Where-Object {
                $_.available -and $_.admitted_for_policy }).Count
        } else { 0 }
        product_executor_count = [int]$productHealth.product_executor_count
        live_training_loop_report_path = $loopReportPath
        crop_teacher_resume_enabled = $resumeCropTeacher
        crop_teacher_resume_source_ledger_path = if ($resumeCropTeacher) {
            $ResumeCropTeacherTransitionLedger
        } else { "" }
        crop_teacher_transition_ledger_path =
            [string]$loopReport.crop_teacher_transition_ledger_path
        crop_teacher_run_completed =
            [bool]$loopReport.crop_teacher_run_completed
        golden_walnut_fresh_save_evidence_capture_enabled =
            [bool]$loopReport.golden_walnut_fresh_save_evidence_capture_enabled
        golden_walnut_fresh_save_episode_manifest_path =
            [string]$loopReport.golden_walnut_fresh_save_episode_manifest_path
        golden_walnut_fresh_save_episode_count =
            [int]$loopReport.golden_walnut_fresh_save_episode_count
        golden_walnut_source_schedule_required =
            [bool]$loopReport.golden_walnut_source_schedule_required
        golden_walnut_source_milestone_ready =
            [bool]$loopReport.golden_walnut_source_milestone_ready
        golden_walnut_source_current_total_day =
            [int]$loopReport.golden_walnut_source_current_total_day
        golden_walnut_deadline_total_day_exclusive =
            [int]$loopReport.golden_walnut_deadline_total_day_exclusive
        golden_walnut_source_schedule_verified =
            [bool]$loopReport.golden_walnut_source_schedule_verified
        golden_walnut_source_schedule_path =
            [string]$loopReport.golden_walnut_source_schedule_path
        max_attempts = $MaxAttempts
        verified_actions = [int]$loopReport.verified_actions
        required_verified_actions = $RequiredVerifiedActions
        save_boundary_max_attempts = $SaveBoundaryMaxAttempts
        native_save_boundary_verified =
            [bool]$loopReport.native_save_boundary_verified
        formal_training_started = $false
    }
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    foreach ($process in @($game, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
