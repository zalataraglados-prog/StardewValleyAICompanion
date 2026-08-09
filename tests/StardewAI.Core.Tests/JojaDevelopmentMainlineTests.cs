using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class JojaDevelopmentMainlineTests
{
    [Fact]
    public void MembershipFlowsThroughCandidatePlanAndActionQueue()
    {
        var snapshot = Snapshot(membershipReceived: false, routeState: "undecided", money: 12000);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "joja.advance_development" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        Assert.Equal("purchase_joja_membership", candidate.Kind);
        AssertParameter(candidate.Parameters, "price", "5000");
        AssertParameter(candidate.Parameters, "expected_money_after", "7000");
        AssertParameter(candidate.Parameters, "expected_mail_for_tomorrow", "JojaMember");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        Assert.Equal("purchase_joja_membership", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.purchase_joja_membership", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("purchase_joja_membership", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MembershipCompilerOwnsTheFirstNativeGreetingInteraction()
    {
        var snapshot = Snapshot(membershipReceived: false, routeState: "undecided", money: 12000);
        var state = snapshot.State.ToDictionary(pair => pair.Key, pair => pair.Value);
        state["world_progress"] = JsonSerializer.Deserialize<JsonElement>(
            state["world_progress"].GetRawText().Replace("\"actor_greeting_received\":true", "\"actor_greeting_received\":false", StringComparison.Ordinal), JsonOptions);
        snapshot = new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = snapshot.GameTick,
            RealTimestamp = snapshot.RealTimestamp,
            Completeness = snapshot.Completeness,
            State = state
        };
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "joja.advance_development" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        Assert.Equal("purchase_joja_membership", candidate.Kind);
        AssertParameter(candidate.Parameters, "expected_greeting_before", "false");
        AssertParameter(candidate.Parameters, "expected_greeting_after", "true");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.purchase_joja_membership", Assert.Single(queue.Items).OptionId);
    }

    [Fact]
    public void ExactProjectFlowsThroughCandidatePlanAndActionQueue()
    {
        var snapshot = Snapshot(membershipReceived: true, routeState: "joja_locked", money: 30000);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "joja.advance_development" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        Assert.Equal("purchase_joja_project", candidate.Kind);
        AssertParameter(candidate.Parameters, "project_id", "boiler_room");
        AssertParameter(candidate.Parameters, "button_number", "1");
        AssertParameter(candidate.Parameters, "price", "15000");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.purchase_joja_project", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("purchase_joja_project", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsProjectWhenMoneyProjectionDrifts()
    {
        var original = Snapshot(membershipReceived: true, routeState: "joja_locked", money: 30000);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(original, new[] { "joja.advance_development" }, true);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot(membershipReceived: true, routeState: "joja_locked", money: 29999);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("joja_development_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CandidateRejectsProjectWhoseButtonPriceMailTupleIsNotNative()
    {
        var snapshot = Snapshot(membershipReceived: true, routeState: "joja_locked", money: 30000);
        var state = snapshot.State.ToDictionary(pair => pair.Key, pair => pair.Value);
        state["world_progress"] = JsonSerializer.Deserialize<JsonElement>(
            state["world_progress"].GetRawText().Replace("\"price\":15000", "\"price\":14000", StringComparison.Ordinal), JsonOptions);
        snapshot = new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = snapshot.GameTick,
            RealTimestamp = snapshot.RealTimestamp,
            Completeness = snapshot.Completeness,
            State = state
        };

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "joja.advance_development" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.CandidateId == "joja-project:boiler_room"));

        Assert.False(candidate.Available);
        Assert.Contains("joja_project_typed_projection_invalid", candidate.BlockReasons);
    }

    [Fact]
    public void TransparencyAndRuntimeUseExactNativeJojaRulesWithoutDirectProgressWrites()
    {
        var adapter = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));
        var runtime = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.Joja.cs"));

        foreach (var exact in new[] { "ccVault", "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank", "jojaVault", "jojaBoilerRoom", "jojaCraftsRoom", "jojaPantry", "jojaFishTank" })
        {
            Assert.Contains(exact, adapter, StringComparison.Ordinal);
        }
        Assert.Contains("Utility.doesAnyFarmerHaveOrWillReceiveMail", adapter, StringComparison.Ordinal);
        Assert.Contains("FindActionTile(jojaMart, \"JoinJoja\")", adapter, StringComparison.Ordinal);
        Assert.Contains("active.Mart.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("characterDialogue.chooseResponse", runtime, StringComparison.Ordinal);
        Assert.Contains("active.Mart.answerDialogue(yes)", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.addMailForTomorrow", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractKeepsActorAndAnyFarmerCompletionEvidenceSeparate()
    {
        var row = new JojaDevelopmentProjectRef
        {
            ProjectId = "vault",
            CcMailReceivedOrPending = false,
            AnyFarmerCcMailReceivedOrPending = true,
            JojaMailReceivedOrPending = false,
            CompleteOrPending = true
        };
        var json = JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"cc_mail_received_or_pending\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"any_farmer_cc_mail_received_or_pending\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"complete_or_pending\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeMatrixUsesIsolatedSaveDailyPlanAndNativeNextDaySettlement()
    {
        var script = File.ReadAllText(FindRepositoryFile(
            "scripts", "Invoke-RuntimeJojaDevelopmentDailyPlanSmoke.ps1"));
        var fixture = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.JojaFixture.cs"));
        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Joja.cs"));
        var supported = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SupportedOptions.cs"));

        Assert.Contains("Copy-Item -LiteralPath $sourceSaveSlot -Destination $isolatedSaveSlot -Recurse", script, StringComparison.Ordinal);
        Assert.Contains("--use-daily-plan", script, StringComparison.Ordinal);
        Assert.Contains("debug.setup_joja_development", script, StringComparison.Ordinal);
        Assert.Contains("debug.prepare_joja_settlement_sleep", script, StringComparison.Ordinal);
        Assert.Contains("\"debug.setup_joja_development\"", supported, StringComparison.Ordinal);
        Assert.Contains("\"debug.prepare_joja_settlement_sleep\"", supported, StringComparison.Ordinal);
        Assert.Contains("executor.sleep", script, StringComparison.Ordinal);
        Assert.Contains("membership_without_greeting", fixture, StringComparison.Ordinal);
        Assert.Contains("membership_with_greeting", fixture, StringComparison.Ordinal);
        foreach (var project in new[] { "vault", "boiler_room", "crafts_room", "pantry", "fish_tank" })
        {
            Assert.Contains("project_" + project, fixture, StringComparison.Ordinal);
        }
        Assert.Contains("PrimitiveVerificationStatus = \"verified\"", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.addMailForTomorrow", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void JojaFixtureCaseRoundTripsThroughTheTypedDebugContract()
    {
        var request = new StardewAI.Contracts.Training.TrainingExecutionRequest
        {
            JojaFixtureCase = "project_vault"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<StardewAI.Contracts.Training.TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"joja_fixture_case\":\"project_vault\"", json, StringComparison.Ordinal);
        Assert.Equal("project_vault", roundTrip.JojaFixtureCase);
    }

    private static SnapshotEnvelope Snapshot(bool membershipReceived, string routeState, int money)
    {
        var membershipStatus = membershipReceived ? "irreversible_route_already_locked" : "ready";
        var projectStatus = membershipReceived ? "ready" : "joja_membership_not_irreversibly_locked";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"JojaMart","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "joja_development":{"value":{
              "location_accessible":true,
              "is_current_location":true,
              "join_action_tile_x":10,
              "join_action_tile_y":10,
              "join_action_raw":"JoinJoja",
              "host_route_state":"{{{routeState}}}",
              "actor_membership_received":{{{membershipReceived.ToString().ToLowerInvariant()}}},
              "actor_membership_pending":false,
              "actor_greeting_received":true,
              "actor_membership_event_seen":true,
              "completion_ceremony_event_seen":false,
              "membership_price":5000,
              "money":{{{money}}},
              "membership_action_status":"{{{membershipStatus}}}",
              "project_order_pending":false,
              "pending_project_mail_ids":[],
              "all_projects_complete_or_pending":false,
              "projects":[
                {"button_number":0,"project_id":"vault","cc_mail_id":"ccVault","joja_mail_id":"jojaVault","price":40000,"complete_or_pending":true,"cc_mail_received_or_pending":true,"any_farmer_cc_mail_received_or_pending":true,"joja_mail_received_or_pending":true,"action_status":"joja_project_complete_or_pending"},
                {"button_number":1,"project_id":"boiler_room","cc_mail_id":"ccBoilerRoom","joja_mail_id":"jojaBoilerRoom","price":15000,"complete_or_pending":false,"cc_mail_received_or_pending":false,"any_farmer_cc_mail_received_or_pending":false,"joja_mail_received_or_pending":false,"action_status":"{{{projectStatus}}}"},
                {"button_number":2,"project_id":"crafts_room","cc_mail_id":"ccCraftsRoom","joja_mail_id":"jojaCraftsRoom","price":25000,"complete_or_pending":true,"cc_mail_received_or_pending":true,"any_farmer_cc_mail_received_or_pending":true,"joja_mail_received_or_pending":true,"action_status":"joja_project_complete_or_pending"},
                {"button_number":3,"project_id":"pantry","cc_mail_id":"ccPantry","joja_mail_id":"jojaPantry","price":35000,"complete_or_pending":true,"cc_mail_received_or_pending":true,"any_farmer_cc_mail_received_or_pending":true,"joja_mail_received_or_pending":true,"action_status":"joja_project_complete_or_pending"},
                {"button_number":4,"project_id":"fish_tank","cc_mail_id":"ccFishTank","joja_mail_id":"jojaFishTank","price":20000,"complete_or_pending":true,"cc_mail_received_or_pending":true,"any_farmer_cc_mail_received_or_pending":true,"joja_mail_received_or_pending":true,"action_status":"joja_project_complete_or_pending"}
              ]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"JojaMart","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
