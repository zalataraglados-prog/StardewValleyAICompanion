using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static NativeObjectSafeItemContext ReadNativeObjectSafeItemContext(SnapshotEnvelope snapshot)
    {
        var value = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
            return NativeObjectSafeItemContext.Unavailable;

        return new NativeObjectSafeItemContext(
            ReadInt(value.Value, "safe_slot_index"),
            ReadString(value.Value, "safe_slot_kind"),
            ReadInt(value.Value, "current_tool_index"));
    }

    private static NativeObjectStand? SelectNearestAvailableNativeObjectStand(
        JsonElement projection,
        int playerX,
        int playerY)
    {
        if (!projection.TryGetProperty("stand_tiles", out var stands) ||
            stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return stands.EnumerateArray()
            .Where(stand => ReadBool(stand, "available") == true)
            .Select(stand =>
            {
                var x = ReadInt(stand, "tile_x");
                var y = ReadInt(stand, "tile_y");
                return new NativeObjectStand(
                    x,
                    y,
                    Math.Abs(playerX - x) + Math.Abs(playerY - y),
                    stand);
            })
            .OrderBy(stand => stand.Distance)
            .ThenBy(stand => stand.Y)
            .ThenBy(stand => stand.X)
            .FirstOrDefault();
    }

    private sealed record NativeObjectSafeItemContext(int SafeSlotIndex, string SafeSlotKind, int RestoreSlotIndex)
    {
        public static NativeObjectSafeItemContext Unavailable { get; } = new(-1, "unavailable", -1);

        public bool AllowsEmpty =>
            SafeSlotIndex is >= 0 and <= 11 &&
            RestoreSlotIndex is >= 0 and <= 11 &&
            string.Equals(SafeSlotKind, "empty", StringComparison.Ordinal);

        public bool AllowsEmptyOrTool =>
            SafeSlotIndex is >= 0 and <= 11 &&
            RestoreSlotIndex is >= 0 and <= 11 &&
            SafeSlotKind is "empty" or "tool";
    }

    private sealed record NativeObjectStand(int X, int Y, int Distance, JsonElement Projection);
}
