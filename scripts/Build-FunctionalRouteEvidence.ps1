param(
    [Parameter(Mandatory)][string]$SourcePacketPath,
    [Parameter(Mandatory)][string]$ExpectedPacketSha256,
    [Parameter(Mandatory)][string]$BinaryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$TargetQualifiedItemId='(O)836'
)
$ErrorActionPreference='Stop'
if((Get-FileHash -LiteralPath $SourcePacketPath).Hash -ine $ExpectedPacketSha256){throw 'Retained source packet hash changed.'}
$packet=Get-Content -LiteralPath $SourcePacketPath -Raw|ConvertFrom-Json
foreach($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll'){
    [Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot $name))|Out-Null
}
$options=[Text.Json.JsonSerializerOptions]::new([Text.Json.JsonSerializerDefaults]::Web)
$options.WriteIndented=$true
function Pin([string]$Path){
    [StardewAI.Contracts.Training.FrozenRouteArtifact]::new([IO.Path]::GetFullPath($Path),(Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant())
}
$actions=[Collections.Generic.List[StardewAI.Contracts.Training.FunctionalNativeActionReference]]::new()
$selected=$null
foreach($case in $packet.cases){
    foreach($role in 'original_compiled_queue','native_receipt','original_after'){
        if((Get-FileHash -LiteralPath $case.$role.path).Hash -ine $case.$role.sha256){throw ('Retained native input changed: '+$role)}
    }
    $frame=[StardewAI.Core.Infrastructure.NativeExecutionFrameReader]::ReadVerifiedProduct(
        $case.original_compiled_queue.path,$case.native_receipt.path,$case.original_after.path)
    $actions.Add([StardewAI.Contracts.Training.FunctionalNativeActionReference]::new(
        (Pin $case.original_compiled_queue.path),(Pin $case.native_receipt.path),(Pin $case.original_after.path)))
    if($frame.Result.ChangedFacts|Where-Object {$_.Path -ceq 'fishing.caught_qualified_item_id' -and $_.After -ceq $TargetQualifiedItemId}){
        if($selected){throw 'Target source is ambiguous.'};$selected=$frame
    }
}
if(!$selected){throw 'No retained native target outcome.'}
$snapshot=$selected.Before
$evaluator=[StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator]::new()
$available=$evaluator.Evaluate($snapshot,[string[]]@(),$false,$null)
$ranker=[StardewAI.Core.Training.EventCandidateRanker]::new()
$goal='goal.fishing.complete_master_angler'
$ranked=$ranker.Rank([StardewAI.Contracts.Training.BaselineTrainingReport]::new(),$available,$goal)
$primary=[string[]]@($ranked|Where-Object {$_.Available -and $_.OptionId -ceq 'fishing.catch_fish'}|ForEach-Object CandidateId)
if(!$primary.Count){throw 'Original evaluator did not offer an available fishing goal.'}
$request=[StardewAI.Contracts.Training.DailyPlanCompileRequest]::new()
$request.StateHash=$snapshot.StateHash;$request.GoalId=$goal;$request.ExecutionMode='training_singleplayer'
$request.CompileActionQueue=$true;$request.UseJointCalendar=$true;$request.MaxCandidates=2
$request.PrimaryCandidateIds=$primary;$request.RankedEventCandidates=$ranked
$clock=[StardewAI.Core.Infrastructure.SnapshotGameClockBudgetReader]::CurrentTime($snapshot)
$minutes=[StardewAI.Core.Training.DailyPlanResourceBudget]::AvailablePlanMinutes([int]$clock,$ranked)
$energy=[StardewAI.Core.Training.DailyPlanResourceBudget]::JointEnergyBudget($snapshot)
$plan=[StardewAI.Core.Training.DailyPlanCompiler]::new().CompileWithPrimaryGoal($ranked,$snapshot.StateHash,[int]$clock,
    [string[]]@(),$primary,$goal,'training_singleplayer',2,$minutes,$energy)
$queue=[StardewAI.Core.Execution.ActionQueueCompiler]::new().Compile($plan,$snapshot,$null)
if($queue.Status -cne 'pending'){throw ('Original joint plan did not compile: '+($queue.BlockReasons -join ','))}
$out=New-Item -ItemType Directory -Path $OutputDirectory
$requestPath=Join-Path $out.FullName 'compile-request.json'
$responsePath=Join-Path $out.FullName 'compile-response.json'
[IO.File]::WriteAllText($requestPath,[Text.Json.JsonSerializer]::Serialize($request,$request.GetType(),$options))
$planJson=[Text.Json.JsonSerializer]::Serialize($plan,$plan.GetType(),$options)
$queueJson=[Text.Json.JsonSerializer]::Serialize($queue,$queue.GetType(),$options)
[IO.File]::WriteAllText($responsePath,('{"schema_version":"daily_plan_compile_response.v1","plan":'+$planJson+',"action_queue":'+$queueJson+'}'))
$evidence=[StardewAI.Contracts.Training.FunctionalRouteEvidence]::new('functional_native_route_evidence.v1',
    (Pin $selected.BeforeSnapshotPath),(Pin $requestPath),(Pin $responsePath),$TargetQualifiedItemId,$actions.ToArray())
$evidencePath=Join-Path $out.FullName 'functional-route-evidence.json'
[IO.File]::WriteAllText($evidencePath,[Text.Json.JsonSerializer]::Serialize($evidence,$evidence.GetType(),$options))
$proof=[StardewAI.Contracts.Training.FormalNativeRouteProofReference]::new(
    'functional_native_route_proof_reference.v1',$evidencePath,(Get-FileHash -LiteralPath $evidencePath).Hash.ToLowerInvariant(),$null)
[IO.File]::WriteAllText((Join-Path $out.FullName 'native-route-proof.json'),[Text.Json.JsonSerializer]::Serialize($proof,$proof.GetType(),$options))
$assessment=[StardewAI.Core.Training.FormalNativeRouteAdmissionConsumer]::Evaluate($proof)
[IO.File]::WriteAllText((Join-Path $out.FullName 'native-route-assessment.json'),[Text.Json.JsonSerializer]::Serialize($assessment,$assessment.GetType(),$options))
[pscustomobject]@{native_actions=$actions.Count;candidate_pool=$ranked.Count;accepted=@($plan.CandidateAudit|Where-Object Decision -ceq 'accepted'|Select-Object CandidateId,Kind,RemainingMinutesBefore,RemainingMinutesAfter,RemainingEnergyBefore,RemainingEnergyAfter);gates=$assessment.Gates;scope=$assessment.ProofScope}|ConvertTo-Json -Depth 6
if(@($assessment.Gates|Where-Object {!$_.Satisfied}).Count){exit 2}
