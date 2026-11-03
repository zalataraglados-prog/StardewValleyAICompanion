using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record AcquisitionCurrentNativeSourceBinding(
    [property: JsonPropertyName("snapshot_sha256")] string SnapshotSha256,
    [property: JsonPropertyName("state_hash")] string StateHash,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion)
{
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "non_community_center";

    internal static AcquisitionCurrentNativeSourceBinding Read(string path)
    {
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "Current native source snapshot");
        if (snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State) || string.IsNullOrWhiteSpace(snapshot.GameVersion) ||
            snapshot.SaveId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.SaveId.Value) ||
            snapshot.PlayerId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.PlayerId.Value) ||
            !MatchesIdentity(snapshot, "save_id", snapshot.SaveId.Value) ||
            !MatchesIdentity(snapshot, "player_id", snapshot.PlayerId.Value))
            throw new InvalidDataException("Current native source snapshot hash/actor/save identity is invalid.");
        return new(CurrentTeacherFrontierSupport.HashFile(path), snapshot.StateHash, snapshot.SaveId.Value, snapshot.PlayerId.Value, snapshot.GameVersion);
    }

    private static bool MatchesIdentity(SnapshotEnvelope snapshot, string name, string expected) =>
        snapshot.State.TryGetValue("identity", out var domain) && domain.ValueKind == JsonValueKind.Object &&
        domain.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Object &&
        field.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "available" &&
        field.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == expected;
}
