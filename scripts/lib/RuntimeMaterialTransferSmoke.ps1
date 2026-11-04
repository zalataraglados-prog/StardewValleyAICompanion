function Get-RuntimeMaterialNode($Graph, [string] $NodeId) {
    $nodes = @($Graph.inventory_nodes | Where-Object node_id -CEQ $NodeId)
    if ($nodes.Count -ne 1) { throw "Material node is not unique: $NodeId" }
    return $nodes[0]
}

function Get-RuntimeMaterialQuantity($Node, [string] $QualifiedItemId, [int] $Quality) {
    return [int](($Node.slots | Where-Object {
        $_.qualified_item_id -ceq $QualifiedItemId -and $_.quality -eq $Quality
    } | Measure-Object -Property stack -Sum).Sum)
}

function Get-RuntimeMaterialSlot($Node, [string] $QualifiedItemId, [int] $Quality) {
    $slots = @($Node.slots | Where-Object {
        $_.qualified_item_id -ceq $QualifiedItemId -and $_.quality -eq $Quality
    })
    if ($slots.Count -ne 1) { throw "Fixture item slot is not unique: $QualifiedItemId/$Quality" }
    return $slots[0]
}

function Assert-RuntimeMaterialNativeLineage {
    param($BeforeGraph, $AfterGraph, $Intent, $RecordedProjection, [string] $CoreAssemblyPath)
    $type = ([Reflection.Assembly]::LoadFrom($CoreAssemblyPath)).GetType('StardewAI.Core.Execution.MaterialTransferProjector')
    $project = $type.GetMethod('Project')
    $verify = $type.GetMethod('VerifyNativeSlots')
    $types = @($project.GetParameters().ParameterType)
    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.PropertyNamingPolicy = [System.Text.Json.JsonNamingPolicy]::SnakeCaseLower
    $values = @($BeforeGraph, $Intent, $AfterGraph)
    $objects = [object[]]::new(3)
    for ($i=0; $i -lt 3; $i++) {
        $target = if ($i -eq 1) { $types[1] } else { $types[0] }
        $objects[$i] = [System.Text.Json.JsonSerializer]::Deserialize(
            ($values[$i] | ConvertTo-Json -Depth 100 -Compress), $target, $options)
    }
    $arguments = [object[]]::new(2)
    $arguments[0] = $objects[0]
    $arguments[1] = $objects[1]
    $projection = $project.Invoke([Activator]::CreateInstance($type), $arguments)
    if ($projection.Status -cne 'projected' -or $projection.NativeSlotTransitions.Count -eq 0) {
        throw ('Original transfer projector rejected source: ' + ($projection.BlockingReasons -join ','))
    }
    $actual = [System.Text.Json.JsonSerializer]::Serialize($projection, $projection.GetType(), $options)
    if (-not [System.Text.Json.Nodes.JsonNode]::DeepEquals(
        [System.Text.Json.Nodes.JsonNode]::Parse($actual),
        [System.Text.Json.Nodes.JsonNode]::Parse(($RecordedProjection | ConvertTo-Json -Depth 100 -Compress)))) {
        throw 'Native receipt projection differs from original independent reconstruction.'
    }
    $arguments = [object[]]::new(3)
    $arguments[0] = $objects[0]
    $arguments[1] = $objects[2]
    $arguments[2] = $projection
    $errors = @($verify.Invoke($null, $arguments))
    if ($errors.Count -ne 0) { throw ('Native slot lineage mismatch: ' + ($errors -join ',')) }
}

