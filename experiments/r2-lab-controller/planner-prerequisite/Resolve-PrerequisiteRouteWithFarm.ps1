[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $SnapshotPath,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [ValidateSet('CommunityCenter','WizardHouse','BusStop','Mountain','Forest','Town','Backwoods','Railroad','Woods','Farm')][string] $Destination = 'CommunityCenter',
    [string] $BinaryRoot = ""
)

$ErrorActionPreference = 'Stop'
$lab = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$binary = if ($BinaryRoot) { $BinaryRoot } else { Join-Path $lab 'published\b38c3cca-junimo-note-recovery\goal-conditioned-bootstrap' }
[System.Reflection.Assembly]::LoadFrom((Join-Path $binary 'StardewAI.Contracts.dll')) | Out-Null
[System.Reflection.Assembly]::LoadFrom((Join-Path $binary 'StardewAI.Core.dll')) | Out-Null

$SnapshotPath = (Resolve-Path -LiteralPath $SnapshotPath).Path
$raw = [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($SnapshotPath)
$jsonOptions = [System.Text.Json.JsonSerializerOptions]::new()
$jsonOptions.PropertyNameCaseInsensitive = $true
$jsonOptions.WriteIndented = $true
$snapshot = [System.Text.Json.JsonSerializer]::Deserialize[StardewAI.Contracts.State.SnapshotEnvelope]($raw, $jsonOptions)
$view = $raw | ConvertFrom-Json
$location = [string]$view.state.player.location_id.value
if ($snapshot.GameVersion -ne '1.6.15' -or $snapshot.SaveId.Value -ne 'ProofFarm' -or
    $snapshot.PlayerId.Value -ne '319842636613009663' -or [string]::IsNullOrWhiteSpace($location)) {
    throw 'Snapshot does not bind the verified Spring 15 save and actor.'
}
if ($location -eq $Destination) {
    return [pscustomobject]@{ status = 'already_present'; location = $location; target = $Destination }
}

$evaluator = [StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator]::new()
$available = $evaluator.Evaluate($snapshot, [string[]]@('exploration.visit_location'), $false, $null)
$flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
$method = $evaluator.GetType().GetMethod('FindResolvedRoutePlan', $flags)
$inputs = [object[]]::new(4)
$inputs[0] = $snapshot
$inputs[1] = $location
$inputs[2] = $Destination
$inputs[3] = $available.Options[0].EventCandidates
$route = $method.Invoke($null, $inputs)
$step = $route.FirstActionCandidate
if ($null -eq $step -or !$step.Available) {
    return [pscustomobject]@{
        status = 'no_pending_first_action'; location = $location; target = $Destination
        candidate = $step.CandidateId; reasons = @($step.BlockReasons)
    }
}

$ranker = [StardewAI.Core.Training.EventCandidateRanker]::new()
$ranked = $ranker.Rank([StardewAI.Contracts.Training.BaselineTrainingReport]::new(), $available, 'grandpa.maximum_21')
$selected = [StardewAI.Contracts.Training.PolicyEventCandidatePrediction[]]@(
    $ranked | Where-Object { $_.Available -and $_.CandidateId -eq $step.CandidateId }
)
if ($selected.Count -ne 1) {
    throw 'The native first action did not bind one available exploration candidate.'
}

$plan = [StardewAI.Core.Training.DailyPlanCompiler]::new().Compile(
    $selected, $snapshot.StateHash, 'grandpa.maximum_21', 'training_singleplayer', 1, $null, $null)
$queue = [StardewAI.Core.Execution.ActionQueueCompiler]::new().Compile($plan, $snapshot, $null)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$planPath = Join-Path $OutputDirectory 'plan.json'
$queuePath = Join-Path $OutputDirectory 'queue.json'
[IO.File]::WriteAllText($planPath, [System.Text.Json.JsonSerializer]::Serialize($plan, $plan.GetType(), $jsonOptions))
[IO.File]::WriteAllText($queuePath, [System.Text.Json.JsonSerializer]::Serialize($queue, $queue.GetType(), $jsonOptions))
$pending = $queue.Status -eq 'pending' -and @($queue.Items | Where-Object Status -ne 'pending').Count -eq 0
$actions = @($queue.Items | ForEach-Object OptionId)
$connector = $actions.Count -eq 1 -and $actions[0] -eq 'executor.traverse_connector'
$legRepair = $step.Kind -eq 'clear_obstacle_tile' -and
    $step.CandidateId.StartsWith('route-repair:', [StringComparison]::Ordinal) -and
    ($actions -join ',') -in @('executor.clear_obstacle','executor.move_to_tile,executor.clear_obstacle')
if (-not $pending -or (-not $connector -and -not $legRepair)) {
    return [pscustomobject]@{
        status = 'compiler_rejected'; location = $location; target = $Destination
        candidate = $step.CandidateId; plan = $planPath; queue = $queuePath
        candidate_kind = $step.Kind; queue_status = $queue.Status
        queue_reasons = @($queue.BlockReasons); items = @($queue.Items | Select-Object OptionId,Status,BlockReasons)
    }
}
$result = [pscustomobject]@{
    status = 'pending'; snapshot = $SnapshotPath; state_hash = $snapshot.StateHash
    day = $view.state.time.total_days.value; time = $view.in_game_time.value
    location = $location; target = $Destination; candidate = $step.CandidateId
    actions = $actions; first_action = $queue.Items[0].OptionId; plan = $planPath; queue = $queuePath
    waypoints = @($route.Path | Select-Object FromLocation, TargetLocation, FromX, FromY, TargetX, TargetY)
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'selection.json') -Encoding utf8
$result
