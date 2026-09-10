[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory = $true)]
    [string] $SavesPath,
    [Parameter(Mandatory = $true)]
    [string] $SaveSlot,
    [string] $OutputRoot = "E:\StardewAITraining\teacher-product-runs",
    [string] $RunId = ("stage-one-collection-teacher-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $RequirementInventory = (Join-Path $ProjectRoot `
        "experiments\local-data\output\authoritative-requirement-inventory-v1.json"),
    [string] $AcquisitionLowering = (Join-Path $ProjectRoot `
        "experiments\local-data\output\acquisition-route-option-lowering-v1.json"),
    [string] $MasterAnglerWindows = (Join-Path $ProjectRoot `
        "experiments\local-data\output\master-angler-stage-one-window-index-v1.json"),
    [string] $RouteTimingCalibration = `
        "I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json",
    [string] $Goal = "grandpa_max_score_year3",
    [string] $KnowledgeDictionaryVersion =
        "game-1.6.15-20260723T093543Z-linux-v24",
    [int] $BackendPort = 8795,
    [int] $ProductPort = 8768,
    [int] $StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

function Write-Utf8Json([string] $Path, [string] $Json) {
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    [IO.File]::WriteAllText(
        $Path,
        $Json,
        [Text.UTF8Encoding]::new($false))
}

function Wait-Json([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    $last = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -TimeoutSec 15
            if ($response.StatusCode -eq 200) {
                return $response.Content | ConvertFrom-Json
            }
        } catch {
            $last = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $last"
}

function Wait-World([string] $SnapshotUrl, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $SnapshotUrl `
                -TimeoutSec 30
            $snapshot = $response.Content | ConvertFrom-Json
            if ($snapshot.state.player.location_id.status -in `
                    @("available", "derived") -and
                $snapshot.state.identity.save_id.status -in `
                    @("available", "derived")) {
                return $response.Content
            }
        } catch {}
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for the isolated Teacher save."
}

function Invoke-Bootstrap([string[]] $Arguments) {
    & dotnet $script:bootstrapDll @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap failed: $($Arguments[0])"
    }
}

