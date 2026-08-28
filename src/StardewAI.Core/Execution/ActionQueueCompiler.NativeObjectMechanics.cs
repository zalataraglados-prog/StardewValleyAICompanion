using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static NativeObjectCompilerSafeItemContext ReadNativeObjectCompilerSafeItemContext(
        SnapshotEnvelope snapshot)
    {
        var value = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        return !value.HasValue || value.Value.ValueKind != JsonValueKind.Object
            ? NativeObjectCompilerSafeItemContext.Unavailable
            : new NativeObjectCompilerSafeItemContext(
                ReadInt(value.Value, "safe_slot_index"),
                ReadString(value.Value, "safe_slot_kind"),
                ReadInt(value.Value, "current_tool_index"));
    }

    private static NativeObjectCompilerProjection? SelectExactReadyNativeObjectProjection(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        string projectionPropertyName)
    {
        var requestedX = ReadIntParameter(action, "target_tile_x");
        var requestedY = ReadIntParameter(action, "target_tile_y");
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!requestedX.HasValue || !requestedY.HasValue || !objects.HasValue ||
            objects.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var row = objects.Value.EnumerateArray().FirstOrDefault(item =>
            ReadInt(item, "tile_x") == requestedX.Value &&
            ReadInt(item, "tile_y") == requestedY.Value &&
            item.TryGetProperty(projectionPropertyName, out var projection) &&
            projection.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(projection, "status"), "ready", StringComparison.Ordinal));
        if (row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty(projectionPropertyName, out var value) ||
            !value.TryGetProperty("stand_tiles", out var stands) ||
            stands.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var stand = stands.EnumerateArray()
            .Where(item => ReadBool(item, "available") == true)
            .Select(item =>
            {
                var x = ReadInt(item, "tile_x");
                var y = ReadInt(item, "tile_y");
                return new
                {
                    X = x,
                    Y = y,
                    Distance = Math.Abs(playerX - x) + Math.Abs(playerY - y),
                    Projection = item
                };
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Y)
            .ThenBy(item => item.X)
            .FirstOrDefault();
        return stand is null
            ? null
            : new NativeObjectCompilerProjection(
                requestedX.Value, requestedY.Value, stand.X, stand.Y, row, value, stand.Projection);
    }

    private sealed record NativeObjectCompilerProjection(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        JsonElement Row,
        JsonElement Projection,
        JsonElement Stand);

    private sealed record NativeObjectCompilerSafeItemContext(
        int SafeSlotIndex,
        string SafeSlotKind,
        int RestoreSlotIndex)
    {
        public static NativeObjectCompilerSafeItemContext Unavailable { get; } =
            new(-1, "unavailable", -1);

        public bool AllowsEmpty =>
            HasValidSlots && string.Equals(SafeSlotKind, "empty", StringComparison.Ordinal);

        public bool AllowsEmptyOrTool =>
            HasValidSlots && SafeSlotKind is "empty" or "tool";

        private bool HasValidSlots =>
            SafeSlotIndex is >= 0 and <= 11 && RestoreSlotIndex is >= 0 and <= 11;
    }
}
