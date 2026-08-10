using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> ConstructQuestBuildingSteps(PolicyEventCandidatePrediction candidate)
    {
        var names = new[]
        {
            "construction_building_type", "project_id", "construction_build_days", "construction_build_cost", "price",
            "construction_materials_json", "expected_money_before", "expected_money_after",
            "commitment_ledger_id", "commitment_ledger_revision", "material_reservation_guard_status",
            "material_reservation_ledger_id", "material_reservation_ledger_revision", "material_reservation_ids_json",
            "location_id", "target_tile_x", "target_tile_y", "stand_tile_x", "stand_tile_y",
            "placement_location_id", "building_tile_x", "building_tile_y", "placement_verification",
            "carpenter_action_raw", "native_contract", "quest_candidate_id", "quest_family", "quest_id",
            "quest_key", "quest_runtime_type", "quest_next_action", "quest_objective_index",
            "quest_expected_current_count", "quest_expected_target_count"
        };
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "construct_quest_building", 0),
                Kind = "construct_quest_building",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 10,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_HaveBuildingQuest_active=true", "native_blueprint_resources_and_placement_rebound=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "native_Carpenter_dialogue_and_CarpenterMenu_only", "no_direct_money_inventory_building_or_quest_mutation", "runtime_recheck_exact_placement" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = names.Select(name => Parameter(name, CandidateParameter(candidate, name))).ToArray()
            }
        };
    }
}
