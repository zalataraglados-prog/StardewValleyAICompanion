using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult
        ExecuteSetupStorageCraftingTarget(
            TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var recipeName = string.IsNullOrWhiteSpace(
            request.RecipeName)
                ? "Chest"
                : request.RecipeName;
        if (!Game1.player.craftingRecipes.ContainsKey(recipeName) ||
            !CraftingRecipe.craftingRecipes.ContainsKey(recipeName))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_storage_crafting_target",
                "player.storage_crafting.recipe_ready=true",
                "recipe=" + recipeName,
                "storage_crafting_fixture_requires_learned_recipe");
        }

        CraftingRecipe recipe;
        Item output;
        try
        {
            recipe = new CraftingRecipe(
                recipeName,
                isCookingRecipe: false);
            output = recipe.createItem();
        }
        catch (Exception ex)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_storage_crafting_target",
                "player.storage_crafting.recipe_ready=true",
                "recipe=" + recipeName,
                "storage_crafting_fixture_recipe_creation_failed:" +
                ex.GetType().Name);
        }

        if (output is not StardewValley.Object storage ||
            !TryClassifyNativePlayerStorageItem(
                storage,
                out var branch) ||
            branch is
                "native_object_placement_mini_fridge" or
                "native_object_placement_mini_shipping_bin" ||
            storage is Chest chest &&
                chest.SpecialChestType is not (
                    Chest.SpecialChestTypes.None or
                    Chest.SpecialChestTypes.BigChest))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_storage_crafting_target",
                "player.storage_crafting.recipe_ready=true",
                "output=" + output.QualifiedItemId,
                "storage_crafting_fixture_requires_ordinary_storage_output");
        }

        var ingredientSlots = new List<string>();
        foreach (var ingredient in recipe.recipeList)
        {
            if (!int.TryParse(
                    ingredient.Key,
                    out var itemId) ||
                itemId < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_storage_crafting_target",
                    "player.storage_crafting.recipe_ready=true",
                    "requirement=" + ingredient.Key,
                    "storage_crafting_fixture_requires_exact_item_ingredients");
            }

            var qualifiedId =
                ItemRegistry.ManuallyQualifyItemId(
                    itemId.ToString(),
                    "(O)");
            var slot = EnsureInventoryItem(
                qualifiedId,
                ingredient.Value);
            if (slot < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_storage_crafting_target",
                    "player.storage_crafting.recipe_ready=true",
                    "requirement=" + qualifiedId,
                    "storage_crafting_fixture_inventory_full");
            }
            ingredientSlots.Add(
                qualifiedId + "@" + slot + ":" +
                ingredient.Value);
        }

        var verified =
            recipe.doesFarmerHaveIngredientsInInventory() &&
            Game1.player.couldInventoryAcceptThisItem(output);
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
            PrimitiveKind =
                "debug_setup_storage_crafting_target",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "learned_storage_recipe_preserved",
                    "exact_recipe_ingredients_available",
                    "native_output_inventory_acceptance=true",
                    "recipe_name=" + recipeName,
                    "output_qualified_item_id=" +
                        output.QualifiedItemId,
                    "native_storage_branch=" + branch,
                    "ingredient_slots=" +
                        string.Join(",", ingredientSlots)
                }
                : new[]
                {
                    "native_recipe_ingredient_or_output_gate_failed"
                },
            RequestedEffect =
                "player.storage_crafting.recipe_ready=true",
            ObservedEffect =
                "recipe_name=" + recipeName +
                ";output_qualified_item_id=" +
                output.QualifiedItemId +
                ";ingredients_ready=" +
                recipe.doesFarmerHaveIngredientsInInventory()
                    .ToString().ToLowerInvariant() +
                ";output_accepted=" +
                Game1.player.couldInventoryAcceptThisItem(output)
                    .ToString().ToLowerInvariant(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "storage_crafting_fixture_verification_failed"
                }
        };
    }

    private TrainingExecutionResult
        ExecuteSetupStoragePlacementTarget(
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
                "debug_setup_storage_placement_target",
                "player.inventory.storage_available=true",
                "target_tile=missing",
                "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var targetVector = new Vector2(
            target.X,
            target.Y);
        var qualifiedItemId =
            string.IsNullOrWhiteSpace(
                request.QualifiedItemId)
                ? "(BC)130"
                : request.QualifiedItemId;
        farm.objects.Remove(targetVector);
        farm.terrainFeatures.Remove(targetVector);
        var slotIndex = EnsureInventoryItem(
            qualifiedItemId,
            1);
        var moved = MoveFixtureFarmerToFarmAdjacent(
            target,
            out var stand,
            out var moveReason);
        var storageItem =
            slotIndex >= 0 &&
            slotIndex < Game1.player.Items.Count
                ? Game1.player.Items[slotIndex] as
                    StardewValley.Object
                : null;
        var nativeBranch = string.Empty;
        var classified = storageItem is not null &&
            TryClassifyNativePlayerStorageItem(
                storageItem,
                out nativeBranch);
        var nativeLegal = classified &&
            Utility.playerCanPlaceItemHere(
                farm,
                storageItem!,
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
            CompletedAt =
                DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind =
                "debug_setup_storage_placement_target",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_inventory_storage_available",
                    "target_tile_cleared",
                    "player_moved_adjacent",
                    "Utility.playerCanPlaceItemHere=true",
                    "inventory_slot_index=" + slotIndex,
                    "native_storage_branch=" +
                    nativeBranch,
                    "stand_tile=" + stand.X +
                    "," + stand.Y
                }
                : new[]
                {
                    slotIndex >= 0
                        ? "inventory_storage_available"
                        : "inventory_storage_unavailable",
                    classified
                        ? "native_storage_classified"
                        : "inventory_item_not_supported_storage",
                    moved
                        ? "player_moved_adjacent"
                        : moveReason,
                    nativeLegal
                        ? "native_placement_legal"
                        : "native_placement_illegal"
                },
            RequestedEffect =
                "player.inventory.storage_available=true" +
                ";location_id=Farm;target_tile=" +
                target.X + "," + target.Y,
            ObservedEffect =
                "location_id=" +
                (Game1.currentLocation?.NameOrUniqueName ??
                 "null") +
                ";target_tile=" + target.X +
                "," + target.Y +
                ";stand_tile=" + stand.X +
                "," + stand.Y +
                ";inventory_slot_index=" + slotIndex +
                ";qualified_item_id=" +
                (storageItem?.QualifiedItemId ?? "null") +
                ";native_storage_branch=" +
                (classified ? nativeBranch : "unavailable") +
                ";native_placement_legal=" +
                nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "storage_placement_fixture_not_ready"
                },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "player.inventory[" +
                            slotIndex + "]",
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

    private TrainingExecutionResult ExecutePlaceStorage(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested =
            "current_location.chests[" +
            request.LocationId + ":" +
            request.TargetTileX + "," +
            request.TargetTileY +
            "].qualified_item_id=" +
            request.QualifiedItemId +
            ";player.inventory[" +
            request.InventorySlotIndex +
            "].stack_decreases=1";
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "typed_target=missing",
                "place_storage_typed_target_fields_required");
        }
        if (string.IsNullOrWhiteSpace(
                request.NativeStorageBranch) ||
            string.IsNullOrWhiteSpace(
                request.SpecialChestType) ||
            !request.ExpectedStorageCapacity.HasValue ||
            request.ExpectedStorageCapacity.Value <= 0 ||
            string.IsNullOrWhiteSpace(
                request.StorageRole))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "storage_projection_fields=missing_or_invalid",
                "place_storage_projection_fields_required");
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
                "place_storage",
                requested,
                "location_id=" +
                (location?.NameOrUniqueName ?? "unavailable"),
                "place_storage_location_mismatch");
        }

        var slotIndex = request.InventorySlotIndex.Value;
        if (slotIndex < 0 ||
            slotIndex >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_slot=" + slotIndex,
                "place_storage_inventory_slot_out_of_range");
        }
        if (Game1.player.Items[slotIndex] is not
                StardewValley.Object storageItem ||
            !TryClassifyNativePlayerStorageItem(
                storageItem,
                out var nativeBranch))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_slot_item=not_player_storage",
                "place_storage_inventory_slot_not_storage");
        }
        if (!string.Equals(
                storageItem.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                nativeBranch,
                request.NativeStorageBranch,
                StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(request.ItemId) &&
             !string.Equals(
                 storageItem.ItemId,
                 request.ItemId,
                 StringComparison.Ordinal)))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_item=" +
                storageItem.QualifiedItemId,
                "place_storage_inventory_identity_mismatch");
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
                "place_storage",
                requested,
                "player_tile=" + playerTile.X +
                "," + playerTile.Y,
                "place_storage_player_not_adjacent");
        }

        var targetVector = new Vector2(
            target.X,
            target.Y);
        var pixelX = target.X * Game1.tileSize;
        var pixelY = target.Y * Game1.tileSize;
        if (location.objects.ContainsKey(targetVector) ||
            !Utility.playerCanPlaceItemHere(
                location,
                storageItem,
                pixelX,
                pixelY,
                Game1.player))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "native_placement_recheck=false",
                "place_storage_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, storageItem, slotIndex, target);
        var inventoryConsumed =
            attempt.StackAfter == attempt.StackBefore - 1;
        var placedStorageMatches =
            attempt.PlacedObject is Chest placedChest &&
            placedChest.playerChest.Value &&
            string.Equals(
                placedChest.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                placedChest.SpecialChestType.ToString(),
                request.SpecialChestType,
                StringComparison.Ordinal) &&
            placedChest.GetActualCapacity() ==
                request.ExpectedStorageCapacity &&
            StorageRoleMatches(
                placedChest,
                request.StorageRole);
        var verified = attempt.Placed &&
            placedStorageMatches &&
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
            CompletedAt =
                DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_storage",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "Utility.playerCanPlaceItemHere_rechecked",
                    "Utility.tryToPlaceItem_applied_native_callbacks",
                    "placed_player_chest_identity_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed
                        ? "native_place_returned_true"
                        : "native_place_returned_false",
                    placedStorageMatches
                        ? "placed_storage_identity_matches"
                        : "placed_storage_missing_or_mismatched",
                    inventoryConsumed
                        ? "inventory_consumed_one"
                        : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect =
                "location_id=" +
                location.NameOrUniqueName +
                ";target_tile=" + target.X +
                "," + target.Y +
                ";placed_qualified_item_id=" +
                (attempt.PlacedObject?.QualifiedItemId ?? "null") +
                ";placed_runtime_type=" +
                (attempt.PlacedObject?.GetType().FullName ?? "null") +
                ";placed_special_chest_type=" +
                (attempt.PlacedObject is Chest observedChest
                    ? observedChest.SpecialChestType.ToString()
                    : "null") +
                ";placed_capacity=" +
                (attempt.PlacedObject is Chest capacityChest
                    ? capacityChest.GetActualCapacity().ToString()
                    : "null") +
                ";expected_storage_role=" +
                request.StorageRole +
                ";inventory_stack_before=" +
                attempt.StackBefore +
                ";inventory_stack_after=" +
                attempt.StackAfter,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "place_storage_post_state_mismatch"
                },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "current_location.chests[" +
                            location.NameOrUniqueName +
                            ":" + target.X + "," +
                            target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId
                    },
                    new SimulatedFactChange
                    {
                        Path =
                            "player.inventory[" +
                            slotIndex + "].stack",
                        Before = attempt.StackBefore.ToString(),
                        After = attempt.StackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static bool TryClassifyNativePlayerStorageItem(
        StardewValley.Object item,
        out string branch)
    {
        branch = item.QualifiedItemId switch
        {
            "(BC)130" or "(BC)232" =>
                "native_object_placement_normal_chest",
            "(BC)BigChest" or "(BC)BigStoneChest" =>
                "native_object_placement_big_chest",
            "(BC)216" =>
                "native_object_placement_mini_fridge",
            "(BC)248" =>
                "native_object_placement_mini_shipping_bin",
            "(BC)256" =>
                "native_object_placement_junimo_chest",
            _ => string.Empty
        };
        if (branch.Length > 0)
        {
            return true;
        }
        if (item is Chest chest &&
            chest.playerChest.Value)
        {
            branch =
                "inventory_runtime_chest_placement";
            return true;
        }
        return false;
    }

    private static bool StorageRoleMatches(
        Chest chest,
        string storageRole)
    {
        return storageRole switch
        {
            "ordinary_material" =>
                (chest.SpecialChestType is
                    Chest.SpecialChestTypes.None or
                    Chest.SpecialChestTypes.BigChest) &&
                !chest.fridge.Value,
            "shared_global" =>
                chest.SpecialChestType ==
                    Chest.SpecialChestTypes.JunimoChest,
            "shipping" =>
                chest.SpecialChestType ==
                    Chest.SpecialChestTypes.MiniShippingBin,
            "fridge" => chest.fridge.Value,
            "special_storage" => true,
            _ => false
        };
    }
}
