namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumTerminalNativeJoinCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (!output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("Museum native join output must be a new JSON file.");
        var report = verify ? MuseumTerminalNativeJoinBuilder.Verify(options.Required("join")) :
            MuseumTerminalNativeJoinBuilder.Build(options.Required("request"));
        write(output, report);
    }
}
