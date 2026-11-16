using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static bool SameLiveObservationBoundary(SnapshotEnvelope accepted, SnapshotEnvelope live, out string matchKind)
    {
        matchKind = string.Empty;
        if (!accepted.State.TryGetValue("player", out var beforePlayer) || !live.State.TryGetValue("player", out var livePlayer))
            return false;
        if (JsonNode.Parse(beforePlayer.GetRawText()) is not JsonObject first ||
            JsonNode.Parse(livePlayer.GetRawText()) is not JsonObject next ||
            first["movement_timing_context"] is not JsonObject firstField ||
            next["movement_timing_context"] is not JsonObject nextField ||
            firstField["value"] is not JsonObject before || nextField["value"] is not JsonObject current)
            return false;
        var terrainFields = new[] { "temporary_speed_buff_now", "current_native_on_foot_speed_scalar" };
        var terrainChanged = terrainFields.Any(name => !JsonNode.DeepEquals(before[name], current[name]));
        var pauseChanged = new[] { "executor_owned_idle_pause", "game_paused", "native_control_gate_open", "route_timing_ready_now" }
            .Any(name => !JsonNode.DeepEquals(before[name], current[name]));
        if (!terrainChanged && !pauseChanged) return false;
        if (terrainChanged)
        {
            if (!TryTerrainMovement(accepted, out _) || !TryTerrainMovement(live, out _)) return false;
            foreach (var name in terrainFields) current[name] = before[name]!.DeepClone();
        }
        if (pauseChanged)
        {
            var normalizedBefore = NormalizeVerifiedOwnedPause(firstField);
            var normalizedAfter = NormalizeVerifiedOwnedPause(nextField);
            if (!normalizedBefore && !normalizedAfter) return false;
        }
        // Views only: keep every real snapshot, ledger source, and unrelated state field intact.
        string Hash(SnapshotEnvelope source, JsonObject player) => SnapshotHash.ComputeStateHash(
            new Dictionary<string, JsonElement>(source.State) { ["player"] = JsonSerializer.SerializeToElement(player) });
        if (Hash(accepted, first) != Hash(live, next)) return false;
        matchKind = terrainChanged ? pauseChanged ? "native_terrain_speed_and_owned_pause_refresh" : "native_terrain_speed_refresh"
            : "executor_owned_idle_pause_refresh";
        return true;
    }

    private static bool NormalizeVerifiedOwnedPause(JsonObject field)
    {
        if (!FutureRouteTimingCalibrationLoader.HasVerifiedExecutorOwnedIdlePause(JsonSerializer.SerializeToElement(field)) ||
            field["value"] is not JsonObject movement ||
            movement["native_control_gate_open"] is not JsonValue gate || !gate.TryGetValue<bool>(out var gateOpen) || gateOpen ||
            movement["route_timing_ready_now"] is not JsonValue ready || !ready.TryGetValue<bool>(out var routeReady) || routeReady)
            return false;
        movement["executor_owned_idle_pause"] = null;
        movement["game_paused"] = false;
        movement["native_control_gate_open"] = true;
        movement["route_timing_ready_now"] = true;
        return true;
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
