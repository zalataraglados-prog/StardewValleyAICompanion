using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CalicoJackMainlineTests
{
    [Fact]
    public void MissingCasinoRarecrowDemandFlowsFromTransparentCandidateToNativeExecutor()
    {
        var snapshot = Snapshot("calico-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "minigame.play_calico_jack" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_calico_jack", candidate.Kind);
        AssertParameter(candidate.Parameters, "calico_target_item_id", "(BC)126");
        AssertParameter(candidate.Parameters, "calico_target_club_coins", "10000");
        AssertParameter(candidate.Parameters, "calico_bet", "100");
        AssertParameter(candidate.Parameters, "calico_exit_policy", "quit_after_one_native_settlement");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_calico_jack", step.Kind);
        Assert.Contains("no_direct_card_rng_coin_or_result_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_calico_jack", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_calico_jack", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompletedRarecrowDependencyIsExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot("calico-complete", automaticDemand: false, remainingDemand: 0,
                gateStatus: "complete_no_calico_jack_currency_demand"),
            new[] { "minigame.play_calico_jack" }, true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsSeedProjectionDrift()
    {
        var original = Snapshot("calico-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "minigame.play_calico_jack" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("calico-b", timesPlayedSeed: 43);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("calico_jack_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RollingRouteContinuationLocksCurrencyTarget()
    {
        var route = JsonNode.Parse("""
        {"option_id":"executor.traverse_connector","normalized_command":{"parameters":[
          {"name":"continuation.option_id","value":"minigame.play_calico_jack"},
          {"name":"continuation.calico_target_club_coins","value":"10000"},
          {"name":"continuation.calico_target_item_id","value":"(BC)126"}
        ]}}
        """)!.AsObject();
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);
        Assert.Equal("calico_jack", continuation!["kind"]!.GetValue<string>());

        var ranked = JsonNode.Parse("""
        [{"option_id":"minigame.play_calico_jack","parameters":[
          {"name":"calico_target_club_coins","value":"10000"},{"name":"calico_target_item_id","value":"(BC)126"}
        ]},{"option_id":"minigame.play_calico_jack","parameters":[
          {"name":"calico_target_club_coins","value":"5000"},{"name":"calico_target_item_id","value":"(BC)126"}
        ]}]
        """)!.AsArray();
        Assert.Single(QueueReplanFilter.FilterRankedCandidates(ranked, continuation));

        var terminal = JsonNode.Parse("""
        {"option_id":"executor.play_calico_jack","normalized_command":{"parameters":[
          {"name":"calico_target_club_coins","value":"10000"},{"name":"calico_target_item_id","value":"(BC)126"}
        ]}}
        """)!.AsObject();
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "blocked"));
    }

    [Fact]
    public void ReplayableRandomCursorClonesWithoutSharingState()
    {
        var cursor = new CalicoJackRandomCursor(() => new Random(304));
        cursor.Next(1, 12);
        cursor.NextDouble();
        var clone = cursor.Clone();

        Assert.Equal(cursor.OperationCount, clone.OperationCount);
        Assert.Equal(cursor.Next(1, 10), clone.Next(1, 10));
        Assert.Equal(cursor.NextDouble(), clone.NextDouble());
    }

    [Fact]
    public void CapabilityAndRuntimeSourcesOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "minigame.play_calico_jack", "executor.play_calico_jack" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-304" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-304" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-304" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-304" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-304" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CalicoJack.cs"));
        var bridge = File.ReadAllText(Path.Combine(
            root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.CalicoJack.cs"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("game.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("CalicoJackDecisionModel.Recommend", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"clubCoins\s*[+\-]?=(?!=)"), runtime);
        Assert.DoesNotContain("playerCards.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("dealerCards.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("currentBet *=", runtime, StringComparison.Ordinal);
        Assert.Contains("wiki_says_luck_does_not_affect_results_but_1.6.15", bridge, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        bool automaticDemand = true,
        int remainingDemand = 9500,
        string gateStatus = "ready",
        int timesPlayedSeed = 42)
    {
        var recommendedBet = automaticDemand ? 100 : 0;
        var recommendedTable = automaticDemand ? "low_stakes" : "none";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Club","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":22,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "club_coins":{"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_club_card":{"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "calico_jack":{"value":{
              "schema_version":"calico_jack.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"{{{fingerprint}}}","gate_status":"{{{gateStatus}}}",
              "location_id":"Club","is_current_location":true,"has_club_card":true,"club_coins":500,
              "target_qualified_item_id":"(BC)126","target_item_exists_anywhere":false,
              "deluxe_scarecrow_recipe_unlocked":false,"rarecrow_society_received_or_pending":false,
              "automatic_currency_demand":{{{automaticDemand.ToString().ToLowerInvariant()}}},
              "target_club_coins":10000,"remaining_club_coin_demand":{{{remainingDemand}}},
              "recommended_bet":{{{recommendedBet}}},"recommended_table_kind":"{{{recommendedTable}}}",
              "next_times_played_seed":{{{timesPlayedSeed}}},"days_played_seed":73,
              "unique_game_id_seed":"9988776655","daily_luck":0.025,"luck_level":2,
              "interaction_tiles":[{"tile_x":23,"tile_y":10,"action_raw":"ClubCards","action_token":"ClubCards","table_kind":"low_stakes","bet":100,"dialogue_key":"CalicoJack","play_response_key":"Play"}],
              "next_round":{"times_played_seed":{{{timesPlayedSeed}}},"player_cards":[8,9],"dealer_cards_including_hidden":[7,8],
                "recommended_first_action":"stand","stand_coin_delta":100,"hit_coin_delta":-100,"projected_next_hit_card":4,
                "coin_delta_per_low_bet":100,"projected_outcome":"player_higher"},
              "native_contract":"ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Club","width":32,"height":24,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
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
