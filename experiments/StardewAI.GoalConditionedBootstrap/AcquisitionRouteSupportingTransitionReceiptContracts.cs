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

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A supporting transition receipt verifies one hash-bound nonterminal queue item against fresh same-save snapshots. Crop planting requires exactly one seed consumed and one live target crop whose source seed and projected harvest item match the authoritative route lineage. It never emits a terminal acquisition receipt or formal training authorization. Success requires a complete fresh-snapshot replan before any later action.";
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
