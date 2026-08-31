using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> StoryEventSteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "advance_story_event", 0),
                Kind = "advance_story_event",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "same_live_event_command_and_dialogue_boundary=true"
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_Event_Update_and_DialogueBox_input_only",
                    "never_call_skipEvent_or_write_event_command_state",
                    "stop_at_next_unbound_choice_minigame_or_player_control_boundary"
                },
                FailurePolicy = new[] { "release_input_then_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
