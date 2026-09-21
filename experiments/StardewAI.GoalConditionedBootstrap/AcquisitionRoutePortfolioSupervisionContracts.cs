using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioSupervisionSourceKinds
{
    public const string TeacherPreference = "teacher_preference";
    public const string NativeOutcome = "native_outcome";
    public const string StudentObservation = "student_observation";
}

public sealed class AcquisitionRoutePortfolioSupervisionDataset
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_supervision_dataset.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("proof_manifest_sha256")]
    public string ProofManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proof_receipt_sha256")]
    public string ProofReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_admission_receipt_sha256")]
    public string RolloutAdmissionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("teacher_preference_count")]
    public int TeacherPreferenceCount { get; set; }

    [JsonPropertyName("native_outcome_count")]
    public int NativeOutcomeCount { get; set; }

    [JsonPropertyName("student_observation_count")]
    public int StudentObservationCount { get; set; }

    [JsonPropertyName("row_chain_tip_sha256")]
    public string RowChainTipSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rows")]
    public AcquisitionRoutePortfolioSupervisionRow[] Rows { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioSupervisionRow>();

    [JsonPropertyName("teacher_training_evidence_eligible")]
    public bool TeacherTrainingEvidenceEligible { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("dataset_policy")]
    public string DatasetPolicy { get; set; } =
        "This adapter rebuilds an admitted terminal rollout proof before emitting one hash-linked row per verified transition. Teacher preference, native outcome and Student observation are separate typed channels. Unavailable portfolios are deferred without negative labels, and a Teacher-driven rollout never fabricates a Student observation. This scoped dataset is eligible Teacher evidence but does not authorize formal product training.";
}

public sealed class AcquisitionRoutePortfolioSupervisionRow
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_supervision_row.v1";

    [JsonPropertyName("row_id")]
    public string RowId { get; set; } = string.Empty;

    [JsonPropertyName("transition_index")]
    public int TransitionIndex { get; set; }

    [JsonPropertyName("prior_row_sha256")]
    public string PriorRowSha256 { get; set; } = string.Empty;

    [JsonPropertyName("row_sha256")]
    public string RowSha256 { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public AcquisitionRoutePortfolioSupervisionPayload Payload { get; set; } =
        new();
}

public sealed class AcquisitionRoutePortfolioSupervisionPayload
{
    [JsonPropertyName("decision_context")]
    public AcquisitionRoutePortfolioSupervisionDecisionContext DecisionContext
    { get; set; } = new();

    [JsonPropertyName("decision_state_hash")]
    public string DecisionStateHash { get; set; } = string.Empty;

    [JsonPropertyName("decision_snapshot_sha256")]
    public string DecisionSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_ledger_revision")]
    public int DecisionLedgerRevision { get; set; }

    [JsonPropertyName("decision_ledger_sha256")]
    public string DecisionLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference")]
    public AcquisitionRoutePortfolioTeacherSupervision TeacherPreference
    { get; set; } = new();

    [JsonPropertyName("native_outcome")]
    public AcquisitionRoutePortfolioNativeOutcomeSupervision NativeOutcome
    { get; set; } = new();

    [JsonPropertyName("student_observation")]
    public AcquisitionRoutePortfolioStudentObservationSupervision
        StudentObservation
    { get; set; } = new();
}

public sealed class AcquisitionRoutePortfolioSupervisionDecisionContext
{
    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("bridge_version")]
    public string BridgeVersion { get; set; } = string.Empty;

    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("total_day")]
    public int TotalDay { get; set; }

    [JsonPropertyName("game_tick")]
    public long GameTick { get; set; }

    [JsonPropertyName("split_key")]
    public string SplitKey { get; set; } = string.Empty;
}

public sealed class AcquisitionRoutePortfolioTeacherSupervision
{
    [JsonPropertyName("source_kind")]
    public string SourceKind { get; set; } =
        AcquisitionRoutePortfolioSupervisionSourceKinds.TeacherPreference;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("preference_request_sha256")]
    public string PreferenceRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference_sha256")]
    public string TeacherPreferenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selection_policy_id")]
    public string SelectionPolicyId { get; set; } = string.Empty;

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

    [JsonPropertyName("selected_proposal_id")]
    public string SelectedProposalId { get; set; } = string.Empty;

    [JsonPropertyName("selected_proposal_sha256")]
    public string SelectedProposalSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("selected_aggregate_cost_vector")]
    public AcquisitionOpportunityCostVector? SelectedAggregateCostVector
    { get; set; }

    [JsonPropertyName("pairwise_preferences")]
    public AcquisitionRoutePortfolioPairwisePreference[] PairwisePreferences
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioPairwisePreference>();

    [JsonPropertyName("unavailable_candidate_label_semantics")]
    public string UnavailableCandidateLabelSemantics { get; set; } =
        "defer_without_negative_label";

    [JsonPropertyName("uses_learner_rank_or_score")]
    public bool UsesLearnerRankOrScore { get; set; }

    [JsonPropertyName("emits_negative_labels_for_unavailable_portfolios")]
    public bool EmitsNegativeLabelsForUnavailablePortfolios { get; set; }
}

public sealed class AcquisitionRoutePortfolioNativeOutcomeSupervision
{
    [JsonPropertyName("source_kind")]
    public string SourceKind { get; set; } =
        AcquisitionRoutePortfolioSupervisionSourceKinds.NativeOutcome;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("before_game_tick")]
    public long BeforeGameTick { get; set; }

    [JsonPropertyName("after_game_tick")]
    public long AfterGameTick { get; set; }

    [JsonPropertyName("execution_binding_sha256")]
    public string ExecutionBindingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settlement_receipt_sha256")]
    public string SettlementReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_checkpoint_sha256")]
    public string RolloutCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("terminal_transition")]
    public AcquisitionTerminalTransitionEvidence TerminalTransition
    { get; set; } = new();

    [JsonPropertyName("queue_execution_verified")]
    public bool QueueExecutionVerified { get; set; }

    [JsonPropertyName("fresh_terminal_receipt_verified")]
    public bool FreshTerminalReceiptVerified { get; set; }

    [JsonPropertyName("reservation_lifecycle_verified")]
    public bool ReservationLifecycleVerified { get; set; }
}

public sealed class AcquisitionRoutePortfolioStudentObservationSupervision
{
    [JsonPropertyName("source_kind")]
    public string SourceKind { get; set; } =
        AcquisitionRoutePortfolioSupervisionSourceKinds.StudentObservation;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_observed_teacher_rollout";

    [JsonPropertyName("observed")]
    public bool Observed { get; set; }

    [JsonPropertyName("positive_preference_label_emitted")]
    public bool PositivePreferenceLabelEmitted { get; set; }

    [JsonPropertyName("missingness_semantics")]
    public string MissingnessSemantics { get; set; } =
        "student_did_not_select_or_execute_this_teacher_rollout";
}
