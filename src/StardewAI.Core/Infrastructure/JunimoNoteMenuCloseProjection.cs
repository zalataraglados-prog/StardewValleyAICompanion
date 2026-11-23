using System;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static class JunimoNoteMenuCloseProjection
{
    internal static string[] BlockReasons(SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object ||
            ReadString(state.Value, "kind") != "junimo_note" ||
            !state.Value.TryGetProperty("ready_to_close", out var ready) ||
            ready.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return new[] { "junimo_note_close_readiness_unavailable" };
        }

        return ready.GetBoolean() ? Array.Empty<string>() : new[] { "menu_not_ready_to_close" };
    }
}
