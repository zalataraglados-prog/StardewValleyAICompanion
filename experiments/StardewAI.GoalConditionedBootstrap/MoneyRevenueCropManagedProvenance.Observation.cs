using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class MoneyRevenueCropManagedProvenance
{
    private static bool OwnedPauseObservation(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        try
        {
            if (after.GameTick < before.GameTick || before.SaveId.Value != after.SaveId.Value ||
                before.PlayerId.Value != after.PlayerId.Value || before.GameVersion != after.GameVersion ||
                before.InGameTime.Value != after.InGameTime.Value ||
                !after.State.TryGetValue("player", out var player) ||
                !player.TryGetProperty("movement_timing_context", out var movement) ||
                !FutureRouteTimingCalibrationLoader.HasVerifiedExecutorOwnedIdlePause(movement) ||
                movement.GetProperty("value").GetProperty("player_id").GetString() != after.PlayerId.Value)
                return false;
            JsonElement Value(SnapshotEnvelope snapshot, string domain, string field) => snapshot.State[domain].GetProperty(field).GetProperty("value");
            foreach (var (domain, field) in new[] { ("time", "total_days"), ("player", "location_id"), ("player", "tile_x"), ("player", "tile_y") })
                if (!EqualJson(Value(before, domain, field), Value(after, domain, field))) return false;
            if (!GoldenWalnutFreshSaveProgressProjectionComparer.SameGingerIslandProgress(
                    Value(before, "locations", "ginger_island_access"), Value(after, "locations", "ginger_island_access")) ||
                !GoldenWalnutFreshSaveProgressProjectionComparer.SameGoldenWalnutSourceProgress(
                    Value(before, "world_progress", "golden_walnut_sources"), Value(after, "world_progress", "golden_walnut_sources")))
                return false;
            CropTeacherSavedResourceReadback.VerifySnapshotContent(before, after);
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or KeyNotFoundException or JsonException)
        {
            return false;
        }
    }
}
