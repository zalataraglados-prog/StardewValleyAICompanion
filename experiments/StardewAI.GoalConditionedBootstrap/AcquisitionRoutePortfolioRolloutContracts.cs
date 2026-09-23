using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioRolloutCheckpoint
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_rollout_checkpoint.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("root_preference_request_sha256")]
    public string RootPreferenceRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("current_teacher_preference_sha256")]
    public string CurrentTeacherPreferenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("current_proposal_id")]
    public string CurrentProposalId { get; set; } = string.Empty;

    [JsonPropertyName("current_portfolio_id")]
    public string CurrentPortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("latest_settlement_receipt_sha256")]
    public string LatestSettlementReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("latest_state_hash")]
    public string LatestStateHash { get; set; } = string.Empty;

    [JsonPropertyName("latest_ledger_revision")]
    public int LatestLedgerRevision { get; set; }

    [JsonPropertyName("latest_ledger_sha256")]
    public string LatestLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("prior_checkpoint_sha256")]
    public string PriorCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("scoped_progress")]
    public AcquisitionRoutePortfolioScopeProgress[] ScopedProgress
    { get; set; } = Array.Empty<AcquisitionRoutePortfolioScopeProgress>();

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("completed_route_occurrence_ids")]
    public string[] CompletedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("pending_selected_route_occurrence_ids")]
    public string[] PendingSelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("checkpoint_verified")]
    public bool CheckpointVerified { get; set; }

    [JsonPropertyName("portfolio_completion_verified")]
    public bool PortfolioCompletionVerified { get; set; }

    [JsonPropertyName("fresh_replan_required")]
    public bool FreshReplanRequired { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A rollout checkpoint rebuilds the current Teacher selection and exact completed-route settlement, maps the verified route back to one authoritative requirement alternative, and reports cumulative progress by selection rule. A continuation checkpoint also rebuilds its prior checkpoint and binds that artifact hash. Whole-portfolio completion requires every scoped rule satisfied, every selected route completed, and no rollout-owned active reservation left behind. Otherwise the old route list is stale and a fresh continuation Teacher replan is mandatory. This checkpoint carries no learner score and never authorizes formal training.";
}

public sealed record AcquisitionRoutePortfolioScopeProgress(
    [property: JsonPropertyName("requirement_set_id")]
    string RequirementSetId,
    [property: JsonPropertyName("requirement_id")]
    string RequirementId,
    [property: JsonPropertyName("selection_rule")]
    string SelectionRule,
    [property: JsonPropertyName("required_alternative_count")]
    int RequiredAlternativeCount,
    [property: JsonPropertyName("alternative_count")]
    int AlternativeCount,
    [property: JsonPropertyName("completed_alternative_indices")]
    int[] CompletedAlternativeIndices,
    [property: JsonPropertyName("remaining_required_slots")]
    int RemainingRequiredSlots,
    [property: JsonPropertyName("scope_complete")]
    bool ScopeComplete);
