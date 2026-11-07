namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyShopReplaySourceBindingCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        Require(output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(output) && !Directory.Exists(output),
            "Money replay binding output must be a new JSON file.");
        var report = verify ? MoneyShopReplaySourceBindingBuilder.Verify(options.Required("receipt")) :
            MoneyShopReplaySourceBindingBuilder.Build(options.Required("manifest"));
        write(output, report);
    }
}
