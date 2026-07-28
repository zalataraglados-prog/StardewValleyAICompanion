using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> ClearObstacleTileSteps(PolicyEventCandidatePrediction candidate)
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
                    StepId = StepId(candidate, "move_to_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var maxToolSwings = ParseValue(candidate.ExpectedEffect, "max_tool_swings=");
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("max_tool_swings", string.IsNullOrWhiteSpace(maxToolSwings) ? "8" : maxToolSwings)
            };
            var toolSlotIndex = ParseValue(candidate.ExpectedEffect, "tool_slot_index=");
            var requiredToolKind = ParseValue(candidate.ExpectedEffect, "required_tool_kind=");
            if (!string.IsNullOrWhiteSpace(toolSlotIndex))
            {
                parameters.Add(Parameter("tool_slot_index", toolSlotIndex));
            }
            if (!string.IsNullOrWhiteSpace(requiredToolKind))
            {
                parameters.Add(Parameter("required_tool_kind", requiredToolKind));
            }
            foreach (var name in new[]
            {
                "clear_output_projection_status",
                "clear_output_items_json",
                "clear_output_qualified_item_id",
                "clear_output_quantity_min",
                "clear_output_quantity_max",
                "clear_bonus_output_qualified_item_id",
                "clear_bonus_output_quantity_min",
                "clear_bonus_output_quantity_max",
                "artifact_spots_dug_before",
                "artifact_spots_dug_delta",
                "artifact_spots_dug_expected_after",
                "clear_terrain_feature_expected_after",
                "defense_book_mail_before",
                "defense_book_mail_expected_after"
            })
            {
                var value = ParseValue(candidate.ExpectedEffect, name + "=");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }
            AddSkillExperienceParameters(parameters, candidate.ExpectedEffect);
            parameters.AddRange(candidate.Parameters.Where(parameter =>
                parameter.Name.StartsWith("quest_", StringComparison.Ordinal)));

            steps.Add(
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "clear_obstacle", 1),
                    Kind = "clear_obstacle",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "target_obstacle_clearable=true", "target_tile_adjacent=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_obstacle_from_transparent_location_state", "executor_requires_adjacent_target" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> ClearFarmResourceClumpSteps(PolicyEventCandidatePrediction candidate)
        {
            var anchor = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_tile=");
            var hitTile = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_hit_tile=");
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "resource_clump_stand_tile=");
            if (!anchor.HasValue || !hitTile.HasValue || !standTile.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("stand_tile_x", standTile.Value.X.ToString()),
                Parameter("stand_tile_y", standTile.Value.Y.ToString()),
                Parameter("resource_clump_tile_x", anchor.Value.X.ToString()),
                Parameter("resource_clump_tile_y", anchor.Value.Y.ToString()),
                Parameter("resource_clump_width", ParseValue(candidate.ExpectedEffect, "resource_clump_width=")),
                Parameter("resource_clump_height", ParseValue(candidate.ExpectedEffect, "resource_clump_height=")),
                Parameter("resource_clump_parent_sheet_index", ParseValue(candidate.ExpectedEffect, "resource_clump_parent_sheet_index=")),
                Parameter("tool_slot_index", ParseValue(candidate.ExpectedEffect, "tool_slot_index=")),
                Parameter("required_tool_kind", ParseValue(candidate.ExpectedEffect, "required_tool_kind=")),
                Parameter("max_tool_swings", ParseValue(candidate.ExpectedEffect, "max_tool_swings=")),
                Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
            };
            AddSkillExperienceParameters(parameters, candidate.ExpectedEffect);
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "break_farm_resource_clump", 0),
                    Kind = "break_farm_resource_clump",
                    TargetLocation = "Farm",
                    TargetTileX = hitTile.Value.X,
                    TargetTileY = hitTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm_resource_clump_identity_still_matches=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "native_cross_frame_tool_lifecycle", "transparent_perimeter_stand_tile", "no_direct_resource_clump_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> PlantSeedTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var seedId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "seed_id=");
            if (string.IsNullOrWhiteSpace(seedId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("seed_id", seedId)
            };
            if (!string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", candidate.QualifiedItemId));
            }
            if (candidate.SlotIndex.HasValue)
            {
                parameters.Add(Parameter("slot_index", candidate.SlotIndex.Value.ToString()));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("seed_stack_available", candidate.Quantity.ToString()));
            }
            var adjustedGrowDays = ParseValue(candidate.ExpectedEffect, "adjusted_grow_days=");
            var daysRemaining = ParseValue(candidate.ExpectedEffect, "days_remaining_in_season=");
            var harvestItemId = ParseValue(candidate.ExpectedEffect, "harvest_item_id=");
            var harvestItemQualifiedId = ParseValue(candidate.ExpectedEffect, "harvest_item_qualified_id=");
            var harvestUnitSalePrice = ParseValue(candidate.ExpectedEffect, "harvest_unit_sale_price=");
            var harvestMinStack = ParseValue(candidate.ExpectedEffect, "harvest_min_stack=");
            var harvestMaxStack = ParseValue(candidate.ExpectedEffect, "harvest_max_stack=");
            var harvestMaxIncreasePerFarmingLevel = ParseValue(candidate.ExpectedEffect, "harvest_max_increase_per_farming_level=");
            var extraHarvestChance = ParseValue(candidate.ExpectedEffect, "extra_harvest_chance=");
            var harvestMinQuality = ParseValue(candidate.ExpectedEffect, "harvest_min_quality=");
            var harvestMaxQuality = ParseValue(candidate.ExpectedEffect, "harvest_max_quality=");
            var harvestMethod = ParseValue(candidate.ExpectedEffect, "harvest_method=");
            var regrowDays = ParseValue(candidate.ExpectedEffect, "regrow_days=");
            var expectedFirstHarvestValue = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_value=");
            var expectedFirstHarvestQuantity = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_quantity=");
            var expectedFirstHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_value_basis=");
            var estimatedFirstHarvestQuantity = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_quantity=");
            var estimatedFirstHarvestValue = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_value=");
            var estimatedFirstHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_value_basis=");
            var estimatedRegrowHarvestCount = ParseValue(candidate.ExpectedEffect, "estimated_regrow_harvest_count=");
            var estimatedTotalHarvestCount = ParseValue(candidate.ExpectedEffect, "estimated_total_harvest_count=");
            var expectedSeasonHarvestValue = ParseValue(candidate.ExpectedEffect, "expected_season_harvest_value=");
            var estimatedSeasonHarvestValue = ParseValue(candidate.ExpectedEffect, "estimated_season_harvest_value=");
            var seedUnitCost = ParseValue(candidate.ExpectedEffect, "seed_unit_cost=");
            var expectedFirstHarvestNetValue = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_net_value=");
            var estimatedFirstHarvestNetValue = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_net_value=");
            var expectedSeasonHarvestNetValue = ParseValue(candidate.ExpectedEffect, "expected_season_harvest_net_value=");
            var estimatedSeasonHarvestNetValue = ParseValue(candidate.ExpectedEffect, "estimated_season_harvest_net_value=");
            var seasonHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "season_harvest_value_basis=");
            var regrowEstimateBasis = ParseValue(candidate.ExpectedEffect, "regrow_estimate_basis=");
            var netValueBasis = ParseValue(candidate.ExpectedEffect, "net_value_basis=");
            if (!string.IsNullOrWhiteSpace(adjustedGrowDays))
            {
                parameters.Add(Parameter("adjusted_grow_days", adjustedGrowDays));
            }
            if (!string.IsNullOrWhiteSpace(daysRemaining))
            {
                parameters.Add(Parameter("days_remaining_in_season", daysRemaining));
            }
            if (int.TryParse(adjustedGrowDays, out var growDays) &&
                int.TryParse(daysRemaining, out var remainingDays))
            {
                parameters.Add(Parameter("maturity_slack_days", (remainingDays - growDays).ToString()));
            }
            if (!string.IsNullOrWhiteSpace(harvestItemId))
            {
                parameters.Add(Parameter("harvest_item_id", harvestItemId));
            }
            if (!string.IsNullOrWhiteSpace(harvestItemQualifiedId))
            {
                parameters.Add(Parameter("harvest_item_qualified_id", harvestItemQualifiedId));
            }
            if (!string.IsNullOrWhiteSpace(harvestUnitSalePrice))
            {
                parameters.Add(Parameter("harvest_unit_sale_price", harvestUnitSalePrice));
            }
            if (!string.IsNullOrWhiteSpace(harvestMinStack))
            {
                parameters.Add(Parameter("harvest_min_stack", harvestMinStack));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxStack))
            {
                parameters.Add(Parameter("harvest_max_stack", harvestMaxStack));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxIncreasePerFarmingLevel))
            {
                parameters.Add(Parameter("harvest_max_increase_per_farming_level", harvestMaxIncreasePerFarmingLevel));
            }
            if (!string.IsNullOrWhiteSpace(extraHarvestChance))
            {
                parameters.Add(Parameter("extra_harvest_chance", extraHarvestChance));
            }
            if (!string.IsNullOrWhiteSpace(harvestMinQuality))
            {
                parameters.Add(Parameter("harvest_min_quality", harvestMinQuality));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxQuality))
            {
                parameters.Add(Parameter("harvest_max_quality", harvestMaxQuality));
            }
            if (!string.IsNullOrWhiteSpace(harvestMethod))
            {
                parameters.Add(Parameter("harvest_method", harvestMethod));
            }
            if (!string.IsNullOrWhiteSpace(regrowDays))
            {
                parameters.Add(Parameter("regrow_days", regrowDays));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestValue))
            {
                parameters.Add(Parameter("expected_first_harvest_value", expectedFirstHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestQuantity))
            {
                parameters.Add(Parameter("expected_first_harvest_quantity", expectedFirstHarvestQuantity));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestValueBasis))
            {
                parameters.Add(Parameter("expected_first_harvest_value_basis", expectedFirstHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestQuantity))
            {
                parameters.Add(Parameter("estimated_first_harvest_quantity", estimatedFirstHarvestQuantity));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestValue))
            {
                parameters.Add(Parameter("estimated_first_harvest_value", estimatedFirstHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestValueBasis))
            {
                parameters.Add(Parameter("estimated_first_harvest_value_basis", estimatedFirstHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(estimatedRegrowHarvestCount))
            {
                parameters.Add(Parameter("estimated_regrow_harvest_count", estimatedRegrowHarvestCount));
            }
            if (!string.IsNullOrWhiteSpace(estimatedTotalHarvestCount))
            {
                parameters.Add(Parameter("estimated_total_harvest_count", estimatedTotalHarvestCount));
            }
            if (!string.IsNullOrWhiteSpace(expectedSeasonHarvestValue))
            {
                parameters.Add(Parameter("expected_season_harvest_value", expectedSeasonHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedSeasonHarvestValue))
            {
                parameters.Add(Parameter("estimated_season_harvest_value", estimatedSeasonHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(seedUnitCost))
            {
                parameters.Add(Parameter("seed_unit_cost", seedUnitCost));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestNetValue))
            {
                parameters.Add(Parameter("expected_first_harvest_net_value", expectedFirstHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestNetValue))
            {
                parameters.Add(Parameter("estimated_first_harvest_net_value", estimatedFirstHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(expectedSeasonHarvestNetValue))
            {
                parameters.Add(Parameter("expected_season_harvest_net_value", expectedSeasonHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedSeasonHarvestNetValue))
            {
                parameters.Add(Parameter("estimated_season_harvest_net_value", estimatedSeasonHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(seasonHarvestValueBasis))
            {
                parameters.Add(Parameter("season_harvest_value_basis", seasonHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(regrowEstimateBasis))
            {
                parameters.Add(Parameter("regrow_estimate_basis", regrowEstimateBasis));
            }
            if (!string.IsNullOrWhiteSpace(netValueBasis))
            {
                parameters.Add(Parameter("net_value_basis", netValueBasis));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "plant_seed", 0),
                    Kind = "plant_seed",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "hard_rule_allows_planting=true", "seed_inventory_contains=" + seedId },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_seed_tile_from_transparent_planting_context", "single_tile_single_seed_slice", "maturity_timing_from_transparent_planting_context", "harvest_value_from_transparent_crop_catalog_when_present" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

    }
}
