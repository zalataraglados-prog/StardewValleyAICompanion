using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class RuntimeSnapshotBoundaryNormalization
{
    internal static JsonObject? TryCreateRuntimeIdentityBoundary(
        SnapshotEnvelope snapshot)
    {
        var state = new JsonObject();
        foreach (var pair in snapshot.State)
            state[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());

        if (state["environment"] is not JsonObject environment)
            return null;
        foreach (var fieldName in new[]
                 {
                     "training_run_id",
                     "save_isolation_path"
                 })
        {
            if (environment[fieldName] is not JsonObject field ||
                !field.ContainsKey("value"))
            {
                return null;
            }
            field["value"] = "__runtime_identity_excluded__";
        }

        if (state["player"] is JsonObject player &&
            player["movie_theater"] is JsonObject movieTheater &&
            movieTheater["value"] is JsonObject movieTheaterValue)
        {
            movieTheaterValue.Remove("projection_fingerprint");
        }
        RemoveNonSemanticMetadata(state);
        return state;
    }

    internal static bool SameRuntimeIdentityBoundary(
        SnapshotEnvelope expected,
        SnapshotEnvelope actual,
        out string normalizedStateSha256)
    {
        normalizedStateSha256 = string.Empty;
        if (expected.SaveId.Value != actual.SaveId.Value ||
            expected.PlayerId.Value != actual.PlayerId.Value ||
            expected.GameVersion != actual.GameVersion)
        {
            return false;
        }
        var expectedState = TryCreateRuntimeIdentityBoundary(expected);
        var actualState = TryCreateRuntimeIdentityBoundary(actual);
        if (expectedState is null || actualState is null ||
            !JsonNode.DeepEquals(expectedState, actualState))
        {
            return false;
        }
        normalizedStateSha256 = HashNormalizedState(expectedState);
        return true;
    }

    private static string HashNormalizedState(JsonObject state)
    {
        var element = JsonSerializer.SerializeToElement(state);
        var canonical = SnapshotHash.Canonicalize(element);
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    internal static void RemoveNonSemanticMetadata(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                RemoveNonSemanticMetadata(item);
            return;
        }
        if (node is not JsonObject objectNode)
            return;

        foreach (var fieldName in new[]
                 {
                     "read_at_tick",
                     "machine_probe_cache_tick",
                     "projection_tick"
                 })
        {
            objectNode.Remove(fieldName);
        }
        foreach (var child in objectNode.ToArray())
            RemoveNonSemanticMetadata(child.Value);
    }
}
