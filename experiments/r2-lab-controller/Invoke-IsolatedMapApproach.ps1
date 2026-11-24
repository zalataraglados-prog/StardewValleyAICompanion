param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [Parameter(Mandatory)][ValidateRange(1, 12)][int]$MaximumConnectors
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$lab = Split-Path -Parent $PSScriptRoot
$binary = Join-Path $lab 'published\c42508c1-managed-lab\live-training-loop'
$folder = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-plan'))
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($leg in 1..$MaximumConnectors) {
    $body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 15).Content
    $fresh = $body | ConvertFrom-Json
    $location = $fresh.state.player.location_id.value
    if ($location -eq $Destination) { return }
    if (!$seen.Add($location)) { throw 'The verified map approach repeated a location.' }
    $snapshotPath = Join-Path $folder.FullName ('before-' + $leg + '.json')
    [IO.File]::WriteAllText($snapshotPath, $body)
    $selection = & (Join-Path $PSScriptRoot 'planner-prerequisite\Resolve-PrerequisiteRouteWithFarm.ps1') `
        -SnapshotPath $snapshotPath -OutputDirectory (Join-Path $folder.FullName ('route-' + $leg)) `
        -Destination $Destination -BinaryRoot $binary
    if ($selection.status -ne 'pending' -or $selection.first_action -ne 'executor.traverse_connector') {
        throw ('The original map planner stopped before a new verified connector. ' + $selection.status)
    }
    $url = $settings.ASPNETCORE_URLS
    $null = Invoke-RestMethod -Uri ($url + '/api/v1/snapshots') -Method Post `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 40
    $queue = Get-Content -LiteralPath $selection.queue -Raw | ConvertFrom-Json
    $goal = $queue.goal_id
    $request = @{
        goal_id = $goal
        execution_mode = 'training_singleplayer'
        state_hash = $fresh.state_hash
        include_blocked_options = $false
        candidate_option_ids = @('exploration.visit_location')
    }
    $rank = Invoke-RestMethod -Uri ($url + '/api/v1/planner/baseline/rank-options') -Method Post `
        -ContentType 'application/json' -Body ($request | ConvertTo-Json -Compress) -TimeoutSec 40
    $rankPath = Join-Path $folder.FullName ('rank-' + $leg + '.json')
    $rank | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $rankPath -Encoding utf8
    if ($queue.state_hash -cne $fresh.state_hash -or $queue.status -cne 'pending' -or
        @($rank.ranked_event_candidates | Where-Object { $_.available -and $_.candidate_id -ceq $selection.candidate }).Count -ne 1) {
        throw 'The original resolved route no longer binds one available native connector.'
    }
    # Execute the exact route compiler output. Recompiling the same selection
    # through a daily-plan slice introduced its unrelated default 30-minute cap.
    & (Join-Path $PSScriptRoot 'Invoke-IsolatedCompiledQueue.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt `
        -QueuePath $selection.queue -Attempt ($Attempt + '-' + $leg)
}
$after = Invoke-RestMethod -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 15
if ($after.state.player.location_id.value -ne $Destination) { throw 'The selected map has not been reached within the planned connector count.' }
