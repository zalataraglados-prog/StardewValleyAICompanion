using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void RustyKeyCausalJoinRebuildsIndependentPreferenceAndNativeRequestChain()
    {
        var f = CreateNativeJoinFixture();
        var result = RustyKeyTeacherNativeJoinBuilder.Build(f.RequestPath);
        Assert.True(result.TeacherComparisonVerified);
        Assert.True(result.NativeTerminalOutcomeVerified);
        Assert.True(result.CausalContinuationVerified);
        Assert.False(result.FullCriterionVerified);
        Assert.False(result.ThresholdSupplyVerified);
        Assert.False(result.GlobalNightHandoffVerified);
        Assert.False(result.FormalTrainingAuthorized);
        Assert.Equal(4, result.Actions.Length);
        Assert.Equal(27, result.SourceSha256.Count);
        Assert.Equal(6, result.DecisionTotalDay);
        Assert.Equal(7, result.TerminalTotalDay);
        Assert.Equal("MuseumFarm:123:6", result.SplitKey);
        Assert.Equal(PolicyTrajectoryDatasetBuilder.PartitionFor(result.SplitKey), result.DatasetPartition);
        Assert.Equal("museum-donate:0:(O)96", result.PreferredCandidateId);
        var sleepQueue = JsonNode.Parse(File.ReadAllText(f.Request.Actions[1].ActionQueuePath))!;
        Assert.Equal("recovery", sleepQueue["items"]![0]!["behavior_category"]!.GetValue<string>());
        var path = Path.Combine(f.Root, "join.json");
        WriteNativeKeyJson(path, result);
        Assert.Equal(JsonSerializer.Serialize(result, JsonOptions),
            JsonSerializer.Serialize(RustyKeyTeacherNativeJoinBuilder.Verify(path), JsonOptions));
    }

    [Fact]
    public void RustyKeyCausalJoinDoesNotMistakeLaterBudgetProofAnnotationForANativeCommandChange()
    {
        var f = CreateNativeJoinFixture();
        EditNativeKeyJson(f.Request.Actions[0].ActionQueuePath, node =>
        {
            var parameters = node["items"]![0]!["normalized_command"]!["parameters"]!.AsArray();
            var marker = parameters.FirstOrDefault(row => row!["name"]!.GetValue<string>() == "safety_constraint" &&
                row["value"]!.GetValue<string>() == "daily_plan_time_budget_checked");
            Assert.NotNull(marker);
            parameters.Remove(marker);
        });
        Assert.True(RustyKeyTeacherNativeJoinBuilder.Build(f.RequestPath).CausalContinuationVerified);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("missing_action")]
    [InlineData("foreign_run")]
    [InlineData("wrong_mode")]
    [InlineData("wrong_actor")]
    [InlineData("different_isolation")]
    [InlineData("different_endpoint")]
    [InlineData("duplicate_nonce")]
    [InlineData("invalid_gzip")]
    [InlineData("unbound_after_tick")]
    [InlineData("donation_over_horizon")]
    [InlineData("wrong_command")]
    [InlineData("different_safety_constraint")]
    [InlineData("wrong_goal")]
    [InlineData("wrong_donation_count")]
    [InlineData("wrong_consumption")]
    [InlineData("early_key")]
    [InlineData("missing_save")]
    [InlineData("missing_sleep_status")]
    [InlineData("wrong_sleep_day_fact")]
    [InlineData("native_result_unverified")]
    [InlineData("missing_arrival_fact")]
    [InlineData("unregistered_actor_profile")]
    [InlineData("wrong_admission_role")]
    [InlineData("missing_request_schema")]
    [InlineData("empty_request_schema")]
    [InlineData("wire_target_item")]
    [InlineData("wire_stack")]
    [InlineData("missing_wire_field")]
    [InlineData("terminal_source_substitution")]
    [InlineData("recorded_queue_substitution")]
    public void RustyKeyCausalJoinRejectsUnboundOrNoncausalSources(string mode)
    {
        var f = CreateNativeJoinFixture();
        void Result(int index, Action<JsonNode> edit) => EditNativeKeyJson(f.Request.Actions[index].ExecutionReceiptPath, edit);
        void DonationAfter(Action<JsonNode> edit, bool bind = true)
        {
            var path = f.Request.Actions[0].AfterSnapshotPath;
            EditNativeKeySnapshot(path, node => edit(node));
            if (bind)
            {
                var after = JsonNode.Parse(File.ReadAllText(path))!;
                Result(0, node => { node["after_state_hash"] = after["state_hash"]!.DeepClone(); node["after_game_tick"] = after["game_tick"]!.DeepClone(); });
            }
        }
        switch (mode)
        {
            case "gap": DonationAfter(node => node["state"]!["player"]!["tile_x"]!["value"] = 20); break;
            case "missing_action":
                EditNativeKeyJson(f.RequestPath, node => node["actions"]!.AsArray().RemoveAt(2)); break;
            case "foreign_run": Result(2, node => node["run_id"] = "same-player-other-run"); break;
            case "wrong_mode": RewriteJoinRequest(f, 2, request => request.ExecutionMode = "coop_companion"); break;
            case "wrong_actor": RewriteJoinRequest(f, 2, request => request.Actor = "ai_host.main"); break;
            case "different_isolation": RewriteJoinRequest(f, 2, request => request.SaveIsolationPath += "-other"); break;
            case "different_endpoint": EditNativeKeyJson(f.JournalPaths[2], node => node["executor_url"] = "http://127.0.0.1:8768/api/v1/training/execute"); break;
            case "duplicate_nonce":
                var nonce = ExecutionRequestJournalReader.Read(f.JournalPaths[1]).Request.RequestNonce;
                RewriteJoinRequest(f, 2, request => request.RequestNonce = nonce); break;
            case "invalid_gzip": File.WriteAllBytes(f.JournalPaths[1] + ".gz", new byte[] { 1, 2, 3 }); break;
            case "unbound_after_tick": DonationAfter(node => node["game_tick"] = 21, bind: false); break;
            case "donation_over_horizon": DonationAfter(node => node["game_tick"] = 602); break;
            case "wrong_command":
                EditNativeKeyJson(f.Request.Actions[0].ActionQueuePath, node =>
                    node["items"]![0]!["normalized_command"]!["steps"]![0]!["target"] = "another-target"); break;
            case "different_safety_constraint":
                EditNativeKeyJson(f.Request.Actions[0].ActionQueuePath, node =>
                    node["items"]![0]!["normalized_command"]!["parameters"]!.AsArray().Add(new JsonObject
                        { ["name"] = "safety_constraint", ["value"] = "unverified_native_action_override" })); break;
            case "wrong_goal": EditNativeKeyJson(f.Request.Actions[0].ActionQueuePath, node => node["goal_id"] = "another-goal"); break;
            case "wrong_donation_count": DonationAfter(node => node["state"]!["world_progress"]!["museum"]!["value"]!["donated_count"] = 59); break;
            case "wrong_consumption": DonationAfter(node => node["state"]!["player"]!["inventory"]!["value"]![0]!["stack"] = 0); break;
            case "early_key": DonationAfter(node => node["state"]!["player"]!["has_rusty_key"]!["value"] = true); break;
            case "missing_save": Result(1, node => node["primitive_verification_reasons"]!.AsArray().RemoveAt(2)); break;
            case "missing_sleep_status": Result(1, node => node["end_of_night_transaction_status"] = "sleep_pending"); break;
            case "wrong_sleep_day_fact": Result(1, node => node["changed_facts"]![0]!["after"] = "8"); break;
            case "native_result_unverified": Result(2, node => node["primitive_verification_status"] = "unknown"); break;
            case "missing_arrival_fact": Result(2, node => node["primitive_verification_reasons"] = new JsonArray()); break;
            case "unregistered_actor_profile":
                EditNativeKeyJson(f.Request.Actions[2].ActionQueuePath, node => node["actor"]!["control_surface"] = "os_keyboard"); break;
            case "wrong_admission_role":
                EditNativeKeyJson(f.Request.Actions[2].ActionQueuePath, node => node["items"]![0]!["training_role"] = "player_command_only"); break;
            case "missing_request_schema":
                RewriteJoinPayload(f, 2, payload => payload.Remove("schema_version")); break;
            case "empty_request_schema": RewriteJoinRequest(f, 2, request => request.SchemaVersion = ""); break;
            case "wire_target_item": RewriteJoinRequest(f, 0, request => request.QualifiedItemId = "(O)100"); break;
            case "wire_stack": RewriteJoinRequest(f, 0, request => request.ExpectedStackBefore = 99); break;
            case "missing_wire_field": RewriteJoinPayload(f, 0, payload => payload.Remove("expected_donated_count_after")); break;
            case "terminal_source_substitution":
                var copyPath = Path.Combine(f.Root, "later-key-capture.json");
                var copy = JsonNode.Parse(File.ReadAllText(f.Request.Actions[^1].AfterSnapshotPath))!;
                copy["another_observation"] = true;
                WriteNativeKeyJson(copyPath, copy);
                EditNativeKeyJson(f.RequestPath, node => node["actions"]![3]!["after_snapshot_path"] = copyPath); break;
            case "recorded_queue_substitution":
                var queuePath = Path.Combine(f.Root, "same-queue-different-source.json");
                WriteNativeKeyJson(queuePath, JsonNode.Parse(File.ReadAllText(f.Request.Actions[2].ActionQueuePath))!);
                EditNativeKeyJson(f.RequestPath, node => node["actions"]![2]!["action_queue_path"] = queuePath); break;
        }
        Assert.Throws<InvalidDataException>(() => RustyKeyTeacherNativeJoinBuilder.Build(f.RequestPath));
    }

    [Theory]
    [InlineData("dataset_partition")]
    [InlineData("decision_total_day")]
    [InlineData("full_criterion_verified")]
    [InlineData("formal_training_authorized")]
    public void RustyKeyCausalJoinReplayDoesNotTrustPersistedClaims(string field)
    {
        var f = CreateNativeJoinFixture();
        var receipt = JsonSerializer.SerializeToNode(RustyKeyTeacherNativeJoinBuilder.Build(f.RequestPath), JsonOptions)!;
        if (field == "dataset_partition") receipt[field] = "validation";
        else if (field == "decision_total_day") receipt[field] = 7;
        else receipt[field] = true;
        var path = Path.Combine(f.Root, "forged-join.json");
        WriteNativeKeyJson(path, receipt);
        Assert.Throws<InvalidDataException>(() => RustyKeyTeacherNativeJoinBuilder.Verify(path));
    }

    private static void RewriteJoinRequest(NativeJoinFixture f, int index, Action<TrainingExecutionRequest> edit)
    {
        var request = ExecutionRequestJournalReader.Read(f.JournalPaths[index]).Request;
        edit(request);
        WriteJoinJournal(f.JournalPaths[index], request, f.BeforePaths[index]);
        BindJoinJournalResult(f, index, request.RequestNonce);
    }

    private static void RewriteJoinPayload(NativeJoinFixture f, int index, Action<JsonObject> edit)
    {
        var request = ExecutionRequestJournalReader.Read(f.JournalPaths[index]).Request;
        WriteJoinJournal(f.JournalPaths[index], request, f.BeforePaths[index], edit);
        BindJoinJournalResult(f, index, request.RequestNonce);
    }

    private static void BindJoinJournalResult(NativeJoinFixture f, int index, string nonce)
    {
        var updated = ExecutionRequestJournalReader.Read(f.JournalPaths[index]);
        EditNativeKeyJson(f.Request.Actions[index].ExecutionReceiptPath, node =>
        {
            node["execution_request_sha256"] = updated.Metadata.GetProperty("raw_request_sha256").GetString();
            node["execution_request_nonce"] = nonce;
        });
    }
}
