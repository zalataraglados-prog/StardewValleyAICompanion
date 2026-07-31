using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class ExecutorInputLifecycleTests
{
    [Fact]
    public void MovementLeaseSwitchesDirectionWithoutIntermediateRelease()
    {
        var lease = new MovementLease();

        Assert.True(lease.Acquire("runtime", 0, 1, out _));
        Assert.True(lease.Acquire("runtime", 1, 2, out _));

        Assert.Equal("runtime", lease.Owner);
        Assert.Equal(1, lease.Direction);
        Assert.Equal("direction_acquired_or_switched", lease.LastTransitionReason);
    }

    [Fact]
    public void MovementLeaseRejectsCompetingOwner()
    {
        var lease = new MovementLease();
        Assert.True(lease.Acquire("movement", 2, 1, out _));

        Assert.False(lease.Acquire("menu", 3, 2, out var reason));

        Assert.Equal("movement_lease_owned_by:movement", reason);
        Assert.Equal(2, lease.Direction);
    }

    [Fact]
    public void NativeToolCycleRequiresPressReleaseAndNativeSettlement()
    {
        var lifecycle = new NativeToolActionLifecycle();

        Assert.Equal(
            NativeToolActionCommand.Press,
            lifecycle.Advance(Idle()).Command);
        Assert.Equal(
            NativeToolActionCommand.Release,
            lifecycle.Advance(Using(canRelease: true)).Command);
        Assert.Equal(
            NativeToolActionCommand.None,
            lifecycle.Advance(Using(canRelease: false)).Command);
        Assert.Equal(
            NativeToolActionCommand.CycleCompleted,
            lifecycle.Advance(Idle()).Command);

        lifecycle.Reset();
        Assert.Equal(NativeToolActionPhase.Ready, lifecycle.Phase);
    }

    [Fact]
    public void NativeToolCycleBlocksWhenPressNeverStartsNativeAction()
    {
        var lifecycle = new NativeToolActionLifecycle(
            nativeStartTimeoutTicks: 1);
        lifecycle.Advance(Idle());

        Assert.Equal(
            NativeToolActionCommand.None,
            lifecycle.Advance(Idle()).Command);
        var blocked = lifecycle.Advance(Idle());

        Assert.Equal(NativeToolActionCommand.Block, blocked.Command);
        Assert.Equal("native_tool_action_did_not_start", blocked.Reason);
    }

    [Fact]
    public void NonChargeToolMayCompleteNativelyWithoutReleasePhase()
    {
        var lifecycle = new NativeToolActionLifecycle();
        lifecycle.Advance(Idle());
        Assert.Equal(
            NativeToolActionCommand.None,
            lifecycle.Advance(Using(canRelease: false)).Command);

        var completed = lifecycle.Advance(Idle());

        Assert.Equal(
            NativeToolActionCommand.CycleCompleted,
            completed.Command);
        Assert.Equal(
            "native_non_charge_tool_completed_without_explicit_release",
            completed.Reason);
    }

    [Fact]
    public void DiagnosticRingBufferKeepsOnlyNewestFramesInOrder()
    {
        var buffer = new ExecutorDiagnosticRingBuffer(2);
        buffer.Add(new ExecutorDiagnosticFrame { Tick = 1 });
        buffer.Add(new ExecutorDiagnosticFrame { Tick = 2 });
        buffer.Add(new ExecutorDiagnosticFrame { Tick = 3 });

        Assert.Equal(new long[] { 2, 3 }, buffer.Snapshot().Select(row => row.Tick));
    }

    private static NativeToolActionObservation Idle() =>
        new(
            usingTool: false,
            canMove: true,
            canReleaseTool: false,
            pauseForSingleAnimation: false);

    private static NativeToolActionObservation Using(bool canRelease) =>
        new(
            usingTool: true,
            canMove: false,
            canReleaseTool: canRelease,
            pauseForSingleAnimation: true);
}
