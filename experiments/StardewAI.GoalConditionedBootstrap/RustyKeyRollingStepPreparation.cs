using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class RustyKeyRollingStepPreparation
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "rusty_key_rolling_step_preparation.v1";
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "blocked";
    [JsonPropertyName("step_ready")]
    public bool StepReady { get; set; }
    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;
    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }
    [JsonPropertyName("control_selection_sha256")]
    public string ControlSelectionSha256 { get; set; } = string.Empty;
    [JsonPropertyName("ranking_sha256")]
    public string RankingSha256 { get; set; } = string.Empty;
    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;
    [JsonPropertyName("compiled_queue")]
    public ActionQueueEnvelope? CompiledQueue { get; set; }
    [JsonPropertyName("owned_supply_deferral")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RustyKeyOwnedSupplyDeferral? OwnedSupplyDeferral { get; set; }
    [JsonPropertyName("atomic_commit_request")]
    public ReservationPortfolioCommitRequest? AtomicCommitRequest { get; set; }
    [JsonPropertyName("support_inputs")]
    public AcquisitionRouteSupportingTransitionRequestInputs? SupportInputs { get; set; }
    [JsonPropertyName("support_request")]
    public AcquisitionRouteSupportingTransitionRequest? SupportRequest { get; set; }
    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("execution_authorized")]
    public bool ExecutionAuthorized => false;
    [JsonPropertyName("teacher_preference_label_eligible")]
    public bool TeacherPreferenceLabelEligible => false;
    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized => false;
}
