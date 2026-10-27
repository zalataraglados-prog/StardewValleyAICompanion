namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveWait
    {
        public ActiveWait(
            PendingExecution pending,
            int targetTicks,
            int? waitUntilTime,
            int maxSlices,
            int startTime)
        {
            Pending = pending;
            TargetTicks = targetTicks;
            WaitUntilTime = waitUntilTime;
            MaxSlices = maxSlices;
            StartTime = startTime;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public int TargetTicks { get; }
        public int? WaitUntilTime { get; }
        public int MaxSlices { get; }
        public int StartTime { get; }
        public int ElapsedTicks { get; set; }
        public string StartedAt { get; }
    }
}
