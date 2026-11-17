using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class ManagedEpisodeArchiveTests
{
    [Fact]
    public void ActualDatasetWriterArchivesANonemptySequentialQueueThroughManagedAndOriginalPaths()
    {
        var f = new Fixture();
        var secondAfterPath = Path.Combine(f.Root, "after-second.json");
        var secondAfter = JsonNode.Parse(File.ReadAllText(f.Episode.AfterSnapshotPath))!;
        secondAfter["game_tick"] = 3;
        File.WriteAllText(secondAfterPath, secondAfter.ToJsonString());
        var items = new JsonArray();
        var steps = new JsonArray();
        for (var index = 0; index < 2; index++)
        {
            var id = "declared-step-" + index;
            items.Add(new JsonObject { ["queue_item_id"] = id, ["option_id"] = "executor.wait" });
            steps.Add(new JsonObject
            {
                ["queue_item_id"] = id, ["option_id"] = "executor.wait", ["status"] = "blocked",
                ["primitive_verification_status"] = "unverified", ["after_snapshot_fresh"] = true,
                ["effective_before_state_hash"] = f.Episode.SourceStateHash, ["after_state_hash"] = f.Episode.AfterStateHash,
                ["effective_before_snapshot_path"] = index == 0 ? f.BeforePath : f.Episode.AfterSnapshotPath,
                ["after_snapshot_path"] = index == 0 ? f.Episode.AfterSnapshotPath : secondAfterPath,
                ["before_game_tick"] = (long)index + 1, ["after_game_tick"] = (long)index + 2,
                ["execution_path"] = f.Episode.ExecutionResultPath
            });
        }
        var queue = new JsonObject { ["items"] = items };
        var execution = (JsonObject)steps[0]!.DeepClone();
        execution["step_results"] = steps;
        var append = new TrainingDatasetAppendResult { EpisodeId = "declared-sequential-archive", RowId = string.Empty };
        typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("WritePlanExecutionEpisode", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [f.Options, 1, f.BeforePath, "declared-model", "declared-queue", queue, execution, append,
                f.Episode.SourceStateHash, "declared-queue-id"]);
        var archivedPaths = Directory.GetFiles(f.Root, GoldenWalnutFreshSaveEpisodeArchive.EpisodeSearchPattern);
        Assert.Equal(2, archivedPaths.Length);
        Assert.Single(Directory.GetFiles(f.Root, "golden-walnut-managed-input-*.json"));
        Assert.Single(Directory.GetFiles(f.Root, "golden-walnut-managed-before-*.json"));
        var archived = archivedPaths.Select(path => JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(File.ReadAllText(path), JsonOptions)!)
            .OrderBy(row => row.BeforeGameTick).ToArray();
        Assert.Equal(new[] { 1L, 2L }, archived.Select(row => row.BeforeGameTick));
        Assert.All(archived, row => Assert.False(row.Success));
        Assert.All(archived, row => Assert.Equal("unverified", row.PrimitiveVerificationStatus));
        Assert.Equal(f.BeforePath, JsonNode.Parse(File.ReadAllText(Path.Combine(f.Root, "plan-execution-episode-0001-item-0001.json")))!
            ["before_snapshot_path"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("content_addressed_gzip")]
    public void ManagedNestedSourcesKeepExactLogicalContentAndOriginalEvidence(string mode)
    {
        var f = new Fixture(mode: mode);
        var beforeHash = f.Hash(f.BeforePath); var text = f.OriginalBefore;
        var captured = f.Capture();
        Assert.True(captured.Captured);
        Assert.Equal(f.OriginalEpisode, File.ReadAllText(f.EpisodePath));
        Assert.Equal(beforeHash, f.Hash(f.BeforePath));
        var archived = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(File.ReadAllText(captured.ArchiveEpisodePath), JsonOptions)!;
        Assert.Equal(text, ContentAddressedJsonArtifactReader.ReadAllText(Path.Combine(f.Root, archived.BeforeSnapshotPath)));
        Assert.Equal(f.Episode.SourceStateHash, archived.SourceStateHash);
        Assert.Equal(f.Episode.PrimitiveVerificationStatus, archived.PrimitiveVerificationStatus);
        Assert.False(archived.Success);
        Assert.Equal("blocked", archived.Status);
        var boundaryPath = GoldenWalnutFreshSaveInitialBoundaryValidator.PathFor(f.Root, f.Options.RunId);
        var boundary = JsonSerializer.Deserialize<GoldenWalnutFreshSaveInitialBoundary>(File.ReadAllText(boundaryPath), JsonOptions)!;
        Assert.Equal(f.Episode.SourceStateHash,
            GoldenWalnutFreshSaveInitialBoundaryValidator.Validate(boundary, boundaryPath, f.Options.RunId).StateHash);
        var descriptor = JsonNode.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(f.Root, "golden-walnut-managed-input-*.json"))))!;
        Assert.False(descriptor["managed_archive_source_reference"]!["formal_product_training_authorized"]!.GetValue<bool>());
        var files = Directory.GetFiles(f.Root).Select(path => (path, f.Hash(path))).ToArray();
        Assert.Equal(captured.ArchiveEpisodePath, f.Capture().ArchiveEpisodePath);
        foreach (var (path, hash) in files) Assert.Equal(hash, f.Hash(path));
        Assert.Equal(files.Length, Directory.GetFiles(f.Root).Length);
        Assert.Throws<InvalidDataException>(() => GoldenWalnutFreshSaveEpisodeArchive.Capture(f.EpisodePath, 1, f.Options.RunId));
    }

    [Fact]
    public void ManagedTopLevelSourceUsesTheOriginalArchiveWithoutAliases()
    {
        var f = new Fixture(nested: false);
        Assert.True(f.Capture().Captured);
        Assert.Empty(Directory.GetFiles(f.Root, "golden-walnut-managed-*.json"));
        Assert.Equal(f.OriginalEpisode, File.ReadAllText(f.EpisodePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureChecksDiskBlobClosureEvenWithWarmMaterialization(bool corrupt)
    {
        var f = new Fixture(mode: "content_addressed_gzip");
        var manifestHash = f.Hash(f.BeforePath);
        using var session = InvocationReadSession.Start(new[] { f.BeforePath });
        Assert.Equal(f.OriginalBefore, ContentAddressedJsonArtifactReader.ReadAllText(f.BeforePath));
        if (!corrupt)
        {
            Assert.True(f.Capture().Captured);
            return;
        }
        var blob = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(f.BeforePath)!, "_snapshot-blobs"),
            "*.json.gz", SearchOption.AllDirectories)[0];
        File.WriteAllBytes(blob, [1, 2, 3]);
        Assert.Equal(manifestHash, f.Hash(f.BeforePath));
        var error = Assert.Throws<TargetInvocationException>(() => f.Capture());
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Empty(Directory.GetFiles(f.Root, GoldenWalnutFreshSaveEpisodeArchive.EpisodeSearchPattern));
        Assert.Equal(f.OriginalEpisode, File.ReadAllText(f.EpisodePath));
    }

    [Fact]
    public void UnmanagedNestedSourceRetainsTheOriginalSameDirectoryRefusal()
    {
        var f = new Fixture(); f.Options.UseManagedCropTeacher = false;
        var error = Assert.Throws<TargetInvocationException>(() => f.Capture());
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Empty(Directory.GetFiles(f.Root, "golden-walnut-managed-*.json"));
    }

    [Fact]
    public void NonfreshWitnessKeepsTheOriginalNoCaptureResultWithoutReadingMissingSources()
    {
        var f = new Fixture(); f.Episode.AfterSnapshotFresh = false; f.Episode.BeforeSnapshotPath = "missing.json"; f.SaveEpisode();
        Assert.False(f.Capture().Captured);
        Assert.Empty(Directory.GetFiles(f.Root, "golden-walnut-managed-*.json"));
    }

    [Theory]
    [InlineData("wrong_schema")]
    [InlineData("wrong_run")]
    [InlineData("missing_source")]
    [InlineData("missing_after")]
    [InlineData("external_source")]
    [InlineData("alias_collision")]
    public void InvalidOrCollidingManagedInputsCannotOverwriteOriginalEvidence(string fault)
    {
        var f = new Fixture(); var beforeHash = f.Hash(f.BeforePath);
        if (fault == "wrong_schema") f.Episode.SchemaVersion = "invalid";
        if (fault == "wrong_run") f.Episode.RunId = "another-run";
        if (fault == "missing_source") f.Episode.BeforeSnapshotPath += ".missing";
        if (fault == "missing_after") f.Episode.AfterSnapshotPath += ".missing";
        if (fault == "external_source")
        {
            var outside = Path.Combine(Path.GetDirectoryName(f.Root)!, "external.json");
            File.WriteAllText(outside, f.OriginalBefore); f.Episode.BeforeSnapshotPath = outside;
        }
        if (fault == "alias_collision") File.WriteAllText(f.AliasPath, "retained-collision");
        f.SaveEpisode();
        var error = Assert.Throws<TargetInvocationException>(() => f.Capture());
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(f.OriginalEpisode, File.ReadAllText(f.EpisodePath));
        Assert.Equal(beforeHash, f.Hash(f.BeforePath));
        Assert.Empty(Directory.GetFiles(f.Root, "golden-walnut-fresh-save-episode-*.json"));
        if (fault == "alias_collision") Assert.Equal("retained-collision", File.ReadAllText(f.AliasPath));
        else Assert.Empty(Directory.GetFiles(f.Root, "golden-walnut-managed-*.json"));
    }

    [Fact]
    public void ChangedArchiveInputIsRetainedInsteadOfRewrittenOrRecaptured()
    {
        var f = new Fixture(); var captured = f.Capture(); var acceptedHash = f.Hash(captured.ArchiveEpisodePath);
        var input = Assert.Single(Directory.GetFiles(f.Root, "golden-walnut-managed-input-*.json"));
        File.WriteAllText(input, "retained-changed-input");
        var error = Assert.Throws<TargetInvocationException>(() => f.Capture());
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal("retained-changed-input", File.ReadAllText(input));
        Assert.Equal(acceptedHash, f.Hash(captured.ArchiveEpisodePath));
    }
}
