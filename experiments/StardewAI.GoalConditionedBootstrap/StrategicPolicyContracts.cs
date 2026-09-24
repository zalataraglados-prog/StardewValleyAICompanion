using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class StrategicPolicyVersionPins
{
    public const string DecisionSchema = "strategic_decision.v1";
    public const string ReplanSchema = "strategic_replan_context.v1";
}

public static class StrategicSelectionAuthorities
{
    public const string DeterministicUniqueStrictPareto =
        "deterministic_unique_strict_pareto";
    public const string LearnedIncomparableFrontierPreference =
        "learned_incomparable_frontier_preference";
}

public static class StrategicReplanTriggers
{
    public const string ExplicitRequest = "explicit_request";
    public const string DayStart = "day_start";
    public const string GoalChanged = "goal_changed";
    public const string ProfileChanged = "profile_changed";
    public const string PreferenceChanged = "preference_changed";
    public const string MaterialAvailabilityDrift =
        "material_availability_drift";
    public const string CurrencyAvailabilityDrift =
        "currency_availability_drift";
    public const string ReservationLedgerDrift =
        "reservation_ledger_drift";
    public const string SelectedMethodCompleted =
        "selected_method_completed";
    public const string ExecutionFailed = "execution_failed";
    public const string PlayerInterrupted = "player_interrupted";

    public static readonly string[] All =
    {
        ExplicitRequest,
        DayStart,
        GoalChanged,
        ProfileChanged,
        PreferenceChanged,
        MaterialAvailabilityDrift,
        CurrencyAvailabilityDrift,
        ReservationLedgerDrift,
        SelectedMethodCompleted,
        ExecutionFailed,
        PlayerInterrupted
    };
}

public sealed class StrategicReplanContext
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        StrategicPolicyVersionPins.ReplanSchema;

    [JsonPropertyName("trigger_kinds")]
    public string[] TriggerKinds { get; set; } =
        new[] { StrategicReplanTriggers.ExplicitRequest };

    [JsonPropertyName("trigger_token")]
    public string TriggerToken { get; set; } = string.Empty;

    [JsonPropertyName("previous_replan_fingerprint")]
    public string PreviousReplanFingerprint { get; set; } = string.Empty;
}

public sealed class StrategicPolicySelectionRequest
{
    public AcquisitionRoutePortfolioInputs CurrentInputs { get; init; } =
        new();

    public string PreferenceRequestPath { get; init; } = string.Empty;

    public string PriorRolloutProofManifestPath { get; init; } = string.Empty;

    public string CheckpointPath { get; init; } = string.Empty;

    public string CorpusManifestPath { get; init; } = string.Empty;

    public bool EnableDeterministicShadowAudit { get; init; }

    public StrategicReplanContext Replan { get; init; } = new();
}

public sealed class StrategicDecision
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        StrategicPolicyVersionPins.DecisionSchema;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("transition_index")]
    public int TransitionIndex { get; set; }

    [JsonPropertyName("candidate_denominator_count")]
    public long CandidateDenominatorCount { get; set; }

    [JsonPropertyName("admitted_candidate_count")]
    public int AdmittedCandidateCount { get; set; }

    [JsonPropertyName("pareto_frontier_count")]
    public int ParetoFrontierCount { get; set; }

    [JsonPropertyName("candidate_denominator_sha256")]
    public string CandidateDenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("pareto_frontier_sha256")]
    public string ParetoFrontierSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference_sha256")]
    public string TeacherPreferenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("deterministic_preference_status")]
    public string DeterministicPreferenceStatus { get; set; } = string.Empty;

    [JsonPropertyName("deterministic_selected_method_id")]
    public string DeterministicSelectedMethodId { get; set; } = string.Empty;

    [JsonPropertyName("selection_authority")]
    public string SelectionAuthority { get; set; } = string.Empty;

    [JsonPropertyName("selected_method_id")]
    public string SelectedMethodId { get; set; } = string.Empty;

    [JsonPropertyName("selected_method_sha256")]
    public string SelectedMethodSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_proposal")]
    public AcquisitionRoutePortfolioProposal? SelectedProposal { get; set; }

    [JsonPropertyName("selected_admission")]
    public AcquisitionRoutePortfolioAdmission? SelectedAdmission { get; set; }

    [JsonPropertyName("model_invoked")]
    public bool ModelInvoked { get; set; }

    [JsonPropertyName("model_checkpoint_id")]
    public string ModelCheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("model_checkpoint_sha256")]
    public string ModelCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("model_corpus_manifest_sha256")]
    public string ModelCorpusManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("model_candidate_scores")]
    public GoalMethodPairwiseCandidateScore[] ModelCandidateScores
    { get; set; } = Array.Empty<GoalMethodPairwiseCandidateScore>();

    [JsonPropertyName("shadow_audit_attempted")]
    public bool ShadowAuditAttempted { get; set; }

    [JsonPropertyName("shadow_audit_succeeded")]
    public bool ShadowAuditSucceeded { get; set; }

    [JsonPropertyName("runtime_selection_authorized")]
    public bool RuntimeSelectionAuthorized { get; set; }

    [JsonPropertyName("runtime_model_authority_authorized")]
    public bool RuntimeModelAuthorityAuthorized { get; set; }

    [JsonPropertyName("portfolio_commit_authorized")]
    public bool PortfolioCommitAuthorized { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("replan_required")]
    public bool ReplanRequired { get; set; }

    [JsonPropertyName("replan_deduplicated")]
    public bool ReplanDeduplicated { get; set; }

    [JsonPropertyName("replan_trigger_kinds")]
    public string[] ReplanTriggerKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("replan_fingerprint")]
    public string ReplanFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("decision_latency_ms")]
    public long DecisionLatencyMilliseconds { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("fallback_reasons")]
    public string[] FallbackReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("decision_policy")]
    public string DecisionPolicy { get; set; } =
        "StrategicPolicy reuses the authoritative candidate denominator, admission, reservation ledger and Pareto artifacts. A unique strict-Pareto member is selected without invoking a model. A learned model may rank only admitted non-dominated members of an incomparable frontier, remains read-only until a separate runtime promotion gate, and never authorizes portfolio commit, compilation, execution or formal product training.";
}
