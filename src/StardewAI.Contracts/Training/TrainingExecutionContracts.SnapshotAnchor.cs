using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_snapshot_anchor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginSnapshotAnchor { get; set; }
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("product_after_state_hash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductAfterStateHash { get; set; }

    [JsonPropertyName("product_after_game_tick")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ProductAfterGameTick { get; set; }
}

// A fresh cached snapshot identity, not an action outcome or production certificate.
public sealed class NativeOutputSnapshotAnchor
{
    private NativeOutputSnapshotAnchor(string stateHash, long gameTick)
    { StateHash = stateHash; GameTick = gameTick; }

    public string StateHash { get; }
    public long GameTick { get; }

    public static NativeOutputSnapshotAnchor Read(string json, TrainingExecutionRequest request,
        long currentTick, string saveId, string playerId)
    {
        Require(request.ActionOriginSnapshotAnchor && request.ActionOriginObservationScopeId is not null &&
                request.ExecutionMode == "training_singleplayer" &&
                (request.ActionOriginTillingResources ? NativeTillingObservationContract.IsCompleteRequest(request) :
                    request.OptionId is "executor.move_to_tile" or "executor.clear_obstacle") &&
                new[] { request.ActionOriginObservationScopeId, request.RunId, request.QueueId, request.QueueItemId,
                        request.RequestNonce, request.BeforeStateHash, request.SourcePlanningOptionId, request.SaveIsolationPath, saveId, playerId }
                    .All(value => !string.IsNullOrWhiteSpace(value)) && currentTick >= 0 &&
                json is not null && json.Length <= 16384,
            "snapshot anchor request/context is incomplete");
        try
        {
            using var document = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            var names = new[] { "schema_version", "status", "snapshot_profile", "state_hash", "game_tick", "game_version",
                "save_id", "player_id", "run_id", "save_isolation_path", "diagnostics", "native_outcome_verified", "formal_product_training_authorized" };
            Require(root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Count() == names.Length &&
                    root.EnumerateObject().Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() == names.Length &&
                    names.All(name => root.TryGetProperty(name, out _)), "snapshot anchor fields are missing, duplicate or unknown");
            string Text(string name)
            {
                var value = root.GetProperty(name);
                Require(value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()),
                    "snapshot anchor field unreadable: " + name);
                return value.GetString()!;
            }
            var hash = Text("state_hash");
            var tick = root.GetProperty("game_tick");
            Require(Text("schema_version") == "native_output_snapshot_anchor.v1" && Text("status") == "available" &&
                    Text("snapshot_profile") == "full" && Text("game_version") == "1.6.15" &&
                    Text("save_id") == saveId && Text("player_id") == playerId && Text("run_id") == request.RunId &&
                    Text("save_isolation_path") == request.SaveIsolationPath && hash.Length == 64 && hash.All(Uri.IsHexDigit) &&
                    tick.ValueKind == JsonValueKind.Number && tick.TryGetInt64(out var observed) && observed == currentTick &&
                    root.GetProperty("diagnostics").ValueKind == JsonValueKind.Array && root.GetProperty("diagnostics").GetArrayLength() == 0 &&
                    root.GetProperty("native_outcome_verified").ValueKind == JsonValueKind.False &&
                    root.GetProperty("formal_product_training_authorized").ValueKind == JsonValueKind.False,
                "snapshot anchor identity/profile/tick or authorization differs from the current native scope");
            return new(hash, currentTick);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Snapshot anchor JSON is invalid.", error);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
