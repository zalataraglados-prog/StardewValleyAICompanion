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

        if (string.Equals(request.InteractionKind, "animal_door", StringComparison.Ordinal))
        {
            return ExecuteAnimalDoorInteract(request, target);
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

        var requestedMailbox = string.Equals(request.ExpectedActionType, "Mailbox", StringComparison.OrdinalIgnoreCase);
        if (requestedMailbox && (Game1.currentLocation is not Farm || Game1.player.getMailboxPosition() != target))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "mailbox_native_target_mismatch");
        }
        var requestedMineElevator = string.Equals(request.ExpectedActionType, "MineElevator", StringComparison.OrdinalIgnoreCase);
        var rawAction = requestedMailbox
            ? "Mailbox"
            : requestedMineElevator && Game1.currentLocation is MineShaft mine && mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") == 112
                ? "MineElevator"
                : Game1.currentLocation.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings");
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
        var isMailboxAction = string.Equals(actionType, "Mailbox", StringComparison.OrdinalIgnoreCase);
        var isMineElevatorAction = string.Equals(actionType, "MineElevator", StringComparison.OrdinalIgnoreCase);
        var mailboxQueueBefore = isMailboxAction ? Game1.player.mailbox.ToArray() : Array.Empty<string>();
        var mailboxFirstBefore = mailboxQueueBefore.FirstOrDefault() ?? string.Empty;
        if (isMailboxAction && (mailboxQueueBefore.Length == 0 ||
            !string.Equals(request.TargetRuntimeIdentity, mailboxFirstBefore, StringComparison.Ordinal)))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "mailbox_first_identity_mismatch");
        }
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
        var mailboxQueueAfter = isMailboxAction ? Game1.player.mailbox.ToArray() : Array.Empty<string>();
        var openedLetter = Game1.activeClickableMenu as LetterViewerMenu;
        var openedMineElevator = Game1.activeClickableMenu as MineElevatorMenu;
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
        else if (isMailboxAction)
        {
            verified = handled &&
                mailboxQueueAfter.Length == mailboxQueueBefore.Length - 1 &&
                mailboxQueueBefore.Skip(1).SequenceEqual(mailboxQueueAfter, StringComparer.Ordinal) &&
                openedLetter is not null &&
                openedLetter.isMail &&
                !openedLetter.isFromCollection &&
                string.Equals(openedLetter.mailTitle, mailboxFirstBefore, StringComparison.Ordinal);
            verificationReasons = verified
                ? new[] { "mailbox_native_action_handled", "mailbox_first_removed", "exact_letter_viewer_opened" }
                : new[] { handled ? "mailbox_native_result_mismatch" : "map_action_not_handled" };
        }
        else if (isMineElevatorAction)
        {
            verified = handled && openedMineElevator is not null && openedMineElevator.elevators.Count > 1;
            verificationReasons = verified
                ? new[] { "mine_elevator_native_action_handled", "exact_mine_elevator_menu_opened" }
                : new[] { handled ? "mine_elevator_native_result_mismatch" : "map_action_not_handled" };
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
        if (isMailboxAction)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "quests.mailbox_processing.queue_count",
                Before = mailboxQueueBefore.Length.ToString(),
                After = mailboxQueueAfter.Length.ToString()
            });
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "quests.mailbox_processing.pending_mail_id",
                Before = mailboxFirstBefore,
                After = mailboxQueueAfter.FirstOrDefault() ?? string.Empty
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

    private TrainingExecutionResult ExecuteAnimalDoorInteract(
        TrainingExecutionRequest request,
        Point target)
    {
        const string nativeContract =
            "Building.doAction->didPlayerJustRightClick->Building.ToggleAnimalDoor->animalDoorOpen";
        var observed = "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";target=" + target.X + "," + target.Y;
        if (!string.Equals(request.ExpectedActionType, "AnimalDoor", StringComparison.Ordinal) ||
            request.ExpectedAnimalDoorOpenBefore != false ||
            request.ExpectedAnimalDoorOpenAfter != true ||
            !string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.BuildingType) ||
            !request.BuildingTileX.HasValue || !request.BuildingTileY.HasValue ||
            !request.SafeSlotIndex.HasValue)
        {
            return BlockedWithPrimitive(request, "interact", "building.animal_door.is_open=true", observed,
                "animal_door_typed_request_invalid");
        }
        var farm = Game1.getFarm();
        if (!ReferenceEquals(Game1.currentLocation, farm) ||
            !string.Equals(request.LocationId, farm.NameOrUniqueName, StringComparison.Ordinal) ||
            !AreAdjacent(Game1.player.TilePoint, target) ||
            Game1.activeClickableMenu is not null)
        {
            return BlockedWithPrimitive(request, "interact", "building.animal_door.is_open=true", observed,
                "animal_door_location_adjacency_or_menu_drifted");
        }
        var building = farm.buildings.SingleOrDefault(value =>
            string.Equals(value.buildingType.Value, request.BuildingType, StringComparison.Ordinal) &&
            value.tileX.Value == request.BuildingTileX &&
            value.tileY.Value == request.BuildingTileY);
        var data = building?.GetData();
        if (building is null || data is null || building.isUnderConstruction() ||
            data.AnimalDoor.X < 0 || data.AnimalDoor.Y < 0 ||
            !building.getRectForAnimalDoor(data).Contains(target.X * Game1.tileSize + Game1.tileSize / 2,
                target.Y * Game1.tileSize + Game1.tileSize / 2) ||
            building.animalDoorOpen.Value)
        {
            return BlockedWithPrimitive(request, "interact", "building.animal_door.is_open=true", observed,
                "animal_door_live_projection_drifted");
        }
        if (request.SafeSlotIndex < 0 || request.SafeSlotIndex >= Math.Min(12, Game1.player.Items.Count) ||
            Game1.player.Items[request.SafeSlotIndex.Value] is not null and not Tool)
        {
            return BlockedWithPrimitive(request, "interact", "building.animal_door.is_open=true", observed,
                "animal_door_safe_slot_drifted");
        }

        var restoreSlot = Game1.player.CurrentToolIndex;
        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.player.CurrentToolIndex = request.SafeSlotIndex.Value;
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        NativeRightClickEdgePatch.Arm();
        bool handled;
        bool rightClickObserved;
        try
        {
            handled = building.doAction(target.ToVector2(), Game1.player);
            rightClickObserved = NativeRightClickEdgePatch.WasObserved;
        }
        finally
        {
            NativeRightClickEdgePatch.Clear();
            Game1.player.CurrentToolIndex = restoreSlot;
        }
        var opened = building.animalDoorOpen.Value;
        var verified = handled && rightClickObserved && opened;
        var reasons = verified
            ? new[] { "native_Building_doAction_handled", "native_right_click_edge_observed", "animalDoorOpen_false_to_true_observed", "selected_toolbar_slot_restored" }
            : new[] { handled ? "animal_door_native_post_state_mismatch" : "animal_door_native_action_not_handled" };
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
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "building.animal_door.is_open=true",
            ObservedEffect = observed + ";handled=" + handled.ToString().ToLowerInvariant() +
                ";right_click_observed=" + rightClickObserved.ToString().ToLowerInvariant() +
                ";animal_door_open=" + opened.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : reasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "farm.buildings[" + request.BuildingType + ":" + request.BuildingTileX + "," + request.BuildingTileY + "].animal_door.is_open",
                    Before = "false",
                    After = opened.ToString().ToLowerInvariant()
                }
            }
        };
    }

    private static bool IsInteractActionTypeWhitelisted(string actionType)
    {
        return actionType is "OpenShop" or "Buy" or "JojaShop" or "Blacksmith" or "Carpenter" or "AnimalShop" or "AdventureShop" or "GoldenScythe" or "Arcade_Minecart" or "Billboard" or "SpecialOrders" or "QiChallengeBoard" or "DesertMarlon" or "Mailbox" or "MineElevator";
    }
}
