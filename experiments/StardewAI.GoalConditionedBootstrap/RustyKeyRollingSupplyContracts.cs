using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class RustyKeyRollingSupplyProposal
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "rusty_key_rolling_supply_proposal.v1";
    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";
    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;
    [JsonPropertyName("source_save_id")]
    public string SourceSaveId { get; set; } = string.Empty;
    [JsonPropertyName("source_player_id")]
    public string SourcePlayerId { get; set; } = string.Empty;
    [JsonPropertyName("source_total_day")]
    public int SourceTotalDay { get; set; }
    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;
    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;
    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }
    [JsonPropertyName("donated_count")]
    public int DonatedCount { get; set; }
    [JsonPropertyName("donation_threshold")]
    public int DonationThreshold { get; set; }
    [JsonPropertyName("remaining_donation_count")]
    public int RemainingDonationCount { get; set; }
    [JsonPropertyName("complete_museum_item_denominator")]
    public RustyKeyAcquisitionItem[] CompleteMuseumItemDenominator { get; set; } = Array.Empty<RustyKeyAcquisitionItem>();
    [JsonPropertyName("candidate_evaluations")]
    public RustyKeyRollingCandidateEvaluation[] CandidateEvaluations { get; set; } = Array.Empty<RustyKeyRollingCandidateEvaluation>();
    [JsonPropertyName("pending_donation_requirement_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? PendingDonationRequirementIds { get; set; }
    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;
    [JsonPropertyName("compiled_plan")]
    public SmallModelPlanEnvelope? CompiledPlan { get; set; }
    [JsonPropertyName("compiled_queue")]
    public ActionQueueEnvelope? CompiledQueue { get; set; }
    [JsonPropertyName("selected_custody_transfer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyRollingCustodySelection? SelectedCustodyTransfer { get; set; }
    [JsonPropertyName("selected_acquisition_proposal")]
    public AcquisitionRoutePortfolioProposal? SelectedAcquisitionProposal { get; set; }
    [JsonPropertyName("selected_acquisition_admission")]
    public AcquisitionRoutePortfolioAdmission? SelectedAcquisitionAdmission { get; set; }
    [JsonPropertyName("rolling_step_ready")]
    public bool RollingStepReady { get; set; }
    [JsonPropertyName("threshold_satisfied")]
    public bool ThresholdSatisfied { get; set; }
    [JsonPropertyName("full_threshold_supply_verified")]
    public bool FullThresholdSupplyVerified => false;
    [JsonPropertyName("donation_output_custody_verified")]
    public bool DonationOutputCustodyVerified => false;
    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible => false;
    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized => false;
    [JsonPropertyName("selection_policy_id")]
    public string SelectionPolicyId { get; set; } = "deterministic_control.safe_held_then_elapsed_energy_stable_id.v1";
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed record RustyKeyRollingCustodySelection(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("reservation_id")] string ReservationId,
    [property: JsonPropertyName("destination_node_id")] string DestinationNodeId,
    [property: JsonPropertyName("position_sha256")] string PositionSha256,
    [property: JsonPropertyName("atomic_commit_request")] ReservationPortfolioCommitRequest? AtomicCommitRequest);

public sealed record RustyKeyRollingCandidateEvaluation(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);
