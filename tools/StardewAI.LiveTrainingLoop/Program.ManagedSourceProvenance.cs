using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

static partial class Program
{
    private static MoneyRevenueCropManagedProvenance? PrepareManagedCropSourceProvenance(LiveTrainingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ManagedCropSourceProvenanceRequestPath)) return null;
        if (options.ArtifactRetentionMode != "stop")
            throw new ArgumentException("Managed source provenance requires existing stop retention, not rolling deletion of its inputs.");
        var context = MoneyRevenueCropManagedProvenance.Prepare(options.ManagedCropSourceProvenanceRequestPath, options.RunId);
        context.RequireNewRunDirectory(options.SnapshotDir);
        return context;
    }

    private static async Task<string?> WriteManagedCropSourceProvenanceAsync(LiveTrainingOptions options, MoneyRevenueCropManagedProvenance? context)
    {
        if (context is null) return null;
        var summary = context.Build(options.SnapshotDir);
        await WritePortfolioJournalFileAsync(options.ManagedCropSourceProvenancePath, summary.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return options.ManagedCropSourceProvenancePath;
    }
}
