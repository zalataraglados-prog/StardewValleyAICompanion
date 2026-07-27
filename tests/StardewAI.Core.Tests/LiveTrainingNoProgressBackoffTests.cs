using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingNoProgressBackoffTests
{
    [Fact]
    public void RepeatedSemanticBlockUsesExponentialCappedDelay()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);

        Assert.Equal(5000, policy.Observe(BlockedQueue()).DelayMs);
        Assert.Equal(10000, policy.Observe(BlockedQueue()).DelayMs);
        Assert.Equal(20000, policy.Observe(BlockedQueue()).DelayMs);
        Assert.Equal(40000, policy.Observe(BlockedQueue()).DelayMs);
        Assert.Equal(60000, policy.Observe(BlockedQueue()).DelayMs);
        Assert.Equal(60000, policy.Observe(BlockedQueue()).DelayMs);
    }

    [Fact]
    public void ChangedSemanticBlockRestartsDelay()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        policy.Observe(BlockedQueue());
        policy.Observe(BlockedQueue());

        var changed = BlockedQueue();
        changed["compiler_diagnostics"] = new JsonArray(
            "different_block");
        var decision = policy.Observe(changed);

        Assert.Equal(1, decision.Streak);
        Assert.Equal(5000, decision.DelayMs);
    }

    [Fact]
    public void ExecutableQueueResetsStreak()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        policy.Observe(BlockedQueue());
        policy.Observe(BlockedQueue());

        var executable = new JsonObject
        {
            ["status"] = "pending",
            ["items"] = new JsonArray(new JsonObject
            {
                ["option_id"] = "executor.wait_ticks",
                ["status"] = "pending"
            })
        };
        var progress = policy.Observe(executable);
        var nextBlock = policy.Observe(BlockedQueue());

        Assert.False(progress.NoProgress);
        Assert.Equal(0, progress.DelayMs);
        Assert.Equal(1, nextBlock.Streak);
        Assert.Equal(5000, nextBlock.DelayMs);
    }

    [Fact]
    public void RepeatedVerifiedRecoveryWaitBacksOffWithoutCountingProgress()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        var queue = RecoveryRefreshWaitQueue();
        var execution = VerifiedWaitExecution();

        Assert.False(policy.Observe(queue).NoProgress);
        Assert.Equal(
            5000,
            policy.ObserveExecution(queue, execution).DelayMs);
        Assert.False(policy.Observe(queue).NoProgress);
        Assert.Equal(
            10000,
            policy.ObserveExecution(queue, execution).DelayMs);
    }

    [Fact]
    public void OrdinaryWaitIsNotTreatedAsRecoveryNoProgress()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        var queue = RecoveryRefreshWaitQueue();
        queue["items"]![0]!["source_action_id"] =
            "shop_wait_until_open.wait.0";

        var decision = policy.ObserveExecution(
            queue,
            VerifiedWaitExecution());

        Assert.False(decision.NoProgress);
        Assert.Equal(0, decision.DelayMs);
    }

    [Fact]
    public void RecoveryWaitWithRealChangedFactResetsBackoff()
    {
        var policy = new NoProgressBackoffPolicy(5000, 60000);
        var queue = RecoveryRefreshWaitQueue();
        policy.Observe(queue);
        policy.ObserveExecution(queue, VerifiedWaitExecution());

        var execution = VerifiedWaitExecution();
        execution["changed_facts"] = new JsonArray(
            new JsonObject
            {
                ["path"] = "time.time",
                ["before"] = "810",
                ["after"] = "820"
            });
        var progress = policy.ObserveExecution(queue, execution);
        var nextBlock = policy.Observe(BlockedQueue());

        Assert.False(progress.NoProgress);
        Assert.Equal(1, nextBlock.Streak);
    }

    [Fact]
    public void DisabledPolicyStillTracksWithoutAddingDelay()
    {
        var policy = new NoProgressBackoffPolicy(0, 0);

        var decision = policy.Observe(BlockedQueue());

        Assert.True(decision.NoProgress);
        Assert.Equal(1, decision.Streak);
        Assert.Equal(0, decision.DelayMs);
    }

    private static JsonObject BlockedQueue()
    {
        return new JsonObject
        {
            ["status"] = "blocked",
            ["compiler_diagnostics"] = new JsonArray(
                "empty_action_list"),
            ["items"] = new JsonArray()
        };
    }

    private static JsonObject RecoveryRefreshWaitQueue()
    {
        return new JsonObject
        {
            ["status"] = "pending",
            ["items"] = new JsonArray(new JsonObject
            {
                ["option_id"] = "executor.wait_ticks",
                ["status"] = "pending",
                ["source_action_id"] =
                    "recovery_refresh_plan_after_stabilization.refresh_wait.0"
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
            ["changed_facts"] = new JsonArray(new JsonObject
            {
                ["path"] = "executor.wait_ticks",
                ["before"] = "0",
                ["after"] = "30"
            })
        };
    }
}
