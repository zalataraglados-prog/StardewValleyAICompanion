namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumLocalDependencyViewCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (!output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("Museum local dependency view output must be a new JSON file.");
        write(output, verify ? MuseumLocalDependencyViewBuilder.Verify(options.Required("view")) :
            MuseumLocalDependencyViewBuilder.Build(options.Required("corpus"), options.Required("dependencies")));
    }
}
