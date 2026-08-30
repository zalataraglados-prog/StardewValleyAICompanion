using System;
using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> JukeboxSelectionSteps(PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(CandidateParameter(candidate, "jukebox_track_id")) ||
            string.IsNullOrWhiteSpace(CandidateParameter(candidate, "jukebox_reason")) ||
            CandidateParameter(candidate, "confirm_jukebox_track") != "true")
            return Array.Empty<SmallModelPlanStep>();
        return new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "choose_jukebox_track", 0),
                Kind = "choose_jukebox_track",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "explicit_player_command_and_confirmation_still_authorized=true",
                    "requested_track_remains_in_exact_native_unlocked_catalog=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "player_command_only_and_excluded_from_strategy_training",
                    "compiler_rebinds_live_track_order_green_rain_gate_and_Saloon_endpoint",
                    "shared_BFS_then_native_Jukebox_action_forward_ok_cancel_input_only",
                    "no_direct_changeMusicTrack_requested_track_song_or_songsHeard_mutation",
                    "mini_jukebox_turn_off_random_and_persistent_location_state_are_out_of_scope"
                },
                FailurePolicy = new[] { "cancel_native_menu_refresh_snapshot_and_require_fresh_player_command" },
                Parameters = candidate.Parameters
            }
        };
    }
}
