[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $RunId =
        ("runtime-crop-teacher-first-day-" +
            (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot =
        "artifacts\runtime-crop-teacher-first-day",
    [int] $BackendPort = 8795,
    [int] $StartupTimeoutSeconds = 300,
    [int] $RunTimeoutSeconds = 1800,
    [ValidateRange(1, 128)]
    [int] $SaveBoundaryMaxAttempts = 32,
    [switch] $FullCycle,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

$resolvedOutputRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputRoot
$runDirectory = Join-Path $resolvedOutputRoot $RunId
$trainingRoot = Join-Path $runDirectory "training"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorUrl = "http://127.0.0.1:8767"
$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$loopStdout = Join-Path $runDirectory "loop.stdout.log"
$loopStderr = Join-Path $runDirectory "loop.stderr.log"
$summaryFileName = if ($FullCycle) {
    "crop-teacher-full-cycle-summary.json"
}
else {
    "crop-teacher-first-day-summary.json"
}
$summaryPath = Join-Path $runDirectory $summaryFileName

Assert-RuntimePortsUnused -Ports @($BackendPort, 8765, 8767) `
    -OperationName "Crop teacher first-day smoke"
if (Test-Path -LiteralPath $runDirectory) {
    throw "Crop teacher first-day run directory already exists: $runDirectory"
}

if (-not $SkipBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"
    )) {
        & dotnet build (Join-Path $ProjectRoot $project) `
            -c Release --no-restore --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed before crop teacher smoke: $project"
        }
    }
}
foreach ($required in @($backendDll, $loopDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Release artifact is missing: $required"
    }
}

