using StardewAI.Contracts.Training;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult
        ExecuteSetupIncubatorHatchNaming(
            TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0 ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_incubator_hatch_naming",
                "incubator.ready=true;native_animalNaming_event=true",
                IncubatorHatchObservedEffect(),
                reasons.Concat(new[]
                {
                    "incubator_hatch_fixture_request_invalid"
                }).ToArray());
        }

        if (Game1.getLocationFromName(request.LocationId) is
            not AnimalHouse house)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_incubator_hatch_naming",
                "incubator.ready=true;native_animalNaming_event=true",
                "location_id=" + request.LocationId,
                "incubator_hatch_fixture_requires_animal_house");
        }

        var target = new Vector2(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var eggId = string.IsNullOrWhiteSpace(
                request.QualifiedItemId)
            ? "(O)176"
            : request.QualifiedItemId;
        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.currentLocation = house;
        Game1.player.currentLocation = house;
        Game1.activeClickableMenu = null;
        house.currentEvent = null;
        Game1.eventUp = false;
        Game1.eventOver = false;

        long? removedAnimalId = null;
        if (house.isFull() &&
            house.animalsThatLiveHere.Count > 0)
        {
            removedAnimalId =
                house.animalsThatLiveHere[^1];
            house.animalsThatLiveHere.RemoveAt(
                house.animalsThatLiveHere.Count - 1);
            house.animals.Remove(removedAnimalId.Value);
        }

        house.objects.Remove(target);
        var incubator = ItemRegistry.Create<
            StardewValley.Object>("(BC)101");
        house.objects[target] = incubator;
        incubator.heldObject.Value =
            ItemRegistry.Create<StardewValley.Object>(eggId);
        incubator.MinutesUntilReady = 0;
        incubator.readyForHarvest.Value = true;

        if (!FarmAnimal.TryGetAnimalDataFromEgg(
                incubator.heldObject.Value,
                house,
                out var animalTypeId,
                out _))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_incubator_hatch_naming",
                "incubator.ready=true;native_animalNaming_event=true",
                IncubatorHatchObservedEffect(),
                "incubator_hatch_fixture_egg_not_supported");
        }

        house.startEvent(new StardewValley.Event(
            "none/-1000 -1000/farmer 2 9 0/animalNaming/pause 1/end"));
        RefreshTransparentMachineProbeCache();
        var verified =
            house.currentEvent is not null &&
            Game1.eventUp &&
            !house.isFull() &&
            house.objects.TryGetValue(
                target,
                out var observed) &&
            ReferenceEquals(observed, incubator) &&
            incubator.heldObject.Value is not null &&
            incubator.MinutesUntilReady <= 0;
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
            CompletedAt =
                DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind =
                "debug_setup_incubator_hatch_naming",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_fixture_ready_incubator_created",
                    "native_animalNaming_event_started",
                    "location_id=" + request.LocationId,
                    "target_tile=" +
                        request.TargetTileX.Value +
                        "," +
                        request.TargetTileY.Value,
                    "egg_qualified_item_id=" + eggId,
                    "target_runtime_type=" + animalTypeId,
                    "removed_fixture_animal_id=" +
                        (removedAnimalId?.ToString() ?? "none")
                }
                : new[]
                {
                    "incubator_hatch_fixture_post_state_mismatch"
                },
            RequestedEffect =
                "incubator.ready=true;native_animalNaming_event=true",
            ObservedEffect = IncubatorHatchObservedEffect(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "incubator_hatch_fixture_post_state_mismatch"
                },
            ChangedFacts = Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteNameHatchedAnimal(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var requested =
            "animal_house.animals.count+=1;name=" +
            request.TargetName +
            ";type=" +
            request.TargetRuntimeType;
        if (reasons.Count > 0 ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.TargetName) ||
            request.TargetName.Length > 32 ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType))
        {
            return BlockedWithPrimitive(
                request,
                "name_hatched_animal",
                requested,
                IncubatorHatchObservedEffect(),
                reasons.Concat(new[]
                {
                    "incubator_hatch_request_invalid"
                }).ToArray());
        }

        if (Game1.currentLocation is not AnimalHouse house ||
            Game1.activeClickableMenu is not NamingMenu menu ||
            menu.doneNamingButton is null ||
            menu.textBox is null ||
            menu.doneNaming is null)
        {
            return BlockedWithPrimitive(
                request,
                "name_hatched_animal",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_native_naming_menu_not_open");
        }

        var nativeReadyIncubator = house.objects.Pairs
            .FirstOrDefault(pair =>
                pair.Value.bigCraftable.Value &&
                pair.Value.GetMachineData()?.IsIncubator == true &&
                pair.Value.heldObject.Value is not null &&
                pair.Value.MinutesUntilReady <= 0);
        var requestedTile = new Microsoft.Xna.Framework.Vector2(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var readyIncubator =
            nativeReadyIncubator.Value is not null &&
            nativeReadyIncubator.Key == requestedTile
                ? nativeReadyIncubator.Value
                : null;
        if (readyIncubator is null || house.isFull())
        {
            return BlockedWithPrimitive(
                request,
                "name_hatched_animal",
                requested,
                IncubatorHatchObservedEffect(),
                nativeReadyIncubator.Value is null
                    ? "incubator_ready_egg_not_found"
                    : readyIncubator is null
                        ? "incubator_native_ready_selection_drifted"
                        : "incubator_animal_house_full");
        }

        if (!FarmAnimal.TryGetAnimalDataFromEgg(
                readyIncubator.heldObject.Value,
                house,
                out var animalTypeId,
                out _) ||
            !string.Equals(
                animalTypeId,
                request.TargetRuntimeType,
                StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "name_hatched_animal",
                requested,
                IncubatorHatchObservedEffect(),
                "incubator_hatch_animal_type_drifted");
        }

        var beforeIds = house.animals.Keys.ToHashSet();
        var beforeCount = house.animalsThatLiveHere.Count;
        var beforeEggId =
            readyIncubator.heldObject.Value?.QualifiedItemId ??
            string.Empty;
        var started =
            DateTimeOffset.UtcNow.ToString("O");

        menu.textBox.Text = request.TargetName;
        var button = menu.doneNamingButton.bounds;
        menu.receiveLeftClick(
            button.Center.X,
            button.Center.Y);

        var newAnimals = house.animals.Values
            .Where(animal =>
                !beforeIds.Contains(animal.myID.Value))
            .ToArray();
        var created = newAnimals.Length == 1
            ? newAnimals[0]
            : null;
        var verified =
            created is not null &&
            house.animalsThatLiveHere.Count ==
                beforeCount + 1 &&
            string.Equals(
                created.Name,
                request.TargetName,
                StringComparison.Ordinal) &&
            string.Equals(
                created.type.Value,
                request.TargetRuntimeType,
                StringComparison.Ordinal) &&
            readyIncubator.heldObject.Value is null &&
            Game1.activeClickableMenu is null;

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
            PrimitiveKind = "name_hatched_animal",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_naming_menu_confirmed",
                    "animal_house_count_incremented",
                    "exact_hatched_animal_observed",
                    "incubator_egg_cleared",
                    "egg_qualified_item_id=" + beforeEggId
                }
                : new[]
                {
                    "incubator_hatch_post_state_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = IncubatorHatchObservedEffect(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "incubator_hatch_post_state_mismatch"
                },
            ChangedFacts = Array.Empty<SimulatedFactChange>()
        };
    }

    private static string IncubatorHatchObservedEffect()
    {
        if (Game1.currentLocation is not AnimalHouse house)
        {
            return "location=not_animal_house";
        }

        var animals = string.Join(
            ",",
            house.animals.Values
                .OrderBy(animal => animal.myID.Value)
                .Select(animal =>
                    animal.myID.Value +
                    ":" +
                    animal.type.Value +
                    ":" +
                    animal.Name));
        var readyEggs = house.objects.Values.Count(machine =>
            machine.GetMachineData()?.IsIncubator == true &&
            machine.heldObject.Value is not null &&
            machine.MinutesUntilReady <= 0);
        return
            "location_id=" +
            house.NameOrUniqueName +
            ";occupants=" +
            house.animalsThatLiveHere.Count +
            "/" +
            house.animalLimit.Value +
            ";ready_incubator_count=" +
            readyEggs +
            ";menu=" +
            (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";animals=" +
            animals;
    }
}
