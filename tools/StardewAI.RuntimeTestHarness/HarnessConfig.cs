namespace StardewAI.RuntimeTestHarness;

public sealed class HarnessConfig
{
    public string SavesPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";

    public string SlotName { get; set; } = string.Empty;

    public bool AutoLoad { get; set; } = true;

    public int LoadAfterTicks { get; set; } = 120;

    public bool EnableTrainingExecutor { get; set; } = true;

    public string CompanionActorId { get; set; } = "ai_companion.main";

    public string CompanionFarmerId { get; set; } = string.Empty;

    public string DedicatedHostActorId { get; set; } = "ai_host.main";

    public string DedicatedHostFarmerId { get; set; } = string.Empty;

    public string ExecutorHost { get; set; } = "127.0.0.1";

    public int ExecutorPort { get; set; } = 8767;

    public int ExecutorRequestTimeoutSeconds { get; set; } = 600;

    public bool DisableMovementTimeouts { get; set; }

    public bool FreezeClockWhileExecutorIdle { get; set; }

    public string JunimoKartExecutionStrategy { get; set; } = "timed_equivalent";

    public int JunimoKartEquivalentDurationTicks { get; set; } = 54000;

    public int JunimoKartEquivalentAcceleration { get; set; } = 60;

    public string DiagnosticOutputPath { get; set; } =
        @"E:\StardewValleyAICompanion-runtime\diagnostics";
}
