using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed class PolicyTrajectoryFriendshipInputTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MethodInfo Resolve = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
        .GetMethod("ResolveSocialPolicyGoal", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FeedbackUsesTheOriginalQueueWithoutRequiringLegacyCommandGoal(bool rootGoal)
    {
        var (decision, step) = Input();
        if (!rootGoal) decision.GoalResolution.EffectiveGoalId = string.Empty;
        var fallback = rootGoal ? "grandpa.maximum_21" : FriendshipPolicyInputProjector.GoalId;
        Assert.Equal(FriendshipPolicyInputProjector.GoalId, Resolve.Invoke(null, new object[] { decision, step, fallback }));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("item")]
    [InlineData("goal")]
    public void FeedbackCannotBorrowAnotherCompiledQueue(string mutation)
    {
        var (decision, step) = Input(mutation == "goal" ? "goal.collection.master_angler" : FriendshipPolicyInputProjector.GoalId);
        if (mutation == "path") step["effective_compiled_queue_path"] = "";
        if (mutation == "item") step["queue_item_id"] = "foreign-item";
        var error = Assert.Throws<TargetInvocationException>(() => Resolve.Invoke(null, new object[] { decision, step, "grandpa.maximum_21" }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void FeedbackForUnrelatedGoalsKeepsItsOriginalProjectionPath()
    {
        Assert.Equal("goal.collection.master_angler", Resolve.Invoke(null, new object[]
            { new AvailabilityAwarePolicyPredictionEnvelope(), new JsonObject(), "goal.collection.master_angler" }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarriageFeedbackUsesItsOwnExactQueue(bool matching)
    {
        var (decision, step) = Input(matching ? MarriagePolicyInputProjector.GoalId : FriendshipPolicyInputProjector.GoalId);
        decision.GoalResolution.EffectiveGoalId = MarriagePolicyInputProjector.GoalId;
        if (matching) Assert.Equal(MarriagePolicyInputProjector.GoalId, Resolve.Invoke(null, new object[] { decision, step, "grandpa.maximum_21" }));
        else Assert.Throws<TargetInvocationException>(() => Resolve.Invoke(null, new object[] { decision, step, "grandpa.maximum_21" }));
    }

    private static (AvailabilityAwarePolicyPredictionEnvelope, JsonObject) Input(string goal = FriendshipPolicyInputProjector.GoalId)
    {
        var path = Path.Combine(Path.GetTempPath(), "stardewai-feedback-goal-" + Guid.NewGuid().ToString("N") + ".json");
        var queue = new ActionQueueEnvelope { GoalId = goal, Items = new[] { new ActionQueueItem { QueueItemId = "item" } } };
        Assert.Null(queue.Items[0].NormalizedCommand.GoalId);
        File.WriteAllText(path, JsonSerializer.Serialize(queue, Json));
        var decision = new AvailabilityAwarePolicyPredictionEnvelope();
        decision.GoalResolution.EffectiveGoalId = FriendshipPolicyInputProjector.GoalId;
        return (decision, new JsonObject { ["effective_compiled_queue_path"] = path, ["queue_item_id"] = "item" });
    }
}
