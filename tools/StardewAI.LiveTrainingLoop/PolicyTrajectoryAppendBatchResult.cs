namespace StardewAI.LiveTrainingLoop;

public sealed class PolicyTrajectoryAppendBatchResult
{
    public int AppendedCount { get; set; }
    public int SkippedCount { get; private set; }
    public string FirstSkipReason { get; private set; } = string.Empty;

    public void Skip(string reason)
    {
        SkippedCount++;
        if (string.IsNullOrWhiteSpace(FirstSkipReason))
        {
            FirstSkipReason = reason;
        }
    }
}
