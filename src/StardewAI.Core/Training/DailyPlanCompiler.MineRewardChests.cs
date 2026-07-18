using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> ClaimMineRewardChestSteps(PolicyEventCandidatePrediction candidate)
        {
            var stand = ParseCoordinate(candidate.ExpectedEffect, "stand_tile=");
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue || !stand.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "claim_mine_reward_chest", 0),
                    Kind = "claim_mine_reward_chest",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "exact_live_mineshaft_reward_chest_still_ready=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "one_native_reward_open_then_wait_for_dumpContents", "empty_chest_cleanup_only_after_items_clear", "no_claim_click_while_open_chest_still_contains_reward", "no_direct_item_experience_mail_or_stamina_mutation" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("stand_tile_x", stand.Value.X.ToString()),
                        Parameter("stand_tile_y", stand.Value.Y.ToString()),
                        Parameter("target_runtime_type", "StardewValley.Objects.Chest"),
                        Parameter("reward_branch", ParseValue(candidate.ExpectedEffect, "reward_branch=")),
                        Parameter("qualified_item_id", candidate.QualifiedItemId),
                        Parameter("quantity", Math.Max(1, candidate.Quantity).ToString()),
                        Parameter("expected_output_quality", CandidateParameter(candidate, "expected_output_quality")),
                        Parameter("expected_output_items_json", CandidateParameter(candidate, "expected_output_items_json")),
                        Parameter("expected_skill_id", "luck"),
                        Parameter("expected_skill_experience_delta", ParseValue(candidate.ExpectedEffect, "expected_luck_experience_delta=")),
                        Parameter("native_gain_experience_call_amount", ParseValue(candidate.ExpectedEffect, "native_gain_experience_call_amount=")),
                        Parameter("expected_stardrop_max_stamina_delta", ParseValue(candidate.ExpectedEffect, "expected_stardrop_max_stamina_delta=")),
                        Parameter("is_stardrop", CandidateParameter(candidate, "is_stardrop")),
                        Parameter("native_contract", "one_reward_open_then_wait_dumpContents_then_empty_chest_cleanup_checkAction"),
                        Parameter("expected_action_type", "MineRewardChest"),
                        Parameter("interaction_kind", "overlay_object"),
                        Parameter("max_movement_tiles", "512")
                    }
                }
            };
        }
    }
}