function New-TeacherPreferenceArtifacts(
    [string] $Suffix,
    [string] $SnapshotUrl,
    [string] $BackendUrl,
    [string] $ArtifactDirectory,
    [string] $TrainingRoot,
    [string] $GoalId,
    [string] $RequirementInventoryPath,
    [string] $AcquisitionLoweringPath,
    [string] $MasterAnglerWindowsPath,
    [string] $RouteTimingCalibrationPath,
    [int] $TimeoutSeconds
) {
    $snapshotJson = Wait-World $SnapshotUrl $TimeoutSeconds
    $beforeSnapshotPath = Join-Path $ArtifactDirectory `
        ("before-snapshot" + $Suffix + ".json")
    Write-Utf8Json $beforeSnapshotPath $snapshotJson

    $ingestResponse = Invoke-WebRequest -UseBasicParsing -Method Post `
        -Uri "$BackendUrl/api/v1/snapshots?profile=full" `
        -ContentType "application/json" -Body $snapshotJson -TimeoutSec 60
    $stateHash = [string](($ingestResponse.Content | ConvertFrom-Json).state_hash)
    if ([string]::IsNullOrWhiteSpace($stateHash)) {
        throw "Backend ingest returned no state hash."
    }

    $rankRequest = [ordered]@{
        goal_id = $GoalId
        execution_mode = "training_singleplayer"
        policy_checkpoint_path = $null
        require_structured_policy = $false
        dataset_path = Join-Path $TrainingRoot `
            "datasets\live-training-feature-rows.jsonl"
        state_hash = $stateHash
        candidate_option_ids = @()
        candidates = @()
        include_blocked_options = $false
    } | ConvertTo-Json -Depth 16
    $rankResponse = Invoke-WebRequest -UseBasicParsing -Method Post `
        -Uri "$BackendUrl/api/v1/planner/baseline/rank-options" `
        -ContentType "application/json" -Body $rankRequest -TimeoutSec 120
    $rankingPath = Join-Path $ArtifactDirectory `
        ("ranking" + $Suffix + ".json")
    Write-Utf8Json $rankingPath $rankResponse.Content

    $intentsPath = Join-Path $ArtifactDirectory `
        ("master-angler-target-date-intents" + $Suffix + ".json")
    Invoke-Bootstrap @(
        "build-master-angler-target-date-intents",
        "--windows", $MasterAnglerWindowsPath,
        "--snapshot", $beforeSnapshotPath,
        "--timing-calibration", $RouteTimingCalibrationPath,
        "--output", $intentsPath
    )
    $preferencePath = Join-Path $ArtifactDirectory `
        ("teacher-preference" + $Suffix + ".json")
    Invoke-Bootstrap @(
        "build-current-stage-one-collection-teacher-preference",
        "--requirement-inventory", $RequirementInventoryPath,
        "--acquisition-lowering", $AcquisitionLoweringPath,
        "--ranking", $rankingPath,
        "--snapshot", $beforeSnapshotPath,
        "--master-angler-target-date-intents", $intentsPath,
        "--output", $preferencePath
    )
    $preference = Get-Content -LiteralPath $preferencePath -Raw |
        ConvertFrom-Json
    if ($preference.status -ne "ready" -or
        -not [bool]$preference.teacher_preference_label_eligible) {
        throw "Teacher preference is not dispatchable: " +
            (@($preference.blocking_reasons) -join ",")
    }
    return [pscustomobject]@{
        StateHash = $stateHash
        BeforeSnapshotPath = $beforeSnapshotPath
        RankingPath = $rankingPath
        IntentsPath = $intentsPath
        PreferencePath = $preferencePath
        Preference = $preference
    }
}

function Get-QueueParameter($Item, [string] $Name) {
    return [string](@($Item.normalized_command.parameters |
        Where-Object name -eq $Name | Select-Object -First 1).value)
}

$savesFullPath = [IO.Path]::GetFullPath($SavesPath)
$saveFile = Join-Path (Join-Path $savesFullPath $SaveSlot) $SaveSlot
if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
    throw "Save slot file was not found: $saveFile"
}

$authorityPaths = @(
    $RequirementInventory,
    $AcquisitionLowering,
    $MasterAnglerWindows,
    $RouteTimingCalibration
)
foreach ($path in $authorityPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Teacher authority artifact was not found: $path"
    }
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
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port `
            -ErrorAction SilentlyContinue)) {
        throw "Teacher rollout requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) $RunId
$trainingRoot = Join-Path $artifactDirectory "training"
$journalRoot = Join-Path $artifactDirectory "product-journal"
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
$isolatedSavesPath = Join-Path $artifactDirectory "isolated-saves"
$isolatedModsPath = Join-Path $artifactDirectory "smapi-mods"
New-Item -ItemType Directory -Path $isolatedSavesPath | Out-Null
New-Item -ItemType Directory -Path $isolatedModsPath | Out-Null
Copy-Item -LiteralPath (Join-Path $savesFullPath $SaveSlot) `
    -Destination (Join-Path $isolatedSavesPath $SaveSlot) -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
foreach ($modName in @(
        "StardewAI.TransparentBridge",
        "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $isolatedModsPath $modName) -Recurse
}

& dotnet build (Join-Path $ProjectRoot `
    "src\StardewAI.Backend\StardewAI.Backend.csproj") `
    -c Release --nologo --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Backend Release build failed." }
& dotnet build (Join-Path $ProjectRoot `
    "tools\StardewAI.ProductExecutor\StardewAI.ProductExecutor.csproj") `
    -c Release --nologo --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "ProductExecutor Release build failed." }
& dotnet build (Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") `
    -c Release --nologo --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "LiveTrainingLoop Release build failed." }
