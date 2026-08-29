using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private const string WildTreeProductNativeContract =
        "GameLocation.checkAction -> Tree.performUseAction -> Tree.shake; exact base Data/WildTrees seed branch; no direct tree, RNG, debris, inventory, or skill mutation";

    private static IEnumerable<SmallModelPlanStep> HarvestWildTreeProductSteps(PolicyEventCandidatePrediction candidate)
    {
        var stand = ParseCoordinate(candidate.ExpectedEffect, "tree_product_stand_tile=");
        var interaction = ParseCoordinate(candidate.ExpectedEffect, "tree_product_interaction_tile=");
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue || !interaction.HasValue)
            return Array.Empty<SmallModelPlanStep>();

        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "harvest_tree_product", 0),
                Kind = "harvest_tree_product",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_wild_tree_product_projection_still_ready=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_checkAction_Tree.performUseAction_and_shake_only",
                    "complete_random_output_domain_without_rng_consumption",
                    "empty_toolbar_slot_then_restore",
                    "no_direct_tree_rng_debris_inventory_or_skill_mutation"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                {
                    Parameter("interaction_tile_x", interaction.Value.X.ToString()), Parameter("interaction_tile_y", interaction.Value.Y.ToString()),
                    Parameter("stand_tile_x", stand.Value.X.ToString()), Parameter("stand_tile_y", stand.Value.Y.ToString()),
                    Parameter("target_runtime_type", "StardewValley.TerrainFeatures.Tree"),
                    Parameter("tree_product_tree_type", ParseValue(candidate.ExpectedEffect, "tree_product_tree_type=")),
                    Parameter("qualified_item_id", candidate.QualifiedItemId), Parameter("quantity", "1"),
                    Parameter("expected_tree_has_seed_before", ParseValue(candidate.ExpectedEffect, "expected_tree_has_seed_before=")),
                    Parameter("expected_tree_has_seed_after", ParseValue(candidate.ExpectedEffect, "expected_tree_has_seed_after=")),
                    Parameter("expected_tree_was_shaken_today_before", ParseValue(candidate.ExpectedEffect, "expected_tree_was_shaken_today_before=")),
                    Parameter("expected_tree_was_shaken_today_after", ParseValue(candidate.ExpectedEffect, "expected_tree_was_shaken_today_after=")),
                    Parameter("expected_output_items_json", ParseValue(candidate.ExpectedEffect, "expected_output_items_json=")),
                    Parameter("tree_product_output_domain_json", ParseValue(candidate.ExpectedEffect, "tree_product_output_domain_json=")),
                    Parameter("tree_product_output_domain_contract", ParseValue(candidate.ExpectedEffect, "tree_product_output_domain_contract=")),
                    Parameter("expected_foraging_experience_delta", ParseValue(candidate.ExpectedEffect, "expected_foraging_experience_delta=")),
                    Parameter("safe_slot_index", ParseValue(candidate.ExpectedEffect, "safe_slot_index=")), Parameter("safe_slot_kind", "empty"),
                    Parameter("restore_slot_index", ParseValue(candidate.ExpectedEffect, "restore_slot_index=")),
                    Parameter("tree_product_projection_status", ParseValue(candidate.ExpectedEffect, "tree_product_projection_status=")),
                    Parameter("tree_product_native_contract", WildTreeProductNativeContract),
                    Parameter("max_movement_tiles", ParseValue(candidate.ExpectedEffect, "max_movement_tiles="))
                }.Concat(candidate.Parameters.Where(parameter => parameter.Name.StartsWith("quest_", StringComparison.Ordinal))).ToArray()
            }
        };
    }
}
