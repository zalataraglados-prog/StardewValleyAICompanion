namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyThresholdShopReplayCorpusCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        Require(output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !File.Exists(output) && !Directory.Exists(output),
            "Money-threshold corpus output must be a new JSON file.");
        MoneyThresholdTeacherCorpus corpus;
        if (verify) corpus = MoneyThresholdTeacherCorpusBuilder.Verify(options.Required("corpus"));
        else
        {
            var request = CurrentTeacherFrontierSupport.Read<MoneyThresholdTeacherCorpusRequest>(Path.GetFullPath(options.Required("request")), "Money replay corpus request");
            Require(request.SchemaVersion == MoneyThresholdTeacherCorpusVersions.ShopReplayRequest, "Replay corpus entry requires explicit v5.");
            corpus = MoneyThresholdTeacherCorpusBuilder.Build(request);
        }
        write(output, corpus);
    }
}