& dotnet build (Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj") `
    -c Release --nologo --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Teacher bootstrap Release build failed." }
& dotnet build (Join-Path $ProjectRoot `
    "tools\StardewAI.PolicyDataset\StardewAI.PolicyDataset.csproj") `
    -c Release --nologo --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "PolicyDataset Release build failed." }

$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$productDll = Join-Path $ProjectRoot `
    "tools\StardewAI.ProductExecutor\bin\Release\net8.0\StardewAI.ProductExecutor.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$script:bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
$policyDatasetDll = Join-Path $ProjectRoot `
    "tools\StardewAI.PolicyDataset\bin\Release\net8.0\StardewAI.PolicyDataset.dll"

$environmentNames = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH", "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "STARDEWAI_PRODUCT_EXECUTOR_URL", "STARDEWAI_NATIVE_EXECUTOR_URL",
    "STARDEWAI_BRIDGE_SNAPSHOT_URL", "STARDEWAI_PRODUCT_JOURNAL_ROOT",
    "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT", "STARDEWAI_PRODUCT_RUN_ID",
    "SDL_AUDIODRIVER", "ALSOFT_DRIVERS", "SMAPI_MODS_PATH",
    "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

$game = $null
$backend = $null
$product = $null
try {
    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $artifactDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $isolatedModsPath

    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null

    $env:STARDEWAI_PRODUCT_EXECUTOR_URL = $productUrl
    $env:STARDEWAI_NATIVE_EXECUTOR_URL = "http://127.0.0.1:8767"
    $env:STARDEWAI_BRIDGE_SNAPSHOT_URL = $snapshotUrl
    $env:STARDEWAI_PRODUCT_JOURNAL_ROOT = $journalRoot
    $env:STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT = $isolatedSavesPath
    $env:STARDEWAI_PRODUCT_RUN_ID = $RunId
    $product = Start-Process dotnet -ArgumentList @($productDll) `
        -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "product.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "product.stderr.log") -PassThru
    $productHealth = Wait-Json "$productUrl/health" 60
    if ($productHealth.status -ne "ready" -or
        [int]$productHealth.product_executor_count -le 0) {
        throw "Product executor health contract failed."
    }

    $game = Start-Process $smapi -WorkingDirectory $gameDirectory `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "game.stderr.log") -PassThru
    Wait-Json "http://127.0.0.1:8767/health" `
        $StartupTimeoutSeconds | Out-Null
    $teacher = New-TeacherPreferenceArtifacts `
        -Suffix "" `
        -SnapshotUrl $snapshotUrl `
        -BackendUrl $backendUrl `
        -ArtifactDirectory $artifactDirectory `
        -TrainingRoot $trainingRoot `
        -GoalId $Goal `
        -RequirementInventoryPath $RequirementInventory `
        -AcquisitionLoweringPath $AcquisitionLowering `
        -MasterAnglerWindowsPath $MasterAnglerWindows `
        -RouteTimingCalibrationPath $RouteTimingCalibration `
        -TimeoutSeconds $StartupTimeoutSeconds
    $preference = $teacher.Preference
    $queueItems = @($preference.compiled_queue.items)
    if ($queueItems.Count -ne 1) {
        $first = $queueItems | Select-Object -First 1
        $targetX = Get-QueueParameter $first "target_tile_x"
        $targetY = Get-QueueParameter $first "target_tile_y"
        if ($queueItems.Count -ne 2 -or
            $first.option_id -ne "executor.move_to_tile" -or
            [string]::IsNullOrWhiteSpace($targetX) -or
            [string]::IsNullOrWhiteSpace($targetY)) {
            throw "Teacher preference cannot be reduced to one native receipt by one positioning action."
        }

        $prepositionRoot = Join-Path $artifactDirectory `
            "preposition-training"
        $prepositionArguments = @(
            $loopDll,
            "--root", $prepositionRoot,
            "--backend-url", $backendUrl,
            "--bridge-snapshot-url", $snapshotUrl,
            "--executor-url", $productUrl,
            "--use-product-executor",
            "--no-manifest",
            "--run-id", $RunId,
            "--save-isolation-path", $isolatedSavesPath,
            "--max-attempts", "1",
            "--required-verified-actions", "1",
            "--skip-training",
            "--use-plan-output",
            "--target-tile-x", $targetX,
            "--target-tile-y", $targetY,
            "--sleep-ms", "0",
            "--after-snapshot-wait-ms", "500"
        )
        $prepositionOutput = & dotnet $prepositionArguments
        $prepositionOutput | Set-Content -LiteralPath `
            (Join-Path $artifactDirectory "preposition.stdout.log") `
            -Encoding utf8
        if ($LASTEXITCODE -ne 0) {
            throw "Teacher preposition action failed with exit code $LASTEXITCODE."
        }

        $teacher = New-TeacherPreferenceArtifacts `
            -Suffix "-positioned" `
            -SnapshotUrl $snapshotUrl `
            -BackendUrl $backendUrl `
            -ArtifactDirectory $artifactDirectory `
            -TrainingRoot $trainingRoot `
            -GoalId $Goal `
            -RequirementInventoryPath $RequirementInventory `
            -AcquisitionLoweringPath $AcquisitionLowering `
            -MasterAnglerWindowsPath $MasterAnglerWindows `
            -RouteTimingCalibrationPath $RouteTimingCalibration `
            -TimeoutSeconds $StartupTimeoutSeconds
        $preference = $teacher.Preference
        $queueItems = @($preference.compiled_queue.items)
        if ($queueItems.Count -ne 1) {
            throw "Positioned Teacher preference still does not contain exactly one queue item."
        }
    }
    $stateHash = $teacher.StateHash
    $beforeSnapshotPath = $teacher.BeforeSnapshotPath
    $rankingPath = $teacher.RankingPath
    $intentsPath = $teacher.IntentsPath
    $preferencePath = $teacher.PreferencePath

    $loopArguments = @(
        $loopDll,
        "--root", $trainingRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--snapshot-file", $beforeSnapshotPath,
        "--executor-url", $productUrl,
        "--use-product-executor",
        "--no-manifest",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--max-attempts", "1",
        "--required-verified-actions", "1",
        "--skip-training",
        "--teacher-preference", $preferencePath,
        "--sleep-ms", "0",
        "--after-snapshot-wait-ms", "500"
    )
    $loopOutput = & dotnet $loopArguments
    $loopOutput | Set-Content -LiteralPath (Join-Path $artifactDirectory `
        "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Teacher Product rollout failed with exit code $LASTEXITCODE."
    }

    $snapshotDirectory = Join-Path $trainingRoot `
        "runs\$RunId\live-snapshots"
    $executionReceiptPath = Join-Path $snapshotDirectory `
        "plan-execution-episode-0001.json"
    $afterSnapshotPath = Join-Path $snapshotDirectory `
        "after-snapshot-0001.json"
    foreach ($path in @($executionReceiptPath, $afterSnapshotPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Teacher rollout evidence was not produced: $path"
        }
    }

    $admissionPath = Join-Path $artifactDirectory `
        "teacher-receipt-admission.json"
    $teacherDatasetPath = Join-Path $artifactDirectory `
        "teacher-policy-decision-trajectories.jsonl"
    Invoke-Bootstrap @(
        "build-current-stage-one-collection-teacher-receipt",
        "--requirement-inventory", $RequirementInventory,
        "--acquisition-lowering", $AcquisitionLowering,
        "--ranking", $rankingPath,
        "--before-snapshot", $beforeSnapshotPath,
        "--master-angler-target-date-intents", $intentsPath,
        "--preference", $preferencePath,
        "--execution-receipt", $executionReceiptPath,
        "--after-snapshot", $afterSnapshotPath,
        "--trajectory-id", ("teacher." + $RunId),
        "--run-id", $RunId,
        "--knowledge-dictionary-version", $KnowledgeDictionaryVersion,
        "--executor-version", "product_executor.v1",
        "--output", $admissionPath,
        "--dataset-output", $teacherDatasetPath
    )
    $admission = Get-Content -LiteralPath $admissionPath -Raw |
        ConvertFrom-Json
    $validatedDatasetRoot = Join-Path $artifactDirectory `
        "validated-policy-dataset"
    $datasetValidationOutput = & dotnet $policyDatasetDll `
        --input $teacherDatasetPath `
        --output-root $validatedDatasetRoot `
        --no-horizon-observations `
        --knowledge-dictionary-version $KnowledgeDictionaryVersion
    $datasetValidationOutput | Set-Content -LiteralPath `
        (Join-Path $artifactDirectory "dataset-validation.stdout.log") `
        -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Teacher policy dataset validation failed with exit code $LASTEXITCODE."
    }
    $datasetManifestPath = Join-Path $validatedDatasetRoot `
        "policy-dataset-manifest.json"
    $datasetManifest = Get-Content -LiteralPath $datasetManifestPath -Raw |
        ConvertFrom-Json
    if ([int]$datasetManifest.counts.accepted_rows -ne 1 -or
        [int]$datasetManifest.counts.rejected_rows -ne 0) {
        throw "Teacher policy dataset did not admit exactly one clean row."
    }
    $summary = [ordered]@{
        schema_version = `
            "stardewai.current_stage_one_collection_teacher_product_rollout.v1"
        status = if ($admission.status -eq "ready") { "passed" } else { "blocked" }
        run_id = $RunId
        source_save_path = $saveFile
        isolated_save_root = $isolatedSavesPath
        source_state_hash = $stateHash
        selected_candidate_id = [string]$preference.selected_candidate.candidate_id
        selected_option_id = [string]$preference.selected_candidate.option_id
        queue_id = [string]$preference.compiled_queue.queue_id
        receipt_status = [string]$admission.status
        verified_requirement_transition_count = `
            @($admission.verified_requirement_transitions).Count
        teacher_training_row_eligible = `
            [bool]$admission.teacher_training_row_eligible
        teacher_dataset_path = $teacherDatasetPath
        validated_dataset_manifest_path = $datasetManifestPath
        validated_dataset_accepted_rows = `
            [int]$datasetManifest.counts.accepted_rows
        validated_dataset_rejected_rows = `
            [int]$datasetManifest.counts.rejected_rows
        formal_training_started = $false
    }
    Write-Utf8Json (Join-Path $artifactDirectory "summary.json") `
        ($summary | ConvertTo-Json -Depth 16)
    $summary | ConvertTo-Json -Depth 16
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    foreach ($process in @($game, $product, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
