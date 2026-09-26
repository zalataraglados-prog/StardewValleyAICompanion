using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioTeacherPreferenceRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_teacher_preference_request.v1";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("scoped_requirements")]
    public AcquisitionRoutePortfolioRequirementScope[] ScopedRequirements
    {
        get;
        set;
    } = Array.Empty<AcquisitionRoutePortfolioRequirementScope>();
}

public sealed class AcquisitionRoutePortfolioTeacherPreference
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_teacher_preference.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("preference_request_sha256")]
    public string PreferenceRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("opportunity_cost_sha256")]
    public string OpportunityCostSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_supporting_transition_replan_sha256")]
    public string PriorSupportingTransitionReplanSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("selection_policy_id")]
    public string SelectionPolicyId { get; set; } =
        "complete_portfolio_denominator_unique_strict_pareto.v1";

    [JsonPropertyName("candidate_limit")]
    public int CandidateLimit { get; set; }

    [JsonPropertyName("candidate_denominator_count")]
    public long CandidateDenominatorCount { get; set; }

    [JsonPropertyName("candidate_denominator_complete")]
    public bool CandidateDenominatorComplete { get; set; }

    [JsonPropertyName("admitted_candidate_count")]
    public int AdmittedCandidateCount { get; set; }

    [JsonPropertyName("pareto_frontier_count")]
    public int ParetoFrontierCount { get; set; }

    [JsonPropertyName("candidate_evaluations")]
    public AcquisitionRoutePortfolioTeacherCandidateEvaluation[]
        CandidateEvaluations
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioTeacherCandidateEvaluation>();

    [JsonPropertyName("selected_proposal")]
    public AcquisitionRoutePortfolioProposal? SelectedProposal { get; set; }

    [JsonPropertyName("selected_admission")]
    public AcquisitionRoutePortfolioAdmission? SelectedAdmission { get; set; }

    [JsonPropertyName("pairwise_preferences")]
    public AcquisitionRoutePortfolioPairwisePreference[] PairwisePreferences
    {
        get;
        set;
    } = Array.Empty<AcquisitionRoutePortfolioPairwisePreference>();

    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("uses_learner_rank_or_score")]
    public bool UsesLearnerRankOrScore { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_portfolios")]
    public bool EmitsNegativeLabelsForUnavailablePortfolios { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The Teacher derives the complete bounded portfolio denominator from authoritative requirement selection rules and all target-date Pareto route occurrences. It evaluates every generated proposal through the existing atomic admission preflight. A preference label exists only when one admitted aggregate cost vector strictly Pareto-dominates every other admitted portfolio. Incomparable or equal frontier portfolios remain blocked; learner scores, caller candidate lists, scalarized currencies and unavailable portfolios never create preference labels. The selected proposal still requires atomic commit, ordered execution and portfolio completion evidence, so formal training authorization remains false.";
}

public sealed record AcquisitionRoutePortfolioTeacherCandidateEvaluation(
    [property: JsonPropertyName("proposal_id")] string ProposalId,
    [property: JsonPropertyName("proposal_sha256")] string ProposalSha256,
    [property: JsonPropertyName("selected_route_occurrence_ids")]
    string[] SelectedRouteOccurrenceIds,
    [property: JsonPropertyName("admission_ready")] bool AdmissionReady,
    [property: JsonPropertyName("aggregate_cost_vector")]
    AcquisitionOpportunityCostVector? AggregateCostVector,
    [property: JsonPropertyName("dominated_by_proposal_ids")]
    string[] DominatedByProposalIds,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionRoutePortfolioPairwisePreference(
    [property: JsonPropertyName("preferred_proposal_id")]
    string PreferredProposalId,
    [property: JsonPropertyName("alternative_proposal_id")]
    string AlternativeProposalId,
    [property: JsonPropertyName("criterion")]
    string Criterion,
    [property: JsonPropertyName("label_semantics")]
    string LabelSemantics);
