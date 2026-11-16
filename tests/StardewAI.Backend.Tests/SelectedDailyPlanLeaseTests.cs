using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class SelectedDailyPlanLeaseTests
{
    [Fact]
    public void PlanAnnotationsUseTheSameLeaseOrderAndTypedContinuationWithoutMutatingInputs()
    {
        var plan = Plan();
        var ranking = Ranking();
        var before = JsonSerializer.Serialize(plan) + ranking.ToJsonString();
        var lease = SelectedQueueDecisionLease.CreateFromPlan(plan, ranking);
        var selected = Assert.Single(lease.Candidates);
        Assert.Equal(0, selected.QueueIndex);
        Assert.Equal("mail", selected.CandidateId);
        Assert.Equal("mail.process_letter", selected.OptionId);
        Assert.Equal("mail", selected.ObjectiveContinuation!["kind"]!.GetValue<string>());
        Assert.Equal("Beat_PK", selected.ObjectiveContinuation["mail_id"]!.GetValue<string>());
        Assert.Equal(before, JsonSerializer.Serialize(plan) + ranking.ToJsonString());
    }

    [Theory]
    [InlineData("compiler")]
    [InlineData("candidate")]
    [InlineData("index")]
    [InlineData("gap")]
    [InlineData("ranking")]
    [InlineData("empty")]
    public void PlanProjectionRetainsTheOriginalLeaseIdentityChecks(string fault)
    {
        var plan = Plan();
        var ranking = Ranking();
        if (fault == "compiler") plan.SourceModel = "not-the-compiler";
        if (fault == "candidate") plan.Steps[0].Preconditions = [];
        if (fault == "index") plan.Steps[0].Parameters = plan.Steps[0].Parameters.Skip(1).ToArray();
        if (fault == "gap") plan.Steps[0].Parameters[0].Value = "1";
        if (fault == "ranking") ranking["ranked_event_candidates"]!.AsArray().Add(ranking["ranked_event_candidates"]![0]!.DeepClone());
        if (fault == "empty") plan.Steps = [];
        if (fault is "compiler" or "candidate" or "index")
            Assert.Throws<InvalidDataException>(() => SelectedQueueDecisionLease.CreateFromPlan(plan, ranking));
        else Assert.Throws<InvalidOperationException>(() => SelectedQueueDecisionLease.CreateFromPlan(plan, ranking));
    }

    private static SmallModelPlanEnvelope Plan() => new()
    {
        SourceModel = "StardewAI.Core.Training.DailyPlanCompiler", PlanType = "daily_candidate_plan", StateHash = "source",
        Steps = [new() { Kind = "open_mailbox_letter", Preconditions = ["candidate_id:mail"], Parameters =
        [
            new() { Name = "budget.accepted_candidate_index", Value = "0" },
            new() { Name = "continuation.option_id", Value = "mail.process_letter" },
            new() { Name = "continuation.mail_id", Value = "Beat_PK" },
            new() { Name = "continuation.target_location", Value = "FarmHouse" }
        ] }]
    };

    private static JsonObject Ranking() => new()
    {
        ["ranked_event_candidates"] = new JsonArray(new JsonObject
            { ["candidate_id"] = "mail", ["option_id"] = "mail.process_letter", ["parameters"] = new JsonArray() })
    };
}
