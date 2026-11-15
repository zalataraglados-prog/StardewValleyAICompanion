using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public async Task ExplicitRetentionBoundaryReplaysOriginalMultiHopIngressAfterTheLastHarvest()
    {
        var f = await RetainedBoundaryFixture.Create();
        var proof = MoneyRevenueCropOutputRetentionProofBuilder.Build(f.RequestPath);
        Assert.Equal(f.LastSnapshotPath, proof.AfterSnapshotPath);
        Assert.NotEqual(f.HarvestSnapshotPath, proof.AfterSnapshotPath);
        Assert.Equal(1, proof.HarvestQueueCount);
        Assert.Equal(2, proof.HarvestActionCount);
        Assert.Equal(3, proof.ProducerRetention!.Segments.Length);
        Assert.True(proof.ProducerRetention.Segments[0].OwnedHarvest);
        Assert.All(proof.ProducerRetention.Segments.Skip(1), row => Assert.False(row.OwnedHarvest));
        Assert.Equal(proof.MinimumUnits, proof.ProducerRetention.RetainedQuantity);
        Assert.Equal(proof.MinimumUnits, proof.InventoryQuantityIncrease);
        Assert.False(proof.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task OldRetentionRequestsCannotSilentlyAdmitAnExplicitLaterBoundary(string version)
    {
        var f = await RetainedBoundaryFixture.Create();
        f.Request.SchemaVersion = "money_revenue_crop_output_retention_proof_request." + version;
        f.SaveRequest();
        Assert.Contains("request v3", Assert.Throws<InvalidDataException>(() =>
            MoneyRevenueCropOutputRetentionProofBuilder.Build(f.RequestPath)).Message);
        f.Request.RetainThroughSnapshotPath = null;
        f.SaveRequest();
        var old = MoneyRevenueCropOutputRetentionProofBuilder.Build(f.RequestPath);
        Assert.Equal(f.HarvestSnapshotPath, old.AfterSnapshotPath);
        Assert.Equal(version == "v2", old.ProducerRetention is not null);
        Assert.DoesNotContain("retain_through_snapshot_path", JsonSerializer.Serialize(f.Request, JsonOptions));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("relative")]
    [InlineData("unbound")]
    [InlineData("before_harvest")]
    public async Task ExplicitRetentionBoundaryRejectsMissingOrUnboundTerminalSources(string fault)
    {
        var f = await RetainedBoundaryFixture.Create();
        f.Request.RetainThroughSnapshotPath = fault switch
        {
            "missing" => null,
            "relative" => "later-snapshot.json",
            "unbound" => Path.Combine(f.Root, "not-an-original-after.json"),
            _ => f.Lifecycle.Transitions[0].AfterSnapshotPath
        };
        f.SaveRequest();
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropOutputRetentionProofBuilder.Build(f.RequestPath));
    }

    [Fact]
    public async Task ExplicitRetentionRebuildRejectsChangedLateRouteInsteadOfTrustingTheOldHarvest()
    {
        var f = await RetainedBoundaryFixture.Create();
        var after = Read<SnapshotEnvelope>(f.LastSnapshotPath);
        after.GameTick++;
        File.WriteAllText(f.LastSnapshotPath, JsonSerializer.Serialize(after, JsonOptions));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropOutputRetentionProofBuilder.Build(f.RequestPath));
    }

    [Fact]
    public async Task JointIngressStillRequiresTheOriginalFinalFarmBoundary()
    {
        var f = await RetainedBoundaryFixture.Create();
        var request = Read<MoneyRevenueCropNativeLifecycleProofRequest>(f.Lifecycle.RequestPath);
        request.Transitions = request.Transitions[..^1];
        var path = Path.Combine(f.Root, "incomplete-ingress-request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request, JsonOptions));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeLifecycleProofBuilder.Build(path));
    }

    private sealed class RetainedBoundaryFixture
    {
        internal string Root { get; }
        internal string RequestPath => Path.Combine(Root, "retained-through-head-request.json");
        internal string HarvestSnapshotPath { get; }
        internal string LastSnapshotPath { get; }
        internal MoneyRevenueCropNativeLifecycleProof Lifecycle { get; }
        internal MoneyRevenueCropOutputRetentionProofRequest Request { get; }

        private RetainedBoundaryFixture(string root, string harvestSnapshot, string lastSnapshot,
            MoneyRevenueCropNativeLifecycleProof lifecycle, MoneyRevenueCropOutputRetentionProofRequest request)
        {
            Root = root; HarvestSnapshotPath = harvestSnapshot; LastSnapshotPath = lastSnapshot;
            Lifecycle = lifecycle; Request = request;
        }

        internal void SaveRequest() => File.WriteAllText(RequestPath, JsonSerializer.Serialize(Request, JsonOptions));

        internal static async Task<RetainedBoundaryFixture> Create()
        {
            var original = new Fixture();
            var evidence = await original.Build();
            var retained = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(evidence.OutputRetentionProofPaths));
            var lifecycle = Read<MoneyRevenueCropNativeLifecycleProof>(retained.NativeLifecycleProofPath);
            var request = Read<MoneyRevenueCropNativeLifecycleProofRequest>(lifecycle.RequestPath);
            request.SchemaVersion = "money_revenue_crop_native_lifecycle_proof_request.v6";
            var rows = request.Transitions.ToList();
            var current = Read<SnapshotEnvelope>(retained.AfterSnapshotPath);
            var sequence = rows.Count;
            // Declared synthetic original connector queues, not a live-game route or Teacher acceptance.
            foreach (var destination in new[] { "FarmHouse", "Farm" })
            {
                object[] args = [original.Output, rows, current, retained.HarvestTotalDay, sequence,
                    "executor.traverse_connector", new[] { 5, 6 }, destination, false];
                current = MoneyCropTeacherFixture.CreateSnapshot("AddLifecycleTransition", args);
                sequence = (int)args[4];
                rows[^1] = rows[^1] with { TransitionKind = "supporting_route_to_crop" };
            }
            request.Transitions = rows.ToArray();
            var lifecycleRequest = Path.Combine(original.Output, "late-ingress-lifecycle-request.json");
            File.WriteAllText(lifecycleRequest, JsonSerializer.Serialize(request, JsonOptions));
            lifecycle = MoneyRevenueCropNativeLifecycleProofBuilder.Build(lifecycleRequest);
            var lifecyclePath = Path.Combine(original.Output, "late-ingress-lifecycle-proof.json");
            File.WriteAllText(lifecyclePath, JsonSerializer.Serialize(lifecycle, JsonOptions));
            var retentionRequest = Read<MoneyRevenueCropOutputRetentionProofRequest>(retained.RequestPath);
            retentionRequest.SchemaVersion = "money_revenue_crop_output_retention_proof_request.v3";
            retentionRequest.NativeLifecycleProofPath = lifecyclePath;
            retentionRequest.RetainThroughSnapshotPath = rows[^1].AfterSnapshotPath;
            var result = new RetainedBoundaryFixture(original.Output, retained.AfterSnapshotPath,
                rows[^1].AfterSnapshotPath, lifecycle, retentionRequest);
            result.SaveRequest();
            return result;
        }
    }
}
