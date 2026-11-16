using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("daily_offer")]
    [InlineData("wrong_quest")]
    [InlineData("wrong_option")]
    [InlineData("non_route")]
    [InlineData("missing_identity")]
    public void JointQuestRoutesReuseCanonicalTaskIdentityWithoutChangingLegacyExactMatching(string fault)
    {
        var option = fault == "daily_offer" ? "quest.accept_daily" : "quest.advance";
        JsonObject Candidate(string id, string quest, string optionId) => new()
        {
            ["candidate_id"] = id, ["kind"] = "route_connector_tile", ["option_id"] = optionId,
            ["available"] = true, ["location_id"] = id == "old-route" ? "Farm" : "BusStop",
            ["parameters"] = new JsonArray(new JsonObject { ["name"] = "quest_candidate_id", ["value"] = quest })
        };
        var original = Candidate("old-route", "quest:13:Quest", option);
        if (fault == "non_route") original["kind"] = "quest_npc_interaction";
        if (fault == "missing_identity") original["parameters"] = new JsonArray();
        var locked = new SelectedQueueCandidateLock(3, "old-route", option, original, null);
        var fresh = new JsonArray(Candidate("new-route", fault == "wrong_quest" ? "quest:14:Quest" : "quest:13:Quest",
            fault == "wrong_option" ? "quest.accept_daily" : option));
        Assert.Empty(SelectedQueueCandidateMatcher.FilterMaterializedCandidates(fresh, locked));
        var matches = CropTeacherSharedCandidateSelection.FilterMaterializedCandidates(fresh, locked);
        if (fault is "valid" or "daily_offer")
        {
            Assert.Equal("new-route", Assert.Single(matches)!["candidate_id"]!.GetValue<string>());
            Assert.Null(locked.ObjectiveContinuation);
            Assert.Equal("old-route", original["candidate_id"]!.GetValue<string>());
        }
        else Assert.Empty(matches);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("wrong_npc")]
    [InlineData("wrong_kind")]
    [InlineData("wrong_option")]
    public void AcceptedJointCursorCarriesRetryBudgetWithoutChangingTheOriginalLock(string fault)
    {
        var original = new JsonObject
        {
            ["kind"] = "social", ["option_id"] = "social.talk_npc", ["npc_name"] = "Abigail",
            ["target_location"] = "Town", ["retry_count"] = "0", ["retry_game_time"] = "600"
        };
        var selected = new SelectedQueueCandidateLock(3, "social:talk:Abigail", "social.talk_npc", new JsonObject
        {
            ["candidate_id"] = "social:talk:Abigail", ["option_id"] = "social.talk_npc",
            ["parameters"] = new JsonArray(new JsonObject { ["name"] = "npc_name", ["value"] = "Abigail" })
        }, original);
        var cursor = original.DeepClone().AsObject();
        cursor["retry_count"] = "7"; cursor["retry_game_time"] = "910"; cursor["target_location"] = "SeedShop";
        if (fault == "wrong_npc") cursor["npc_name"] = "Leah";
        if (fault == "wrong_kind") cursor["kind"] = "quest";
        if (fault == "wrong_option") cursor["option_id"] = "social.gift_npc";
        var accepted = new Dictionary<string, JsonObject> { [selected.CandidateId] = cursor };
        if (fault == "valid")
        {
            var effective = CropTeacherSharedCandidateSelection.WithAcceptedContinuation(selected, accepted);
            Assert.Equal("7", effective.ObjectiveContinuation!["retry_count"]!.GetValue<string>());
            Assert.Equal("910", effective.ObjectiveContinuation["retry_game_time"]!.GetValue<string>());
            Assert.Equal("SeedShop", effective.ObjectiveContinuation["target_location"]!.GetValue<string>());
            effective.ObjectiveContinuation["retry_count"] = "8";
            Assert.Equal("7", cursor["retry_count"]!.GetValue<string>());
            Assert.Equal("0", original["retry_count"]!.GetValue<string>());
        }
        else Assert.Throws<InvalidDataException>(() => CropTeacherSharedCandidateSelection.WithAcceptedContinuation(selected, accepted));
    }

    [Fact]
    public async Task AcceptedSharedCursorSurvivesReopenAndFeedsTheOriginalPendingChoice()
    {
        var f = new NativeMailWorkFixture(); using var host = f.Runtime;
        await using var owner = await f.Files.Open(); f.Compile(owner.CurrentSnapshotPath);
        f.BindDecision(await SaveDaily(owner, f.Selection));
        var originalPlan = File.ReadAllBytes(f.Selection.DecisionPlanPath);
        JsonNode? acceptedCursor = null;
        for (var stage = 0; stage < 2; stage++)
        {
            var before = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
            var operation = await owner.AdmitNativeSharedSourceYieldAsync(f.Selection, f.ReceiptPath, f.AfterPath, f.RuntimePath);
            await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Runtime.Client, f.Runtime.LiveOptions,
                owner, operation, f.Selection.QueuePath, (Func<Task<JsonObject>>)(() => f.Emit(before)));
            acceptedCursor = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!["objective_continuation"]!.DeepClone();
            f.Compile(owner.CurrentSnapshotPath);
        }
        var retained = (await ReadDaily(owner))!;
        var cursors = (IReadOnlyDictionary<string, JsonObject>)retained.GetType().GetProperty("ForeignObjectiveContinuations")!.GetValue(retained)!;
        var current = Assert.Single(cursors);
        Assert.True(JsonNode.DeepEquals(acceptedCursor, current.Value));
        await owner.DisposeAsync();
        await using var reopened = await f.Files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, f.Files.Recovery, f.Files.Post);
        var day = LifecycleRead<SnapshotEnvelope>(reopened.CurrentSnapshotPath).State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program")!;
        var restore = (Task)program.GetMethod("RestoreCropTeacherDailySelectionAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [reopened, day, null])!;
        await restore;
        var selection = restore.GetType().GetProperty("Result")!.GetValue(restore)!;
        var restored = SelectionProperty<IReadOnlyDictionary<string, JsonObject>>(selection, "ForeignObjectiveContinuations")!;
        Assert.True(JsonNode.DeepEquals(current.Value, Assert.Single(restored).Value));
        var lease = SelectionProperty<SelectedQueueDecisionLease>(selection, "Lease")!;
        var effective = CropTeacherSharedCandidateSelection.WithAcceptedContinuation(lease.CandidateAt(f.Selection.SelectedQueueIndex), restored);
        Assert.True(JsonNode.DeepEquals(current.Value, effective.ObjectiveContinuation));
        Assert.Equal(originalPlan, File.ReadAllBytes(f.Selection.DecisionPlanPath));
        Assert.Equal(2, f.Runtime.Handler.Commits);
        Assert.Null(reopened.Pending);
    }
}
