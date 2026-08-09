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
    private TrainingExecutionResult ExecuteSetupFertilizerTarget(TrainingExecutionRequest request, bool useIndoorPot = false)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fertilizer_target", "current_location.planting_context[target].fertilizer_result.hard_rule_allows_application=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        farm.objects.Remove(tile);
        farm.terrainFeatures.Remove(tile);
        HoeDirt dirt;
        if (useIndoorPot)
        {
            var pot = new IndoorPot(tile) { Location = farm };
            farm.objects[tile] = pot;
            dirt = pot.hoeDirt.Value;
        }
        else
        {
            dirt = new HoeDirt(0, farm);
            farm.terrainFeatures[tile] = dirt;
        }

        StardewValley.Object? fertilizer = null;
        foreach (var itemId in Game1.objectData.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            try
            {
                if (ItemRegistry.Create(ItemRegistry.QualifyItemId(itemId), 2) is StardewValley.Object candidate &&
                    candidate.Category == StardewValley.Object.fertilizerCategory &&
                    dirt.CanApplyFertilizer(candidate.QualifiedItemId))
                {
                    fertilizer = candidate;
                    break;
                }
            }
            catch
            {
                // A malformed custom item must not invalidate the base-game fixture scan.
            }
        }
        if (fertilizer is null || !Game1.player.addItemToInventoryBool(fertilizer))
        {
            return BlockedWithPrimitive(request, "debug_setup_fertilizer_target", "current_location.planting_context[target].fertilizer_result.hard_rule_allows_application=true", "fertilizer=missing", "fixture_no_runtime_legal_fertilizer_or_inventory_capacity");
        }

        var slotIndex = FindFertilizerInventoryIndex(fertilizer.QualifiedItemId);
        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));
        var verified = slotIndex >= 0 && dirt.CanApplyFertilizer(fertilizer.QualifiedItemId) &&
            (useIndoorPot
                ? farm.objects.TryGetValue(tile, out var targetObject) && targetObject is IndoorPot
                : farm.terrainFeatures.TryGetValue(tile, out var terrainFeature) && ReferenceEquals(terrainFeature, dirt));
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
            PrimitiveKind = "debug_setup_fertilizer_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "runtime_Data_Objects_fertilizer_selected", "native_HoeDirt_rule_allows_application" }
                : new[] { "fixture_fertilizer_target_not_verified" },
            RequestedEffect = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].fertilizer_result.hard_rule_allows_application=true",
            ObservedEffect = "qualified_item_id=" + fertilizer.QualifiedItemId + ";slot_index=" + slotIndex + ";rule=" + dirt.CheckApplyFertilizerRules(fertilizer.QualifiedItemId) + ";is_garden_pot=" + useIndoorPot.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_fertilizer_target_not_verified" }
        };
    }

    private static int FindFertilizerInventoryIndex(string qualifiedItemId)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (Game1.player.Items[index] is StardewValley.Object item &&
                item.Category == StardewValley.Object.fertilizerCategory &&
                string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

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
        var dirt = location.GetHoeDirtAtTile(tile);
        if (dirt is null)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_not_hoe_dirt");
        }

        if (dirt.crop is not null)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_already_has_crop");
        }

        var isGardenPot = location.objects.TryGetValue(tile, out var tileObject) && tileObject is IndoorPot;
        if (!Game1.cropData.TryGetValue(seedId, out var cropData) ||
            (!location.SeedsIgnoreSeasonsHere() && !(isGardenPot && !location.IsOutdoors) && !cropData.Seasons.Contains(location.GetSeason())))
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_crop_catalog_or_season_blocked");
        }

        if (!(isGardenPot && !location.IsOutdoors) &&
            !location.CanPlantSeedsHere(seedId, request.TargetTileX.Value, request.TargetTileY.Value, isGardenPot, out _))
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
        var hasCrop = location.GetHoeDirtAtTile(tile)?.crop is not null;
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
        var dirt = location.GetHoeDirtAtTile(tile);
        if (dirt?.crop is null)
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
        if (method != HarvestMethod.Grab &&
            !string.IsNullOrWhiteSpace(request.QuestCandidateId) &&
            !request.QuestAcquisitionSourceStep)
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "quest_harvest_requires_grab_method");
        }
        if (!ValidateQuestItemHarvestTarget(request, crop, out var questRemainingBefore, out var questReason))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), questReason);
        }

        if (method == HarvestMethod.Grab && !CanInventoryAcceptHarvest(crop))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_inventory_cannot_accept_grab_yield");
        }

        var beforeReady = dirt.readyForHarvest();
        var beforeHadCrop = dirt.crop is not null;
        var beforeInventory = InventoryStackSignature();
        var beforeFarmingExperience = Game1.player.experiencePoints[Farmer.farmingSkill];
        var beforeForagingExperience = Game1.player.experiencePoints[Farmer.foragingSkill];
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
                Path = "current_location.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest",
                Before = beforeReady.ToString().ToLowerInvariant(),
                After = afterReady.ToString().ToLowerInvariant()
            },
            new()
            {
                Path = "current_location.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].has_crop",
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
        changed.Add(new SimulatedFactChange
        {
            Path = "player.skills.farming.experience",
            Before = beforeFarmingExperience.ToString(System.Globalization.CultureInfo.InvariantCulture),
            After = Game1.player.experiencePoints[Farmer.farmingSkill].ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        changed.Add(new SimulatedFactChange
        {
            Path = "player.skills.foraging.experience",
            Before = beforeForagingExperience.ToString(System.Globalization.CultureInfo.InvariantCulture),
            After = Game1.player.experiencePoints[Farmer.foragingSkill].ToString(System.Globalization.CultureInfo.InvariantCulture)
        });

        var result = new TrainingExecutionResult
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
        ApplyQuestItemHarvestFeedback(result, request, questRemainingBefore);
        return result;
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
        var dirt = Game1.currentLocation.GetHoeDirtAtTile(tile);
        if (dirt?.crop is null)
        {
            return "has_crop=false;ready_for_harvest=false";
        }

        return "has_crop=true;ready_for_harvest=" + dirt.readyForHarvest().ToString().ToLowerInvariant() + ";harvest_method=" + dirt.crop.GetHarvestMethod();
    }

    private void StartHarvestGiantCrop(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", "current_location.resource_clumps[target].is_giant_crop=false", "target_tile=missing", "target_tile_required"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = GiantCropRequestedEffect(request);
        var before = GiantCropObservedEffect(location, target);
        if (!string.IsNullOrWhiteSpace(request.LocationId) &&
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_target_location_mismatch"));
            return;
        }

        var clump = GiantCropAt(location, target);
        if (clump is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_target_not_giant_crop"));
            return;
        }
        var anchor = new Point((int)clump.Tile.X, (int)clump.Tile.Y);
        if (!request.ResourceClumpTileX.HasValue || !request.ResourceClumpTileY.HasValue ||
            !request.ResourceClumpWidth.HasValue || !request.ResourceClumpHeight.HasValue ||
            !request.ResourceClumpParentSheetIndex.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ToolSlotIndex.HasValue ||
            !string.Equals(request.RequiredToolKind, "axe", StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, clump.GetType().FullName, StringComparison.Ordinal) ||
            request.ResourceClumpTileX.Value != anchor.X ||
            request.ResourceClumpTileY.Value != anchor.Y ||
            request.ResourceClumpWidth.Value != clump.width.Value ||
            request.ResourceClumpHeight.Value != clump.height.Value ||
            request.ResourceClumpParentSheetIndex.Value != clump.parentSheetIndex.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_typed_target_or_tool_projection_drifted"));
            return;
        }
        var guaranteedOutputIds = GuaranteedGiantCropOutputIds(clump);
        var resourceQuestReason = ValidateQuestResourceSourceTarget(request, guaranteedOutputIds);
        if (!string.IsNullOrWhiteSpace(resourceQuestReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, resourceQuestReason));
            return;
        }
        if (!ValidateSpecialOrderCollectSourceTarget(request, guaranteedOutputIds, out var specialOrderReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, specialOrderReason));
            return;
        }

        var toolSlot = request.ToolSlotIndex.Value;
        var axe = toolSlot >= 0 && toolSlot < Game1.player.Items.Count
            ? Game1.player.Items[toolSlot] as Axe
            : null;
        if (axe is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_tool_slot_drifted"));
            return;
        }

        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!ResourceClumpContainsTile(clump, target) ||
            ResourceClumpContainsTile(clump, stand) ||
            !AreAdjacent(stand, target) ||
            !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) ||
            IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_hit_or_stand_geometry_drifted"));
            return;
        }
        var maximumMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            location,
            Game1.player.TilePoint,
            stand,
            maximumMovement,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_path_unavailable:" + pathReason));
            return;
        }

        ResourceClumpToolTracePatch.Begin(clump);
        activeResourceClump = new ActiveResourceClump(
            pending,
            location,
            clump,
            anchor,
            target,
            stand,
            path,
            axe,
            "axe",
            0,
            clump.health.Value,
            Math.Clamp(request.MaxCrops, 1, 64),
            maximumMovement,
            request.RestoreSlotIndex ?? Game1.player.CurrentToolIndex,
            "current_location.resource_clumps",
            false,
            Array.Empty<ClearanceOutputItemExpectation>(),
            Array.Empty<int>(),
            null,
            string.Empty,
            0,
            requested);
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
        return "current_location.resource_clumps[" + request.TargetTileX + "," + request.TargetTileY + "].is_giant_crop=false";
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
        var locationId = string.IsNullOrWhiteSpace(request.LocationId)
            ? "Farm"
            : request.LocationId;
        var location = Game1.getLocationFromName(locationId);
        if (location is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_debris_target",
                "current_location.debris[target].chunk_count>0",
                "location_id=" + locationId,
                "fixture_location_not_found");
        }
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var itemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "388" : request.ShopItemId)
            : request.QualifiedItemId;
        var origin = new Vector2(target.X * Game1.tileSize + 32, target.Y * Game1.tileSize + 32);
        var beforeCount = location.debris.Count;
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

        location.debris.Add(debris);
        var moved = MoveFixtureFarmerToLocationAdjacent(
            location,
            target,
            out var stand,
            out var moveReason);
        var afterCount = location.debris.Count;
        var verified = moved &&
            DebrisAt(location, target, afterCount - 1) is not null;
        var factPrefix = string.Equals(
            location.NameOrUniqueName,
            "Farm",
            StringComparison.OrdinalIgnoreCase)
                ? "farm.debris"
                : "current_location.debris";

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
                ? new[] { "isolated_runtime_fixture_debris_present", "location_id=" + location.NameOrUniqueName, "qualified_item_id=" + itemId, "debris_index=" + (afterCount - 1) }
                : new[] { moved ? "fixture_debris_not_present" : moveReason, "qualified_item_id=" + itemId },
            RequestedEffect = factPrefix + "[" + (afterCount - 1) + "].chunk_count>0",
            ObservedEffect = DebrisObservedEffect(location, target, afterCount - 1) +
                ";location_id=" + location.NameOrUniqueName +
                ";stand_tile=" + stand.X + "," + stand.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { moved ? "fixture_debris_not_present" : moveReason },
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = factPrefix + ".count",
                        Before = beforeCount.ToString(),
                        After = afterCount.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
