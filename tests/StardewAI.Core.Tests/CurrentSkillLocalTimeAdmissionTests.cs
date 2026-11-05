using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class CurrentSkillLocalTimeAdmissionTests
{
    [Fact]
    public void DerivesCurrentFlagsOnlyFromTheOriginalPendingLocalPlanAndQueue()
    {
        var (candidate, plan, queue) = Fixture();
        Assert.True(CurrentSkillLocalTimeAdmission.TryCertify(candidate, plan, queue, "state", "Farm", 10));
        Assert.True(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "skill_current_admission_state_hash" && parameter.Value == "state");
        Assert.False(CurrentSkillLocalTimeAdmission.TryCertify(candidate, plan, queue, "state", "Farm", 10));
    }

    [Theory]
    [InlineData("now-false")]
    [InlineData("today-false")]
    [InlineData("window")]
    [InlineData("gate")]
    [InlineData("wait")]
    [InlineData("next-open")]
    [InlineData("location")]
    [InlineData("cross-map")]
    [InlineData("connector")]
    [InlineData("blocked-queue")]
    [InlineData("hash")]
    [InlineData("identity")]
    [InlineData("budget")]
    [InlineData("future")]
    public void NeverTurnsAnUnverifiedWindowOrExplicitBlockIntoPermission(string mutation)
    {
        var (candidate, plan, queue) = Fixture();
        switch (mutation)
        {
            case "now-false": candidate.AllowedNow = false; break;
            case "today-false": candidate.AllowedToday = false; break;
            case "window": candidate.ClosesAt = 900; break;
            case "gate": candidate.GateReasons = new[] { "unknown_native_gate" }; break;
            case "wait": candidate.ScheduledWaitCost = 1; break;
            case "next-open": candidate.NextOpenTime = 900; break;
            case "location": candidate.LocationId = "Town"; break;
            case "cross-map": plan.Steps[0].TargetLocation = "Town"; break;
            case "connector": queue.Items[0].OptionId = "executor.traverse_connector"; break;
            case "blocked-queue": queue.Items[0].Status = "blocked"; break;
            case "hash": queue.Items[0].NormalizedCommand.StateHash = "other"; break;
            case "identity": plan.CandidateAudit[0].CandidateId = "other"; break;
            case "budget": plan.CandidateAudit[0].CandidateMinutes = 11; break;
            case "future": candidate.Parameters = new[] { new SmallModelActionParameter { Name = "continuation.option_id", Value = "fishing.catch_fish" } }; break;
        }
        var now = candidate.AllowedNow;
        var today = candidate.AllowedToday;
        Assert.False(CurrentSkillLocalTimeAdmission.TryCertify(candidate, plan, queue, "state", "Farm", 10));
        Assert.Equal(now, candidate.AllowedNow);
        Assert.Equal(today, candidate.AllowedToday);
        Assert.DoesNotContain(candidate.Parameters, parameter => parameter.Name.StartsWith("skill_current_admission_", StringComparison.Ordinal));
    }

    private static (PolicyEventCandidatePrediction, SmallModelPlanEnvelope, ActionQueueEnvelope) Fixture() =>
        (new PolicyEventCandidatePrediction
        {
            CandidateId = "crab", LocationId = "Farm", Available = true,
            TimelineStatus = "ready_now", EstimatedTicks = 30
        }, new SmallModelPlanEnvelope
        {
            StateHash = "state", Steps = new[] { new SmallModelPlanStep { Kind = "collect_crab_pot", TargetLocation = "Farm" } },
            CandidateAudit = new[] { new SmallModelPlanCandidateAudit
                { CandidateId = "crab", Decision = "accepted", CandidateMinutes = 1 } }
        }, new ActionQueueEnvelope
        {
            StateHash = "state", Status = "pending", Items = new[] { new ActionQueueItem
            {
                OptionId = "executor.collect_crab_pot", Status = "pending",
                NormalizedCommand = new NormalizedCommand
                {
                    StateHash = "state", Steps = new[] { new CompiledActionStep { StepType = "collect_crab_pot" } }
                }
            } }
        });
}
