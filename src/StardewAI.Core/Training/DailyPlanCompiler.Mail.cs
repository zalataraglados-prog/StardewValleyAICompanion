using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> MailboxApproachSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "mailbox_approach", 0),
                Kind = "move_to_tile",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "mailbox_first_identity_matches=true" },
                ExpectedEffects = new[] { "player_at_owned_mailbox_stand_tile=true", "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_collision_path_only", "do_not_warp", "owned_mailbox_only" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> OpenMailboxLetterSteps(PolicyEventCandidatePrediction candidate)
    {
        if (!candidate.TileX.HasValue || !candidate.TileY.HasValue ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_x"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            !int.TryParse(CandidateParameter(candidate, "stand_tile_y"), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "target_runtime_identity")))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "open_mailbox_letter", 0),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "player_at_owned_mailbox_stand_tile=true",
                    "mailbox_first_identity_matches=true",
                    "attachment_capacity_sufficient=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect, "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=Mailbox", "GameLocation.mailbox_only", "do_not_mutate_mail_state_directly" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> ProcessOpenLetterSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "target_runtime_identity")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "mail_menu_identity_sha256")))
        {
            return Array.Empty<SmallModelPlanStep>();
        }
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "process_open_letter", 0),
                Kind = "close_menu",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "menus.active_menu.type=LetterViewerMenu",
                    "mail_menu_identity_matches=true",
                    "attachment_capacity_sufficient=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect, "fresh_snapshot_replan_required=true" },
                SafetyConstraints = new[] { "native_LetterViewerMenu_receiveLeftClick_only", "reuse_executor.close_menu", "do_not_mutate_mail_inventory_recipe_money_quest_or_special_order_state_directly" },
                FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }

    private static IEnumerable<SmallModelPlanStep> MailInputWaitSteps(
        PolicyEventCandidatePrediction candidate)
    {
        var waitTicks = Math.Clamp(
            CandidateInt(candidate, "retry_wait_ticks") ?? 30,
            1,
            MaxWaitTicksPerStep);
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "mail_input_wait", 0),
                Kind = "wait_ticks",
                WaitTicks = waitTicks,
                EstimatedMinutes = TicksToMinutes(waitTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "menus.active_menu.type=LetterViewerMenu",
                    "same_mail_menu_identity=true"
                },
                ExpectedEffects = new[]
                {
                    "letter_viewer.can_receive_input=true",
                    "fresh_snapshot_replan_required=true"
                },
                SafetyConstraints = new[]
                {
                    "native_menu_timer_only",
                    "do_not_mutate_mail_or_menu_state"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
    }
}
