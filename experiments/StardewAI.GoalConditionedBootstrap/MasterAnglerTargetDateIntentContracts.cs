using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MasterAnglerTargetDateIntentSet
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "master_angler_target_date_intents.v1";
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("source_snapshot_path")] public string SourceSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("source_state_hash")] public string SourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("window_index_path")] public string WindowIndexPath { get; set; } = string.Empty;
    [JsonPropertyName("window_index_sha256")] public string WindowIndexSha256 { get; set; } = string.Empty;
    [JsonPropertyName("route_timing_calibration_path")] public string RouteTimingCalibrationPath { get; set; } = string.Empty;
    [JsonPropertyName("route_timing_calibration_sha256")] public string RouteTimingCalibrationSha256 { get; set; } = string.Empty;
    [JsonPropertyName("current_total_day")] public int CurrentTotalDay { get; set; }
    [JsonPropertyName("current_time")] public int CurrentTime { get; set; }
    [JsonPropertyName("current_location_id")] public string CurrentLocationId { get; set; } = string.Empty;
    [JsonPropertyName("missing_species_count")] public int MissingSpeciesCount { get; set; }
    [JsonPropertyName("candidate_count")] public int CandidateCount { get; set; }
    [JsonPropertyName("training_label_eligible")] public bool TrainingLabelEligible { get; set; }
    [JsonPropertyName("candidates")] public MasterAnglerTargetDateIntent[] Candidates { get; set; } = Array.Empty<MasterAnglerTargetDateIntent>();
    [JsonPropertyName("blocking_reasons")] public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class MasterAnglerTargetDateIntent
{
    [JsonPropertyName("intent_id")] public string IntentId { get; set; } = string.Empty;
    [JsonPropertyName("option_id")] public string OptionId { get; set; } = "fishing.catch_fish";
    [JsonPropertyName("target_qualified_item_id")] public string TargetQualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("target_location")] public string TargetLocation { get; set; } = string.Empty;
    [JsonPropertyName("source_kind")] public string SourceKind { get; set; } = string.Empty;
    [JsonPropertyName("source_key")] public string SourceKey { get; set; } = string.Empty;
    [JsonPropertyName("first_total_day")] public int FirstTotalDay { get; set; }
    [JsonPropertyName("last_total_day")] public int LastTotalDay { get; set; }
    [JsonPropertyName("effective_start_time")] public int EffectiveStartTime { get; set; }
    [JsonPropertyName("last_cast_time_exclusive")] public int LastCastTimeExclusive { get; set; }
    [JsonPropertyName("route_edge_count")] public int RouteEdgeCount { get; set; }
    [JsonPropertyName("route_guaranteed_arrival_time")] public int RouteGuaranteedArrivalTime { get; set; }
    [JsonPropertyName("route_timing_status")] public string RouteTimingStatus { get; set; } = string.Empty;
    [JsonPropertyName("route_timing_evidence_id")] public string RouteTimingEvidenceId { get; set; } = string.Empty;
    [JsonPropertyName("deadline_slack_days")] public int DeadlineSlackDays { get; set; }
    [JsonPropertyName("runtime_terminal_validation_required")] public bool RuntimeTerminalValidationRequired { get; set; } = true;
    [JsonPropertyName("parameters")] public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
}
