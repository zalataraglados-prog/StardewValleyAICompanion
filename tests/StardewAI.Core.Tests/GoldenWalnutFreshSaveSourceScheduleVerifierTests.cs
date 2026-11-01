using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveSourceScheduleVerifierTests
{
    [Fact]
    public void MasterAnglerAuditRejectsScheduleWithoutIndependentReceipt()
    {
        using var fixture = new Fixture(phaseOrderedSources: true);
        var inputs = WriteAuditInputs(fixture.Root);
        Assert.Contains("verification receipt", Assert.Throws<InvalidDataException>(() =>
            MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                inputs.Catalog, inputs.Windows, inputs.Dependencies, fixture.SchedulePath)).Message);
    }

    [Fact]
    public async Task MasterAnglerConsumesWrapperReceiptWithoutAuthorizingTraining()
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run", phaseOrderedSources: true);
        var wrapper = await RunProductWrapperVerification(fixture);
        Assert.True(wrapper.ExitCode == 0, wrapper.Error);
        var inputs = WriteAuditInputs(fixture.Root);
        var receiptPath = Path.Combine(fixture.VerificationOutputDirectory,
            "golden-walnut-source-schedule-independent-verification.json");
        var report = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(inputs.Catalog, inputs.Windows,
            inputs.Dependencies, fixture.SchedulePath, receiptPath, "run", "save", "player");
        Assert.False(report.TrainingLabelEligible);
        Assert.Equal(receiptPath, report.GoldenWalnutVerificationReceiptPath);
        Assert.Equal(64, report.GoldenWalnutVerificationReceiptSha256.Length);
        Assert.True(report.GoldenWalnutFreshSaveUnlockTimeline!.SourceScheduleComplete);
        Assert.False(report.GoldenWalnutFreshSaveUnlockTimeline.TrainingAdmissionEligible);
        Assert.False(report.GoldenWalnutResortFundingRoutes!.TrainingAdmissionEligible);
        Assert.Equal(report.GoldenWalnutFreshSaveSourceScheduleSha256,
            report.GoldenWalnutFreshSaveUnlockTimeline.SourceScheduleSha256);

        var baseline = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(inputs.Catalog, inputs.Windows, inputs.Dependencies);
        Assert.Null(baseline.GoldenWalnutFreshSaveUnlockTimeline);
        Assert.False(baseline.TrainingLabelEligible);

        // Exercise the authoritative CLI wiring as well as the direct audit API.
        var outputPath = Path.Combine(fixture.Root, "audit-output.json");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            Path.Combine(FindRepositoryRoot(), "experiments", "StardewAI.GoalConditionedBootstrap", "bin",
                configuration, "net8.0", "StardewAI.GoalConditionedBootstrap.dll"),
            "audit-master-angler-fresh-save-prerequisites", "--catalog", inputs.Catalog,
            "--windows", inputs.Windows, "--dependency-expansions", inputs.Dependencies,
            "--golden-walnut-source-schedule", fixture.SchedulePath,
            "--golden-walnut-verification-receipt", receiptPath,
            "--run-id", "run", "--save-id", "save", "--player-id", "player", "--output", outputPath
        }) start.ArgumentList.Add(argument);
        start.Environment.Remove("STARDEWAI_VALIDATION_TRACE");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error + await output);
        var emitted = JsonSerializer.Deserialize<MasterAnglerFreshSavePrerequisiteAuditReport>(File.ReadAllText(outputPath))!;
        Assert.False(emitted.TrainingLabelEligible);
        Assert.Equal(receiptPath, emitted.GoldenWalnutVerificationReceiptPath);
    }

    [Theory]
    [InlineData("missing_receipt")]
    [InlineData("missing_schedule")]
    [InlineData("stale_schedule_bytes")]
    [InlineData("changed_snapshot")]
    [InlineData("wrong_run")]
    [InlineData("wrong_save")]
    [InlineData("wrong_player")]
    [InlineData("missing_anchor")]
    [InlineData("receipt_status")]
    [InlineData("receipt_schedule")]
    [InlineData("receipt_path")]
    [InlineData("receipt_hash")]
    public async Task MasterAnglerAuditFailsClosedForUnboundOrStalePostExitEvidence(string change)
    {
        using var fixture = new Fixture(scheduleId: "golden-walnut-fresh-save:run", phaseOrderedSources: true);
        var wrapper = await RunProductWrapperVerification(fixture);
        Assert.True(wrapper.ExitCode == 0, wrapper.Error);
        var inputs = WriteAuditInputs(fixture.Root);
        var receipt = Path.Combine(fixture.VerificationOutputDirectory,
            "golden-walnut-source-schedule-independent-verification.json");
        if (change == "missing_receipt") File.Move(receipt, receipt + ".absent");
        if (change == "missing_schedule") File.Move(fixture.SchedulePath, fixture.SchedulePath + ".absent");
        if (change == "stale_schedule_bytes") File.AppendAllText(fixture.SchedulePath, "\n");
        if (change == "changed_snapshot") fixture.Change(fixture.SnapshotPath(2), root => root["diagnostic"] = "changed");
        if (change == "receipt_status") fixture.Change(receipt, root => root["status"] = "error");
        if (change == "receipt_schedule") fixture.Change(receipt, root => root["schedule"]!["source_save_id"] = "other");
        if (change == "receipt_path") fixture.Change(receipt, root => root["source_schedule_path"] = fixture.ManifestPath);
        if (change == "receipt_hash") fixture.Change(receipt, root => root.AsObject().Remove("source_schedule_sha256"));
        var exception = Record.Exception(() => MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
            inputs.Catalog, inputs.Windows, inputs.Dependencies, fixture.SchedulePath, receipt,
            change == "wrong_run" ? "other" : "run", change == "wrong_save" ? "other" : "save",
            change == "missing_anchor" ? null : change == "wrong_player" ? "other" : "player"));
        Assert.True(exception is IOException or InvalidDataException, exception?.ToString() ?? "Audit accepted invalid evidence.");
    }

    private static (string Catalog, string Windows, string Dependencies) WriteAuditInputs(string root)
    {
        // Hermetic authority scaffolding tests wiring, not native fish rules or coverage.
        var catalog = Path.Combine(root, "audit-catalog.json");
        var windows = Path.Combine(root, "audit-windows.json");
        var dependencies = Path.Combine(root, "audit-dependencies.json");
        File.WriteAllText(catalog, JsonSerializer.Serialize(new
        {
            schema_version = "master_angler_opportunity_catalog.v1", status = "complete",
            goal_id = "grandpa.maximum_21", game_version = "1.6.15",
            native_denominator_count = 72, rod_location_species_count = 60,
            mine_override_only_species_count = 2, trap_species_count = 10,
            source_inventory_complete = true, static_calendar_constraint_complete = true,
            location_rule_spawn_chance_input_inventory_complete = true,
            species = Enumerable.Range(0, 72).Select(index => new { qualified_item_id = "fixture-" + index })
        }));
        File.WriteAllText(windows, JsonSerializer.Serialize(new
        {
            schema_version = "master_angler_stage_one_window_index.v1",
            status = "complete_static_windows_dynamic_execution_pending",
            goal_id = "grandpa.maximum_21", game_version = "1.6.15",
            catalog_sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(catalog))),
            static_window_coverage_complete = true, training_label_eligible = false,
            deadline_total_day_exclusive = 224, native_denominator_count = 72,
            species = Enumerable.Range(0, 72).Select(index => new
            {
                qualified_item_id = "fixture-" + index,
                windows = new[] { new { source_kind = "location_rule", location_id = "IslandWest", first_total_day = 0, last_total_day = 1 } }
            })
        }));
        File.WriteAllText(dependencies,
            "{\"schema_version\":\"goal_method_dependency_expansions.v1\",\"goal_id\":\"grandpa.maximum_21\",\"directions\":[]}");
        return (catalog, windows, dependencies);
    }

    [Fact]
    public async Task ProductWrapperVerifiesFrozenArtifactsUsingRealSnapshotIdentityShape()
    {
        using var fixture = new Fixture(
            scheduleId: "golden-walnut-fresh-save:run");

        var result = await RunProductWrapperVerification(fixture);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.Error), result.Error);
        using var report = JsonDocument.Parse(result.Output);
        Assert.Equal("pass", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "player",
            report.RootElement.GetProperty("expected_player_id").GetString());
        Assert.True(File.Exists(Path.Combine(
            fixture.VerificationOutputDirectory,
            "golden-walnut-source-schedule-independent-verification.json")));
    }

    [Theory]
    [InlineData("missing_identity")]
    [InlineData("mismatched_identity")]
    [InlineData("missing_manifest")]
    [InlineData("stale_schedule")]
    public async Task ProductWrapperFailsClosedForInvalidIndependentVerificationInputs(
        string change)
    {
        using var fixture = new Fixture(
            scheduleId: "golden-walnut-fresh-save:run");
        if (change == "missing_identity")
        {
            fixture.Change(fixture.InitialSnapshotPath, root =>
                root["state"]!["identity"]!.AsObject().Remove("player_id"));
        }
        else if (change == "mismatched_identity")
        {
            fixture.Change(fixture.InitialSnapshotPath, root =>
                root["state"]!["identity"]!["player_id"]!["value"] = "other");
        }
        else if (change == "missing_manifest")
        {
            File.Move(fixture.ManifestPath, fixture.ManifestPath + ".absent");
        }
        else
        {
            fixture.Change(fixture.SchedulePath, root =>
                root["source_episode_manifest_sha256"] = "stale");
        }

        var result = await RunProductWrapperVerification(fixture);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.False(File.Exists(Path.Combine(
            fixture.VerificationOutputDirectory,
            "golden-walnut-source-schedule-independent-verification.json")));
    }

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

    [Theory]
    [InlineData("before_snapshot_path", false)]
    [InlineData("before_snapshot_path", true)]
    [InlineData("after_snapshot_path", false)]
    [InlineData("after_snapshot_path", true)]
    [InlineData("snapshot_path", false)]
    [InlineData("snapshot_path", true)]
    public void RejectsSnapshotsOutsideFrozenArtifactDirectory(string field, bool relative)
    {
        using var fixture = new Fixture();
        using var external = new Fixture();
        var snapshotPath = external.SnapshotPath(field == "after_snapshot_path" ? 1 : 0);
        var reference = relative ? Path.GetRelativePath(fixture.Root, snapshotPath) : snapshotPath;
        var receiptPath = field == "snapshot_path" ? fixture.BoundaryPath :
            Path.Combine(fixture.Root, "golden-walnut-fresh-save-episode-0001.json");
        fixture.Change(receiptPath, root => root[field] = reference);
        // Even a schedule regenerated against the external bytes must not make
        // a supposedly frozen directory depend on another artifact tree.
        GoldenWalnutFreshSaveEpisodeScheduleBuilder.Write(fixture.SchedulePath, fixture.ManifestPath);

        Assert.Contains("outside frozen artifact directory",
            Assert.Throws<InvalidDataException>(() => fixture.Verify()).Message);
    }

    [Fact]
    public void AcceptsRelativeDurableSnapshotAliasesInsideFrozenDirectory()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.BoundaryPath, root => root["snapshot_path"] = "snapshot-0.json");
        for (var index = 1; index <= 4; index++)
        {
            var before = $"snapshot-{index - 1}.json";
            var after = $"snapshot-{index}.json";
            fixture.Change(Path.Combine(fixture.Root, $"golden-walnut-fresh-save-episode-{index:D4}.json"), root =>
            {
                root["before_snapshot_path"] = before;
                root["after_snapshot_path"] = after;
            });
        }
        GoldenWalnutFreshSaveEpisodeScheduleBuilder.Write(fixture.SchedulePath, fixture.ManifestPath);

        Assert.Equal(4, fixture.Verify().SourceEpisodeCount);
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

    private static async Task<ProcessResult> RunProductWrapperVerification(
        Fixture fixture)
    {
        var repositoryRoot = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bootstrapDll = Path.Combine(
            repositoryRoot,
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "bin",
            configuration,
            "net8.0",
            "StardewAI.GoalConditionedBootstrap.dll");
        var script = Path.Combine(
            repositoryRoot,
            "scripts",
            "Invoke-ProductBootstrapCalibration.ps1");
        var start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
        {
            "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script,
            "-VerifyGoldenWalnutSourceScheduleArtifactsOnly",
            "-VerificationInitialSnapshotPath", fixture.InitialSnapshotPath,
            "-VerificationEpisodeDirectory", fixture.Root,
            "-VerificationEpisodeManifestPath", fixture.ManifestPath,
            "-VerificationSourceSchedulePath", fixture.SchedulePath,
            "-VerificationBootstrapDllPath", bootstrapDll,
            "-VerificationOutputDirectory", fixture.VerificationOutputDirectory,
            "-RunId", "run"
        })
        {
            start.ArgumentList.Add(argument);
        }
        start.Environment.Remove("STARDEWAI_VALIDATION_TRACE");
        start.Environment.Remove("STARDEWAI_BOOTSTRAP_DEBUG_EXCEPTIONS");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, await output, await error);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException(
            "Cannot find repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "walnut-verifier-" + Guid.NewGuid().ToString("N"));
        public string ManifestPath => Path.Combine(Root, "manifest.json");
        public string SchedulePath => Path.Combine(Root, "schedule.json");
        public string InitialSnapshotPath => Path.Combine(Root, "wrapper-initial-snapshot.json");
        public string VerificationOutputDirectory => Path.Combine(Root, "wrapper-verification");
        public string BoundaryPath => GoldenWalnutFreshSaveInitialBoundaryValidator.PathFor(Root, "run");
        public string SnapshotPath(int index) => Path.Combine(Root, $"snapshot-{index}.json");

        public Fixture(
            bool noSourceIncrement = false,
            string scheduleId = "schedule",
            bool phaseOrderedSources = false)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(InitialSnapshotPath, JsonSerializer.Serialize(new
            {
                state = new
                {
                    identity = new
                    {
                        save_id = new { value = "save", status = "available" },
                        player_id = new { value = "player", status = "available" }
                    }
                }
            }));
            var selectedKeys = GoldenWalnutNativeSourceCatalog.Sources.Where(source =>
                GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(source.AcquisitionOptionId) &&
                source.OptionalUnlockWalnutCost == 0 && !source.ResortOnly &&
                source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal))
                .Take(phaseOrderedSources ? int.MaxValue : 2).Select(source => source.SourceKey).ToHashSet();
            int Quantity(GoldenWalnutNativeSourceDefinition source, int index) =>
                noSourceIncrement || !selectedKeys.Contains(source.SourceKey) ||
                index < (phaseOrderedSources ? source.AccessPhase switch
                {
                    "boat_repaired" or "island_south_initial" => 1,
                    "west_after_turtle" => 3,
                    _ => 2
                } : 1) ? 0 : phaseOrderedSources ? source.NativeQuantity : 1;
            for (var index = 0; index < 5; index++)
            {
                var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(source => new
                {
                    source_key = source.SourceKey,
                    native_quantity = source.NativeQuantity,
                    collected_quantity = Quantity(source, index),
                    remaining_quantity = source.NativeQuantity - Quantity(source, index),
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
            GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(
                ManifestPath,
                Root,
                scheduleId,
                "run");
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
