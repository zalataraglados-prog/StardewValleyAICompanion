using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutSaveIdentityTests
{
    [Theory]
    [InlineData(FieldStatus.Stale, "save")]
    [InlineData(FieldStatus.Unavailable, "save")]
    [InlineData(FieldStatus.Error, "save")]
    [InlineData(FieldStatus.Derived, "save")]
    [InlineData(FieldStatus.Available, " ")]
    public void InitialBoundaryRejectsUnreadableSaveIdentity(string status, string value)
    {
        var root = NewRoot();
        var before = Snapshot("before", progressed: false);
        before.SaveId.Status = status;
        before.SaveId.Value = value;
        var path = Write(root, "before.json", before);

        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveInitialBoundaryValidator.Build(path, "run", "before"));

        Assert.Contains("readable", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, FieldStatus.Stale)]
    [InlineData(false, FieldStatus.Unavailable)]
    [InlineData(false, FieldStatus.Error)]
    [InlineData(false, FieldStatus.Derived)]
    [InlineData(true, FieldStatus.Stale)]
    [InlineData(true, FieldStatus.Unavailable)]
    [InlineData(true, FieldStatus.Error)]
    [InlineData(true, FieldStatus.Derived)]
    public void ScheduleRejectsUnreadableSaveIdentityAtEitherEndpoint(bool changeBefore, string status)
    {
        var (root, before, after, manifestPath) = Sequence();
        var snapshot = changeBefore ? before : after;
        snapshot.SaveId.Status = status;
        // Keep the separate initial-boundary snapshot valid. Only the episode
        // endpoint changes; values and receipt hashes still match.
        Write(root, changeBefore ? "before.json" : "after.json", snapshot);

        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestPath));

        Assert.Contains("readable save identity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleAcceptsAvailableSaveIdentityWithoutChangingEvidenceBinding()
    {
        var (_, _, _, manifestPath) = Sequence();

        var schedule = GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestPath);

        Assert.Equal("save", schedule.SourceSaveId);
        Assert.Equal("run", schedule.SourceRunId);
        Assert.False(schedule.TrainingLabelEligible);
        Assert.Equal(64, schedule.SourceEpisodeEvidenceRootSha256.Length);
    }

    private static (string Root, SnapshotEnvelope Before, SnapshotEnvelope After, string ManifestPath) Sequence()
    {
        var root = NewRoot();
        var before = Snapshot("before", progressed: false);
        var after = Snapshot("after", progressed: true);
        var initialPath = Write(root, "initial.json", before);
        var boundary = GoldenWalnutFreshSaveInitialBoundaryValidator.Build(initialPath, "run", "before");
        Write(root, "boundary.json", boundary);
        Write(root, "before.json", before);
        Write(root, "after.json", after);
        Write(root, "episode.json", new PlanExecutionEpisodeEnvelope
        {
            RunId = "run",
            SourceStateHash = "before",
            AfterStateHash = "after",
            BeforeSnapshotPath = "before.json",
            AfterSnapshotPath = "after.json",
            BeforeGameTick = 0,
            AfterGameTick = 1,
            AfterSnapshotFresh = true,
            ChangedFacts = JsonSerializer.SerializeToElement(new { }),
            Success = true
        });
        var manifestPath = Write(root, "manifest.json", new GoldenWalnutFreshSaveEpisodeManifest(
            GoldenWalnutFreshSaveEpisodeScheduleBuilder.ManifestSchemaVersion,
            "schedule", GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GoalId,
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            false, "run", "boundary.json", new[] { "episode.json" }));
        return (root, before, after, manifestPath);
    }

    private static SnapshotEnvelope Snapshot(string hash, bool progressed)
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(source => new
        {
            source_key = source.SourceKey,
            native_quantity = source.NativeQuantity,
            collected_quantity = 0,
            remaining_quantity = source.NativeQuantity,
            acquisition_option_id = source.AcquisitionOptionId
        }).ToArray();
        return new SnapshotEnvelope
        {
            GameVersion = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            StateHash = hash,
            SaveId = new() { Value = "save", Status = FieldStatus.Available },
            PlayerId = new() { Value = "player", Status = FieldStatus.Available },
            State = new Dictionary<string, JsonElement>
            {
                ["time"] = JsonSerializer.SerializeToElement(new
                {
                    total_days = new { value = progressed ? 100 : 0, status = "available" }
                }),
                ["locations"] = JsonSerializer.SerializeToElement(new
                {
                    ginger_island_access = new
                    {
                        value = new
                        {
                            schema_version = "ginger_island_access.v1",
                            journey = new { island_reached = progressed },
                            first_parrot = new { complete_or_pending = progressed },
                            western_turtle_parrot = new { complete_or_pending = progressed },
                            island_house_parrot = new { complete_or_pending = progressed }
                        },
                        status = "available"
                    }
                }),
                ["world_progress"] = JsonSerializer.SerializeToElement(new
                {
                    golden_walnut_sources = new
                    {
                        value = new
                        {
                            schema_version = GoldenWalnutNativeSourceCatalog.SchemaVersion,
                            projection_status = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
                            native_source_key_count = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
                            native_walnut_total = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
                            tracked_collected_quantity = 0,
                            world_golden_walnuts_found = 0,
                            sources
                        },
                        status = "available"
                    }
                })
            }
        };
    }

    private static string NewRoot()
    {
        // Keep hermetic evidence under ignored test output, inside the sandbox.
        var root = Path.Combine(AppContext.BaseDirectory, "save-identity-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Write<T>(string root, string name, T value)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, JsonSerializer.Serialize(value));
        return path;
    }
}
