param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -Raw -LiteralPath (Join-Path $runtime 'runtime-settings.json') | ConvertFrom-Json -AsHashtable
$session = Get-Content -Raw -LiteralPath (Join-Path $runtime 'live-session.json') | ConvertFrom-Json -DateKind String
$folder = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-evidence'))
$goal = 'goal.world_progress.complete_community_center'
$bridge = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
$backend = $settings.ASPNETCORE_URLS
$body = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
$before = $body | ConvertFrom-Json
if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
    $before.state.time.total_days.value -ne 30 -or $before.state.mining.current_mine.value.mine_kind -cne 'ordinary_mines') {
    throw 'Passability probe requires the same live ordinary mine source.'
}
[IO.File]::WriteAllText((Join-Path $folder 'before.json'), $body)
$null = Invoke-RestMethod -Uri ($backend + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 45
$parameters = @(@{name='target_depth';value='10'}, @{name='target_location_family';value='ordinary_mines'})
$request = @{
    goal_id=$goal; execution_mode='training_singleplayer'; state_hash=$before.state_hash
    include_blocked_options=$true; candidates=@(@{option_id='mining.reach_depth';parameters=$parameters})
}
$ranking = Invoke-RestMethod -Uri ($backend + '/api/v1/planner/baseline/rank-options') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 12 -Compress) -TimeoutSec 45
$rankPath = Join-Path $folder 'ranking.json'
$ranking | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath $rankPath -Encoding utf8
$candidate = $ranking.ranked_event_candidates | Where-Object {
    $_.available -and $_.option_id -ceq 'mining.reach_depth' -and $_.kind -ceq 'mining_buried_item_plan_envelope'
} | Sort-Object rank | Select-Object -First 1
if (!$candidate) { throw 'Original mine planner did not offer one native eligible digging tile.' }
$x = [int](@($candidate.parameters | Where-Object name -ceq 'target_tile_x')[0].value)
$y = [int](@($candidate.parameters | Where-Object name -ceq 'target_tile_y')[0].value)
$bound = @{
    RunDirectory=$RunDirectory; RuntimeAttempt=$RuntimeAttempt; RankPath=$rankPath
    CandidateId=$candidate.candidate_id; ExpectedStateHash=$before.state_hash; GoalId=$goal
    Attempt=($Attempt + '-hoe'); CandidateParameters=$parameters
}
& (Join-Path $PSScriptRoot 'Invoke-IsolatedRankedQueue.ps1') @bound
if ($LASTEXITCODE -ne 0) { throw 'Original native hoe probe stopped.' }
$afterBody = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
$after = $afterBody | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $folder 'after-hoe.json'), $afterBody)
$soil = @($after.state.current_location.terrain_features.value | Where-Object {
    $_.tile_x -eq $x -and $_.tile_y -eq $y -and $_.type -ceq 'StardewValley.TerrainFeatures.HoeDirt' -and !$_.has_crop
})
if ($soil.Count -ne 1 -or $after.state.mining.tiles.value.collision_context.blocked_rows[$y][$x] -ne '0' -or
    $after.state.player.location_id.value -cne $before.state.player.location_id.value) {
    throw 'The fresh mine projection did not preserve the empty hoed tile as traversable.'
}
$null = Invoke-RestMethod -Uri ($backend + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($afterBody)) -TimeoutSec 45
$action = @{
    schema_version='small_model_action.v1'; model_output_id=$Attempt; source_model='native_mine_empty_hoe_dirt'
    state_hash=$after.state_hash; goal_id=$goal; execution_mode='training_singleplayer'
    actor=@{actor_id='training_farmer.main';actor_type='training_farmer';control_surface='training_sandbox'}
    actions=@(@{
        action_id='walk-on-native-hoed-tile'; option_id='executor.move_to_tile'
        parameters=@(
            @{name='target_location';value=$after.state.player.location_id.value},
            @{name='target_tile_x';value=$x.ToString()}, @{name='target_tile_y';value=$y.ToString()}
        )
    })
}
$queue = Invoke-RestMethod -Uri ($backend + '/api/v1/small-model/action-queue/compile') -Method Post -ContentType 'application/json' -Body ($action | ConvertTo-Json -Depth 15 -Compress) -TimeoutSec 45
$queuePath = Join-Path $folder 'move-queue.json'
$queue | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $queuePath -Encoding utf8
if ($queue.status -cne 'pending' -or $queue.state_hash -cne $after.state_hash -or @($queue.items).Count -ne 1) {
    throw 'Original native movement compiler rejected the hoed tile.'
}
$argsFor = Get-Content -Raw -LiteralPath (Join-Path $RunDirectory 'spring27-chest-adapter-replay\execution-arguments.json') | ConvertFrom-Json
$argsFor[[array]::IndexOf($argsFor,'--precompiled-queue') + 1] = $queuePath
$argsFor[[array]::IndexOf($argsFor,'--max-persisted-iterations') + 1] = '400'
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$settings[$key],'Process') }
$dotnet = $session.processes | Where-Object role -ceq 'backend' | Select-Object -ExpandProperty exe
& $dotnet @argsFor *> (Join-Path $folder 'move-native.log')
if ($LASTEXITCODE -ne 0) { throw 'Original native movement failed on the projected passable tile.' }
$lastBody = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
$last = $lastBody | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $folder 'after-move.json'), $lastBody)
if ($last.state.player.tile_x.value -ne $x -or $last.state.player.tile_y.value -ne $y -or
    $last.save_id.value -cne $before.save_id.value -or $last.player_id.value -cne $before.player_id.value -or
    $last.state.time.total_days.value -ne 30 -or $last.state.player.location_id.value -cne $before.state.player.location_id.value) {
    throw 'Native probe did not finish on the exact original hoed tile.'
}
[ordered]@{
    save=$last.save_id.value; player=$last.player_id.value; day=30; location=$last.state.player.location_id.value
    tile=@($x,$y); before=$before.state_hash; after_hoe=$after.state_hash; after_move=$last.state_hash
    empty_hoe_dirt=$true; projected_blocked=$false; native_actor_on_tile=$true
} | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $folder 'verified.json')
