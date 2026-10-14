using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueTerminalExecutionProofManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_execution_proof_manifest.v2";

    [JsonPropertyName("counterfactual_request_manifest_path")]
    public string CounterfactualRequestManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_path")]
    public string CounterfactualRequestCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("alternatives")]
    public MoneyRevenueTerminalAlternativeProof[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueTerminalAlternativeProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueTerminalAlternativeProof
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("before_strategy_ledger_capture_path")]
    public string BeforeStrategyLedgerCapturePath { get; set; } = string.Empty;

    [JsonPropertyName("after_strategy_ledger_capture_path")]
    public string AfterStrategyLedgerCapturePath { get; set; } = string.Empty;

    [JsonPropertyName("route_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[] RouteTransitions
    { get; set; } =
        Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();

    [JsonPropertyName("shop_sale_episode_path")]
    public string ShopSaleEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("shipping_deposit_transition")]
    public MoneyRevenueTerminalQueueTransitionProof?
        ShippingDepositTransition
    { get; set; }

    [JsonPropertyName("revenue_recurrence_manifest_path")]
    public string RevenueRecurrenceManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_path")]
    public string RevenueRecurrenceReceiptPath { get; set; } = string.Empty;
}

public sealed class MoneyRevenueTerminalQueueTransitionProof
{
    [JsonPropertyName("transition_id")]
    public string TransitionId { get; set; } = string.Empty;

    [JsonPropertyName("queue_path")]
    public string QueuePath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;
}

public sealed record MoneyRevenueTerminalQueueTransitionEvidence(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("before_snapshot_sha256")]
        string BeforeSnapshotSha256,
    [property: JsonPropertyName("execution_receipt_sha256")]
        string ExecutionReceiptSha256,
    [property: JsonPropertyName("after_snapshot_sha256")]
        string AfterSnapshotSha256,
    [property: JsonPropertyName("source_state_hash")]
        string SourceStateHash,
    [property: JsonPropertyName("after_state_hash")]
        string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick,
    [property: JsonPropertyName("option_ids")] string[] OptionIds);

public sealed record MoneyRevenueTerminalExecutionEvidence(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("concrete_alternative_id")]
        string ConcreteAlternativeId,
    [property: JsonPropertyName("terminal_transition_kind")]
        string TerminalTransitionKind,
    [property: JsonPropertyName("terminal_start_state_hash")]
        string TerminalStartStateHash,
    [property: JsonPropertyName("terminal_action_before_state_hash")]
        string TerminalActionBeforeStateHash,
    [property: JsonPropertyName("terminal_action_after_state_hash")]
        string TerminalActionAfterStateHash,
    [property: JsonPropertyName("elapsed_update_ticks")]
        long ElapsedUpdateTicks,
    [property: JsonPropertyName("elapsed_game_minutes")]
        int ElapsedGameMinutes,
    [property: JsonPropertyName("terminal_opportunity_cost")]
        AcquisitionOpportunityCostVector TerminalOpportunityCost,
    [property: JsonPropertyName("target_inventory_node_id")]
        string TargetInventoryNodeId,
    [property: JsonPropertyName("target_inventory_slot_index")]
        int TargetInventorySlotIndex,
    [property: JsonPropertyName("target_inventory_stack")]
        int TargetInventoryStack,
    [property: JsonPropertyName("target_reserved_quantity")]
        int TargetReservedQuantity,
    [property: JsonPropertyName("target_available_quantity")]
        int TargetAvailableQuantity,
    [property: JsonPropertyName("strategy_ledger_execution_run_id")]
        string StrategyLedgerExecutionRunId,
    [property: JsonPropertyName("before_strategy_ledger_capture_id")]
        string BeforeStrategyLedgerCaptureId,
    [property: JsonPropertyName("before_strategy_ledger_capture_sha256")]
        string BeforeStrategyLedgerCaptureSha256,
    [property: JsonPropertyName("after_strategy_ledger_capture_id")]
        string AfterStrategyLedgerCaptureId,
    [property: JsonPropertyName("after_strategy_ledger_capture_sha256")]
        string AfterStrategyLedgerCaptureSha256,
    [property: JsonPropertyName("before_strategy_ledger_sha256")]
        string BeforeStrategyLedgerSha256,
    [property: JsonPropertyName("after_strategy_ledger_sha256")]
        string AfterStrategyLedgerSha256,
    [property: JsonPropertyName("strategy_ledger_id")]
        string StrategyLedgerId,
    [property: JsonPropertyName("strategy_ledger_revision")]
        int StrategyLedgerRevision,
    [property: JsonPropertyName("route_transitions")]
        MoneyRevenueTerminalQueueTransitionEvidence[] RouteTransitions,
    [property: JsonPropertyName("native_terminal_artifacts")]
        MoneyRevenueArtifactBinding[] NativeTerminalArtifacts,
    [property: JsonPropertyName("revenue_recurrence_manifest_sha256")]
        string RevenueRecurrenceManifestSha256,
    [property: JsonPropertyName("revenue_recurrence_receipt_sha256")]
        string RevenueRecurrenceReceiptSha256,
    [property: JsonPropertyName("reservation_safety_verified")]
        bool ReservationSafetyVerified,
    [property: JsonPropertyName("strategy_ledger_boundary_artifacts_verified")]
        bool StrategyLedgerBoundaryArtifactsVerified,
    [property: JsonPropertyName("resource_neutral_route_verified")]
        bool ResourceNeutralRouteVerified,
    [property: JsonPropertyName("native_terminal_transition_verified")]
        bool NativeTerminalTransitionVerified,
    [property: JsonPropertyName("revenue_recurrence_verified")]
        bool RevenueRecurrenceVerified,
    [property: JsonPropertyName("evidence_sha256")] string EvidenceSha256);

public sealed class MoneyRevenueTerminalExecutionProofReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_execution_proof_receipt.v2";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_sha256")]
    public string CounterfactualRequestCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }

    [JsonPropertyName("verified_alternative_count")]
    public int VerifiedAlternativeCount { get; set; }

    [JsonPropertyName("terminal_opportunity_cost_proofs_verified")]
    public bool TerminalOpportunityCostProofsVerified { get; set; }

    [JsonPropertyName("terminal_reservation_claim_proofs_verified")]
    public bool TerminalReservationClaimProofsVerified { get; set; }

    [JsonPropertyName("strategy_ledger_boundary_artifacts_verified")]
    public bool StrategyLedgerBoundaryArtifactsVerified { get; set; }

    [JsonPropertyName("native_revenue_recurrences_verified")]
    public bool NativeRevenueRecurrencesVerified { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("alternatives")]
    public MoneyRevenueTerminalExecutionEvidence[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueTerminalExecutionEvidence>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every authoritative Revenue request must bind one resource-neutral terminal route, " +
        "one native sale or shipping-deposit transition, two independently identified strategy-ledger " +
        "boundary captures, an unchanged external strategy ledger, " +
        "an exact unreserved target stack and a replayable Revenue recurrence. Actual receipt " +
        "ticks are converted by the shared game-clock policy. This receipt still cannot select " +
        "a production Teacher preference or authorize formal product training.";
}
