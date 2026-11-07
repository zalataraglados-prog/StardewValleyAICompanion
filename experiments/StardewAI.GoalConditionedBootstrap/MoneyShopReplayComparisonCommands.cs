namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyShopReplayComparisonCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        Require(output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(output) && !Directory.Exists(output),
            "Money replay comparison output must be a new JSON file.");
        var report = verify ? MoneyShopReplayComparisonBuilder.Verify(options.Required("receipt")) :
            MoneyShopReplayComparisonBuilder.Build(options.Required("manifest"));
        write(output, report);
    }
}
