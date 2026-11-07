namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyCounterfactualSourceAuditCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        Require(output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(output) && !Directory.Exists(output),
            "Money source audit output must be a new JSON file.");
        var report = verify ? MoneyCounterfactualSourceAuditBuilder.Verify(options.Required("audit")) :
            MoneyCounterfactualSourceAuditBuilder.Build(options.Required("reference-manifest"), options.Required("request-id"),
                options.Required("replay-catalog"));
        write(output, report);
    }
}
