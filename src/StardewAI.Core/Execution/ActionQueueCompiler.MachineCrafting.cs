using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
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

        private static string[] ValidateCraftMachineItemPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string goalId,
            StrategyCommitmentLedger? commitmentLedger)
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

            var craftingSource = ReadParameter(action, "crafting_source");
            var usesWorkbench = string.Equals(
                craftingSource,
                "native_workbench_crafting_menu",
                StringComparison.Ordinal);
            var source = usesWorkbench
                ? MachineWorkbenchCraftingSource(
                    row.Value,
                    ReadParameter(action, "workbench_access_point_id"))
                : row;
            if (!source.HasValue)
            {
                reasons.Add("craft_machine_item_source_not_verified_by_transparent_state");
                return reasons.ToArray();
            }

            var expectedIngredientRows = source.Value.TryGetProperty("ingredient_rows", out var ingredientRows)
                ? ingredientRows.GetRawText()
                : "[]";
            var expectedReadyStatus = usesWorkbench
                ? "ready_for_native_workbench_crafting_menu"
                : "ready_for_native_personal_crafting_menu";
            var actualReadyStatus = usesWorkbench
                ? ReadString(source.Value, "craft_candidate_status")
                : ReadString(row.Value, "craft_candidate_status");
            var demand = MachineDemandProjectionEvaluator.Evaluate(snapshot, row.Value, commitmentLedger);
            var reservationGuard = new MachineCraftingMaterialReservationGuard().Evaluate(
                snapshot,
                ingredientRows,
                usesWorkbench,
                commitmentLedger);
            var materialOpportunityCost =
                MachineCraftMaterialOpportunityCostProjection.Evaluate(
                    ingredientRows,
                    usesWorkbench);
            var expectedGoalSupport =
                ExplicitGoalSupportProjection.Read(
                    "craft_machine_item",
                    "machine_demand_class=" +
                    demand.DemandClass +
                    ";machine_build_window_open=" +
                    Lower(demand.BuildWindowOpen) +
                    ";required_additional_machine_count=" +
                    demand.RequiredAdditionalMachineCount +
                    ";machine_economic_value_status=" +
                    demand.EconomicValueStatus +
                    ";machine_capacity_deficit_processing_net_value=" +
                    demand.CapacityDeficitProcessingNetValue +
                    ";machine_craft_material_opportunity_cost_status=" +
                    materialOpportunityCost.Status +
                    ";machine_craft_material_opportunity_cost=" +
                    materialOpportunityCost.TotalSaleValue,
                    goalId);
            if (!string.Equals(actualReadyStatus, expectedReadyStatus, StringComparison.Ordinal) ||
                ReadBool(source.Value, "output_inventory_acceptance_after_material_consumption") != true)
            {
                reasons.Add("craft_machine_item_recipe_not_ready");
            }
            if (!string.Equals(ReadParameter(action, "output_qualified_item_id"), ReadString(row.Value, "output_qualified_item_id"), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "output_item_id"), ReadString(row.Value, "output_item_id"), StringComparison.Ordinal) ||
                ReadIntParameter(action, "output_count") != ReadInt(row.Value, "output_count_per_craft") ||
                ReadIntParameter(action, "times_crafted_before") != ReadInt(row.Value, "times_crafted") ||
                !string.Equals(ReadParameter(action, "ingredient_rows_json"), expectedIngredientRows, StringComparison.Ordinal) ||
                !string.Equals(
                    craftingSource,
                    usesWorkbench
                        ? "native_workbench_crafting_menu"
                        : "native_personal_crafting_menu",
                    StringComparison.Ordinal))
            {
                reasons.Add("craft_machine_item_projection_drifted");
            }
            if (usesWorkbench)
            {
                var expectedNodeIds = source.Value.TryGetProperty("native_container_node_ids", out var nodeIds)
                    ? nodeIds.GetRawText()
                    : "[]";
                var targetX = NullableReadInt(source.Value, "tile_x");
                var targetY = NullableReadInt(source.Value, "tile_y");
                var standX = ReadIntParameter(action, "stand_tile_x");
                var standY = ReadIntParameter(action, "stand_tile_y");
                if (!string.Equals(
                        ReadParameter(action, "workbench_container_node_ids_json"),
                        expectedNodeIds,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        ReadParameter(action, "location_id"),
                        ReadString(source.Value, "location_id"),
                        StringComparison.Ordinal) ||
                    ReadIntParameter(action, "target_tile_x") != targetX ||
                    ReadIntParameter(action, "target_tile_y") != targetY ||
                    !standX.HasValue ||
                    !standY.HasValue ||
                    !targetX.HasValue ||
                    !targetY.HasValue ||
                    Math.Abs(standX.Value - targetX.Value) +
                    Math.Abs(standY.Value - targetY.Value) != 1)
                {
                    reasons.Add("craft_machine_item_workbench_projection_drifted");
                }
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
                !string.Equals(ReadParameter(action, "machine_economic_value_status"), demand.EconomicValueStatus, StringComparison.Ordinal) ||
                ReadIntParameter(action, "machine_backlog_processing_net_value") != demand.BacklogProcessingNetValue ||
                ReadIntParameter(action, "machine_capacity_deficit_processing_net_value") != demand.CapacityDeficitProcessingNetValue ||
                ReadIntParameter(action, "potential_input_count") != demand.PotentialInputCount ||
                ReadIntParameter(action, "backlog_input_units") != demand.BacklogInputUnits ||
                ReadIntParameter(action, "placed_same_machine_count") != demand.PlacedSameMachineCount ||
                ReadIntParameter(action, "inventory_same_machine_count") != demand.InventorySameMachineCount ||
                ReadIntParameter(action, "idle_same_machine_count") != demand.IdleSameMachineCount ||
                ReadIntParameter(action, "process_cycle_minutes") != demand.ProcessCycleMinutes ||
                ReadIntParameter(action, "next_arrival_days") != demand.NextArrivalDays ||
                ReadIntParameter(action, "next_arrival_units") != demand.NextArrivalUnits ||
                ReadIntParameter(action, "next_arrival_service_interval_days") != demand.NextArrivalServiceIntervalDays ||
                ReadIntParameter(action, "capacity_before_next_arrival") != demand.CapacityBeforeNextArrival ||
                ReadIntParameter(action, "capacity_deficit_units") != demand.CapacityDeficitUnits ||
                ReadIntParameter(action, "capacity_between_arrival_waves") != demand.CapacityBetweenArrivalWaves ||
                ReadIntParameter(action, "arrival_wave_capacity_deficit_units") != demand.ArrivalWaveCapacityDeficitUnits ||
                ReadIntParameter(action, "required_additional_machine_count") != demand.RequiredAdditionalMachineCount ||
                ReadIntParameter(action, "latest_build_lead_minutes") != demand.LatestBuildLeadMinutes ||
                ReadIntParameter(action, "minutes_until_next_arrival") != demand.MinutesUntilNextArrival ||
                !string.Equals(ReadParameter(action, "machine_build_window_open"), Lower(demand.BuildWindowOpen), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "next_arrival_source"), demand.NextArrivalSource, StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "commitment_ledger_id"), demand.CommitmentLedgerId, StringComparison.Ordinal) ||
                ReadIntParameter(action, "commitment_ledger_revision") != demand.CommitmentLedgerRevision ||
                !string.Equals(ReadParameter(action, "commitment_ids_json"), JsonSerializer.Serialize(demand.CommitmentIds), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "collection_path_required"), Lower(demand.CollectionPathRequired), StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "collection_path_source"), demand.CollectionPathSource, StringComparison.Ordinal))
            {
                reasons.Add("craft_machine_item_demand_projection_drifted");
            }
            if (!reservationGuard.Ready)
            {
                reasons.AddRange(reservationGuard.BlockingReasons);
            }
            if (!string.Equals(
                    ReadParameter(action, "material_reservation_guard_status"),
                    reservationGuard.Status,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(action, "material_reservation_ledger_id"),
                    reservationGuard.LedgerId,
                    StringComparison.Ordinal) ||
                ReadIntParameter(action, "material_reservation_ledger_revision") !=
                    reservationGuard.LedgerRevision ||
                !string.Equals(
                    ReadParameter(action, "material_reservation_ids_json"),
                    JsonSerializer.Serialize(reservationGuard.ReservationIds),
                    StringComparison.Ordinal))
            {
                reasons.Add("craft_machine_item_material_reservation_projection_drifted");
            }
            if (!string.Equals(
                    ReadParameter(
                        action,
                        "machine_craft_material_opportunity_cost_status"),
                    materialOpportunityCost.Status,
                    StringComparison.Ordinal) ||
                ReadIntParameter(
                    action,
                    "machine_craft_material_opportunity_cost") !=
                    materialOpportunityCost.TotalSaleValue)
            {
                reasons.Add(
                    "craft_machine_item_material_opportunity_cost_projection_drifted");
            }
            if (!GoalSupportMatches(
                    action,
                    expectedGoalSupport))
            {
                reasons.Add(
                    "craft_machine_item_goal_support_projection_drifted");
            }
            if (!MachineSupportIntentMatches(
                    action,
                    commitmentLedger,
                    expectedGoalSupport,
                    demand,
                    ReadString(
                        row.Value,
                        "output_qualified_item_id"),
                    ReadString(
                        row.Value,
                        "output_item_id")))
            {
                reasons.Add(
                    "craft_machine_item_support_intent_drifted");
            }

            return reasons.ToArray();
        }

        private static bool GoalSupportMatches(
            SmallModelAction action,
            ExplicitGoalSupportDemand expected)
        {
            var expectedParameters =
                ExplicitGoalSupportProjection.Parameters(
                    expected);
            return expectedParameters.All(parameter =>
                string.Equals(
                    ReadParameter(action, parameter.Name),
                    parameter.Value,
                    StringComparison.Ordinal));
        }

        private static bool MachineSupportIntentMatches(
            SmallModelAction action,
            StrategyCommitmentLedger? ledger,
            ExplicitGoalSupportDemand expectedSupport,
            MachineDemandProjection demand,
            string outputQualifiedItemId,
            string outputItemId)
        {
            var intentId = ReadParameter(
                action,
                "machine_support_intent_id");
            if (expectedSupport.Status !=
                "supported_bounded_positive_net_benefit")
            {
                return string.IsNullOrWhiteSpace(intentId);
            }

            var intent = ledger?.MachineSupportIntents
                .FirstOrDefault(row =>
                    string.Equals(
                        row.IntentId,
                        intentId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        row.Status,
                        StrategyCommitmentStatuses.Active,
                        StringComparison.Ordinal));
            return intent is not null &&
                ReadIntParameter(
                    action,
                    "machine_support_intent_revision") ==
                    intent.Revision &&
                string.Equals(
                    ReadParameter(
                        action,
                        "machine_support_intent_stage"),
                    intent.Stage,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(
                        action,
                        "machine_support_intent_source_state_hash"),
                    intent.SourceStateHash,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.Stage,
                    MachineSupportIntentStages.CraftSelected,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.GoalId,
                    expectedSupport.ParentGoalId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.QualifiedItemId,
                    outputQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    intent.ItemId,
                    outputItemId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.DemandClass,
                    demand.DemandClass,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.SupportKind,
                    expectedSupport.SupportKind,
                    StringComparison.Ordinal) &&
                string.Equals(
                    intent.EvidenceStatus,
                    expectedSupport.EvidenceStatus,
                    StringComparison.Ordinal) &&
                intent.GrossBenefit ==
                    expectedSupport.GrossBenefit &&
                intent.OpportunityCost ==
                    expectedSupport.OpportunityCost &&
                intent.NetBenefit ==
                    expectedSupport.NetBenefit &&
                Math.Abs(
                    intent.SupportScore -
                    expectedSupport.Score) < 0.0000001 &&
                intent.RequiredAdditionalMachineCount ==
                    demand.RequiredAdditionalMachineCount;
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

        private static JsonElement? MachineWorkbenchCraftingSource(
            JsonElement row,
            string? accessPointId)
        {
            if (string.IsNullOrWhiteSpace(accessPointId) ||
                !row.TryGetProperty("workbench_crafting_sources", out var sources) ||
                sources.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var source in sources.EnumerateArray())
            {
                if (source.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(source, "workbench_access_point_id"),
                        accessPointId,
                        StringComparison.Ordinal))
                {
                    return source;
                }
            }
            return null;
        }
    }
}
