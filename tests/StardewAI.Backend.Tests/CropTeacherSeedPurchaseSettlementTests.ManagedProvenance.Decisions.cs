using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedProvenanceDecisionBindingsRetainOriginalSelectionAndFreshRebindWithoutEquatingTheirSources(bool rebound)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var plan = Path.Combine(f.Witness.Root, "original-selected-plan.json");
        var queue = Path.Combine(f.Witness.Root, "fresh-compiled-queue.json");
        var ranking = Path.Combine(f.Witness.Root, "original-ranking.json");
        var execution = Path.Combine(f.Witness.Root, "original-execution.json");
        Fixture.Save(plan, new { original = "selected-plan" });
        Fixture.Save(queue, new { original = "fresh-queue" });
        Fixture.Save(ranking, new { original = "ranking" });
        var effectiveSource = rebound ? new string('d', 64) : c.Episode.SourceStateHash;
        Fixture.Save(execution, new
        {
            status = "applied", effective_model_plan_path = plan, effective_ranking_path = ranking,
            effective_compiled_queue_path = queue, effective_decision_snapshot_path = c.SourcePath,
            effective_decision_source_state_hash = effectiveSource, effective_decision_revision = rebound ? 1 : 0,
            effective_selected_candidate_id = "original-choice", effective_selected_queue_index = 0
        });
        c.Episode.ModelPlanPath = plan; c.Episode.CompiledQueuePath = queue; c.Episode.ExecutionResultPath = execution;
        var episodePath = Directory.GetFiles(c.Request.PriorEpisodeDirectories.Single(), "plan-execution-episode-*.json").Single();
        Fixture.Save(episodePath, c.Episode);
        var report = MoneyRevenueCropManagedProvenance.Prepare(c.Path).Build();
        var row = Assert.Single(report["segments"]![0]!["decision_artifact_bindings"]!.AsArray())!;
        Assert.Equal(effectiveSource, row["effective_decision_source_state_hash"]!.GetValue<string>());
        Assert.Equal(c.Episode.SourceStateHash, row["effective_decision_snapshot_state_hash"]!.GetValue<string>());
        Assert.Equal("original-choice", row["effective_selected_candidate_id"]!.GetValue<string>());
        foreach (var path in new[] { plan, queue, ranking, execution, c.SourcePath })
            Assert.Equal(SiteTillNativeFixture.FileHash(path), report["input_artifact_hashes"]![path]!.GetValue<string>());
        Assert.Equal(7, row["artifacts"]!.AsArray().Count);
        Assert.False(report["decision_lineage_replayed"]!.GetValue<bool>());
        Assert.False(report["global_route_verified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedProvenanceDecisionBindingsRejectMissingOrChangedDeclaredArtifacts(bool changed)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var plan = Path.Combine(f.Witness.Root, "declared-plan.json");
        c.Episode.ModelPlanPath = plan;
        Fixture.Save(Directory.GetFiles(c.Request.PriorEpisodeDirectories.Single(), "plan-execution-episode-*.json").Single(), c.Episode);
        if (changed) Fixture.Save(plan, new { retained = true });
        var context = MoneyRevenueCropManagedProvenance.Prepare(c.Path);
        if (changed) { context.Build(); File.AppendAllText(plan, " "); }
        Assert.Throws<InvalidDataException>(() => context.Build());
    }

    [Fact]
    public async Task ManagedProvenanceDecisionBindingsRetainAggregateAndChildArtifactsWithoutDoubleCountingObservations()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var parentPlan = Path.Combine(f.Witness.Root, "parent-plan.json");
        var childPlan = Path.Combine(f.Witness.Root, "child-plan.json");
        Fixture.Save(parentPlan, new { parent = true }); Fixture.Save(childPlan, new { child = true });
        var path = Directory.GetFiles(c.Request.PriorEpisodeDirectories.Single(), "plan-execution-episode-*.json").Single();
        c.Episode.ModelPlanPath = parentPlan; Fixture.Save(path, c.Episode);
        var child = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(JsonSerializer.Serialize(c.Episode, JsonOptions), JsonOptions)!;
        child.EpisodeId += ".item.0001"; child.ModelPlanPath = childPlan;
        Fixture.Save(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-item-0001.json"), child);
        var report = MoneyRevenueCropManagedProvenance.Prepare(c.Path).Build();
        Assert.Equal(2, report["retained_episode_payload_count"]!.GetValue<int>());
        Assert.Equal(1, report["observation_episode_count"]!.GetValue<int>());
        Assert.Equal(2, report["segments"]![0]!["decision_artifact_bindings"]!.AsArray().Count);
        Assert.NotNull(report["input_artifact_hashes"]![parentPlan]); Assert.NotNull(report["input_artifact_hashes"]![childPlan]);
    }

    [Fact]
    public async Task ManagedProvenanceDecisionBindingsPreserveAbsentLegacyOptionalPaths()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var report = MoneyRevenueCropManagedProvenance.Prepare(c.Path).Build();
        var row = Assert.Single(report["segments"]![0]!["decision_artifact_bindings"]!.AsArray())!;
        Assert.Empty(row["artifacts"]!.AsArray());
        Assert.Null(row["effective_decision_source_state_hash"]);
        Assert.False(report["decision_lineage_replayed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ManagedProvenanceDecisionBindingsRecheckDistinctDecisionSnapshotBlobWithoutChangingItsManifest()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var snapshot = Path.Combine(f.Witness.Root, "decision-only", "snapshot.json");
        await ContentAddressedJsonArtifactStore.WriteAsync(snapshot, File.ReadAllText(c.SourcePath), "content_addressed_gzip");
        var execution = Path.Combine(f.Witness.Root, "decision-only-execution.json");
        Fixture.Save(execution, new { effective_decision_snapshot_path = snapshot });
        c.Episode.ExecutionResultPath = execution;
        Fixture.Save(Directory.GetFiles(c.Request.PriorEpisodeDirectories.Single(), "plan-execution-episode-*.json").Single(), c.Episode);
        using var cache = InvocationReadSession.Start(Array.Empty<string>(), enabled: true);
        var context = MoneyRevenueCropManagedProvenance.Prepare(c.Path);
        context.Build();
        _ = ContentAddressedJsonArtifactReader.ReadAllText(snapshot);
        var original = SiteTillNativeFixture.FileHash(snapshot);
        var blob = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(snapshot)!, "_snapshot-blobs"), "*.json.gz", SearchOption.AllDirectories).First();
        var bytes = File.ReadAllBytes(blob); bytes[0] ^= 0xff; File.WriteAllBytes(blob, bytes);
        Assert.Equal(original, SiteTillNativeFixture.FileHash(snapshot));
        Assert.Throws<InvalidDataException>(() => context.Build());
    }
}
