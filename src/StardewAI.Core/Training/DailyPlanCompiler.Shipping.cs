using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> ShipInventoryItemToBinSteps(PolicyEventCandidatePrediction candidate)
        {
            var shippingStage = CandidateParameter(candidate, "shipping_stage");
            var slotIndexStr = CandidateParameter(candidate, "slot_index");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : CandidateParameter(candidate, "item_id");
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : CandidateParameter(candidate, "qualified_item_id");
            var quantity = candidate.Quantity > 0
                ? candidate.Quantity.ToString()
                : CandidateParameter(candidate, "quantity");

            if (!int.TryParse(slotIndexStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var slotIndex))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "route_stand_tile=");
            if (standTile.HasValue &&
                !string.Equals(shippingStage, "deposit", StringComparison.Ordinal))
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_shipping_bin", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = ShippingContinuationStepParameters(candidate)
                });
            }

            if (string.Equals(shippingStage, "approach", StringComparison.Ordinal))
            {
                return steps;
            }

            var binTile = ParseCoordinate(candidate.ExpectedEffect, "shipping_bin_tile=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(slotIndexStr))
            {
                parameters.Add(Parameter("slot_index", slotIndexStr));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(quantity))
            {
                parameters.Add(Parameter("quantity", quantity));
            }
            var expectedUnitPrice = CandidateParameter(candidate, "expected_unit_price");
            if (!string.IsNullOrWhiteSpace(expectedUnitPrice))
            {
                parameters.Add(Parameter("expected_unit_price", expectedUnitPrice));
            }

            var routeStandTileX = CandidateParameter(candidate, "route_stand_tile_x");
            var routeStandTileY = CandidateParameter(candidate, "route_stand_tile_y");
            if (!string.IsNullOrWhiteSpace(routeStandTileX))
            {
                parameters.Add(Parameter("stand_tile_x", routeStandTileX));
            }
            if (!string.IsNullOrWhiteSpace(routeStandTileY))
            {
                parameters.Add(Parameter("stand_tile_y", routeStandTileY));
            }
            parameters.AddRange(ShippingContinuationStepParameters(candidate));

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "ship_inventory_item_to_bin", standTile.HasValue ? 1 : 0),
                Kind = "ship_inventory_item_to_bin",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = binTile?.X,
                TargetTileY = binTile?.Y,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "shipping_bin_completed=true",
                    "inventory_slot_contains_item=" + slotIndex
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "target_item_from_transparent_inventory_state",
                    "shipping_bin_from_transparent_farm_state",
                    "never_ship_protected_items"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

        private static SmallModelActionParameter[] ShippingContinuationStepParameters(
            PolicyEventCandidatePrediction candidate)
        {
            return candidate.Parameters
                .Where(parameter =>
                    parameter.Name.StartsWith("continuation.", StringComparison.Ordinal) ||
                    parameter.Name.StartsWith("shipping_route.", StringComparison.Ordinal))
                .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
                .Select(group => group.Last())
                .ToArray();
        }

    }
}
