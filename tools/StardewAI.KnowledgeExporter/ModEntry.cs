using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAI.KnowledgeExporter;

public sealed class ModEntry : Mod
{
    private ExporterConfig config = new();
    private Task<List<ContentFileRecord>>? inventoryTask;
    private KnowledgeExportSession? session;
    private bool completionLogged;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ExporterConfig>();
        var enabledOverride = Environment.GetEnvironmentVariable("STARDEWAI_KNOWLEDGE_EXPORT_ENABLED");
        if (bool.TryParse(enabledOverride, out var enabled))
        {
            config.Enabled = enabled;
        }

        if (!config.Enabled)
        {
            Monitor.Log("Knowledge exporter disabled by configuration.", LogLevel.Info);
            return;
        }

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var contentRoot = Path.Combine(Constants.GamePath, "Content");
        inventoryTask = Task.Run(() => ContentInventory.Build(contentRoot));
        Monitor.Log("Building authoritative raw content inventory in the background.", LogLevel.Info);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (session is null)
        {
            if (inventoryTask is null || !inventoryTask.IsCompleted)
            {
                return;
            }

            if (inventoryTask.IsFaulted)
            {
                Monitor.Log($"Content inventory failed: {inventoryTask.Exception}", LogLevel.Error);
                Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                return;
            }

            var outputRoot = ResolveOutputRoot();
            Directory.CreateDirectory(outputRoot);
            session = new KnowledgeExportSession(
                Helper,
                Monitor,
                outputRoot,
                inventoryTask.Result,
                config.ExportDynamicStringAssets);
            Monitor.Log($"Knowledge export started at {session.RunDirectory}.", LogLevel.Info);
        }

        if (!session.IsComplete)
        {
            session.ProcessNext();
            return;
        }

        if (!completionLogged)
        {
            completionLogged = true;
            session.Complete();
            Monitor.Log($"Knowledge export completed at {session.RunDirectory}.", LogLevel.Info);
            Helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
        }
    }

    private string ResolveOutputRoot()
    {
        var environmentPath = Environment.GetEnvironmentVariable("STARDEWAI_KNOWLEDGE_EXPORT_PATH");
        var configuredPath = !string.IsNullOrWhiteSpace(environmentPath)
            ? environmentPath
            : config.OutputPath;
        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Helper.DirectoryPath, "exports")
            : Path.GetFullPath(configuredPath);
    }
}
