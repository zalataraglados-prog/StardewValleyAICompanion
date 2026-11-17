using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedReadbackSourceBindingReusesOriginalLoadAndRetainsLaterObservation(bool metadataRefresh)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var (path, request, episode, sourcePath) = await CreateReadbackSourceBinding(f, owner, metadataRefresh);
        var hashes = new[] { path, request.NativeAfterSnapshotPath, request.ContinuationEpisodePath, sourcePath, f.LoadedPath }
            .ToDictionary(value => value, SiteTillNativeFixture.FileHash);
        var result = GoldenWalnutManagedReadbackSourceBindingBuilder.Build(path);
        Assert.True(result.SourceBindingVerified);
        Assert.Equal(episode.SourceStateHash, result.SourceStateHash);
        Assert.Equal(episode.EpisodeId, result.SourceEpisodeId);
        Assert.Equal(result.LoadedGameTick, result.SourceGameTick);
        Assert.Equal(!metadataRefresh, result.LoadedStateHash == result.SourceStateHash);
        Assert.Single(result.ReadbackAcceptanceSha256s);
        Assert.False(result.NativeDispatchPerformed);
        Assert.False(result.SourceScheduleVerified);
        Assert.False(result.GlobalRouteVerified);
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.False(result.FormalProductTrainingAuthorized);
        foreach (var (input, hash) in hashes) Assert.Equal(hash, SiteTillNativeFixture.FileHash(input));
        if (metadataRefresh)
            Assert.Throws<InvalidDataException>(() => CropTeacherAcceptedSavedReadbackVerifier.Verify(
                request.NativeAfterSnapshotPath, sourcePath, request.RunId, request.AcceptedSavedReadbacks));
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("wrong_run")]
    [InlineData("wrong_episode_source")]
    [InlineData("money")]
    [InlineData("quest_identity")]
    [InlineData("game_time")]
    [InlineData("clock_backward")]
    [InlineData("island_progress")]
    [InlineData("missing_readback")]
    [InlineData("readback_hash")]
    [InlineData("actor")]
    public async Task ManagedReadbackSourceBindingRejectsChangedAuthorityOrUnreceiptedProgress(string fault)
    {
        using var f = new SavedReadbackOwnerFixture(productExecutor: true, prepareSource: DeclareReadbackSourceFields);
        await using var owner = await f.Night.Open();
        var (path, request, episode, sourcePath) = await CreateReadbackSourceBinding(f, owner, metadataRefresh: true);
        var source = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(sourcePath), JsonOptions)!;
        if (fault == "wrong_run") request = request with { RunId = "different-run" };
        if (fault == "wrong_episode_source") episode.SourceStateHash = "different-source";
        if (fault == "money") ChangeState(source, "player", player => player["money"]!["value"] = 987654);
        if (fault == "quest_identity") ChangeState(source, "quests", quests => quests["daily_quest_offer"]!["value"]!["quest"]!["quest_id"] = 999);
        if (fault == "game_time") source.InGameTime.Value += 10;
        if (fault == "clock_backward") { source.GameTick--; episode.BeforeGameTick = source.GameTick; }
        if (fault == "island_progress") ChangeState(source, "world_progress", world => world["golden_walnut_sources"]!["value"]!["world_golden_walnuts_found"] = 1);
        if (fault == "actor") source.PlayerId.Value = "different-player";
        if (fault == "missing_readback") request = request with { AcceptedSavedReadbacks = [] };
        if (fault == "readback_hash")
        {
            var reference = request.AcceptedSavedReadbacks[0];
            var hashes = reference.InputArtifactHashes.ToDictionary(pair => pair.Key, pair => pair.Value);
            hashes[reference.ObservationPath] = new string('0', 64);
            request = request with { AcceptedSavedReadbacks = [reference with { InputArtifactHashes = hashes }] };
        }
        if (fault != "wrong_episode_source") episode.SourceStateHash = source.StateHash;
        Fixture.Save(sourcePath, source); Fixture.Save(request.ContinuationEpisodePath, episode); Fixture.Save(path, request);
        Assert.Throws<InvalidDataException>(() => GoldenWalnutManagedReadbackSourceBindingBuilder.Build(path));
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(4, f.Night.Shared.Runtime.Handler.Commits);
    }

    private static async Task<(string, GoldenWalnutManagedReadbackSourceBindingRequest, PlanExecutionEpisodeEnvelope, string)>
        CreateReadbackSourceBinding(SavedReadbackOwnerFixture f, CropTeacherManagedSeedOwner owner, bool metadataRefresh)
    {
        await f.CompleteNight(owner);
        await f.Run(owner, await f.Admit(owner));
        var trace = await owner.ReadProductionTraceAsync();
        var reference = Assert.Single(trace.SavedContentReadbacks).Reference;
        var fresh = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.LoadedPath), JsonOptions)!;
        if (metadataRefresh)
            ChangeState(fresh, "quests", quests =>
            {
                quests["daily_quest_offer"]!["value"]!["offer_fingerprint"] = "materialized-fingerprint";
                quests["daily_quest_offer"]!["value"]!["quest"]!["description"] = "materialized-description";
            });
        var output = Path.Combine(f.Witness.Root, "later-artifact-run"); Directory.CreateDirectory(output);
        var sourcePath = Path.Combine(output, "before.json"); Fixture.Save(sourcePath, fresh);
        var episode = new PlanExecutionEpisodeEnvelope { EpisodeId = "original-later-episode", RunId = f.Night.Options.RunId,
            SourceStateHash = fresh.StateHash, BeforeGameTick = fresh.GameTick, BeforeSnapshotPath = "before.json",
            Status = "blocked", Success = false, PrimitiveVerificationStatus = "unverified",
            ChangedFacts = JsonSerializer.SerializeToElement(Array.Empty<object>()) };
        var episodePath = Path.Combine(output, "plan-execution-episode-0001.json"); Fixture.Save(episodePath, episode);
        var request = new GoldenWalnutManagedReadbackSourceBindingRequest("golden_walnut_managed_readback_source_binding_request.v1",
            f.Night.Options.RunId, trace.Transitions[^1].NativeTransition.AfterSnapshotPath, episodePath, [reference]);
        var path = Path.Combine(f.Witness.Root, "source-binding-request.json"); Fixture.Save(path, request);
        return (path, request, episode, sourcePath);
    }

    private static void DeclareReadbackSourceFields(SnapshotEnvelope snapshot)
    {
        void Set(string domain, string name, object value)
        {
            var fields = snapshot.State.TryGetValue(domain, out var current) ? JsonNode.Parse(current.GetRawText())!.AsObject() : new JsonObject();
            fields[name] = JsonSerializer.SerializeToNode(new { status = "available", value });
            snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
        }
        Set("locations", "ginger_island_access", new { status = "blocked", journey = new { island_reached = false } });
        Set("world_progress", "golden_walnut_sources", new { world_golden_walnuts_found = 0, sources = Array.Empty<object>() });
        Set("quests", "daily_quest_offer", new { offer_fingerprint = "unmaterialized", quest = new { quest_id = 17, description = "" } });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
