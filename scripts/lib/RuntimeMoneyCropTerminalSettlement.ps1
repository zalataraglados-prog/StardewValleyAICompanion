function New-RuntimeCropSettlementCapacityProof {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] [string] $DailyCapacityProofPath,
        [Parameter(Mandatory)] [string] $EvidenceDirectory
    )

    Assert-MoneyCropEvidenceFiles -Paths @(
        $BootstrapDll,
        $ProductionPlanPath,
        $DailyCapacityProofPath)
    $requestPath = Join-Path $EvidenceDirectory `
        "settlement-capacity-request.json"
    $proofPath = Join-Path $EvidenceDirectory `
        "settlement-capacity-proof.json"
    Write-JsonFile -Path $requestPath -Value ([ordered]@{
        schema_version =
            "money_revenue_crop_settlement_capacity_proof_request.v1"
        crop_production_plan_path = $ProductionPlanPath
        daily_capacity_proof_path = $DailyCapacityProofPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-crop-settlement-capacity-proof",
        "--request", $requestPath,
        "--output", $proofPath)
    return [IO.Path]::GetFullPath($proofPath)
}

function Invoke-RuntimeCropTerminalSettlement {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $BackendUrl,
        [Parameter(Mandatory)] [string] $SnapshotProfile,
        [Parameter(Mandatory)] [string] $SourceSnapshotPath,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] [string] $OutputRetentionProofPath,
        [Parameter(Mandatory)] [string] $EvidenceDirectory,
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $RunId,
        [Parameter(Mandatory)] [int] $TotalDay
    )

    Assert-MoneyCropEvidenceFiles -Paths @(
        $SourceSnapshotPath,
        $ProductionPlanPath,
        $OutputRetentionProofPath,
        $BootstrapDll)
    $retention = Get-Content -LiteralPath $OutputRetentionProofPath -Raw |
        ConvertFrom-Json
    if ([string]$retention.schema_version -ne
            "money_revenue_crop_output_retention_proof.v1" -or
        -not [bool]$retention.native_output_retention_verified -or
        [bool]$retention.formal_product_training_authorized -or
        [int]$retention.minimum_units -le 0 -or
        [long]$retention.minimum_gross_revenue -le 0 -or
        [string]::IsNullOrWhiteSpace(
            [string]$retention.harvest_item_qualified_id)) {
        throw "Money crop terminal retention proof is not executable."
    }
    if (-not [string]::Equals(
            [IO.Path]::GetFullPath($SourceSnapshotPath),
            [IO.Path]::GetFullPath([string]$retention.after_snapshot_path),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Money crop terminal source is not the retained-output boundary."
    }
    Assert-RuntimeFileIdentity -Path $SourceSnapshotPath `
        -ExpectedSha256 ([string]$retention.after_snapshot_sha256) `
        -Label "Money crop retained-output terminal boundary" | Out-Null

    $currentPath = [IO.Path]::GetFullPath($SourceSnapshotPath)
    $current = Import-RuntimeSnapshotFile -Path $currentPath `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
    if ([string]$current.Value.state_hash -ne
            [string]$retention.after_state_hash -or
        [int](Read-StateValue $current.Value "time" "total_days") -ne
            $TotalDay) {
        throw "Money crop terminal boundary state or day drifted."
    }

    $qualifiedItemId = [string]$retention.harvest_item_qualified_id
    $minimumUnits = [int]$retention.minimum_units
    $inventoryRows = @(Read-StateValue $current.Value "player" "inventory")
    $item = @($inventoryRows | Where-Object {
        [string]$_.qualified_item_id -eq $qualifiedItemId -and
        [int]$_.stack -ge $minimumUnits -and
        [int]$_.sell_to_store_price -gt 0 -and
        [bool]$_.can_be_shipped
    } | Sort-Object slot_index)
    if ($item.Count -ne 1) {
        throw "Money crop terminal requires one exact shippable retained-output stack."
    }
    $slotIndex = [int]$item[0].slot_index
    $unitPrice = [int]$item[0].sell_to_store_price

    $shippingBins = @(Read-StateValue $current.Value "farm" "shipping_bins")
    $bin = @($shippingBins | Where-Object {
        [bool]$_.completed -and
        [bool]$_.pending_settlement_value_complete -and
        [long]$_.pending_settlement_value -eq 0 -and
        $null -ne $_.interaction_stand_tile_x -and
        $null -ne $_.interaction_stand_tile_y -and
        [string]::IsNullOrWhiteSpace(
            [string]$_.interaction_stand_tile_blocked_reason)
    } | Sort-Object tile_y,tile_x)
    if ($bin.Count -ne 1) {
        throw "Money crop terminal requires one exact empty completed shipping bin."
    }
    $binTileX = [int]$bin[0].tile_x
    $binTileY = [int]$bin[0].tile_y
    $standTileX = [int]$bin[0].interaction_stand_tile_x
    $standTileY = [int]$bin[0].interaction_stand_tile_y
    $candidateRoot = "ship:Farm:$binTileX,$binTileY" +
        ":$slotIndex`:$qualifiedItemId"
    $continuationParameters = @(
        [ordered]@{ name = "continuation.option_id"; value =
            "executor.ship_inventory_item_to_bin" },
        [ordered]@{ name = "continuation.target_location"; value =
            "Farm" },
        [ordered]@{ name = "continuation.qualified_item_id"; value =
            $qualifiedItemId },
        [ordered]@{ name = "continuation.slot_index"; value =
            [string]$slotIndex },
        [ordered]@{ name = "continuation.quantity"; value = "1" },
        [ordered]@{ name = "continuation.expected_unit_price"; value =
            [string]$unitPrice }
    )

    $routeTransitions = [Collections.Generic.List[object]]::new()
    $playerX = [int](Read-StateValue $current.Value "player" "tile_x")
    $playerY = [int](Read-StateValue $current.Value "player" "tile_y")
    if ($playerX -ne $standTileX -or $playerY -ne $standTileY) {
        $moveParameters = @(
            [ordered]@{ name = "target_tile_x"; value =
                [string]$standTileX },
            [ordered]@{ name = "target_tile_y"; value =
                [string]$standTileY }
        ) + $continuationParameters
        $move = Invoke-RuntimeBoundSingleAction -Context $Context `
            -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile `
            -SourceSnapshotPath $currentPath `
            -OptionId "executor.move_to_tile" `
            -CandidateId "$candidateRoot`:approach" `
            -Parameters $moveParameters -Name "terminal-shipping-approach"
        $routeTransitions.Add((New-RuntimeTerminalQueueTransition `
            -TransitionId "terminal-shipping-approach" `
            -Artifacts $move -RunId $RunId))
        $currentPath = $move.AfterPath
    }

    $depositTransitions = [Collections.Generic.List[object]]::new()
    for ($index = 1; $index -le $minimumUnits; $index++) {
        $shipParameters = @(
            [ordered]@{ name = "target_location"; value = "Farm" },
            [ordered]@{ name = "target_tile_x"; value =
                [string]$binTileX },
            [ordered]@{ name = "target_tile_y"; value =
                [string]$binTileY },
            [ordered]@{ name = "stand_tile_x"; value =
                [string]$standTileX },
            [ordered]@{ name = "stand_tile_y"; value =
                [string]$standTileY },
            [ordered]@{ name = "slot_index"; value =
                [string]$slotIndex },
            [ordered]@{ name = "qualified_item_id"; value =
                $qualifiedItemId },
            [ordered]@{ name = "quantity"; value = "1" },
            [ordered]@{ name = "expected_unit_price"; value =
                [string]$unitPrice }
        ) + $continuationParameters
        $ship = Invoke-RuntimeBoundSingleAction -Context $Context `
            -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile `
            -SourceSnapshotPath $currentPath `
            -OptionId "executor.ship_inventory_item_to_bin" `
            -CandidateId "$candidateRoot`:deposit:$index" `
            -Parameters $shipParameters `
            -Name ("terminal-shipping-deposit-{0:D2}" -f $index)
        $depositTransitions.Add((New-RuntimeTerminalQueueTransition `
            -TransitionId ("terminal-shipping-deposit-{0:D2}" -f $index) `
            -Artifacts $ship -RunId $RunId))
        $currentPath = $ship.AfterPath
    }

    $homeSnapshot = Import-RuntimeSnapshotFile -Path $currentPath `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile
    $homeLocation = Get-RuntimeCropHomeLocation -Snapshot $homeSnapshot.Value
    $homeRoute = Invoke-RuntimeCropRoute -Context $Context `
        -BackendUrl $BackendUrl -SnapshotProfile $SnapshotProfile `
        -SourceSnapshotPath $currentPath -TargetLocation $homeLocation `
        -TotalDay $TotalDay -Name "terminal-settlement-home-ingress"
    $settlementRouteTransitions = [Collections.Generic.List[object]]::new()
    $routeIndex = 0
    foreach ($artifact in @($homeRoute.Artifacts)) {
        $routeIndex++
        $settlementRouteTransitions.Add((
            New-RuntimeTerminalQueueTransition `
                -TransitionId ("terminal-settlement-home-ingress-{0:D2}" -f
                    $routeIndex) -Artifacts $artifact -RunId $RunId))
    }
    if ($settlementRouteTransitions.Count -eq 0) {
        throw "Money crop terminal settlement did not emit a home ingress route."
    }
    $currentPath = $homeRoute.SourcePath

    $sleep = Invoke-RuntimeDailyPlanStep -Context $Context `
        -Name "terminal-native-shipping-settlement" `
        -OptionId "recovery.stabilize_day" `
        -CandidateKind "recovery_sleep_immediately" `
        -CandidateId "recovery:native_shipping_settlement" `
        -CandidateParameters @(
            "control_plane.native_save_boundary=true") `
        -SourceSnapshotPath $currentPath
    $sleepItems = @($sleep.Queue.items)
    if ($sleepItems.Count -ne 1 -or
        [string]$sleepItems[0].option_id -ne "executor.sleep") {
        throw "Money crop shipping settlement did not compile one native sleep."
    }

    $safeId = Get-MoneyCropSafeArtifactName -Value (
        [string]$retention.evidence_request_id)
    $recurrenceManifestPath = Join-Path $EvidenceDirectory `
        "terminal-recurrence-manifest-$safeId.json"
    $recurrenceReceiptPath = Join-Path $EvidenceDirectory `
        "terminal-recurrence-receipt-$safeId.json"
    Write-JsonFile -Path $recurrenceManifestPath -Value ([ordered]@{
        schema_version = "money_revenue_recurrence_proof_manifest.v1"
        transitions = @([ordered]@{
            transition_id = "crop-shipping-day-$TotalDay-settlement"
            proof_kind = "native_shipping_settlement"
            shop_sale_episode_path = ""
            queue_path = $sleep.QueuePath
            before_snapshot_path = $sleep.BeforePath
            execution_receipt_path = $sleep.ExecutionPath
            after_snapshot_path = $sleep.AfterPath
            run_id = $RunId
            executor_version = "runtime_test_harness_executor.v1"
            selected_candidate_id =
                [string]$sleep.Execution.selected_candidate_id
        })
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-recurrence-proof",
        "--manifest", $recurrenceManifestPath,
        "--output", $recurrenceReceiptPath)

    $terminalRequestPath = Join-Path $EvidenceDirectory `
        "terminal-request-$safeId.json"
    $terminalProofPath = Join-Path $EvidenceDirectory `
        "terminal-proof-$safeId.json"
    Write-JsonFile -Path $terminalRequestPath -Value ([ordered]@{
        schema_version =
            "money_revenue_crop_terminal_settlement_proof_request.v3"
        crop_production_plan_path = $ProductionPlanPath
        output_retention_proof_path = $OutputRetentionProofPath
        route_transitions = @($routeTransitions)
        shop_sale_episode_path = ""
        shipping_deposit_transitions = @($depositTransitions)
        settlement_route_transitions = @($settlementRouteTransitions)
        revenue_recurrence_manifest_path = $recurrenceManifestPath
        revenue_recurrence_receipt_path = $recurrenceReceiptPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-crop-terminal-settlement-proof",
        "--request", $terminalRequestPath,
        "--output", $terminalProofPath)

    return [pscustomobject]@{
        TerminalProofPath = [IO.Path]::GetFullPath($terminalProofPath)
        SourcePath = [IO.Path]::GetFullPath($sleep.AfterPath)
        ShippingDepositCount = $depositTransitions.Count
        SettlementRouteTransitionCount =
            $settlementRouteTransitions.Count
        SettledUnitPrice = $unitPrice
    }
}
