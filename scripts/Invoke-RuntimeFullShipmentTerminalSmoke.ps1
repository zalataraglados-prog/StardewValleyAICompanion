param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = (
        "runtime-full-shipment-terminal-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $RequirementInventory =
        "experiments\local-data\output\authoritative-requirement-inventory-v1.json",
    [string] $AcquisitionLowering =
        "experiments\local-data\output\acquisition-route-option-lowering-v1.json",
    [string] $TerminalQualifiedItemId = "(O)24",
    [int] $BackendPort = 8798,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Resolve-InputPath {
    param([string] $Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Get-DirectoryContentHash {
    param([string] $Path)
    $root = [System.IO.Path]::GetFullPath($Path)
    $rows = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Sort-Object FullName | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath(
                $root,
                $_.FullName)
            $fileHash = (Get-FileHash -LiteralPath $_.FullName `
                -Algorithm SHA256).Hash.ToLowerInvariant()
            "$relativePath`t$($_.Length)`t$fileHash"
        })
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes(
            [string]::Join("`n", $rows))
        return [System.Convert]::ToHexString(
            $algorithm.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Read-FieldValue {
    param($Snapshot, [string] $Domain, [string] $Field)
    $domainNode = $Snapshot.state.$Domain
    if ($null -eq $domainNode) { return $null }
    $fieldNode = $domainNode.$Field
    if ($null -eq $fieldNode) { return $null }
    return $fieldNode.value
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 240)
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod -Method Get -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-Json {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $value = Invoke-JsonGet -Url $Url -TimeoutSeconds 8
            if ($null -ne $value) { return $value }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 15
            $location = [string](Read-FieldValue $snapshot "player" "location_id")
            $progress = Read-FieldValue `
                $snapshot "world_progress" "full_shipment_progress"
            if (-not [string]::IsNullOrWhiteSpace($location) -and
                $null -ne $progress -and
                [int]$progress.eligible_item_count -eq $expectedDenominator) {
                return $snapshot
            }
            $lastState =
                "location=$location;eligible=$($progress.eligible_item_count)"
        }
        catch { $lastState = $_.Exception.Message }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for Full Shipment snapshot. Last state: $lastState"
}

function Get-TerminalBinCount {
    param($Snapshot)
    $bins = @(Read-FieldValue $Snapshot "farm" "shipping_bins")
    if ($bins.Count -eq 0) { throw "Transparent shipping-bin view is empty." }
    $counts = @($bins | ForEach-Object {
        [int](@($_.contents | Where-Object {
            [string]$_.qualified_item_id -eq $TerminalQualifiedItemId
        } | ForEach-Object { [int]$_.count } | Measure-Object -Sum).Sum)
    })
    if (@($counts | Select-Object -Unique).Count -ne 1) {
        throw "Shared shipping-bin views disagree for $TerminalQualifiedItemId."
    }
    return [int]$counts[0]
}

function Assert-TerminalState {
    param(
        $Snapshot,
        [int] $ExpectedShippedCount,
        [int] $ExpectedMissingCount,
        [int] $ExpectedTerminalShippedCount,
        [int] $ExpectedTerminalBinCount,
        [bool] $ExpectedAchievement,
        [string] $Phase
    )
    $progress = Read-FieldValue `
        $Snapshot "world_progress" "full_shipment_progress"
    $row = @($progress.items | Where-Object {
        [string]$_.qualified_item_id -eq $TerminalQualifiedItemId
    })
    $achievements = @(Read-FieldValue `
        $Snapshot "world_progress" "achievements")
    $achievement = $achievements -contains 34
    $binCount = Get-TerminalBinCount -Snapshot $Snapshot
    if ($row.Count -ne 1 -or
        [int]$progress.eligible_item_count -ne $expectedDenominator -or
        [int]$progress.shipped_eligible_item_count -ne $ExpectedShippedCount -or
        [int]$progress.missing_item_count -ne $ExpectedMissingCount -or
        [int]$row[0].current_shipped_count -ne
            $ExpectedTerminalShippedCount -or
        $binCount -ne $ExpectedTerminalBinCount -or
        $achievement -ne $ExpectedAchievement) {
        throw "$Phase Full Shipment state mismatch: shipped=" +
            "$($progress.shipped_eligible_item_count), missing=" +
            "$($progress.missing_item_count), terminal=" +
            "$($row[0].current_shipped_count), bin=$binCount, " +
            "achievement34=$achievement."
    }
}

function New-BaseRequest {
    param($Snapshot, [string] $OptionId, [string] $QueueItemId)
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "$RunId.fixture"
        queue_item_id = $QueueItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $isolatedSavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-LoopStep {
    param(
        [string] $Name,
        $Snapshot,
        [string] $OptionId,
        [string] $CandidateKind,
        [string] $CandidateId,
        [string[]] $CandidateParameters = @()
    )
    $stepDirectory = Join-Path $artifactDirectory $Name
    $stepLoopRoot = Join-Path $stepDirectory "loop"
    New-Item -ItemType Directory -Force -Path $stepDirectory | Out-Null
    $sourcePath = Join-Path $stepDirectory "source-snapshot.json"
    Write-JsonFile -Path $sourcePath -Value $Snapshot

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @(
        $loopDll,
        "--root", $stepLoopRoot,
        "--backend-url", $backendUrl,
        "--bridge-snapshot-url", $snapshotUrl,
        "--executor-url", $executorRoot,
        "--snapshot-file", $sourcePath,
        "--no-manifest",
        "--skip-training",
        "--run-id", $RunId,
        "--save-isolation-path", $isolatedSavesPath,
        "--iterations", "1",
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", "8",
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", $OptionId,
        "--daily-plan-candidate-kind", $CandidateKind,
        "--daily-plan-candidate-id", $CandidateId,
        "--emit-queue-execution-receipt",
        "--after-snapshot-wait-ms", "1500",
        "--continue-after-blocked-queue-items"
    )) {
        $arguments.Add([string]$value)
    }
    foreach ($parameter in $CandidateParameters) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add($parameter)
    }

    $loopOutput = & dotnet $arguments
    $loopOutput | Set-Content `
        -LiteralPath (Join-Path $stepDirectory "loop.stdout.log") `
        -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "Full Shipment loop step $Name failed with exit $LASTEXITCODE."
    }

    $snapshotDirectory = Join-Path $stepLoopRoot (
        "runs\" + $RunId + "\live-snapshots")
    $queuePath = Join-Path $snapshotDirectory "compiled-queue-0001.json"
    $beforePath = Join-Path $snapshotDirectory "before-snapshot-0001.json"
    $executionPath = Join-Path $snapshotDirectory "execution-0001.json"
    $afterPath = Join-Path $snapshotDirectory "after-snapshot-0001.json"
    foreach ($path in @($queuePath, $beforePath, $executionPath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Full Shipment loop step $Name did not produce $path."
        }
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw |
        ConvertFrom-Json
    if ($execution.status -ne "applied" -or
        -not [bool]$execution.after_snapshot_fresh) {
        throw "Full Shipment loop step $Name did not produce fresh applied execution."
    }
    return [ordered]@{
        name = $Name
        candidate_id = $CandidateId
        queue_path = $queuePath
        before_path = $beforePath
        execution_path = $executionPath
        after_path = $afterPath
        execution = $execution
        after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json
    }
}

$requirementInventoryPath = Resolve-InputPath $RequirementInventory
$acquisitionLoweringPath = Resolve-InputPath $AcquisitionLowering
foreach ($path in @($requirementInventoryPath, $acquisitionLoweringPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required authority artifact not found: $path"
    }
}
$authority = Get-Content -LiteralPath $requirementInventoryPath -Raw |
    ConvertFrom-Json
$fullShipmentSets = @($authority.requirement_sets | Where-Object {
    [string]$_.requirement_set_id -eq "full_shipment"
})
if ($authority.schema_version -ne
        "authoritative_goal_requirement_inventory.v1" -or
    -not [bool]$authority.denominator_complete -or
    $fullShipmentSets.Count -ne 1) {
    throw "Full Shipment authority is incomplete or ambiguous."
}
$fullShipmentSet = $fullShipmentSets[0]
$expectedDenominator = [int]$fullShipmentSet.required_group_count
$terminalGroups = @($fullShipmentSet.groups | Where-Object {
    @($_.alternatives).Count -eq 1 -and
    [string]$_.alternatives[0].qualified_item_id -eq
        $TerminalQualifiedItemId
})
if ($expectedDenominator -ne 154 -or
    @($fullShipmentSet.groups).Count -ne $expectedDenominator -or
    $terminalGroups.Count -ne 1) {
    throw "Full Shipment authority denominator or terminal identity drifted."
}
$terminalItemId = [string]$terminalGroups[0].alternatives[0].item_id

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$runtimeSavesPath = Join-Path $RuntimeRoot "saves"
$smapi = Join-Path $gameDirectory "StardewModdingAPI.exe"
$backendUrl = "http://127.0.0.1:$BackendPort"
$executorRoot = "http://127.0.0.1:8767"
$nativeExecutorUrl = $executorRoot + "/api/v1/training/execute"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
if (-not (Test-Path -LiteralPath $smapi -PathType Leaf)) {
    throw "SMAPI executable not found: $smapi"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $runtimeSavesPath -Directory |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated runtime save exists under $runtimeSavesPath."
    }
    $SaveSlot = $slot.Name
}
$sourceSavePath = Join-Path $runtimeSavesPath $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Runtime save slot not found: $sourceSavePath"
}
foreach ($port in @(8765, 8767, $BackendPort)) {
    if ($null -ne (Get-NetTCPConnection -State Listen `
            -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Full Shipment terminal smoke requires unused port $port."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" `
        -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach."
}

$artifactDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-full-shipment-terminal\" + $RunId)
$isolatedSavesPath = Join-Path $artifactDirectory "isolated-saves"
$isolatedSavePath = Join-Path $isolatedSavesPath $SaveSlot
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
$sourceSaveHashBefore = Get-DirectoryContentHash -Path $sourceSavePath
New-Item -ItemType Directory -Force -Path $isolatedSavesPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath `
    -Destination $isolatedSavePath -Recurse
New-Item -ItemType Directory -Force `
    -Path $trainingOutputDirectory | Out-Null

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
foreach ($project in @(
    "src\StardewAI.Backend\StardewAI.Backend.csproj",
    "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj",
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
)) {
    dotnet build (Join-Path $ProjectRoot $project) `
        -c Release --no-restore --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Release build failed: $project" }
}
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

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
    "ASPNETCORE_URLS"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] =
        [Environment]::GetEnvironmentVariable($name)
}
$backend = $null
$game = $null
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process dotnet -ArgumentList @(
        "run", "--no-restore", "--project",
        (Join-Path $ProjectRoot `
            "src\StardewAI.Backend\StardewAI.Backend.csproj"),
        "--no-launch-profile"
    ) -WorkingDirectory $ProjectRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "backend.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "backend.stderr.log") -PassThru
    Wait-Json -Url ($backendUrl + "/health") -TimeoutSeconds 60 | Out-Null

    $env:STARDEWAI_TEST_SAVES = $isolatedSavesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $isolatedSavesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $game = Start-Process -FilePath $smapi `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $artifactDirectory `
            "game.stdout.log") `
        -RedirectStandardError (Join-Path $artifactDirectory `
            "game.stderr.log") -PassThru
    Wait-Json -Url ($executorRoot + "/health") `
        -TimeoutSeconds 60 | Out-Null
    $loaded = Wait-WorldSnapshot -TimeoutSeconds $StartupTimeoutSeconds

    $fixtureRequest = New-BaseRequest `
        -Snapshot $loaded `
        -OptionId "debug.setup_full_shipment_terminal" `
        -QueueItemId "$RunId.fixture.setup"
    $fixtureRequest.qualified_item_id = $TerminalQualifiedItemId
    $fixtureRequest.quantity = 1
    $fixtureRequest.full_shipment_expected_eligible_item_count =
        $expectedDenominator
    $fixture = Invoke-JsonPost -Url $nativeExecutorUrl -Body $fixtureRequest
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "fixture-setup.json") `
        -Value $fixture
    if ($fixture.status -ne "applied" -or
        $fixture.primitive_verification_status -ne "verified") {
        throw "Full Shipment terminal fixture setup failed."
    }
    $fixtureSnapshot = Wait-WorldSnapshot -TimeoutSeconds 60
    Assert-TerminalState `
        -Snapshot $fixtureSnapshot `
        -ExpectedShippedCount 153 `
        -ExpectedMissingCount 1 `
        -ExpectedTerminalShippedCount 0 `
        -ExpectedTerminalBinCount 0 `
        -ExpectedAchievement $false `
        -Phase "fixture"
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "fixture-snapshot.json") `
        -Value $fixtureSnapshot

    $item = @((Read-FieldValue $fixtureSnapshot "player" "inventory") |
        Where-Object {
            [string]$_.qualified_item_id -eq $TerminalQualifiedItemId -and
            [int]$_.stack -gt 0
        } | Sort-Object slot_index | Select-Object -First 1)[0]
    $bin = @((Read-FieldValue $fixtureSnapshot "farm" "shipping_bins") |
        Where-Object {
            [int]$_.days_of_construction_left -le 0 -and
            $null -ne $_.interaction_stand_tile_x -and
            $null -ne $_.interaction_stand_tile_y
        } | Select-Object -First 1)[0]
    if ($null -eq $item -or $null -eq $bin) {
        throw "Terminal item or completed shipping-bin binding is missing."
    }
    $shippingCandidateId = "ship:Farm:{0},{1}:{2}:{3}:deposit" -f @(
        [int]$bin.tile_x,
        [int]$bin.tile_y,
        [int]$item.slot_index,
        $terminalItemId)
    $shipping = Invoke-LoopStep `
        -Name "01-ship-terminal-item" `
        -Snapshot $fixtureSnapshot `
        -OptionId "economy.ship_items" `
        -CandidateKind "ship_inventory_item_to_bin" `
        -CandidateId $shippingCandidateId `
        -CandidateParameters @(
            "continuation.option_id=economy.ship_items",
            "continuation.target_location=Farm",
            "continuation.item_id=$($item.item_id)",
            "continuation.qualified_item_id=$($item.qualified_item_id)",
            "continuation.slot_index=$($item.slot_index)",
            "continuation.quantity=1",
            "continuation.expected_unit_price=$($item.sell_to_store_price)",
            "continuation.bin_location=Farm",
            "continuation.bin_tile_x=$($bin.tile_x)",
            "continuation.bin_tile_y=$($bin.tile_y)",
            "continuation.stand_tile_x=$($bin.interaction_stand_tile_x)",
            "continuation.stand_tile_y=$($bin.interaction_stand_tile_y)"
        )
    Assert-TerminalState `
        -Snapshot $shipping.after `
        -ExpectedShippedCount 153 `
        -ExpectedMissingCount 1 `
        -ExpectedTerminalShippedCount 0 `
        -ExpectedTerminalBinCount 1 `
        -ExpectedAchievement $false `
        -Phase "post-deposit"

    $sleepFixtureRequest = New-BaseRequest `
        -Snapshot $shipping.after `
        -OptionId "debug.prepare_full_shipment_terminal_sleep" `
        -QueueItemId "$RunId.fixture.sleep"
    $sleepFixture = Invoke-JsonPost `
        -Url $nativeExecutorUrl -Body $sleepFixtureRequest
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "sleep-fixture-setup.json") `
        -Value $sleepFixture
    if ($sleepFixture.status -ne "applied" -or
        $sleepFixture.primitive_verification_status -ne "verified") {
        throw "Full Shipment native sleep fixture setup failed."
    }
    $sleepReadySnapshot = Wait-WorldSnapshot -TimeoutSeconds 60
    Assert-TerminalState `
        -Snapshot $sleepReadySnapshot `
        -ExpectedShippedCount 153 `
        -ExpectedMissingCount 1 `
        -ExpectedTerminalShippedCount 0 `
        -ExpectedTerminalBinCount 1 `
        -ExpectedAchievement $false `
        -Phase "sleep-ready"

    $sleepCandidateId = "recovery:native_save_boundary"
    $sleep = Invoke-LoopStep `
        -Name "02-native-terminal-settlement" `
        -Snapshot $sleepReadySnapshot `
        -OptionId "recovery.stabilize_day" `
        -CandidateKind "recovery_sleep_immediately" `
        -CandidateId $sleepCandidateId `
        -CandidateParameters @(
            "control_plane.native_save_boundary=true"
        )
    Assert-TerminalState `
        -Snapshot $sleep.after `
        -ExpectedShippedCount 154 `
        -ExpectedMissingCount 0 `
        -ExpectedTerminalShippedCount 1 `
        -ExpectedTerminalBinCount 0 `
        -ExpectedAchievement $true `
        -Phase "post-settlement"

    $admissionPath = Join-Path $artifactDirectory `
        "full-shipment-terminal-settlement-admission.json"
    & dotnet $bootstrapDll `
        "build-full-shipment-terminal-settlement-receipt" `
        "--requirement-inventory" $requirementInventoryPath `
        "--acquisition-lowering" $acquisitionLoweringPath `
        "--queue" $sleep.queue_path `
        "--before-snapshot" $sleep.before_path `
        "--execution-receipt" $sleep.execution_path `
        "--after-snapshot" $sleep.after_path `
        "--run-id" $RunId `
        "--executor-version" "runtime_test_harness_executor.v1" `
        "--selected-candidate-id" $sleepCandidateId `
        "--output" $admissionPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Full Shipment terminal settlement admission command failed."
    }
    $admission = Get-Content -LiteralPath $admissionPath -Raw |
        ConvertFrom-Json
    if ($admission.status -ne "ready" -or
        -not [bool]$admission.training_label_eligible) {
        throw "Full Shipment terminal settlement evidence was not admitted."
    }
    $sourceSaveHashAfter = Get-DirectoryContentHash -Path $sourceSavePath
    if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
        throw "Source save changed during isolated Full Shipment terminal smoke."
    }

    $summary = [ordered]@{
        schema_version =
            "stardewai.runtime_full_shipment_terminal_smoke.v1"
        status = "passed"
        run_id = $RunId
        source_save_slot = $SaveSlot
        isolated_save_root = $isolatedSavesPath
        source_save_preserved = $true
        source_save_hash_before = $sourceSaveHashBefore
        source_save_hash_after = $sourceSaveHashAfter
        authority_game_version = [string]$authority.game_version
        authoritative_requirement_count = $expectedDenominator
        terminal_item_id = $terminalItemId
        terminal_qualified_item_id = $TerminalQualifiedItemId
        shipping_candidate_id = $shippingCandidateId
        sleep_candidate_id = $sleepCandidateId
        terminal_settlement_status = [string]$admission.status
        training_label_eligible =
            [bool]$admission.training_label_eligible
        before_shipped_item_count =
            [int]$admission.transition_evidence.before_shipped_item_count
        after_shipped_item_count =
            [int]$admission.transition_evidence.after_shipped_item_count
        before_total_day =
            [int]$admission.transition_evidence.before_total_day
        after_total_day =
            [int]$admission.transition_evidence.after_total_day
        achievement_34_after =
            [bool]$admission.transition_evidence.achievement_34_after
        admission_path = $admissionPath
    }
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "summary.json") `
        -Value $summary
    $summary | ConvertTo-Json -Depth 48
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $savedEnvironment[$name],
            "Process")
    }
    if (-not $KeepGameRunning) {
        foreach ($process in @($game, $backend)) {
            if ($null -ne $process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force `
                    -ErrorAction SilentlyContinue
                $process.WaitForExit(10000) | Out-Null
            }
        }
    }
}
