using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class BootstrapCommandBatchRunner
{
    public const string ManifestSchema = "stardewai.bootstrap_command_batch.v1";
    public const string ReportSchema = "stardewai.bootstrap_command_batch_report.v1";

    private static readonly string[] ProhibitedChildArguments =
    {
        "batch-manifest",
        "validation-trace",
        "validation-trace-scenario",
        "read-session",
        "read-session-max-mib",
        "read-session-entry-max-mib",
        "deterministic-build-session",
        "deterministic-cache-max-mib",
        "deterministic-cache-entry-max-mib",
        "json-output"
    };

    public static BootstrapCommandBatchReport Run(
        string manifestPath,
        string reportPath,
        IReadOnlyDictionary<string, CommandDefinition> commandRegistry)
    {
        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifest = JsonSerializer.Deserialize<BootstrapCommandBatchManifest>(
            File.ReadAllText(fullManifestPath),
            JsonDefaults.Options)
            ?? throw new InvalidDataException("Bootstrap command batch is null.");
        if (!string.Equals(
                manifest.SchemaVersion,
                ManifestSchema,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Unexpected bootstrap command batch schema: " +
                manifest.SchemaVersion);
        }
        if (manifest.Commands.Length == 0 || manifest.Commands.Length > 64)
        {
            throw new InvalidDataException(
                "Bootstrap command batch must contain 1 through 64 commands.");
        }

        var parsed = manifest.Commands
            .Select((entry, index) => ParseEntry(entry, index, commandRegistry))
            .ToArray();
        AssertUniqueOutputs(
            parsed,
            new[] { fullManifestPath, Path.GetFullPath(reportPath) });

        var results = new List<BootstrapCommandBatchEntryReport>();
        var previousExitCode = Environment.ExitCode;
        try
        {
            foreach (var entry in parsed)
            {
                Environment.ExitCode = 0;
                var clock = Stopwatch.StartNew();
                try
                {
                    entry.Definition.Execute(entry.Options);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Bootstrap batch command {entry.Index} " +
                        $"'{entry.Command}' failed: {ex.Message}",
                        ex);
                }
                finally
                {
                    clock.Stop();
                }

                if (Environment.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Bootstrap batch command {entry.Index} " +
                        $"'{entry.Command}' returned exit code " +
                        Environment.ExitCode + ".");
                }
                results.Add(new BootstrapCommandBatchEntryReport
                {
                    Index = entry.Index,
                    Command = entry.Command,
                    ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds
                });
            }
        }
        finally
        {
            Environment.ExitCode = previousExitCode;
        }

        return new BootstrapCommandBatchReport
        {
            SchemaVersion = ReportSchema,
            Status = "passed",
            ManifestPath = fullManifestPath,
            CommandCount = results.Count,
            Commands = results.ToArray()
        };
    }

    private static ParsedBatchEntry ParseEntry(
        BootstrapCommandBatchEntry entry,
        int index,
        IReadOnlyDictionary<string, CommandDefinition> commandRegistry)
    {
        var arguments = entry.Arguments ?? Array.Empty<string>();
        if (arguments.Length == 0 || (arguments.Length - 1) % 2 != 0)
        {
            throw new InvalidDataException(
                $"Bootstrap batch command {index} has invalid arguments.");
        }
        var command = arguments[0];
        if (string.Equals(
                command,
                "run-command-batch",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Recursive bootstrap command batches are prohibited.");
        }
        if (!commandRegistry.TryGetValue(command, out var definition))
        {
            throw new InvalidDataException(
                $"Bootstrap batch command {index} is unknown: {command}");
        }
        if (!definition.BatchSafe)
        {
            throw new InvalidDataException(
                $"Bootstrap batch command {index} is not batch-safe: " +
                command);
        }
        for (var argumentIndex = 1;
             argumentIndex < arguments.Length;
             argumentIndex += 2)
        {
            var name = arguments[argumentIndex];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Bootstrap batch command {index} has an invalid option.");
            }
            var normalized = name[2..];
            if (ProhibitedChildArguments.Contains(
                    normalized,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Bootstrap batch child argument is controller-owned: " +
                    normalized);
            }
        }

        return new ParsedBatchEntry(
            index,
            command,
            definition,
            Arguments.Parse(arguments.Skip(1).ToArray()),
            OutputPaths(arguments));
    }

    private static string[] OutputPaths(string[] arguments)
    {
        var outputs = new List<string>();
        for (var index = 1; index < arguments.Length; index += 2)
        {
            var name = arguments[index][2..];
            if (!name.Contains("output", StringComparison.OrdinalIgnoreCase))
                continue;
            outputs.Add(Path.GetFullPath(arguments[index + 1]));
        }
        return outputs.ToArray();
    }

    private static void AssertUniqueOutputs(
        ParsedBatchEntry[] entries,
        string[] controllerPaths)
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seen = new HashSet<string>(controllerPaths, comparer);
        foreach (var output in entries.SelectMany(entry => entry.OutputPaths))
        {
            if (!seen.Add(output))
            {
                throw new InvalidDataException(
                    "Bootstrap command batch contains a duplicate output: " +
                    output);
            }
        }
    }

    private sealed record ParsedBatchEntry(
        int Index,
        string Command,
        CommandDefinition Definition,
        Arguments Options,
        string[] OutputPaths);
}

internal sealed class BootstrapCommandBatchManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("commands")]
    public BootstrapCommandBatchEntry[] Commands { get; set; } =
        Array.Empty<BootstrapCommandBatchEntry>();
}

internal sealed class BootstrapCommandBatchEntry
{
    [JsonPropertyName("arguments")]
    public string[] Arguments { get; set; } = Array.Empty<string>();
}

internal sealed class BootstrapCommandBatchReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("command_count")]
    public int CommandCount { get; set; }

    [JsonPropertyName("commands")]
    public BootstrapCommandBatchEntryReport[] Commands { get; set; } =
        Array.Empty<BootstrapCommandBatchEntryReport>();
}

internal sealed class BootstrapCommandBatchEntryReport
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("elapsed_ms")]
    public double ElapsedMilliseconds { get; set; }
}
