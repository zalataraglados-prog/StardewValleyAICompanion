using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSeedAcquisitionExecutionManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_acquisition_execution_manifest.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("transitions")]
    public MoneyRevenueCropNativeLifecycleTransitionRequest[] Transitions
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeLifecycleTransitionRequest>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropSeedAcquisitionProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_acquisition_proof_request.v2";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("execution_manifest_path")]
    public string ExecutionManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("prior_seed_acquisition_proof_path")]
    public string PriorSeedAcquisitionProofPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueCropSeedAcquisitionTransitionProof(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("queue_path")] string QueuePath,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")] string BeforeSnapshotSha256,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("execution_receipt_sha256")] string ExecutionReceiptSha256,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")] string AfterSnapshotSha256,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick,
    [property: JsonPropertyName("option_ids")] string[] OptionIds,
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("stock_id")] string StockId,
    [property: JsonPropertyName("output_stack_per_purchase")] int OutputStackPerPurchase,
    [property: JsonPropertyName("output_quality")] int OutputQuality,
    [property: JsonPropertyName("max_unit_price")] int MaxUnitPrice,
    [property: JsonPropertyName("expected_unit_price")] int? ExpectedUnitPrice,
    [property: JsonPropertyName("inventory_quantity_before")] int InventoryQuantityBefore,
    [property: JsonPropertyName("inventory_quantity_after")] int InventoryQuantityAfter,
    [property: JsonPropertyName("inventory_quantity_increase")] int InventoryQuantityIncrease,
    [property: JsonPropertyName("money_before")] int MoneyBefore,
    [property: JsonPropertyName("money_after")] int MoneyAfter,
    [property: JsonPropertyName("money_decrease")] int MoneyDecrease,
    [property: JsonPropertyName("verified")] bool Verified);

public sealed class MoneyRevenueCropSeedAcquisitionProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_acquisition_proof.v2";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("execution_manifest_path")]
    public string ExecutionManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_manifest_sha256")]
    public string ExecutionManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_seed_acquisition_proof_path")]
    public string PriorSeedAcquisitionProofPath { get; set; } = string.Empty;

    [JsonPropertyName("prior_seed_acquisition_proof_sha256")]
    public string PriorSeedAcquisitionProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_seed_acquisition_evidence_request_id")]
    public string PriorSeedAcquisitionEvidenceRequestId { get; set; } =
        string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_state_hash")]
    public string BeforeStateHash { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("initial_seed_quantity")]
    public int InitialSeedQuantity { get; set; }

    [JsonPropertyName("final_seed_quantity")]
    public int FinalSeedQuantity { get; set; }

    [JsonPropertyName("required_shortfall_quantity")]
    public int RequiredShortfallQuantity { get; set; }

    [JsonPropertyName("verified_quantity_increase")]
    public int VerifiedQuantityIncrease { get; set; }

    [JsonPropertyName("seed_supply_deadline_total_day")]
    public int SeedSupplyDeadlineTotalDay { get; set; }

    [JsonPropertyName("acquisition_total_day")]
    public int AcquisitionTotalDay { get; set; }

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("supporting_transition_count")]
    public int SupportingTransitionCount { get; set; }

    [JsonPropertyName("purchase_slice_count")]
    public int PurchaseSliceCount { get; set; }

    [JsonPropertyName("transitions")]
    public MoneyRevenueCropSeedAcquisitionTransitionProof[] Transitions
        { get; set; } =
            Array.Empty<MoneyRevenueCropSeedAcquisitionTransitionProof>();

    [JsonPropertyName("seed_acquisition_verified")]
    public bool SeedAcquisitionVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "The proof rebuilds the exact crop obligation, then verifies a continuous " +
        "native economy.buy_supplies execution chain. Every route or shop stage must " +
        "preserve resources, every purchase must remain bound to one qualified seed, " +
        "shop, stock row, output stack, price ceiling and quantity-one slice, and the " +
        "observed inventory and money deltas must match. The accumulated seed increase " +
        "must equal the planned shortfall before its deadline. No Teacher label, " +
        "production cell or formal training authorization is created.";
}
