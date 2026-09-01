using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class EffectiveDecisionArtifactTrackerTests
{
    [Fact]
    public void DispatchReplanReplacementAppliesBeforeExecution()
    {
        var tracker = new EffectiveDecisionArtifactTracker(
            "initial-plan.json",
            "initial-ranking.json",
            "initial-queue.json",
            "hash.initial");
        tracker.Replace(
            "dispatch-plan.json",
            "dispatch-ranking.json",
            "dispatch-queue.json",
            "hash.dispatch");
        var execution = new JsonObject();

        tracker.Stamp(execution);

        Assert.Equal("dispatch-plan.json", execution["effective_model_plan_path"]!.GetValue<string>());
        Assert.Equal("dispatch-ranking.json", execution["effective_ranking_path"]!.GetValue<string>());
        Assert.Equal("dispatch-queue.json", execution["effective_compiled_queue_path"]!.GetValue<string>());
        Assert.Equal("hash.dispatch", execution["effective_decision_source_state_hash"]!.GetValue<string>());
        Assert.Equal(1, execution["effective_decision_revision"]!.GetValue<int>());
    }

    [Fact]
    public void PostActionReplanReplacementDoesNotRelabelCompletedAction()
    {
        var tracker = new EffectiveDecisionArtifactTracker(
            "initial-plan.json",
            "initial-ranking.json",
            "initial-queue.json",
            "hash.initial");
        var completedExecution = new JsonObject();
        tracker.Stamp(completedExecution);

        tracker.Replace(
            "next-plan.json",
            "next-ranking.json",
            "next-queue.json",
            "hash.next");
        var nextExecution = new JsonObject();
        tracker.Stamp(nextExecution);

        Assert.Equal("initial-ranking.json", completedExecution["effective_ranking_path"]!.GetValue<string>());
        Assert.Equal("hash.initial", completedExecution["effective_decision_source_state_hash"]!.GetValue<string>());
        Assert.Equal("next-ranking.json", nextExecution["effective_ranking_path"]!.GetValue<string>());
        Assert.Equal("hash.next", nextExecution["effective_decision_source_state_hash"]!.GetValue<string>());
    }

    [Fact]
    public void CandidateIdComesFromCompiledPreconditionWithoutTruncation()
    {
        var execution = new JsonObject
        {
            ["effective_queue_item"] = new JsonObject
            {
                ["normalized_command"] = new JsonObject
                {
                    ["parameters"] = new JsonArray
                    {
                        Parameter("precondition", "target_still_exists=true"),
                        Parameter("precondition", "candidate_id:social:gift:Abigail:slot:11:(O)388:route:FarmHouse:27,31")
                    }
                }
            }
        };

        var candidateId = EffectiveDecisionArtifactTracker.ReadCandidateId(execution);

        Assert.Equal("social:gift:Abigail:slot:11:(O)388:route:FarmHouse:27,31", candidateId);
    }

    [Fact]
    public void MechanicalContinuationKeepsOriginalDecisionAndSelectedCandidate()
    {
        var tracker = new EffectiveDecisionArtifactTracker(
            "initial-plan.json",
            "initial-ranking.json",
            "initial-queue.json",
            "hash.initial",
            "before-initial.json");
        tracker.SelectCandidate("mail:process:spring_18", 0);
        var execution = new JsonObject();

        tracker.Stamp(execution);

        Assert.Equal("initial-ranking.json", execution["effective_ranking_path"]!.GetValue<string>());
        Assert.Equal("hash.initial", execution["effective_decision_source_state_hash"]!.GetValue<string>());
        Assert.Equal("before-initial.json", execution["effective_decision_snapshot_path"]!.GetValue<string>());
        Assert.Equal("mail:process:spring_18", EffectiveDecisionArtifactTracker.ReadCandidateId(execution));
        Assert.Equal(0, execution["effective_selected_queue_index"]!.GetValue<int>());
        Assert.Equal(0, execution["effective_decision_revision"]!.GetValue<int>());
    }

    private static JsonObject Parameter(string name, string value) => new()
    {
        ["name"] = name,
        ["value"] = value
    };
}
