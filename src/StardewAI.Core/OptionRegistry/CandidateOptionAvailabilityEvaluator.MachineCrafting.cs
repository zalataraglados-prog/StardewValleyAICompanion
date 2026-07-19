using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static EventCandidate[] MachineCraftingCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var context = ReadStateFieldValue(snapshot, "player", "machine_crafting");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => BuildMachineCraftingCandidate(snapshot, row, commitmentLedger))
                .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private static EventCandidate BuildMachineCraftingCandidate(
            SnapshotEnvelope snapshot,
            JsonElement row,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var recipeName = ReadString(row, "recipe_name");
            var outputQualifiedId = ReadString(row, "output_qualified_item_id");
            var outputItemId = ReadString(row, "output_item_id");
            var outputCount = Math.Max(1, ReadInt(row, "output_count_per_craft", 1));
            var timesCrafted = Math.Max(0, ReadInt(row, "times_crafted"));
            var ingredientRowsJson = row.TryGetProperty("ingredient_rows", out var ingredientRows)
                ? ingredientRows.GetRawText()
                : "[]";
            var demand = MachineDemandProjectionEvaluator.Evaluate(snapshot, row, commitmentLedger);
            var blockReasons = new List<string>();
            if (!string.Equals(ReadString(row, "craft_candidate_status"), "ready_for_native_personal_crafting_menu", StringComparison.Ordinal))
            {
                blockReasons.Add("machine_recipe_not_ready_for_native_personal_crafting");
            }
            if (ReadBool(row, "output_inventory_acceptance_after_material_consumption") != true)
            {
                blockReasons.Add("machine_recipe_output_cannot_fit_after_material_consumption");
            }
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                blockReasons.Add("machine_crafting_menu_must_be_clear");
            }
            if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(outputQualifiedId))
            {
                blockReasons.Add("machine_recipe_identity_unavailable");
            }
            if (!demand.HasDemand)
            {
                blockReasons.Add(demand.DemandClass == "blocked_incomplete_capacity_horizon"
                    ? "machine_capacity_horizon_incomplete"
                    : demand.DemandClass == "deferred_until_latest_build_window"
                        ? "machine_build_deferred_too_early"
                        : "machine_recipe_has_no_proven_task_production_or_collection_requirement");
            }

            return new EventCandidate
            {
                CandidateId = "machine-craft:" + recipeName + ":" + outputQualifiedId,
                Kind = "craft_machine_item",
                Available = blockReasons.Count == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "player.inventory.materials_consumed_by_native_recipe=true" +
                    ";recipe_name=" + recipeName +
                    ";output_qualified_item_id=" + outputQualifiedId +
                    ";output_item_id=" + outputItemId +
                    ";output_count=" + outputCount +
                    ";times_crafted_before=" + timesCrafted +
                    ";times_crafted_after=" + (timesCrafted + outputCount) +
                    ";machine_demand_class=" + demand.DemandClass +
                    ";machine_scale=" + demand.MachineScale +
                    ";machine_horizon_status=" + demand.HorizonStatus +
                    ";machine_timing_status=" + demand.TimingStatus +
                    ";machine_demand_priority=" + demand.Priority +
                    ";next_arrival_source=" + demand.NextArrivalSource +
                    ";commitment_ledger_revision=" + demand.CommitmentLedgerRevision +
                    ";priority_task_required=" + demand.PriorityTaskRequired.ToString().ToLowerInvariant() +
                    ";production_capacity_required=" + demand.ProductionCapacityRequired.ToString().ToLowerInvariant() +
                    ";collection_path_required=" + demand.CollectionPathRequired.ToString().ToLowerInvariant() +
                    ";native_contract=CraftingPage.receiveLeftClick",
                ItemId = outputItemId,
                QualifiedItemId = outputQualifiedId,
                Quantity = outputCount,
                EstimatedTicks = 30,
                EnergyCost = 0,
                AvailabilityClass = "transparent_machine_recipe_native_personal_crafting",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("recipe_name", recipeName),
                    Parameter("output_qualified_item_id", outputQualifiedId),
                    Parameter("output_item_id", outputItemId),
                    Parameter("output_count", outputCount.ToString()),
                    Parameter("times_crafted_before", timesCrafted.ToString()),
                    Parameter("ingredient_rows_json", ingredientRowsJson),
                    Parameter("crafting_source", "native_personal_crafting_menu"),
                    Parameter("machine_demand_class", demand.DemandClass),
                    Parameter("machine_scale", demand.MachineScale),
                    Parameter("machine_horizon_status", demand.HorizonStatus),
                    Parameter("machine_timing_status", demand.TimingStatus),
                    Parameter("machine_demand_priority", demand.Priority.ToString()),
                    Parameter("priority_task_required", demand.PriorityTaskRequired.ToString().ToLowerInvariant()),
                    Parameter("priority_task_sources_json", JsonSerializer.Serialize(demand.PriorityTaskSources)),
                    Parameter("production_capacity_required", demand.ProductionCapacityRequired.ToString().ToLowerInvariant()),
                    Parameter("potential_input_count", demand.PotentialInputCount.ToString()),
                    Parameter("backlog_input_units", demand.BacklogInputUnits.ToString()),
                    Parameter("placed_same_machine_count", demand.PlacedSameMachineCount.ToString()),
                    Parameter("idle_same_machine_count", demand.IdleSameMachineCount.ToString()),
                    Parameter("process_cycle_minutes", demand.ProcessCycleMinutes.ToString()),
                    Parameter("next_arrival_days", demand.NextArrivalDays.ToString()),
                    Parameter("next_arrival_units", demand.NextArrivalUnits.ToString()),
                    Parameter("next_arrival_service_interval_days", demand.NextArrivalServiceIntervalDays.ToString()),
                    Parameter("capacity_before_next_arrival", demand.CapacityBeforeNextArrival.ToString()),
                    Parameter("capacity_deficit_units", demand.CapacityDeficitUnits.ToString()),
                    Parameter("capacity_between_arrival_waves", demand.CapacityBetweenArrivalWaves.ToString()),
                    Parameter("arrival_wave_capacity_deficit_units", demand.ArrivalWaveCapacityDeficitUnits.ToString()),
                    Parameter("required_additional_machine_count", demand.RequiredAdditionalMachineCount.ToString()),
                    Parameter("latest_build_lead_minutes", demand.LatestBuildLeadMinutes.ToString()),
                    Parameter("minutes_until_next_arrival", demand.MinutesUntilNextArrival.ToString()),
                    Parameter("machine_build_window_open", demand.BuildWindowOpen.ToString().ToLowerInvariant()),
                    Parameter("next_arrival_source", demand.NextArrivalSource),
                    Parameter("commitment_ledger_id", demand.CommitmentLedgerId),
                    Parameter("commitment_ledger_revision", demand.CommitmentLedgerRevision.ToString()),
                    Parameter("commitment_ids_json", JsonSerializer.Serialize(demand.CommitmentIds)),
                    Parameter("collection_path_required", demand.CollectionPathRequired.ToString().ToLowerInvariant()),
                    Parameter("collection_path_source", demand.CollectionPathSource)
                }
            };
        }
    }
}
