[CmdletBinding()]
param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [Parameter(Mandatory)]
    [string] $SourceSaveSlotPath,
    [Parameter(Mandatory)]
    [ValidateSet(
        "money_50000",
        "money_100000",
        "money_200000",
        "money_300000",
        "money_500000",
        "money_1000000")]
    [string] $CriterionId,
    [Parameter(Mandatory)]
    [ValidateSet("train", "validation", "test")]
    [string] $DatasetPartition,
    [string] $DependencyExpansion =
        "experiments\goal-method-dependency-expansions.v1.json",
    [string] $RunId =
        ("money-native-outcome-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-money-threshold-native-outcome",
    [int] $BackendPort = 5132,
    [int] $StartupTimeoutSeconds = 180,
    [int] $NaturalWaitTimeoutSeconds = 360,
    [switch] $SkipBuild,
    [switch] $SkipDeploy
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Assert-SourceSaveSlot {
    param([Parameter(Mandatory)] [string] $Path)
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Source save slot is missing: $fullPath"
    }
    $slotName = Split-Path -Leaf $fullPath
    $saveFile = Join-Path $fullPath $slotName
    if (-not (Test-Path -LiteralPath $saveFile -PathType Leaf)) {
        throw "Source save slot does not contain its primary save file: $saveFile"
    }
    $item = Get-Item -LiteralPath $fullPath -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Source save slot must not be a reparse point: $fullPath"
    }
    return $fullPath
}

