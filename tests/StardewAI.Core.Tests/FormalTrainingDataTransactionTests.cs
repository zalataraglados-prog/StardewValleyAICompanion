using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class FormalTrainingDataTransactionTests
{
    [Fact]
    public void FailedNativeBoundaryLeavesCanonicalArtifactsUnchanged()
    {
        var fixture = CreateFixture("failed");
        var transaction = FormalTrainingDataTransaction.Begin(fixture.Options);

        File.AppendAllText(fixture.Options.DatasetPath, "staged\n");
        File.WriteAllText(fixture.Options.EffectivePolicyCheckpointPath, "staged-checkpoint");
        transaction.Complete(verifiedTargetMet: false);

        Assert.Equal("baseline\n", File.ReadAllText(fixture.CanonicalDataset));
        Assert.Equal("baseline-checkpoint", File.ReadAllText(fixture.CanonicalCheckpoint));
        Assert.Equal("staged_not_committed", transaction.Status);
        Assert.False(transaction.CanonicalArtifactsUpdated);
        Assert.True(File.Exists(fixture.Options.DatasetPath));
    }

    [Fact]
    public void VerifiedNativeBoundaryPromotesStagedArtifacts()
    {
        var fixture = CreateFixture("verified");
        var transaction = FormalTrainingDataTransaction.Begin(fixture.Options);

        File.AppendAllText(fixture.Options.DatasetPath, "staged\n");
        File.WriteAllText(fixture.Options.EffectivePolicyCheckpointPath, "staged-checkpoint");
        transaction.Complete(verifiedTargetMet: true);

        Assert.Equal("baseline\nstaged\n", File.ReadAllText(fixture.CanonicalDataset));
        Assert.Equal("staged-checkpoint", File.ReadAllText(fixture.CanonicalCheckpoint));
        Assert.Equal("committed_after_native_save_boundary", transaction.Status);
        Assert.True(transaction.CanonicalArtifactsUpdated);
    }

    private static Fixture CreateFixture(string name)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-training-transaction-tests",
            name + "." + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(root, "runs", "run." + name);
        var manifest = Path.Combine(runDir, "training-run-manifest.json");
        var dataset = Path.Combine(root, "datasets", "live-training-feature-rows.jsonl");
        var checkpoint = Path.Combine(root, "checkpoints", "structured-policy-latest.json");
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dataset)!);
        Directory.CreateDirectory(Path.GetDirectoryName(checkpoint)!);
        File.WriteAllText(manifest, "{}");
        File.WriteAllText(dataset, "baseline\n");
        File.WriteAllText(checkpoint, "baseline-checkpoint");
        var options = new LiveTrainingOptions
        {
            Root = root,
            RunId = "run." + name,
            ManifestPath = manifest,
            PolicyCheckpointPath = checkpoint,
            RequireNativeSaveBoundary = true,
            RequireStructuredPolicy = true,
            UseProductExecutor = true,
            UseDailyPlan = true
        };
        return new Fixture(options, dataset, checkpoint);
    }

    private sealed record Fixture(
        LiveTrainingOptions Options,
        string CanonicalDataset,
        string CanonicalCheckpoint);
}
