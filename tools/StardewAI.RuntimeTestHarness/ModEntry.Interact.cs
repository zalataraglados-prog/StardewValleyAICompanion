using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private TrainingExecutionResult ExecuteInteract(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_target_tile_required");
        }

        if (!string.Equals(request.InteractionKind, "map_action", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_kind_unsupported");
        }

        if (!IsInteractActionTypeWhitelisted(request.ExpectedActionType))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_expected_action_type_not_whitelisted");
        }

        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_target_not_adjacent");
        }

        if (Game1.activeClickableMenu is not null)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_menu_must_be_clear");
        }

        var rawAction = Game1.currentLocation.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings");
        if (string.IsNullOrWhiteSpace(rawAction))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_action_property_missing");
        }

        var actionType = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!string.Equals(actionType, request.ExpectedActionType, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_expected_action_type_mismatch");
        }

        var isGoldenScytheAction = string.Equals(actionType, "GoldenScythe", StringComparison.OrdinalIgnoreCase);
        var goldenScytheClaimedBefore = isGoldenScytheAction && Game1.player.mailReceived.Contains("gotGoldenScythe");
        var goldenScytheCountBefore = isGoldenScytheAction ? CountInventoryItems("(W)53") : 0;
        if (isGoldenScytheAction && goldenScytheClaimedBefore)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "golden_scythe_already_claimed_use_native_mine_exit");
        }
        if (isGoldenScytheAction && !goldenScytheClaimedBefore && Game1.player.isInventoryFull())
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "golden_scythe_inventory_full");
        }

        var beforeMenuOpen = Game1.activeClickableMenu is not null;
        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeLocation = Game1.currentLocation.NameOrUniqueName;
        var beforeTile = Game1.player.TilePoint;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        var afterMenuOpen = Game1.activeClickableMenu is not null;
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterLocation = Game1.currentLocation.NameOrUniqueName;
        var afterTile = Game1.player.TilePoint;
        var goldenScytheClaimedAfter = isGoldenScytheAction && Game1.player.mailReceived.Contains("gotGoldenScythe");
        var goldenScytheCountAfter = isGoldenScytheAction ? CountInventoryItems("(W)53") : 0;
        var verified = handled && (afterMenuOpen != beforeMenuOpen ||
            !string.Equals(afterMenuType, beforeMenuType, StringComparison.Ordinal) ||
            !string.Equals(afterLocation, beforeLocation, StringComparison.Ordinal) ||
            afterTile != beforeTile);
        string[] verificationReasons;
        if (isGoldenScytheAction && !goldenScytheClaimedBefore)
        {
            verified = handled && goldenScytheClaimedAfter && goldenScytheCountAfter > goldenScytheCountBefore;
            verificationReasons = verified
                ? new[] { "golden_scythe_native_action_handled", "gotGoldenScythe_mail_received", "golden_scythe_inventory_increased" }
                : new[] { handled ? "golden_scythe_native_claim_not_observed" : "map_action_not_handled" };
        }
        else
        {
            verificationReasons = verified
                ? new[] { "map_action_handled", "observable_state_changed" }
                : new[] { handled ? "map_action_handled_without_observable_change" : "map_action_not_handled" };
        }

        var changedFacts = new List<SimulatedFactChange>
        {
            new() { Path = "menus.active_menu.is_open", Before = beforeMenuOpen.ToString().ToLowerInvariant(), After = afterMenuOpen.ToString().ToLowerInvariant() },
            new() { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType },
            new() { Path = "player.location_id", Before = beforeLocation, After = afterLocation },
            new() { Path = "player.tile", Before = beforeTile.X + "," + beforeTile.Y, After = afterTile.X + "," + afterTile.Y }
        };
        if (isGoldenScytheAction)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.mail_received.gotGoldenScythe",
                Before = goldenScytheClaimedBefore.ToString().ToLowerInvariant(),
                After = goldenScytheClaimedAfter.ToString().ToLowerInvariant()
            });
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.inventory.(W)53.count",
                Before = goldenScytheCountBefore.ToString(),
                After = goldenScytheCountAfter.ToString()
            });
        }

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
            PrimitiveKind = "interact",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request),
            ObservedEffect = InteractObservedEffect() +
                (isGoldenScytheAction
                    ? ";gotGoldenScythe=" + goldenScytheClaimedAfter.ToString().ToLowerInvariant() + ";golden_scythe_count=" + goldenScytheCountAfter
                    : string.Empty),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = changedFacts.ToArray()
        };
    }

    private static bool IsInteractActionTypeWhitelisted(string actionType)
    {
        return actionType is "OpenShop" or "Buy" or "JojaShop" or "Blacksmith" or "Carpenter" or "AnimalShop" or "AdventureShop" or "GoldenScythe" or "Arcade_Minecart" or "Billboard";
    }
}
