using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

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
    [JsonPropertyName("selected_donation_route_schedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MuseumDonationRouteSchedule? SelectedDonationRouteSchedule { get; set; }
    [JsonPropertyName("selected_custody_transfer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyRollingCustodySelection? SelectedCustodyTransfer { get; set; }
    [JsonPropertyName("selected_custody_donation_schedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyCustodyDonationSchedule? SelectedCustodyDonationSchedule { get; set; }
    [JsonPropertyName("selected_day_recovery")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyRollingDayRecovery? SelectedDayRecovery { get; set; }
    [JsonPropertyName("selected_acquisition_proposal")]
    public AcquisitionRoutePortfolioProposal? SelectedAcquisitionProposal { get; set; }
    [JsonPropertyName("selected_acquisition_admission")]
    public AcquisitionRoutePortfolioAdmission? SelectedAcquisitionAdmission { get; set; }
    [JsonPropertyName("selected_acquisition_donation_schedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyAcquisitionDonationSchedule? SelectedAcquisitionDonationSchedule { get; set; }
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

public sealed record RustyKeyRollingDayRecovery(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("source_total_day")] int SourceTotalDay,
    [property: JsonPropertyName("earliest_resume_total_day")] int EarliestResumeTotalDay,
    [property: JsonPropertyName("owned_requirement_ids")] string[] OwnedRequirementIds,
    [property: JsonPropertyName("recovery_candidate_id")] string RecoveryCandidateId,
    [property: JsonPropertyName("recovery_candidate_kind")] string RecoveryCandidateKind)
{
    [JsonPropertyName("future_date_route_verified")]
    public bool FutureDateRouteVerified => false;
    [JsonPropertyName("fresh_snapshot_replan_required")]
    public bool FreshSnapshotReplanRequired => true;
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

public sealed record RustyKeyCustodyDonationSchedule(
    [property: JsonPropertyName("reservation_id")] string ReservationId,
    [property: JsonPropertyName("position_sha256")] string PositionSha256,
    [property: JsonPropertyName("destination_node_id")] string DestinationNodeId,
    [property: JsonPropertyName("route_schedule")] MuseumWithdrawalDonationRouteSchedule? RouteSchedule,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("current_day_custody_donation_route_ready")]
    public bool CurrentDayCustodyDonationRouteReady => BlockingReasons.Length == 0 &&
        RouteSchedule is { CurrentDayWithdrawalDonationRouteReady: true };
    [JsonPropertyName("joint_execution_schedule_verified")]
    public bool JointExecutionScheduleVerified => false;
}

public sealed record RustyKeyAcquisitionDonationSchedule(
    [property: JsonPropertyName("route_occurrence_id")] string RouteOccurrenceId,
    [property: JsonPropertyName("acquisition_daily_budget_sha256")] string AcquisitionDailyBudgetSha256,
    [property: JsonPropertyName("terminal_location_id")] string TerminalLocationId,
    [property: JsonPropertyName("terminal_stand_tile_x")] int? TerminalStandTileX,
    [property: JsonPropertyName("terminal_stand_tile_y")] int? TerminalStandTileY,
    [property: JsonPropertyName("acquisition_completion_by_time")] int? AcquisitionCompletionByTime,
    [property: JsonPropertyName("donation_route_schedule")] MuseumDonationRouteSchedule? DonationRouteSchedule,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("current_day_supply_donation_schedule_ready")]
    public bool CurrentDaySupplyDonationScheduleReady => BlockingReasons.Length == 0 && DonationRouteSchedule is { CurrentDayRouteReady: true };
    [JsonPropertyName("native_acquisition_verified")]
    public bool NativeAcquisitionVerified => false;
    [JsonPropertyName("joint_execution_schedule_verified")]
    public bool JointExecutionScheduleVerified => false;
}
