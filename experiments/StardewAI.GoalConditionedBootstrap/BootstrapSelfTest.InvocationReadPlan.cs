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

        VerifyInvocationDeterministicBuildSession(immutablePath);
    }

    private static void VerifyInvocationDeterministicBuildSession(
        string immutablePath)
    {
        var builds = 0;
        using (var buildSession =
            InvocationDeterministicBuildSession.Start())
        {
            var identity = new[]
            {
                InvocationDeterministicBuildSession.File(
                    "FixturePath",
                    immutablePath)
            };
            var first = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test",
                "v1",
                identity,
                () =>
                {
                    builds++;
                    return new DeterministicBuildFixture { Value = 1 };
                });
            first.Value = 2;
            var second = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test",
                "v1",
                identity,
                () =>
                {
                    builds++;
                    return new DeterministicBuildFixture { Value = 3 };
                });
            var snapshot = buildSession.Snapshot();
            Require(
                builds == 1 && second.Value == 1 &&
                snapshot.BuildCount == 1 && snapshot.CacheHitCount == 1 &&
                snapshot.CachedBytes > 0,
                "Invocation deterministic build reuse is not isolated and exact.");

            var referenceBuilds = 0;
            var firstReference =
                InvocationDeterministicBuildSession.GetOrBuildReference(
                    "self-test-reference",
                    "v1",
                    identity,
                    () =>
                    {
                        referenceBuilds++;
                        return new DeterministicBuildFixture { Value = 6 };
                    });
            var secondReference =
                InvocationDeterministicBuildSession.GetOrBuildReference(
                    "self-test-reference",
                    "v1",
                    identity,
                    () =>
                    {
                        referenceBuilds++;
                        return new DeterministicBuildFixture { Value = 7 };
                    });
            Require(
                referenceBuilds == 1 &&
                ReferenceEquals(firstReference, secondReference),
                "Invocation immutable reference reuse is not scoped and exact.");
        }

        using (InvocationDeterministicBuildSession.Start(enabled: false))
        {
            var identity = new[]
            {
                InvocationDeterministicBuildSession.File(
                    "FixturePath",
                    immutablePath)
            };
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-disabled",
                "v1",
                identity,
                () =>
                {
                    builds++;
                    return new DeterministicBuildFixture { Value = 4 };
                });
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-disabled",
                "v1",
                identity,
                () =>
                {
                    builds++;
                    return new DeterministicBuildFixture { Value = 5 };
                });
            var firstReference =
                InvocationDeterministicBuildSession.GetOrBuildReference(
                    "self-test-reference-disabled",
                    "v1",
                    identity,
                    () => new DeterministicBuildFixture { Value = 8 });
            var secondReference =
                InvocationDeterministicBuildSession.GetOrBuildReference(
                    "self-test-reference-disabled",
                    "v1",
                    identity,
                    () => new DeterministicBuildFixture { Value = 9 });
            Require(
                !ReferenceEquals(firstReference, secondReference),
                "Disabled immutable reference reuse changed fallback behavior.");
        }
        Require(
            builds == 3,
            "Disabled deterministic build reuse changed fallback behavior.");
    }

    private static string WriteFixture(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}" + Environment.NewLine);
        return path;
    }

    private sealed class DeterministicBuildFixture
    {
        public int Value { get; set; }
    }
}
