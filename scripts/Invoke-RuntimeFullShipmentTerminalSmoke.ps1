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
    [ValidateRange(-1, 29)]
    [int] $PendingProfessionChoiceId = -1,
    [ValidateRange(0, 2)]
    [int] $PendingOrdinaryLevelCount = 0,
    [ValidateSet("", "day_31_earthquake")]
    [string] $PendingFarmEvent = "",
    [ValidateSet(
        "",
        "native_kent_post_save",
        "native_cat_adoption_unsafe_question")]
    [string] $PostSaveStoryHandoff = "",
    [ValidateRange(0, 30)]
    [int] $PostSaveStoryVisualHoldSeconds = 0,
    [ValidateSet(
        "",
        "unknown_menu_transition_timeout",
        "overall_timeout")]
    [string] $EndOfNightNegativeProfile = "",
    [ValidateRange(0, 30)]
    [int] $NegativeVisualHoldSeconds = 0,
    [switch] $NoShipment,
    [switch] $OwnerComposite,
    [int] $BackendPort = 8798,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $NoBuild,
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

if ($OwnerComposite -and
    ($NoShipment -or
     $PendingProfessionChoiceId -lt 0 -or
     $PendingOrdinaryLevelCount -ne 2 -or
     $PendingFarmEvent -ne "day_31_earthquake" -or
     -not $VisibleGame)) {
    throw "Owner composite requires visible positive shipping, two ordinary levels, one planned profession, and the day-31 earthquake fixture."
}
if (-not [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff) -and
    (-not $NoShipment -or
     $PendingProfessionChoiceId -ge 0 -or
     $PendingOrdinaryLevelCount -gt 0 -or
     -not [string]::IsNullOrWhiteSpace($PendingFarmEvent) -or
     $OwnerComposite)) {
    throw "Post-save native story handoff requires one isolated no-shipment night without level, profession, FarmEvent, or owner-composite fixtures."
}
if ($PostSaveStoryVisualHoldSeconds -gt 0 -and
    ($PostSaveStoryHandoff -ne
        "native_cat_adoption_unsafe_question" -or
     -not $VisibleGame)) {
    throw "Post-save story visual hold requires the visible native cat-question profile."
}
if (-not [string]::IsNullOrWhiteSpace($EndOfNightNegativeProfile) -and
    (-not $NoShipment -or
     $PendingProfessionChoiceId -ge 0 -or
     $PendingOrdinaryLevelCount -gt 0 -or
     -not [string]::IsNullOrWhiteSpace($PendingFarmEvent) -or
     -not [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff) -or
     $OwnerComposite)) {
    throw "End-of-night negative profile requires one isolated no-shipment night without level, profession, FarmEvent, story-handoff, or owner-composite fixtures."
}
if ($NegativeVisualHoldSeconds -gt 0 -and
    ([string]::IsNullOrWhiteSpace($EndOfNightNegativeProfile) -or
     -not $VisibleGame)) {
    throw "Negative visual hold requires a visible end-of-night negative profile."
}

$matrixScenario = $NoShipment -or
    $PendingProfessionChoiceId -ge 0 -or
    $PendingOrdinaryLevelCount -gt 0 -or
    -not [string]::IsNullOrWhiteSpace($PendingFarmEvent) -or
    -not [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff) -or
    -not [string]::IsNullOrWhiteSpace($EndOfNightNegativeProfile)

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

$script:logicalJsonArtifactReaderLoaded = $false

