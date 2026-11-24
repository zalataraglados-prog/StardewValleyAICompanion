using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FunctionalRouteEvidenceTests
{
    [Fact]
    public void AStandaloneActionCanBeReusedInsideAJointPlanWithoutChangingActuatorEvidence()
    {
        var original = Command();
        var joint = Clone(original);
        joint.Steps[0].StepId = "regenerated-id";
        joint.Parameters = joint.Parameters.Concat(new[]
        {
            P("budget.remaining_minutes_before", "770"), P("budget.remaining_energy_after", "419"),
            P("compiler_context.plan_step_count", "2"), P("compiler_context.is_terminal_step", "false"),
            P("safety_constraint", "daily_plan_joint_calendar_checked")
        }).ToArray();
        Assert.True(FunctionalRouteReplayer.SameFunctionalCommand(original, joint));
    }

    [Theory]
    [InlineData("option")]
    [InlineData("actor")]
    [InlineData("mode")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("rod")]
    [InlineData("outcomes")]
    [InlineData("primitive")]
    [InlineData("constraint")]
    public void ReuseStillRequiresTheExactActuatorAuthorityAndParameters(string mutation)
    {
        var original = Command();
        var changed = Clone(original);
        switch (mutation)
        {
            case "option": changed.OptionId = "executor.collect_crab_pot"; break;
            case "actor": changed.Actor.ActorId = "another-player"; break;
            case "mode": changed.ExecutionMode = "coop_companion"; break;
            case "source": changed.StateHash = "another-source"; break;
            case "target": changed.Steps[0].Target = "another-bobber-tile"; break;
            case "rod": changed.Parameters[0].Value = "4"; break;
            case "outcomes": changed.Parameters[1].Value = "[\"(O)130\"]"; break;
            case "primitive": changed.Steps[0].StepType = "claim_fishing_item_grab_reward"; break;
            case "constraint": changed.Parameters[2].Value = "allow_forced_result"; break;
        }
        Assert.False(FunctionalRouteReplayer.SameFunctionalCommand(original, changed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha")]
    public void CallerFlagsOrAnUnboundFunctionalReferenceCannotOpenEitherGate(string hash)
    {
        var assessment = FormalNativeRouteAdmissionConsumer.Evaluate(new(
            FormalNativeRouteAdmissionConsumer.FunctionalProofReferenceSchemaVersion,
            Path.Combine(Path.GetTempPath(), "unbound-functional-route.json"), hash, null));
        Assert.False(assessment.SourceReplayVerified);
        Assert.All(assessment.Gates, gate => Assert.False(gate.Satisfied));
    }

    [Fact]
    public void ExistingReferencesAndNullRequestsRetainTheirOriginalFailClosedMeaning()
    {
        var absent = FormalNativeRouteAdmissionConsumer.Evaluate(null);
        Assert.All(absent.Gates, gate => Assert.Equal("selected_route_unavailable", gate.Reason));
        var old = FormalNativeRouteAdmissionConsumer.Evaluate(new(
            FormalNativeRouteAdmissionConsumer.ProofReferenceSchemaVersion, "missing", "", null));
        Assert.All(old.Gates, gate => Assert.Equal("selected_route_binding_invalid", gate.Reason));
        var json = JsonSerializer.Serialize(absent);
        Assert.DoesNotContain("proof_scope", json);
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static NormalizedCommand Clone(NormalizedCommand value) => JsonSerializer.Deserialize<NormalizedCommand>(JsonSerializer.Serialize(value))!;
    private static NormalizedCommand Command() => new()
    {
        OptionId = "executor.catch_fish", StateHash = "exact-source", ExecutionMode = "training_singleplayer",
        Actor = ExecutionTargetProfiles.CreateActor("training_singleplayer"),
        Parameters = new[] { P("rod_slot_index", "3"), P("possible_qualified_item_ids_json", "[\"(O)836\"]"), P("safety_constraint", "no_forced_catch_result") },
        Steps = new[] { new CompiledActionStep { StepId = "original-id", StepType = "catch_fish", Target = "exact-bobber-tile" } }
    };
}
