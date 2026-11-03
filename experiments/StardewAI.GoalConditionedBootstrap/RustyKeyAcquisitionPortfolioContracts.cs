using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class RustyKeyAcquisitionPortfolioAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "rusty_key_acquisition_portfolio_admission.v1";
    [JsonPropertyName("direction_id")]
    public string DirectionId { get; init; } = "obtain_rusty_key";
    [JsonPropertyName("goal_id")]
    public string GoalId { get; init; } = string.Empty;
    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked_rusty_key_acquisition_portfolio";
    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; init; } = string.Empty;
    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; init; } = string.Empty;
    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; init; } = string.Empty;
    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; init; } = string.Empty;
    [JsonPropertyName("proposal_sha256")]
    public string ProposalSha256 { get; init; } = string.Empty;
    [JsonPropertyName("source_save_id")]
    public string SourceSaveId { get; init; } = string.Empty;
    [JsonPropertyName("source_player_id")]
    public string SourcePlayerId { get; init; } = string.Empty;
    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; init; }
    [JsonPropertyName("source_total_day")]
    public int SourceTotalDay { get; set; }
    [JsonPropertyName("donation_threshold")]
    public int DonationThreshold { get; set; }
    [JsonPropertyName("donated_count")]
    public int DonatedCount { get; set; }
    [JsonPropertyName("remaining_donation_count")]
    public int RemainingDonationCount { get; set; }
    [JsonPropertyName("complete_museum_item_denominator")]
    public RustyKeyAcquisitionItem[] CompleteMuseumItemDenominator { get; set; } = Array.Empty<RustyKeyAcquisitionItem>();
    [JsonPropertyName("threshold_satisfied")]
    public bool ThresholdSatisfied { get; set; }
    [JsonPropertyName("acquisition_portfolio_ready")]
    public bool AcquisitionPortfolioReady { get; set; }
    [JsonPropertyName("acquisition_portfolio")]
    public AcquisitionRoutePortfolioAdmission? AcquisitionPortfolio { get; set; }
    [JsonPropertyName("joint_execution_schedule_verified")]
    public bool JointExecutionScheduleVerified => false;
    [JsonPropertyName("donation_output_custody_verified")]
    public bool DonationOutputCustodyVerified => false;
    [JsonPropertyName("native_donation_and_key_handoff_verified")]
    public bool NativeDonationAndKeyHandoffVerified => false;
    [JsonPropertyName("teacher_preference_verified")]
    public bool TeacherPreferenceVerified => false;
    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized => false;
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy => "The full museum denominator is retained. A proposed acquisition portfolio must cover exactly the live deficit with distinct undonated items and pass the existing joint resource/reservation preflight. Other requirement scopes cannot be released. This is read-only supply admission, not proof of an optimal Teacher choice, a joint execution schedule, donation output custody, the native key event or formal training readiness.";
}

public sealed record RustyKeyAcquisitionItem(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("already_donated")] bool AlreadyDonated,
    [property: JsonPropertyName("selected_for_acquisition")] bool SelectedForAcquisition);
