namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumPolicyRowAdmissionCommands
{
    internal static void Write(Arguments options, Action<string, object> write, bool verify)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (!output.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new InvalidDataException("Museum policy admission output must be a new JSON file.");
        var report = verify ? MuseumPolicyRowAdmissionBuilder.Verify(options.Required("admission")) :
            MuseumPolicyRowAdmissionBuilder.Build(options.Required("join"));
        write(output, report);
    }
}
