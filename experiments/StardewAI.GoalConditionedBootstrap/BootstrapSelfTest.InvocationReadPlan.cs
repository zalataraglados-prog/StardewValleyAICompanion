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

        var firstHash = ContentInventoryVerifier.HashFile(immutablePath);
        var secondHash = ContentInventoryVerifier.HashFile(immutablePath);
        var readSnapshot = session.Snapshot();
        Require(
            firstHash == secondHash &&
            readSnapshot.PhysicalReadCount == 1 &&
            readSnapshot.FullHashCount == 1 &&
            readSnapshot.CacheHitCount >= 1,
            "Content authority hashing bypassed invocation read reuse.");

        VerifyInvocationDeterministicBuildSession(
            immutablePath,
            firstHash);
    }

    private static void VerifyInvocationDeterministicBuildSession(
        string immutablePath,
        string immutableSha256)
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
                snapshot.EntryCount == 1 &&
                snapshot.CachedBytes > 0 && snapshot.MaxEntryBytes > 0 &&
                snapshot.Producers.Length == 1 &&
                snapshot.Producers[0].ProducerId == "self-test" &&
                snapshot.Producers[0].BuildCount == 1 &&
                snapshot.Producers[0].CacheHitCount == 1,
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
                ReferenceEquals(firstReference, secondReference) &&
                buildSession.Snapshot().Producers.Single(producer =>
                    producer.ProducerId == "self-test-reference")
                    .ReferenceHitCount == 1,
                "Invocation immutable reference reuse is not scoped and exact.");

            var axisBuilds = 0;
            var firstAxis = AcquisitionRouteDependencyObservation.ObserveAxis(
                "self-test-axis",
                () =>
                {
                    axisBuilds++;
                    return new DeterministicBuildFixture { Value = 12 };
                },
                immutablePath);
            firstAxis.Value = 13;
            var secondAxis = AcquisitionRouteDependencyObservation.ObserveAxis(
                "self-test-axis",
                () =>
                {
                    axisBuilds++;
                    return new DeterministicBuildFixture { Value = 14 };
                },
                immutablePath);
            var axisSnapshot = buildSession.Snapshot();
            Require(
                axisBuilds == 1 && secondAxis.Value == 12 &&
                axisSnapshot.BuildCount == 2 &&
                axisSnapshot.CacheHitCount == 2,
                "Acquisition axis reuse is not invocation-scoped and isolated.");

            var transitiveBuilds = 0;
            var verifiedIdentity = new[]
            {
                InvocationDeterministicBuildSession.VerifiedTransitiveFile(
                    "AuthoritySource",
                    immutablePath,
                    immutableSha256)
            };
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-verified-transitive",
                "v1",
                verifiedIdentity,
                () =>
                {
                    transitiveBuilds++;
                    return new DeterministicBuildFixture { Value = 15 };
                });
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-verified-transitive",
                "v1",
                verifiedIdentity,
                () =>
                {
                    transitiveBuilds++;
                    return new DeterministicBuildFixture { Value = 16 };
                });
            var changedIdentity = new[]
            {
                InvocationDeterministicBuildSession.VerifiedTransitiveFile(
                    "AuthoritySource",
                    immutablePath,
                    new string(
                        immutableSha256[0] == 'a' ? 'b' : 'a',
                        64))
            };
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-verified-transitive",
                "v1",
                changedIdentity,
                () =>
                {
                    transitiveBuilds++;
                    return new DeterministicBuildFixture { Value = 17 };
                });
            var transitiveSnapshot = buildSession.Snapshot();
            Require(
                transitiveBuilds == 2 &&
                transitiveSnapshot.BuildCount == 4 &&
                transitiveSnapshot.CacheHitCount == 3,
                "Verified transitive identity was reused across content drift.");
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

        var optionalPathBuilds = 0;
        using (var optionalPathSession =
            InvocationDeterministicBuildSession.Start())
        {
            var identity = InvocationDeterministicBuildSession.Describe(
                new OptionalPathFixture
                {
                    RequiredPath = immutablePath,
                    OptionalPath = string.Empty
                });
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-optional-path",
                "v1",
                identity,
                () =>
                {
                    optionalPathBuilds++;
                    return new DeterministicBuildFixture { Value = 10 };
                });
            _ = InvocationDeterministicBuildSession.GetOrBuild(
                "self-test-optional-path",
                "v1",
                identity,
                () =>
                {
                    optionalPathBuilds++;
                    return new DeterministicBuildFixture { Value = 11 };
                });
            var snapshot = optionalPathSession.Snapshot();
            Require(
                optionalPathBuilds == 1 &&
                snapshot.BuildCount == 1 &&
                snapshot.CacheHitCount == 1,
                "Empty optional path did not remain an explicit reusable value identity.");
        }

        var budgetBuilds = 0;
        using (var budgetSession = InvocationDeterministicBuildSession.Start(
                   cacheBudgetBytes: 0,
                   entryLimitBytes: 1024))
        {
            var identity = new[]
            {
                InvocationDeterministicBuildSession.File(
                    "FixturePath",
                    immutablePath)
            };
            for (var index = 0; index < 2; index++)
            {
                _ = InvocationDeterministicBuildSession.GetOrBuild(
                    "self-test-budget",
                    "v1",
                    identity,
                    () =>
                    {
                        budgetBuilds++;
                        return new DeterministicBuildFixture { Value = index };
                    });
            }
            var snapshot = budgetSession.Snapshot();
            Require(
                budgetBuilds == 2 &&
                snapshot.EntryCount == 0 &&
                snapshot.RejectedEntryCount == 2 &&
                snapshot.CachedBytes == 0,
                "Deterministic cache budget did not fail open to recomputation.");
        }
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

    private sealed class OptionalPathFixture
    {
        public string RequiredPath { get; init; } = string.Empty;

        public string OptionalPath { get; init; } = string.Empty;
    }
}
