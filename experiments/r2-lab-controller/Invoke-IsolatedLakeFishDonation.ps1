param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [Parameter(Mandatory)][ValidatePattern('^\(O\)[0-9]+$')][string]$ItemId,
    [int]$BundleId = 7
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$session = Get-Content -LiteralPath (Join-Path $runtime 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$proof = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $Attempt)
$url = $settings.ASPNETCORE_URLS
$bridge = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
$goal = 'goal.world_progress.complete_community_center'
foreach ($service in $session.processes) {
    $process = Get-Process -Id $service.id
    if ($process.Path -cne $service.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($service.started).UtcDateTime) {
        throw 'Original isolation process identity changed.'
    }
}

function Read-Source {
    $data = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
    return @{ Json = $data; Snapshot = ($data | ConvertFrom-Json) }
}

function Get-NativeBundle($snapshot) {
    $bundle = @($snapshot.state.world_progress.community_center.value.bundle_rows | Where-Object {
        $_.bundle_id -eq $BundleId -and $_.projection_status -ceq 'exact'
    })
    if ($bundle.Count -ne 1) { throw 'The requested native bundle is unavailable or ambiguous.' }
    return $bundle[0]
}

function Get-ItemCount($snapshot) {
    return [int](($snapshot.state.player.inventory.value | Where-Object qualified_item_id -ceq $ItemId | Measure-Object stack -Sum).Sum)
}

$source = Read-Source
$before = $source.Snapshot
$bundle = Get-NativeBundle $before
$row = @($bundle.ingredients | Where-Object item_id_or_category -ceq $ItemId.Substring(3))
$beforeCount = Get-ItemCount $before
if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
    $before.state.player.location_id.value -cne 'CommunityCenter' -or !$bundle.note_appears -or
    $row.Count -ne 1 -or $row[0].completed -or $row[0].required_stack -ne 1 -or $beforeCount -lt 1) {
    throw 'The native scroll and exact held item do not bind a pending donation.'
}
[IO.File]::WriteAllText((Join-Path $proof.FullName 'before.json'), $source.Json)
$null = Invoke-RestMethod -Uri ($url + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($source.Json)) -TimeoutSec 45
$request = @{ state_hash=$before.state_hash; goal_id=$goal; execution_mode='training_singleplayer'; include_blocked_options=$true; candidates=@(@{option_id='community_center.donate_bundle_items';explicit_confirmation_granted=$true}) }
$rank = Invoke-RestMethod -Uri ($url + '/api/v1/planner/baseline/rank-options') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 15 -Compress) -TimeoutSec 45
$rank | ConvertTo-Json -Depth 35 | Set-Content -LiteralPath (Join-Path $proof.FullName 'rank.json') -Encoding utf8
$choice = $rank.ranked_event_candidates | Where-Object {
    $_.available -and $_.kind -ceq 'donate_community_center_item' -and $_.qualified_item_id -ceq $ItemId -and
    $_.candidate_id -like ('community-center-donate:' + $BundleId + ':*') -and $_.quantity -eq 1
} | Sort-Object slot_index,rank | Select-Object -First 1
if (!$choice -or $rank.goal_resolution.source_state_hash -cne $before.state_hash) {
    throw 'The existing planner did not admit this exact native donation.'
}
$request = @{ state_hash=$before.state_hash; goal_id=$goal; execution_mode='training_singleplayer'; max_candidates=1; compile_action_queue=$true; ranked_event_candidates=@($choice) }
$plan = Invoke-RestMethod -Uri ($url + '/api/v1/planner/daily-plan/compile') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 32 -Compress) -TimeoutSec 45
$plan | ConvertTo-Json -Depth 44 | Set-Content -LiteralPath (Join-Path $proof.FullName 'compile.json') -Encoding utf8
$queue = $plan.action_queue
if ($queue.status -ne 'pending' -or $queue.state_hash -cne $before.state_hash -or !($queue.items.option_id -ccontains 'executor.donate_community_center_item')) {
    throw 'The original donation did not compile on the selected snapshot.'
}
$queuePath = Join-Path $proof.FullName 'queue.json'
$queue | ConvertTo-Json -Depth 44 | Set-Content -LiteralPath $queuePath -Encoding utf8
$nativeArgs = Get-Content -LiteralPath (Join-Path $RunDirectory 'spring27-chest-adapter-replay\execution-arguments.json') -Raw | ConvertFrom-Json
$nativeArgs[[array]::IndexOf($nativeArgs,'--precompiled-queue') + 1] = $queuePath
$nativeArgs[[array]::IndexOf($nativeArgs,'--max-queue-item-attempts') + 1] = '12'
$nativeArgs[[array]::IndexOf($nativeArgs,'--max-persisted-iterations') + 1] = '800'
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$settings[$key],'Process') }
& 'D:\StardewAI-TestLab\dotnet\dotnet.exe' @nativeArgs *> (Join-Path $proof.FullName 'native.log')
$code = $LASTEXITCODE
$afterSource = Read-Source
$after = $afterSource.Snapshot
[IO.File]::WriteAllText((Join-Path $proof.FullName 'after.json'), $afterSource.Json)
$result = Get-NativeBundle $after
$done = $result.ingredients | Where-Object item_id_or_category -ceq $ItemId.Substring(3)
$afterCount = Get-ItemCount $after
if ($code -ne 0 -or !$done.completed -or $beforeCount - $afterCount -ne 1 -or $after.player_id.value -cne $before.player_id.value) {
    throw 'The native ingredient and inventory deltas did not both verify.'
}
[ordered]@{ before=$before.state_hash; after=$after.state_hash; bundle_id=$BundleId; item=$ItemId; before_inventory=$beforeCount; after_inventory=$afterCount; ingredient_index=$done.ingredient_index; total_completed_ingredients=$result.completed_ingredient_count; time=$after.in_game_time.value } |
    ConvertTo-Json | Tee-Object -FilePath (Join-Path $proof.FullName 'verified.json')
