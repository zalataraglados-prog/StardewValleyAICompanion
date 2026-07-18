using System;
using System.Collections.Generic;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileCraftMachineItemStep(SmallModelAction action)
        {
            var recipeName = ReadParameter(action, "recipe_name");
            var outputQualifiedId = ReadParameter(action, "output_qualified_item_id");
            var outputCount = ReadIntParameter(action, "output_count");
            if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(outputQualifiedId) || !outputCount.HasValue)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "craft_machine_item",
                    "recipe:" + recipeName + ":" + outputQualifiedId,
                    "player.inventory.materials_consumed_by_native_recipe=true;player.inventory.output_increases=" + outputQualifiedId + ":" + outputCount.Value + ";player.crafting_recipes[" + recipeName + "].count_increases=" + outputCount.Value,
                    30)
            };
        }

        private static string[] ValidateCraftMachineItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.craft_machine_item")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("craft_machine_item_menu_must_be_clear");
            }

            var recipeName = ReadParameter(action, "recipe_name");
            var row = MachineCraftingRow(snapshot, recipeName);
            if (!row.HasValue)
            {
                reasons.Add("craft_machine_item_recipe_not_verified_by_transparent_state");
                return reasons.ToArray();
            }

            var expectedIngredientRows = row.Value.TryGetProperty("ingredient_rows", out var ingredientRows)
                ? ingredientRows.GetRawText()
                : "[]";
            var demand = MachineDemandProjectionEvaluator.Evaluate(snapshot, row.Value);
            if (!string.Equals(ReadString(row.Value, "craft_candidate_status"), "ready_for_native_personal_crafting_menu", StringComparison.Ordinal) ||
                ReadBool(row.Value, "output_inventory_acceptance_after_material_consumption") != true)
            {
                reasons.Add("craft_machine_item_recipe_not_ready");
            }
            if (!string.Equals(ReadParameter(action, "output_qualified_item_id"), ReadString(row.Value, "output_qualified_item_id"), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "output_item_id"), ReadString(row.Value, "output_item_id"), StringComparison.Ordinal) ||
                ReadIntParameter(action, "output_count") != ReadInt(row.Value, "output_count_per_craft") ||
                ReadIntParameter(action, "times_crafted_before") != ReadInt(row.Value, "times_crafted") ||
                !string.Equals(ReadParameter(action, "ingredient_rows_json"), expectedIngredientRows, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "crafting_source"), "native_personal_crafting_menu", StringComparison.Ordinal))
            {
                reasons.Add("craft_machine_item_projection_drifted");
            }
            if (!demand.HasDemand ||
                !string.Equals(ReadParameter(action, "machine_demand_class"), demand.DemandClass, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "machine_scale"), demand.MachineScale, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "machine_horizon_status"), demand.HorizonStatus, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "machine_timing_status"), demand.TimingStatus, StringComparison.Ordinal) ||
                ReadIntParameter(action, "machine_demand_priority") != demand.Priority ||
                !string.Equals(ReadParameter(action, "priority_task_required"), Lower(demand.PriorityTaskRequired), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "priority_task_sources_json"), JsonSerializer.Serialize(demand.PriorityTaskSources), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "production_capacity_required"), Lower(demand.ProductionCapacityRequired), StringComparison.Ordinal) ||
                ReadIntParameter(action, "potential_input_count") != demand.PotentialInputCount ||
                ReadIntParameter(action, "backlog_input_units") != demand.BacklogInputUnits ||
                ReadIntParameter(action, "placed_same_machine_count") != demand.PlacedSameMachineCount ||
                ReadIntParameter(action, "idle_same_machine_count") != demand.IdleSameMachineCount ||
                ReadIntParameter(action, "process_cycle_minutes") != demand.ProcessCycleMinutes ||
                ReadIntParameter(action, "next_arrival_days") != demand.NextArrivalDays ||
                ReadIntParameter(action, "next_arrival_units") != demand.NextArrivalUnits ||
                ReadIntParameter(action, "capacity_before_next_arrival") != demand.CapacityBeforeNextArrival ||
                ReadIntParameter(action, "capacity_deficit_units") != demand.CapacityDeficitUnits ||
                ReadIntParameter(action, "required_additional_machine_count") != demand.RequiredAdditionalMachineCount ||
                ReadIntParameter(action, "latest_build_lead_minutes") != demand.LatestBuildLeadMinutes ||
                ReadIntParameter(action, "minutes_until_next_arrival") != demand.MinutesUntilNextArrival ||
                !string.Equals(ReadParameter(action, "machine_build_window_open"), Lower(demand.BuildWindowOpen), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "collection_path_required"), Lower(demand.CollectionPathRequired), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "collection_path_source"), demand.CollectionPathSource, StringComparison.Ordinal))
            {
                reasons.Add("craft_machine_item_demand_projection_drifted");
            }

            return reasons.ToArray();
        }

        private static JsonElement? MachineCraftingRow(SnapshotEnvelope snapshot, string? recipeName)
        {
            var context = ReadStateFieldValue(snapshot, "player", "machine_crafting");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(row, "recipe_name"), recipeName, StringComparison.Ordinal))
                {
                    return row;
                }
            }
            return null;
        }
    }
}
