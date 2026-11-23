param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [string]$VerifiedPlacementPath,
    [string[]]$ItemIds = @('(O)472', '(O)427', '(O)495'),
    [hashtable]$ItemQualities = @{},
    [switch]$Withdraw
)

$ErrorActionPreference = 'Stop'
if ($Withdraw -and !$VerifiedPlacementPath) { throw 'Withdrawal requires the live binding of a verified chest.' }
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$folder = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-evidence'))
$goal = 'goal.world_progress.complete_community_center'

function Read-FarmSource {
    $body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 15).Content
    $fresh = $body | ConvertFrom-Json
    if ($fresh.state.player.location_id.value -ne 'Farm' -or $fresh.state.menus.active_menu.value.is_open) {
        throw 'Native storage bootstrap requires the same clear Farm source.'
    }
    return @{ Body = $body; Snapshot = $fresh }
}

function Invoke-StorageCandidate($source, [string]$name, [string]$option, [string]$kind, [string]$item, [object[]]$inputs = @()) {
    $url = $settings.ASPNETCORE_URLS
    [IO.File]::WriteAllText((Join-Path $folder.FullName ($name + '-before.json')), $source.Body)
    $null = Invoke-RestMethod -Uri ($url + '/api/v1/snapshots') -Method Post `
        -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($source.Body)) -TimeoutSec 40
    $request = @{
        goal_id = $goal
        execution_mode = 'training_singleplayer'
        state_hash = $source.Snapshot.state_hash
        include_blocked_options = $true
    }
    if ($inputs.Count -gt 0) { $request.candidates = @(@{ option_id = $option; parameters = $inputs }) }
    else { $request.candidate_option_ids = @($option) }
    $rank = Invoke-RestMethod -Uri ($url + '/api/v1/planner/baseline/rank-options') -Method Post `
        -ContentType 'application/json' -Body ($request | ConvertTo-Json -Depth 16 -Compress) -TimeoutSec 45
    $rankPath = Join-Path $folder.FullName ($name + '-ranking.json')
    $rank | ConvertTo-Json -Depth 36 | Set-Content -LiteralPath $rankPath -Encoding utf8
    $choice = $rank.ranked_event_candidates | Where-Object {
        $_.available -and $_.option_id -eq $option -and $_.kind -eq $kind -and $_.qualified_item_id -eq $item
    } | Sort-Object rank | Select-Object -First 1
    if (!$choice) { throw "No native pending $name candidate on the exact source." }
    & (Join-Path $PSScriptRoot 'Invoke-IsolatedRankedQueue.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt `
        -RankPath $rankPath -CandidateId $choice.candidate_id -ExpectedStateHash $source.Snapshot.state_hash -GoalId $goal `
        -Attempt ($Attempt + '-' + $name) -CandidateParameters $inputs
    if ($LASTEXITCODE -ne 0) { throw "Native $name did not verify." }
}

$start = Read-FarmSource
if ($VerifiedPlacementPath) {
    $placement = Get-Content -LiteralPath $VerifiedPlacementPath -Raw | ConvertFrom-Json
    if ($placement.after -cne $start.Snapshot.state_hash -or !$placement.chest_node_id) {
        throw 'The verified placement does not bind the live storage source.'
    }
}
else {
    $recipe = @($start.Snapshot.state.player.storage_crafting.value.rows | Where-Object recipe_name -ceq 'Chest')
    if ($recipe.Count -ne 1 -or !$recipe[0].has_ingredients_for_one -or !$recipe[0].output_inventory_acceptance_after_material_consumption) {
        throw 'The native chest recipe cannot currently consume its ingredients and fit the chest in inventory.'
    }
    Invoke-StorageCandidate $start 'craft' 'farm.process_machines' 'craft_storage_item' '(BC)130'
    $carrying = Read-FarmSource
    if (@($carrying.Snapshot.state.player.inventory.value | Where-Object qualified_item_id -eq '(BC)130').Count -ne 1) {
        throw 'The single wooden chest is missing after native crafting.'
    }
    Invoke-StorageCandidate $carrying 'place' 'farm.process_machines' 'place_storage_item' '(BC)130'
}

foreach ($id in $ItemIds) {
    $before = Read-FarmSource
    $graph = $before.Snapshot.state.farm.material_inventory_graph.value
    $player = @($graph.inventory_nodes | Where-Object { $_.inventory_kind -eq 'player_inventory' -and $_.actor_use_authorized })
    $storage = @($graph.access_points | Where-Object {
        $_.access_kind -eq 'placed_chest' -and $_.location_id -eq 'Farm' -and $_.actor_use_authorized -and !$_.locked_by_other_player
    })
    if ($player.Count -ne 1 -or $storage.Count -ne 1) { throw 'No unique actor inventory and writable placed chest.' }
    if ($VerifiedPlacementPath -and $storage[0].node_id -cne $placement.chest_node_id) {
        throw 'The authoritative current material graph no longer identifies the verified chest.'
    }
    $chest = $graph.inventory_nodes | Where-Object node_id -ceq $storage[0].node_id
    $sourceNode = if ($Withdraw) { $chest } else { $player[0] }
    $destinationNode = if ($Withdraw) { $player[0] } else { $chest }
    $slot = @($sourceNode.slots | Where-Object {
        $_.qualified_item_id -ceq $id -and (!$ItemQualities.ContainsKey($id) -or $_.quality -eq $ItemQualities[$id])
    })
    if ($slot.Count -ne 1 -or $slot[0].stack -le 0) { throw 'The expected inventory stack changed.' }
    $priorStored = [int](($destinationNode.slots | Where-Object { $_.qualified_item_id -ceq $id -and $_.quality -eq $slot[0].quality } | Measure-Object stack -Sum).Sum)
    $inputs = @{
        source_node_id = $sourceNode.node_id
        destination_node_id = $destinationNode.node_id
        source_slot_index = $slot[0].slot_index
        qualified_item_id = $id
        quality = $slot[0].quality
        quantity = $slot[0].stack
        expected_source_stack = $slot[0].stack
    }.GetEnumerator() | ForEach-Object { @{ name = $_.Key; value = $_.Value.ToString() } }
    $attemptItem = ($id -replace '^\([A-Z]+\)','').ToLowerInvariant()
    Invoke-StorageCandidate $before ('cache-' + $attemptItem) 'inventory.transfer_item' 'transfer_inventory_item' $id $inputs
    $after = (Read-FarmSource).Snapshot
    if (!$Withdraw -and $after.state.player.inventory_capacity.value.empty_slots -le $before.Snapshot.state.player.inventory_capacity.value.empty_slots) {
        throw 'The verified native transfer did not release an inventory slot.'
    }
    $afterNodes = $after.state.farm.material_inventory_graph.value.inventory_nodes
    $afterDestination = $afterNodes | Where-Object node_id -ceq $destinationNode.node_id
    $afterSource = $afterNodes | Where-Object node_id -ceq $sourceNode.node_id
    $stored = ($afterDestination.slots | Where-Object { $_.qualified_item_id -ceq $id -and $_.quality -eq $slot[0].quality } | Measure-Object stack -Sum).Sum
    $stillInSource = @($afterSource.slots | Where-Object { $_.slot_index -eq $slot[0].slot_index -and $_.qualified_item_id -ceq $id })
    if (($stored - $priorStored) -ne $slot[0].stack -or $stillInSource.Count) {
        throw 'The exact native transfer did not balance across the two inventory nodes.'
    }
}
$done = (Read-FarmSource).Snapshot
[ordered]@{
    source = $start.Snapshot.state_hash
    result = $done.state_hash
    day = $done.state.time.day.value
    time = $done.state.time.time.value
    empty_slots = $done.state.player.inventory_capacity.value.empty_slots
} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $folder.FullName 'verified-ready.json')
