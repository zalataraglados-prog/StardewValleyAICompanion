using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class SlotsMainlineTests
{
    [Fact]
    public void MissingCasinoRarecrowDemandFlowsThroughOneNativeStochasticSpin()
    {
        var snapshot = Snapshot("slots-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "minigame.play_slots" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_slots", candidate.Kind);
        AssertParameter(candidate.Parameters, "slots_target_item_id", "(BC)126");
        AssertParameter(candidate.Parameters, "slots_target_club_coins", "10000");
        AssertParameter(candidate.Parameters, "slots_bet", "100");
        AssertParameter(candidate.Parameters, "slots_rng_contract", "shared_Game1.random_live_feedback_not_stable_future_prediction");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_slots", step.Kind);
        Assert.Contains("no_direct_rng_reel_coin_result_or_stat_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_slots", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_slots", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompletedRarecrowDependencyIsExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot("slots-complete", automaticDemand: false, remainingDemand: 0,
                gateStatus: "complete_no_slots_currency_demand"),
            new[] { "minigame.play_slots" }, true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsProbabilityProjectionDrift()
    {
        var original = Snapshot("slots-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "minigame.play_slots" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("slots-b", timesPlayed: 43);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("slots_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RollingRouteContinuationLocksCurrencyTarget()
    {
        var route = JsonNode.Parse("""
        {"option_id":"executor.traverse_connector","normalized_command":{"parameters":[
          {"name":"continuation.option_id","value":"minigame.play_slots"},
          {"name":"continuation.slots_target_club_coins","value":"10000"},
          {"name":"continuation.slots_target_item_id","value":"(BC)126"}
        ]}}
        """)!.AsObject();
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);
        Assert.Equal("slots", continuation!["kind"]!.GetValue<string>());

        var terminal = JsonNode.Parse("""
        {"option_id":"executor.play_slots","normalized_command":{"parameters":[
          {"name":"slots_target_club_coins","value":"10000"},
          {"name":"slots_target_item_id","value":"(BC)126"}
        ]}}
        """)!.AsObject();
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void CapabilityAndRuntimeOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "minigame.play_slots", "executor.play_slots" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-308" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-308" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-308" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-308" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-308" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Slots.cs"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("game.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"clubCoins\s*[+\-]?=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"payoutModifier\s*=(?!=)"), runtime);
        Assert.DoesNotContain("Game1.random.", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        bool automaticDemand = true,
        int remainingDemand = 9500,
        string gateStatus = "ready",
        int timesPlayed = 42)
    {
        var recommendedBet = automaticDemand ? 100 : 0;
        var expectedPayout = 6.2265d;
        var expectedNet = recommendedBet * (expectedPayout - 1d);
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Club","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":12,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":7,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "club_coins":{"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_club_card":{"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "slots":{"value":{
              "schema_version":"slots.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"{{{fingerprint}}}","gate_status":"{{{gateStatus}}}",
              "location_id":"Club","is_current_location":true,"has_club_card":true,"club_coins":500,
              "target_qualified_item_id":"(BC)126","target_item_exists_anywhere":false,
              "deluxe_scarecrow_recipe_unlocked":false,"rarecrow_society_received_or_pending":false,
              "automatic_currency_demand":{{{automaticDemand.ToString().ToLowerInvariant()}}},
              "target_club_coins":10000,"remaining_club_coin_demand":{{{remainingDemand}}},
              "recommended_bet":{{{recommendedBet}}},"low_bet":10,"high_bet":100,"times_played":{{{timesPlayed}}},
              "daily_luck":0.025,"luck_level":0,"luck_multiplier":1.05,
              "expected_payout_multiplier":{{{expectedPayout.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}}},
              "expected_net_coin_delta":{{{expectedNet.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}}},
              "rng_contract":"shared_Game1.random_live_feedback_not_stable_future_prediction",
              "payout_rows":[{"outcomeId":"triple_stardrop","lowerThreshold":0,"upperThreshold":0.001,"probability":0.00105,"payoutMultiplier":2500,"resultPattern":"[5,5,5]"}],
              "interaction_tiles":[{"tile_x":11,"tile_y":7,"action_raw":"ClubSlots","action_token":"ClubSlots"}],
              "exit_policy":"done_after_one_native_settlement",
              "native_contract":"ClubSlots_checkAction_then_native_Slots_10_or_100_spin_then_native_random_settlement_then_done"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Club","width":34,"height":14,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
