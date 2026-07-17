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
        private static string[] ValidateMovementPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.move_to_tile")
            {
                return Array.Empty<string>();
            }

            return ReadIntParameter(action, "target_tile_x").HasValue && ReadIntParameter(action, "target_tile_y").HasValue
                ? Array.Empty<string>()
                : new[] { "movement_target_tile_required" };
        }

        private static string[] ValidateClearObstaclePlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.clear_obstacle")
            {
                return Array.Empty<string>();
            }

            return ReadIntParameter(action, "target_tile_x").HasValue && ReadIntParameter(action, "target_tile_y").HasValue
                ? Array.Empty<string>()
                : new[] { "clear_obstacle_target_tile_required" };
        }

        private static string[] ValidateTillSoilPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.till_soil")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("till_soil_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("till_soil_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("till_soil_target_location_mismatch");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidatePlantSeedPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.plant_seed")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("plant_seed_target_tile_required");
            }

            var seedId = ReadParameter(action, "seed_id");
            if (string.IsNullOrWhiteSpace(seedId))
            {
                seedId = ReadParameter(action, "shop_item_id");
            }

            if (string.IsNullOrWhiteSpace(seedId))
            {
                reasons.Add("plant_seed_seed_id_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("plant_seed_menu_must_be_clear");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !string.IsNullOrWhiteSpace(seedId) &&
                !PlantingContextAllows(snapshot, targetX.Value, targetY.Value, seedId))
            {
                reasons.Add("plant_seed_not_allowed_by_transparent_context");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateHarvestCropPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_crop")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("harvest_crop_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_crop_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_crop_target_location_mismatch");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !HarvestCropReadyAt(snapshot, targetX.Value, targetY.Value))
            {
                reasons.Add("harvest_crop_not_ready_by_transparent_farm_state");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                HarvestCropUsesGrab(action, snapshot, targetX.Value, targetY.Value) &&
                !InventoryMayAcceptHarvestYield(snapshot, targetX.Value, targetY.Value))
            {
                reasons.Add("harvest_crop_inventory_cannot_accept_grab_yield");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateHarvestGiantCropPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.harvest_giant_crop")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("harvest_giant_crop_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("harvest_giant_crop_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("harvest_giant_crop_target_location_mismatch");
            }

            if (targetX.HasValue &&
                targetY.HasValue &&
                !GiantCropResourceClumpAt(snapshot, targetX.Value, targetY.Value).HasValue)
            {
                reasons.Add("harvest_giant_crop_not_verified_by_transparent_resource_clump");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidatePickupDebrisPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.pickup_debris")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("pickup_debris_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("pickup_debris_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("pickup_debris_target_location_mismatch");
            }

            JsonElement? targetDebris = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                targetDebris = DebrisAt(snapshot, targetX.Value, targetY.Value, ReadIntParameter(action, "debris_index"));
                if (!targetDebris.HasValue)
                {
                    reasons.Add("pickup_debris_not_verified_by_transparent_farm_state");
                }
            }

            if (targetDebris.HasValue &&
                !InventoryMayAcceptDebrisItem(snapshot, targetDebris.Value))
            {
                reasons.Add("pickup_debris_inventory_cannot_accept_item");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? DebrisAt(SnapshotEnvelope snapshot, int targetX, int targetY, int? debrisIndex)
        {
            var debris = ReadStateFieldValue(snapshot, "farm", "debris");
            if (!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in debris.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (debrisIndex.HasValue && ReadInt(item, "debris_index") != debrisIndex.Value)
                {
                    continue;
                }

                if (DebrisHasChunkAt(item, targetX, targetY))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool DebrisHasChunkAt(JsonElement debris, int targetX, int targetY)
        {
            if (!debris.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Object &&
                    ReadInt(chunk, "tile_x") == targetX &&
                    ReadInt(chunk, "tile_y") == targetY)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InventoryMayAcceptDebrisItem(SnapshotEnvelope snapshot, JsonElement debris)
        {
            var qualifiedItemId = ReadString(debris, "qualified_item_id");
            var itemId = ReadString(debris, "item_id");
            var normalizedQualifiedId = !string.IsNullOrWhiteSpace(qualifiedItemId)
                ? qualifiedItemId
                : string.IsNullOrWhiteSpace(itemId)
                    ? string.Empty
                    : itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
            if (string.IsNullOrWhiteSpace(normalizedQualifiedId))
            {
                return false;
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

            var quality = ReadInt(debris, "item_quality");
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

                if (string.Equals(ReadString(item, "qualified_item_id"), normalizedQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ValidateCollectMachineOutputPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.collect_machine_output")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("collect_machine_output_target_tile_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("collect_machine_output_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("collect_machine_output_target_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetX.Value, targetY.Value);
                if (!machine.HasValue)
                {
                    reasons.Add("collect_machine_output_not_verified_by_transparent_farm_state");
                }
            }

            if (machine.HasValue)
            {
                if (ReadBool(machine.Value, "ready_for_harvest") != true)
                {
                    reasons.Add("collect_machine_output_not_ready");
                }

                if (!machine.Value.TryGetProperty("held_item", out var heldItem) ||
                    heldItem.ValueKind != JsonValueKind.Object ||
                    string.IsNullOrWhiteSpace(ReadString(heldItem, "qualified_item_id")))
                {
                    reasons.Add("collect_machine_output_item_unavailable");
                }
                else
                {
                    var requestedQualifiedId = ReadParameter(action, "qualified_item_id");
                    if (!string.IsNullOrWhiteSpace(requestedQualifiedId) &&
                        !string.Equals(ReadString(heldItem, "qualified_item_id"), requestedQualifiedId, StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("collect_machine_output_item_mismatch");
                    }

                    if (!InventoryMayAcceptMachineOutput(snapshot, heldItem))
                    {
                        reasons.Add("collect_machine_output_inventory_cannot_accept_item");
                    }
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachineAt(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var machine in machines.Value.EnumerateArray())
            {
                if (machine.ValueKind == JsonValueKind.Object &&
                    ReadInt(machine, "tile_x") == targetX &&
                    ReadInt(machine, "tile_y") == targetY)
                {
                    return machine;
                }
            }

            return null;
        }

        private static bool InventoryMayAcceptMachineOutput(SnapshotEnvelope snapshot, JsonElement heldItem)
        {
            var qualifiedItemId = ReadString(heldItem, "qualified_item_id");
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return false;
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

            var quality = ReadInt(heldItem, "quality");
            var stack = Math.Max(1, ReadInt(heldItem, "stack"));
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

                if (string.Equals(ReadString(item, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "maximum_stack_size") - ReadInt(item, "stack") >= stack)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] ValidateLoadMachineInputPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.load_machine_input")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var inputSlot = ReadIntParameter(action, "input_slot_index");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("load_machine_input_target_tile_required");
            }

            if (!inputSlot.HasValue)
            {
                reasons.Add("load_machine_input_slot_required");
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("load_machine_input_menu_must_be_clear");
            }

            var targetLocation = ReadParameter(action, "target_location");
            if (!string.IsNullOrWhiteSpace(targetLocation) &&
                !string.Equals(targetLocation, "Farm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("load_machine_input_target_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetX.Value, targetY.Value);
                if (!machine.HasValue)
                {
                    reasons.Add("load_machine_input_not_verified_by_transparent_farm_state");
                }
            }

            if (machine.HasValue)
            {
                if (ReadInt(machine.Value, "minutes_until_ready") > 0 || ReadBool(machine.Value, "ready_for_harvest") == true)
                {
                    reasons.Add("load_machine_input_target_busy");
                }

                JsonElement? input = null;
                if (inputSlot.HasValue)
                {
                    input = MachineLoadableInputAt(machine.Value, inputSlot.Value);
                    if (!input.HasValue)
                    {
                        reasons.Add("load_machine_input_not_verified_by_transparent_probe");
                    }
                }

                if (input.HasValue)
                {
                    var requestedQualifiedId = ReadParameter(action, "qualified_item_id");
                    if (!string.IsNullOrWhiteSpace(requestedQualifiedId) &&
                        !string.Equals(ReadString(input.Value, "qualified_item_id"), requestedQualifiedId, StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add("load_machine_input_item_mismatch");
                    }
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachineLoadableInputAt(JsonElement machine, int slotIndex)
        {
            if (!machine.TryGetProperty("loadable_inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var input in inputs.EnumerateArray())
            {
                if (input.ValueKind == JsonValueKind.Object && ReadInt(input, "slot_index") == slotIndex)
                {
                    return input;
                }
            }

            return null;
        }

    }
}
