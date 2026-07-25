using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;
using System.Reflection;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMachinePlacementTarget(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_placement_target",
                "player.inventory.machine_available=true",
                "target_tile=missing",
                "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        var qualifiedItemId = string.IsNullOrWhiteSpace(
            request.QualifiedItemId)
                ? "(BC)12"
                : request.QualifiedItemId;
        farm.objects.Remove(targetVector);
        farm.terrainFeatures.Remove(targetVector);
        var slotIndex = EnsureInventoryItem(qualifiedItemId, 1);
        var moved = MoveFixtureFarmerToFarmAdjacent(
            target,
            out var stand,
            out var moveReason);
        var machine = slotIndex >= 0 &&
            slotIndex < Game1.player.Items.Count
                ? Game1.player.Items[slotIndex] as StardewValley.Object
                : null;
        var nativeLegal = machine is not null &&
            machine.bigCraftable.Value &&
            machine.GetMachineData() is not null &&
            Utility.playerCanPlaceItemHere(
                farm,
                machine,
                target.X * Game1.tileSize,
                target.Y * Game1.tileSize,
                Game1.player);
        var verified = slotIndex >= 0 &&
            moved &&
            nativeLegal &&
            Game1.currentLocation == farm &&
            Game1.player.TilePoint == stand;

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
            PrimitiveKind = "debug_setup_machine_placement_target",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_inventory_machine_available",
                    "target_tile_cleared",
                    "player_moved_adjacent",
                    "Utility.playerCanPlaceItemHere=true",
                    "inventory_slot_index=" + slotIndex,
                    "stand_tile=" + stand.X + "," + stand.Y
                }
                : new[]
                {
                    slotIndex >= 0
                        ? "inventory_machine_available"
                        : "inventory_machine_unavailable",
                    moved ? "player_moved_adjacent" : moveReason,
                    nativeLegal
                        ? "native_placement_legal"
                        : "native_placement_illegal"
                },
            RequestedEffect = "player.inventory.machine_available=true" +
                ";location_id=Farm;target_tile=" + target.X + "," +
                target.Y,
            ObservedEffect = "location_id=" +
                (Game1.currentLocation?.NameOrUniqueName ?? "null") +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slotIndex +
                ";qualified_item_id=" +
                (machine?.QualifiedItemId ?? "null") +
                ";native_placement_legal=" +
                nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "machine_placement_fixture_not_ready" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slotIndex + "]",
                        Before = "unknown",
                        After = qualifiedItemId + ":1"
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.tile",
                        Before = "unknown",
                        After = stand.X + "," + stand.Y
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecutePlaceMachine(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested = "farm.machines[" + request.LocationId + ":" +
            request.TargetTileX + "," + request.TargetTileY +
            "].qualified_item_id=" + request.QualifiedItemId +
            ";player.inventory[" + request.InventorySlotIndex +
            "].stack_decreases=1";
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "typed_target=missing",
                "place_machine_typed_target_fields_required");
        }

        var location = Game1.currentLocation;
        if (location is null ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(
                location.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "location_id=" +
                (location?.NameOrUniqueName ?? "unavailable"),
                "place_machine_location_mismatch");
        }

        var slotIndex = request.InventorySlotIndex.Value;
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_slot=" + slotIndex,
                "place_machine_inventory_slot_out_of_range");
        }
        if (Game1.player.Items[slotIndex] is not StardewValley.Object machine ||
            !machine.bigCraftable.Value ||
            machine.GetMachineData() is null)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_slot_item=not_machine",
                "place_machine_inventory_slot_not_machine");
        }
        if (!string.Equals(
                machine.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(request.ItemId) &&
             !string.Equals(
                 machine.ItemId,
                 request.ItemId,
                 StringComparison.Ordinal)))
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_item=" + machine.QualifiedItemId,
                "place_machine_inventory_identity_mismatch");
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) +
                Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "player_tile=" + playerTile.X + "," + playerTile.Y,
                "place_machine_player_not_adjacent");
        }

        var targetVector = new Vector2(target.X, target.Y);
        var pixelX = target.X * Game1.tileSize;
        var pixelY = target.Y * Game1.tileSize;
        if (location.objects.ContainsKey(targetVector) ||
            !Utility.playerCanPlaceItemHere(
                location,
                machine,
                pixelX,
                pixelY,
                Game1.player))
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "native_placement_recheck=false",
                "place_machine_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var selectedSlotBefore = Game1.player.CurrentToolIndex;
        var stackBefore = machine.Stack;
        Game1.player.CurrentToolIndex = slotIndex;
        var placed = Utility.tryToPlaceItem(
            location,
            machine,
            pixelX,
            pixelY);
        if (selectedSlotBefore >= 0 &&
            selectedSlotBefore < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = selectedSlotBefore;
        }

        location.objects.TryGetValue(
            targetVector,
            out var placedObject);
        var afterSlot = slotIndex < Game1.player.Items.Count
            ? Game1.player.Items[slotIndex]
            : null;
        var stackAfter = afterSlot?.Stack ?? 0;
        var inventoryConsumed = stackAfter == stackBefore - 1;
        var placedIdentityMatches = placedObject is not null &&
            string.Equals(
                placedObject.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase);
        var verified = placed &&
            placedIdentityMatches &&
            inventoryConsumed;

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
            PrimitiveKind = "place_machine",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "Utility.playerCanPlaceItemHere_rechecked",
                    "Utility.tryToPlaceItem_applied_native_callbacks",
                    "placed_machine_identity_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    placed
                        ? "native_place_returned_true"
                        : "native_place_returned_false",
                    placedIdentityMatches
                        ? "placed_identity_matches"
                        : "placed_identity_missing_or_mismatched",
                    inventoryConsumed
                        ? "inventory_consumed_one"
                        : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" +
                location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_qualified_item_id=" +
                (placedObject?.QualifiedItemId ?? "null") +
                ";inventory_stack_before=" + stackBefore +
                ";inventory_stack_after=" + stackAfter,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "place_machine_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" +
                            location.NameOrUniqueName + ":" +
                            target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slotIndex + "].stack",
                        Before = stackBefore.ToString(),
                        After = stackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteRemoveMachine(
        TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Pickaxe.DoFunction_to_Object.performToolAction_then_performRemoveAction_and_exact_machine_debris";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested = "farm.machines[" + request.LocationId +
            ":" + request.TargetTileX + "," +
            request.TargetTileY + "]=missing;location.debris[" +
            request.QualifiedItemId + "].count_increases=1";
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue ||
            !request.StandTileY.HasValue ||
            !request.ToolSlotIndex.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            string.IsNullOrWhiteSpace(request.RelocationIntentId) ||
            string.IsNullOrWhiteSpace(
                request.MachineRemovalProjectionFingerprint))
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "typed_target_or_intent=missing",
                "remove_machine_typed_target_and_intent_fields_required");
        }
        if (!string.Equals(
                request.NativeContract,
                nativeContract,
                StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "native_contract=" + request.NativeContract,
                "remove_machine_native_contract_mismatch");
        }
        if (Game1.activeClickableMenu is not null ||
            Game1.dialogueUp ||
            Game1.player.UsingTool ||
            !Game1.player.CanMove)
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "player=busy_or_menu_open",
                "remove_machine_tool_or_menu_conflict");
        }

        var location = Game1.currentLocation;
        if (location is null ||
            !string.Equals(
                location.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "location=" +
                (location?.NameOrUniqueName ?? "unavailable"),
                "remove_machine_location_mismatch");
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var stand = new Point(
            request.StandTileX.Value,
            request.StandTileY.Value);
        if (Game1.player.TilePoint != stand ||
            Math.Abs(stand.X - target.X) +
                Math.Abs(stand.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "player_tile=" + Game1.player.TilePoint.X + "," +
                Game1.player.TilePoint.Y,
                "remove_machine_player_not_at_adjacent_stand");
        }

        var targetVector = new Vector2(target.X, target.Y);
        if (!location.objects.TryGetValue(
                targetVector,
                out var machine) ||
            !string.Equals(
                machine.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "machine=missing_or_identity_mismatch",
                "remove_machine_target_identity_mismatch");
        }

        var runtimeBlockReasons = MachineRemovalRuntimeBlockReasons(
            machine,
            Game1.player);
        if (runtimeBlockReasons.Length > 0)
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                MachineObservedEffect(location, target),
                runtimeBlockReasons);
        }

        var toolSlot = request.ToolSlotIndex.Value;
        if (toolSlot < 0 ||
            toolSlot >= Game1.player.Items.Count ||
            Game1.player.Items[toolSlot] is not Pickaxe pickaxe ||
            !string.Equals(
                pickaxe.QualifiedItemId,
                request.ToolQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "remove_machine",
                requested,
                "tool_slot=" + toolSlot,
                "remove_machine_pickaxe_identity_mismatch");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var debrisBefore = CountDebrisByQualifiedItemId(
            location,
            request.QualifiedItemId);
        var energyBefore = Game1.player.Stamina;
        var selectedSlotBefore = Game1.player.CurrentToolIndex;
        Game1.player.CurrentToolIndex = toolSlot;
        pickaxe.DoFunction(
            location,
            target.X * Game1.tileSize,
            target.Y * Game1.tileSize,
            0,
            Game1.player);
        if (selectedSlotBefore >= 0 &&
            selectedSlotBefore < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = selectedSlotBefore;
        }

        var sourceRemoved =
            !location.objects.ContainsKey(targetVector);
        var debrisAfter = CountDebrisByQualifiedItemId(
            location,
            request.QualifiedItemId);
        var debrisCreated = debrisAfter == debrisBefore + 1;
        var verified = sourceRemoved && debrisCreated;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "remove_machine",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "Pickaxe.DoFunction_invoked",
                    "Object.performToolAction_returned_removal_path",
                    "performRemoveAction_native_callbacks_applied",
                    "source_machine_removed",
                    "exact_machine_debris_created"
                }
                : new[]
                {
                    sourceRemoved
                        ? "source_machine_removed"
                        : "source_machine_still_present",
                    debrisCreated
                        ? "exact_machine_debris_created"
                        : "exact_machine_debris_delta_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect =
                "source_present=" + (!sourceRemoved)
                    .ToString().ToLowerInvariant() +
                ";debris_before=" + debrisBefore +
                ";debris_after=" + debrisAfter +
                ";qualified_item_id=" +
                request.QualifiedItemId +
                ";relocation_intent_id=" +
                request.RelocationIntentId,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "remove_machine_post_state_mismatch" },
            ToolQualifiedItemId = pickaxe.QualifiedItemId,
            ToolUpgradeLevel = pickaxe.UpgradeLevel,
            EnergyBefore = energyBefore,
            EnergyAfter = Game1.player.Stamina,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" +
                            location.NameOrUniqueName + ":" +
                            target.X + "," + target.Y + "]",
                        Before = request.QualifiedItemId,
                        After = "missing"
                    },
                    new SimulatedFactChange
                    {
                        Path = "location.debris[" +
                            request.QualifiedItemId + "].count",
                        Before = debrisBefore.ToString(),
                        After = debrisAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string[] MachineRemovalRuntimeBlockReasons(
        StardewValley.Object machine,
        Farmer player)
    {
        var reasons = new List<string>();
        var toolActionDeclaringType = machine.GetType()
            .GetMethod(
                nameof(StardewValley.Object.performToolAction),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Tool) },
                modifiers: null)
            ?.DeclaringType?.FullName ?? string.Empty;
        if (machine.owner.Value != 0 &&
            machine.owner.Value != player.UniqueMultiplayerID)
        {
            reasons.Add("remove_machine_owner_mismatch");
        }
        if (!machine.bigCraftable.Value ||
            machine.GetMachineData() is null ||
            !string.Equals(
                machine.Type,
                "Crafting",
                StringComparison.Ordinal))
        {
            reasons.Add("remove_machine_not_native_crafting_machine");
        }
        if (machine.Fragility != 0)
        {
            reasons.Add("remove_machine_fragility_not_recoverable");
        }
        if (machine.MinutesUntilReady > 0)
        {
            reasons.Add("remove_machine_processing");
        }
        if (machine.readyForHarvest.Value)
        {
            reasons.Add("remove_machine_output_ready");
        }
        if (machine.heldObject.Value is not null)
        {
            reasons.Add(
                "remove_machine_held_item_or_attachment_present");
        }
        if (machine.isTemporarilyInvisible)
        {
            reasons.Add("remove_machine_temporarily_invisible");
        }
        if (!string.Equals(
                toolActionDeclaringType,
                typeof(StardewValley.Object).FullName,
                StringComparison.Ordinal) &&
            !string.Equals(
                toolActionDeclaringType,
                typeof(Cask).FullName,
                StringComparison.Ordinal))
        {
            reasons.Add(
                "remove_machine_runtime_tool_override_not_verified");
        }
        return reasons.ToArray();
    }

    private static int CountDebrisByQualifiedItemId(
        GameLocation location,
        string qualifiedItemId) =>
        location.debris.Count(debris =>
            string.Equals(
                DebrisQualifiedItemId(debris),
                qualifiedItemId,
                StringComparison.OrdinalIgnoreCase));
}
