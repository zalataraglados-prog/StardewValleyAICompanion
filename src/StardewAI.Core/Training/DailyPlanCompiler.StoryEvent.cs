using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    public SmallModelPlanEnvelope CompileNativeStoryInterrupt(
        IReadOnlyCollection<PolicyEventCandidatePrediction> candidates,
        string stateHash,
        string goalId,
        int? availableMinutes,
        int? energyBudget)
    {
        if (candidates.Any(row => row.OptionId != "story.advance_event" ||
            row.Kind is not ("advance_story_event_automatic" or "advance_story_event_choice") ||
            row.ScheduledWaitCost is not (null or 0) || row.TimelineStatus == "deferred"))
            throw new ArgumentException("Only the current native story boundary can interrupt the daily plan.");

        // A native singleplayer event pauses the normal game clock. Execute one boundary, then reobserve.
        return CompileCore(candidates, stateHash, goalId, ExecutionTargetProfiles.TrainingSingleplayer,
            1, availableMinutes, energyBudget, null, nativeStoryInterrupt: true);
    }

    private static IEnumerable<SmallModelPlanStep> StoryEventSteps(PolicyEventCandidatePrediction candidate, bool nativeStoryInterrupt) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "advance_story_event", 0),
                Kind = "advance_story_event",
                TargetLocation = candidate.LocationId,
                EstimatedMinutes = nativeStoryInterrupt ? 0 : 1,
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
