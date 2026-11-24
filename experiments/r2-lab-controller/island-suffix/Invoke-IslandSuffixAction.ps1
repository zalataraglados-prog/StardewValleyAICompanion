param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [ValidateSet('Move','Catch','CloseMenu')][string]$Mode='Move',
    [string]$Destination='IslandSouthEastCave',
    [ValidateRange(1,12)][int]$MaximumActions=6,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt
)
$ErrorActionPreference='Stop'
$fixture=Get-Content -LiteralPath (Join-Path $RunDirectory 'fixture.json') -Raw | ConvertFrom-Json
$session=Get-Content -LiteralPath (Join-Path $RunDirectory 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$settings=Get-Content -LiteralPath (Join-Path $RunDirectory 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$lab=Split-Path -Parent $PSScriptRoot
$binary=Join-Path $lab 'published\full-native-replay-20261124\live-training-loop'
$dotnet='D:\StardewAI-TestLab\dotnet\dotnet.exe'
$goal='goal.fishing.complete_master_angler'
[Reflection.Assembly]::LoadFrom((Join-Path $binary 'StardewAI.Contracts.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $binary 'StardewAI.Core.dll')) | Out-Null
$jsonOptions=[Text.Json.JsonSerializerOptions]::new()
$jsonOptions.PropertyNameCaseInsensitive=$true
$jsonOptions.WriteIndented=$true
$out=New-Item -ItemType Directory -Path (Join-Path $RunDirectory $Attempt)
foreach($key in $settings.Keys){[Environment]::SetEnvironmentVariable($key,$settings[$key],'Process')}
foreach($n in 1..$MaximumActions){
    foreach($row in $session.processes){
        $p=Get-Process -Id $row.id
        if($p.Path -cne $row.exe -or $p.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime){throw 'Recorded native process identity changed.'}
    }
    $body=(Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
    $view=$body | ConvertFrom-Json
    if($view.save_id.value -cne $fixture.expected_save_id -or $view.player_id.value -cne $fixture.expected_player_id -or $view.state.environment.training_run_id.value -cne $fixture.run_id -or $settings.STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT -cne $fixture.save_root){throw 'Exact isolated fixture binding changed.'}
    $location=$view.state.player.location_id.value
    if($Mode -ceq 'Move' -and $location -ceq $Destination){[pscustomobject]@{status='arrived';location=$location;actions=$n-1};return}
    if($Mode -ceq 'Catch' -and $location -cne $Destination){throw 'Catch requires the verified target map.'}
    $step=New-Item -ItemType Directory -Path (Join-Path $out.FullName ('action-'+$n.ToString('00')))
    [IO.File]::WriteAllText((Join-Path $step.FullName 'before.json'),$body)
    $snapshot=[Text.Json.JsonSerializer]::Deserialize[StardewAI.Contracts.State.SnapshotEnvelope]($body,$jsonOptions)
    $option=if($Mode -ceq 'Move'){'exploration.visit_location'}elseif($Mode -ceq 'CloseMenu'){'recovery.stabilize_day'}else{'fishing.catch_fish'}
    $evaluator=[StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator]::new()
    $available=$evaluator.Evaluate($snapshot,[string[]]@($option),$false,$null)
    $ranker=[StardewAI.Core.Training.EventCandidateRanker]::new()
    $ranked=$ranker.Rank([StardewAI.Contracts.Training.BaselineTrainingReport]::new(),$available,$goal)
    if($Mode -ceq 'Move'){
        $method=$evaluator.GetType().GetMethod('FindResolvedRoutePlan',([Reflection.BindingFlags]::NonPublic -bor [Reflection.BindingFlags]::Static))
        $inputs=[object[]]@($snapshot,$location,$Destination,$available.Options[0].EventCandidates)
        $route=$method.Invoke($null,$inputs)
        $candidate=$route.FirstActionCandidate
        if(!$candidate -or !$candidate.Available){throw ('Original route planner has no available next connector from '+$location)}
        $selected=[StardewAI.Contracts.Training.PolicyEventCandidatePrediction[]]@($ranked | Where-Object { $_.Available -and $_.CandidateId -ceq $candidate.CandidateId })
    }elseif($Mode -ceq 'CloseMenu'){
        $selected=[StardewAI.Contracts.Training.PolicyEventCandidatePrediction[]]@($ranked | Where-Object { $_.Available -and $_.Kind -ceq 'recovery_close_menu' } | Sort-Object Rank | Select-Object -First 1)
    }else{
        $selected=[StardewAI.Contracts.Training.PolicyEventCandidatePrediction[]]@($ranked | Where-Object Available | Sort-Object Rank | Select-Object -First 1)
    }
    if($selected.Count -ne 1){throw 'Original candidate evaluator did not provide one executable native action.'}
    $plan=[StardewAI.Core.Training.DailyPlanCompiler]::new().Compile($selected,$snapshot.StateHash,$goal,'training_singleplayer',1,$null,$null)
    $queue=[StardewAI.Core.Execution.ActionQueueCompiler]::new().Compile($plan,$snapshot,$null)
    $queuePath=Join-Path $step.FullName 'queue.json'
    [IO.File]::WriteAllText((Join-Path $step.FullName 'plan.json'),[Text.Json.JsonSerializer]::Serialize($plan,$plan.GetType(),$jsonOptions))
    [IO.File]::WriteAllText($queuePath,[Text.Json.JsonSerializer]::Serialize($queue,$queue.GetType(),$jsonOptions))
    if($queue.Status -cne 'pending' -or !$queue.Items.Count){throw ('Original compiler rejected action: '+($queue.BlockReasons -join ','))}
    $null=Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS+'/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 40
    $arguments=[string[]]@((Join-Path $binary 'StardewAI.LiveTrainingLoop.dll'),'--root',(Join-Path $step.FullName 'training'),'--run-id',$fixture.run_id,'--no-manifest','--backend-url',$settings.ASPNETCORE_URLS,'--bridge-snapshot-url',$settings.STARDEWAI_BRIDGE_SNAPSHOT_URL,'--executor-url',$settings.STARDEWAI_PRODUCT_EXECUTOR_URL,'--goal',$goal,'--use-product-executor','--skip-training','--precompiled-queue',$queuePath,'--execution-snapshot-profile','full','--retain-full-execution-snapshots','--snapshot-artifact-mode','content_addressed_gzip','--artifact-retention-mode','stop','--max-attempts','1','--required-verified-actions','1','--max-queue-item-attempts','12','--max-persisted-iterations','128','--sleep-ms','1000','--after-snapshot-wait-ms','500','--save-isolation-path',$fixture.save_root,'--save-slot',$fixture.slot)
    $arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $step.FullName 'arguments.json') -Encoding utf8
    & $dotnet @arguments *> (Join-Path $step.FullName 'native.log')
    $exitCode=$LASTEXITCODE
    $log=Get-Content -LiteralPath (Join-Path $step.FullName 'native.log') -Raw | ConvertFrom-Json
    [pscustomobject]@{action=$n;mode=$Mode;from=$location;candidate=$selected[0].CandidateId;status=$log.status;verified_actions=$log.verified_actions;exit_code=$exitCode}
    if($exitCode -ne 0 -or $log.status -cne 'ok' -or $log.verified_actions -lt 1){throw ('Native action stopped; retain '+$step.FullName)}
    if($Mode -cne 'Move'){return}
}
$after=Invoke-RestMethod -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20
if($Mode -ceq 'Move' -and $after.state.player.location_id.value -cne $Destination){throw 'Bounded native connector traversal did not reach destination.'}
