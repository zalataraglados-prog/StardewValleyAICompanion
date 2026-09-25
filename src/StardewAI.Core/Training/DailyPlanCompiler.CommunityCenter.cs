using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> CommunityCenterFirstNoteSteps(
        PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "interact", 0),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "community_center_lifecycle_stage=first_junimo_note_pending"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_CommunityCenter_checkAction_only",
                    "native_JunimoNoteMenu_setUpMenu_only",
                    "no_direct_mail_quest_bundle_or_event_mutation"
                },
                FailurePolicy = new[]
                {
                    "release_mutex_refresh_snapshot_and_replan"
                },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> CommunityCenterDonationSteps(PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "donate_community_center_item", 0),
                Kind = "donate_community_center_item",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "community_center_bundle_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "route_state_must_allow_community_center",
                    "native_CommunityCenter_checkBundle_only",
                    "native_JunimoNoteMenu_receiveLeftClick_only",
                    "donate_exactly_one_verified_bundle_ingredient",
                    "no_direct_bundle_inventory_reward_mail_or_route_mutation"
                },
                FailurePolicy = new[] { "close_junimo_note_menu_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> CommunityCenterRewardSteps(
        PolicyEventCandidatePrediction candidate)
    {
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "claim_community_center_bundle_reward", 0),
                Kind = "claim_community_center_bundle_reward",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "community_center_bundle_reward_projection_still_matches=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_JunimoNoteMenu_or_MissedRewards_entry_only",
                    "native_ItemGrabMenu_exact_bundle_reward_click_only",
                    "no_direct_bundle_reward_or_inventory_mutation"
                },
                FailurePolicy = new[]
                {
                    "close_native_reward_menu_refresh_snapshot_and_replan"
                },
                Parameters = candidate.Parameters
            }
        };
    }
}
