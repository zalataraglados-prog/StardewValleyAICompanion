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
        private static CompiledActionStep[] CompileSelectSafeItemSlotStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var safeSlot = ReadIntParameter(action, "safe_slot_index") ?? SafeSlotIndex(snapshot);
            if (!safeSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("select_safe_item_slot", safeSlot.Value.ToString(), "player.current_tool_index=" + safeSlot.Value + ";player.active_object_qualified_id=null", 10)
            };
        }

        private static CompiledActionStep[] CompileMoveToTileStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var location = ReadParameter(action, "target_location") ?? "current_location";
            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
            return new[]
            {
                Step("move_to_tile", location + "(" + x.Value + "," + y.Value + ")", "player_reaches_target_tile_or_blocked", estimatedTicks)
            };
        }

        private static CompiledActionStep[] CompileClearObstacleStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var maxSwings = Math.Clamp(ReadIntParameter(action, "max_tool_swings") ?? 8, 1, 64);
            return new[]
            {
                Step(
                    "clear_obstacle",
                    "current_location(" + x.Value + "," + y.Value + ")",
                    "current_location.obstacle[" + x.Value + "," + y.Value + "]=clear_or_blocked",
                    maxSwings * 60)
            };
        }

        private static CompiledActionStep[] CompileFarmResourceClumpStep(SmallModelAction action)
        {
            var anchorX = ReadIntParameter(action, "resource_clump_tile_x");
            var anchorY = ReadIntParameter(action, "resource_clump_tile_y");
            var maximumSwings = ReadIntParameter(action, "max_tool_swings");
            if (!anchorX.HasValue || !anchorY.HasValue || !maximumSwings.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "break_resource_clump",
                    "Farm(" + anchorX.Value + "," + anchorY.Value + ")",
                    "farm.resource_clumps[" + anchorX.Value + "," + anchorY.Value + "].present=false_or_blocked",
                    Math.Clamp(maximumSwings.Value, 1, 64) * 60)
            };
        }

        private static CompiledActionStep[] CompileTillSoilStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "till_soil",
                    "Farm(" + x.Value + "," + y.Value + ")",
                    "farm.terrain_features[" + x.Value + "," + y.Value + "].type=HoeDirt;native_tool=Hoe",
                    EstimateToolActionTicks(snapshot, x.Value, y.Value))
            };
        }

        private static CompiledActionStep[] CompilePlantSeedStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var seedId = ReadParameter(action, "seed_id") ?? ReadParameter(action, "shop_item_id") ?? string.Empty;
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(seedId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "plant_seed",
                    "current_location(" + x.Value + "," + y.Value + "):" + seedId,
                    "current_location.planting_context[" + x.Value + "," + y.Value + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases",
                    60)
            };
        }

        private static CompiledActionStep[] CompileHarvestCropStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var harvestMethod = ReadParameter(action, "harvest_method") ?? "unknown";
            return new[]
            {
                Step(
                    "harvest_crop",
                    "Farm(" + x.Value + "," + y.Value + "):" + harvestMethod,
                    "farm.crops[" + x.Value + "," + y.Value + "].ready_for_harvest=false_or_blocked",
                    60)
            };
        }

        private static CompiledActionStep[] CompileHarvestGiantCropStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var maxSwings = Math.Clamp(ReadIntParameter(action, "max_tool_swings") ?? 16, 1, 64);
            return new[]
            {
                Step(
                    "harvest_giant_crop",
                    "Farm(" + x.Value + "," + y.Value + "):axe",
                    "farm.resource_clumps[" + x.Value + "," + y.Value + "].is_giant_crop=false_or_blocked",
                    maxSwings * 60)
            };
        }

        private static CompiledActionStep[] CompilePickupDebrisStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var debrisIndex = ReadIntParameter(action, "debris_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            return new[]
            {
                Step(
                    "pickup_debris",
                    "Farm(" + x.Value + "," + y.Value + "):" + (debrisIndex.HasValue ? "debris_index=" + debrisIndex.Value : qualifiedItemId),
                    "farm.debris[" + (debrisIndex.HasValue ? debrisIndex.Value.ToString() : x.Value + "," + y.Value) + "].chunk_count_decreases_or_removed=true;player.inventory.updated",
                    30)
            };
        }

        private static CompiledActionStep[] CompileCollectSpawnedObjectStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }
            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 2) * 60;

            return new[]
            {
                Step(
                    "collect_spawned_object",
                    "current_location(" + x.Value + "," + y.Value + "):" + qualifiedItemId,
                    "current_location.objects[" + x.Value + "," + y.Value + "].present=false_or_blocked",
                    estimatedTicks)
            };
        }

        private static CompiledActionStep[] CompileCollectAnimalProductStep(SmallModelAction action)
        {
            var animalId = ReadParameter(action, "target_runtime_identity");
            var tool = ReadParameter(action, "required_tool_kind");
            var output = ReadParameter(action, "qualified_item_id");
            if (string.IsNullOrWhiteSpace(animalId) || string.IsNullOrWhiteSpace(tool) || string.IsNullOrWhiteSpace(output))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "collect_animal_product",
                    "animal:" + animalId + ":" + tool,
                    "farm.animals[" + animalId + "].current_produce=null;player.inventory[" + output + "].stack_increases;player.skills.farming.experience_increases",
                    120)
            };
        }

        private static CompiledActionStep[] CompilePanOreSpotStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(ReadParameter(action, "expected_output_items_json")))
            {
                return Array.Empty<CompiledActionStep>();
            }
            return new[]
            {
                Step(
                    "pan_ore_spot",
                    "current_location(" + x.Value + "," + y.Value + "):Pan",
                    "current_location.panning.ore_pan_point_consumed=true;player.inventory.updated;player.stats.TimesPanned.increases;player.skills.mining.experience.increases;player.skills.foraging.experience.increases",
                    180)
            };
        }

        private static CompiledActionStep[] CompileCollectMachineOutputStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var expectedEffect = "farm.machines[" + x.Value + "," + y.Value + "].held_item=null;player.inventory.updated";
            expectedEffect += OptionalEffect(action, "qualified_item_id");
            expectedEffect += OptionalEffect(action, "item_id");
            expectedEffect += OptionalEffect(action, "output_stack");
            expectedEffect += OptionalEffect(action, "output_sale_price");
            expectedEffect += OptionalEffect(action, "output_total_value");
            expectedEffect += OptionalEffect(action, "machine_value_basis");
            return new[]
            {
                Step(
                    "collect_machine_output",
                    "Farm(" + x.Value + "," + y.Value + "):" + qualifiedItemId,
                    expectedEffect,
                    30)
            };
        }

        private static CompiledActionStep[] CompileCollectCrabPotStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var outputId = ReadParameter(action, "qualified_item_id");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(outputId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "collect_crab_pot",
                    "current_location(" + x.Value + "," + y.Value + "):" + outputId,
                    "current_location.objects[" + x.Value + "," + y.Value + "].crab_pot_ready_for_harvest=false;player.inventory.updated;player.skills.fishing.experience_increases",
                    Math.Max(30, (ReadIntParameter(action, "estimated_minutes") ?? 2) * 60))
            };
        }

        private static CompiledActionStep[] CompileCollectFishPondOutputStep(SmallModelAction action)
        {
            return CompileFishPondStep(action, "collect_fish_pond_output", "fish_pond.output=null;player.inventory.updated;player.skills.fishing.experience_increases");
        }

        private static CompiledActionStep[] CompileCompleteFishPondRequestStep(SmallModelAction action)
        {
            return CompileFishPondStep(action, "complete_fish_pond_request", "fish_pond.has_completed_request=true;fish_pond.population_gate_increases;player.inventory.updated;player.skills.fishing.experience_increases");
        }

        private static CompiledActionStep[] CompileFishPondStep(SmallModelAction action, string primitive, string effect)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var buildingX = ReadIntParameter(action, "building_tile_x");
            var buildingY = ReadIntParameter(action, "building_tile_y");
            if (!targetX.HasValue || !targetY.HasValue || !buildingX.HasValue || !buildingY.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }
            return new[]
            {
                Step(
                    primitive,
                    "FarmPond(" + buildingX.Value + "," + buildingY.Value + "):target(" + targetX.Value + "," + targetY.Value + ")",
                    effect,
                    Math.Max(30, (ReadIntParameter(action, "estimated_minutes") ?? 2) * 60))
            };
        }

        private static CompiledActionStep[] CompileLoadMachineInputStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var inputSlot = ReadIntParameter(action, "input_slot_index");
            if (!x.HasValue || !y.HasValue || !inputSlot.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var qualifiedItemId = ReadParameter(action, "qualified_item_id") ?? string.Empty;
            var expectedEffect = "farm.machines[" + x.Value + "," + y.Value + "].minutes_until_ready>0_or_ready=true;player.inventory[" + inputSlot.Value + "].stack_decreases";
            expectedEffect += OptionalEffect(action, "input_slot_index");
            expectedEffect += OptionalEffect(action, "qualified_item_id");
            expectedEffect += OptionalEffect(action, "item_id");
            expectedEffect += OptionalEffect(action, "input_stack_available");
            expectedEffect += OptionalEffect(action, "predicted_output_qualified_item_id");
            expectedEffect += OptionalEffect(action, "predicted_output_item_id");
            expectedEffect += OptionalEffect(action, "predicted_output_stack");
            expectedEffect += OptionalEffect(action, "predicted_output_sale_price");
            expectedEffect += OptionalEffect(action, "predicted_output_total_value");
            expectedEffect += OptionalEffect(action, "predicted_output_net_value");
            expectedEffect += OptionalEffect(action, "predicted_minutes_until_ready");
            return new[]
            {
                Step(
                    "load_machine_input",
                    "Farm(" + x.Value + "," + y.Value + "):slot" + inputSlot.Value + ":" + qualifiedItemId,
                    expectedEffect,
                    30)
            };
        }

        private static string OptionalEffect(SmallModelAction action, string parameterName)
        {
            var value = ReadParameter(action, parameterName);
            return string.IsNullOrWhiteSpace(value) ? string.Empty : ";" + parameterName + "=" + value;
        }

        private static CompiledActionStep[] CompileTraverseConnectorStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var targetLocation = ReadParameter(action, "expected_target_location");
            if (!x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(targetLocation))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var arrivalX = ReadIntParameter(action, "expected_arrival_tile_x");
            var arrivalY = ReadIntParameter(action, "expected_arrival_tile_y");
            var expected = "location=" + targetLocation;
            if (arrivalX.HasValue && arrivalY.HasValue)
            {
                expected += ";player.tile=" + arrivalX.Value + "," + arrivalY.Value;
            }

            var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
            return new[]
            {
                Step("traverse_connector", "current_location(" + x.Value + "," + y.Value + ")", expected, estimatedTicks)
            };
        }

        private static CompiledActionStep[] CompileFaceDirectionStep(SmallModelAction action)
        {
            var direction = ReadIntParameter(action, "direction");
            if (!direction.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("face_direction", direction.Value.ToString(), "player_facing_direction_changed", 1)
            };
        }

        private static CompiledActionStep[] CompileWaitTicksStep(SmallModelAction action)
        {
            var waitTicks = ReadIntParameter(action, "wait_ticks");
            if (!waitTicks.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("wait_ticks", waitTicks.Value.ToString(), "ticks_elapsed_without_mutation", waitTicks.Value)
            };
        }

        private static CompiledActionStep[] CompileSleepSteps(SnapshotEnvelope snapshot, SmallModelAction? action = null)
        {
            if (action is null ? ActiveMenuOpen(snapshot) : ActionSeesActiveMenuOpen(action, snapshot))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var target = SleepTarget(snapshot);
            if (target is null)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("move_to_bed_adjacent", target.HomeLocation + "(" + target.StandX + "," + target.StandY + ")", "player.tile=" + target.StandX + "," + target.StandY, target.EstimatedTicks),
                Step("step_onto_sleep_touch_tile", target.HomeLocation + "(" + target.BedX + "," + target.BedY + ")", "TouchAction=Sleep;menus.sleep_prompt_context.prompt_open=true", 30),
                Step("confirm_sleep_yes", "menus.sleep_prompt_context", "day_safely_ended", 120)
            };
        }

        private static CompiledActionStep[] CompileInteractStep(SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var expectedActionType = ReadParameter(action, "expected_action_type") ?? "unknown";
            if (!x.HasValue || !y.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("interact", "current_location(" + x.Value + "," + y.Value + ")", "interact_map_action_" + expectedActionType, 30)
            };
        }

        private static CompiledActionStep[] CompileBuyShopItemStep(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var normalized = BuildBuyShopItemParameters(action, snapshot);
            var qualifiedItemId = normalized.FirstOrDefault(item => item.Name == "qualified_item_id")?.Value ?? string.Empty;
            var quantity = normalized.FirstOrDefault(item => item.Name == "quantity")?.Value ?? "1";
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("buy_shop_item", qualifiedItemId + "x" + quantity, "player.inventory_count_increases;player.money_decreases", 20)
            };
        }

        private static CompiledActionStep[] CompileChooseDialogueResponseStep(SmallModelAction action)
        {
            var expectedDialogueKey = ReadParameter(action, "expected_dialogue_key") ?? string.Empty;
            var responseKey = ReadParameter(action, "dialogue_response_key") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expectedDialogueKey) || string.IsNullOrWhiteSpace(responseKey))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step("choose_dialogue_response", expectedDialogueKey + ":" + responseKey, "expected_dialogue_response_effect", 20)
            };
        }

    }
}
