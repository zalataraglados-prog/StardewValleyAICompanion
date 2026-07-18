using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> CollectMachineOutputSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_machine_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var parameters = new List<SmallModelActionParameter>();
            parameters.Add(Parameter("machine_location_id", string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId));
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("quantity", candidate.Quantity.ToString()));
            }
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_stack");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_value_basis");
            parameters.Add(Parameter("machine_harvest_experience_raw", CandidateParameter(candidate, "machine_harvest_experience_raw")));
            parameters.Add(Parameter("expected_skill_experience_deltas_json", CandidateParameter(candidate, "expected_skill_experience_deltas_json")));
            parameters.Add(Parameter("expected_mastery_experience_delta", CandidateParameter(candidate, "expected_mastery_experience_delta")));
            parameters.Add(Parameter("skill_experience_projection_status", CandidateParameter(candidate, "skill_experience_projection_status")));
            parameters.Add(Parameter("skill_experience_condition", CandidateParameter(candidate, "skill_experience_condition")));
            foreach (var name in new[] { "skill_experience_skill_id", "skill_experience_on_success_min", "skill_experience_on_success_max" })
            {
                var value = CandidateParameter(candidate, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_machine_output", 1),
                Kind = "collect_machine_output",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.machines.target_ready=true", "player.inventory_can_accept=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "target_machine_from_transparent_farm_state", "runtime_verified_machine_output_collect" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> LoadMachineInputSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_machine_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var slotIndex = candidate.SlotIndex >= 0
                ? candidate.SlotIndex.ToString()
                : ParseValue(candidate.ExpectedEffect, "input_slot_index=");
            var parameters = new List<SmallModelActionParameter>();
            parameters.Add(Parameter("machine_location_id", string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId));
            if (!string.IsNullOrWhiteSpace(slotIndex))
            {
                parameters.Add(Parameter("input_slot_index", slotIndex));
            }

            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("input_stack_available", candidate.Quantity.ToString()));
            }
            AddParsedParameter(parameters, candidate.ExpectedEffect, "input_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_opportunity_cost");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_value_basis");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_output_rule_count");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_has_output_rule");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_output_prediction_status");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_qualified_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_stack");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_price_source");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_items");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_available");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_net_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_rule_required_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_rule_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_preserve_type");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_preserved_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_minutes_until_ready");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_probe_source");

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "load_machine_input", 1),
                Kind = "load_machine_input",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.machines.target_accepts_input_probe=true", "player.inventory_slot_contains_input=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "target_machine_input_from_transparent_probe", "runtime_verified_machine_input_load" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

    }
}
