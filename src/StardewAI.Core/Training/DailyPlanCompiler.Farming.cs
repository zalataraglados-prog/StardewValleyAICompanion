using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> WaterCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "maintain_crops", 0),
                    Kind = "maintain_crops",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "crop_needs_watering=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_crop_tile_from_transparent_farm_state" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_crops", "1")
                    }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> HarvestCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var harvestItemId = ParseValue(candidate.ExpectedEffect, "harvest_item_id=");
            var harvestMethod = ParseValue(candidate.ExpectedEffect, "harvest_method=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(harvestItemId))
            {
                parameters.Add(Parameter("harvest_item_id", harvestItemId));
            }
            if (!string.IsNullOrWhiteSpace(harvestMethod))
            {
                parameters.Add(Parameter("harvest_method", harvestMethod));
            }
            AddSkillExperienceParameters(parameters, candidate.ExpectedEffect);

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_crop", 0),
                    Kind = "harvest_crop",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.crops.ready_for_harvest=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_crop_tile_from_transparent_farm_state", "runtime_verified_single_tile_harvest" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> HarvestGiantCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var giantCropId = ParseValue(candidate.ExpectedEffect, "giant_crop_id=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(giantCropId))
            {
                parameters.Add(Parameter("giant_crop_id", giantCropId));
            }
            parameters.Add(Parameter("required_tool", "axe"));
            AddSkillExperienceParameters(parameters, candidate.ExpectedEffect);

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_giant_crop", 0),
                    Kind = "harvest_giant_crop",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.resource_clumps.is_giant_crop=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_giant_crop_from_transparent_resource_clumps", "runtime_verified_multi_hit_axe_harvest" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static void AddSkillExperienceParameters(List<SmallModelActionParameter> parameters, string expectedEffect)
        {
            var names = new[]
            {
                "skill_experience_skill_id",
                "skill_experience_on_success_min",
                "skill_experience_on_success_max",
                "skill_experience_condition",
                "skill_experience_projection_status"
            };
            foreach (var name in names)
            {
                var value = ParseValue(expectedEffect, name + "=");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }
        }

        private static IEnumerable<SmallModelPlanStep> PickupDebrisItemSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var debrisIndex = ParseValue(candidate.ExpectedEffect, "debris_index=");
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(debrisIndex))
            {
                parameters.Add(Parameter("debris_index", debrisIndex));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_debris", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + candidate.TileX.Value + "," + candidate.TileY.Value },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "pickup_debris", 1),
                    Kind = "pickup_debris",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.debris.target_exists=true", "player.inventory_can_accept=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_debris_from_transparent_farm_state", "runtime_verified_debris_collect" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

    }
}
