using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed class ModEntry : Mod
{
    private HarnessConfig config = new();
    private int ticksSeen;
    private bool loadAttempted;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<HarnessConfig>();
        ApplyEnvironmentOverrides();

        if (string.IsNullOrWhiteSpace(config.SavesPath))
        {
            Monitor.Log("Runtime harness disabled: SavesPath is empty.", LogLevel.Warn);
            return;
        }

        config.SavesPath = Path.GetFullPath(config.SavesPath);
        Directory.CreateDirectory(config.SavesPath);

        SavesFolderPatch.RedirectPath = config.SavesPath;
        new Harmony(ModManifest.UniqueID).Patch(
            original: AccessTools.Method("StardewValley.Program:GetSavesFolder"),
            postfix: new HarmonyMethod(typeof(SavesFolderPatch), nameof(SavesFolderPatch.Postfix)));

        Monitor.Log($"Redirected Stardew save folder to {config.SavesPath}", LogLevel.Info);

        if (config.AutoLoad)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }
    }

    private void ApplyEnvironmentOverrides()
    {
        var savesPath = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SAVES");
        if (!string.IsNullOrWhiteSpace(savesPath))
        {
            config.SavesPath = savesPath;
        }

        var slotName = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SLOT");
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            config.SlotName = slotName;
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (loadAttempted || Context.IsWorldReady || Game1.gameMode != 0)
        {
            return;
        }

        ticksSeen++;
        if (ticksSeen < config.LoadAfterTicks)
        {
            return;
        }

        loadAttempted = true;
        if (string.IsNullOrWhiteSpace(config.SlotName))
        {
            Monitor.Log("AutoLoad skipped: SlotName is empty.", LogLevel.Warn);
            return;
        }

        var slotPath = Path.Combine(config.SavesPath, config.SlotName);
        if (!Directory.Exists(slotPath))
        {
            Monitor.Log($"AutoLoad skipped: save slot not found at {slotPath}", LogLevel.Error);
            return;
        }

        Monitor.Log($"Loading isolated test save slot {config.SlotName}", LogLevel.Info);
        SaveGame.Load(config.SlotName);
        Game1.exitActiveMenu();
    }
}

internal static class SavesFolderPatch
{
    public static string? RedirectPath { get; set; }

    public static void Postfix(ref string __result)
    {
        if (string.IsNullOrWhiteSpace(RedirectPath))
        {
            return;
        }

        Directory.CreateDirectory(RedirectPath);
        __result = RedirectPath;
    }
}
