using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

internal static class SinglePlayerScope
{
    public static bool IsKnownSinglePlayer(SnapshotEnvelope? snapshot)
    {
        if (snapshot is null || !SnapshotValueReader.ReadableStatus(
                SnapshotValueReader.ReadStateFieldStatus(snapshot, "player", "multiplayer_runtime")))
            return false;
        var value = SnapshotValueReader.ReadStateFieldValue(snapshot, "player", "multiplayer_runtime");
        return value is { ValueKind: JsonValueKind.Object } &&
            value.Value.TryGetProperty("context_is_multiplayer", out var multiplayer) &&
            multiplayer.ValueKind == JsonValueKind.False;
    }

    public static bool IsSinglePlayerTraining(FeatureVector? features)
    {
        var modes = (features?.Categorical ?? Array.Empty<CategoricalFeature>())
            .Where(feature => feature is not null && feature.Name == "world.mode").Take(2).ToArray();
        return modes.Length == 1 && string.Equals(modes[0].Value,
            ExecutionTargetProfiles.TrainingSingleplayer, StringComparison.Ordinal);
    }
}
