namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumTeacherCorpusCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (!output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("Museum corpus output must be a new JSON file.");
        write(output, verify ? MuseumTeacherCorpusBuilder.Verify(options.Required("corpus")) :
            MuseumTeacherCorpusBuilder.Build(options.Required("request")));
    }
}
