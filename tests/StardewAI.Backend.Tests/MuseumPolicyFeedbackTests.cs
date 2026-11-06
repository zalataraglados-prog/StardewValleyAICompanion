using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class MuseumPolicyFeedbackTests
{
    [Theory]
    [InlineData(MuseumPolicyInputProjector.GoalId, "executor.donate_museum_item", "owned", true)]
    [InlineData("grandpa.maximum_21", "executor.donate_museum_item", "owned", true)]
    [InlineData("daily.closed_loop", "executor.donate_museum_item", "owned", false)]
    [InlineData(MuseumPolicyInputProjector.GoalId, "executor.wait_ticks", "owned", false)]
    [InlineData(MuseumPolicyInputProjector.GoalId, "executor.donate_museum_item", "other", false)]
    public void ExplicitMuseumFeedbackRequiresTheOriginalOwnedDonationQueue(string queueGoal, string primitive, string itemId, bool accepted)
    {
        var path = Path.Combine(Path.GetTempPath(), "museum-feedback-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(new ActionQueueEnvelope
        {
            GoalId = queueGoal, Items = new[] { new ActionQueueItem { QueueItemId = itemId, OptionId = primitive } }
        }));
        var first = new JsonObject { ["effective_compiled_queue_path"] = path, ["queue_item_id"] = "owned" };
        var method = Resolver();
        var args = new object[] { new AvailabilityAwarePolicyPredictionEnvelope(), first, MuseumPolicyInputProjector.GoalId };
        if (accepted) Assert.Equal(MuseumPolicyInputProjector.GoalId, method.Invoke(null, args));
        else Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, args)).InnerException);
    }

    [Fact]
    public void OtherFeedbackDoesNotInferMuseumFromAPrimitiveOrReadAnUndeclaredFile()
    {
        Assert.Equal("grandpa.maximum_21", Resolver().Invoke(null, new object[]
            { new AvailabilityAwarePolicyPredictionEnvelope(), new JsonObject(), "grandpa.maximum_21" }));
    }

    private static MethodInfo Resolver() => typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
        .GetMethod("ResolveMuseumPolicyGoal", BindingFlags.Static | BindingFlags.NonPublic)!;
}
