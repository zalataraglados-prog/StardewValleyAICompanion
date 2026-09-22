using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class GoalMethodPairwiseCandidateScore
{
    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("proposal_sha256")]
    public string ProposalSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("model_score")]
    public double ModelScore { get; set; }

    [JsonPropertyName("teacher_selected")]
    public bool TeacherSelected { get; set; }

    [JsonPropertyName("on_deterministic_pareto_frontier")]
    public bool OnDeterministicParetoFrontier { get; set; }
}

public sealed class GoalMethodPairwiseScoringResult
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_goal_method_scoring_result.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } =
        "ready_verified_goal_method_shadow_ranking";

    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("corpus_manifest_sha256")]
    public string CorpusManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("supervision_row_id")]
    public string SupervisionRowId { get; set; } = string.Empty;

    [JsonPropertyName("supervision_row_sha256")]
    public string SupervisionRowSha256 { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("transition_index")]
    public int TransitionIndex { get; set; }

    [JsonPropertyName("candidate_denominator_verified")]
    public bool CandidateDenominatorVerified { get; set; }

    [JsonPropertyName("candidate_scores")]
    public GoalMethodPairwiseCandidateScore[] CandidateScores { get; set; } =
        Array.Empty<GoalMethodPairwiseCandidateScore>();

    [JsonPropertyName("model_top_proposal_id")]
    public string ModelTopProposalId { get; set; } = string.Empty;

    [JsonPropertyName("teacher_selected_proposal_id")]
    public string TeacherSelectedProposalId { get; set; } = string.Empty;

    [JsonPropertyName("model_agrees_with_teacher")]
    public bool ModelAgreesWithTeacher { get; set; }

    [JsonPropertyName("selection_mode")]
    public string SelectionMode { get; set; } =
        "read_only_teacher_comparison";

    [JsonPropertyName("portfolio_commit_authorized")]
    public bool PortfolioCommitAuthorized { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("scoring_policy")]
    public string ScoringPolicy { get; set; } =
        "The scorer re-verifies the checkpoint-bound corpus and ranks only admitted candidates from one verified supervision row. The model top score is observational: deterministic candidate admission, Teacher evidence, portfolio commit, compilation and execution remain authoritative. This artifact cannot authorize portfolio commit or formal product training.";
}

public sealed class GoalMethodPairwiseLiveShadowSelection
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_goal_method_live_shadow_selection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("corpus_manifest_sha256")]
    public string CorpusManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("transition_index")]
    public int TransitionIndex { get; set; }

    [JsonPropertyName("teacher_preference_status")]
    public string TeacherPreferenceStatus { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference_sha256")]
    public string TeacherPreferenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_verified")]
    public bool CandidateDenominatorVerified { get; set; }

    [JsonPropertyName("candidate_scores")]
    public GoalMethodPairwiseCandidateScore[] CandidateScores { get; set; } =
        Array.Empty<GoalMethodPairwiseCandidateScore>();

    [JsonPropertyName("model_top_proposal_id")]
    public string ModelTopProposalId { get; set; } = string.Empty;

    [JsonPropertyName("teacher_selected_proposal_id")]
    public string TeacherSelectedProposalId { get; set; } = string.Empty;

    [JsonPropertyName("model_agrees_with_teacher")]
    public bool? ModelAgreesWithTeacher { get; set; }

    [JsonPropertyName("shadow_selected_proposal")]
    public AcquisitionRoutePortfolioProposal? ShadowSelectedProposal
    { get; set; }

    [JsonPropertyName("shadow_selected_admission")]
    public AcquisitionRoutePortfolioAdmission? ShadowSelectedAdmission
    { get; set; }

    [JsonPropertyName("selection_authority")]
    public string SelectionAuthority { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_commit_authorized")]
    public bool PortfolioCommitAuthorized { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("selection_policy")]
    public string SelectionPolicy { get; set; } =
        "The current snapshot, target-date evidence and strategy ledger rebuild the complete deterministic portfolio denominator. A unique strict-Pareto Teacher choice remains authoritative. Only an admitted incomparable Pareto frontier may receive a checkpoint-ranked shadow choice. This artifact never authorizes portfolio commit, dispatch or formal product training.";
}
