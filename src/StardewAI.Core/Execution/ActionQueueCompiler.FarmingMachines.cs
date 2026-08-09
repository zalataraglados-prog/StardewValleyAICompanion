using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static int EstimateToolActionTicks(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var routeTicks = playerX.HasValue && playerY.HasValue
                ? Math.Max(0, Math.Abs(playerX.Value - targetX) + Math.Abs(playerY.Value - targetY) - 1) * 30
                : 30;
            return routeTicks + 5 + 60 + 20;
        }

        private static JsonElement? ReadCropArray(SnapshotEnvelope snapshot)
        {
            if (snapshot.State.TryGetValue("current_location", out var currentLocation) &&
                currentLocation.ValueKind == JsonValueKind.Object &&
                currentLocation.TryGetProperty("crops", out var currentCropsField) &&
                currentCropsField.TryGetProperty("value", out var currentCrops) &&
                currentCrops.ValueKind == JsonValueKind.Array)
            {
                return currentCrops;
            }

            return null;
        }

        private static bool PlantingContextAllows(SnapshotEnvelope snapshot, int targetX, int targetY, string seedId)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "planting_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("hoe_dirt_tiles", out var tiles) ||
                tiles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var tile in tiles.EnumerateArray())
            {
                if (tile.ValueKind != JsonValueKind.Object ||
                    ReadInt(tile, "tile_x") != targetX ||
                    ReadInt(tile, "tile_y") != targetY ||
                    ReadBool(tile, "has_crop") == true ||
                    !tile.TryGetProperty("seed_results", out var seedResults) ||
                    seedResults.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in seedResults.EnumerateArray())
                {
                    if (result.ValueKind == JsonValueKind.Object &&
                        string.Equals(ReadString(result, "seed_id"), seedId, StringComparison.OrdinalIgnoreCase) &&
                        ReadBool(result, "hard_rule_allows_planting") == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HarvestCropReadyAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crop = HarvestCropAt(snapshot, targetX, targetY);
            return crop.HasValue && ReadBool(crop.Value, "ready_for_harvest") == true;
        }

        private static bool HarvestCropUsesGrab(SmallModelAction action, SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var actionMethod = ReadParameter(action, "harvest_method");
            if (!string.IsNullOrWhiteSpace(actionMethod))
            {
                return string.Equals(actionMethod, "Grab", StringComparison.OrdinalIgnoreCase);
            }

            var crop = HarvestCropAt(snapshot, targetX, targetY);
            return crop.HasValue &&
                string.Equals(ReadString(crop.Value, "harvest_method"), "Grab", StringComparison.OrdinalIgnoreCase);
        }

        private static bool InventoryMayAcceptHarvestYield(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crop = HarvestCropAt(snapshot, targetX, targetY);
            if (!crop.HasValue)
            {
                return false;
            }

            var harvestItemId = ReadString(crop.Value, "harvest_item_id");
            if (string.IsNullOrWhiteSpace(harvestItemId))
            {
                return true;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var qualifiedHarvestId = harvestItemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase)
                ? harvestItemId
                : "(O)" + harvestItemId;
            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), qualifiedHarvestId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == 0 &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

        private static JsonElement? HarvestCropAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var crops = ReadCropArray(snapshot);
            if (!crops.HasValue)
            {
                return null;
            }

            foreach (var crop in crops.Value.EnumerateArray())
            {
                if (crop.ValueKind == JsonValueKind.Object &&
                    ReadInt(crop, "tile_x") == targetX &&
                    ReadInt(crop, "tile_y") == targetY)
                {
                    return crop;
                }
            }

            return null;
        }

        private static JsonElement? GiantCropResourceClumpAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
            if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var clump in clumps.Value.EnumerateArray())
            {
                if (clump.ValueKind != JsonValueKind.Object ||
                    ReadBool(clump, "is_giant_crop") != true)
                {
                    continue;
                }

                var x = ReadInt(clump, "tile_x");
                var y = ReadInt(clump, "tile_y");
                var width = Math.Max(1, ReadInt(clump, "width"));
                var height = Math.Max(1, ReadInt(clump, "height"));
                if (targetX >= x && targetX < x + width &&
                    targetY >= y && targetY < y + height)
                {
                    return clump;
                }
            }

            return null;
        }

        private static CompiledActionStep[] CompileMachineProcessingSteps(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("farm", out var farm) ||
                farm.ValueKind != JsonValueKind.Object ||
                !farm.TryGetProperty("machines", out var machinesField) ||
                !machinesField.TryGetProperty("value", out var machines) ||
                machines.ValueKind != JsonValueKind.Array)
            {
                return new[]
                {
                    Step("machine_processing_noop", "Farm", "no_machine_data_available", 0)
                };
            }

            var steps = new List<CompiledActionStep>();
            foreach (var machine in machines.EnumerateArray())
            {
                if (machine.ValueKind != JsonValueKind.Object || !IsMachineReady(machine))
                {
                    continue;
                }

                var x = ReadInt(machine, "tile_x");
                var y = ReadInt(machine, "tile_y");
                steps.Add(Step("process_machine", "Farm(" + x + "," + y + ")", "machine_output_collected_or_input_loaded", 80));
            }

            return steps.Count == 0
                ? new[] { Step("machine_processing_noop", "Farm", "no_machine_ready", 0) }
                : steps.ToArray();
        }

    }
}
