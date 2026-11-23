param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][int]$ExpectedTotalDay,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt
)

$ErrorActionPreference = 'Stop'
$settings = Get-Content -Raw -LiteralPath (Join-Path $RunDirectory ($RuntimeAttempt + '\runtime-settings.json')) | ConvertFrom-Json -AsHashtable
$folder = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-evidence'))
$bridge = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
$backend = $settings.ASPNETCORE_URLS
$goal = 'goal.world_progress.complete_community_center'
$expected = @('(O)254', '(O)256', '(O)258', '(O)260')

for ($stage = 1; $stage -le 5; $stage++) {
    $body = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
    $before = $body | ConvertFrom-Json
    if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
        $before.state.time.total_days.value -ne $ExpectedTotalDay -or $before.state.player.location_id.value -cne 'Farm') {
        throw 'Crop care left the expected save, player, date or Farm.'
    }
    $crops = @($before.state.farm.crops.value | Where-Object harvest_item_qualified_id -cin $expected)
    if ($crops.Count -ne 4 -or @($crops | Where-Object dead).Count) { throw 'The four original crops changed.' }
    if (!@($crops | Where-Object needs_watering).Count) {
        [IO.File]::WriteAllText((Join-Path $folder 'after.json'), $body)
        [ordered]@{
            save=$before.save_id.value; player=$before.player_id.value; day=$ExpectedTotalDay
            after=$before.state_hash; water=$before.state.player.watering_can_state.value.water_left
            crops=@($crops | Select-Object tile_x,tile_y,harvest_item_qualified_id,watered,days_until_next_harvest_if_watered)
        } | ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $folder 'verified.json')
        return
    }
    [IO.File]::WriteAllText((Join-Path $folder ('before-' + $stage + '.json')), $body)
    $null = Invoke-RestMethod -Uri ($backend + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 45
    $request = @{
        goal_id=$goal; execution_mode='training_singleplayer'; state_hash=$before.state_hash
        include_blocked_options=$true; candidate_option_ids=@('farm.maintain_crops')
    }
    $ranking = Invoke-RestMethod -Uri ($backend + '/api/v1/planner/baseline/rank-options') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Compress) -TimeoutSec 45
    $path = Join-Path $folder ('rank-' + $stage + '.json')
    $ranking | ConvertTo-Json -Depth 45 | Set-Content -LiteralPath $path -Encoding utf8
    $candidate = $ranking.ranked_event_candidates | Where-Object {
        $_.available -and $_.option_id -ceq 'farm.maintain_crops' -and $_.kind -ceq 'water_crop_tile'
    } | Sort-Object rank | Select-Object -First 1
    if (!$candidate) { throw 'Original crop-care planner did not admit a watering candidate.' }
    $dispatch = @{
        RunDirectory=$RunDirectory; RuntimeAttempt=$RuntimeAttempt; RankPath=$path
        CandidateId=$candidate.candidate_id; ExpectedStateHash=$before.state_hash; GoalId=$goal
        Attempt=($Attempt + '-' + $stage)
    }
    & (Join-Path $PSScriptRoot 'Invoke-IsolatedRankedQueue.ps1') @dispatch
    if ($LASTEXITCODE -ne 0) { throw 'Original native watering stopped.' }
}
throw 'The current crop-care stage budget finished without a verified terminal state.'
