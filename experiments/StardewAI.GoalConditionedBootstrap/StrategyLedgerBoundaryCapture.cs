using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class StrategyLedgerBoundaryKinds
{
    public const string BeforeTerminalAction = "before_terminal_action";
    public const string AfterTerminalAction = "after_terminal_action";

    public static bool IsKnown(string value) =>
        value == BeforeTerminalAction || value == AfterTerminalAction;
}

public sealed class StrategyLedgerBoundaryCapture
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "strategy_ledger_boundary_capture.v1";

    [JsonPropertyName("capture_id")]
    public string CaptureId { get; set; } = string.Empty;

    [JsonPropertyName("boundary_kind")]
    public string BoundaryKind { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_id")]
    public string ConcreteAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("game_tick")]
    public long GameTick { get; set; }

    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_id")]
    public string StrategyLedgerId { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("strategy_ledger_source_state_hash")]
    public string StrategyLedgerSourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("actor_player_id")]
    public long ActorPlayerId { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public static class StrategyLedgerBoundaryCaptureBuilder
{
    public static StrategyLedgerBoundaryCapture Build(
        string boundaryKind,
        string requestId,
        string concreteAlternativeId,
        string runId,
        string snapshotPath,
        string strategyLedgerPath)
    {
        Require(StrategyLedgerBoundaryKinds.IsKnown(boundaryKind) &&
                !string.IsNullOrWhiteSpace(requestId) &&
                !string.IsNullOrWhiteSpace(concreteAlternativeId) &&
                !string.IsNullOrWhiteSpace(runId),
            "Strategy-ledger boundary identity is invalid.");
        var fullSnapshotPath = RequiredFile(snapshotPath, "Boundary snapshot");
        var fullLedgerPath = RequiredFile(
            strategyLedgerPath,
            "Boundary strategy ledger");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            fullSnapshotPath,
            "Boundary snapshot");
        var ledgerState = AcquisitionStrategyLedgerReader.Read(
            fullLedgerPath,
            snapshot);
        var ledger = ledgerState.Ledger;
        var snapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
            fullSnapshotPath);
        var ledgerSha256 = CurrentTeacherFrontierSupport.HashFile(
            fullLedgerPath);
        var identity = new
        {
            schema_version = "strategy_ledger_boundary_capture_identity.v1",
            boundary_kind = boundaryKind,
            request_id = requestId,
            concrete_alternative_id = concreteAlternativeId,
            run_id = runId,
            snapshot_sha256 = snapshotSha256,
            state_hash = snapshot.StateHash,
            game_tick = snapshot.GameTick,
            strategy_ledger_sha256 = ledgerSha256,
            strategy_ledger_id = ledger.LedgerId,
            strategy_ledger_revision = ledger.Revision,
            strategy_ledger_source_state_hash = ledger.SourceStateHash,
            actor_player_id = ledgerState.ActorPlayerId
        };
        return new StrategyLedgerBoundaryCapture
        {
            CaptureId = "strategy-ledger-boundary." +
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ArtifactSha256(identity),
            BoundaryKind = boundaryKind,
            RequestId = requestId,
            ConcreteAlternativeId = concreteAlternativeId,
            RunId = runId,
            SnapshotPath = fullSnapshotPath,
            SnapshotSha256 = snapshotSha256,
            StateHash = snapshot.StateHash,
            GameTick = snapshot.GameTick,
            StrategyLedgerPath = fullLedgerPath,
            StrategyLedgerSha256 = ledgerSha256,
            StrategyLedgerId = ledger.LedgerId,
            StrategyLedgerRevision = ledger.Revision,
            StrategyLedgerSourceStateHash = ledger.SourceStateHash,
            ActorPlayerId = ledgerState.ActorPlayerId,
            FormalProductTrainingAuthorized = false
        };
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
