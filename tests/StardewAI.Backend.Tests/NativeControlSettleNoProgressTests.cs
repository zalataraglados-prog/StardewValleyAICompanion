using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class NativeControlSettleNoProgressTests
{
    [Fact]
    public void VerifiedSettleWaitUsesRecoveryBackoff()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        var queue = SettleWaitQueue();

        Assert.False(policy.Observe(queue).NoProgress);

        var first = policy.ObserveExecution(queue, VerifiedWaitExecution());
        Assert.True(first.NoProgress);
        Assert.Equal(1, first.Streak);
        Assert.Equal(5000, first.DelayMs);

        Assert.False(policy.Observe(queue).NoProgress);
        Assert.Equal(
            10000,
            policy.ObserveExecution(queue, VerifiedWaitExecution()).DelayMs);
    }

    private static JsonObject SettleWaitQueue()
    {
        return new JsonObject
        {
            ["status"] = "pending",
            ["items"] = new JsonArray(new JsonObject
            {
                ["option_id"] = "executor.wait_ticks",
                ["status"] = "pending",
                ["source_action_id"] =
                    "recovery_wait_for_native_control_settle.refresh_wait.0"
            })
        };
    }

    private static JsonObject VerifiedWaitExecution()
    {
        return new JsonObject
        {
            ["status"] = "applied",
            ["primitive_kind"] = "wait_ticks",
            ["primitive_verification_status"] = "verified",
            ["changed_facts"] = new JsonArray(
                new JsonObject
                {
                    ["path"] = "executor.wait_ticks",
                    ["before"] = "0",
                    ["after"] = "30"
                },
                new JsonObject
                {
                    ["path"] = "time.time",
                    ["before"] = "600",
                    ["after"] = "600"
                })
        };
    }
}
