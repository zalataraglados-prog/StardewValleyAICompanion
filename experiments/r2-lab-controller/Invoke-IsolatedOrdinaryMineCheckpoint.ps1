param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidateRange(1,120)][int]$TargetDepth,
    [Parameter(Mandatory)][int]$ExpectedTotalDay,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [ValidateRange(1,40)][int]$MaximumStages=16
)

$ErrorActionPreference='Stop'
$runtime=Join-Path $RunDirectory $RuntimeAttempt
$settings=Get-Content -Raw -LiteralPath (Join-Path $runtime 'runtime-settings.json') | ConvertFrom-Json -AsHashtable
$folder=New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt+'-evidence'))
$bridge=$settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
$backend=$settings.ASPNETCORE_URLS
$goal='goal.world_progress.complete_community_center'
$parameters=@(
    @{name='target_depth';value=$TargetDepth.ToString()},
    @{name='target_location_family';value='ordinary_mines'},
    @{name='latest_exit_time';value='1800'},
    @{name='minimum_reserve_health';value='45'},
    @{name='minimum_reserve_energy';value='45'}
)
$visited=[Collections.Generic.HashSet[string]]::new()
for($step=1;$step -le $MaximumStages;$step++) {
    $source=(Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
    $before=$source|ConvertFrom-Json
    if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
        $before.state.time.total_days.value -ne $ExpectedTotalDay) { throw 'The native mine source changed.' }
    if ($before.state.player.deepest_mine_level.value -ge $TargetDepth -or
        $before.in_game_time.value -ge 1800 -or $before.state.player.health.value -lt 45 -or
        $before.state.player.energy.value -lt 45 -or !$visited.Add($before.state_hash)) { return }
    [IO.File]::WriteAllText((Join-Path $folder ('before-'+$step+'.json')),$source)
    $null=Invoke-RestMethod -Uri ($backend+'/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($source)) -TimeoutSec 45
    $request=@{
        goal_id=$goal; execution_mode='training_singleplayer'; state_hash=$before.state_hash
        include_blocked_options=$true; candidates=@(@{option_id='mining.reach_depth';parameters=$parameters})
    }
    $rank=Invoke-RestMethod -Uri ($backend+'/api/v1/planner/baseline/rank-options') -Method Post -ContentType 'application/json' -Body ($request|ConvertTo-Json -Depth 14 -Compress) -TimeoutSec 55
    $rankFile=Join-Path $folder ('rank-'+$step+'.json')
    $rank|ConvertTo-Json -Depth 52|Set-Content -LiteralPath $rankFile -Encoding utf8
    $candidate=$rank.ranked_event_candidates | Where-Object {
        $_.available -and $_.option_id -ceq 'mining.reach_depth' -and
        ($_.kind -cin 'mining_reach_depth_plan_envelope','route_connector_tile' -or
         $_.candidate_id -clike 'mining.reach_depth:elevator:*')
    } | Sort-Object rank | Select-Object -First 1
    if (!$candidate) {
        [ordered]@{status='no_native_candidate';step=$step;state=$before.state_hash;place=$before.state.player.location_id.value;reasons=@($rank.ranked_event_candidates|Where-Object option_id -ceq 'mining.reach_depth'|Select-Object -First 8 candidate_id,block_reasons)}|ConvertTo-Json -Depth 6 -Compress|Tee-Object -FilePath (Join-Path $folder 'progress.jsonl') -Append
        return
    }
    $bound=@{
        RunDirectory=$RunDirectory; RuntimeAttempt=$RuntimeAttempt; RankPath=$rankFile
        CandidateId=$candidate.candidate_id; ExpectedStateHash=$before.state_hash; GoalId=$goal
        Attempt=($Attempt+'-'+$step); CandidateParameters=$parameters
    }
    & (Join-Path $PSScriptRoot 'Invoke-IsolatedRankedQueue.ps1') @bound
    $nativeExit=$LASTEXITCODE
    $result=(Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
    $after=$result|ConvertFrom-Json
    [IO.File]::WriteAllText((Join-Path $folder ('after-'+$step+'.json')),$result)
    [ordered]@{step=$step;candidate=$candidate.kind;place=$after.state.player.location_id.value;depth=$after.state.player.current_mine_level.value;deepest=$after.state.player.deepest_mine_level.value;time=$after.in_game_time.value;health=$after.state.player.health.value;energy=$after.state.player.energy.value;free_slots=$after.state.player.inventory_capacity.value.empty_slots;state=$after.state_hash;native_exit=$nativeExit}|ConvertTo-Json -Compress|Tee-Object -FilePath (Join-Path $folder 'progress.jsonl') -Append
    if ($nativeExit -ne 0 -or $after.state_hash -ceq $before.state_hash) { return }
}