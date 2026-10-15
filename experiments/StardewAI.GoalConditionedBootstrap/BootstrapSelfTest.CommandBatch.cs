using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunCommandBatch()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-bootstrap-command-batch",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var calls = new List<string>();
        var registry = new Dictionary<string, CommandDefinition>(
            StringComparer.Ordinal)
        {
            ["record"] = new(
                "record",
                options => calls.Add(options.Required("value")),
                BatchSafe: true),
            ["fail"] = new(
                "fail",
                _ => throw new InvalidOperationException("expected failure"),
                BatchSafe: true),
            ["unsafe"] = new("unsafe", _ => { })
        };

        var valid = WriteCommandBatchManifest(
            root,
            "valid.json",
            new[]
            {
                new[] { "record", "--value", "first" },
                new[] { "record", "--value", "second" }
            });
        var report = BootstrapCommandBatchRunner.Run(
            valid,
            Path.Combine(root, "valid-report.json"),
            registry);
        Require(
            report.Status == "passed" &&
            report.CommandCount == 2 &&
            calls.SequenceEqual(new[] { "first", "second" }),
            "Bootstrap command batch did not preserve sequential order.");

        calls.Clear();
        var failFast = WriteCommandBatchManifest(
            root,
            "fail-fast.json",
            new[]
            {
                new[] { "record", "--value", "before" },
                new[] { "fail" },
                new[] { "record", "--value", "after" }
            });
        ExpectCommandBatchFailure(
            () => BootstrapCommandBatchRunner.Run(
                failFast,
                Path.Combine(root, "fail-fast-report.json"),
                registry),
            "expected failure");
        Require(
            calls.SequenceEqual(new[] { "before" }),
            "Bootstrap command batch did not stop at the first failure.");

        var duplicateOutput = Path.Combine(root, "duplicate-output.json");
        var duplicate = WriteCommandBatchManifest(
            root,
            "duplicate.json",
            new[]
            {
                new[]
                {
                    "record", "--value", "first",
                    "--output", duplicateOutput
                },
                new[]
                {
                    "record", "--value", "second",
                    "--selected-output", duplicateOutput
                }
            });
        ExpectCommandBatchFailure(
            () => BootstrapCommandBatchRunner.Run(
                duplicate,
                Path.Combine(root, "duplicate-report.json"),
                registry),
            "duplicate output");

        var recursive = WriteCommandBatchManifest(
            root,
            "recursive.json",
            new[] { new[] { "run-command-batch" } });
        ExpectCommandBatchFailure(
            () => BootstrapCommandBatchRunner.Run(
                recursive,
                Path.Combine(root, "recursive-report.json"),
                registry),
            "Recursive bootstrap command batches");

        var unsafeCommand = WriteCommandBatchManifest(
            root,
            "unsafe.json",
            new[] { new[] { "unsafe" } });
        ExpectCommandBatchFailure(
            () => BootstrapCommandBatchRunner.Run(
                unsafeCommand,
                Path.Combine(root, "unsafe-report.json"),
                registry),
            "not batch-safe");

        var controllerCollision = WriteCommandBatchManifest(
            root,
            "controller-collision.json",
            new[]
            {
                new[]
                {
                    "record", "--value", "collision",
                    "--output", Path.Combine(root, "controller-report.json")
                }
            });
        ExpectCommandBatchFailure(
            () => BootstrapCommandBatchRunner.Run(
                controllerCollision,
                Path.Combine(root, "controller-report.json"),
                registry),
            "duplicate output");

        Console.WriteLine("bootstrap command batch self-test passed");
    }

    private static void ExpectCommandBatchFailure(
        Action action,
        string expectedMessage)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex.Message.Contains(
            expectedMessage,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected bootstrap command batch failure: " + expectedMessage);
    }

    private static string WriteCommandBatchManifest(
        string root,
        string name,
        string[][] commands)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new BootstrapCommandBatchManifest
                {
                    SchemaVersion = BootstrapCommandBatchRunner.ManifestSchema,
                    Commands = commands.Select(arguments =>
                        new BootstrapCommandBatchEntry
                        {
                            Arguments = arguments
                        }).ToArray()
                },
                JsonDefaults.Options));
        return path;
    }
}
