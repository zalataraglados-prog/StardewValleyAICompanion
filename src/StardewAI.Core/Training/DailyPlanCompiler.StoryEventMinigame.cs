using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> StoryEventMinigameSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "advance_story_event_minigame", 0),
                Kind = "advance_story_event_minigame",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "same_live_currentMinigame_instance_type_and_event_boundary=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "Game1_native_minigame_tick_and_Event_Update_own_progress",
                    "only_exact_compiler_bound_DialogueBox_input_is_injected",
                    "never_call_tick_forceQuit_or_write_event_or_minigame_state",
                    "stop_at_minigame_end_instance_change_or_fresh_decision"
                },
                FailurePolicy = new[] { "release_input_then_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