function Read-LogicalJsonArtifact {
    param([string] $Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Logical JSON artifact not found: $fullPath"
    }

    if (-not $script:logicalJsonArtifactReaderLoaded) {
        if ([string]::IsNullOrWhiteSpace([string]$loopDll)) {
            throw "LiveTrainingLoop output is unavailable for logical JSON artifact reads."
        }
        $readerAssemblyPath = Join-Path (Split-Path -Parent $loopDll) `
            "StardewAI.Core.dll"
        if (-not (Test-Path -LiteralPath $readerAssemblyPath -PathType Leaf)) {
            throw "Content-addressed JSON reader assembly not found: $readerAssemblyPath"
        }
        $readerType =
            "StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader" `
                -as [type]
        if ($null -eq $readerType) {
            Add-Type -Path $readerAssemblyPath
        }
        $script:logicalJsonArtifactReaderLoaded = $true
    }

    try {
        $raw = [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText(
            $fullPath)
        return ConvertFrom-Json -InputObject $raw
    }
    catch {
        throw "Failed to read logical JSON artifact $fullPath`: $($_.Exception.Message)"
    }
}

function Get-VerifiedEndOfNightEvidence {
    param(
        $Tracker,
        [string] $ExpectedStatus,
        [string] $ExpectedRunId,
        [string] $TrainingOutputDirectory
    )
    $path = [string]$Tracker.evidence_path
    if ([string]::IsNullOrWhiteSpace($path) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "End-of-night transaction evidence file is missing: $path"
    }

    $root = ([IO.Path]::GetFullPath($TrainingOutputDirectory)).TrimEnd(
        [char[]]"\/") + [IO.Path]::DirectorySeparatorChar
    $fullPath = [IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith(
            $root,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "End-of-night transaction evidence escaped its isolated output root: $fullPath"
    }

    $evidence = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    if ([string]$evidence.schema_version -ne
            "end_of_night_transaction.v1" -or
        [string]$evidence.transaction_id -ne
            [string]$Tracker.transaction_id -or
        [string]$evidence.run_id -ne $ExpectedRunId -or
        [string]$evidence.status -ne $ExpectedStatus) {
        throw "End-of-night transaction evidence identity or status mismatched its live tracker."
    }

    $evidenceHash = ((Get-FileHash -LiteralPath $fullPath `
        -Algorithm SHA256).Hash).ToLowerInvariant()
    return [ordered]@{
        path = $fullPath
        sha256 = $evidenceHash
    }
}

function Get-VerifiedCompletedStoryInterrupt {
    param(
        $LoopStep,
        [string] $ExpectedEventId
    )

    $matches = @($LoopStep.execution.step_results | Where-Object {
        $eventIdChanges = @($_.changed_facts | Where-Object {
            [string]$_.path -eq "player.story_event.event_id" -and
            [string]$_.before -eq $ExpectedEventId -and
            [string]::IsNullOrEmpty([string]$_.after)
        })
        [string]$_.option_id -eq "executor.advance_story_event" -and
        [string]$_.source_planning_option_id -eq "story.advance_event" -and
        $eventIdChanges.Count -eq 1
    })
    if ($matches.Count -eq 0) {
        return $null
    }
    if ($matches.Count -ne 1) {
        throw "Expected at most one completed story interrupt for event " +
            "$ExpectedEventId, found $($matches.Count)."
    }

    $step = $matches[0]
    $reasons = @($step.primitive_verification_reasons | ForEach-Object {
        [string]$_
    })
    $seenChanges = @($step.changed_facts | Where-Object {
        [string]$_.path -eq "player.story_event.event_seen" -and
        [string]$_.before -eq "false" -and
        [string]$_.after -eq "true"
    })
    if ([string]$step.status -ne "applied" -or
        [string]$step.primitive_verification_status -ne "verified" -or
        @($step.block_reasons).Count -ne 0 -or
        [string]$step.source -ne "runtime_test_harness_executor" -or
        $seenChanges.Count -ne 1 -or
        $reasons -notcontains "native_event_input_only" -or
        $reasons -notcontains "boundary=event_completed" -or
        $reasons -notcontains "skipEvent_not_used" -or
        [string]$step.observed_effect -notlike
            "boundary=event_completed;event_id=none;*progress_observed=true") {
        throw "Story interrupt for event $ExpectedEventId lacked one verified native completion receipt."
    }

    $snapshotRoot = ([IO.Path]::GetFullPath(
        (Split-Path -Parent ([string]$LoopStep.execution_path)))).TrimEnd(
            [char[]]"\/") + [IO.Path]::DirectorySeparatorChar
    $beforePath = [IO.Path]::GetFullPath(
        [string]$step.effective_before_snapshot_path)
    $queuePath = [IO.Path]::GetFullPath(
        [string]$step.mechanical_compiled_queue_path)
    foreach ($path in @($beforePath, $queuePath)) {
        if (-not $path.StartsWith(
                $snapshotRoot,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Story interrupt evidence escaped its loop snapshot root: $path"
        }
    }

    $beforeSnapshot = Read-LogicalJsonArtifact -Path $beforePath
    $story = Read-StoryEventValue -Snapshot $beforeSnapshot
    if ([string]$beforeSnapshot.state_hash -ne
            [string]$step.effective_before_state_hash -or
        -not [bool]$story.active -or
        [string]$story.event_id -ne $ExpectedEventId -or
        [string]$story.location_id -ne "Farm" -or
        [string]$story.from_asset_name -ne "Data\Events\Farm" -or
        [string]$story.boundary_kind -ne "automatic_progress") {
        throw "Story interrupt for event $ExpectedEventId did not bind its exact native Farm event pre-state."
    }

    $queue = Read-LogicalJsonArtifact -Path $queuePath
    $queueItems = @($queue.items | Where-Object {
        [string]$_.queue_item_id -eq [string]$step.queue_item_id
    })
    if ($queueItems.Count -ne 1) {
        throw "Story interrupt for event $ExpectedEventId did not bind one compiled queue item."
    }
    $queueItem = $queueItems[0]
    $parameters = @($queueItem.normalized_command.parameters)
    $expectedCandidateId =
        "story-event:${ExpectedEventId}:" +
        "advance_story_event_automatic:continue"
    $candidateBindings = @($parameters | Where-Object {
        [string]$_.name -eq "precondition" -and
        [string]$_.value -eq "candidate_id:$expectedCandidateId"
    })
    $eventBindings = @($parameters | Where-Object {
        [string]$_.name -eq "story_event_id" -and
        [string]$_.value -eq $ExpectedEventId
    })
    $commandSteps = @($queueItem.normalized_command.steps | Where-Object {
        [string]$_.step_type -eq "advance_story_event" -and
        [string]$_.target -eq
            "Event:id=${ExpectedEventId}:command=0:response=none"
    })
    if ([string]$queueItem.option_id -ne "executor.advance_story_event" -or
        [string]$queueItem.normalized_command.option_id -ne
            "executor.advance_story_event" -or
        $candidateBindings.Count -ne 1 -or
        $eventBindings.Count -ne 1 -or
        $commandSteps.Count -ne 1) {
        throw "Story interrupt for event $ExpectedEventId did not preserve its compiled candidate identity."
    }

    return [ordered]@{
        step = $step
        story = $story
        before_snapshot_path = $beforePath
        compiled_queue_path = $queuePath
    }
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

function Get-TextSha256 {
    param([string] $Value)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.Convert]::ToHexString(
            $algorithm.ComputeHash(
                [System.Text.Encoding]::UTF8.GetBytes($Value))).ToLowerInvariant()
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

function Read-StoryEventValue {
    param($Snapshot)
    return Read-FieldValue $Snapshot "player" "story_event"
}

function Wait-StoryEventSnapshot {
    param(
        [string] $EventId,
        [bool] $ExpectedActive,
        [string] $ExpectedBoundaryKind = "",
        [int] $TimeoutSeconds = 60
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $snapshotUrl -TimeoutSeconds 15
            $story = Read-StoryEventValue -Snapshot $snapshot
            $active = [bool]$story.active
            $eventMatches = -not $ExpectedActive -or
                [string]$story.event_id -eq $EventId
            $boundaryMatches = [string]::IsNullOrWhiteSpace(
                    $ExpectedBoundaryKind) -or
                [string]$story.boundary_kind -eq $ExpectedBoundaryKind
            if ($active -eq $ExpectedActive -and
                $eventMatches -and
                $boundaryMatches) {
                return $snapshot
            }
            $lastState = "active=$active;event=$($story.event_id);" +
                "boundary=$($story.boundary_kind)"
        }
        catch { $lastState = $_.Exception.Message }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for native story event $EventId. Last state: $lastState"
}

function Get-ExactFarmEntryConnector {
    param($Snapshot)
    $routes = Read-FieldValue $Snapshot "locations" "route_connectors"
    $sourceLocation = [string]$routes.location_id
    if ([string]::IsNullOrWhiteSpace($sourceLocation)) {
        $sourceLocation = [string](Read-FieldValue `
            $Snapshot "player" "location_id")
    }
    if ($sourceLocation -notlike "FarmHouse*") {
        throw "Safe next-day action must begin in an owned FarmHouse, not $sourceLocation."
    }
    $matches = @($routes.connectors | Where-Object {
        [bool]$_.resolved -and
        [string]$_.target_location -eq "Farm"
    })
    if ($matches.Count -ne 1) {
        throw "Expected one exact FarmHouse -> Farm connector, found $($matches.Count)."
    }
    $connector = $matches[0]
    $kind = ([string]$connector.kind).ToLowerInvariant()
    $candidateId = "route:{0}:{1},{2}:{3}:to={4}" -f @(
        $sourceLocation,
        [int]$connector.tile_x,
        [int]$connector.tile_y,
        $kind,
        [string]$connector.target_location)
    if ($null -ne $connector.target_x -and
        $null -ne $connector.target_y) {
        $candidateId += ":arrival={0},{1}" -f @(
            [int]$connector.target_x,
            [int]$connector.target_y)
    }
    return [ordered]@{
        candidate_id = $candidateId
        source_location_id = $sourceLocation
        target_location_id = [string]$connector.target_location
        connector_kind = $kind
        tile_x = [int]$connector.tile_x
        tile_y = [int]$connector.tile_y
    }
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

function Get-TerminalBinSettlementValue {
    param($Snapshot)
    $bins = @(Read-FieldValue $Snapshot "farm" "shipping_bins")
    if ($bins.Count -eq 0) { throw "Transparent shipping-bin view is empty." }
    $values = @($bins | ForEach-Object {
        [int](@($_.contents | Where-Object {
            [string]$_.qualified_item_id -eq $TerminalQualifiedItemId
        } | ForEach-Object {
            [int]$_.total_sell_to_store_price
        } | Measure-Object -Sum).Sum)
    })
    if (@($values | Select-Object -Unique).Count -ne 1) {
        throw "Shared shipping-bin settlement views disagree for $TerminalQualifiedItemId."
    }
    return [int]$values[0]
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
        [string] $CandidateId = "",
        [string[]] $CandidateParameters = @(),
        [string[]] $AllowedExecutionStatuses = @("applied"),
        [ValidateRange(1, 32)]
        [int] $MaxQueueItemAttempts = 8,
        [ValidateRange(1, 32)]
        [int] $MaxLoopAttempts = 1
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
        "--iterations", $MaxLoopAttempts.ToString(),
        "--required-verified-actions", "1",
        "--max-queue-item-attempts", $MaxQueueItemAttempts.ToString(),
        "--sleep-ms", "0",
        "--use-daily-plan",
        "--daily-plan-max-candidates", "1",
        "--daily-plan-candidate-options", $OptionId,
        "--daily-plan-candidate-kind", $CandidateKind,
        "--after-snapshot-wait-ms", "1500",
        "--continue-after-blocked-queue-items"
    )) {
        $arguments.Add([string]$value)
    }
    if ($MaxLoopAttempts -eq 1) {
        $arguments.Add("--emit-queue-execution-receipt")
    }
    if (-not [string]::IsNullOrWhiteSpace($CandidateId)) {
        $arguments.Add("--daily-plan-candidate-id")
        $arguments.Add($CandidateId)
    }
    foreach ($parameter in $CandidateParameters) {
        $arguments.Add("--daily-plan-candidate-parameter")
        $arguments.Add($parameter)
    }

    $loopOutput = & dotnet $arguments
    $loopExitCode = $LASTEXITCODE
    $loopOutput | Set-Content `
        -LiteralPath (Join-Path $stepDirectory "loop.stdout.log") `
        -Encoding utf8
    if ($loopExitCode -notin @(0, 2)) {
        throw "Full Shipment loop step $Name failed with exit $loopExitCode."
    }

    $snapshotDirectory = Join-Path $stepLoopRoot (
        "runs\" + $RunId + "\live-snapshots")
    $reportPath = Join-Path $stepLoopRoot (
        "runs\" + $RunId + "\live-training-loop-report.json")
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "Full Shipment loop step $Name did not produce $reportPath."
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $finalAttempt = [int]$report.attempts_started
    if ($finalAttempt -lt 1 -or $finalAttempt -gt $MaxLoopAttempts) {
        throw "Full Shipment loop step $Name reported an invalid final attempt: " +
            "$finalAttempt;max=$MaxLoopAttempts."
    }
    $artifactOrdinal = $finalAttempt.ToString("D4")
    $queuePath = Join-Path $snapshotDirectory (
        "compiled-queue-$artifactOrdinal.json")
    $beforePath = Join-Path $snapshotDirectory (
        "before-snapshot-$artifactOrdinal.json")
    $executionPath = Join-Path $snapshotDirectory (
        "execution-$artifactOrdinal.json")
    $afterPath = Join-Path $snapshotDirectory (
        "after-snapshot-$artifactOrdinal.json")
    foreach ($path in @($queuePath, $beforePath, $executionPath, $afterPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Full Shipment loop step $Name did not produce $path."
        }
    }
    $execution = Get-Content -LiteralPath $executionPath -Raw |
        ConvertFrom-Json
    $expectedBlockedExit =
        $loopExitCode -eq 2 -and
        [string]$execution.status -eq "blocked" -and
        "blocked" -in $AllowedExecutionStatuses
    if (($loopExitCode -eq 2 -and -not $expectedBlockedExit) -or
        [string]$execution.status -notin $AllowedExecutionStatuses -or
        -not [bool]$execution.after_snapshot_fresh) {
        throw "Full Shipment loop step $Name did not produce an allowed fresh execution: " +
            "exit=$loopExitCode;status=$($execution.status);" +
            "allowed=$($AllowedExecutionStatuses -join ',')."
    }
    return [ordered]@{
        name = $Name
        candidate_id = $CandidateId
        queue_path = $queuePath
        before_path = $beforePath
        execution_path = $executionPath
        after_path = $afterPath
        execution = $execution
        after = Read-LogicalJsonArtifact -Path $afterPath
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
$nightTransactionUrl =
    $executorRoot + "/api/v1/training/end-of-night-transaction"
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
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -NoBuild:$NoBuild | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot `
    -NoBuild:$NoBuild | Out-Null
if (-not $NoBuild) {
    foreach ($project in @(
        "src\StardewAI.Backend\StardewAI.Backend.csproj",
        "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj",
        "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
    )) {
        dotnet build (Join-Path $ProjectRoot $project) `
            -c Release --no-restore --nologo | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Release build failed: $project"
        }
    }
}
$backendDll = Join-Path $ProjectRoot `
    "src\StardewAI.Backend\bin\Release\net8.0\StardewAI.Backend.dll"
$loopDll = Join-Path $ProjectRoot `
    "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
foreach ($output in @($backendDll, $loopDll, $bootstrapDll)) {
    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
        throw "Required Release output not found: $output"
    }
}
$candidateBinaryPaths = [ordered]@{
    transparent_bridge = Join-Path $gameDirectory `
        "Mods\StardewAI.TransparentBridge\StardewAI.TransparentBridge.dll"
    runtime_test_harness = Join-Path $gameDirectory `
        "Mods\StardewAI.RuntimeTestHarness\StardewAI.RuntimeTestHarness.dll"
    contracts = Join-Path $gameDirectory `
        "Mods\StardewAI.RuntimeTestHarness\StardewAI.Contracts.dll"
    runtime_primitives = Join-Path $gameDirectory `
        "Mods\StardewAI.RuntimeTestHarness\StardewAI.RuntimePrimitives.dll"
    backend = $backendDll
    live_training_loop = $loopDll
    goal_conditioned_bootstrap = $bootstrapDll
}
$candidateBinaryIdentity = [ordered]@{}
foreach ($entry in $candidateBinaryPaths.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Candidate binary not found: $($entry.Value)"
    }
    $candidateBinaryIdentity[$entry.Key] = ((Get-FileHash `
        -LiteralPath $entry.Value -Algorithm SHA256).Hash).ToLowerInvariant()
}
$candidateRuntimeBundlePaths = [ordered]@{
    transparent_bridge_mod = Join-Path $gameDirectory `
        "Mods\StardewAI.TransparentBridge"
    runtime_test_harness_mod = Join-Path $gameDirectory `
        "Mods\StardewAI.RuntimeTestHarness"
    backend_output = Split-Path -Parent $backendDll
    live_training_loop_output = Split-Path -Parent $loopDll
    goal_conditioned_bootstrap_output = Split-Path -Parent $bootstrapDll
}
$candidateRuntimeBundleIdentity = [ordered]@{}
foreach ($entry in $candidateRuntimeBundlePaths.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Container)) {
        throw "Candidate runtime bundle not found: $($entry.Value)"
    }
    $candidateRuntimeBundleIdentity[$entry.Key] =
        Get-DirectoryContentHash -Path $entry.Value
}
$projectHeadOutput = & git -C $ProjectRoot rev-parse HEAD 2>$null
if ($LASTEXITCODE -ne 0 -or @($projectHeadOutput).Count -ne 1) {
    throw "Could not resolve candidate project HEAD."
}
$projectHeadCommit = [string]($projectHeadOutput | Select-Object -First 1)
$projectStatusOutput = @(& git -C $ProjectRoot status `
    --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve candidate project worktree status."
}
$projectWorktreeDirty = $projectStatusOutput.Count -gt 0
$projectWorktreeStatusSha256 = Get-TextSha256 -Value (
    [string]::Join("`n", $projectStatusOutput))
if ($OwnerComposite -and $projectWorktreeDirty) {
    throw "Owner composite requires one committed clean-worktree candidate."
}
$candidateScriptSha256 = ((Get-FileHash -LiteralPath $PSCommandPath `
    -Algorithm SHA256).Hash).ToLowerInvariant()

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
    $backend = Start-Process dotnet -ArgumentList @($backendDll) `
        -WorkingDirectory (Split-Path -Parent $backendDll) `
        -WindowStyle Hidden `
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
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = if ($VisibleGame) { "0" } else { "1" }
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $gameWindowStyle = if ($VisibleGame) { "Normal" } else { "Hidden" }
    $game = Start-Process -FilePath $smapi `
        -WorkingDirectory $gameDirectory -WindowStyle $gameWindowStyle `
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

    $shippingCandidateId = ""
    $expectedSettlementIncome = 0
    if ($NoShipment) {
        $shipping = [ordered]@{ after = $fixtureSnapshot }
    }
    else {
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
    }

    $sleepFixtureOptionId = if (
        [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff)) {
        "debug.prepare_full_shipment_terminal_sleep"
    }
    else {
        "debug.prepare_native_story_handoff"
    }
    $sleepFixtureRequest = New-BaseRequest `
        -Snapshot $shipping.after `
        -OptionId $sleepFixtureOptionId `
        -QueueItemId "$RunId.fixture.sleep"
    if (-not [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff)) {
        $sleepFixtureRequest.story_event_boundary_kind =
            $PostSaveStoryHandoff
    }
    if ($PendingProfessionChoiceId -ge 0) {
        $sleepFixtureRequest.profession_choice_id =
            $PendingProfessionChoiceId
    }
    if ($PendingOrdinaryLevelCount -gt 0) {
        $sleepFixtureRequest.debug_end_of_night_ordinary_level_count =
            $PendingOrdinaryLevelCount
    }
    if (-not [string]::IsNullOrWhiteSpace($PendingFarmEvent)) {
        $sleepFixtureRequest.debug_end_of_night_farm_event =
            $PendingFarmEvent
    }
    if (-not [string]::IsNullOrWhiteSpace($EndOfNightNegativeProfile)) {
        $sleepFixtureRequest.debug_end_of_night_negative_profile =
            $EndOfNightNegativeProfile
    }
    $sleepFixture = Invoke-JsonPost `
        -Url $nativeExecutorUrl -Body $sleepFixtureRequest
    Write-JsonFile `
        -Path (Join-Path $artifactDirectory "sleep-fixture-setup.json") `
        -Value $sleepFixture
    if ($sleepFixture.status -ne "applied" -or
        $sleepFixture.primitive_verification_status -ne "verified") {
        throw "Native sleep fixture setup failed for $sleepFixtureOptionId."
    }
    $sleepReadySnapshot = Wait-WorldSnapshot -TimeoutSeconds 60
    $moneyBeforeSettlement = [int](Read-FieldValue `
        $sleepReadySnapshot "player" "money")
    if (-not $NoShipment) {
        $expectedSettlementIncome = Get-TerminalBinSettlementValue `
            -Snapshot $sleepReadySnapshot
    }
    Assert-TerminalState `
        -Snapshot $sleepReadySnapshot `
        -ExpectedShippedCount 153 `
        -ExpectedMissingCount 1 `
        -ExpectedTerminalShippedCount 0 `
        -ExpectedTerminalBinCount $(if ($NoShipment) { 0 } else { 1 }) `
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
        ) `
        -AllowedExecutionStatuses $(if ($PendingProfessionChoiceId -ge 0) {
            @("blocked")
        } else {
            @("applied")
        }) `
        -MaxQueueItemAttempts $(if ($PendingProfessionChoiceId -ge 0) {
            1
        } else {
            8
        })

    $current = $sleep.after
    $selectedProfessionSkillId = -1
    $selectedProfessionLevel = -1
    $selectedProfessionTitle = ""
    if (-not [string]::IsNullOrWhiteSpace($EndOfNightNegativeProfile)) {
        $negativeDeadline = (Get-Date).AddSeconds(45)
        $tracker = $null
        while ((Get-Date) -lt $negativeDeadline) {
            $tracker = Invoke-JsonGet `
                -Url $nightTransactionUrl -TimeoutSeconds 15
            if ([string]$tracker.status -eq "blocked") {
                break
            }
            Start-Sleep -Milliseconds 250
        }

        if ($null -eq $tracker -or [string]$tracker.status -ne "blocked") {
            throw "End-of-night negative profile did not reach a bounded block: $EndOfNightNegativeProfile"
        }
        $failureReason = [string]$tracker.failure_reason
        $expectedFailurePrefix = if (
            $EndOfNightNegativeProfile -eq
                "unknown_menu_transition_timeout") {
            "end_of_night_transition_timeout:menu:QuestLog:"
        }
        else {
            "end_of_night_transaction_overall_timeout"
        }
        if (-not $failureReason.StartsWith(
                $expectedFailurePrefix,
                [StringComparison]::Ordinal)) {
            throw "End-of-night negative profile blocked for the wrong reason: " +
                "$failureReason;expected_prefix=$expectedFailurePrefix"
        }
        $negativeTransitionKeys = @($tracker.transitions | ForEach-Object {
            [string]$_.key
        })
        if (-not [bool]$tracker.debug_negative_fixture_injected -or
            -not [bool]$tracker.saw_native_save_commit -or
            -not [bool]$tracker.saw_expected_new_day -or
            -not [bool]$tracker.saw_safe_new_day_ready_state -or
            [string]$tracker.debug_negative_profile -ne
                $EndOfNightNegativeProfile -or
            [int]$tracker.expected_total_days -ne
                ([int]$tracker.start_total_days + 1) -or
            [int]$tracker.completed_total_days -ne
                [int]$tracker.expected_total_days -or
            @($negativeTransitionKeys | Where-Object {
                $_ -like "menu:QuestLog:*"
            }).Count -ne 1 -or
            -not [string]::IsNullOrWhiteSpace(
                [string]$tracker.safe_next_day_option_id)) {
            throw "End-of-night negative profile lost its native saved-night or unknown-menu evidence."
        }
        $negativeEvidence = Get-VerifiedEndOfNightEvidence `
            -Tracker $tracker `
            -ExpectedStatus "blocked" `
            -ExpectedRunId $RunId `
            -TrainingOutputDirectory $trainingOutputDirectory

        $sourceSaveHashAfter = Get-DirectoryContentHash -Path $sourceSavePath
        if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
            throw "Source save changed during isolated negative end-of-night smoke."
        }
        $negativeSummary = [ordered]@{
            schema_version =
                "stardewai.runtime_end_of_night_negative_smoke.v1"
            status = "passed"
            run_id = $RunId
            matrix_scenario = $true
            negative_profile = $EndOfNightNegativeProfile
            project_head_commit = $projectHeadCommit
            project_worktree_dirty = $projectWorktreeDirty
            project_worktree_status_sha256 =
                $projectWorktreeStatusSha256
            candidate_script_sha256 = $candidateScriptSha256
            candidate_binary_sha256 = $candidateBinaryIdentity
            candidate_runtime_bundle_sha256 =
                $candidateRuntimeBundleIdentity
            source_save_slot = $SaveSlot
            isolated_save_root = $isolatedSavesPath
            source_save_preserved = $true
            source_save_hash_before = $sourceSaveHashBefore
            source_save_hash_after = $sourceSaveHashAfter
            transaction_id = [string]$tracker.transaction_id
            transaction_status = [string]$tracker.status
            transaction_failure_reason = $failureReason
            transaction_evidence_path = [string]$negativeEvidence.path
            transaction_evidence_sha256 = [string]$negativeEvidence.sha256
            native_save_committed_at =
                [string]$tracker.native_save_committed_at
            start_total_days = [int]$tracker.start_total_days
            expected_total_days = [int]$tracker.expected_total_days
            start_year = [int]$tracker.start_year
            start_season = [string]$tracker.start_season
            start_day = [int]$tracker.start_day
            start_time = [int]$tracker.start_time
            completed_total_days = [int]$tracker.completed_total_days
            completed_year = [int]$tracker.completed_year
            completed_season = [string]$tracker.completed_season
            completed_day = [int]$tracker.completed_day
            completed_time = [int]$tracker.completed_time
            transition_count = @($tracker.transitions).Count
            transition_keys = $negativeTransitionKeys
            debug_negative_fixture_injected =
                [bool]$tracker.debug_negative_fixture_injected
            saw_native_save_commit = [bool]$tracker.saw_native_save_commit
            saw_expected_new_day = [bool]$tracker.saw_expected_new_day
            saw_safe_new_day_ready_state =
                [bool]$tracker.saw_safe_new_day_ready_state
            cleanup_boundary =
                "isolated_game_process_terminated_by_finally"
            training_label_eligible = $false
            owner_visual_gate_required = $false
            owner_visual_gate_passed = $false
            visual_hold_seconds = $NegativeVisualHoldSeconds
        }
        Write-JsonFile `
            -Path (Join-Path $artifactDirectory "summary.json") `
            -Value $negativeSummary
        $negativeSummary | ConvertTo-Json -Depth 48
        if ($NegativeVisualHoldSeconds -gt 0) {
            Start-Sleep -Seconds $NegativeVisualHoldSeconds
        }
        return
    }
    if ($PendingProfessionChoiceId -ge 0) {
        $sleepReasons = @($sleep.execution.block_reasons)
        if ($sleepReasons -notcontains
                "post_sleep_profession_choice_requires_planned_selection") {
            throw "Compound night did not yield at the planned profession boundary."
        }
        if ([string]::IsNullOrWhiteSpace(
                [string]$sleep.execution.end_of_night_transaction_id) -or
            [string]$sleep.execution.end_of_night_transaction_status -ne
                "awaiting_planned_profession_selection") {
            throw "Compound night did not preserve transaction lineage at profession yield."
        }
        $professionReadySnapshot = $sleep.after
        Assert-TerminalState `
            -Snapshot $professionReadySnapshot `
            -ExpectedShippedCount $(if ($NoShipment) { 153 } else { 154 }) `
            -ExpectedMissingCount $(if ($NoShipment) { 1 } else { 0 }) `
            -ExpectedTerminalShippedCount $(if ($NoShipment) { 0 } else { 1 }) `
            -ExpectedTerminalBinCount 0 `
            -ExpectedAchievement (-not $NoShipment) `
            -Phase "profession-yield"

        $professionState = Read-FieldValue `
            $professionReadySnapshot "menus" "menu_specific_state"
        $activeProfessionMenu = Read-FieldValue `
            $professionReadySnapshot "menus" "active_menu"
        $professionChoices = @($professionState.profession_choices)
        $selectedChoice = @($professionChoices | Where-Object {
            [int]$_.profession_id -eq $PendingProfessionChoiceId
        })
        if ([string]$activeProfessionMenu.type -ne "LevelUpMenu" -or
            -not [bool]$professionState.is_profession_chooser -or
            $selectedChoice.Count -ne 1) {
            throw "Native profession menu did not offer the requested exact choice."
        }
        $selectedProfessionSkillId = [int]$professionState.current_skill
        $selectedProfessionLevel = [int]$professionState.current_level
        $selectedProfessionTitle = [string]$selectedChoice[0].title
        if ([string]::IsNullOrWhiteSpace($selectedProfessionTitle)) {
            throw "Native profession menu did not expose the selected profession title."
        }
        $professionCandidateId = "skills:profession:{0}:{1}:{2}" -f @(
            $selectedProfessionSkillId,
            $selectedProfessionLevel,
            $PendingProfessionChoiceId)
        $profession = Invoke-LoopStep `
            -Name "03-planned-profession-selection" `
            -Snapshot $professionReadySnapshot `
            -OptionId "skills.choose_profession" `
            -CandidateKind "choose_profession" `
            -CandidateId $professionCandidateId `
            -CandidateParameters @(
                "profession_choice_id=$PendingProfessionChoiceId",
                "profession_choice_source=owner_visual_gate_exact_offered_choice"
            ) `
            -MaxLoopAttempts 8

        $current = $profession.after
        $recoveryOrdinal = 0
        $deadline = (Get-Date).AddMinutes(4)
        while ((Get-Date) -lt $deadline) {
            $tracker = Invoke-JsonGet `
                -Url $nightTransactionUrl -TimeoutSeconds 15
            if ([string]$tracker.status -eq "blocked") {
                throw "Compound night tracker blocked: $($tracker.failure_reason)"
            }
            if ([string]$tracker.status -eq
                    "ready_for_safe_next_day_action") {
                break
            }

            $current = Wait-WorldSnapshot -TimeoutSeconds 30
            $menu = Read-FieldValue $current "menus" "active_menu"
            $menuType = [string]$menu.type
            if ($menuType -eq "LevelUpMenu" -or
                $menuType -eq "ShippingMenu") {
                $specific = Read-FieldValue `
                    $current "menus" "menu_specific_state"
                if ($menuType -eq "LevelUpMenu" -and
                    [bool]$specific.is_profession_chooser) {
                    throw "Compound fixture exposed an unplanned second profession choice."
                }
                $recoveryOrdinal++
                $recovery = Invoke-LoopStep `
                    -Name ("04-native-menu-recovery-{0:D2}" -f $recoveryOrdinal) `
                    -Snapshot $current `
                    -OptionId "recovery.stabilize_day" `
                    -CandidateKind "recovery_close_menu" `
                    -CandidateId "recovery:close_blocking_menu"
                $current = $recovery.after
                continue
            }
            if ($menuType -eq "SaveGameMenu" -or
                [string]::IsNullOrWhiteSpace($menuType) -or
                $menuType -eq "none") {
                Start-Sleep -Milliseconds 500
                continue
            }
            throw "Unsupported compound-night menu during smoke: $menuType"
        }
    }

    $tracker = Invoke-JsonGet -Url $nightTransactionUrl -TimeoutSeconds 15
    $expectedShippingSummary = -not $NoShipment
    $expectedProfessionYield = $PendingProfessionChoiceId -ge 0
    if ([string]$tracker.status -ne "ready_for_safe_next_day_action" -or
        -not [bool]$tracker.saw_native_save_commit -or
        [bool]$tracker.saw_shipping_summary -ne
            $expectedShippingSummary -or
        [bool]$tracker.yielded_for_planned_profession -ne
            $expectedProfessionYield) {
        throw "End-of-night transaction did not reach the expected saved next-day handoff."
    }
    if ($NoShipment -and -not [bool]$tracker.saw_save_menu) {
        throw "No-shipment night did not traverse native SaveGameMenu."
    }
    if (-not [string]::IsNullOrWhiteSpace($PendingFarmEvent) -and
        -not [bool]$tracker.saw_farm_event) {
        throw "Requested native FarmEvent was not observed."
    }
    $transitionKeys = @($tracker.transitions | ForEach-Object {
        [string]$_.key
    })
    $ordinaryTransitions = @($tracker.transitions | Where-Object {
        [string]$_.key -like "menu:LevelUpMenu:ordinary:*"
    })
    $professionTransitions = @($tracker.transitions | Where-Object {
        [string]$_.key -like "menu:LevelUpMenu:profession:*"
    })
    $expectedProfessionTransitionCount = if (
        $PendingProfessionChoiceId -ge 0) { 1 } else { 0 }
    if ($ordinaryTransitions.Count -ne $PendingOrdinaryLevelCount -or
        $professionTransitions.Count -ne
            $expectedProfessionTransitionCount) {
        throw "Native level-menu transition count mismatch: ordinary=" +
            "$($ordinaryTransitions.Count);expected=$PendingOrdinaryLevelCount;" +
            "profession=$($professionTransitions.Count);" +
            "expected=$expectedProfessionTransitionCount."
    }
    $terminalMenuTransitions = if ($NoShipment) {
        @($tracker.transitions | Where-Object {
            [string]$_.key -like "menu:SaveGameMenu:*"
        })
    }
    else {
        @($tracker.transitions | Where-Object {
            [string]$_.key -like "menu:ShippingMenu:*"
        })
    }
    if ($terminalMenuTransitions.Count -ne 1) {
        throw "Native terminal menu transition count mismatch: " +
            "$($terminalMenuTransitions.Count);expected=1."
    }
    $levelTransitions = @($ordinaryTransitions + $professionTransitions)
    $terminalMenuOrdinal = [int]$terminalMenuTransitions[0].ordinal
    if ($levelTransitions.Count -gt 0) {
        $firstLevelOrdinal = [int]($levelTransitions |
            Measure-Object -Property ordinal -Minimum).Minimum
        $lastLevelOrdinal = [int]($levelTransitions |
            Measure-Object -Property ordinal -Maximum).Maximum
        if ($lastLevelOrdinal -ge $terminalMenuOrdinal) {
            throw "Native level menus did not precede the shipping/save terminal menu."
        }
    }
    if ($PendingFarmEvent -eq "day_31_earthquake" -and
        $transitionKeys -notcontains
            "farm_event:SoundInTheNightEvent") {
        throw "Day-31 fixture did not traverse native SoundInTheNightEvent."
    }
    if ($PendingFarmEvent -eq "day_31_earthquake") {
        $eventTransition = @($tracker.transitions | Where-Object {
            [string]$_.key -eq "farm_event:SoundInTheNightEvent"
        })
        if ($eventTransition.Count -ne 1) {
            throw "Native FarmEvent transition count was not exactly one."
        }
        if ($levelTransitions.Count -gt 0 -and
            [int]$eventTransition[0].ordinal -ge $firstLevelOrdinal) {
            throw "Native FarmEvent did not precede the queued level menus."
        }
        if ([int]$tracker.start_year -ne 1 -or
            [string]$tracker.start_season -ne "summer" -or
            [int]$tracker.start_day -ne 2 -or
            [int]$tracker.start_total_days -ne 29 -or
            [int]$tracker.completed_year -ne 1 -or
            [string]$tracker.completed_season -ne "summer" -or
            [int]$tracker.completed_day -ne 3 -or
            [int]$tracker.completed_total_days -ne 30) {
            throw "Day-31 event fixture did not preserve the native Summer 2 -> 3 date relation."
        }
    }
    $current = Wait-WorldSnapshot -TimeoutSeconds 30

    $skillsAfterNight = Read-FieldValue `
        $current "player" "skills_detail"
    if ($PendingProfessionChoiceId -ge 0) {
        if (@($skillsAfterNight.profession_ids) -notcontains
                $PendingProfessionChoiceId) {
            throw "Selected profession was not committed to native player state."
        }
    }
    if (($PendingProfessionChoiceId -ge 0 -or
         $PendingOrdinaryLevelCount -gt 0) -and
        @($skillsAfterNight.new_levels).Count -ne 0) {
        throw "Pending native level screens remained after the end-of-night transaction."
    }

    $farmEntryConnector = Get-ExactFarmEntryConnector -Snapshot $current
    $nextDay = Invoke-LoopStep `
        -Name "05-safe-next-day-controller-action" `
        -Snapshot $current `
        -OptionId "exploration.visit_location" `
        -CandidateKind "route_connector_tile" `
        -CandidateId $farmEntryConnector.candidate_id `
        -MaxLoopAttempts 8
    $safeNextDaySteps = @($nextDay.execution.step_results | Where-Object {
        [string]$_.end_of_night_transaction_status -eq "complete"
    })
    if ($safeNextDaySteps.Count -ne 1) {
        throw "Safe next-day queue did not expose one exact terminal transaction receipt."
    }
    $safeNextDayStep = $safeNextDaySteps[0]
    $nextDayExecutionOptionId = [string]$safeNextDayStep.option_id
    if ($nextDayExecutionOptionId -notin @(
            "executor.move_to_tile",
            "executor.traverse_connector")) {
        throw "Safe next-day controller action did not execute a verified movement primitive: " +
            $nextDayExecutionOptionId
    }
    if ([string]$safeNextDayStep.source_planning_option_id -ne
            "exploration.visit_location") {
        throw "Safe next-day execution receipt lost its planning lineage."
    }
    $tracker = Invoke-JsonGet -Url $nightTransactionUrl -TimeoutSeconds 15
    if ([string]$tracker.status -ne "complete" -or
        [string]$safeNextDayStep.end_of_night_transaction_id -ne
            [string]$tracker.transaction_id -or
        [string]$tracker.safe_next_day_planning_option_id -ne
            "exploration.visit_location" -or
        [string]$tracker.safe_next_day_option_id -ne
            $nextDayExecutionOptionId -or
        [string]$tracker.safe_next_day_queue_id -ne
            [string]$safeNextDayStep.queue_id -or
        [string]$tracker.safe_next_day_queue_item_id -ne
            [string]$safeNextDayStep.queue_item_id -or
        [int]$tracker.expected_total_days -ne
            ([int]$tracker.start_total_days + 1) -or
        [int]$tracker.completed_total_days -ne
            [int]$tracker.expected_total_days -or
        [int]$tracker.completed_time -lt 600 -or
        [int]$tracker.completed_time -ge 700) {
        throw "End-of-night transaction did not record a verified next-day controller action."
    }
    $transactionEvidence = Get-VerifiedEndOfNightEvidence `
        -Tracker $tracker `
        -ExpectedStatus "complete" `
        -ExpectedRunId $RunId `
        -TrainingOutputDirectory $trainingOutputDirectory
    $transitionKeys = @($tracker.transitions | ForEach-Object {
        [string]$_.key
    })
    $postActionSnapshot = $nextDay.after
    $storyHandoffSummary = $null
    if (-not [string]::IsNullOrWhiteSpace($PostSaveStoryHandoff)) {
        if ($PostSaveStoryHandoff -eq "native_kent_post_save") {
            $completedInterrupt = Get-VerifiedCompletedStoryInterrupt `
                -LoopStep $nextDay `
                -ExpectedEventId "63"
            if ($null -ne $completedInterrupt) {
                $storyHandoffSummary = [ordered]@{
                    profile = $PostSaveStoryHandoff
                    event_id = "63"
                    source_asset =
                        [string]$completedInterrupt.story.from_asset_name
                    entry_boundary_kind =
                        [string]$completedInterrupt.story.boundary_kind
                    execution_status =
                        [string]$completedInterrupt.step.status
                    completion_source =
                        "safe_next_day_runtime_priority_interrupt"
                    terminal_state = "native_event_completed"
                    unsafe_question_block_reason = "not_applicable"
                }
            }
            else {
                $storySnapshot = Wait-StoryEventSnapshot `
                    -EventId "63" `
                    -ExpectedActive $true `
                    -TimeoutSeconds 60
                $story = Read-StoryEventValue -Snapshot $storySnapshot
                if ([string]$story.location_id -ne "Farm" -or
                    [string]$story.from_asset_name -ne
                        "Data\Events\Farm" -or
                    [string]$story.boundary_kind -ne
                        "automatic_progress") {
                    throw "Kent post-save handoff was not an automatic native Farm data event."
                }
                $storyStep = Invoke-LoopStep `
                    -Name "06-native-kent-story-handoff" `
                    -Snapshot $storySnapshot `
                    -OptionId "story.advance_event" `
                    -CandidateKind "advance_story_event_automatic" `
                    -CandidateId (
                        "story-event:63:" +
                        "advance_story_event_automatic:continue")
                $postActionSnapshot = Wait-StoryEventSnapshot `
                    -EventId "63" `
                    -ExpectedActive $false `
                    -TimeoutSeconds 90
                $storyHandoffSummary = [ordered]@{
                    profile = $PostSaveStoryHandoff
                    event_id = "63"
                    source_asset = [string]$story.from_asset_name
                    entry_boundary_kind = [string]$story.boundary_kind
                    execution_status = [string]$storyStep.execution.status
                    completion_source = "dedicated_story_handoff_step"
                    terminal_state = "native_event_completed"
                    unsafe_question_block_reason = "not_applicable"
                }
            }
        }
        else {
            $storySnapshot = Wait-StoryEventSnapshot `
                -EventId "1590166" `
                -ExpectedActive $true `
                -TimeoutSeconds 60
            $story = Read-StoryEventValue -Snapshot $storySnapshot
            if ([string]$story.location_id -ne "Farm" -or
                [string]$story.from_asset_name -ne "Data\Events\Farm") {
                throw "Post-save story handoff was not a native Farm data event."
            }
            if ([string]$story.boundary_kind -eq "automatic_progress") {
                $toDecision = Invoke-LoopStep `
                    -Name "06-native-cat-story-to-question" `
                    -Snapshot $storySnapshot `
                    -OptionId "story.advance_event" `
                    -CandidateKind "advance_story_event_automatic" `
                    -CandidateId (
                        "story-event:1590166:" +
                        "advance_story_event_automatic:continue")
                $storySnapshot = $toDecision.after
            }
            $storySnapshot = Wait-StoryEventSnapshot `
                -EventId "1590166" `
                -ExpectedActive $true `
                -ExpectedBoundaryKind "dialogue_decision" `
                -TimeoutSeconds 90
            $story = Read-StoryEventValue -Snapshot $storySnapshot
            if ([string]::IsNullOrWhiteSpace(
                    [string]$story.dialogue_question_key) -or
                @($story.dialogue_responses).Count -eq 0) {
                throw "Cat adoption did not expose its native typed question."
            }
            $adoptionBefore = Read-FieldValue `
                $storySnapshot "farm" "initial_pet_adoption"
            if ([string]$adoptionBefore.event_id -ne "1590166" -or
                [string]$adoptionBefore.projection_status -ne
                    "complete_locked_base_1.6.15" -or
                -not [bool]$adoptionBefore.event_key_matches_locked_base -or
                -not [bool]$adoptionBefore.base_english_event_script_matches_locked_base -or
                -not [bool]$adoptionBefore.event_active -or
                [bool]$adoptionBefore.has_initial_pet -or
                [bool]$adoptionBefore.event_seen) {
                throw "Cat adoption projection drifted before the unsafe request."
            }

            $unsafeRequest = New-BaseRequest `
                -Snapshot $storySnapshot `
                -OptionId "executor.advance_story_event" `
                -QueueItemId "$RunId.story.unsafe-question"
            $unsafeRequest.source_planning_option_id = "story.advance_event"
            $unsafeRequest.story_event_projection_fingerprint =
                [string]$story.projection_fingerprint
            $unsafeRequest.story_event_id = [string]$story.event_id
            $unsafeRequest.story_event_location_id = [string]$story.location_id
            $unsafeRequest.story_event_command_index =
                [int]$story.current_command_index
            $unsafeRequest.story_event_command_raw =
                [string]$story.current_command_raw
            $unsafeRequest.story_event_boundary_kind =
                [string]$story.boundary_kind
            $unsafeRequest.story_event_max_runtime_ticks = 600
            $unsafeRequest.story_event_question_key =
                [string]$story.dialogue_question_key
            $unsafeResult = Invoke-JsonPost `
                -Url $nativeExecutorUrl -Body $unsafeRequest
            Write-JsonFile `
                -Path (Join-Path $artifactDirectory `
                    "unsafe-story-question-result.json") `
                -Value $unsafeResult
            if ([string]$unsafeResult.status -ne "blocked" -or
                [string]$unsafeResult.primitive_verification_status -ne
                    "blocked" -or
                @($unsafeResult.block_reasons) -notcontains
                    "story_event_dialogue_response_mismatch") {
                throw "Unsafe native story question was not rejected fail-closed."
            }

            $postActionSnapshot = Wait-StoryEventSnapshot `
                -EventId "1590166" `
                -ExpectedActive $true `
                -ExpectedBoundaryKind "dialogue_decision" `
                -TimeoutSeconds 30
            $storyAfterUnsafe = Read-StoryEventValue `
                -Snapshot $postActionSnapshot
            $adoptionAfter = Read-FieldValue `
                $postActionSnapshot "farm" "initial_pet_adoption"
            if ([string]$storyAfterUnsafe.projection_fingerprint -ne
                    [string]$story.projection_fingerprint -or
                [bool]$adoptionAfter.has_initial_pet -or
                [bool]$adoptionAfter.event_seen -or
                -not [bool]$adoptionAfter.event_active) {
                throw "Unsafe story request changed the native decision boundary."
            }
            $storyHandoffSummary = [ordered]@{
                profile = $PostSaveStoryHandoff
                event_id = "1590166"
                source_asset = [string]$story.from_asset_name
                entry_boundary_kind = [string]$story.boundary_kind
                execution_status = [string]$unsafeResult.status
                terminal_state = "native_question_held_for_model_decision"
                unsafe_question_block_reason =
                    "story_event_dialogue_response_mismatch"
            }
        }
    }
    $sleep.after = $postActionSnapshot
    $expectedPostShippedCount = if ($NoShipment) { 153 } else { 154 }
    $expectedPostMissingCount = if ($NoShipment) { 1 } else { 0 }
    $expectedPostTerminalCount = if ($NoShipment) { 0 } else { 1 }
    Assert-TerminalState `
        -Snapshot $sleep.after `
        -ExpectedShippedCount $expectedPostShippedCount `
        -ExpectedMissingCount $expectedPostMissingCount `
        -ExpectedTerminalShippedCount $expectedPostTerminalCount `
        -ExpectedTerminalBinCount 0 `
        -ExpectedAchievement (-not $NoShipment) `
        -Phase "post-settlement"
    $moneyAfterSettlement = [int](Read-FieldValue `
        $sleep.after "player" "money")
    if ($moneyAfterSettlement -ne
            $moneyBeforeSettlement + $expectedSettlementIncome) {
        throw "Native overnight money settlement mismatch: before=" +
            "$moneyBeforeSettlement;expected_income=$expectedSettlementIncome;" +
            "after=$moneyAfterSettlement."
    }

    if ($matrixScenario) {
        $sourceSaveHashAfter = Get-DirectoryContentHash -Path $sourceSavePath
        if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
            throw "Source save changed during isolated compound-night smoke."
        }
        $compoundSummary = [ordered]@{
            schema_version =
                "stardewai.runtime_compound_end_of_night_smoke.v1"
            status = "passed"
            run_id = $RunId
            matrix_scenario = $true
            owner_composite = [bool]$OwnerComposite
            project_head_commit = $projectHeadCommit
            project_worktree_dirty = $projectWorktreeDirty
            project_worktree_status_sha256 =
                $projectWorktreeStatusSha256
            candidate_script_sha256 = $candidateScriptSha256
            candidate_binary_sha256 = $candidateBinaryIdentity
            candidate_runtime_bundle_sha256 =
                $candidateRuntimeBundleIdentity
            source_save_slot = $SaveSlot
            isolated_save_root = $isolatedSavesPath
            source_save_preserved = $true
            source_save_hash_before = $sourceSaveHashBefore
            source_save_hash_after = $sourceSaveHashAfter
            terminal_item_qualified_id = $TerminalQualifiedItemId
            game_version = [string]$current.game_version
            smapi_version = [string]$current.smapi_version
            bridge_version = [string]$current.bridge_version
            installed_mods = @($current.installed_mods)
            no_shipment = [bool]$NoShipment
            money_before_settlement = $moneyBeforeSettlement
            expected_settlement_income = $expectedSettlementIncome
            money_after_settlement = $moneyAfterSettlement
            profession_choice_id = $PendingProfessionChoiceId
            profession_skill_id = $selectedProfessionSkillId
            profession_level = $selectedProfessionLevel
            profession_title = $selectedProfessionTitle
            profession_requested = $PendingProfessionChoiceId -ge 0
            profession_committed =
                $PendingProfessionChoiceId -lt 0 -or
                (@($skillsAfterNight.profession_ids) -contains
                    $PendingProfessionChoiceId)
            pending_new_level_count_after =
                @($skillsAfterNight.new_levels).Count
            ordinary_level_screen_count_requested =
                $PendingOrdinaryLevelCount
            farm_event_requested = $PendingFarmEvent
            post_save_story_handoff_requested = $PostSaveStoryHandoff
            post_save_story_handoff = $storyHandoffSummary
            post_save_story_visual_hold_seconds =
                $PostSaveStoryVisualHoldSeconds
            safe_next_day_connector = $farmEntryConnector
            transaction_id = [string]$tracker.transaction_id
            transaction_status = [string]$tracker.status
            native_save_committed_at =
                [string]$tracker.native_save_committed_at
            transaction_completed_at = [string]$tracker.completed_at
            start_total_days = [int]$tracker.start_total_days
            expected_total_days = [int]$tracker.expected_total_days
            start_year = [int]$tracker.start_year
            start_season = [string]$tracker.start_season
            start_day = [int]$tracker.start_day
            start_time = [int]$tracker.start_time
            completed_total_days = [int]$tracker.completed_total_days
            completed_year = [int]$tracker.completed_year
            completed_season = [string]$tracker.completed_season
            completed_day = [int]$tracker.completed_day
            completed_time = [int]$tracker.completed_time
            transition_count = @($tracker.transitions).Count
            transition_keys = $transitionKeys
            ordinary_level_transition_count = $ordinaryTransitions.Count
            profession_transition_count = $professionTransitions.Count
            terminal_menu_transition_count = $terminalMenuTransitions.Count
            saw_shipping_summary = [bool]$tracker.saw_shipping_summary
            saw_save_menu = [bool]$tracker.saw_save_menu
            saw_farm_event = [bool]$tracker.saw_farm_event
            saw_native_save_commit = [bool]$tracker.saw_native_save_commit
            saw_expected_new_day = [bool]$tracker.saw_expected_new_day
            saw_safe_new_day_ready_state =
                [bool]$tracker.saw_safe_new_day_ready_state
            safe_next_day_option_id =
                [string]$tracker.safe_next_day_option_id
            safe_next_day_planning_option_id =
                [string]$tracker.safe_next_day_planning_option_id
            safe_next_day_queue_id =
                [string]$tracker.safe_next_day_queue_id
            safe_next_day_queue_item_id =
                [string]$tracker.safe_next_day_queue_item_id
            training_label_eligible = $false
            owner_visual_gate_required = [bool]$OwnerComposite
            owner_visual_gate_passed = $false
            transaction_evidence_path = [string]$transactionEvidence.path
            transaction_evidence_sha256 =
                [string]$transactionEvidence.sha256
        }
        Write-JsonFile `
            -Path (Join-Path $artifactDirectory "summary.json") `
            -Value $compoundSummary
        $compoundSummary | ConvertTo-Json -Depth 48
        if ($PostSaveStoryVisualHoldSeconds -gt 0) {
            Start-Sleep -Seconds $PostSaveStoryVisualHoldSeconds
        }
        return
    }

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
        project_head_commit = $projectHeadCommit
        project_worktree_dirty = $projectWorktreeDirty
        project_worktree_status_sha256 = $projectWorktreeStatusSha256
        candidate_script_sha256 = $candidateScriptSha256
        candidate_binary_sha256 = $candidateBinaryIdentity
        candidate_runtime_bundle_sha256 =
            $candidateRuntimeBundleIdentity
        source_save_slot = $SaveSlot
        isolated_save_root = $isolatedSavesPath
        source_save_preserved = $true
        source_save_hash_before = $sourceSaveHashBefore
        source_save_hash_after = $sourceSaveHashAfter
        authority_game_version = [string]$authority.game_version
        game_version = [string]$sleep.after.game_version
        smapi_version = [string]$sleep.after.smapi_version
        bridge_version = [string]$sleep.after.bridge_version
        installed_mods = @($sleep.after.installed_mods)
        authoritative_requirement_count = $expectedDenominator
        terminal_item_id = $terminalItemId
        terminal_qualified_item_id = $TerminalQualifiedItemId
        shipping_candidate_id = $shippingCandidateId
        sleep_candidate_id = $sleepCandidateId
        transaction_id = [string]$tracker.transaction_id
        transaction_status = [string]$tracker.status
        transaction_evidence_path = [string]$transactionEvidence.path
        transaction_evidence_sha256 = [string]$transactionEvidence.sha256
        native_save_committed_at =
            [string]$tracker.native_save_committed_at
        transaction_completed_at = [string]$tracker.completed_at
        safe_next_day_planning_option_id =
            [string]$tracker.safe_next_day_planning_option_id
        safe_next_day_option_id =
            [string]$tracker.safe_next_day_option_id
        safe_next_day_queue_id =
            [string]$tracker.safe_next_day_queue_id
        safe_next_day_queue_item_id =
            [string]$tracker.safe_next_day_queue_item_id
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