$existingGameIds = @(Get-Process -Name "StardewModdingAPI" `
    -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$gameProcess = $null
$backendProcess = $null
$loopProcess = $null
$savedEnvironment = Save-RuntimeProcessEnvironment `
    -Names @("ASPNETCORE_URLS")

try {
    & (Join-Path $ProjectRoot "scripts\Invoke-RuntimeStarterGiftSmoke.ps1") `
        -ProjectRoot $ProjectRoot `
        -RuntimeRoot $RuntimeRoot `
        -RunId $RunId `
        -OutputRoot $resolvedOutputRoot `
        -StartupTimeoutSeconds $StartupTimeoutSeconds `
        -KeepGameRunning | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Starter gift prerequisite failed with exit code $LASTEXITCODE."
    }

    $newGames = @(Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue | Where-Object {
            $existingGameIds -notcontains $_.Id
        })
    if ($newGames.Count -ne 1) {
        throw "Expected one owned fresh-save game; found $($newGames.Count)."
    }
    $gameProcess = $newGames[0]

    $starterSummaryPath = Join-Path $runDirectory `
        "starter-gift-summary.json"
    $starterSummary = Get-Content -LiteralPath $starterSummaryPath -Raw |
        ConvertFrom-Json
    if ([string]$starterSummary.status -ne "passed") {
        throw "Starter gift prerequisite did not pass."
    }
    $isolatedSaves = [IO.Path]::GetFullPath(
        [string]$starterSummary.isolated_save_root)
    $saveSlot = [string]$starterSummary.native_save_slot
    Assert-RuntimeSourceSaveSlot -Path (
        Join-Path $isolatedSaves $saveSlot) | Out-Null

    $initial = Get-FreshRuntimeSnapshot `
        -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $initialTotalDay = [int](Read-StateValue `
        $initial.Value "time" "total_days")
    $initialStateHash = [string]$initial.Value.state_hash

    $env:ASPNETCORE_URLS = $backendUrl
    $backendProcess = Start-RuntimeEvidenceProcess `
        -FilePath "dotnet" -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot `
        -StandardOutputPath $backendStdout `
        -StandardErrorPath $backendStderr
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $loopArguments = @(
        $loopDll,
        "--root", $trainingRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorUrl,
        "--use-runtime-test-harness-executor",
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSaves,
        "--save-slot", $saveSlot,
        "--goal", "goal.grandpa_max_score_year3",
        "--max-attempts", "3",
        "--required-verified-actions", "1",
        "--require-native-save-boundary",
        "--save-boundary-max-attempts", $SaveBoundaryMaxAttempts,
        "--skip-training",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "4",
        "--continue-after-blocked-queue-items",
        "--execution-snapshot-profile", "full",
        "--snapshot-artifact-mode", "content_addressed_gzip",
        "--artifact-retention-mode", "rolling",
        "--max-persisted-iterations", "256",
        "--max-consecutive-errors", "3",
        "--executor-timeout-seconds", "600",
        "--sleep-ms", "500",
        "--after-snapshot-wait-ms", "1500",
        "--after-snapshot-poll-ms", "100",
        "--max-unverified-execution-streak", "3"
    )
    if (-not $FullCycle) {
        $loopArguments += "--stop-after-crop-teacher-first-day"
    }
    $loopProcess = Start-RuntimeEvidenceProcess `
        -FilePath "dotnet" -ArgumentList $loopArguments `
        -WorkingDirectory $ProjectRoot `
        -StandardOutputPath $loopStdout `
        -StandardErrorPath $loopStderr
    if (-not $loopProcess.WaitForExit($RunTimeoutSeconds * 1000)) {
        throw "Crop teacher first-day LiveTrainingLoop timed out."
    }
    if ($loopProcess.ExitCode -ne 0) {
        $tail = if (Test-Path -LiteralPath $loopStderr -PathType Leaf) {
            @(Get-Content -LiteralPath $loopStderr -Tail 20) -join " | "
        }
        else {
            "stderr_missing"
        }
        throw "Crop teacher LiveTrainingLoop failed with exit code " +
            "$($loopProcess.ExitCode): $tail"
    }

    $loopReportPath = Join-Path (
        Join-Path $trainingRoot "runs\$RunId") `
        "live-training-loop-report.json"
    if (-not (Test-Path -LiteralPath $loopReportPath -PathType Leaf)) {
        throw "Crop teacher LiveTrainingLoop report is missing."
    }
    $loopReport = Get-Content -LiteralPath $loopReportPath -Raw |
        ConvertFrom-Json
    $cycleCompletionInvalid = if ($FullCycle) {
        -not [bool]$loopReport.crop_teacher_run_completed -or
        [bool]$loopReport.crop_teacher_replan_required -or
        [int]$loopReport.crop_teacher_harvest_receipt_count -lt 1 -or
        [int]$loopReport.crop_teacher_day_boundary_receipt_count -lt 1
    }
    else {
        -not [bool]$loopReport.crop_teacher_first_day_completed
    }
    if (-not [string]::IsNullOrWhiteSpace(
            [string]$loopReport.stop_reason) -or
        $cycleCompletionInvalid -or
        [int]$loopReport.crop_teacher_control_attempts_started -lt 1 -or
        [int]$loopReport.verified_actions -lt 1 -or
        -not [bool]$loopReport.native_save_boundary_required -or
        -not [bool]$loopReport.native_save_boundary_verified -or
        [string]::IsNullOrWhiteSpace(
            [string]$loopReport.planning_handoff_capture_manifest_path) -or
        [string]::IsNullOrWhiteSpace(
            [string]$loopReport.planning_handoff_teacher_dispatch_manifest_path) -or
        [string]::IsNullOrWhiteSpace(
            [string]$loopReport.planning_handoff_native_run_plan_path)) {
        throw "Crop teacher report did not close every required gate."
    }

    $after = Get-FreshRuntimeSnapshot `
        -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $afterTotalDay = [int](Read-StateValue `
        $after.Value "time" "total_days")
    $afterCrops = @(Read-StateValue $after.Value "farm" "crops")
    if ($afterTotalDay -le $initialTotalDay -or
        (-not $FullCycle -and $afterCrops.Count -lt 1)) {
        throw "Crop teacher after-state did not satisfy its cycle scope."
    }

    $summary = [ordered]@{
        schema_version = $(if ($FullCycle) {
            "stardewai.runtime_crop_teacher_full_cycle_smoke.v1"
        }
        else {
            "stardewai.runtime_crop_teacher_first_day_smoke.v1"
        })
        status = "passed"
        run_id = $RunId
        save_slot = $saveSlot
        isolated_save_root = $isolatedSaves
        initial_total_day = $initialTotalDay
        after_total_day = $afterTotalDay
        initial_state_hash = $initialStateHash
        after_state_hash = [string]$after.Value.state_hash
        after_crop_count = $afterCrops.Count
        crop_teacher_control_attempts_started =
            [int]$loopReport.crop_teacher_control_attempts_started
        crop_teacher_run_completed =
            [bool]$loopReport.crop_teacher_run_completed
        crop_teacher_replan_count =
            [int]$loopReport.crop_teacher_replan_count
        crop_teacher_harvest_receipt_count =
            [int]$loopReport.crop_teacher_harvest_receipt_count
        crop_teacher_day_boundary_receipt_count =
            [int]$loopReport.crop_teacher_day_boundary_receipt_count
        native_save_boundary_verified =
            [bool]$loopReport.native_save_boundary_verified
        planning_handoff_capture_manifest_path =
            [string]$loopReport.planning_handoff_capture_manifest_path
        planning_handoff_teacher_dispatch_manifest_path =
            [string]$loopReport.planning_handoff_teacher_dispatch_manifest_path
        planning_handoff_native_run_plan_path =
            [string]$loopReport.planning_handoff_native_run_plan_path
        live_training_loop_report_path = $loopReportPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness")
        formal_training_started = $false
    }
    Write-JsonFile -Path $summaryPath -Value $summary
    $summary | ConvertTo-Json -Depth 16
}
finally {
    Restore-RuntimeProcessEnvironment `
        -Values $savedEnvironment
    Stop-RuntimeEvidenceProcesses -Processes @(
        $loopProcess,
        $gameProcess,
        $backendProcess)
}
