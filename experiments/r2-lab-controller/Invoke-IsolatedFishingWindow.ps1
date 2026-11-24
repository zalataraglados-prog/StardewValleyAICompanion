param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$TargetItemId,
    [Parameter(Mandatory)][string]$TargetLocation,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [ValidateRange(1, 64)][int]$MaximumAttempts = 1,
    [ValidateRange(1, 64)][int]$RequiredVerifiedActions = 1,
    [switch]$StopAfterObjectiveComplete
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 15).Content
$snapshot = $body | ConvertFrom-Json
if ($snapshot.state.player.inventory_capacity.value.empty_slots -lt 1) { throw 'No verified free fishing output slot.' }
$dir = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-plan'))
$snapshotPath = Join-Path $dir.FullName 'before.json'
[IO.File]::WriteAllText($snapshotPath, $body)
$null = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/snapshots') -Method Post `
    -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 40
$lab = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $lab 'published\full-native-replay-20261124\live-training-loop'
$windows = Join-Path (Split-Path -Parent $lab) 'R2第5步离线校验\52e681b1\local-master-angler-windows.json'
$intentPath = Join-Path $dir.FullName 'intents.json'
& 'D:\StardewAI-TestLab\dotnet\dotnet.exe' exec --runtimeconfig (Join-Path $bin 'StardewAI.LiveTrainingLoop.runtimeconfig.json') `
    --depsfile (Join-Path $bin 'StardewAI.LiveTrainingLoop.deps.json') (Join-Path $bin 'StardewAI.GoalConditionedBootstrap.dll') `
    build-master-angler-target-date-intents --windows $windows --snapshot $snapshotPath `
    --timing-calibration 'D:\Recovered-From-OldPC\I\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json' --output $intentPath
if (!(Test-Path -LiteralPath $intentPath)) { throw 'The original intent builder returned no authoritative date report.' }
$intents = Get-Content -LiteralPath $intentPath -Raw | ConvertFrom-Json
$target = @($intents.candidates | Where-Object { $_.target_qualified_item_id -eq $TargetItemId -and $_.target_location -eq $TargetLocation })
if ($target.Count -ne 1 -or $intents.source_state_hash -ne $snapshot.state_hash) { throw 'No unique supported target and date window on the live source.' }
$goal = 'goal.fishing.complete_master_angler'
$request = @{
    goal_id = $goal
    execution_mode = 'training_singleplayer'
    state_hash = $snapshot.state_hash
    include_blocked_options = $true
    additional_candidates = @(@{ option_id = $target[0].option_id; parameters = $target[0].parameters })
}
$rank = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/planner/baseline/rank-options') -Method Post `
    -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 16 -Compress) -TimeoutSec 45
$rankPath = Join-Path $dir.FullName 'rank-response.json'
$rank | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $rankPath -Encoding utf8
$selected = $rank.ranked_event_candidates | Where-Object {
    $_.available -and $_.option_id -eq $target[0].option_id -and
    ($_.parameters | Where-Object { $_.name -eq 'master_angler_target_qualified_item_id' -and $_.value -eq $TargetItemId }) -and
    ($_.parameters | Where-Object { $_.name -eq 'master_angler_target_location' -and $_.value -eq $TargetLocation })
} | Sort-Object rank | Select-Object -First 1
if (!$selected) { throw 'The original planner did not compile an available target candidate on this live source.' }
& (Join-Path $PSScriptRoot 'Invoke-IsolatedRankedQueue.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt -RankPath $rankPath `
    -CandidateId $selected.candidate_id -ExpectedStateHash $snapshot.state_hash -GoalId $goal -Attempt $Attempt `
    -CandidateParameters $target[0].parameters -RetainFullExecutionSnapshots `
    -MaximumAttempts $MaximumAttempts -RequiredVerifiedActions $RequiredVerifiedActions -StopAfterObjectiveComplete:$StopAfterObjectiveComplete
exit $LASTEXITCODE
