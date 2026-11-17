namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyRevenueCropSupplementalEvidenceCommands
{
    internal static void Run(Arguments options, Action<string, object> write)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Supplement summary output already exists.");
        var request = options.Required("request");
        write(output, options.Optional("evidence-mode") == "managed_saved_closeout"
            ? MoneyRevenueCropManagedProvenance.Prepare(request).Build()
            : MoneyRevenueCropSupplementalEvidenceSummary.Build(request));
    }
}
