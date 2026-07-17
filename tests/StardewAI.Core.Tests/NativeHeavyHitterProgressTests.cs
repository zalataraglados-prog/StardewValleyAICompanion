using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeHeavyHitterProgressTests
{
    [Fact]
    public void CompletedActionsConsumeBudgetAndRecordObservedHealth()
    {
        var progress = new NativeHeavyHitterProgress(4, 2);

        progress.MarkActionIssued();
        progress.RecordCompletedSwing(2);
        progress.MarkActionIssued();
        progress.RecordRemoval();

        Assert.Equal(2, progress.SwingCount);
        Assert.False(progress.ActionIssued);
        Assert.False(progress.CanIssueAction());
        Assert.Equal(new[] { 4, 2, 0 }, progress.ObservedHealth);
    }

    [Fact]
    public void AnimationPollingCannotDoubleCountOneIssuedAction()
    {
        var progress = new NativeHeavyHitterProgress(3, 3);

        progress.RecordCompletedSwing(2);
        progress.MarkActionIssued();
        progress.MarkActionIssued();
        progress.RecordCompletedSwing(2);
        progress.RecordCompletedSwing(1);

        Assert.Equal(1, progress.SwingCount);
        Assert.Equal(new[] { 3, 2 }, progress.ObservedHealth);
    }
}
