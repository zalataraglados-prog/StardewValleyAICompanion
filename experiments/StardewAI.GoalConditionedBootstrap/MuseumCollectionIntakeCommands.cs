namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumCollectionIntakeCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (!output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("Museum intake output must be a new JSON file.");
        var report = verify ? CurrentMuseumCollectionIntakeBuilder.Verify(options.Required("intake")) :
            CurrentMuseumCollectionIntakeBuilder.Build(options.Required("snapshot"), options.Required("knowledge"),
                options.Required("requirement-inventory"), options.Required("dependencies"));
        write(output, report);
    }
}
