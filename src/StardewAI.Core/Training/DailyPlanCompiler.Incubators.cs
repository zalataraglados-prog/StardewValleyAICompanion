using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep>
            NameHatchedAnimalSteps(
                PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue ||
                !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(
                        candidate,
                        "name_hatched_animal",
                        0),
                    Kind = "name_hatched_animal",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "menus.active_menu.type=NamingMenu",
                        "incubator.native_ready_selected=true",
                        "animal_house.has_capacity=true"
                    },
                    ExpectedEffects = new[]
                    {
                        candidate.ExpectedEffect
                    },
                    SafetyConstraints = new[]
                    {
                        "native_naming_menu_confirm_only",
                        "target_rebound_to_native_first_ready_incubator",
                        "no_direct_animal_or_machine_state_mutation"
                    },
                    FailurePolicy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    Parameters = new[]
                    {
                        Parameter(
                            "machine_location_id",
                            CandidateParameter(
                                candidate,
                                "machine_location_id")),
                        Parameter(
                            "held_egg_qualified_item_id",
                            CandidateParameter(
                                candidate,
                                "held_egg_qualified_item_id")),
                        Parameter(
                            "target_runtime_type",
                            CandidateParameter(
                                candidate,
                                "target_runtime_type")),
                        Parameter(
                            "target_name",
                            CandidateParameter(
                                candidate,
                                "target_name")),
                        Parameter(
                            "animal_house_occupant_count_before",
                            CandidateParameter(
                                candidate,
                                "animal_house_occupant_count_before")),
                        Parameter(
                            "animal_house_occupant_limit",
                            CandidateParameter(
                                candidate,
                                "animal_house_occupant_limit")),
                        Parameter(
                            "machine_special_prediction_model_id",
                            CandidateParameter(
                                candidate,
                                "machine_special_prediction_model_id")),
                        Parameter(
                            "native_ready_selection_ordinal",
                            CandidateParameter(
                                candidate,
                                "native_ready_selection_ordinal")),
                        Parameter(
                            "native_contract",
                            CandidateParameter(
                                candidate,
                                "native_contract"))
                    }
                }
            };
        }
    }
}
