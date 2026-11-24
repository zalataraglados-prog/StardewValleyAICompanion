param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$out=Join-Path $RunDirectory 'route-handoff'
$fixture=Get-Content -LiteralPath (Join-Path $RunDirectory 'fixture.json') -Raw|ConvertFrom-Json
$summary=Get-Content -LiteralPath (Join-Path $RunDirectory 'verified-local-suffix.json') -Raw|ConvertFrom-Json
$initial=Get-Content -LiteralPath (Join-Path $RunDirectory 'initial-readback.json') -Raw|ConvertFrom-Json
$last=Get-Content -LiteralPath (Join-Path $RunDirectory 'final-live-snapshot.json') -Raw|ConvertFrom-Json
$binary=Join-Path (Split-Path -Parent $PSScriptRoot) 'published\full-native-replay-20261124\live-training-loop'
foreach($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll','StardewAI.GoalConditionedBootstrap.dll'){
    [Reflection.Assembly]::LoadFrom((Join-Path $binary $name))|Out-Null
}
function Artifact([string]$Path){
    $full=[IO.Path]::GetFullPath($Path)
    [ordered]@{path=$full;sha256=(Get-FileHash -LiteralPath $full).Hash}
}
function Read-Frame([string]$Path){
    [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($Path)|ConvertFrom-Json
}
$certificatePath=Join-Path $out 'current-source-access-certificate.json'
$certificate=[StardewAI.GoalConditionedBootstrap.MasterAnglerSharedCapabilityCertificateBuilder]::ReadAndReplay($certificatePath)
if(!$certificate.Ready -or $certificate.RunId -cne $fixture.run_id -or $certificate.SourceStateHash -cne $initial.state_hash){throw 'Original shared certificate does not bind the actual starting readback.'}
$sourceMain=Join-Path $fixture.original_source $fixture.slot
if((Get-FileHash -LiteralPath $sourceMain).Hash -cne $fixture.original_hashes.($fixture.slot)){throw 'Original unlocked save changed.'}
[xml]$xml=Get-Content -LiteralPath $sourceMain -Raw
$wallet=[int]$xml.SaveGame.goldenWalnuts
if($wallet -ne $initial.state.world_progress.golden_walnuts.value.current -or $wallet -ne $last.state.world_progress.golden_walnuts.value.current){throw 'Original XML and actual native wallet differ.'}
$items=@(foreach($row in $summary.independent_original_replays){
    $replayPath=Join-Path $RunDirectory $row.path
    if((Get-FileHash -LiteralPath $replayPath).Hash -cne $row.sha256){throw 'An independently replayed source record changed.'}
    $step=if($row.path -match '^cove-catch-'){
        Join-Path (Split-Path -Parent $replayPath) 'action-01'
    }else{Split-Path -Parent $replayPath}
    $directory=Join-Path $step ('training\runs\'+$fixture.run_id+'\live-snapshots')
    $receiptPath=Join-Path $directory 'execution-0001-item-0001.json'
    $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
    $journalPath=Join-Path $directory 'execution-request-0001-item-0001.json'
    $journal=Get-Content -LiteralPath $journalPath -Raw|ConvertFrom-Json
    $before=Read-Frame $journal.before_snapshot_path
    $after=Read-Frame $receipt.after_snapshot_path
    if($before.save_id.value -cne $fixture.expected_save_id -or $after.save_id.value -cne $fixture.expected_save_id -or
       $before.player_id.value -cne $fixture.expected_player_id -or $after.player_id.value -cne $fixture.expected_player_id -or
       $before.state.world_progress.golden_walnuts.value.current -ne $wallet -or $after.state.world_progress.golden_walnuts.value.current -ne $wallet){throw 'Selected action identity or inherited walnut balance differs.'}
    $planPath=Join-Path $step 'plan.json'
    $plan=Get-Content -LiteralPath $planPath -Raw|ConvertFrom-Json
    $sourceReplay=Get-Content -LiteralPath $replayPath -Raw|ConvertFrom-Json
    [ordered]@{
        case_path=[IO.Path]::GetRelativePath($RunDirectory,$step).Replace('\','/')
        original_plan=(Artifact $planPath);original_compiled_queue=(Artifact $receipt.mechanical_compiled_queue_path)
        native_receipt=(Artifact $receiptPath);original_request_journal=(Artifact $journalPath)
        original_request_gzip=(Artifact (Join-Path $directory $journal.blob_file_name))
        original_before=(Artifact $journal.before_snapshot_path);original_after=(Artifact $receipt.after_snapshot_path)
        independent_replay=(Artifact $replayPath);independent_source_hashes=$sourceReplay.source_sha256
        candidate_audit=$plan.candidate_audit;option_id=$receipt.option_id
        before_state_hash=$before.state_hash;after_state_hash=$after.state_hash
        date=[ordered]@{year=$before.state.time.year.value;season=$before.state.time.season.value;day=$before.state.time.day.value;total_day=$before.state.time.total_days.value;after_total_day=$after.state.time.total_days.value;before_time=$before.state.time.time.value;after_time=$after.state.time.time.value}
        from=$before.state.player.location_id.value;to=$after.state.player.location_id.value
        walnuts_before=$before.state.world_progress.golden_walnuts.value.current;walnuts_after=$after.state.world_progress.golden_walnuts.value.current
        observed_catch=@($receipt.changed_facts|Where-Object path -ceq 'fishing.caught_qualified_item_id'|ForEach-Object after)
    }
})
$access=$initial.state.locations.ginger_island_access.value
$upgrades=@(foreach($name in 'first_parrot','western_turtle_parrot','island_house_parrot','resort_parrot'){
    $row=$access.$name
    [ordered]@{name=$name;native_state=$row.current_state;complete_or_pending=$row.complete_or_pending;identity_exact=$row.identity_exact;native_cost=$row.required_nuts;new_payment_in_local_suffix=$false}
})
$targetCases=@($items|Where-Object {$_.observed_catch -contains '(O)836'})
if($targetCases.Count -ne 1){throw 'The package must name one actual Stingray catch case.'}
$targetCase=$targetCases[0]
$targetReceipt=Get-Content -LiteralPath $targetCase.native_receipt.path -Raw|ConvertFrom-Json
$distribution=$targetReceipt.changed_facts|Where-Object path -ceq 'fishing.planned_outcome_distribution_json'
$targetRules=@(($distribution.after|ConvertFrom-Json)|Where-Object qualified_item_id -ceq '(O)836')
if($targetRules.Count -ne 1){throw 'Actual native outcome did not bind one compiled Stingray source rule.'}
$packet=[ordered]@{
    schema_version='r2_existing_access_local_route_handoff.v1';recorded_at=[DateTimeOffset]::Now.ToString('o')
    scope='User-authorized post-unlock local suffix; existing R2 L1/L2 prerequisites reused. Not an L3 fresh-save acquisition trajectory.'
    entry_resolution='The user explicitly directed use of an already-unlocked island save to verify the remaining suffix. This replaces continuing acquisition on the earlier locked test save; it does not join that other player or reopen accepted prerequisites.'
    run_id=$fixture.run_id;save_id=$fixture.expected_save_id;player_id=$fixture.expected_player_id;game_version=$initial.game_version
    selected_local_goal='goal.fishing.complete_master_angler';selected_local_target='(O)836';selected_location='IslandSouthEastCave'
    selection_origin='Original candidate evaluator/ranker, DailyPlanCompiler, ActionQueueCompiler and Product/native execution; caller scoped the local island destination. Not a whole-game/global selector lease.'
    actual_start_readback=(Artifact (Join-Path $RunDirectory 'initial-readback.json'));original_save=(Artifact $sourceMain)
    existing_access_certificate=(Artifact $certificatePath)
    original_certificate_replay='MasterAnglerSharedCapabilityCertificateBuilder.ReadAndReplay: ready; same run/save/player/source hash'
    resource_ledger=[ordered]@{source='Existing native save balance, independently matched to original XML goldenWalnuts';starting_walnuts=$wallet;ending_walnuts=$wallet;new_source_completions=@();new_payments=@();new_payment_order=@();new_walnuts_required=0;existing_upgrades=$upgrades;payment_history_claim=$false;global_joint_calendar_claim=$false}
    fishing_date=[ordered]@{year=$targetCase.date.year;season=$targetCase.date.season;day=$targetCase.date.day;total_day=$targetCase.date.total_day;stingray_cast_before_time=$targetCase.date.before_time;stingray_cast_after_time=$targetCase.date.after_time;source_rule=$targetRules[0].source_key;actual_native_outcome='(O)836';year_three_deadline_claim=$false}
    cases=$items
    causal_boundary='Catch cases and the six cross-map connector cases are separate local cases on the same fixture. The successful new-species catch preceded the forward island traversal; they are not relabeled as a single acquire-access/travel/new-catch chain.'
    setup=$summary.setup;native_stingray=$summary.native_stingray;durability=$summary.durability
    inherited_results=$summary.inherited_results;retained_failures=$summary.retained_failures
    consumer_handoff=[ordered]@{target_issue=570;latest_development_commit='08f1b80002078c45fb1d9307c0d95d603ab1cbcd';current_formal_consumer='Source replay is wired but successful replay unconditionally returns selected_route_reconciliation_pending; its current reference requires a GoldenWalnutNativeEvidenceBinding for full source schedule';next='Use this scoped local package as actual post-unlock evidence and keep global-source acquisition reconciliation in its own contract. Do not reopen community-center or local fishing acceptance.';formal_native_gate_claim=$false;training_label_eligible=$false;formal_training_authorized=$false}
}
$developmentReplay=Join-Path $out 'development-source-replay\summary.json'
if(Test-Path -LiteralPath $developmentReplay){$packet['current_development_replay']=Artifact $developmentReplay}
$packet|ConvertTo-Json -Depth 22|Set-Content -LiteralPath (Join-Path $out 'selected-local-route-and-ledger.json') -Encoding utf8
[ordered]@{original_shared_certificate_ready=$certificate.Ready;inherited_wallet=$wallet;new_payments=0;action_cases=$items.Count;packet=(Artifact (Join-Path $out 'selected-local-route-and-ledger.json'))}|ConvertTo-Json -Depth 4
