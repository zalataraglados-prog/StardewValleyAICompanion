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

        private static string[] ValidateClearObstaclePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.clear_obstacle")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                reasons.Add("clear_obstacle_target_tile_required");
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("clear_obstacle_menu_must_be_clear");
            }

            if (targetX.HasValue && targetY.HasValue)
            {
                var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
                var target = terrainFeatures.HasValue && terrainFeatures.Value.ValueKind == JsonValueKind.Array
                    ? terrainFeatures.Value.EnumerateArray().FirstOrDefault(feature =>
                        ReadInt(feature, "tile_x") == targetX.Value &&
                        ReadInt(feature, "tile_y") == targetY.Value)
                    : default;
                if (target.ValueKind == JsonValueKind.Object &&
                    ReadString(target, "type").EndsWith(".Tree", StringComparison.Ordinal))
                {
                    var status = ReadString(target, "tree_clear_executor_status");
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        reasons.Add(string.IsNullOrWhiteSpace(status) ? "tree_clear_projection_unavailable" : status);
                    }
                    var expectedHits = NullableReadInt(target, "expected_axe_hits_to_clear");
                    var maximumHits = ReadIntParameter(action, "max_tool_swings");
                    if (!expectedHits.HasValue)
                    {
                        reasons.Add("tree_clear_expected_hits_unavailable");
                    }
                    else if (!maximumHits.HasValue || maximumHits.Value < expectedHits.Value)
                    {
                        reasons.Add("tree_clear_tool_swing_budget_insufficient");
                    }
                }

                var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
                var targetObject = objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array
                    ? objects.Value.EnumerateArray().FirstOrDefault(item =>
                        ReadInt(item, "tile_x") == targetX.Value &&
                        ReadInt(item, "tile_y") == targetY.Value &&
                        ReadString(item, "clear_kind") is "twig" or "artifact_spot")
                    : default;
                if (targetObject.ValueKind == JsonValueKind.Object)
                {
                    var status = ReadString(targetObject, "clear_obstacle_executor_status");
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        reasons.Add(string.IsNullOrWhiteSpace(status) ? "object_clear_projection_unavailable" : status);
                    }
                    var expectedHits = NullableReadInt(targetObject, "expected_tool_hits_to_clear");
                    var maximumHits = ReadIntParameter(action, "max_tool_swings");
                    if (!expectedHits.HasValue || !maximumHits.HasValue || maximumHits.Value < expectedHits.Value)
                    {
                        reasons.Add("object_clear_tool_swing_budget_insufficient");
                    }
                    if (ReadIntParameter(action, "tool_slot_index") != NullableReadInt(targetObject, "tool_slot_index"))
                    {
                        reasons.Add("object_clear_tool_slot_drifted");
                    }
                    if (!string.Equals(ReadParameter(action, "required_tool_kind"), ReadString(targetObject, "required_tool_kind"), StringComparison.Ordinal))
                    {
                        reasons.Add("object_clear_required_tool_kind_drifted");
                    }
                    if (!string.Equals(ReadParameter(action, "skill_experience_projection_status"), "exact", StringComparison.Ordinal) ||
                        !string.Equals(ReadString(targetObject, "harvest_experience_projection_status"), "exact", StringComparison.Ordinal) ||
                        !string.Equals(ReadParameter(action, "skill_experience_skill_id"), ReadString(targetObject, "harvest_experience_skill_id"), StringComparison.Ordinal) ||
                        ReadIntParameter(action, "skill_experience_on_success_min") != NullableReadInt(targetObject, "harvest_experience_on_success_min") ||
                        ReadIntParameter(action, "skill_experience_on_success_max") != NullableReadInt(targetObject, "harvest_experience_on_success_max"))
                    {
                        reasons.Add("object_clear_experience_projection_drifted");
                    }
                    if (!string.Equals(ReadParameter(action, "clear_output_projection_status"), "exact", StringComparison.Ordinal) ||
                        !string.Equals(ReadString(targetObject, "clear_output_projection_status"), "exact", StringComparison.Ordinal) ||
                        !string.Equals(ReadParameter(action, "clear_output_items_json"), ReadString(targetObject, "clear_output_items_json"), StringComparison.Ordinal) ||
                        !string.Equals(ReadParameter(action, "clear_output_qualified_item_id") ?? string.Empty, ReadString(targetObject, "clear_output_qualified_item_id"), StringComparison.OrdinalIgnoreCase) ||
                        ReadIntParameter(action, "clear_output_quantity_min") != NullableReadInt(targetObject, "clear_output_quantity_min") ||
                        ReadIntParameter(action, "clear_output_quantity_max") != NullableReadInt(targetObject, "clear_output_quantity_max") ||
                        !string.Equals(ReadParameter(action, "clear_bonus_output_qualified_item_id") ?? string.Empty, ReadString(targetObject, "clear_bonus_output_qualified_item_id"), StringComparison.OrdinalIgnoreCase) ||
                        ReadIntParameter(action, "clear_bonus_output_quantity_min") != NullableReadInt(targetObject, "clear_bonus_output_quantity_min") ||
                        ReadIntParameter(action, "clear_bonus_output_quantity_max") != NullableReadInt(targetObject, "clear_bonus_output_quantity_max") ||
                        ReadIntParameter(action, "artifact_spots_dug_before") != NullableReadInt(targetObject, "artifact_spots_dug_before") ||
                        ReadIntParameter(action, "artifact_spots_dug_delta") != NullableReadInt(targetObject, "artifact_spots_dug_delta") ||
                        ReadIntParameter(action, "artifact_spots_dug_expected_after") != NullableReadInt(targetObject, "artifact_spots_dug_expected_after") ||
                        !string.Equals(ReadParameter(action, "clear_terrain_feature_expected_after") ?? string.Empty, ReadString(targetObject, "clear_terrain_feature_expected_after"), StringComparison.Ordinal) ||
                        ReadIntParameter(action, "defense_book_mail_before") != NullableReadInt(targetObject, "defense_book_mail_before") ||
                        ReadIntParameter(action, "defense_book_mail_expected_after") != NullableReadInt(targetObject, "defense_book_mail_expected_after"))
                    {
                        reasons.Add("object_clear_output_projection_drifted");
                    }
                }

            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateFarmResourceClumpPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.break_farm_resource_clump")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
            var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
            var width = ReadIntParameter(action, "resource_clump_width");
            var height = ReadIntParameter(action, "resource_clump_height");
            var parentSheetIndex = ReadIntParameter(action, "resource_clump_parent_sheet_index");
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var toolSlot = ReadIntParameter(action, "tool_slot_index");
            var maximumHits = ReadIntParameter(action, "max_tool_swings");
            if (!anchorX.HasValue || !anchorY.HasValue || !width.HasValue || !height.HasValue ||
                !parentSheetIndex.HasValue || !targetX.HasValue || !targetY.HasValue ||
                !standX.HasValue || !standY.HasValue || !toolSlot.HasValue || !maximumHits.HasValue)
            {
                reasons.Add("farm_resource_clump_typed_target_fields_required");
                return reasons.ToArray();
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("farm_resource_clump_menu_must_be_clear");
            }
            if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "Farm", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("farm_resource_clump_requires_loaded_farm");
            }
            if (!string.Equals(ReadParameter(action, "required_tool_kind"), "axe", StringComparison.Ordinal))
            {
                reasons.Add("farm_resource_clump_requires_axe");
            }
            if (width.Value < 1 || height.Value < 1 ||
                !TileInsideRectangle(targetX.Value, targetY.Value, anchorX.Value, anchorY.Value, width.Value, height.Value) ||
                TileInsideRectangle(standX.Value, standY.Value, anchorX.Value, anchorY.Value, width.Value, height.Value) ||
                Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1)
            {
                reasons.Add("farm_resource_clump_hit_or_stand_geometry_invalid");
            }

            var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
            var clump = clumps.HasValue && clumps.Value.ValueKind == JsonValueKind.Array
                ? clumps.Value.EnumerateArray().FirstOrDefault(row =>
                    ReadInt(row, "tile_x") == anchorX.Value &&
                    ReadInt(row, "tile_y") == anchorY.Value &&
                    ReadInt(row, "width") == width.Value &&
                    ReadInt(row, "height") == height.Value &&
                    ReadInt(row, "parent_sheet_index") == parentSheetIndex.Value)
                : default;
            if (clump.ValueKind != JsonValueKind.Object ||
                ReadString(clump, "clear_kind") is not ("resource_stump" or "hollow_log"))
            {
                reasons.Add("farm_resource_clump_target_not_found_or_drifted");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            var status = ReadString(clump, "clear_obstacle_executor_status");
            if (!string.Equals(status, "ready", StringComparison.Ordinal))
            {
                reasons.Add(string.IsNullOrWhiteSpace(status) ? "farm_resource_clump_projection_unavailable" : status);
            }
            var expectedHits = NullableReadInt(clump, "expected_tool_hits_to_clear");
            if (!expectedHits.HasValue || maximumHits.Value < expectedHits.Value)
            {
                reasons.Add("farm_resource_clump_tool_swing_budget_insufficient");
            }
            if (NullableReadInt(clump, "tool_slot_index") != toolSlot.Value)
            {
                reasons.Add("farm_resource_clump_tool_slot_drifted");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool TileInsideRectangle(int x, int y, int anchorX, int anchorY, int width, int height)
        {
            return x >= anchorX && x < anchorX + width && y >= anchorY && y < anchorY + height;
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
            var machineLocation = ReadParameter(action, "machine_location_id");
            if (string.IsNullOrWhiteSpace(targetLocation) ||
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("collect_machine_output_target_location_mismatch");
            }
            if (string.IsNullOrWhiteSpace(machineLocation) ||
                !string.Equals(machineLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("collect_machine_output_machine_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetLocation, targetX.Value, targetY.Value);
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

                var expectedDeltasJson = ReadParameter(action, "expected_skill_experience_deltas_json");
                var expectedMasteryDelta = ReadIntParameter(action, "expected_mastery_experience_delta");
                if (string.IsNullOrWhiteSpace(expectedDeltasJson) || !expectedMasteryDelta.HasValue ||
                    !string.Equals(ReadParameter(action, "machine_harvest_experience_raw"), ReadString(machine.Value, "harvest_experience_raw"), StringComparison.Ordinal) ||
                    !string.Equals(expectedDeltasJson, ReadString(machine.Value, "harvest_experience_deltas_json"), StringComparison.Ordinal) ||
                    expectedMasteryDelta.Value != ReadInt(machine.Value, "harvest_mastery_experience_delta") ||
                    !string.Equals(ReadParameter(action, "skill_experience_projection_status"), ReadString(machine.Value, "harvest_experience_projection_status"), StringComparison.Ordinal) ||
                    !string.Equals(ReadParameter(action, "skill_experience_condition"), "native_machine_output_collection", StringComparison.Ordinal))
                {
                    reasons.Add("collect_machine_output_experience_projection_drifted");
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? MachineAt(SnapshotEnvelope snapshot, string? locationId, int targetX, int targetY)
        {
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var machine in machines.Value.EnumerateArray())
            {
                if (machine.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var machineLocation = ReadString(machine, "location_id");
                if (string.IsNullOrWhiteSpace(machineLocation))
                {
                    machineLocation = "Farm";
                }
                if (string.Equals(machineLocation, locationId, StringComparison.OrdinalIgnoreCase) &&
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
            var machineLocation = ReadParameter(action, "machine_location_id");
            if (string.IsNullOrWhiteSpace(targetLocation) ||
                !string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("load_machine_input_target_location_mismatch");
            }
            if (string.IsNullOrWhiteSpace(machineLocation) ||
                !string.Equals(machineLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("load_machine_input_machine_location_mismatch");
            }

            JsonElement? machine = null;
            if (targetX.HasValue && targetY.HasValue)
            {
                machine = MachineAt(snapshot, targetLocation, targetX.Value, targetY.Value);
                if (!machine.HasValue)
                {
                    reasons.Add("load_machine_input_not_verified_by_transparent_farm_state");
                }
            }

            if (machine.HasValue)
            {
                if (!machine.Value.TryGetProperty("machine_execution_semantics", out var executionSemantics) ||
                    executionSemantics.ValueKind != JsonValueKind.Object ||
                    ReadString(executionSemantics, "execution_status") is not ("available_data_driven" or "available_native_runtime_override"))
                {
                    reasons.Add("load_machine_input_execution_semantics_not_supported");
                }

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
                    if (!string.Equals(
                            ReadString(input.Value, "load_executor_status"),
                            "covered_for_runtime_load",
                            StringComparison.Ordinal))
                    {
                        reasons.Add("load_machine_input_runtime_load_not_verified");
                    }
                    if (!input.Value.TryGetProperty("predicted_output", out var predictedOutput) ||
                        predictedOutput.ValueKind != JsonValueKind.Object ||
                        !string.Equals(
                            ReadString(predictedOutput, "training_eligibility_status"),
                            "exact_current_snapshot_probe_supported",
                            StringComparison.Ordinal))
                    {
                        reasons.Add("load_machine_input_prediction_not_exact_for_training");
                    }

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