function Invoke-RuntimeMaterialTransferDirection {
    param($Context, [string] $Name, $Before, [string] $BeforePath, [string] $SourceNodeId, [string] $DestinationNodeId,
        [int] $SourceSlotIndex, [int] $Quantity, [int] $ExpectedSourceStack)
    $sourcePath = Join-Path $Context.ArtifactDirectory "$Name-source.json"
    $core = Join-Path (Split-Path -Parent $Context.LoopDll) 'StardewAI.Core.dll'
    $raw = Read-RuntimeLogicalJsonArtifact -Path $BeforePath -CoreAssemblyPath $core -Raw
    Save-RuntimeSnapshotAndIngest -Path $sourcePath -Capture ([pscustomobject]@{
        Value = $Before; Raw = $raw
    }) -BackendUrl $Context.BackendUrl -SnapshotProfile $Context.SnapshotProfile
    $parameters = @(
        @{name='source_node_id';value=$SourceNodeId}, @{name='destination_node_id';value=$DestinationNodeId},
        @{name='source_slot_index';value=[string]$SourceSlotIndex}, @{name='qualified_item_id';value='(O)388'},
        @{name='quality';value='4'}, @{name='quantity';value=[string]$Quantity},
        @{name='expected_source_stack';value=[string]$ExpectedSourceStack}
    )
    $ranking = Invoke-RuntimeRanking -BackendUrl $Context.BackendUrl -GoalId 'daily.closed_loop' `
        -SnapshotStateHash $Before.state_hash -OutputPath (Join-Path $Context.ArtifactDirectory "$Name-ranking.json") `
        -OptionId 'inventory.transfer_item' -Parameters $parameters
    $candidates = @($ranking.ranked_event_candidates | Where-Object {
        $_.option_id -ceq 'inventory.transfer_item' -and $_.kind -ceq 'transfer_inventory_item' -and $_.available -eq $true
    })
    if ($candidates.Count -ne 1) { throw "Expected one exact $Name transfer candidate." }
    $result = Invoke-RuntimeDailyPlanStep -Context $Context -Name $Name -OptionId 'inventory.transfer_item' `
        -CandidateId $candidates[0].candidate_id -CandidateKind $candidates[0].kind `
        -CandidateParameters @($parameters | ForEach-Object { "$($_.name)=$($_.value)" }) `
        -SourceSnapshotPath $sourcePath -ExecutionTarget runtime_harness
    $steps = @($result.Execution.step_results | Where-Object option_id -CEQ 'executor.transfer_material')
    if ($steps.Count -ne 1) { throw "$Name needs one native transfer receipt." }
    $receipt = $steps[0]
    if ($receipt.status -cne 'applied' -or $receipt.primitive_verification_status -cne 'verified' -or
        $receipt.material_transfer_native_menu_opened -ne $true -or
        $receipt.material_transfer_native_lock_released -ne $true -or
        $receipt.material_transfer_click_count -ne $Quantity) { throw "$Name native transfer/menu lifecycle failed." }
    $beforeGraph = Read-StateValue $result.Before 'farm' 'material_inventory_graph'
    $afterGraph = Read-StateValue $result.After 'farm' 'material_inventory_graph'
    Assert-RuntimeMaterialNativeLineage -BeforeGraph $beforeGraph -AfterGraph $afterGraph `
        -Intent $receipt.material_transfer_intent -RecordedProjection $receipt.material_transfer_projection -CoreAssemblyPath $core
    $sourceBefore = Get-RuntimeMaterialNode $beforeGraph $SourceNodeId
    $sourceAfter = Get-RuntimeMaterialNode $afterGraph $SourceNodeId
    $destinationBefore = Get-RuntimeMaterialNode $beforeGraph $DestinationNodeId
    $destinationAfter = Get-RuntimeMaterialNode $afterGraph $DestinationNodeId
    if ((Get-RuntimeMaterialQuantity $sourceBefore '(O)388' 4) - (Get-RuntimeMaterialQuantity $sourceAfter '(O)388' 4) -ne $Quantity -or
        (Get-RuntimeMaterialQuantity $destinationAfter '(O)388' 4) - (Get-RuntimeMaterialQuantity $destinationBefore '(O)388' 4) -ne $Quantity) {
        throw "$Name inventory conservation failed."
    }
    $episodePath = Join-Path (Split-Path -Parent $result.ExecutionPath) 'plan-execution-episode-0001.json'
    $episode = Get-Content -LiteralPath $episodePath -Raw | ConvertFrom-Json
    if ($episode.option_id -cne 'executor.transfer_material' -or
        $episode.material_transfer_native_menu_opened -ne $true -or $episode.material_transfer_native_lock_released -ne $true -or
        $null -eq $episode.material_transfer_projection.native_slot_transitions) { throw "$Name episode lost native lineage." }
    return [pscustomobject]@{ After=$result.After; AfterPath=$result.AfterPath; Execution=$receipt
        ExecutionPath=$result.ExecutionPath; QueuePath=$result.QueuePath; EpisodePath=$episodePath }
}

