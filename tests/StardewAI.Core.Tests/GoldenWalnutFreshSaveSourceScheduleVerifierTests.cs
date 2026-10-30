using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveSourceScheduleVerifierTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapCommandReportsIndependentPassOrNonzeroFailure(bool mismatch)
    {
        using var fixture = new Fixture();
        if (mismatch)
            fixture.Change(fixture.SchedulePath, root => root["source_save_id"] = "wrong");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = directory.Parent!.Name;
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            Path.Combine(directory!.FullName, "experiments", "StardewAI.GoalConditionedBootstrap", "bin", configuration,
                "net8.0", "StardewAI.GoalConditionedBootstrap.dll"),
            "verify-golden-walnut-fresh-save-source-schedule",
            "--episode-directory", fixture.Root, "--episode-manifest", fixture.ManifestPath,
            "--source-schedule", fixture.SchedulePath, "--schedule-id", "schedule",
            "--run-id", "run", "--save-id", "save", "--player-id", "player"
        }) start.ArgumentList.Add(argument);
        start.Environment.Remove("STARDEWAI_VALIDATION_TRACE");
        start.Environment.Remove("STARDEWAI_BOOTSTRAP_DEBUG_EXCEPTIONS");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.Equal(mismatch ? 1 : 0, process.ExitCode);
        using var report = JsonDocument.Parse(mismatch ? await error : await output);
        Assert.Equal(mismatch ? "error" : "pass", report.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void ReplaysFrozenEvidenceWithoutReadingLoopFlagsOrWritingInputs()
    {
        using var fixture = new Fixture();
        // A loop report, including its self-attestation, is neither required nor consulted.
        File.WriteAllText(Path.Combine(fixture.Root, "live-training-loop-report.json"),
            "{\"golden_walnut_source_schedule_verified\":false}");
        var before = Directory.GetFiles(fixture.Root).ToDictionary(path => path, File.ReadAllText);
        var verified = fixture.Verify();
        Assert.Equal("save", verified.SourceSaveId);
        Assert.Equal(4, verified.SourceEpisodeCount);
        Assert.Equal(2, verified.SourceCompletions.Length);
        Assert.False(verified.TrainingLabelEligible);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(fixture.Root).Order());
        foreach (var (path, text) in before)
            Assert.Equal(text, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("schedule_id")]
    [InlineData("goal_id")]
    [InlineData("game_version")]
    [InlineData("training_label_eligible")]
    [InlineData("source_episode_manifest_sha256")]
    [InlineData("source_episode_evidence_root_sha256")]
    [InlineData("source_run_id")]
    [InlineData("source_save_id")]
    [InlineData("source_episode_count")]
    [InlineData("first_island_arrival_total_day")]
    [InlineData("planned_first_parrot_unlock_total_day")]
    [InlineData("planned_western_turtle_unlock_total_day")]
    [InlineData("planned_island_house_unlock_total_day")]
    public void RejectsEveryMismatchedScheduleField(string field)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SchedulePath, root =>
        {
            var value = root[field]!.AsValue();
            root[field] = value.TryGetValue<string>(out _) ? JsonValue.Create("wrong") :
                value.TryGetValue<bool>(out _) ? JsonValue.Create(true) : JsonValue.Create(999);
        });
        Assert.Contains("schedule." + field,
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Theory]
    [InlineData("source_key")]
    [InlineData("planned_quantity")]
    [InlineData("planned_completion_total_day")]
    [InlineData("order")]
    [InlineData("missing")]
    [InlineData("extra")]
    public void RejectsSourceRowValueMembershipAndOrderMismatch(string change)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SchedulePath, root =>
        {
            var rows = root["source_completions"]!.AsArray();
            if (change == "order")
            {
                var first = rows[0]!.DeepClone();
                rows.RemoveAt(0);
                rows.Add(first);
            }
            else if (change == "missing") rows.RemoveAt(0);
            else if (change == "extra") rows.Add(rows[0]!.DeepClone());
            else rows[0]![change] = change == "source_key"
                ? JsonValue.Create("wrong") : JsonValue.Create(999);
        });
        Assert.Contains("schedule.source_completions",
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Theory]
    [InlineData("schema_version")]
    [InlineData("schedule_id")]
    [InlineData("goal_id")]
    [InlineData("game_version")]
    [InlineData("training_label_eligible")]
    [InlineData("source_run_id")]
    [InlineData("initial_boundary_path")]
    [InlineData("episode_paths")]
    public void RejectsMissingManifestProperties(string field)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.ManifestPath, root => root.Remove(field));
        Assert.Contains("manifest", Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("omit")]
    [InlineData("duplicate")]
    public void IndependentlyReconstructsManifestMembershipAndOrder(string change)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.ManifestPath, root =>
        {
            var paths = root["episode_paths"]!.AsArray();
            if (change == "omit") paths.RemoveAt(3);
            else if (change == "duplicate") paths[1] = paths[0]!.DeepClone();
            else
            {
                var first = paths[0]!.DeepClone();
                paths.RemoveAt(0);
                paths.Add(first);
            }
        });
        Assert.Contains("manifest.episode_paths",
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("player")]
    [InlineData("run")]
    [InlineData("schedule")]
    public void RejectsWrongCallerIdentityAnchors(string identity)
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => fixture.Verify(
            scheduleId: identity == "schedule" ? "wrong" : "schedule",
            runId: identity == "run" ? "wrong" : "run",
            saveId: identity == "save" ? "wrong" : "save",
            playerId: identity == "player" ? "wrong" : "player"));
    }

    [Theory]
    [InlineData("save_id", "other", "available")]
    [InlineData("save_id", "save", "unavailable")]
    [InlineData("save_id", "", "available")]
    [InlineData("player_id", "other", "available")]
    [InlineData("player_id", "player", "unavailable")]
    [InlineData("player_id", "", "available")]
    public void ReplaysAndRejectsUnreadableOrCrossIdentitySnapshots(string field, string value, string status)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(2), root =>
        {
            root[field]!["value"] = value;
            root[field]!["status"] = status;
        });
        Assert.Throws<InvalidDataException>(() => fixture.Verify());
    }

    [Theory]
    [InlineData("journey", "island_reached")]
    [InlineData("first_parrot", "complete_or_pending")]
    [InlineData("western_turtle_parrot", "complete_or_pending")]
    [InlineData("island_house_parrot", "complete_or_pending")]
    public void RejectsMissingNativeTransitionsEvenWhenEmittedScheduleClaimsThem(string section, string field)
    {
        using var fixture = new Fixture();
        for (var index = 0; index < 5; index++)
            fixture.Change(fixture.SnapshotPath(index), root =>
                root["state"]!["locations"]!["ginger_island_access"]!["value"]![section]![field] = false);
        fixture.RebindBoundary();
        Assert.Contains("transition", Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Fact]
    public void RejectsMissingSourceIncrementEvenWhenAllUnlockHistoryExists()
    {
        using var fixture = new Fixture(noSourceIncrement: true);
        Assert.Contains("no source increment", Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Fact]
    public void RejectsEvidenceRootChangeThatLeavesProgressUnchanged()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(2), root => root["unrelated_diagnostic"] = "changed");
        Assert.Contains("schedule.source_episode_evidence_root_sha256",
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Fact]
    public void RejectsChangedManifestByteBindingEvenWhenMembershipIsIdentical()
    {
        using var fixture = new Fixture();
        File.AppendAllText(fixture.ManifestPath, "\n");
        Assert.Contains("schedule.source_episode_manifest_sha256",
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("schedule")]
    [InlineData("snapshot")]
    [InlineData("boundary")]
    public void RejectsMissingArtifacts(string artifact)
    {
        using var fixture = new Fixture();
        var path = artifact switch
        {
            "manifest" => fixture.ManifestPath,
            "schedule" => fixture.SchedulePath,
            "boundary" => fixture.BoundaryPath,
            _ => fixture.SnapshotPath(2)
        };
        // Rename, rather than remove, to simulate an absent input.
        File.Move(path, path + ".absent");
        var exception = Record.Exception(() => fixture.Verify());
        Assert.True(exception is IOException or InvalidDataException);
    }

    [Theory]
    [InlineData("missing_false")]
    [InlineData("duplicate")]
    [InlineData("invalid_json")]
    public void RejectsMalformedScheduleWithoutDefaultingMissingFields(string change)
    {
        using var fixture = new Fixture();
        if (change == "missing_false")
            fixture.Change(fixture.SchedulePath, root => root.Remove("training_label_eligible"));
        else if (change == "duplicate")
        {
            var text = File.ReadAllText(fixture.SchedulePath);
            File.WriteAllText(fixture.SchedulePath, text.Insert(1, "\"training_label_eligible\":false,"));
        }
        else File.WriteAllText(fixture.SchedulePath, "{");
        var exception = Record.Exception(() => fixture.Verify());
        Assert.True(exception is InvalidDataException or JsonException);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "walnut-verifier-" + Guid.NewGuid().ToString("N"));
        public string ManifestPath => Path.Combine(Root, "manifest.json");
        public string SchedulePath => Path.Combine(Root, "schedule.json");
        public string BoundaryPath => GoldenWalnutFreshSaveInitialBoundaryValidator.PathFor(Root, "run");
        public string SnapshotPath(int index) => Path.Combine(Root, $"snapshot-{index}.json");

        public Fixture(bool noSourceIncrement = false)
        {
            Directory.CreateDirectory(Root);
            var selectedKeys = GoldenWalnutNativeSourceCatalog.Sources.Where(source =>
                GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(source.AcquisitionOptionId) &&
                source.OptionalUnlockWalnutCost == 0 && !source.ResortOnly &&
                source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal))
                .Take(2).Select(source => source.SourceKey).ToHashSet();
            for (var index = 0; index < 5; index++)
            {
                var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(source => new
                {
                    source_key = source.SourceKey,
                    native_quantity = source.NativeQuantity,
                    collected_quantity = !noSourceIncrement && index > 0 && selectedKeys.Contains(source.SourceKey) ? 1 : 0,
                    remaining_quantity = source.NativeQuantity - (!noSourceIncrement && index > 0 && selectedKeys.Contains(source.SourceKey) ? 1 : 0),
                    acquisition_option_id = source.AcquisitionOptionId
                }).ToArray();
                var snapshot = new SnapshotEnvelope
                {
                    GameVersion = "1.6.15",
                    GameTick = index,
                    StateHash = "hash-" + index,
                    SaveId = new() { Value = "save", Status = FieldStatus.Available },
                    PlayerId = new() { Value = "player", Status = FieldStatus.Available },
                    InGameTime = new() { Value = 600, Status = FieldStatus.Available },
                    State = new Dictionary<string, JsonElement>
                    {
                        ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { value = index * 10, status = "available" } }),
                        ["locations"] = JsonSerializer.SerializeToElement(new
                        {
                            ginger_island_access = new { status = "available", value = new
                            {
                                schema_version = "ginger_island_access.v1",
                                journey = new { island_reached = index > 0 },
                                first_parrot = new { complete_or_pending = index > 1 },
                                western_turtle_parrot = new { complete_or_pending = index > 2 },
                                island_house_parrot = new { complete_or_pending = index > 3 }
                            } }
                        }),
                        ["world_progress"] = JsonSerializer.SerializeToElement(new
                        {
                            golden_walnut_sources = new { status = "available", value = new
                            {
                                schema_version = GoldenWalnutNativeSourceCatalog.SchemaVersion,
                                projection_status = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
                                native_source_key_count = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
                                native_walnut_total = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
                                tracked_collected_quantity = sources.Sum(row => row.collected_quantity),
                                world_golden_walnuts_found = sources.Sum(row => row.collected_quantity),
                                sources
                            } }
                        })
                    }
                };
                File.WriteAllText(SnapshotPath(index), JsonSerializer.Serialize(snapshot));
                if (index == 0) continue;
                File.WriteAllText(Path.Combine(Root, $"golden-walnut-fresh-save-episode-{index:D4}.json"),
                    JsonSerializer.Serialize(new PlanExecutionEpisodeEnvelope
                    {
                        EpisodeId = "episode-" + index,
                        RunId = "run",
                        SourceStateHash = "hash-" + (index - 1),
                        AfterStateHash = "hash-" + index,
                        BeforeGameTick = index - 1,
                        AfterGameTick = index,
                        BeforeSnapshotPath = SnapshotPath(index - 1),
                        AfterSnapshotPath = SnapshotPath(index),
                        AfterSnapshotFresh = true,
                        Success = true,
                        Status = "applied",
                        ChangedFacts = JsonSerializer.SerializeToElement(new { })
                    }));
            }
            RebindBoundary();
            GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(ManifestPath, Root, "schedule", "run");
            GoldenWalnutFreshSaveEpisodeScheduleBuilder.Write(SchedulePath, ManifestPath);
        }

        public void RebindBoundary() => File.WriteAllText(BoundaryPath, JsonSerializer.Serialize(
            GoldenWalnutFreshSaveInitialBoundaryValidator.Build(SnapshotPath(0), "run", "hash-0")));

        public GoldenWalnutFreshSaveSourceSchedule Verify(string scheduleId = "schedule", string runId = "run",
            string saveId = "save", string playerId = "player") =>
            GoldenWalnutFreshSaveSourceScheduleVerifier.Verify(Root, ManifestPath, SchedulePath, scheduleId, runId, saveId, playerId);

        public void Change(string path, Action<JsonObject> change)
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            change(root);
            File.WriteAllText(path, root.ToJsonString());
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
