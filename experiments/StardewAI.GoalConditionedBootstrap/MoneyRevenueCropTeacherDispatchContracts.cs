using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class PlanningHandoffCaptureManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("map_width")]
    public int MapWidth { get; set; }

    [JsonPropertyName("map_height")]
    public int MapHeight { get; set; }

    [JsonPropertyName("layout_chunk_size")]
    public int LayoutChunkSize { get; set; }

    [JsonPropertyName("layout_snapshot_count")]
    public int LayoutSnapshotCount { get; set; }

    [JsonPropertyName("layout_snapshots")]
    public PlanningHandoffLayoutSnapshotBinding[] LayoutSnapshots
        { get; set; } = Array.Empty<PlanningHandoffLayoutSnapshotBinding>();

    [JsonPropertyName("planning_handoff")]
    public JsonElement PlanningHandoff { get; set; }
}

public sealed class PlanningHandoffLayoutSnapshotBinding
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("min_x")]
    public int MinX { get; set; }

    [JsonPropertyName("min_y")]
    public int MinY { get; set; }

    [JsonPropertyName("max_x")]
    public int MaxX { get; set; }

    [JsonPropertyName("max_y")]
    public int MaxY { get; set; }

    [JsonPropertyName("tile_count")]
    public int TileCount { get; set; }
}

public sealed class MoneyRevenueCropTeacherDispatchManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_teacher_dispatch_manifest.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } =
        "native_crop_production_run_plan_ready";

    [JsonPropertyName("capture_manifest_path")]
    public string CaptureManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("capture_manifest_sha256")]
    public string CaptureManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("teacher_request_path")]
    public string TeacherRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("teacher_request_sha256")]
    public string TeacherRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_decision_path")]
    public string TeacherDecisionPath { get; set; } = string.Empty;

    [JsonPropertyName("teacher_decision_sha256")]
    public string TeacherDecisionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_receipt_path")]
    public string DecisionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("decision_receipt_sha256")]
    public string DecisionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("obligation_request_path")]
    public string ObligationRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("obligation_request_sha256")]
    public string ObligationRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("obligation_plan_path")]
    public string ObligationPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("obligation_plan_sha256")]
    public string ObligationPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_run_plan_request_path")]
    public string NativeRunPlanRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("native_run_plan_request_sha256")]
    public string NativeRunPlanRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_run_plan_path")]
    public string NativeRunPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("native_run_plan_sha256")]
    public string NativeRunPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("covered_commitment_ids")]
    public string[] CoveredCommitmentIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("first_total_day")]
    public int FirstTotalDay { get; set; }

    [JsonPropertyName("last_total_day")]
    public int LastTotalDay { get; set; }

    [JsonPropertyName("action_count")]
    public int ActionCount { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
