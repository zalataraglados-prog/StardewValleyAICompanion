using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure
{
    internal static class JsonSnapshotEnvelopeAdapter
    {
        public static bool TryCreate(
            JsonElement snapshot,
            out SnapshotEnvelope envelope)
        {
            envelope = new SnapshotEnvelope();
            if (snapshot.ValueKind != JsonValueKind.Object ||
                !snapshot.TryGetProperty("state", out var state) ||
                state.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            envelope.State = state.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value,
                StringComparer.Ordinal);
            envelope.GameVersion = ReadString(snapshot, "game_version");
            envelope.StateHash = ReadString(snapshot, "state_hash");
            envelope.GameTick = snapshot.TryGetProperty(
                    "game_tick",
                    out var tick) &&
                tick.TryGetInt64(out var parsedTick)
                    ? parsedTick
                    : 0;
            return true;
        }

        private static string ReadString(
            JsonElement source,
            string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }
}
