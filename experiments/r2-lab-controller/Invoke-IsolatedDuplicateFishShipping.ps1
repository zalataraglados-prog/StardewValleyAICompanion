param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [Parameter(Mandatory)][ValidateCount(1,12)][int[]]$InventorySlots
)

$ErrorActionPreference = 'Stop'
if (@($InventorySlots | Select-Object -Unique).Count -ne $InventorySlots.Count) { throw 'Duplicate requested inventory slot.' }
$settings = Get-Content -LiteralPath (Join-Path $RunDirectory ($RuntimeAttempt + '\runtime-settings.json')) -Raw | ConvertFrom-Json -AsHashtable
$folder = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-evidence'))
$goal = 'goal.world_progress.complete_community_center'
$verified = [Collections.Generic.List[object]]::new()

foreach ($slotIndex in $InventorySlots) {
    $shipped = $false
    for ($routePass = 0; $routePass -lt 2; $routePass++) {
        $raw = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
        $before = $raw | ConvertFrom-Json
        if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or
            $before.state.player.location_id.value -cne 'Farm') { throw 'Shipping requires the same isolated farmer on the Farm.' }
        $held = @($before.state.player.inventory.value | Where-Object slot_index -eq $slotIndex)
        if ($held.Count -ne 1 -or $held[0].stack -ne 1 -or $held[0].category -ne -4 -or
            $held[0].protected_from_auto_sell -or !$held[0].can_be_shipped) { throw 'Only a disposable single-item fish stack can release a slot.' }
        $item = $held[0].qualified_item_id
        $allPhysical = @($before.state.farm.material_inventory_graph.value.inventory_nodes | ForEach-Object { $_.slots } | Where-Object qualified_item_id -ceq $item)
        $physicalCount = [int](($allPhysical | Measure-Object stack -Sum).Sum)
        if ($physicalCount -le 1) { throw 'Keep at least one physical copy; collection history is insufficient.' }
        $prefix = Join-Path $folder.FullName ('slot-' + $slotIndex + '-pass-' + $routePass)
        [IO.File]::WriteAllText(($prefix + '-before.json'), $raw)
        $null = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($raw)) -TimeoutSec 45
        $request = @{state_hash=$before.state_hash;goal_id=$goal;execution_mode='training_singleplayer';include_blocked_options=$true;candidates=@(@{option_id='economy.ship_items';explicit_confirmation_granted=$true})}
        $rank = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/planner/baseline/rank-options') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 12 -Compress) -TimeoutSec 45
        $rank | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath ($prefix + '-rank.json') -Encoding utf8
        $candidate = @($rank.ranked_event_candidates | Where-Object {
            $_.available -and $_.option_id -ceq 'economy.ship_items' -and $_.kind -ceq 'ship_inventory_item_to_bin' -and
            $_.slot_index -eq $slotIndex -and $_.qualified_item_id -ceq $item -and $_.quantity -eq 1
        })
        if ($candidate.Count -ne 1 -or $rank.goal_resolution.source_state_hash -cne $before.state_hash) { throw 'The original planner did not admit this exact one-item shipping candidate.' }
        $request = @{state_hash=$before.state_hash;goal_id=$goal;execution_mode='training_singleplayer';max_candidates=1;compile_action_queue=$true;ranked_event_candidates=@($candidate[0])}
        $compiled = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/planner/daily-plan/compile') -Method Post -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 40 -Compress) -TimeoutSec 45
        $compiled | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath ($prefix + '-compile.json') -Encoding utf8
        $queue = $compiled.action_queue
        if ($queue.status -cne 'pending' -or $queue.state_hash -cne $before.state_hash -or
            !($queue.items.option_id -ccontains 'executor.ship_inventory_item_to_bin') -or
            @($queue.items | Where-Object option_id -cnotin @('executor.move_to_tile','executor.ship_inventory_item_to_bin')).Count) {
            throw 'The unchanged original compiler did not return a bounded shipping queue.'
        }
        $queuePath = $prefix + '-queue.json'
        $queue | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $queuePath -Encoding utf8
        if (@($queue.items).Count -gt 1) {
            if ($routePass -ne 0 -or @($queue.items).Count -ne 2 -or $queue.items[0].option_id -cne 'executor.move_to_tile') {
                throw 'Unexpected original shipping approach; do not dispatch a multi-source shipping queue.'
            }
            # The strict native reader binds one action frame. Preserve the original
            # planned destination, compile that approach independently, then re-rank
            # shipping on its real arrival frame. Never restamp the original queue.
            $moveParameters = @($queue.items[0].normalized_command.parameters | Where-Object name -cin @('target_location','target_tile_x','target_tile_y'))
            if ($moveParameters.Count -ne 3 -or @($moveParameters | Where-Object { $null -eq $_.value }).Count) {
                throw 'The original normalized movement command lacks one exact destination.'
            }
            $approach = @{schema_version='small_model_action.v1';model_output_id='original-shipping-approach';source_model='retained_original_shipping_route';state_hash=$before.state_hash;goal_id=$goal;execution_mode='training_singleplayer';actor=@{actor_id='training_farmer.main';actor_type='training_farmer';control_surface='training_sandbox'};actions=@(@{action_id='reach-original-shipping-stand';option_id='executor.move_to_tile';parameters=$moveParameters})}
            $approach | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath ($prefix + '-approach-request.json') -Encoding utf8
            $approachQueue = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/api/v1/small-model/action-queue/compile') -Method Post -ContentType 'application/json' -Body ($approach | ConvertTo-Json -Depth 20 -Compress) -TimeoutSec 45
            if ($approachQueue.status -cne 'pending' -or @($approachQueue.items).Count -ne 1 -or $approachQueue.items[0].option_id -cne 'executor.move_to_tile') {
                throw 'The original compiler did not admit the selected approach independently.'
            }
            $approachPath = $prefix + '-approach-queue.json'
            $approachQueue | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $approachPath -Encoding utf8
            & (Join-Path $PSScriptRoot 'Invoke-IsolatedCompiledQueue.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt -QueuePath $approachPath -Attempt ($Attempt + '-slot-' + $slotIndex + '-approach')
            continue
        }
        & (Join-Path $PSScriptRoot 'Invoke-IsolatedCompiledQueue.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt -QueuePath $queuePath -Attempt ($Attempt + '-slot-' + $slotIndex)
        $rawAfter = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
        $after = $rawAfter | ConvertFrom-Json
        [IO.File]::WriteAllText(($prefix + '-after.json'), $rawAfter)
        $beforeHeld = [int](($before.state.player.inventory.value | Where-Object qualified_item_id -ceq $item | Measure-Object stack -Sum).Sum)
        $afterHeld = [int](($after.state.player.inventory.value | Where-Object qualified_item_id -ceq $item | Measure-Object stack -Sum).Sum)
        if ($after.save_id.value -cne $before.save_id.value -or $after.player_id.value -cne $before.player_id.value -or
            $after.state.time.total_days.value -ne $before.state.time.total_days.value -or
            $afterHeld -ne ($beforeHeld - 1) -or
            $after.state.player.inventory_capacity.value.empty_slots -ne ($before.state.player.inventory_capacity.value.empty_slots + 1)) {
            throw 'Native shipping did not release exactly one verified slot.'
        }
        $verified.Add([ordered]@{slot=$slotIndex;item=$item;quantity=1;quality=$held[0].quality;physical_copies_before=$physicalCount;inventory_before=$beforeHeld;inventory_after=$afterHeld;before=$before.state_hash;after=$after.state_hash;queue=$queuePath;queue_sha256=(Get-FileHash -LiteralPath $queuePath).Hash;empty_slots=$after.state.player.inventory_capacity.value.empty_slots})
        $shipped = $true
        break
    }
    if (!$shipped) { throw 'The bounded approach did not produce a separately compiled shipping frame.' }
}
[ordered]@{purpose='Bounded warm-run inventory capacity recovery through the original shipping compiler; no global ledger or training admission claim.';verified_transfers=$verified.ToArray();formal_training_authorized=$false} |
    ConvertTo-Json -Depth 8 | Tee-Object -FilePath (Join-Path $folder.FullName 'verified.json')
