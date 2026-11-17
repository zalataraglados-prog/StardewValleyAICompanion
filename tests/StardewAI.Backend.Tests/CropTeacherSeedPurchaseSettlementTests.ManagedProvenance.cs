using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedProvenanceActualLoopHooksAppendNonemptyCurrentDirectoryWithoutCopyingHistoricalClosure()
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var options = ManagedProvenanceOptions(c, Path.Combine(f.Witness.Root, "new-run"));
        var next = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(JsonSerializer.Serialize(c.Episode, JsonOptions), JsonOptions)!;
        next.EpisodeId = "new-original-episode"; next.BeforeSnapshotPath = c.SourcePath;
        Fixture.Save(Path.Combine(options.SnapshotDir, "plan-execution-episode-0001.json"), next);
        var context = InvokeLive<MoneyRevenueCropManagedProvenance>("PrepareManagedCropSourceProvenance", options);
        var output = await InvokeLive<Task<string?>>("WriteManagedCropSourceProvenanceAsync", options, context);
        var report = JsonNode.Parse(File.ReadAllText(output!))!;
        Assert.Equal(2, report["retained_episode_payload_count"]!.GetValue<int>());
        Assert.Equal(2, report["observation_episode_count"]!.GetValue<int>());
        Assert.True(report["continuation_observation_linkage_verified"]!.GetValue<bool>());
        Assert.True(report["initial_day_zero_boundary_verified"]!.GetValue<bool>());
        Assert.False(report["input_artifact_hashes"]!.AsObject().ContainsKey(c.HistoryOnlyPath));
        foreach (var flag in new[] { "production_replayed", "complete_managed_runtime_verified", "source_schedule_verified",
            "global_route_verified", "native_dispatch_performed", "teacher_preference_label_eligible", "formal_product_training_authorized" })
            Assert.False(report[flag]!.GetValue<bool>());
        Assert.NotNull(report["readback_source_binding"]!["embedded_request"]);
        Assert.Equal(1, f.Night.NativeNightCalls); Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("long_observation")]
    [InlineData("nonfresh")]
    [InlineData("sequential")]
    [InlineData("owned_pause")]
    [InlineData("foreign_pause")]
    public async Task ManagedProvenanceRetainsLegalUnverifiedOrSequentialProducerFormsWithoutFalseProof(string mode)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner);
        var directory = Path.Combine(f.Witness.Root, "next-segment"); Directory.CreateDirectory(directory);
        var episode = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(JsonSerializer.Serialize(c.Episode, JsonOptions), JsonOptions)!;
        episode.EpisodeId = "next-original"; episode.BeforeSnapshotPath = c.SourcePath;
        if (mode == "nonfresh") episode.AfterSnapshotFresh = false;
        if (mode is "long_observation" or "owned_pause" or "foreign_pause")
        {
            var source = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(c.SourcePath), JsonOptions)!;
            source.GameTick += 100;
            ChangeState(source, "quests", quests => quests["daily_quest_offer"]!["value"]!["quest"]!["description"] = "later display refresh");
            if (mode != "long_observation")
                ChangeState(source, "player", player => player["movement_timing_context"] = JsonSerializer.SerializeToNode(new
                {
                    status = "available", value = new
                    {
                        player_id = source.PlayerId.Value, game_unique_id = "declared-game", game_paused = true,
                        player_can_move_now = true, player_using_tool_now = false, player_freeze_pause = 0,
                        dialogue_up = false, global_fade = false, freeze_controls = false, is_warping = false,
                        current_event_active = false, event_up = false, immobilizing_buff_19_active = false,
                        executor_owned_idle_pause = new { schema_version = "executor_owned_idle_pause.v1", owner_mod_id = "StardewAI.RuntimeTestHarness",
                            release_policy = "release_before_pending_execution_update", applied = true,
                            player_id = mode == "foreign_pause" ? "another-player" : source.PlayerId.Value,
                            game_unique_id = "declared-game", pause_epoch = 1 }
                    }
                }));
            var path = Path.Combine(directory, "source.json"); Fixture.Save(path, source);
            episode.BeforeSnapshotPath = path; episode.AfterSnapshotPath = path;
            episode.SourceStateHash = source.StateHash; episode.AfterStateHash = source.StateHash;
            episode.BeforeGameTick = source.GameTick; episode.AfterGameTick = source.GameTick;
        }
        Fixture.Save(Path.Combine(directory, "plan-execution-episode-0001.json"), episode);
        if (mode == "sequential")
            for (var ordinal = 1; ordinal <= 2; ordinal++)
            {
                var child = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(JsonSerializer.Serialize(episode, JsonOptions), JsonOptions)!;
                child.EpisodeId += ".item." + ordinal.ToString("D4");
                Fixture.Save(Path.Combine(directory, "plan-execution-episode-0001-item-" + ordinal.ToString("D4") + ".json"), child);
            }
        var report = MoneyRevenueCropManagedProvenance.Prepare(c.Path).Build(directory);
        Assert.Equal(mode is "sequential" or "owned_pause", report["continuation_observation_linkage_verified"]!.GetValue<bool>());
        Assert.Equal(mode == "sequential" ? 4 : 2, report["retained_episode_payload_count"]!.GetValue<int>());
        Assert.Equal(mode == "sequential" ? 3 : 2, report["observation_episode_count"]!.GetValue<int>());
        if (mode is "long_observation" or "foreign_pause") Assert.Single(report["held_observation_edges"]!.AsArray());
        if (mode == "owned_pause")
        {
            Assert.Single(report["owned_pause_observation_edges"]!.AsArray());
            var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(c.SourcePath), JsonOptions)!;
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(episode.BeforeSnapshotPath), JsonOptions)!;
            Assert.Throws<InvalidDataException>(() => GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(c.SourcePath,
                c.Episode, before, episode.BeforeSnapshotPath, episode, after));
        }
        Assert.False(report["global_route_verified"]!.GetValue<bool>());
        Assert.Equal(1, f.Night.NativeNightCalls);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("role")]
    [InlineData("manual")]
    [InlineData("run")]
    [InlineData("day0")]
    [InlineData("opening")]
    [InlineData("inherited")]
    [InlineData("readback")]
    public async Task ManagedProvenanceRejectsChangedOrCrossedAcceptedReferencesBeforeNativeWork(string fault)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var c = await CreateManagedProvenanceCase(f, owner); var request = c.Request;
        if (fault == "hash") File.AppendAllText(request.AcceptedManagedCloseout.Path, " ");
        if (fault == "role") request = request with { AcceptedManagedCloseout = request.AcceptedManagedCloseout with { Role = "manual_closeout" } };
        if (fault == "manual")
        {
            var value = JsonNode.Parse(File.ReadAllText(request.AcceptedManagedCloseout.Path))!;
            value["schema_version"] = "stardewai.manual_native_closeout.v1"; Fixture.Save(request.AcceptedManagedCloseout.Path, value);
            request = request with { AcceptedManagedCloseout = request.AcceptedManagedCloseout with { Sha256 = SiteTillNativeFixture.FileHash(request.AcceptedManagedCloseout.Path) } };
        }
        if (fault == "run") request = request with { RunId = "another-run" };
        if (fault == "day0")
        {
            var value = JsonNode.Parse(File.ReadAllText(request.InitialBoundary.Path))!;
            value["source_total_day"] = 17; Fixture.Save(request.InitialBoundary.Path, value);
            request = request with { InitialBoundary = request.InitialBoundary with { Sha256 = SiteTillNativeFixture.FileHash(request.InitialBoundary.Path) } };
        }
        if (fault == "opening")
        {
            var value = JsonNode.Parse(File.ReadAllText(request.OpeningEpisodePath))!;
            value["after_state_hash"] = "another-state"; Fixture.Save(request.OpeningEpisodePath, value);
        }
        if (fault == "inherited") File.AppendAllText(c.HistoryOnlyPath, " ");
        if (fault == "readback") request = request with { AcceptedSavedReadbacks = [request.AcceptedSavedReadbacks[0] with { AcceptancePath = request.OpeningEpisodePath }] };
        Fixture.Save(c.Path, request);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropManagedProvenance.Prepare(c.Path));
        Assert.Equal(1, f.Night.NativeNightCalls); Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ManagedProvenanceDefaultHooksDoNothingAndDefaultReportShapeIsUnchanged()
    {
        var options = new LiveTrainingOptions();
        Assert.Null(InvokeLive<MoneyRevenueCropManagedProvenance?>("PrepareManagedCropSourceProvenance", options));
        Assert.Null(await InvokeLive<Task<string?>>("WriteManagedCropSourceProvenanceAsync", options, null!));
        Assert.DoesNotContain("managed_crop_source_provenance_path", JsonSerializer.Serialize(new LiveTrainingLoopReport()));
    }

    [Fact]
    public void ManagedProvenanceRejectsRollingBeforeTryingToReadMissingEvidence()
    {
        var options = new LiveTrainingOptions { ManagedCropSourceProvenanceRequestPath = "missing-evidence.json" };
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<MoneyRevenueCropManagedProvenance>(
            "PrepareManagedCropSourceProvenance", options));
        Assert.IsType<ArgumentException>(error.InnerException);
    }
}
