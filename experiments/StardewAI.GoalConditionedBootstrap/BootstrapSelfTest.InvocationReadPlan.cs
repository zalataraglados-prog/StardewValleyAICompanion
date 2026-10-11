using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyInvocationReadPlan()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-invocation-read-plan",
            Guid.NewGuid().ToString("N"));
        var outputRoot = Path.Combine(root, "output");
        Directory.CreateDirectory(outputRoot);
        var immutablePath = WriteFixture(root, "immutable.json");
        var exactOutputPath = WriteFixture(root, "exact-output.json");
        var outputDescendantPath = WriteFixture(
            outputRoot,
            "output-descendant.json");
        var ledgerPath = WriteFixture(root, "strategy-ledger.json");

        var arguments = Arguments.Parse(new[]
        {
            "--snapshot", immutablePath,
            "--before", exactOutputPath,
            "--output", exactOutputPath,
            "--after", outputDescendantPath,
            "--output-root", outputRoot,
            "--strategy-ledger", ledgerPath,
            "--read-session", "enabled"
        });
        var plan = arguments.InvocationReadPlan();
        using var session = InvocationReadSession.Start(
            plan.ImmutableInputPaths,
            plan.ProhibitedPaths,
            plan.ProhibitedRoots);

        Require(
            InvocationReadSession.IsPathReusable(immutablePath),
            "Ordinary frozen input was not registered for invocation reuse.");
        Require(
            !InvocationReadSession.IsPathReusable(exactOutputPath),
            "Input/output path collision entered invocation reuse.");
        Require(
            !InvocationReadSession.IsPathReusable(outputDescendantPath),
            "Output-root descendant entered invocation reuse.");
        Require(
            !InvocationReadSession.IsPathReusable(ledgerPath),
            "Mutable ledger entered invocation reuse.");
        Require(
            session.Snapshot().RegisteredPathCount == 1,
            "Invocation read plan registered an unexpected path count.");
    }

    private static string WriteFixture(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}" + Environment.NewLine);
        return path;
    }
}
