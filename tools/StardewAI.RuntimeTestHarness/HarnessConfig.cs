namespace StardewAI.RuntimeTestHarness;

public sealed class HarnessConfig
{
    public string SavesPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";

    public string SlotName { get; set; } = string.Empty;

    public bool AutoLoad { get; set; } = true;

    public int LoadAfterTicks { get; set; } = 120;
}
