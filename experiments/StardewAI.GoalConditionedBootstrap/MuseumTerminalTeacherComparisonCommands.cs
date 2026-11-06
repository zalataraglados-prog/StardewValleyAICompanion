namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumTerminalTeacherComparisonCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = NewOutput(options.Required("output"));
        var report = verify ? MuseumTerminalTeacherComparisonBuilder.Verify(options.Required("comparison")) :
            MuseumTerminalTeacherComparisonBuilder.Build(options.Required("request"));
        write(output, report);
        if (!report.IndependentPairwiseLabelEligible) Environment.ExitCode = 2;
    }

    internal static void Compile(Arguments options, Action<string, object> write)
    {
        var plan = NewOutput(options.Required("plan-output"));
        var queue = NewOutput(options.Required("output"));
        if (plan.Equals(queue, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Museum plan and queue outputs must differ.");
        var result = MuseumTerminalTeacherComparisonBuilder.Compile(options.Required("comparison"), options.Required("candidate-id"));
        write(plan, result.Plan);
        write(queue, result.Queue);
    }

    private static string NewOutput(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(full) || Directory.Exists(full))
            throw new InvalidDataException("Museum Teacher outputs must be new JSON files.");
        return full;
    }
}