function Get-SaleEpisodePath {
    param([Parameter(Mandatory)] [string] $SnapshotDirectory)
    $matches = @(Get-ChildItem -LiteralPath $SnapshotDirectory `
        -Filter "plan-execution-episode-*.json" -File |
        Sort-Object Name | Where-Object {
            $episode = Get-Content -LiteralPath $_.FullName -Raw |
                ConvertFrom-Json
            [string]$episode.option_id -eq "executor.sell_shop_item" -and
            [string]$episode.status -eq "applied" -and
            [bool]$episode.success -and
            [string]$episode.primitive_verification_status -eq "verified"
        })
    if ($matches.Count -ne 1) {
        throw "Expected one verified native shop-sale episode; found $($matches.Count)."
    }
    return $matches[0].FullName
}

function Assert-ProductionParameters {
    param([Parameter(Mandatory)] [object[]] $Parameters)
    foreach ($parameter in $Parameters) {
        $name = [string]$parameter.name
        $value = [string]$parameter.value
        if ($name.StartsWith("debug.", [StringComparison]::Ordinal) -or
            $value.StartsWith("debug.", [StringComparison]::Ordinal)) {
            throw "Production native-outcome selection contains a debug parameter."
        }
    }
}

function Wait-NativeGameTime {
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [int] $TargetTime,
        [Parameter(Mandatory)] [int] $SourceTotalDay,
        [Parameter(Mandatory)] [int] $TimeoutSeconds
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $capture = Get-FreshRuntimeSnapshot -Url $Url -TimeoutSeconds 30
        $totalDay = [int](Read-StateValue $capture.Value "time" "total_days")
        $time = [int](Read-StateValue $capture.Value "time" "time")
        if ($totalDay -ne $SourceTotalDay) {
            throw "Natural service wait crossed a day boundary."
        }
        if ($time -ge $TargetTime) { return $capture }
        Start-Sleep -Seconds 5
    }
    throw "Timed out waiting naturally for service time $TargetTime."
}

$sourceSave = Assert-SourceSaveSlot -Path $SourceSaveSlotPath
$sourceSlotName = Split-Path -Leaf $sourceSave
$dependencyPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $DependencyExpansion
$outputRoot = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $OutputDirectory
$runDirectory = Join-Path $outputRoot $RunId
$isolatedSaves = Join-Path $runDirectory "isolated-saves"
$isolatedSlot = Join-Path $isolatedSaves $sourceSlotName
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
$snapshotPath = Join-Path $runDirectory "source-snapshot.json"
$candidateCatalogPath = Join-Path $runDirectory `
    "money-revenue-candidate-denominator.json"
$selectionPath = Join-Path $runDirectory `
    "money-threshold-native-outcome-selection.json"
$corpusRequestPath = Join-Path $runDirectory `
    "money-threshold-native-outcome-corpus-request.json"
$corpusPath = Join-Path $runDirectory `
    "money-threshold-native-outcome-corpus.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$failurePath = Join-Path $runDirectory "failure.json"

foreach ($required in @($dependencyPath, $smapiPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required production native-outcome input is missing: $required"
    }
}
Assert-RuntimePortsUnused -Ports @($BackendPort, 8765, 8767) `
    -OperationName "Money threshold native-outcome collection"
if (Test-Path -LiteralPath $runDirectory) {
    throw "Money threshold run directory already exists: $runDirectory"
}

New-Item -ItemType Directory -Force -Path $isolatedSaves | Out-Null
$sourceSaveSha256Before = Get-DirectoryContentHash -Path $sourceSave
Copy-Item -LiteralPath $sourceSave -Destination $isolatedSlot -Recurse
$isolatedSaveSha256Before = Get-DirectoryContentHash -Path $isolatedSlot
if ($sourceSaveSha256Before -ne $isolatedSaveSha256Before) {
    throw "Isolated save copy does not equal its source before execution."
}

if (-not $SkipBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj",
        "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
    )) {
        & dotnet build (Join-Path $ProjectRoot $project) `
            -c Release --no-restore --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed before Money threshold collection: $project"
        }
    }
}
foreach ($required in @($backendDll, $loopDll, $bootstrapDll)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required Release artifact is missing: $required"
    }
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
    $env:ASPNETCORE_URLS = $backendUrl

    $backendProcess = Start-RuntimeEvidenceProcess `
        -FilePath "dotnet" `
        -ArgumentList @($backendDll) `
        -WorkingDirectory $ProjectRoot `
        -StandardOutputPath $backendStdout `
        -StandardErrorPath $backendStderr
    Wait-Json -Url "$backendUrl/health" -TimeoutSeconds 60 | Out-Null

    $gameProcess = Start-RuntimeEvidenceProcess `
        -FilePath $smapiPath `
        -WorkingDirectory $runtimeGameDirectory `
        -StandardOutputPath $gameStdout `
        -StandardErrorPath $gameStderr
    Wait-Json -Url "$executorUrl/health" -TimeoutSeconds 60 | Out-Null
    $capture = Get-FreshRuntimeSnapshot `
        -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $naturalWaitApplied = $false
    while ($true) {
        Write-Utf8Text -Path $snapshotPath -Value $capture.Raw
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-revenue-candidate-denominator",
            "--snapshot", $snapshotPath,
            "--dependencies", $dependencyPath,
            "--output", $candidateCatalogPath)
        $candidateCatalog = Get-Content -LiteralPath $candidateCatalogPath -Raw |
            ConvertFrom-Json
        $saleNode = @($candidateCatalog.nodes | Where-Object {
            [string]$_.option_id -eq "economy.sell_items"
        })
        if ($saleNode.Count -ne 1) {
            throw "Revenue denominator did not expose one shop-sale node."
        }
        $availableSales = @($saleNode[0].candidates | Where-Object {
            [string]$_.candidate_channel -eq "event_candidate" -and
            [bool]$_.available
        })
        if ($availableSales.Count -gt 0) { break }
        $currentTime = [int](Read-StateValue $capture.Value "time" "time")
        $sourceTotalDay =
            [int](Read-StateValue $capture.Value "time" "total_days")
        $futureOpenTimes = @($saleNode[0].candidates | Where-Object {
            [string]$_.candidate_channel -eq "event_candidate" -and
            $_.allowed_today -eq $true -and
            $null -ne $_.effective_open_time -and
            [int]$_.effective_open_time -gt $currentTime
        } | ForEach-Object { [int]$_.effective_open_time } |
            Sort-Object -Unique)
        if ($naturalWaitApplied -or $futureOpenTimes.Count -eq 0) { break }
        $capture = Wait-NativeGameTime `
            -Url $snapshotUrl `
            -TargetTime $futureOpenTimes[0] `
            -SourceTotalDay $sourceTotalDay `
            -TimeoutSeconds $NaturalWaitTimeoutSeconds
        $naturalWaitApplied = $true
    }
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-threshold-native-outcome-selection",
        "--candidate-catalog", $candidateCatalogPath,
        "--criterion", $CriterionId,
        "--partition", $DatasetPartition,
        "--output", $selectionPath)

    $selection = Get-Content -LiteralPath $selectionPath -Raw |
        ConvertFrom-Json
    Assert-ProductionParameters `
        -Parameters @($selection.objective_parameters)
    $loopRoot = Join-Path $runDirectory "loop"
    $loopArguments = [Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        $loopDll,
        "--root", $loopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--execution-snapshot-profile", "full",
        "--executor-url", $executorUrl,
        "--no-manifest",
        "--skip-training",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSaves,
        "--goal", "daily.closed_loop",
        "--max-attempts", "24",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "16",
        "--sleep-ms", "100",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", "economy.sell_items",
        "--continue-after-blocked-queue-items",
        "--after-snapshot-wait-ms", "750",
        "--after-snapshot-poll-ms", "250",
        "--stop-after-objective-complete"
    )) { $loopArguments.Add([string]$argument) }
    foreach ($parameter in @($selection.objective_parameters)) {
        $loopArguments.Add("--daily-plan-candidate-parameter")
        $loopArguments.Add(
            [string]$parameter.name + "=" + [string]$parameter.value)
    }
    $loopStdout = @(& dotnet @loopArguments 2>&1)
    $loopStdout | Set-Content -LiteralPath `
        (Join-Path $runDirectory "loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Money threshold LiveTrainingLoop failed with exit $LASTEXITCODE."
    }

    $snapshotDirectory = Join-Path $loopRoot `
        "runs\$RunId\live-snapshots"
    $episodePath = Get-SaleEpisodePath `
        -SnapshotDirectory $snapshotDirectory
    Write-JsonFile -Path $corpusRequestPath -Value ([ordered]@{
        schema_version = "money_threshold_teacher_corpus_request.v4"
        corpus_id = "native-outcome-$RunId"
        sources = @([ordered]@{
            source_id = $RunId
            execution_episode_path = $episodePath
            comparison_admissions = @()
        })
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-threshold-teacher-corpus",
        "--request", $corpusRequestPath,
        "--output", $corpusPath)
    $corpus = Get-Content -LiteralPath $corpusPath -Raw |
        ConvertFrom-Json
    $row = @($corpus.rows)[0]
    $crossedIds = @($row.crossed_thresholds | ForEach-Object {
        [string]$_.criterion_id
    })
    $expectedCrossedIds = @(
        $selection.expected_crossed_criterion_ids |
            ForEach-Object { [string]$_ })
    if (@($corpus.rows).Count -ne 1 -or
        -not [bool]$row.native_terminal_outcome_verified -or
        [string]$row.dataset_partition -ne $DatasetPartition -or
        $crossedIds -notcontains $CriterionId -or
        ($crossedIds -join "|") -ne ($expectedCrossedIds -join "|") -or
        [uint64]$row.total_money_earned_before -ne
            [uint64]$selection.total_money_earned_before -or
        [uint64]$row.total_money_earned_after -ne
            [uint64]$selection.expected_total_money_earned_after) {
        throw "Verified native shop-sale outcome drifted from its production selection."
    }

    $sourceSaveSha256After = Get-DirectoryContentHash -Path $sourceSave
    if ($sourceSaveSha256After -ne $sourceSaveSha256Before) {
        throw "Source save changed during isolated native-outcome collection."
    }
    $summary = [ordered]@{
        schema_version = "money_threshold_native_outcome_run_summary.v1"
        status = "passed"
        run_id = $RunId
        criterion_id = $CriterionId
        dataset_partition = $DatasetPartition
        source_save_slot_path = $sourceSave
        source_save_sha256_before = $sourceSaveSha256Before
        source_save_sha256_after = $sourceSaveSha256After
        isolated_save_slot_path = $isolatedSlot
        isolated_save_sha256_before = $isolatedSaveSha256Before
        source_state_hash = [string]$selection.source_state_hash
        natural_service_wait_applied = $naturalWaitApplied
        selection_path = $selectionPath
        selection_sha256 = [string]$selection.selection_sha256
        candidate_denominator_path = $candidateCatalogPath
        candidate_denominator_sha256 =
            (Get-FileHash -LiteralPath $candidateCatalogPath -Algorithm SHA256).Hash.ToLowerInvariant()
        execution_episode_path = $episodePath
        money_threshold_corpus_path = $corpusPath
        crossed_criterion_ids = $crossedIds
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
        schema_version = "money_threshold_native_outcome_run_failure.v1"
        status = "failed"
        run_id = $RunId
        criterion_id = $CriterionId
        dataset_partition = $DatasetPartition
        error = $failureMessage
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
