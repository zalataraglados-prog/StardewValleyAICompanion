[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory)]
    [string] $ProductionInputManifestPath,
    [string] $RunId =
        ("money-crop-native-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-money-crop-native-production",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $SkipBuild,
    [switch] $SkipDeploy
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeMoneyCropNative.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeMoneyCropSeedAcquisition.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeMoneyCropProductionInput.ps1")
. (Join-Path $PSScriptRoot `
    "lib\RuntimeMoneyCropEvidenceFinalization.ps1")

function Copy-RuntimeSnapshotArtifact {
    param(
        [Parameter(Mandatory)] [string] $SourcePath,
        [Parameter(Mandatory)] [string] $DestinationPath
    )
    Write-Utf8Text -Path $DestinationPath -Value (
        Get-Content -LiteralPath $SourcePath -Raw)
    return [IO.Path]::GetFullPath($DestinationPath)
}

$productionInput = Read-MoneyCropNativeProductionInputManifest `
    -ProjectRoot $ProjectRoot -Path $ProductionInputManifestPath
$productionInputManifest = $productionInput.Manifest
$productionInputManifestPath = $productionInput.ManifestPath
$productionInputManifestSha256 = $productionInput.ManifestSha256
$sourceSave = $productionInput.SourceSavePath
$sourceSlotName = Split-Path -Leaf $sourceSave
$outputRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputDirectory
$runDirectory = Join-Path $outputRoot $RunId
$isolatedSaves = Join-Path $runDirectory "isolated-saves"
$isolatedSlot = Join-Path $isolatedSaves $sourceSlotName
$isolatedMods = Join-Path $runDirectory "smapi-mods"
$artifactDirectory = Join-Path $runDirectory "transitions"
$evidenceDirectory = Join-Path $runDirectory "evidence"
$sitePreparationDirectory = Join-Path $evidenceDirectory "site-preparation"
$runPlanPath = Join-Path $runDirectory "crop-native-run-plan.json"
$fragmentIndexPath = Join-Path $evidenceDirectory "fragment-index.json"
$checkpointRequestPath = Join-Path $evidenceDirectory `
    "checkpoint-request.json"
$checkpointPath = Join-Path $evidenceDirectory "checkpoint.json"
$sourcePreflightPath = Join-Path $evidenceDirectory "source-preflight.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$failurePath = Join-Path $runDirectory "failure.json"

$runtimeGameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiPath = Join-Path $runtimeGameDirectory "StardewModdingAPI.exe"
$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$backendUrl = "http://127.0.0.1:$BackendPort"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl = "http://127.0.0.1:8767"
$backendStdout = Join-Path $runDirectory "backend.stdout.log"
$backendStderr = Join-Path $runDirectory "backend.stderr.log"
$gameStdout = Join-Path $runDirectory "game.stdout.log"
$gameStderr = Join-Path $runDirectory "game.stderr.log"

if (-not (Test-Path -LiteralPath $smapiPath -PathType Leaf)) {
    throw "Runtime SMAPI is missing: $smapiPath"
}
Assert-RuntimePortsUnused -Ports @($BackendPort, 8765, 8767) `
    -OperationName "Money crop native production"
if (Test-Path -LiteralPath $runDirectory) {
    throw "Money crop native run directory already exists: $runDirectory"
}

New-Item -ItemType Directory -Force -Path $isolatedSaves | Out-Null
New-Item -ItemType Directory -Force -Path $isolatedMods | Out-Null
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $evidenceDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $sitePreparationDirectory | Out-Null

if (-not $SkipBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj",
        "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
    )) {
        & dotnet build (Join-Path $ProjectRoot $project) `
            -c Release --no-restore --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed before Money crop native production: $project"
        }
    }
}
foreach ($required in @($backendDll, $loopDll, $bootstrapDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Release artifact is missing: $required"
    }
}

Copy-Item -LiteralPath $productionInput.RunPlanPath `
    -Destination $runPlanPath
Assert-RuntimeFileIdentity -Path $runPlanPath `
    -ExpectedSha256 ([string]$productionInputManifest.crop_native_run_plan_sha256) `
    -Label "Copied Money crop native run plan" | Out-Null
$runPlan = Get-Content -LiteralPath $runPlanPath -Raw | ConvertFrom-Json
Assert-MoneyCropNativeRunPlan -Plan $runPlan
$productionPlanPath = $productionInput.ObligationPlanPath
$productionPlan = $productionInput.ObligationPlan
$runPlanRequest = $productionInput.RunPlanRequest

$sourceSaveSha256Before = Get-DirectoryContentHash -Path $sourceSave
if ($sourceSaveSha256Before -ne
    [string]$productionInputManifest.source_save_tree_sha256) {
    throw "Money crop source save drifted after manifest validation."
}
Copy-Item -LiteralPath $sourceSave -Destination $isolatedSlot -Recurse
$isolatedSaveSha256Before = Get-DirectoryContentHash -Path $isolatedSlot
if ($sourceSaveSha256Before -ne $isolatedSaveSha256Before) {
    throw "Isolated crop source save does not equal its source."
}

if (-not $SkipDeploy) {
    $deployArguments = @{
        ProjectRoot = $ProjectRoot
        RuntimeRoot = $RuntimeRoot
    }
    if ($SkipBuild) { $deployArguments.NoBuild = $true }
    & (Join-Path $ProjectRoot `
        "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
        @deployArguments | Out-Null
    & (Join-Path $ProjectRoot `
        "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
        @deployArguments | Out-Null
}
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    $sourceMod = Join-Path $runtimeGameDirectory "Mods\$modName"
    if (-not (Test-Path -LiteralPath $sourceMod -PathType Container)) {
        throw "Required crop runtime Mod is missing: $sourceMod"
    }
    Copy-Item -LiteralPath $sourceMod `
        -Destination (Join-Path $isolatedMods $modName) -Recurse
}
$runtimeModAllowlist = @(Get-RuntimeExactModAllowlist `
    -ModsRoot $isolatedMods `
    -OperationName "Money crop runtime" `
    -ExpectedUniqueIds ([ordered]@{
        "StardewAI.RuntimeTestHarness" = "StardewAI.RuntimeTestHarness"
        "StardewAI.TransparentBridge" = "StardewAI.TransparentBridge"
    }))
$isolatedModsSha256Before = Get-DirectoryContentHash -Path $isolatedMods
$currentProductionInput = Read-MoneyCropNativeProductionInputManifest `
    -ProjectRoot $ProjectRoot -Path $productionInputManifestPath
if ($currentProductionInput.ManifestSha256 -ne
    $productionInputManifestSha256) {
    throw "Money crop production input manifest changed before process startup."
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH",
    "ASPNETCORE_URLS")
$savedEnvironment = Save-RuntimeProcessEnvironment -Names $environmentNames
$backendProcess = $null
$gameProcess = $null
$sourceSaveSha256After = "not_checked"

try {
    $env:STARDEWAI_TEST_SAVES = $isolatedSaves
    $env:STARDEWAI_TEST_SLOT = $sourceSlotName
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSaves
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $isolatedMods
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-RuntimeEvidenceProcess `
        -FilePath "dotnet" -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot `
        -StandardOutputPath $backendStdout `
        -StandardErrorPath $backendStderr
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null
    $gameProcess = Start-RuntimeEvidenceProcess `
        -FilePath $smapiPath -WorkingDirectory $runtimeGameDirectory `
        -StandardOutputPath $gameStdout `
        -StandardErrorPath $gameStderr
    Wait-Json -Url "$executorUrl/health" `
        -TimeoutSeconds $StartupTimeoutSeconds | Out-Null

    $initial = Get-FreshRuntimeSnapshot `
        -Url "$snapshotUrl&fresh=true" `
        -TimeoutSeconds $StartupTimeoutSeconds
    $initialPath = Join-Path $evidenceDirectory "initial-snapshot.json"
    Save-RuntimeSnapshotAndIngest -Path $initialPath -Capture $initial `
        -BackendUrl $backendUrl -SnapshotProfile "full"
    $initialDay = [int](Read-StateValue `
        $initial.Value "time" "total_days")
    $initialStateHash = [string]$initial.Value.state_hash
    $expectedSourceSnapshot = Get-Content `
        -LiteralPath $productionInput.SnapshotPath -Raw | ConvertFrom-Json
    $expectedSourceBoundaryHash = Get-MoneyCropSourceBoundaryHash `
        -Snapshot $expectedSourceSnapshot
    $observedSourceBoundaryHash = Get-MoneyCropSourceBoundaryHash `
        -Snapshot $initial.Value
    $sourceBoundaryMatches =
        $initialDay -eq [int]$runPlan.source_total_day -and
        $observedSourceBoundaryHash -eq $expectedSourceBoundaryHash
    Write-JsonFile -Path $sourcePreflightPath -Value ([ordered]@{
        schema_version = "money_crop_native_source_preflight.v2"
        status = if ($sourceBoundaryMatches) { "matched" } else { "rejected" }
        production_input_manifest_path = $productionInputManifestPath
        production_input_manifest_sha256 = $productionInputManifestSha256
        run_plan_path = $runPlanPath
        expected_source_state_hash = [string]$runPlan.source_state_hash
        observed_source_state_hash = $initialStateHash
        expected_source_boundary_hash = $expectedSourceBoundaryHash
        observed_source_boundary_hash = $observedSourceBoundaryHash
        boundary_hash_excluded_runtime_identity_fields = @(
            "environment.training_run_id",
            "environment.save_isolation_path"
        )
        boundary_hash_excluded_nonsemantic_fields = @(
            "*.read_at_tick",
            "*.machine_probe_cache_tick",
            "*.projection_tick",
            "player.movie_theater.projection_fingerprint"
        )
        expected_source_total_day = [int]$runPlan.source_total_day
        observed_source_total_day = $initialDay
        source_save_slot_path = $sourceSave
        source_save_sha256 = $sourceSaveSha256Before
        source_boundary_matches = $sourceBoundaryMatches
        formal_product_training_authorized = $false
    })
    if (-not $sourceBoundaryMatches) {
        throw "Isolated crop save does not match the exact run-plan source boundary."
    }

    $runtimeQueueContext = New-RuntimeQueueContext `
        -LoopDll $loopDll -ArtifactDirectory $artifactDirectory `
        -BackendUrl $backendUrl -SnapshotUrl $snapshotUrl `
        -SnapshotProfile "full" -ExecutorRoot $executorUrl `
        -ProductUrl "http://127.0.0.1:8768" -RunId $RunId `
        -IsolatedSavesPath $isolatedSaves
    $currentPath = $initialPath
    $transitions = [Collections.Generic.List[object]]::new()
    $seedAcquisitionSummaries = [Collections.Generic.List[object]]::new()
    $seedAcquisitionProofPaths = [Collections.Generic.List[string]]::new()
    $seedReservationProofPaths = [Collections.Generic.List[string]]::new()
    $previousSeedAcquisitionProofPath = ""
    $dailySnapshotPaths = [Collections.Generic.List[string]]::new()
    $wateringCanRefillReceipts = [Collections.Generic.List[object]]::new()
    $layoutSnapshotByCommitment = @{}
    $sitePreparationPlanPaths = [Collections.Generic.List[string]]::new()
    $sitePreparationActionCount = 0
    $proofFirstDay = [int](@($productionPlan.commitments |
        ForEach-Object { [int]$_.planting_total_day } |
        Measure-Object -Minimum).Minimum)
    $executedCropBatchCount = 0
    $skippedWaterBatchCount = 0

    foreach ($acquisition in @($runPlan.seed_acquisitions)) {
        $safeCommitmentId = Get-MoneyCropSafeArtifactName `
            -Value ([string]$acquisition.commitment_id)
        $result = Invoke-RuntimeCropSeedAcquisition `
            -Context $runtimeQueueContext -BackendUrl $backendUrl `
            -SnapshotProfile "full" -SourceSnapshotPath $currentPath `
            -Acquisition $acquisition `
            -Name "seed-acquisition-$safeCommitmentId"
        $currentPath = $result.SourcePath
        $acquisitionTransitions = @($result.Executions | ForEach-Object {
            New-RuntimeCropTransition `
                -TransitionKind "seed_acquisition_stage" `
                -TotalDay $initialDay -Artifacts $_ -RunId $RunId
        })
        if ($acquisitionTransitions.Count -eq 0) {
            throw "Crop seed acquisition $safeCommitmentId emitted no native transitions."
        }
        $acquisitionManifestPath = Join-Path $evidenceDirectory `
            "seed-acquisition-execution-$safeCommitmentId.json"
        $acquisitionRequestPath = Join-Path $evidenceDirectory `
            "seed-acquisition-request-$safeCommitmentId.json"
        $acquisitionProofPath = Join-Path $evidenceDirectory `
            "seed-acquisition-proof-$safeCommitmentId.json"
        Write-JsonFile -Path $acquisitionManifestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_seed_acquisition_execution_manifest.v1"
            crop_production_plan_path = $productionPlanPath
            crop_production_plan_sha256 =
                (Get-RuntimeFileSha256 -Path $productionPlanPath)
            commitment_id = [string]$acquisition.commitment_id
            source_state_hash =
                [string]$result.Executions[0].Before.state_hash
            run_id = $RunId
            transitions = $acquisitionTransitions
            formal_product_training_authorized = $false
        })
        Write-JsonFile -Path $acquisitionRequestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_seed_acquisition_proof_request.v2"
            crop_production_plan_path = $productionPlanPath
            commitment_id = [string]$acquisition.commitment_id
            execution_manifest_path = $acquisitionManifestPath
            prior_seed_acquisition_proof_path =
                $previousSeedAcquisitionProofPath
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-revenue-crop-seed-acquisition-proof",
            "--request", $acquisitionRequestPath,
            "--output", $acquisitionProofPath)
        $seedAcquisitionProofPaths.Add($acquisitionProofPath)
        $previousSeedAcquisitionProofPath = $acquisitionProofPath
        $seedAcquisitionSummaries.Add([ordered]@{
            commitment_id = [string]$acquisition.commitment_id
            seed_qualified_item_id =
                [string]$acquisition.seed_qualified_item_id
            required_quantity = [int]$result.RequiredQuantity
            initial_quantity = [int]$result.InitialQuantity
            final_quantity = [int]$result.FinalQuantity
            purchase_slice_count = [int]$result.PurchaseSliceCount
            execution_count = @($result.Executions).Count
            final_snapshot_path = [string]$result.SourcePath
            proof_path = $acquisitionProofPath
        })
    }

    foreach ($commitment in @($productionPlan.commitments)) {
        $reservationProofPath = New-MoneyCropSeedReservationProof `
            -BootstrapDll $bootstrapDll `
            -ProductionPlanPath $productionPlanPath `
            -CommitmentId ([string]$commitment.commitment_id) `
            -ReservationSnapshotPath $currentPath `
            -EvidenceDirectory $evidenceDirectory
        $seedReservationProofPaths.Add($reservationProofPath)
    }

    foreach ($day in @($runPlan.days)) {
        $totalDay = [int]$day.total_day
        $current = Import-RuntimeSnapshotFile -Path $currentPath `
            -BackendUrl $backendUrl -SnapshotProfile "full"
        if ([int](Read-StateValue $current.Value "time" "total_days") -ne
            $totalDay) {
            throw "Crop runtime day boundary drifted before day $totalDay."
        }
        $visitCropLocation = [bool]$day.capture_daily_capacity_snapshot -or
            @($day.action_batches).Count -gt 0
        if ($visitCropLocation) {
            $route = Invoke-RuntimeCropRoute -Context $runtimeQueueContext `
                -BackendUrl $backendUrl -SnapshotProfile "full" `
                -SourceSnapshotPath $currentPath `
                -TargetLocation ([string]$runPlan.location_id) `
                -TotalDay $totalDay -Name "day-$totalDay-crop-ingress"
            foreach ($routeArtifact in @($route.Artifacts)) {
                if ($totalDay -lt $proofFirstDay -or
                    @($day.capture_layout_commitment_ids).Count -gt 0) {
                    continue
                }
                $transitions.Add((New-RuntimeCropTransition `
                    -TransitionKind "supporting_route_to_crop" `
                    -TotalDay $totalDay -Artifacts $routeArtifact `
                    -RunId $RunId))
            }
            $currentPath = $route.SourcePath
        }

        foreach ($commitmentId in @($day.capture_layout_commitment_ids)) {
            $layoutIntents = @($runPlanRequest.layout_intents | Where-Object {
                [string]$_.commitment_id -eq [string]$commitmentId
            })
            if ($layoutIntents.Count -ne 1) {
                throw "Expected one layout intent for planting commitment $commitmentId."
            }
            $preparation = Invoke-RuntimeCropSitePreparation `
                -Context $runtimeQueueContext -BackendUrl $backendUrl `
                -SnapshotRootUrl "http://127.0.0.1:8765" `
                -BootstrapDll $bootstrapDll `
                -ProductionPlanPath $productionPlanPath `
                -SourceSnapshotPath $currentPath `
                -LayoutIntent $layoutIntents[0] -TotalDay $totalDay `
                -OutputDirectory $sitePreparationDirectory
            $currentPath = $preparation.SourcePath
            foreach ($planPath in @($preparation.PlanPaths)) {
                $sitePreparationPlanPaths.Add([string]$planPath)
            }
            $sitePreparationActionCount += @($preparation.Executions).Count
        }

        if ([bool]$day.capture_daily_capacity_snapshot) {
            $dailyPath = Join-Path $evidenceDirectory `
                "daily-capacity-day-$totalDay.json"
            $dailySnapshotPaths.Add((Copy-RuntimeSnapshotArtifact `
                -SourcePath $currentPath -DestinationPath $dailyPath))
        }
        foreach ($commitmentId in @($day.capture_layout_commitment_ids)) {
            $safeId = Get-MoneyCropSafeArtifactName `
                -Value ([string]$commitmentId)
            $layoutPath = Join-Path $evidenceDirectory `
                "layout-snapshot-$safeId.json"
            $layoutSnapshotByCommitment[[string]$commitmentId] =
                Copy-RuntimeSnapshotArtifact -SourcePath $currentPath `
                    -DestinationPath $layoutPath
        }

        foreach ($batch in @($day.action_batches)) {
            $batchName = "day-$totalDay-batch-$([int]$batch.batch_index)"
            $result = Invoke-RuntimeCropActionBatch `
                -Context $runtimeQueueContext -BackendUrl $backendUrl `
                -SnapshotProfile "full" -SourceSnapshotPath $currentPath `
                -Batch $batch -TotalDay $totalDay -Name $batchName
            if ($result.Skipped) {
                $skippedWaterBatchCount++
                continue
            }
            $transitions.Add((New-RuntimeCropTransition `
                -TransitionKind "crop_actions" -TotalDay $totalDay `
                -Artifacts $result.Artifacts -RunId $RunId))
            $refillSteps = @($result.Artifacts.Execution.step_results |
                Where-Object {
                    [string]$_.option_id -eq "executor.water_crop" -and
                    [string]$_.status -eq "applied" -and
                    [string]$_.primitive_verification_status -eq "verified" -and
                    $null -ne $_.water_before -and
                    [int]$_.water_before -eq 0 -and
                    $null -ne $_.water_after -and
                    [int]$_.water_after -gt 0
                })
            if ($refillSteps.Count -gt 0) {
                $wateringCanRefillReceipts.Add([ordered]@{
                    total_day = $totalDay
                    execution_receipt_path =
                        [string]$result.Artifacts.ExecutionPath
                })
            }
            $currentPath = $result.SourcePath
            $executedCropBatchCount++
        }

        if ([bool]$day.native_sleep_required) {
            $current = Import-RuntimeSnapshotFile -Path $currentPath `
                -BackendUrl $backendUrl -SnapshotProfile "full"
            $homeLocation = Get-RuntimeCropHomeLocation `
                -Snapshot $current.Value
            $homeRoute = Invoke-RuntimeCropRoute `
                -Context $runtimeQueueContext -BackendUrl $backendUrl `
                -SnapshotProfile "full" -SourceSnapshotPath $currentPath `
                -TargetLocation $homeLocation -TotalDay $totalDay `
                -Name "day-$totalDay-sleep-ingress"
            foreach ($routeArtifact in @($homeRoute.Artifacts)) {
                if ($totalDay -lt $proofFirstDay) { continue }
                $transitions.Add((New-RuntimeCropTransition `
                    -TransitionKind "supporting_route_to_sleep" `
                    -TotalDay $totalDay -Artifacts $routeArtifact `
                    -RunId $RunId))
            }
            $currentPath = $homeRoute.SourcePath
            $sleep = Invoke-RuntimeDailyPlanStep `
                -Context $runtimeQueueContext `
                -Name "day-$totalDay-native-rollover" `
                -OptionId "recovery.stabilize_day" `
                -CandidateKind "recovery_sleep_immediately" `
                -CandidateId "recovery:native_save_boundary" `
                -CandidateParameters @(
                    "control_plane.native_save_boundary=true") `
                -SourceSnapshotPath $currentPath
            $sleepItems = @($sleep.Queue.items)
            if ($sleepItems.Count -ne 1 -or
                [string]$sleepItems[0].option_id -ne "executor.sleep") {
                throw "Day $totalDay did not compile one native sleep queue."
            }
            if ($totalDay -ge $proofFirstDay) {
                $transitions.Add((New-RuntimeCropTransition `
                    -TransitionKind "native_day_rollover" `
                    -TotalDay $totalDay -Artifacts $sleep -RunId $RunId))
            }
            $currentPath = $sleep.AfterPath
        }
    }

    $layoutProofPaths = [Collections.Generic.List[string]]::new()
    $layoutProofByCommitment = @{}
    foreach ($intent in @($runPlanRequest.layout_intents)) {
        $commitmentId = [string]$intent.commitment_id
        if (-not $layoutSnapshotByCommitment.ContainsKey($commitmentId)) {
            throw "Missing planting boundary for commitment $commitmentId."
        }
        $safeId = Get-MoneyCropSafeArtifactName -Value $commitmentId
        $layoutRequestPath = Join-Path $evidenceDirectory `
            "layout-request-$safeId.json"
        $layoutProofPath = Join-Path $evidenceDirectory `
            "layout-proof-$safeId.json"
        Write-JsonFile -Path $layoutRequestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_planting_layout_proof_request.v1"
            crop_production_plan_path = $productionPlanPath
            commitment_id = $commitmentId
            planting_snapshot_path =
                $layoutSnapshotByCommitment[$commitmentId]
            location_id = [string]$intent.location_id
            tiles = @($intent.tiles)
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-revenue-crop-planting-layout-proof",
            "--request", $layoutRequestPath,
            "--output", $layoutProofPath)
        $layoutProofPaths.Add($layoutProofPath)
        $layoutProofByCommitment[$commitmentId] = $layoutProofPath
    }

    $capacityProofPaths = [Collections.Generic.List[string]]::new()
    $lifecycleProofPaths = [Collections.Generic.List[string]]::new()
    $retentionProofPaths = [Collections.Generic.List[string]]::new()
    foreach ($commitment in @($productionPlan.commitments)) {
        $commitmentId = [string]$commitment.commitment_id
        $safeId = Get-MoneyCropSafeArtifactName -Value $commitmentId
        $capacityRequestPath = Join-Path $evidenceDirectory `
            "capacity-request-$safeId.json"
        $capacityProofPath = Join-Path $evidenceDirectory `
            "capacity-proof-$safeId.json"
        Write-JsonFile -Path $capacityRequestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_daily_capacity_proof_request.v1"
            crop_production_plan_path = $productionPlanPath
            commitment_id = $commitmentId
            planting_layout_proof_paths = @($layoutProofPaths)
            daily_snapshot_paths = @($dailySnapshotPaths)
            watering_can_refill_receipts = @($wateringCanRefillReceipts)
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-revenue-crop-daily-capacity-proof",
            "--request", $capacityRequestPath,
            "--output", $capacityProofPath)
        $capacityProofPaths.Add($capacityProofPath)

        $lifecycleRequestPath = Join-Path $evidenceDirectory `
            "lifecycle-request-$safeId.json"
        $lifecycleProofPath = Join-Path $evidenceDirectory `
            "lifecycle-proof-$safeId.json"
        Write-JsonFile -Path $lifecycleRequestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_native_lifecycle_proof_request.v4"
            crop_production_plan_path = $productionPlanPath
            commitment_id = $commitmentId
            planting_layout_proof_path =
                $layoutProofByCommitment[$commitmentId]
            daily_capacity_proof_path = $capacityProofPath
            transitions = @($transitions)
            checkpoint_resumes = @()
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-revenue-crop-native-lifecycle-proof",
            "--request", $lifecycleRequestPath,
            "--output", $lifecycleProofPath)
        $lifecycleProofPaths.Add($lifecycleProofPath)

        $commitmentRetentionPaths = @(New-MoneyCropOutputRetentionProofs `
            -BootstrapDll $bootstrapDll `
            -ProductionPlanPath $productionPlanPath `
            -Commitment $commitment `
            -LifecycleProofPath $lifecycleProofPath `
            -EvidenceDirectory $evidenceDirectory)
        foreach ($path in $commitmentRetentionPaths) {
            $retentionProofPaths.Add([string]$path)
        }
    }

    $checkpointResult = Complete-MoneyCropProductionProofCheckpoint `
        -BootstrapDll $bootstrapDll `
        -ProductionPlanPath $productionPlanPath `
        -EvidenceDirectory $evidenceDirectory `
        -PlantingLayoutProofPaths $layoutProofPaths.ToArray() `
        -SeedReservationProofPaths $seedReservationProofPaths.ToArray() `
        -SeedAcquisitionProofPaths $seedAcquisitionProofPaths.ToArray() `
        -DailyCapacityProofPaths $capacityProofPaths.ToArray() `
        -NativeLifecycleProofPaths $lifecycleProofPaths.ToArray() `
        -OutputRetentionProofPaths $retentionProofPaths.ToArray()
    $fragmentIndexPath = $checkpointResult.FragmentIndexPath
    $checkpointRequestPath = $checkpointResult.CheckpointRequestPath
    $checkpointPath = $checkpointResult.CheckpointPath
    $checkpoint = $checkpointResult.Checkpoint

    $sourceSaveSha256After = Get-DirectoryContentHash -Path $sourceSave
    if ($sourceSaveSha256After -ne $sourceSaveSha256Before) {
        throw "Source save changed during isolated crop native production."
    }
    $isolatedModsSha256After = Get-DirectoryContentHash -Path $isolatedMods
    if ($isolatedModsSha256After -ne $isolatedModsSha256Before) {
        throw "Isolated runtime Mods changed during crop native production."
    }
    $currentProductionInput = Read-MoneyCropNativeProductionInputManifest `
        -ProjectRoot $ProjectRoot -Path $productionInputManifestPath
    if ($currentProductionInput.ManifestSha256 -ne
        $productionInputManifestSha256) {
        throw "Money crop production input manifest changed during native production."
    }
    $summary = [ordered]@{
        schema_version = "money_crop_native_production_run_summary.v1"
        status = "passed"
        run_id = $RunId
        production_input_manifest_path = $productionInputManifestPath
        production_input_manifest_sha256 = $productionInputManifestSha256
        run_plan_path = $runPlanPath
        run_plan_sha256 = Get-RuntimeFileSha256 -Path $runPlanPath
        crop_production_plan_path = $productionPlanPath
        source_save_slot_path = $sourceSave
        source_save_sha256_before = $sourceSaveSha256Before
        source_save_sha256_after = $sourceSaveSha256After
        source_preflight_path = $sourcePreflightPath
        isolated_save_slot_path = $isolatedSlot
        isolated_save_sha256_before = $isolatedSaveSha256Before
        isolated_mods_root_path = $isolatedMods
        isolated_mods_sha256_before = $isolatedModsSha256Before
        isolated_mods_sha256_after = $isolatedModsSha256After
        runtime_mod_allowlist = $runtimeModAllowlist
        transition_count = $transitions.Count
        executed_crop_batch_count = $executedCropBatchCount
        skipped_water_batch_count = $skippedWaterBatchCount
        seed_acquisition_count = $seedAcquisitionSummaries.Count
        seed_acquisition_proof_count = $seedAcquisitionProofPaths.Count
        seed_acquisition_proof_paths = @($seedAcquisitionProofPaths)
        seed_reservation_proof_count = $seedReservationProofPaths.Count
        seed_reservation_proof_paths = @($seedReservationProofPaths)
        seed_acquisitions = $seedAcquisitionSummaries.ToArray()
        site_preparation_plan_count = $sitePreparationPlanPaths.Count
        site_preparation_action_count = $sitePreparationActionCount
        site_preparation_plan_paths = @($sitePreparationPlanPaths)
        planting_layout_proof_count = $layoutProofPaths.Count
        daily_capacity_proof_count = $capacityProofPaths.Count
        watering_can_refill_receipt_count =
            $wateringCanRefillReceipts.Count
        native_lifecycle_proof_count = $lifecycleProofPaths.Count
        output_retention_proof_count = $retentionProofPaths.Count
        checkpoint_path = $checkpointPath
        checkpoint_verified_fragment_count =
            [int]$checkpoint.verified_fragment_count
        checkpoint_pending_request_count =
            [int]$checkpoint.pending_request_count
        formal_product_training_authorized = $false
    }
    Write-JsonFile -Path $summaryPath -Value $summary
    $summary | ConvertTo-Json -Depth 32
}
catch {
    $failureMessage = $_.Exception.Message
    try {
        $sourceSaveSha256After = Get-DirectoryContentHash -Path $sourceSave
    }
    catch {
        $sourceSaveSha256After = "hash_failed"
    }
    Write-JsonFile -Path $failurePath -Value ([ordered]@{
        schema_version = "money_crop_native_production_run_failure.v1"
        status = "failed"
        run_id = $RunId
        error = $failureMessage
        production_input_manifest_path = $productionInputManifestPath
        production_input_manifest_sha256 = $productionInputManifestSha256
        source_save_slot_path = $sourceSave
        source_save_sha256_before = $sourceSaveSha256Before
        source_save_sha256_after = $sourceSaveSha256After
        source_save_unchanged =
            $sourceSaveSha256After -eq $sourceSaveSha256Before
        formal_product_training_authorized = $false
    })
    throw
}
finally {
    Stop-RuntimeEvidenceProcesses -Processes @($gameProcess, $backendProcess)
    Restore-RuntimeProcessEnvironment -Values $savedEnvironment
}
