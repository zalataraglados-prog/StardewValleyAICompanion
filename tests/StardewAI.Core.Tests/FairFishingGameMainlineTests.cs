using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FairFishingGameMainlineTests
{
    [Fact]
    public void StrategyIsTrainableAndNativeSessionExecutorIsCalibrationOnly()
    {
        var strategy = OptionCapabilityRegistrySource.GetRequired("festival.play_fishing_game");
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.play_fair_fishing_game");

        Assert.True(TrainingEligibilityPolicy.IsEligible(strategy));
        Assert.Contains(strategy.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(new[] { "EVD-293" }, strategy.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-293" }, strategy.OutputEvidenceIds);
        Assert.Contains("100_second_native_FishingGame", strategy.TrainingEvidenceScope, StringComparison.Ordinal);

        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, executor.TrainingEligibility);
        Assert.DoesNotContain(executor.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.OutputTrainingGate);
        Assert.Equal(new[] { "EVD-293" }, executor.RuntimeEvidenceIds);
    }

    [Fact]
    public void FreshStardropDemandCompilesToOneNativeFishingSession()
    {
        var snapshot = Snapshot("fair-a", remainingDemand: 1000);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.play_fishing_game" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_fair_fishing_game", candidate.Kind);
        AssertParameter(candidate.Parameters, "entry_fee_money", "50");
        AssertParameter(candidate.Parameters, "game_duration_ms", "100000");
        AssertParameter(candidate.Parameters, "execution_strategy", "native_predictive_legal_input");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_fair_fishing_game", step.Kind);
        Assert.Contains("one_native_50g_100_second_session_only", step.SafetyConstraints);
        Assert.Contains("no_direct_money_score_fish_timer_reward_or_inventory_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_fair_fishing_game", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_fair_fishing_game", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void NoRemainingAutomaticDemandIsExcludedUpstream()
    {
        var snapshot = Snapshot("fair-complete", remainingDemand: 0, gateStatus: "complete_projected_tokens_cover_stardrop");
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.play_fishing_game" }, true).Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("fair_fishing_no_remaining_automatic_token_demand", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsProjectionDriftBeforeRuntime()
    {
        var original = Snapshot("fair-a", remainingDemand: 1000);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "festival.play_fishing_game" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("fair-b", remainingDemand: 1000);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("fair_fishing_game_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeDialogueAndSharedLegalInputWithoutOutcomeWrites()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FairFishingGame.cs"));
        var ordinaryFishing = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Fishing.cs"));

        Assert.Contains("Festival.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.performHoverAction(clickX, clickY)", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(clickX, clickY)", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation.answerDialogue", runtime, StringComparison.Ordinal);
        Assert.Contains("PerfectBobberBarShouldPress", runtime, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiLeftButtonOverride", runtime, StringComparison.Ordinal);
        Assert.Contains("game.receiveKeyPress(Keys.Escape)", runtime, StringComparison.Ordinal);
        Assert.Contains("private static bool PerfectBobberBarShouldPress", ordinaryFishing, StringComparison.Ordinal);
        Assert.DoesNotContain("game.score = expected", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("game.perfections = expected", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("festivalScore +=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Money -=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("doneHoldingFish", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string fingerprint, int remainingDemand, string gateStatus = "ready")
    {
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money":{"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "fair_fishing_game":{"value":{
              "projection_status":"complete_current_festival_fall16_fishing_game_context",
              "projection_fingerprint":"{{{fingerprint}}}",
              "gate_status":"{{{gateStatus}}}",
              "festival_id":"festival_fall16",
              "festival_location_id":"Town",
              "player_money":500,
              "entry_fee_money":50,
              "festival_score":0,
              "stardrop_price_star_tokens":2000,
              "projected_unclaimed_grange_tokens":1000,
              "remaining_star_token_demand":{{{remainingDemand}}},
              "game_duration_ms":100000,
              "results_duration_ms":11100,
              "dialogue_key":"fishingGame",
              "play_response_key":"Play",
              "execution_strategy":"native_predictive_legal_input",
              "native_contract":"Event.checkAction(festival_fall16_buildings_503_504)->DialogueBox(fishingGame:Play).receiveLeftClick->Event.answerDialogue(fishingGame,0)->Money-50->globalFadeToBlack(FishingGame.startMe)->native_100000ms_FishingGame_input_session->perfection_score_reward->festivalScore",
              "interaction_tiles":[{"tile_x":10,"tile_y":9,"tile_index":503}]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Town","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
            RealTimestamp = "2026-08-29T00:00:00Z",
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
