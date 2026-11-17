namespace StardewAI.GoalConditionedBootstrap;

internal static class GoldenWalnutManagedReadbackSourceBindingCommands
{
    internal static void Run(Arguments options, Action<string, object> write)
    {
        var output = Path.GetFullPath(options.Required("output"));
        if (File.Exists(output) || Directory.Exists(output))
            throw new IOException("Managed readback source binding output already exists.");
        write(output, GoldenWalnutManagedReadbackSourceBindingBuilder.Build(options.Required("request")));
    }
}
