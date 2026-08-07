using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPetCareTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.ExpectedFriendshipBefore.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_pet_care_target", "farm.pet_care_fixture=ready", "request=missing_fixture_projection", "pet_fixture_target_and_friendship_required");
        }

        var farm = Game1.getFarm();
        var bowl = farm.buildings.OfType<PetBowl>().FirstOrDefault(candidate => candidate.GetType() == typeof(PetBowl));
        if (bowl is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_pet_care_target", "farm.pet_care_fixture=ready", "pet_bowl=missing", "vanilla_pet_bowl_required");
        }

        foreach (var location in Game1.locations.Append(farm).Distinct())
        {
            foreach (var existingPet in location.characters.OfType<Pet>().ToArray())
            {
                location.characters.Remove(existingPet);
            }
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var pet = new Pet(target.X, target.Y, "0", "Dog")
        {
            currentLocation = farm,
            Name = "EvdPet"
        };
        pet.displayName = pet.Name;
        pet.Position = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
        pet.friendshipTowardFarmer.Value = Math.Clamp(request.ExpectedFriendshipBefore.Value, 0, Pet.maxFriendship);
        pet.grantedFriendshipForPet.Value = false;
        pet.lastPetDay.Remove(Game1.player.UniqueMultiplayerID);
        pet.CurrentBehavior = "SitDown";
        pet.Halt();

        var desiredGiftTrigger = request.PetGiftTriggerExpected ?? false;
        var data = pet.GetPetData();
        var timesPet = FindPetFixtureTimesPet(pet, data?.GiftChance ?? 0d, desiredGiftTrigger);
        if (!timesPet.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_pet_care_target", "farm.pet_care_fixture=ready", "gift_trigger=unreachable", "pet_fixture_gift_trigger_unreachable");
        }
        pet.timesPet.Value = timesPet.Value;
        farm.characters.Add(pet);
        bowl.AssignPet(pet);
        bowl.watered.Value = false;

        foreach (var farmer in Game1.getAllFarmers())
        {
            farmer.mailReceived.Remove("petLoveMessage");
            farmer.mailbox.Remove("petLoveMessage");
            farmer.mailForTomorrow.Remove("petLoveMessage");
            farmer.mailReceived.Remove("MarniePetAdoption");
            farmer.mailbox.Remove("MarniePetAdoption");
            farmer.mailForTomorrow.Remove("MarniePetAdoption");
            farmer.mailForTomorrow.Remove("MarniePetAdoption%&NL&%");
            if (pet.friendshipTowardFarmer.Value >= Pet.maxFriendship)
            {
                farmer.mailReceived.Add("petLoveMessage");
                farmer.mailForTomorrow.Add("MarniePetAdoption");
            }
        }

        var canSlot = EnsureFixtureTool(new WateringCan());
        var can = canSlot >= 0 ? Game1.player.Items[canSlot] as WateringCan : null;
        if (can is not null)
        {
            can.WaterLeft = Math.Max(can.WaterLeft, 40);
        }

        var bowlActionTile = FindPetBowlFixtureActionTile(bowl);
        var targetForPlayer = string.Equals(request.InteractionKind, "pet_bowl", StringComparison.Ordinal)
            ? bowlActionTile
            : target;
        var stand = Point.Zero;
        var moveReason = "pet_fixture_target_missing";
        var moved = targetForPlayer.HasValue && MoveFixtureFarmerToFarmAdjacent(targetForPlayer.Value, out stand, out moveReason);
        var actualGiftTrigger = Utility.CreateDaySaveRandom(pet.timesPet.Value, 71928.0, pet.petId.Value.GetHashCode()).NextDouble() < (data?.GiftChance ?? 0d);
        var verified = data is not null && moved && bowlActionTile.HasValue && can?.GetType() == typeof(WateringCan) &&
            pet.GetType() == typeof(Pet) && pet.currentLocation == farm && bowl.petId.Value == pet.petId.Value &&
            !bowl.watered.Value && actualGiftTrigger == desiredGiftTrigger;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_pet_care_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_vanilla_pet_and_bowl_fixture_ready", "pet_id=" + pet.petId.Value.ToString("D"), "gift_trigger=" + actualGiftTrigger.ToString().ToLowerInvariant(), "stand_tile=" + stand.X + "," + stand.Y }
                : new[] { "pet_fixture_not_ready", moveReason },
            RequestedEffect = "farm.pet_care_fixture=ready",
            ObservedEffect = PetObservedEffect(pet) + ";bowl_action_tile=" + (bowlActionTile?.X.ToString() ?? "missing") + "," + (bowlActionTile?.Y.ToString() ?? "missing") + ";watering_can_slot=" + canSlot,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "pet_fixture_not_ready:" + moveReason }
        };
    }

    private TrainingExecutionResult ExecutePreparePetBowlSleep(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var home = Utility.getHomeOfFarmer(Game1.player);
        Game1.currentLocation = home;
        Game1.player.currentLocation = home;
        var target = ResolveHomeSleepTarget(Game1.player.TilePoint, out var targetReason);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_prepare_pet_bowl_sleep", "player.at_sleep_stand=true", SleepObservedEffect(), targetReason);
        }

        Game1.player.Position = new Vector2(target.StandTile.X * Game1.tileSize, target.StandTile.Y * Game1.tileSize);
        Game1.player.faceDirection(DirectionTo(target.StandTile, target.BedTile));
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_prepare_pet_bowl_sleep",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "isolated_fixture_farmer_moved_to_native_sleep_stand" },
            RequestedEffect = "player.at_sleep_stand=true",
            ObservedEffect = SleepObservedEffect()
        };
    }

    private static int? FindPetFixtureTimesPet(Pet pet, double giftChance, bool desiredTrigger)
    {
        for (var timesPet = 0; timesPet < 10000; timesPet++)
        {
            var trigger = Utility.CreateDaySaveRandom(timesPet, 71928.0, pet.petId.Value.GetHashCode()).NextDouble() < giftChance;
            if (trigger == desiredTrigger)
            {
                return timesPet;
            }
        }
        return null;
    }

    private static Point? FindPetBowlFixtureActionTile(PetBowl bowl)
    {
        for (var y = bowl.tileY.Value; y < bowl.tileY.Value + bowl.tilesHigh.Value; y++)
        {
            for (var x = bowl.tileX.Value; x < bowl.tileX.Value + bowl.tilesWide.Value; x++)
            {
                string propertyValue = null!;
                if (bowl.doesTileHaveProperty(x, y, "PetBowl", "Buildings", ref propertyValue))
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }
}
