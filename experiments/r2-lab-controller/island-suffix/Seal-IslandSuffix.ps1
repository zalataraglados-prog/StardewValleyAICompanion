param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$fixture=Get-Content -LiteralPath (Join-Path $RunDirectory 'fixture.json') -Raw|ConvertFrom-Json
$session=Get-Content -LiteralPath (Join-Path $RunDirectory 'live-session.json') -Raw|ConvertFrom-Json -DateKind String
$settings=Get-Content -LiteralPath (Join-Path $RunDirectory 'runtime-settings.json') -Raw|ConvertFrom-Json -AsHashtable
$raw=(Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$after=$raw|ConvertFrom-Json
$before=Get-Content -LiteralPath (Join-Path $RunDirectory 'initial-readback.json') -Raw|ConvertFrom-Json
$captureBefore=Get-Content -LiteralPath (Join-Path $RunDirectory 'cove-catch-03\action-01\before.json') -Raw|ConvertFrom-Json
$captureAfter=Get-Content -LiteralPath (Join-Path $RunDirectory 'cove-catch-03\after-fresh.json') -Raw|ConvertFrom-Json
$targetBefore=$captureBefore.state.world_progress.fish_collection_progress.value.items|Where-Object qualified_item_id -ceq '(O)836'
$targetAfter=$captureAfter.state.world_progress.fish_collection_progress.value.items|Where-Object qualified_item_id -ceq '(O)836'
if($targetBefore.caught -or $targetBefore.caught_count -ne 0 -or !$targetAfter.caught -or $targetAfter.caught_count -ne 1 -or $after.save_id.value -cne $fixture.expected_save_id -or $after.player_id.value -cne $fixture.expected_player_id){throw 'Native target transition or fixture identity differs.'}
$catchReceipt=Get-Content -LiteralPath (Join-Path $RunDirectory ('cove-catch-03\action-01\training\runs\'+$fixture.run_id+'\live-snapshots\execution-0001-item-0001.json')) -Raw|ConvertFrom-Json
$signature=$catchReceipt.changed_facts|Where-Object path -ceq 'player.inventory.stack_signature'
$inventoryCounts=@{}
foreach($side in 'before','after'){
    $sum=0
    foreach($match in [regex]::Matches($signature.$side,'(?:^|\|)\d+:\(O\)836:(\d+)(?=\||$)')){$sum += [int]$match.Groups[1].Value}
    $inventoryCounts[$side]=$sum
}
if(($inventoryCounts.after-$inventoryCounts.before) -ne 1){throw 'Native Stingray inventory delta differs.'}
[IO.File]::WriteAllText((Join-Path $RunDirectory 'final-live-snapshot.json'),$raw)
$paths=@('cove-catch-02\independent-original-replay.json','cove-catch-03\independent-original-replay.json')
foreach($direction in 'cove-to-west','west-to-cove-native'){
    foreach($n in 1..3){$paths+=($direction+'\action-'+$n.ToString('00')+'\independent-original-replay.json')}
}
$verified=@(foreach($path in $paths){
    $full=Join-Path $RunDirectory $path
    $j=Get-Content -LiteralPath $full -Raw|ConvertFrom-Json
    if(!$j.original_native_source_replay_verified -or $j.run_id -cne $fixture.run_id){throw 'Original independent replay binding differs.'}
    [ordered]@{path=$path.Replace('\','/');sha256=(Get-FileHash -LiteralPath $full).Hash;option_id=$j.option_id;before_state_hash=$j.before_state_hash;after_state_hash=$j.after_state_hash;source_file_count=@($j.source_sha256.PSObject.Properties).Count}
})
$sourceFiles=@(foreach($file in $fixture.original_hashes.PSObject.Properties){
    $original=Join-Path $fixture.original_source $file.Name
    $isolated=Join-Path (Join-Path $fixture.save_root $fixture.slot) $file.Name
    if((Get-FileHash -LiteralPath $original).Hash -cne $file.Value -or (Get-FileHash -LiteralPath $isolated).Hash -cne $file.Value){throw 'Original source or unsaved isolated main changed.'}
    [ordered]@{name=$file.Name;sha256=$file.Value;original_unchanged=$true;isolated_disk_unchanged=$true}
})
$result=[ordered]@{
    schema_version='r2_local_island_suffix_verification.v1';recorded_at=[DateTimeOffset]::Now.ToString('o')
    run_id=$fixture.run_id;save_id=$fixture.expected_save_id;player_id=$fixture.expected_player_id
    game_version=$before.game_version;total_day=$after.state.time.total_days.value;initial_location=$before.state.player.location_id.value;final_location=$after.state.player.location_id.value
    local_suffix_verified=$true;formal_training_authorized=$false;day_zero_claim=$false;stage_one_deadline_claim=$false
    original_sources=$sourceFiles
    reused_island_capability='GingerIsland局部基线/local-ginger-island-certificate-replay.json; existing access evidence, no new community-center or boat acquisition claim'
    setup=[ordered]@{entry='debug.setup_location_fishing';original_fishing_level=$before.state.fishing.location_context.value.fishing_level;tested_fishing_level=$captureBefore.state.fishing.location_context.value.fishing_level;original_cash=$before.state.player.money.value;tested_cash=$captureBefore.state.player.money.value;original_walnuts_found=$before.state.world_progress.golden_walnuts.value.found;tested_walnuts_found=$after.state.world_progress.golden_walnuts.value.found;injected_tutorial_catch=$false;scope='Existing fixture positions actor, equips existing rod, raises fishing level/XP and sets cash to zero. Setup is excluded from native route/catch evidence; not unmodified level-3 gameplay, resource acquisition, or payment proof.'}
    native_stingray=[ordered]@{qualified_item_id='(O)836';caught_before=$targetBefore.caught;count_before=$targetBefore.caught_count;caught_after=$targetAfter.caught;count_after=$targetAfter.caught_count;native_inventory_increment=1;receipt='cove-catch-03/action-01/training/runs/'+$fixture.run_id+'/live-snapshots/execution-0001-item-0001.json';independent_original_reader_verified=$true}
    routes=[ordered]@{forward=@('IslandWest','IslandSouth','IslandSouthEast','IslandSouthEastCave');reverse=@('IslandSouthEastCave','IslandSouthEast','IslandSouth','IslandWest');verified_connector_count=6;new_walnut_payments=0}
    independent_original_replays=$verified
    retained_failures=@('cove-route-v2: native movement encountered Sleep dialogue; no route pass','decline-sleep: original close-menu candidate unavailable; no action dispatched','west-to-cove: generic fixture chose disconnected IslandWest 0,0; no route pass','cove-catch-01: catch applied, but original independent reader rejected second item frame binding; excluded from independent replay passes')
    durability='Native receipts and snapshots sealed; isolated disk save remains original. New catch is an unsaved local-test tail; no save/cold-readback claim.'
    inherited_results='Lionfish and BlueDiscus already caught; Gourmand already 3/3. Not counted as new catches or new order transitions.'
}
$result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $RunDirectory 'verified-local-suffix.json') -Encoding utf8
foreach($row in $session.processes){
    $p=Get-Process -Id $row.id
    if($p.Path -cne $row.exe -or $p.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime){throw 'Process identity changed before shutdown.'}
}
$stopped=@(foreach($role in 'product','backend','game'){
    $row=$session.processes|Where-Object role -ceq $role
    Stop-Process -Id $row.id
    Wait-Process -Id $row.id -Timeout 10 -ErrorAction SilentlyContinue
    $p=Get-Process -Id $row.id -ErrorAction SilentlyContinue
    if($p -and $p.StartTime.ToUniversalTime() -eq [DateTimeOffset]::Parse($row.started).UtcDateTime){throw 'Recorded service has not stopped.'}
    [ordered]@{role=$role;id=$row.id;started=$row.started;exe=$row.exe;stopped=$true}
})
[ordered]@{recorded_at=[DateTimeOffset]::Now.ToString('o');run_id=$fixture.run_id;all_recorded_services_stopped=$true;services=$stopped;unsaved_test_tail_retained_in_receipts=$true}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $RunDirectory 'stopped.json') -Encoding utf8
[pscustomobject]@{local_suffix_verified=$true;stingray='0 -> 1';native_connectors=6;independent_original_replays=$verified.Count;original_skill=$before.state.fishing.location_context.value.fishing_level;tested_skill=$captureBefore.state.fishing.location_context.value.fishing_level;all_recorded_services_stopped=$true;formal=$false}|ConvertTo-Json
