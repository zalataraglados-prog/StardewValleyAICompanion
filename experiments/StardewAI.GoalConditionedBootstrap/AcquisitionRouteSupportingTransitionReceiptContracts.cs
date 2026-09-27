using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_supporting_transition_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_id")]
    public string SourceCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("compilation_sha256")]
    public string CompilationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("queue_execution_verified")]
    public bool QueueExecutionVerified { get; set; }

    [JsonPropertyName("supporting_transition_verified")]
    public bool SupportingTransitionVerified { get; set; }

    [JsonPropertyName("fresh_replan_required")]
    public bool FreshReplanRequired { get; set; }

    [JsonPropertyName("terminal_receipt_eligible")]
    public bool TerminalReceiptEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("crop_planting_transition")]
    public AcquisitionCropPlantingTransitionEvidence?
        CropPlantingTransition { get; set; }

    [JsonPropertyName("machine_input_transition")]
    public AcquisitionMachineInputTransitionEvidence?
        MachineInputTransition { get; set; }

    [JsonPropertyName("machine_capacity_transition")]
    public AcquisitionMachineCapacityTransitionEvidence?
        MachineCapacityTransition { get; set; }

    [JsonPropertyName("material_transfer_transition")]
    public AcquisitionMaterialTransferTransitionEvidence?
        MaterialTransferTransition { get; set; }

    [JsonPropertyName("purchase_transition")]
    public AcquisitionPurchaseTransitionEvidence? PurchaseTransition
    { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A supporting transition receipt verifies one hash-bound nonterminal action, the exact existing move-plus-transfer macro, or the exact existing bounded-wait/purchase/menu-close macro against fresh same-save snapshots. Crop planting requires exactly one seed consumed and one live target crop whose source seed and projected harvest item match the authoritative route lineage. Machine loading requires every request-bound material consumption to match an exact inventory-node slot delta and the exact target machine to move from idle to a native processing or ready state whose last input, output and unique authoritative route source match the compiled lineage. Machine material staging requires the reserved chest stack to decrease and one projected player-inventory slot to increase by the exact full claim quantity; it records relocation, not consumption. Machine purchase route and interaction progress must leave both the bound currency and item count unchanged; a purchase must decrease the exact native currency by one bound unit price and increase the exact player-inventory qualified item by one bound output stack. Machine-capacity craft requires the exact inventory increase declared by the existing craft candidate; placement requires one exact inventory decrement and exactly one new idle machine at the bound location and tile. It never emits a terminal acquisition receipt or formal training authorization. Success requires a complete fresh-snapshot replan before any later action.";
}

public sealed class AcquisitionMachineCapacityTransitionEvidence
{
    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [JsonPropertyName("machine_qualified_item_id")]
    public string MachineQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("inventory_quantity_before")]
    public int? InventoryQuantityBefore { get; set; }

    [JsonPropertyName("inventory_quantity_after")]
    public int? InventoryQuantityAfter { get; set; }

    [JsonPropertyName("observed_inventory_delta")]
    public int? ObservedInventoryDelta { get; set; }

    [JsonPropertyName("before_target_machine_present")]
    public bool? BeforeTargetMachinePresent { get; set; }

    [JsonPropertyName("after_target_machine_present")]
    public bool? AfterTargetMachinePresent { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class AcquisitionPurchaseTransitionEvidence
{
    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("shop_id")]
    public string ShopId { get; set; } = string.Empty;

    [JsonPropertyName("stock_id")]
    public string StockId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("currency_id")]
    public int? CurrencyId { get; set; }

    [JsonPropertyName("expected_currency_decrease")]
    public int? ExpectedCurrencyDecrease { get; set; }

    [JsonPropertyName("currency_before")]
    public int? CurrencyBefore { get; set; }

    [JsonPropertyName("currency_after")]
    public int? CurrencyAfter { get; set; }

    [JsonPropertyName("observed_currency_decrease")]
    public int? ObservedCurrencyDecrease { get; set; }

    [JsonPropertyName("expected_item_increase")]
    public int? ExpectedItemIncrease { get; set; }

    [JsonPropertyName("item_quantity_before")]
    public int? ItemQuantityBefore { get; set; }

    [JsonPropertyName("item_quantity_after")]
    public int? ItemQuantityAfter { get; set; }

    [JsonPropertyName("observed_item_increase")]
    public int? ObservedItemIncrease { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class AcquisitionMaterialTransferTransitionEvidence
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_slot_index")]
    public int? SourceSlotIndex { get; set; }

    [JsonPropertyName("destination_node_id")]
    public string DestinationNodeId { get; set; } = string.Empty;

    [JsonPropertyName("destination_slot_index")]
    public int? DestinationSlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("source_quantity_before")]
    public int? SourceQuantityBefore { get; set; }

    [JsonPropertyName("source_quantity_after")]
    public int? SourceQuantityAfter { get; set; }

    [JsonPropertyName("destination_quantity_before")]
    public int? DestinationQuantityBefore { get; set; }

    [JsonPropertyName("destination_quantity_after")]
    public int? DestinationQuantityAfter { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class AcquisitionMachineInputTransitionEvidence
{
    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("machine_qualified_item_id")]
    public string MachineQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("input_qualified_item_id")]
    public string InputQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("output_qualified_item_id")]
    public string OutputQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("before_capacity_state")]
    public string BeforeCapacityState { get; set; } = string.Empty;

    [JsonPropertyName("after_capacity_state")]
    public string AfterCapacityState { get; set; } = string.Empty;

    [JsonPropertyName("after_minutes_until_ready")]
    public int? AfterMinutesUntilReady { get; set; }

    [JsonPropertyName("material_consumptions")]
    public AcquisitionSupportMaterialConsumptionEvidence[]
        MaterialConsumptions { get; set; } =
            Array.Empty<AcquisitionSupportMaterialConsumptionEvidence>();

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class AcquisitionSupportMaterialConsumptionEvidence
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("expected_consumed_quantity")]
    public int ExpectedConsumedQuantity { get; set; }

    [JsonPropertyName("before_quantity")]
    public int? BeforeQuantity { get; set; }

    [JsonPropertyName("after_quantity")]
    public int? AfterQuantity { get; set; }

    [JsonPropertyName("observed_consumed_quantity")]
    public int? ObservedConsumedQuantity { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class AcquisitionCropPlantingTransitionEvidence
{
    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("seed_id")]
    public string SeedId { get; set; } = string.Empty;

    [JsonPropertyName("harvest_item_qualified_id")]
    public string HarvestItemQualifiedId { get; set; } = string.Empty;

    [JsonPropertyName("before_seed_quantity")]
    public int? BeforeSeedQuantity { get; set; }

    [JsonPropertyName("after_seed_quantity")]
    public int? AfterSeedQuantity { get; set; }

    [JsonPropertyName("seed_quantity_decrease")]
    public int? SeedQuantityDecrease { get; set; }

    [JsonPropertyName("before_target_crop_present")]
    public bool? BeforeTargetCropPresent { get; set; }

    [JsonPropertyName("after_target_crop_present")]
    public bool? AfterTargetCropPresent { get; set; }

    [JsonPropertyName("after_crop_dead")]
    public bool? AfterCropDead { get; set; }

    [JsonPropertyName("after_crop_ready_for_harvest")]
    public bool? AfterCropReadyForHarvest { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}