function Assert-RuntimeMaterialStaleProjection {
    param($Context, $After, [string] $ChestNodeId, [string] $PlayerNodeId, [int] $TargetTileX, [int] $TargetTileY)
    $graph = Read-StateValue $After 'farm' 'material_inventory_graph'
    $chest = Get-RuntimeMaterialNode $graph $ChestNodeId
    $slot = Get-RuntimeMaterialSlot $chest '(O)388' 4
    $request = [ordered]@{
        schema_version='training_execution_request.v1';run_id=$Context.RunId;queue_id="$($Context.RunId).negative"
        queue_item_id="$($Context.RunId).negative.stale_source";before_state_hash=$After.state_hash
        option_id='executor.transfer_material';execution_mode='training_singleplayer';actor='training_farmer.main'
        save_isolation_path=$Context.IsolatedSavesPath;request_nonce=[guid]::NewGuid().ToString('N')
        created_at=[DateTimeOffset]::UtcNow.ToString('O');location_id='Farm';target_tile_x=$TargetTileX;target_tile_y=$TargetTileY
        stand_tile_x=$After.state.player.tile_x.value;stand_tile_y=$After.state.player.tile_y.value;max_movement_tiles=512
        material_transfer_intent=@{schema_version='material_transfer_intent.v1';source_node_id=$ChestNodeId;destination_node_id=$PlayerNodeId
            source_slot_index=$slot.slot_index;qualified_item_id='(O)388';quality=4;quantity=1;expected_source_stack=([int]$slot.stack - 1)}
        material_transfer_projection=@{schema_version='material_transfer_projection.v1';status='projected'
            native_branch='negative_stale_projection_must_not_execute';source_stack_after=([int]$slot.stack - 2)
            destination_quantity_before=0;destination_quantity_after=1;destination_slot_changes=@();blocking_reasons=@()}
    }
    $response = Invoke-JsonPost -Url "$($Context.ExecutorRoot)/api/v1/training/execute" -Body $request
    Write-Utf8Text -Path (Join-Path $Context.ArtifactDirectory 'negative-stale-result.json') -Value $response.Raw
    $fresh = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 30
    Write-Utf8Text -Path (Join-Path $Context.ArtifactDirectory 'negative-stale-after-snapshot.json') -Value $fresh.Raw
    $currentGraph = Read-StateValue $fresh.Value 'farm' 'material_inventory_graph'
    if ($response.Value.status -cne 'blocked' -or $response.Value.material_transfer_click_count -ne 0 -or
        @($response.Value.block_reasons) -notcontains 'material_transfer_source_projection_drifted') { throw 'Stale projection did not fail closed.' }
    foreach ($id in @($ChestNodeId,$PlayerNodeId)) {
        $old = Get-RuntimeMaterialNode $graph $id
        $new = Get-RuntimeMaterialNode $currentGraph $id
        if (-not [System.Text.Json.Nodes.JsonNode]::DeepEquals(
            [System.Text.Json.Nodes.JsonNode]::Parse((ConvertTo-Json -InputObject @($old.slots) -Depth 50 -Compress)),
            [System.Text.Json.Nodes.JsonNode]::Parse((ConvertTo-Json -InputObject @($new.slots) -Depth 50 -Compress)))) {
            throw 'Rejected projection changed inventory slots.'
        }
    }
}
