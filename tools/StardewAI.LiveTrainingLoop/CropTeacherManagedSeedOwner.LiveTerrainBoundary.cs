using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static bool SameLiveTerrainSpeedBoundary(SnapshotEnvelope accepted, SnapshotEnvelope live)
    {
        if (!TryTerrainMovement(accepted, out var before) || !TryTerrainMovement(live, out _))
            return false;
        var player = JsonNode.Parse(live.State["player"].GetRawText())!.AsObject();
        var movement = player["movement_timing_context"]!["value"]!.AsObject();
        // Comparison view only: native collision can refresh these at the same paused tick.
        // Keep both real snapshots/hashes and every other control/resource field untouched.
        foreach (var name in new[] { "temporary_speed_buff_now", "current_native_on_foot_speed_scalar" })
            movement[name] = JsonNode.Parse(before.GetProperty(name).GetRawText());
        var comparison = new Dictionary<string, JsonElement>(live.State)
        {
            ["player"] = JsonSerializer.SerializeToElement(player)
        };
        return SnapshotHash.ComputeStateHash(comparison) == accepted.StateHash;
    }

    private static bool TryTerrainMovement(SnapshotEnvelope snapshot, out JsonElement movement)
    {
        movement = default;
        if (!snapshot.State.TryGetValue("player", out var player) || player.ValueKind != JsonValueKind.Object ||
            !player.TryGetProperty("movement_timing_context", out var field) || field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String ||
            !FieldEnvelopeValidator.IsReadableStatus(status.GetString()!) ||
            !field.TryGetProperty("value", out movement) || movement.ValueKind != JsonValueKind.Object ||
            !movement.TryGetProperty("projection_status", out var projection) || projection.ValueKind != JsonValueKind.String ||
            projection.GetString() != "exact_current_player_native_cardinal_movement_context" ||
            !movement.TryGetProperty("riding_horse_now", out var horse) || horse.ValueKind != JsonValueKind.False)
            return false;
        var context = movement;
        return Number("base_speed_now", out var speed) && Number("added_speed_now", out var added) &&
            Number("temporary_speed_buff_now", out var temporary) && temporary is >= -3f and <= 0f &&
            Number("current_native_on_foot_speed_scalar", out var scalar) && scalar == Math.Max(1f, speed + added + temporary);

        bool Number(string name, out float value)
        {
            value = 0;
            return context.TryGetProperty(name, out var number) && number.ValueKind == JsonValueKind.Number &&
                number.TryGetSingle(out value) && float.IsFinite(value);
        }
    }
}
