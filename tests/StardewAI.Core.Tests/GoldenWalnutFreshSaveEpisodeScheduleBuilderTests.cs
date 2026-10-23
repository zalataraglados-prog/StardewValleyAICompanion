using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveEpisodeScheduleBuilderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public void BuildsScheduleOnlyFromFreshSuccessfulNativeTransitions()
    {
        using var fixture = new Fixture();
        var source = EligibleSource();
        var states = new[]
        {
            Snapshot("h0", 100, 0, false, false, false, false, source.SourceKey),
            Snapshot("h1", 100, 1, true, false, false, false, source.SourceKey),
            Snapshot("h2", 101, 1, true, true, false, false, source.SourceKey),
            Snapshot("h3", 102, 1, true, true, true, false, source.SourceKey),
            Snapshot("h4", 103, 1, true, true, true, true, source.SourceKey)
        };
        var episodePaths = fixture.WriteSequence(states);
        var manifestPath = fixture.WriteManifest(episodePaths);

        var schedule = GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestPath);

        Assert.Equal(100, schedule.FirstIslandArrivalTotalDay);
        Assert.Equal(101, schedule.PlannedFirstParrotUnlockTotalDay);
        Assert.Equal(102, schedule.PlannedWesternTurtleUnlockTotalDay);
        Assert.Equal(103, schedule.PlannedIslandHouseUnlockTotalDay);
        Assert.Equal("fresh-save-run", schedule.SourceRunId);
        Assert.Equal("fresh-save-test", schedule.SourceSaveId);
        Assert.Equal(4, schedule.SourceEpisodeCount);
        Assert.Equal(64, schedule.SourceEpisodeManifestSha256.Length);
        Assert.Equal(64, schedule.SourceEpisodeEvidenceRootSha256.Length);
        var completion = Assert.Single(schedule.SourceCompletions);
        Assert.Equal(source.SourceKey, completion.SourceKey);
        Assert.Equal(1, completion.PlannedQuantity);
        Assert.Equal(100, completion.PlannedCompletionTotalDay);
        Assert.False(schedule.TrainingLabelEligible);
    }

    [Fact]
    public void RejectsSourceDeltaWithoutFreshSuccessfulReceipt()
    {
        using var fixture = new Fixture();
        var source = EligibleSource();
        var before = Snapshot("h0", 100, 0, false, false, false, false, source.SourceKey);
        var after = Snapshot("h1", 100, 1, true, false, false, false, source.SourceKey);
        var episodePath = fixture.WriteEpisode(0, before, after, success: false);
        var manifestPath = fixture.WriteManifest(new[] { episodePath });

        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestPath));

        Assert.Contains("successful episodes", error.Message, StringComparison.Ordinal);
    }

    private static GoldenWalnutNativeSourceDefinition EligibleSource() =>
        GoldenWalnutNativeSourceCatalog.Sources.First(source =>
            GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(
                source.AcquisitionOptionId) &&
            source.OptionalUnlockWalnutCost == 0 &&
            !source.ResortOnly &&
            source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal));

    private static SnapshotEnvelope Snapshot(
        string hash,
        int day,
        int collectedQuantity,
        bool islandReached,
        bool firstParrot,
        bool westernTurtle,
        bool islandHouse,
        string changedSourceKey)
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources
            .Select(source => new
            {
                source_key = source.SourceKey,
                native_quantity = source.NativeQuantity,
                collected_quantity = source.SourceKey == changedSourceKey
                    ? collectedQuantity
                    : 0,
                remaining_quantity = source.NativeQuantity -
                    (source.SourceKey == changedSourceKey ? collectedQuantity : 0)
            })
            .ToArray();
        return new SnapshotEnvelope
        {
            GameVersion = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            SaveId = new FieldEnvelope<string?>
            {
                Value = "fresh-save-test",
                Status = FieldStatus.Available
            },
            StateHash = hash,
            State = new Dictionary<string, JsonElement>
            {
                ["time"] = JsonSerializer.SerializeToElement(new
                {
                    total_days = new { value = day, status = "available" }
                }),
                ["locations"] = JsonSerializer.SerializeToElement(new
                {
                    ginger_island_access = new
                    {
                        value = new
                        {
                            journey = new { island_reached = islandReached },
                            first_parrot = new { complete_or_pending = firstParrot },
                            western_turtle_parrot = new { complete_or_pending = westernTurtle },
                            island_house_parrot = new { complete_or_pending = islandHouse }
                        },
                        status = "available"
                    }
                }),
                ["world_progress"] = JsonSerializer.SerializeToElement(new
                {
                    golden_walnut_sources = new
                    {
                        value = new { sources },
                        status = "available"
                    }
                })
            }
        };
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-walnut-schedule-" + Guid.NewGuid().ToString("N"));

        public Fixture() => Directory.CreateDirectory(root);

        public string[] WriteSequence(IReadOnlyList<SnapshotEnvelope> snapshots)
        {
            var paths = new List<string>();
            for (var index = 0; index < snapshots.Count - 1; index++)
                paths.Add(WriteEpisode(index, snapshots[index], snapshots[index + 1]));
            return paths.ToArray();
        }

        public string WriteEpisode(
            int index,
            SnapshotEnvelope before,
            SnapshotEnvelope after,
            bool success = true)
        {
            var beforeName = $"before-{index}.json";
            var afterName = $"after-{index}.json";
            File.WriteAllText(Path.Combine(root, beforeName),
                JsonSerializer.Serialize(before, JsonOptions));
            File.WriteAllText(Path.Combine(root, afterName),
                JsonSerializer.Serialize(after, JsonOptions));
            var episodeName = $"episode-{index}.json";
            File.WriteAllText(Path.Combine(root, episodeName),
                JsonSerializer.Serialize(new PlanExecutionEpisodeEnvelope
                {
                    EpisodeId = "episode-" + index,
                    RunId = "fresh-save-run",
                    SourceStateHash = before.StateHash,
                    AfterStateHash = after.StateHash,
                    BeforeGameTick = index * 10,
                    AfterGameTick = index * 10 + 1,
                    AfterSnapshotFresh = true,
                    BeforeSnapshotPath = beforeName,
                    AfterSnapshotPath = afterName,
                    Status = success ? "applied" : "blocked",
                    Success = success,
                    ChangedFacts = JsonSerializer.SerializeToElement(new { })
                }, JsonOptions));
            return episodeName;
        }

        public string WriteManifest(string[] episodePaths)
        {
            var path = Path.Combine(root, "manifest.json");
            File.WriteAllText(path, JsonSerializer.Serialize(
                new GoldenWalnutFreshSaveEpisodeManifest(
                    GoldenWalnutFreshSaveEpisodeScheduleBuilder.ManifestSchemaVersion,
                    "fresh-save-schedule:test",
                    GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GoalId,
                    GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
                    false,
                    episodePaths),
                JsonOptions));
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
