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
    private TrainingExecutionResult ExecuteSetupPlantSeedTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_plant_seed_target", "current_location.planting_context[target].hard_rule_allows_planting=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var seedId = PlantSeedId(request);
        var farm = Game1.getFarm();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 1;
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        farm.terrainFeatures[tile] = new HoeDirt(0, farm);
        EnsureSeedInInventory(seedId);
        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));

        var verified = farm.terrainFeatures.TryGetValue(tile, out var feature) &&
            feature is HoeDirt dirt &&
            dirt.crop is null &&
            FindSeedInventoryIndex(seedId, farm) >= 0 &&
            farm.CanPlantSeedsHere(seedId, request.TargetTileX.Value, request.TargetTileY.Value, false, out _) &&
            Game1.cropData.TryGetValue(seedId, out var cropData) &&
            cropData.Seasons.Contains(farm.GetSeason());

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
            PrimitiveKind = "debug_setup_plant_seed_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_plantable_seed_tile" }
                : new[] { "fixture_plant_seed_target_not_verified" },
            RequestedEffect = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].hard_rule_allows_planting=true",
            ObservedEffect = PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_plant_seed_target_not_verified" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].hard_rule_allows_planting",
                        Before = "unknown",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecutePlantSeed(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "plant_seed", "current_location.planting_context[target].has_crop=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var seedId = PlantSeedId(request);
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_not_hoe_dirt");
        }

        if (dirt.crop is not null)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_already_has_crop");
        }

        if (!Game1.cropData.TryGetValue(seedId, out var cropData) || !cropData.Seasons.Contains(location.GetSeason()))
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_crop_catalog_or_season_blocked");
        }

        if (!location.CanPlantSeedsHere(seedId, request.TargetTileX.Value, request.TargetTileY.Value, false, out _))
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_location_rule_blocked");
        }

        var seedIndex = FindSeedInventoryIndex(seedId, location);
        if (seedIndex < 0)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_inventory_seed_missing");
        }

        var beforeStack = Game1.player.Items[seedIndex]?.Stack ?? 0;
        var planted = dirt.plant(seedId, Game1.player, isFertilizer: false);
        if (planted)
        {
            ConsumeOneInventoryItem(seedIndex);
        }

        var afterStack = Game1.player.Items.ElementAtOrDefault(seedIndex)?.Stack ?? 0;
        var verified = planted && dirt.crop is not null && afterStack == beforeStack - 1;
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
            PrimitiveKind = "plant_seed",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_tile_crop_created", "seed_stack_decreased" }
                : new[] { "plant_seed_post_state_mismatch" },
            RequestedEffect = PlantSeedRequestedEffect(request, seedId),
            ObservedEffect = PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "plant_seed_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].has_crop",
                        Before = "false",
                        After = "true"
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.seed_inventory[" + seedId + "].stack",
                        Before = beforeStack.ToString(),
                        After = afterStack.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupHarvestCropTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_harvest_crop_target", "farm.crops[target].ready_for_harvest=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var seedId = string.IsNullOrWhiteSpace(request.SeedId) ? "472" : PlantSeedId(request);
        var farm = Game1.getFarm();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 1;
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        var dirt = new HoeDirt(0, farm)
        {
            crop = new Crop(seedId, request.TargetTileX.Value, request.TargetTileY.Value, farm)
        };
        dirt.crop.growCompletely();
        farm.terrainFeatures[tile] = dirt;
        if (request.DebugFillInventory)
        {
            FillInventoryWithBlockingItems(dirt.crop.indexOfHarvest.Value);
        }

        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));

        var verified = farm.terrainFeatures.TryGetValue(tile, out var afterFeature) &&
            afterFeature is HoeDirt afterDirt &&
            afterDirt.crop is not null &&
            afterDirt.readyForHarvest() &&
            (!request.DebugFillInventory || !CanInventoryAcceptHarvest(afterDirt.crop));

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
            PrimitiveKind = "debug_setup_harvest_crop_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { request.DebugFillInventory ? "isolated_runtime_fixture_crop_ready_for_harvest_inventory_full" : "isolated_runtime_fixture_crop_ready_for_harvest" }
                : new[] { "fixture_crop_not_ready_for_harvest" },
            RequestedEffect = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest=true",
            ObservedEffect = HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_crop_not_ready_for_harvest" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest",
                        Before = "unknown",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string PlantSeedId(TrainingExecutionRequest request)
    {
        var raw = !string.IsNullOrWhiteSpace(request.SeedId)
            ? request.SeedId
            : !string.IsNullOrWhiteSpace(request.ShopItemId)
                ? request.ShopItemId
                : request.QualifiedItemId;
        return raw.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? raw[3..] : raw;
    }

    private static int FindSeedInventoryIndex(string seedId, GameLocation location)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var item = Game1.player.Items[index];
            if (item is null)
            {
                continue;
            }

            if (string.Equals(Crop.ResolveSeedId(item.ItemId, location), seedId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ItemId, seedId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.QualifiedItemId, "(O)" + seedId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void EnsureSeedInInventory(string seedId)
    {
        if (FindSeedInventoryIndex(seedId, Game1.getFarm()) >= 0)
        {
            return;
        }

        var item = ItemRegistry.Create("(O)" + seedId, 2);
        Game1.player.addItemToInventoryBool(item);
    }

    private static void ConsumeOneInventoryItem(int index)
    {
        var item = Game1.player.Items[index];
        if (item is null)
        {
            return;
        }

        item.Stack -= 1;
        if (item.Stack <= 0)
        {
            Game1.player.Items[index] = null;
        }
    }

    private static string PlantSeedRequestedEffect(TrainingExecutionRequest request, string seedId)
    {
        return "current_location.planting_context[" + request.TargetTileX + "," + request.TargetTileY + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases";
    }

    private static string PlantSeedObservedEffect(int x, int y, string seedId)
    {
        var location = Game1.currentLocation;
        var tile = new Vector2(x, y);
        var hasCrop = location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt { crop: not null };
        var seedIndex = FindSeedInventoryIndex(seedId, location);
        var stack = seedIndex >= 0 ? Game1.player.Items[seedIndex]?.Stack ?? 0 : 0;
        return "has_crop=" + hasCrop.ToString().ToLowerInvariant() + ";seed_id=" + seedId + ";seed_stack=" + stack;
    }

    private TrainingExecutionResult ExecuteHarvestCrop(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "harvest_crop", "farm.crops[target].ready_for_harvest=false", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = HarvestCropRequestedEffect(request);
        if (!location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_target_not_crop");
        }

        if (!dirt.readyForHarvest())
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_not_ready");
        }

        var crop = dirt.crop;
        var method = crop.GetHarvestMethod();
        if (!string.IsNullOrWhiteSpace(request.HarvestMethod) &&
            !string.Equals(method.ToString(), request.HarvestMethod, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_method_mismatch");
        }

        if (method == HarvestMethod.Grab && !CanInventoryAcceptHarvest(crop))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_inventory_cannot_accept_grab_yield");
        }

        var beforeReady = dirt.readyForHarvest();
        var beforeHadCrop = dirt.crop is not null;
        var beforeInventory = InventoryStackSignature();
        var harvestItemId = crop.indexOfHarvest.Value;
        var beforeHarvestDebrisCount = CountDebrisForItem(location, harvestItemId);
        var expectedRegrow = crop.RegrowsAfterHarvest();
        var harvestCallApplied = crop.harvest(request.TargetTileX.Value, request.TargetTileY.Value, dirt, null, isForcedScytheHarvest: method == HarvestMethod.Scythe);
        if (!expectedRegrow && dirt.crop is not null && (harvestCallApplied || !dirt.readyForHarvest()))
        {
            dirt.destroyCrop(showAnimation: false);
        }

        var afterReady = dirt.crop is not null && dirt.readyForHarvest();
        var afterHadCrop = dirt.crop is not null;
        var afterInventory = InventoryStackSignature();
        var afterHarvestDebrisCount = CountDebrisForItem(location, harvestItemId);
        var verifiedRegrowState = beforeReady && afterHadCrop && !afterReady;
        var verifiedRemovedState = beforeReady && !afterHadCrop && !afterReady;
        var cropStateChanged = verifiedRegrowState || verifiedRemovedState;
        var inventoryChanged = !string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal);
        var harvestDebrisCreated = method != HarvestMethod.Scythe ||
            string.IsNullOrWhiteSpace(harvestItemId) ||
            afterHarvestDebrisCount > beforeHarvestDebrisCount;
        var verified = cropStateChanged &&
            (method != HarvestMethod.Grab || inventoryChanged) &&
            harvestDebrisCreated;
        var changed = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest",
                Before = beforeReady.ToString().ToLowerInvariant(),
                After = afterReady.ToString().ToLowerInvariant()
            },
            new()
            {
                Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].has_crop",
                Before = beforeHadCrop.ToString().ToLowerInvariant(),
                After = afterHadCrop.ToString().ToLowerInvariant()
            }
        };
        if (inventoryChanged)
        {
            changed.Add(new SimulatedFactChange
            {
                Path = "player.inventory.stack_signature",
                Before = beforeInventory,
                After = afterInventory
            });
        }

        if (method == HarvestMethod.Scythe && !string.IsNullOrWhiteSpace(harvestItemId))
        {
            changed.Add(new SimulatedFactChange
            {
                Path = "farm.debris[" + QualifyObjectId(harvestItemId) + "].count",
                Before = beforeHarvestDebrisCount.ToString(),
                After = afterHarvestDebrisCount.ToString()
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
            PrimitiveKind = "harvest_crop",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? (method == HarvestMethod.Scythe
                    ? new[] { verifiedRegrowState ? "target_crop_regrow_state_updated" : "target_crop_removed_or_no_longer_ready", "target_harvest_debris_created" }
                    : new[] { verifiedRegrowState ? "target_crop_regrow_state_updated" : "target_crop_removed_or_no_longer_ready" })
                : new[] { method == HarvestMethod.Grab && !inventoryChanged ? "harvest_crop_inventory_did_not_change" : !harvestDebrisCreated ? "harvest_crop_debris_not_created" : "harvest_crop_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value),
            BlockReasons = verified ? Array.Empty<string>() : new[] { method == HarvestMethod.Grab && !inventoryChanged ? "harvest_crop_inventory_did_not_change" : !harvestDebrisCreated ? "harvest_crop_debris_not_created" : "harvest_crop_post_state_mismatch" },
            ChangedFacts = verified ? changed.ToArray() : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupGiantCropTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_giant_crop_target", "farm.resource_clumps[target].is_giant_crop=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var requestedGiantCropId = string.IsNullOrWhiteSpace(request.GiantCropId) ? "276" : request.GiantCropId;
        var giantCropId = ResolveGiantCropId(requestedGiantCropId);
        if (string.IsNullOrWhiteSpace(giantCropId) || !GiantCrop.TryGetData(giantCropId, out _))
        {
            return BlockedWithPrimitive(request, "debug_setup_giant_crop_target", "farm.resource_clumps[target].is_giant_crop=true", "requested_giant_crop_id=" + requestedGiantCropId + ";valid=false", "giant_crop_id_unknown");
        }

        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var area = new XnaRectangle(target.X * Game1.tileSize, target.Y * Game1.tileSize, 3 * Game1.tileSize, 3 * Game1.tileSize);
        for (var x = target.X; x < target.X + 3; x++)
        {
            for (var y = target.Y; y < target.Y + 3; y++)
            {
                var key = new Vector2(x, y);
                farm.objects.Remove(key);
                farm.terrainFeatures.Remove(key);
            }
        }

        foreach (var existing in farm.resourceClumps.Where(clump => clump.getBoundingBox().Intersects(area)).ToList())
        {
            farm.resourceClumps.Remove(existing);
        }

        var before = GiantCropObservedEffect(farm, target);
        farm.resourceClumps.Add(new GiantCrop(giantCropId, tile));
        MoveFixtureFarmerToFarmAdjacent(target);
        var after = GiantCropObservedEffect(farm, target);
        var verified = GiantCropAt(farm, target) is not null;

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
            PrimitiveKind = "debug_setup_giant_crop_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_giant_crop_present", "giant_crop_id=" + giantCropId }
                : new[] { "fixture_giant_crop_not_present", "giant_crop_id=" + giantCropId },
            RequestedEffect = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop=true",
            ObservedEffect = after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_giant_crop_not_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop",
                        Before = before,
                        After = after
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static int CountDebrisForItem(GameLocation location, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        var qualified = QualifyObjectId(itemId);
        return location.debris.Count(debris =>
            string.Equals(debris.item?.QualifiedItemId, qualified, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(debris.itemId.Value, qualified, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(debris.itemId.Value, itemId, StringComparison.OrdinalIgnoreCase));
    }

    private static string QualifyObjectId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
    }

    private static bool CanInventoryAcceptHarvest(Crop crop)
    {
        if (string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value))
        {
            return true;
        }

        return Game1.player.couldInventoryAcceptThisItem(crop.indexOfHarvest.Value, 1);
    }

    private static void FillInventoryWithBlockingItems(string harvestItemId)
    {
        var fillerIds = new[] { "390", "388", "770", "382" };
        var maxItems = Game1.player.maxItems.Value;
        for (var index = 0; index < maxItems; index++)
        {
            var fillerId = fillerIds[index % fillerIds.Length];
            if (string.Equals(fillerId, harvestItemId, StringComparison.OrdinalIgnoreCase))
            {
                fillerId = fillerIds.First(id => !string.Equals(id, harvestItemId, StringComparison.OrdinalIgnoreCase));
            }

            var item = ItemRegistry.Create("(O)" + fillerId, 999);
            Game1.player.Items[index] = item;
        }
    }

    private static string HarvestCropRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.crops[" + request.TargetTileX + "," + request.TargetTileY + "].ready_for_harvest=false";
    }

    private static string HarvestCropObservedEffect(int x, int y)
    {
        var tile = new Vector2(x, y);
        if (!Game1.currentLocation.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            return "has_crop=false;ready_for_harvest=false";
        }

        return "has_crop=true;ready_for_harvest=" + dirt.readyForHarvest().ToString().ToLowerInvariant() + ";harvest_method=" + dirt.crop.GetHarvestMethod();
    }

    private TrainingExecutionResult ExecuteHarvestGiantCrop(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", "farm.resource_clumps[target].is_giant_crop=false", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = GiantCropRequestedEffect(request);
        var before = GiantCropObservedEffect(location, target);
        var clump = GiantCropAt(location, target);
        if (clump is null)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_target_not_giant_crop");
        }

        var axe = FindTool<Axe>();
        if (axe is null)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_axe_missing");
        }

        var beforeDebrisCount = location.debris.Count;
        var beforeHealth = clump.health.Value;
        var swings = 0;
        const int maxSwings = 64;
        axe.lastUser = Game1.player;
        while (GiantCropAt(location, target) is GiantCrop current && swings < maxSwings)
        {
            swings++;
            if (current.performToolAction(axe, 0, current.Tile))
            {
                location.resourceClumps.Remove(current);
            }
        }

        var after = GiantCropObservedEffect(location, target);
        var afterDebrisCount = location.debris.Count;
        var removed = GiantCropAt(location, target) is null;
        var debrisCreated = afterDebrisCount > beforeDebrisCount;
        var verified = removed && debrisCreated;

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
            PrimitiveKind = "harvest_giant_crop",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_giant_crop_removed", "target_giant_crop_debris_created", "tool=Axe", "swings=" + swings }
                : new[] { removed ? "target_giant_crop_removed" : "target_giant_crop_still_present", debrisCreated ? "target_giant_crop_debris_created" : "target_giant_crop_debris_not_created", "tool=Axe", "swings=" + swings },
            RequestedEffect = requested,
            ObservedEffect = after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { removed ? "harvest_giant_crop_debris_not_created" : "harvest_giant_crop_still_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop",
                        Before = "true",
                        After = "false"
                    },
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].health",
                        Before = beforeHealth.ToString(),
                        After = "0"
                    },
                    new SimulatedFactChange
                    {
                        Path = "farm.debris.count",
                        Before = beforeDebrisCount.ToString(),
                        After = afterDebrisCount.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static GiantCrop? GiantCropAt(GameLocation location, Point target)
    {
        var tileRect = TileRectangle(target);
        return location.resourceClumps
            .OfType<GiantCrop>()
            .FirstOrDefault(clump => clump.getBoundingBox().Intersects(tileRect));
    }

    private static string ResolveGiantCropId(string requested)
    {
        if (GiantCrop.TryGetData(requested, out _))
        {
            return requested;
        }

        var qualifiedCropId = QualifyObjectId(requested);
        var matches = GiantCrop.GetGiantCropsFor(qualifiedCropId);
        return matches.Count > 0 ? matches[0].Key : string.Empty;
    }

    private static string GiantCropRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.resource_clumps[" + request.TargetTileX + "," + request.TargetTileY + "].is_giant_crop=false";
    }

    private static string GiantCropObservedEffect(GameLocation location, Point target)
    {
        var clump = GiantCropAt(location, target);
        return clump is null
            ? "is_giant_crop=false"
            : "is_giant_crop=true;id=" + clump.Id + ";health=" + clump.health.Value + ";tile=" + (int)clump.Tile.X + "," + (int)clump.Tile.Y;
    }

    private TrainingExecutionResult ExecuteSetupDebrisTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_debris_target", "farm.debris[target].chunk_count>0", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var itemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "388" : request.ShopItemId)
            : request.QualifiedItemId;
        var origin = new Vector2(target.X * Game1.tileSize + 32, target.Y * Game1.tileSize + 32);
        var beforeCount = farm.debris.Count;
        var debris = new Debris(ItemRegistry.Create(itemId, Math.Max(1, request.Quantity ?? 1)), origin, Utility.PointToVector2(Game1.player.StandingPixel))
        {
            timeSinceDoneBouncing = -60000f,
            chunksMoveTowardPlayer = false
        };
        foreach (var chunk in debris.Chunks)
        {
            chunk.position.Value = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
            chunk.xVelocity.Value = 0f;
            chunk.yVelocity.Value = 0f;
            chunk.hasPassedRestingLineOnce.Value = true;
        }

        farm.debris.Add(debris);
        MoveFixtureFarmerToFarmAdjacent(target);
        var afterCount = farm.debris.Count;
        var verified = DebrisAt(farm, target, afterCount - 1) is not null;

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
            PrimitiveKind = "debug_setup_debris_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_debris_present", "qualified_item_id=" + itemId, "debris_index=" + (afterCount - 1) }
                : new[] { "fixture_debris_not_present", "qualified_item_id=" + itemId },
            RequestedEffect = "farm.debris[" + (afterCount - 1) + "].chunk_count>0",
            ObservedEffect = DebrisObservedEffect(farm, target, afterCount - 1),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_debris_not_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.debris.count",
                        Before = beforeCount.ToString(),
                        After = afterCount.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
