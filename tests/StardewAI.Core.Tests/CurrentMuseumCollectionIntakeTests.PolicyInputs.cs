using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    [Fact]
    public void MuseumPolicyFeaturesRetainAll95AndDoNotChangeOtherGoals()
    {
        var snapshot = NativeTransition().Before;
        var projector = new PolicyStateFeatureProjector();
        var world = new WorldModelProjector().Project(snapshot, MuseumPolicyInputProjector.GoalId, "training_singleplayer");
        var features = projector.Project(world, snapshot);
        Assert.Equal(94, features.Numeric.Single(row => row.Name == "museum.collection.donated_count").Value);
        Assert.Equal(1, features.Numeric.Single(row => row.Name == "museum.collection.missing_count").Value);
        Assert.Equal(95, features.Boolean.Count(row => row.Name.StartsWith("museum.collection.item.", StringComparison.Ordinal)));
        Assert.False(features.Boolean.Single(row => row.Name == "museum.collection.item.(O)96.donated").Value);
        Assert.False(features.Boolean.Single(row => row.Name == "museum.collection.achievement_5").Value);
        Assert.Equal(JsonSerializer.Serialize(features), JsonSerializer.Serialize(new MuseumPolicyInputProjector(snapshot).ProjectState(features)));
        world.UserGoal = "grandpa.maximum_21";
        Assert.Equal(JsonSerializer.Serialize(projector.Project(world)), JsonSerializer.Serialize(projector.Project(world, snapshot)));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("total")]
    [InlineData("count")]
    [InlineData("duplicate")]
    [InlineData("missing_field")]
    public void MuseumPolicyInputRejectsUnreadableOrInconsistentObservations(string change)
    {
        var snapshot = NativeTransition().Before;
        if (change == "hash") snapshot.StateHash = new string('f', 64);
        else
        {
            var state = JsonSerializer.SerializeToNode(snapshot.State, Json)!;
            var museum = state["world_progress"]!["museum"]!["value"]!;
            if (change == "total") museum["total_donatable_items"] = 94;
            if (change == "count") museum["donated_count"] = 93;
            if (change == "duplicate") museum["donatable_items"]![94]!["qualified_item_id"] = "(O)1";
            if (change == "missing_field") museum.AsObject().Remove("complete_collection_achievement_received");
            snapshot.State = state.Deserialize<Dictionary<string, JsonElement>>(Json)!;
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        Assert.ThrowsAny<Exception>(() => new MuseumPolicyInputProjector(snapshot));
    }

    [Fact]
    public void MuseumCandidateProjectionPreservesParametersUnknownsAndCounterfactualIdentity()
    {
        var snapshot = NativeTransition().Before;
        var candidates = new[]
        {
            new PolicyEventCandidatePrediction { CandidateId = "donor", OptionId = "museum.donate_items", Kind = "donate_museum_item",
                QualifiedItemId = "(O)96", Parameters = new[] { new SmallModelActionParameter { Name = "expected_donated_count_after", Value = "95" } } },
            new PolicyEventCandidatePrediction { CandidateId = "wait", OptionId = "executor.wait_ticks", Kind = MuseumPolicyInputProjector.DeferKind }
        };
        var original = JsonSerializer.Serialize(candidates);
        var projector = new MuseumPolicyInputProjector(snapshot);
        var projected = projector.ProjectCandidates(candidates);
        Assert.Equal(original, JsonSerializer.Serialize(candidates));
        Assert.Equal(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(projector.ProjectCandidates(projected)));
        Assert.Equal(JsonSerializer.Serialize(candidates.Select(row => row.Parameters)), JsonSerializer.Serialize(projected.Select(row => row.Parameters)));
        Assert.Equal(95, projected[0].PolicyFeatures!.Numeric.Single().Value);
        Assert.False(projected[0].PolicyFeatures!.Boolean.Single(row => row.Name == "museum.donation.comparison_horizon_ticks.known").Value);
        Assert.DoesNotContain(projected[0].PolicyFeatures!.Numeric, row => row.Name.Contains("horizon", StringComparison.Ordinal));
        Assert.Equal("executor.wait_ticks", projected[1].OptionId);
        Assert.True(projected[1].PolicyFeatures!.Boolean.Single(row => row.Name == "museum.comparison.counterfactual_only").Value);
        Assert.Empty(new PolicyTrainingAdmissionFilter().FilterOptionIds(new[] { projected[1].OptionId }));
    }
}
