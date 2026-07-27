using StardewAI.Contracts.Training;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePrepareIncubatorSleep(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var requested =
            "player.location=home;sleep_executor_ready=true;" +
            "incubator_state_unchanged=true";
        if (reasons.Count > 0 ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_incubator_sleep",
                requested,
                IncubatorHatchObservedEffect(),
                reasons.Concat(new[]
                {
                    "incubator_sleep_fixture_request_invalid"
                }).ToArray());
        }

        if (Game1.getLocationFromName(request.LocationId) is
                not AnimalHouse house ||
            Game1.getLocationFromName(
                Game1.player.homeLocation.Value) is
                not FarmHouse home)
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_incubator_sleep",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_or_player_home_location_unavailable");
        }

        var target = new Vector2(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        if (!house.objects.TryGetValue(
                target,
                out var incubator) ||
            incubator.GetMachineData()?.IsIncubator != true ||
            incubator.heldObject.Value is null ||
            incubator.MinutesUntilReady <= 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_incubator_sleep",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_sleep_fixture_requires_loaded_unready_incubator");
        }

        var minutesBefore = incubator.MinutesUntilReady;
        var eggBefore =
            incubator.heldObject.Value.QualifiedItemId;
        var bed = home.GetPlayerBedSpot();
        if (!BedFurniture.IsBedHere(
                home,
                bed.X,
                bed.Y))
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_incubator_sleep",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_sleep_fixture_home_bed_unverified");
        }

        var stand = new[]
            {
                new Point(bed.X - 1, bed.Y),
                new Point(bed.X + 1, bed.Y),
                new Point(bed.X, bed.Y + 1),
                new Point(bed.X, bed.Y - 1)
            }
            .FirstOrDefault(tile =>
                IsTileWalkable(home, tile));
        if (stand == default)
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_incubator_sleep",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_sleep_fixture_home_bed_stand_unavailable");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.activeClickableMenu = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.warpFarmer(
            home.NameOrUniqueName,
            stand.X,
            stand.Y,
            false);
        var verified =
            house.objects.TryGetValue(
                target,
                out var observed) &&
            ReferenceEquals(observed, incubator) &&
            incubator.MinutesUntilReady == minutesBefore &&
            string.Equals(
                incubator.heldObject.Value?.QualifiedItemId,
                eggBefore,
                StringComparison.Ordinal);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind =
                "debug_prepare_incubator_sleep",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Game1_warpFarmer_to_home_requested",
                    "home_location_id=" +
                        home.NameOrUniqueName,
                    "home_stand_tile=" +
                        stand.X + "," + stand.Y,
                    "incubator_state_unchanged",
                    "minutes_until_ready=" + minutesBefore,
                    "egg_qualified_item_id=" + eggBefore
                }
                : new[]
                {
                    "incubator_sleep_fixture_post_state_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect =
                "player.location=" +
                (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";player.tile=" +
                Game1.player.TilePoint.X +
                "," +
                Game1.player.TilePoint.Y +
                ";minutes_until_ready=" +
                incubator.MinutesUntilReady,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "incubator_sleep_fixture_post_state_mismatch"
                },
            ChangedFacts = Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteEnterReadyIncubatorHouse(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var requested =
            "player.location=animal_house;" +
            "native_incubator_naming_event_requested=true";
        if (reasons.Count > 0 ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            return BlockedWithPrimitive(
                request,
                "debug_enter_ready_incubator_house",
                requested,
                IncubatorHatchObservedEffect(),
                reasons.Concat(new[]
                {
                    "incubator_house_entry_fixture_request_invalid"
                }).ToArray());
        }

        if (Game1.getLocationFromName(request.LocationId) is
                not AnimalHouse house ||
            house.warps.Count == 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_enter_ready_incubator_house",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_house_entry_fixture_location_unavailable");
        }

        var target = new Vector2(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        if (!house.objects.TryGetValue(
                target,
                out var incubator) ||
            incubator.GetMachineData()?.IsIncubator != true ||
            incubator.heldObject.Value is null ||
            incubator.MinutesUntilReady > 0 ||
            house.isFull())
        {
            return BlockedWithPrimitive(
                request,
                "debug_enter_ready_incubator_house",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_house_entry_requires_ready_hatch_and_capacity");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var entry = house.warps[0];
        Game1.activeClickableMenu = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.warpFarmer(
            house.NameOrUniqueName,
            entry.X,
            entry.Y - 1,
            false);
        var verified =
            house.objects.TryGetValue(
                target,
                out var observed) &&
            ReferenceEquals(observed, incubator) &&
            incubator.heldObject.Value is not null &&
            incubator.MinutesUntilReady <= 0;
        RefreshTransparentMachineProbeCache();
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind =
                "debug_enter_ready_incubator_house",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Game1_warpFarmer_animal_house_entry_requested",
                    "AnimalHouse_resetForPlayerEntry_path_requested",
                    "ready_incubator_preserved_for_native_selection"
                }
                : new[]
                {
                    "incubator_house_entry_fixture_post_state_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = IncubatorHatchObservedEffect(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "incubator_house_entry_fixture_post_state_mismatch"
                },
            ChangedFacts = Array.Empty<SimulatedFactChange>()
        };
    }
}
