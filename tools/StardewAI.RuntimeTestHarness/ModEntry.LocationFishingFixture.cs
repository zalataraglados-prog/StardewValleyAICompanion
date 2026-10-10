using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupLocationFishing(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_location_fishing",
                "location_fishing.fixture=ready",
                "location_fishing.fixture=blocked",
                reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.LocationId) ||
            Game1.getLocationFromName(request.LocationId) is not GameLocation location ||
            !location.canFishHere() ||
            !TryFindLocationFishingStand(location, out var stand, out var bobber))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_location_fishing",
                "location_fishing.fixture=ready",
                "location_fishing.fixture=blocked",
                "location_fishing_fixture_location_or_cast_unavailable");
        }

        var player = Game1.player;
        var initializedTutorialPrerequisite = false;
        if (player.fishCaught.Length == 0)
        {
            player.fishCaught.Add("(O)145", new[] { 1, 1 });
            initializedTutorialPrerequisite = true;
        }
        var rod = player.Items.OfType<FishingRod>()
            .OrderByDescending(value => value.UpgradeLevel)
            .FirstOrDefault();
        if (rod is null)
        {
            var slot = FirstEmptyInventorySlot(player);
            if (slot < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_location_fishing",
                    "location_fishing.fixture=ready",
                    "location_fishing.fixture=blocked",
                    "location_fishing_fixture_inventory_slot_unavailable");
            }
            rod = new FishingRod(4);
            player.Items[slot] = rod;
        }

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        StopAllMovement();
        Game1.currentLocation = location;
        player.currentLocation = location;
        player.Position = stand.ToVector2() * Game1.tileSize;
        player.CurrentToolIndex = player.Items.IndexOf(rod);
        player.experiencePoints[Farmer.fishingSkill] = Math.Max(
            player.experiencePoints[Farmer.fishingSkill],
            15000);
        player.fishingLevel.Value = Math.Max(player.fishingLevel.Value, 10);
        player.Stamina = Math.Max(player.Stamina, 200f);
        player.forceCanMove();

        var verified = ReferenceEquals(Game1.currentLocation, location) &&
            ReferenceEquals(player.currentLocation, location) &&
            player.TilePoint == stand &&
            player.CurrentTool is FishingRod &&
            player.fishCaught.Length > 0 &&
            location.isTileFishable(bobber.X, bobber.Y);
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
            PrimitiveKind = "debug_setup_location_fishing",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_equipped_rod_without_injecting_catch_output",
                    "fixture_satisfied_native_tutorial_gate_with_non_target_prior_catch",
                    "fixture_selected_native_fishable_tile",
                    "fixture_transition_is_not_training_eligible"
                }
                : new[] { "location_fishing_fixture_post_state_mismatch" },
            RequestedEffect = "location_fishing.fixture=ready;location=" +
                request.LocationId,
            ObservedEffect = "location=" + location.NameOrUniqueName +
                ";stand=" + stand.X + "," + stand.Y +
                ";bobber=" + bobber.X + "," + bobber.Y +
                ";rod_slot=" + player.CurrentToolIndex +
                ";initialized_tutorial_prerequisite=" +
                initializedTutorialPrerequisite,
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = stand.X,
            TargetTileY = stand.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "location_fishing_fixture_post_state_mismatch" }
        };
    }

    private static bool TryFindLocationFishingStand(
        GameLocation location,
        out Point stand,
        out Point bobber)
    {
        stand = default;
        bobber = default;
        if (location.Map?.Layers.Count is not > 0)
            return false;

        var width = location.Map.Layers[0].LayerWidth;
        var height = location.Map.Layers[0].LayerHeight;
        var directions = new[]
        {
            new Point(0, -1),
            new Point(1, 0),
            new Point(0, 1),
            new Point(-1, 0)
        };
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var candidateStand = new Point(x, y);
                if (!IsTileWalkable(location, candidateStand) ||
                    IsTileOccupiedByCharacter(location, candidateStand))
                {
                    continue;
                }
                foreach (var direction in directions)
                {
                    for (var distance = 2; distance <= 5; distance++)
                    {
                        var candidateBobber = new Point(
                            x + direction.X * distance,
                            y + direction.Y * distance);
                        if (IsTileOnMap(location, candidateBobber) &&
                            location.isTileFishable(
                                candidateBobber.X,
                                candidateBobber.Y))
                        {
                            stand = candidateStand;
                            bobber = candidateBobber;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }
}
