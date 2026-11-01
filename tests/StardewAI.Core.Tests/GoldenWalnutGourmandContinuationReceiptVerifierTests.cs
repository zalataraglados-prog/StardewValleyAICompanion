using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutGourmandContinuationReceiptVerifierTests
{
    [Fact]
    public void VerifiesAllThreeNativeTransitionsFromTheSameFrozenManifest()
    {
        using var fixture = new Fixture();
        var result = fixture.Verify();
        Assert.True(result.Ready, string.Join(",", result.BlockingReasons));
        Assert.True(result.Verified);
        Assert.Equal(3, result.VerifiedTransitionCount);
        Assert.Empty(result.BlockingReasons);
        Assert.Equal(Path.GetFullPath(fixture.ManifestPath), result.SourceEpisodeManifestPath);
        Assert.Equal(fixture.ManifestHash, result.SourceEpisodeManifestSha256);
        Assert.Equal(fixture.SourceRoot, result.SourceEpisodeEvidenceRootSha256);
        Assert.Matches("^[0-9a-f]{64}$", result.NativeReceiptEvidenceRootSha256);
        Assert.Equal("run", result.SourceRunId);
        Assert.Equal("save", result.SaveId);
        Assert.Equal("player", result.PlayerId);
        Assert.Equal("1.6.15", result.GameVersion);
        Assert.False(result.TrainingLabelEligible);
    }

    [Fact]
    public void WritesAndAtomicallyReplacesTypedReceiptWithoutChangingSourceManifest()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "continuation-receipt.json");
        var result = GoldenWalnutGourmandContinuationReceiptVerifier.Write(path, fixture.Root, fixture.ManifestPath);
        Assert.True(result.Ready, string.Join(",", result.BlockingReasons));
        var written = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(GoldenWalnutGourmandContinuationReceiptVerifier.SchemaVersion, written["schema_version"]!.GetValue<string>());
        Assert.True(written["ready"]!.GetValue<bool>());
        Assert.True(written["verified"]!.GetValue<bool>());
        Assert.Equal(3, written["verified_transition_count"]!.GetValue<int>());
        Assert.Empty(written["blocking_reasons"]!.AsArray());
        Assert.False(written["training_label_eligible"]!.GetValue<bool>());
        Assert.Equal(fixture.ManifestHash, written["source_episode_manifest_sha256"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(fixture.ManifestPath), written["source_episode_manifest_path"]!.GetValue<string>());
        fixture.Change(fixture.ExecutionPath(6), root => root["status"] = "blocked");
        Assert.False(GoldenWalnutGourmandContinuationReceiptVerifier.Write(path, fixture.Root, fixture.ManifestPath).Ready);
        Assert.False(JsonNode.Parse(File.ReadAllText(path))!["verified"]!.GetValue<bool>());
        Assert.DoesNotContain(Directory.EnumerateFiles(fixture.Root), name => name.Contains(".tmp.", StringComparison.Ordinal));
        Assert.Equal(fixture.ManifestHash, Fixture.Hash(File.ReadAllText(fixture.ManifestPath)));
        Assert.Throws<ArgumentException>(() => GoldenWalnutGourmandContinuationReceiptVerifier.Write(
            fixture.ManifestPath, fixture.Root, fixture.ManifestPath));
    }

    [Theory]
    [InlineData("requests_fulfilled", "2")]
    [InlineData("requests_fulfilled", "0")]
    [InlineData("requests_fulfilled", "4")]
    [InlineData("requests_fulfilled", null)]
    [InlineData("complete", "true")]
    [InlineData("total_requests", "4")]
    [InlineData("active_request_index", "2")]
    [InlineData("required_crop_item_id", "\"248\"")]
    [InlineData("schema_version", "\"island_gourmand_progress.v0\"")]
    public void RejectsDuplicateSkippedOrMalformedRequestStates(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(5), root => Set(Progress(root), field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("episode_order")]
    [InlineData("episode_duplicate")]
    [InlineData("episode_omitted")]
    [InlineData("episode_added")]
    [InlineData("episode_id_duplicate")]
    [InlineData("ticks_reversed")]
    public void RejectsManifestOrderingMembershipAndReceiptOrderDrift(string change)
    {
        using var fixture = new Fixture();
        if (change == "episode_order") fixture.Change(fixture.ManifestPath, root =>
        {
            var paths = root["episode_paths"]!.AsArray();
            var first = paths[4]!.GetValue<string>();
            paths[4] = paths[5]!.GetValue<string>();
            paths[5] = first;
        });
        if (change == "episode_duplicate") fixture.Change(fixture.ManifestPath, root =>
            root["episode_paths"]![5] = root["episode_paths"]![4]!.GetValue<string>());
        if (change == "episode_omitted") fixture.Change(fixture.ManifestPath, root => root["episode_paths"]!.AsArray().RemoveAt(5));
        if (change == "episode_added") File.Copy(fixture.EpisodePath(5), Path.Combine(fixture.Root, "golden-walnut-fresh-save-episode-99999999-extra.json"));
        if (change == "episode_id_duplicate") fixture.Change(fixture.EpisodePath(5), root => root["episode_id"] = "episode-4");
        if (change == "ticks_reversed") fixture.Change(fixture.EpisodePath(5), root => root["before_game_tick"] = 0);
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("save_id", "value", "\"other\"")]
    [InlineData("player_id", "value", "\"other\"")]
    [InlineData("save_id", "status", "\"stale\"")]
    [InlineData("player_id", "status", "\"unavailable\"")]
    [InlineData("player_id", "value", null)]
    [InlineData("save_id", "value", "null")]
    public void RejectsSaveOrPlayerDiscontinuityEvenOnAHashContinuousChain(string identity, string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root => Set(root[identity]!.AsObject(), field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("game_version", "\"1.6.14\"")]
    [InlineData("game_version", null)]
    [InlineData("state_hash", "\"stale-hash\"")]
    [InlineData("game_tick", "59")]
    [InlineData("schema_version", null)]
    public void RejectsSnapshotVersionHashAndTickDrift(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root => Set(root, field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("collected_quantity", "4")]
    [InlineData("collected_quantity", "6")]
    [InlineData("collected_quantity", null)]
    [InlineData("remaining_quantity", "1")]
    [InlineData("remaining_quantity", null)]
    [InlineData("native_quantity", "6")]
    [InlineData("source_key", "\"IslandGourmand3\"")]
    [InlineData("acquisition_option_id", "\"debug.setup_island_gourmand\"")]
    public void RejectsIncorrectGourmandQuantitiesAndSourceIdentity(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root => Set(Source(root, "IslandGourmand2"), field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("status", "\"blocked\"")]
    [InlineData("option_id", "\"debug.setup_island_gourmand\"")]
    [InlineData("option_id", "\"executor.route_connector_tile\"")]
    [InlineData("primitive_verification_status", "\"stale_after_snapshot\"")]
    [InlineData("primitive_verification_status", null)]
    [InlineData("primitive_kind", "\"direct_mutation\"")]
    [InlineData("feedback_available", "false")]
    [InlineData("after_snapshot_fresh", "false")]
    [InlineData("after_snapshot_fresh", null)]
    [InlineData("block_reasons", "[\"blocked\"]")]
    [InlineData("schema_version", "\"queue_execution_receipt.v1\"")]
    [InlineData("run_id", "\"other-run\"")]
    [InlineData("queue_id", "\"other-queue\"")]
    [InlineData("queue_item_id", "\"other-item\"")]
    [InlineData("before_state_hash", "\"old-hash\"")]
    [InlineData("after_state_hash", "\"old-hash\"")]
    [InlineData("before_game_tick", "5")]
    [InlineData("after_game_tick", "999")]
    [InlineData("target_location", "\"Farm\"")]
    [InlineData("actual_ticks", "0")]
    [InlineData("started_at", "\"not-a-date\"")]
    [InlineData("completed_at", "\"2020-01-01T00:00:00Z\"")]
    [InlineData("requested_effect", "\"gourmand_stage=introduce_request\"")]
    [InlineData("observed_effect", "\"request_index=3\"")]
    [InlineData("primitive_verification_reasons", "[]")]
    [InlineData("changed_facts", "[]")]
    public void RejectsStaleNonNativeMismatchedOrMissingExecutionFields(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.ExecutionPath(5), root => Set(root, field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("success", "false")]
    [InlineData("state_hash_changed", "false")]
    [InlineData("after_snapshot_fresh", "false")]
    [InlineData("primitive_kind", "\"mutate\"")]
    [InlineData("primitive_verification_reasons", "[]")]
    [InlineData("execution_result_path", null)]
    [InlineData("effective_queue_item", "null")]
    public void RejectsUnverifiedOrUnboundNativeEpisode(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.EpisodePath(5), root => Set(root, field, value));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MatchingEpisodeAndExecutionMustStillContainEveryNativeCompletionReason(int index)
    {
        using var fixture = new Fixture();
        foreach (var path in new[] { fixture.EpisodePath(5), fixture.ExecutionPath(5) })
            fixture.Change(path, root => root["primitive_verification_reasons"]!.AsArray().RemoveAt(index));
        Blocked(fixture.Verify(), "native_completion_reasons_missing");
    }

    [Theory]
    [InlineData(0, "before", "0")]
    [InlineData(0, "after", "3")]
    [InlineData(1, "after", "False")]
    [InlineData(2, "after", "1")]
    [InlineData(3, "after", "100")]
    [InlineData(4, "before", "True")]
    [InlineData(4, "after", "False")]
    [InlineData(4, "path", "player.team.collected_nut_tracker[IslandGourmand3]")]
    public void MatchingChangedFactsCannotSubstituteForTheExactNativeOutcome(int index, string field, string value)
    {
        using var fixture = new Fixture();
        foreach (var path in new[] { fixture.EpisodePath(5), fixture.ExecutionPath(5) })
            fixture.Change(path, root => root["changed_facts"]![index]![field] = value);
        Blocked(fixture.Verify(), "native_changed_facts_invalid");
    }

    [Theory]
    [InlineData("requested_effect")]
    [InlineData("observed_effect")]
    public void MirroringAStaleEffectDoesNotVerifyANativeTransition(string field)
    {
        using var fixture = new Fixture();
        foreach (var path in new[] { fixture.EpisodePath(5), fixture.ExecutionPath(5) })
            fixture.Change(path, root => root[field] = "stale_effect_from_another_request");
        Blocked(fixture.Verify(), "native_effect_mismatch");
    }

    [Theory]
    [InlineData("host_authority", "false")]
    [InlineData("is_current_location", "false")]
    [InlineData("menu_clear", "false")]
    [InlineData("mutex_projection_available", "false")]
    [InlineData("mutex_locked", "true")]
    [InlineData("triggered_gourmand", "true")]
    [InlineData("required_mail_satisfied", "false")]
    [InlineData("talked_to_gourmand_received_or_pending", "false")]
    [InlineData("ready_matching_crop_count", "0")]
    [InlineData("walnut_debris_count", "1")]
    [InlineData("native_contract", "\"mutate_tracker_directly\"")]
    [InlineData("location_id", "\"Farm\"")]
    [InlineData("projection_status", "\"blocked_island_house_upgrade_missing\"")]
    public void RejectsNativeContextThatCannotHaveCompletedTheRequest(string field, string? value)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(5), root => Set(Progress(root), field, value));
        Blocked(fixture.Verify());
    }

    [Fact]
    public void CannotCountTerminalStateWithNoNativeExecutionReceipts()
    {
        using var fixture = new Fixture();
        foreach (var index in new[] { 4, 5, 6 })
            fixture.Change(fixture.EpisodePath(index), root => root["option_id"] = "debug.setup_island_gourmand");
        Assert.True(GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(
            JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(fixture.SnapshotPath(7)))!).Ready);
        Blocked(fixture.Verify(), "native_episode_not_verified");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IncompletePrefixReturnsVerifiedCountWithoutClaimingReadiness(int transitions)
    {
        using var fixture = new Fixture();
        for (var index = 4 + transitions; index < 7; index++)
            File.Move(fixture.EpisodePath(index), fixture.EpisodePath(index) + ".not-in-manifest");
        GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(fixture.ManifestPath, fixture.Root, "schedule", "run");
        var result = fixture.Verify();
        Blocked(result, "gourmand_native_transitions_incomplete");
        Assert.Equal(transitions, result.VerifiedTransitionCount);
    }

    [Fact]
    public void PinsManifestAndAllSnapshotEvidenceUsingExistingScheduleHashes()
    {
        using var fixture = new Fixture();
        Assert.True(GoldenWalnutGourmandContinuationReceiptVerifier.Verify(fixture.Root, fixture.ManifestPath,
            fixture.ManifestHash, fixture.SourceRoot).Ready);
        fixture.Change(fixture.ManifestPath, root => root["schedule_id"] = "changed-schedule");
        Blocked(GoldenWalnutGourmandContinuationReceiptVerifier.Verify(fixture.Root, fixture.ManifestPath,
            fixture.ManifestHash), "manifest_hash_mismatch");
        fixture.Change(fixture.ManifestPath, root => root["schedule_id"] = "schedule");
        fixture.Change(fixture.SnapshotPath(6), root => root["bridge_version"] = "changed-artifact-bytes");
        Blocked(GoldenWalnutGourmandContinuationReceiptVerifier.Verify(fixture.Root, fixture.ManifestPath,
            expectedEvidenceRootSha256: fixture.SourceRoot), "source_evidence_hash_mismatch");
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("boundary")]
    [InlineData("episode")]
    [InlineData("snapshot")]
    [InlineData("execution")]
    public void MissingOrMalformedFilesFailClosedWithoutThrowing(string artifact)
    {
        using var fixture = new Fixture();
        var path = artifact switch
        {
            "manifest" => fixture.ManifestPath, "boundary" => fixture.BoundaryPath,
            "episode" => fixture.EpisodePath(5), "snapshot" => fixture.SnapshotPath(6), _ => fixture.ExecutionPath(5)
        };
        foreach (var text in new[] { "{", "null", "[]", "{\"schema_version\":false}" })
        {
            File.WriteAllText(path, text);
            Blocked(fixture.Verify());
        }
        File.Move(path, path + ".missing");
        Blocked(fixture.Verify());
    }

    [Fact]
    public void UnreceiptedHandoffProgressCannotHideBehindAnEqualStateHash()
    {
        using var fixture = new Fixture();
        var copy = Path.Combine(fixture.Root, "split-before.json");
        File.Copy(fixture.SnapshotPath(5), copy);
        fixture.Change(copy, root =>
        {
            Progress(root)["requests_fulfilled"] = 2;
            Progress(root)["active_request_index"] = 2;
            Progress(root)["required_crop_item_id"] = "248";
            Progress(root)["required_crop_qualified_item_id"] = "(O)248";
            Source(root, "IslandGourmand2")["collected_quantity"] = 5;
            Source(root, "IslandGourmand2")["remaining_quantity"] = 0;
            Catalog(root)["tracked_collected_quantity"] = 11;
            Catalog(root)["tracked_remaining_quantity"] = 119;
        });
        fixture.Change(fixture.EpisodePath(5), root => root["before_snapshot_path"] = copy);
        Blocked(fixture.Verify(), "gourmand_unreceipted_handoff_progress");
    }

    [Fact]
    public void ReplayedNativeCompletionWithoutAnyProgressIsRejected()
    {
        using var fixture = new Fixture();
        var episode = JsonNode.Parse(File.ReadAllText(fixture.EpisodePath(6)))!.AsObject();
        episode["episode_id"] = "replay";
        episode["source_state_hash"] = "h7";
        episode["before_game_tick"] = 70;
        episode["after_game_tick"] = 70;
        episode["before_snapshot_path"] = fixture.SnapshotPath(7);
        File.WriteAllText(Path.Combine(fixture.Root, "golden-walnut-fresh-save-episode-99999999-replay.json"), episode.ToJsonString());
        GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(fixture.ManifestPath, fixture.Root, "schedule", "run");
        Blocked(fixture.Verify(), "native_receipt_without_transition");
    }

    [Fact]
    public void ExternalOrReusedExecutionArtifactsCannotCount()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.EpisodePath(5), root => root["execution_result_path"] = fixture.ExecutionPath(4));
        Blocked(fixture.Verify(), "native_receipt_reused");
        fixture.Change(fixture.EpisodePath(5), root => root["execution_result_path"] = typeof(Fixture).Assembly.Location);
        Blocked(fixture.Verify(), "artifact_outside_frozen_directory");
    }

    [Fact]
    public void DuplicatePropertiesAndDefaultedZeroFieldsFailClosed()
    {
        using var fixture = new Fixture();
        var path = fixture.ExecutionPath(5);
        var original = File.ReadAllText(path);
        File.WriteAllText(path, original.Insert(1, "\"feedback_available\":true,"));
        Blocked(fixture.Verify(), "artifact_duplicate_json_property");
        File.WriteAllText(path, original);
        fixture.Change(fixture.SnapshotPath(6), root => Progress(root).Remove("walnut_debris_count"));
        Blocked(fixture.Verify());
    }

    [Theory]
    [InlineData("source_total_day")]
    [InlineData("source_game_tick")]
    [InlineData("training_label_eligible")]
    [InlineData("snapshot_logical_sha256")]
    public void InitialBoundaryMustContainExplicitZeroAndFalseFields(string field)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.BoundaryPath, root => root.Remove(field));
        Blocked(fixture.Verify(), "initial_boundary_fields_missing");
    }

    [Theory]
    [InlineData("missing_source")]
    [InlineData("fake_denominator")]
    [InlineData("duplicate_source")]
    [InlineData("wrong_totals")]
    [InlineData("wrong_world_found")]
    public void RejectsSourceDenominatorAndFoundQuantityDrift(string change)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root =>
        {
            var catalog = Catalog(root);
            if (change == "missing_source") catalog["sources"]!.AsArray().RemoveAt(0);
            if (change == "fake_denominator") catalog["native_source_key_count"] = 3;
            if (change == "duplicate_source") catalog["sources"]!.AsArray().Add(catalog["sources"]![0]!.DeepClone());
            if (change == "wrong_totals") catalog["tracked_collected_quantity"] = 0;
            if (change == "wrong_world_found") catalog["world_golden_walnuts_found"] = 12;
        });
        Blocked(fixture.Verify());
    }

    [Fact]
    public void AFieldStatusOfStaleCannotCountAsTransparentNativeEvidence()
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root => root["state"]!["world_progress"]!["island_gourmand"]!["status"] = "stale");
        Blocked(fixture.Verify(), "transparent_projection_unavailable");
    }

    [Theory]
    [InlineData("requests_fulfilled")]
    [InlineData("remaining_quantity")]
    [InlineData("native_source_key_count")]
    [InlineData("tracked_remaining_quantity")]
    public void MissingZeroAndDenominatorFieldsDoNotBecomeDefaults(string field)
    {
        using var fixture = new Fixture();
        fixture.Change(fixture.SnapshotPath(6), root =>
        {
            var target = field == "requests_fulfilled" ? Progress(root) :
                field == "remaining_quantity" ? Source(root, "IslandGourmand2") : Catalog(root);
            target.Remove(field);
        });
        Blocked(fixture.Verify());
    }

    [Fact]
    public void NullArgumentsAreProgrammerErrorsButInvalidPathsAreBlocked()
    {
        Assert.Throws<ArgumentNullException>(() => GoldenWalnutGourmandContinuationReceiptVerifier.Verify(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => GoldenWalnutGourmandContinuationReceiptVerifier.Verify("x", null!));
        Blocked(GoldenWalnutGourmandContinuationReceiptVerifier.Verify("", ""));
    }

    private static void Blocked(GoldenWalnutGourmandContinuationReceipt result, string? reason = null)
    {
        Assert.False(result.Ready);
        Assert.False(result.Verified);
        Assert.False(result.TrainingLabelEligible);
        Assert.NotEmpty(result.BlockingReasons);
        if (reason is not null) Assert.Contains(reason, result.BlockingReasons);
    }
    private static void Set(JsonObject root, string field, string? value)
    {
        if (value is null) root.Remove(field);
        else root[field] = JsonNode.Parse(value);
    }
    private static JsonObject Progress(JsonObject root) => root["state"]!["world_progress"]!["island_gourmand"]!["value"]!.AsObject();
    private static JsonObject Catalog(JsonObject root) => root["state"]!["world_progress"]!["golden_walnut_sources"]!["value"]!.AsObject();
    private static JsonObject Source(JsonObject root, string key) => Catalog(root)["sources"]!.AsArray().OfType<JsonObject>()
        .Single(row => row["source_key"]!.GetValue<string>() == key);

    private sealed class Fixture : IDisposable
    {
        private const string NativeContract = "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward";
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gourmand-continuity-" + Guid.NewGuid().ToString("N"));
        public string ManifestPath => Path.Combine(Root, "manifest.json");
        public string BoundaryPath => GoldenWalnutFreshSaveInitialBoundaryValidator.PathFor(Root, "run");
        public string ManifestHash { get; }
        public string SourceRoot { get; }
        public string SnapshotPath(int index) => Path.Combine(Root, $"snapshot-{index}.json");
        public string EpisodePath(int index) => Path.Combine(Root, $"golden-walnut-fresh-save-episode-{index:D8}-test.json");
        public string ExecutionPath(int index) => Path.Combine(Root, $"execution-{index}.json");

        public Fixture()
        {
            Directory.CreateDirectory(Root);
            for (var index = 0; index < 8; index++)
                Write(SnapshotPath(index), Snapshot(index));
            for (var index = 0; index < 7; index++)
            {
                var native = index >= 4;
                var fulfilled = index - 3;
                var option = native ? "executor.advance_gourmand_request" : "executor.route_connector_tile";
                var reasons = native ? new[]
                {
                    "native_Gourmand_Yes_response_completed", "native_crop_event_and_return_observed",
                    "native_five_walnut_debris_spawn_and_pickup_verified", "request_tracker_and_collected_nut_receipt_verified"
                } : Array.Empty<string>();
                var beforeFound = 1 + (fulfilled - 1) * 5;
                var afterFound = 1 + fulfilled * 5;
                var requested = native ? $"gourmand_stage=complete_request;request_index_after={fulfilled};collected_nut_key=IslandGourmand{fulfilled};walnut_debris_spawn_count=5;golden_walnuts_found_after={afterFound}" : "route";
                var observed = native ? $"location=IslandFarmCave;menu=none;request_index={fulfilled};talked=true;walnut_debris=0;golden_walnuts_found={afterFound}" : "route";
                var changes = new[]
                {
                    Fact("world_progress.island_gourmand.requests_fulfilled", (fulfilled - 1).ToString(), fulfilled.ToString()),
                    Fact("world_progress.island_gourmand.talked_to_gourmand_received_or_pending", "True", "True"),
                    Fact("world_progress.island_gourmand.walnut_debris_count", "0", "0"),
                    Fact("world_progress.golden_walnuts.found", beforeFound.ToString(), afterFound.ToString()),
                    Fact($"player.team.collected_nut_tracker[IslandGourmand{fulfilled}]", "False", "True")
                };
                var item = new { queue_item_id = "item-" + index, option_id = option };
                var episode = new PlanExecutionEpisodeEnvelope
                {
                    EpisodeId = "episode-" + index, RunId = "run", SourceStateHash = "h" + index,
                    AfterStateHash = "h" + (index + 1), StateHashChanged = true,
                    BeforeGameTick = index * 10, AfterGameTick = (index + 1) * 10,
                    AfterSnapshotFresh = true, BeforeSnapshotPath = SnapshotPath(index), AfterSnapshotPath = SnapshotPath(index + 1),
                    ExecutionResultPath = ExecutionPath(index), QueueId = "queue-" + index, OptionId = option,
                    Status = "applied", Success = true, PrimitiveKind = native ? "advance_gourmand_request" : "route_connector_tile",
                    PrimitiveVerificationStatus = "verified", PrimitiveVerificationReasons = reasons,
                    RequestedEffect = requested, ObservedEffect = observed,
                    ChangedFacts = JsonSerializer.SerializeToElement(changes), EffectiveQueueItem = JsonSerializer.SerializeToElement(item)
                };
                Write(EpisodePath(index), episode);
                Write(ExecutionPath(index), new
                {
                    schema_version = "training_execution_result.v1", run_id = "run", queue_id = episode.QueueId,
                    queue_item_id = "item-" + index, option_id = option, status = "applied", feedback_available = true,
                    before_state_hash = episode.SourceStateHash, after_state_hash = episode.AfterStateHash,
                    before_game_tick = episode.BeforeGameTick, after_game_tick = episode.AfterGameTick,
                    after_snapshot_fresh = true, primitive_kind = episode.PrimitiveKind,
                    primitive_verification_status = "verified", primitive_verification_reasons = reasons,
                    block_reasons = Array.Empty<string>(), requested_effect = requested, observed_effect = observed,
                    changed_facts = changes, actual_ticks = 10, target_location = "IslandFarmCave",
                    started_at = "2026-11-01T00:00:00Z", completed_at = "2026-11-01T00:01:00Z"
                });
            }
            Write(BoundaryPath, GoldenWalnutFreshSaveInitialBoundaryValidator.Build(SnapshotPath(0), "run", "h0"));
            GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(ManifestPath, Root, "schedule", "run");
            ManifestHash = Hash(File.ReadAllText(ManifestPath));
            SourceRoot = GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(ManifestPath).SourceEpisodeEvidenceRootSha256;
        }

        public GoldenWalnutGourmandContinuationReceipt Verify() => GoldenWalnutGourmandContinuationReceiptVerifier.Verify(Root, ManifestPath);
        public void Change(string path, Action<JsonObject> change)
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            change(root);
            File.WriteAllText(path, root.ToJsonString());
        }
        public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        private static object Fact(string path, string before, string after) => new { path, before, after };
        private static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));

        private static SnapshotEnvelope Snapshot(int index)
        {
            var fulfilled = Math.Max(0, index - 4);
            var otherSource = GoldenWalnutNativeSourceCatalog.Sources.First(source => source.AcquisitionOptionId == "foraging.harvest_bushes");
            var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(definition =>
            {
                var collected = definition.SourceKey == otherSource.SourceKey && index > 0 ? 1 : 0;
                for (var request = 1; request <= fulfilled; request++)
                    if (definition.SourceKey == "IslandGourmand" + request) collected = 5;
                return new GoldenWalnutSourceProgressRef
                {
                    SourceKey = definition.SourceKey, TrackingKind = definition.TrackingKind, LocationId = definition.LocationId,
                    TileX = definition.TileX, TileY = definition.TileY, NativeQuantity = definition.NativeQuantity,
                    CollectedQuantity = collected, RemainingQuantity = definition.NativeQuantity - collected,
                    AcquisitionOptionId = definition.AcquisitionOptionId, ProbabilityStatus = definition.ProbabilityStatus,
                    AccessPhase = definition.AccessPhase, RequiredMailKey = definition.RequiredMailKey,
                    RequiredMailSatisfied = true, ResortOnly = definition.ResortOnly,
                    OptionalUnlockWalnutCost = definition.OptionalUnlockWalnutCost, RequiredSecretNoteId = definition.RequiredSecretNoteId
                };
            }).ToArray();
            var crop = fulfilled == 3 ? string.Empty : new[] { "254", "262", "248" }[fulfilled];
            var progress = new IslandGourmandProgressRef
            {
                ProjectionStatus = fulfilled == 3 ? "complete_native_three_requests" : "exact_native_active_request_projected",
                RequiredMailSatisfied = index >= 4, TalkedToGourmandReceivedOrPending = index >= 4,
                RequestsFulfilled = fulfilled, TotalRequests = 3, Complete = fulfilled == 3,
                ActiveRequestIndex = fulfilled == 3 ? null : fulfilled, RequiredCropItemId = crop,
                RequiredCropQualifiedItemId = crop.Length == 0 ? string.Empty : "(O)" + crop,
                RequiredCropDisplayName = crop.Length == 0 ? string.Empty : "Crop",
                RequestState = fulfilled == 3 ? "complete" : "ready_matching_crop_present",
                ReadyMatchingCropCount = 1, LocationId = "IslandFarmCave", IsCurrentLocation = true,
                HostAuthority = true, MenuClear = true, MutexProjectionAvailable = true, NativeContract = NativeContract
            };
            var catalog = new GoldenWalnutSourceCatalogRef
            {
                SchemaVersion = GoldenWalnutNativeSourceCatalog.SchemaVersion,
                ProjectionStatus = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
                NativeSourceKeyCount = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
                NativeWalnutTotal = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal, Sources = sources,
                TrackedCollectedQuantity = sources.Sum(source => source.CollectedQuantity),
                TrackedRemainingQuantity = sources.Sum(source => source.RemainingQuantity),
                WorldGoldenWalnutsFound = sources.Sum(source => source.CollectedQuantity)
            };
            return new SnapshotEnvelope
            {
                GameVersion = "1.6.15", StateHash = "h" + index, GameTick = index * 10,
                SaveId = new() { Status = FieldStatus.Available, Value = "save" },
                PlayerId = new() { Status = FieldStatus.Available, Value = "player" },
                InGameTime = new() { Status = FieldStatus.Available, Value = 600 },
                State = new Dictionary<string, JsonElement>
                {
                    ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = index == 0 ? 0 : index + 100 } }),
                    ["locations"] = JsonSerializer.SerializeToElement(new
                    {
                        ginger_island_access = new { status = "available", value = new
                        {
                            schema_version = "ginger_island_access.v1", journey = new { island_reached = index >= 1 },
                            first_parrot = new { complete_or_pending = index >= 2 },
                            western_turtle_parrot = new { complete_or_pending = index >= 3 },
                            island_house_parrot = new { complete_or_pending = index >= 4 }
                        } }
                    }),
                    ["world_progress"] = JsonSerializer.SerializeToElement(new
                    {
                        island_gourmand = new { status = "available", value = progress },
                        golden_walnut_sources = new { status = "available", value = catalog }
                    })
                }
            };
        }

        public void Dispose()
        {
            // Only this fixture's flat temporary files are removed; never recurse into a supplied path.
            foreach (var file in Directory.EnumerateFiles(Root)) File.Delete(file);
            Directory.Delete(Root);
        }
    }
}
