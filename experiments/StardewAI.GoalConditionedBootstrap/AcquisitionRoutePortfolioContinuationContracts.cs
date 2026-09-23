using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioInitialCheckpointProof
{
    [JsonPropertyName("execution_inputs")]
    public AcquisitionRouteExecutionBindingInputs ExecutionInputs
    { get; init; } = new();

    [JsonPropertyName("execution_binding_path")]
    public string ExecutionBindingPath { get; init; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; init; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; init; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_path")]
    public string FreshTerminalReceiptPath { get; init; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; init; } = string.Empty;

    [JsonPropertyName("settlement_request_path")]
    public string SettlementRequestPath { get; init; } = string.Empty;

    [JsonPropertyName("settlement_result_path")]
    public string SettlementResultPath { get; init; } = string.Empty;

    [JsonPropertyName("settled_ledger_path")]
    public string SettledLedgerPath { get; init; } = string.Empty;

    [JsonPropertyName("settlement_receipt_path")]
    public string SettlementReceiptPath { get; init; } = string.Empty;
}

public sealed class AcquisitionRoutePortfolioContinuationTeacherRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_continuation_teacher_request.v1";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_checkpoint_sha256")]
    public string PriorCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("root_preference_request_sha256")]
    public string RootPreferenceRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("scoped_progress")]
    public AcquisitionRoutePortfolioScopeProgress[] ScopedProgress
    { get; set; } = Array.Empty<AcquisitionRoutePortfolioScopeProgress>();

    [JsonPropertyName("completed_route_occurrence_ids")]
    public string[] CompletedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("request_policy")]
    public string RequestPolicy { get; set; } =
        "A continuation request is derived only by exactly rebuilding the prior rollout checkpoint. It binds the checkpoint hash, cumulative completed alternatives, latest state and settled ledger. Completed alternatives are excluded inside the Teacher denominator; callers cannot supply or alter them. A completed checkpoint emits no continuation request, and this artifact never authorizes formal training.";
}

public sealed record AcquisitionRoutePortfolioCompletedAlternatives(
    [property: JsonPropertyName("requirement_set_id")]
    string RequirementSetId,
    [property: JsonPropertyName("requirement_id")]
    string RequirementId,
    [property: JsonPropertyName("alternative_indices")]
    int[] AlternativeIndices);
