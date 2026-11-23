param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$RankPath,
    [Parameter(Mandatory)][string]$CandidateId,
    [Parameter(Mandatory)][string]$ExpectedStateHash,
    [Parameter(Mandatory)][string]$GoalId,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [object[]]$CandidateParameters = @(),
    [switch]$UseOriginalFreshSelection,
    [ValidateRange(1,1000000)][int]$MaxPersistedIterations = 600
)

$ErrorActionPreference = 'Stop'
$script:RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$live = Get-Content -LiteralPath (Join-Path $runtime 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$rank = Get-Content -LiteralPath $RankPath -Raw | ConvertFrom-Json
$rows = @($rank.ranked_event_candidates | Where-Object { $_.available -and $_.candidate_id -ceq $CandidateId })
if ($rows.Count -ne 1) { throw 'The retained ranking does not select one available exact candidate.' }
$selected = $rows[0]
if ($rank.goal_resolution.source_state_hash -ne $ExpectedStateHash -or $rank.goal_resolution.requested_goal_id -ne $GoalId) {
    throw 'The candidate ranking does not bind the requested live state and goal.'
}
foreach ($service in $live.processes) {
    $process = Get-Process -Id $service.id
    if ($process.HasExited -or $process.Path -ne $service.exe -or
        $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($service.started).UtcDateTime) {
        throw 'Original isolation process identity changed.'
    }
}
$snapshot = Invoke-RestMethod -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 15
if ($settings.STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT -ne $live.save_root -or
    $snapshot.state.environment.training_run_id.value -cne $live.run_id -or
    $snapshot.save_id.value -cne 'ProofFarm' -or $snapshot.player_id.value -cne '319842636613009663' -or
    (!$UseOriginalFreshSelection -and $snapshot.state_hash -ne $ExpectedStateHash)) {
    throw 'The fresh game or isolation binding no longer matches the selected decision.'
}
foreach ($setting in $settings.Keys) { [Environment]::SetEnvironmentVariable($setting, $settings[$setting], 'Process') }
$lab = Split-Path -Parent $PSScriptRoot
$build = Join-Path $lab 'published\c42508c1-managed-lab\live-training-loop\StardewAI.LiveTrainingLoop.dll'
$arguments = [Collections.Generic.List[string]]::new([string[]]@(
    $build, '--root', (Join-Path $RunDirectory 'training'), '--run-id', $live.run_id, '--no-manifest',
    '--backend-url', $settings.ASPNETCORE_URLS, '--bridge-snapshot-url', $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL,
    '--executor-url', $settings.STARDEWAI_PRODUCT_EXECUTOR_URL, '--goal', $GoalId,
    '--use-product-executor', '--skip-training', '--use-daily-plan', '--daily-plan-candidate-options', $selected.option_id,
    '--daily-plan-max-candidates', '1', '--daily-plan-candidate-kind', $selected.kind, '--daily-plan-candidate-id', $CandidateId,
    '--execution-snapshot-profile', 'full', '--snapshot-artifact-mode', 'content_addressed_gzip', '--artifact-retention-mode', 'stop',
    '--max-attempts', '1', '--required-verified-actions', '1', '--max-persisted-iterations', $MaxPersistedIterations.ToString(),
    '--sleep-ms', '1000', '--after-snapshot-wait-ms', '500', '--continue-after-blocked-queue-items',
    '--emit-queue-execution-receipt', '--max-queue-item-attempts', '12', '--save-isolation-path', $live.save_root, '--save-slot', $live.slot
))
foreach ($parameter in $CandidateParameters) {
    $arguments.Add('--daily-plan-candidate-parameter')
    $arguments.Add($parameter.name + '=' + $parameter.value)
}
$output = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $Attempt)
$arguments.ToArray() | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output.FullName 'loop-arguments.json') -Encoding utf8
$selection = [ordered]@{
    original_ranking_path = $RankPath
    original_ranking_sha256 = (Get-FileHash -LiteralPath $RankPath -Algorithm SHA256).Hash
    run_id = $live.run_id
    source_state_hash = $ExpectedStateHash
    selected_candidate = $selected.candidate_id
    option_id = $selected.option_id
    pre_controller_state_hash = $snapshot.state_hash
    original_controller_fresh_selection = [bool]$UseOriginalFreshSelection
    fresh_selection_scope = 'Original live loop re-ranks and compiles only the same goal, option, candidate kind/id and parameters on its freshly captured source; original Product/native precondition guards remain active.'
}
$selection | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output.FullName 'bound-selection.json') -Encoding utf8
$dotnet = $live.processes | Where-Object role -eq 'backend' | Select-Object -ExpandProperty exe
$argumentArray = $arguments.ToArray()
& $dotnet @argumentArray *> (Join-Path $output.FullName 'loop.log')
exit $LASTEXITCODE
