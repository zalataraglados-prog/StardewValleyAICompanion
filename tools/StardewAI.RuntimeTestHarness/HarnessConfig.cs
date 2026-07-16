namespace StardewAI.RuntimeTestHarness;

public sealed class HarnessConfig
{
    public string SavesPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";

    public string SlotName { get; set; } = string.Empty;

    public bool AutoLoad { get; set; } = true;

    public int LoadAfterTicks { get; set; } = 120;

    public bool EnableTrainingExecutor { get; set; } = true;

    public string ExecutorHost { get; set; } = "127.0.0.1";

    public int ExecutorPort { get; set; } = 8767;

    public int ExecutorRequestTimeoutSeconds { get; set; } = 600;

    public bool DisableMovementTimeouts { get; set; }
}
