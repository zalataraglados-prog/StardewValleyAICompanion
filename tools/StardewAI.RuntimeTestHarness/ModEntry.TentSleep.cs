using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string TentSleepNativeRuntimeContract =
        "GameLocation.checkAction->Tent.performUseAction->SleepTent_Yes->startSleep->CanWakeUpHere(sleptInTemporaryBed)->Tent.dayUpdate/tickUpdate";

    private void StartTentSleep(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = TentSleepRequestedEffect(request);
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), reasons.ToArray()));
            return;
        }
        if (activeSleep is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "sleep_executor_busy"));
            return;
        }
        if (Game1.activeClickableMenu is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_menu_must_be_clear"));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue || request.Direction != 0 ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_typed_target_fields_required"));
            return;
        }
        if (!string.Equals(request.TargetRuntimeType, typeof(Tent).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, TentSleepNativeRuntimeContract, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_native_contract_mismatch"));
            return;
        }

        var location = Game1.currentLocation;
        var anchor = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            stand != new Point(anchor.X, anchor.Y + 1))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_location_or_canonical_stand_drifted"));
            return;
        }
        var tent = location.largeTerrainFeatures.FirstOrDefault(feature =>
            feature.GetType() == typeof(Tent) && feature.Tile == anchor.ToVector2()) as Tent;
        if (tent is null || tent.health.Value <= 0 || !tent.isPassable(Game1.player) || tent.isPassable())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_exact_tent_identity_or_health_drifted"));
            return;
        }

        var startTile = Game1.player.TilePoint;
        var path = TryBuildTilePath(location, startTile, stand, 512, out var pathReason, avoidSoftObstacles: true);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "sleep_in_tent", requested, SleepObservedEffect(), "tent_sleep_" + pathReason));
            return;
        }

        activeSleep = new ActiveSleep(
            pending,
            startTile,
            anchor,
            stand,
            path,
            Game1.year,
            Game1.dayOfMonth,
            Game1.timeOfDay,
            Game1.currentSeason,
            SleepMode.Tent,
            location.NameOrUniqueName,
            anchor);
        Monitor.Log($"Started terminal Tent sleep through {location.NameOrUniqueName}@{anchor.X},{anchor.Y} via canonical stand {stand.X},{stand.Y}.", LogLevel.Info);
    }

    private bool TryOpenNativeTentSleepPrompt(ActiveSleep sleep, out string reason)
    {
        reason = string.Empty;
        if (sleep.TentAnchor is not Point anchor || Game1.currentLocation is not { } location ||
            !string.Equals(location.NameOrUniqueName, sleep.StartLocationId, StringComparison.Ordinal) ||
            Game1.player.TilePoint != sleep.StandTile || sleep.StandTile != new Point(anchor.X, anchor.Y + 1))
        {
            reason = "tent_sleep_location_or_canonical_stand_drifted";
            return false;
        }
        var tent = location.largeTerrainFeatures.FirstOrDefault(feature =>
            feature.GetType() == typeof(Tent) && feature.Tile == anchor.ToVector2()) as Tent;
        if (tent is null || tent.health.Value <= 0 || !tent.isPassable(Game1.player) || tent.isPassable())
        {
            reason = "tent_sleep_exact_tent_identity_or_health_drifted";
            return false;
        }
        if (Game1.newDay || !Game1.shouldTimePass() || !Game1.player.hasMoved || Game1.player.passedOut ||
            Game1.activeClickableMenu is not null)
        {
            reason = "tent_sleep_native_prompt_gate_closed_at_dispatch";
            return false;
        }

        Game1.player.faceDirection(0);
        sleep.NativeTentPromptDispatched = true;
        location.checkAction(
            new TileLocation(anchor.X, anchor.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (Tent.lastTentTouchedByPlayer != anchor.ToVector2())
        {
            reason = "tent_sleep_native_touch_receipt_missing";
            return false;
        }
        return true;
    }

    private static string TentSleepRequestedEffect(TrainingExecutionRequest request)
    {
        return "time.total_days=before+1;player.location_id=" + request.LocationId +
            ";player.tile=" + request.StandTileX + "," + request.StandTileY +
            ";current_location.large_terrain_features[" + request.TargetTileX + "," + request.TargetTileY + "]=destroyed";
    }
}
