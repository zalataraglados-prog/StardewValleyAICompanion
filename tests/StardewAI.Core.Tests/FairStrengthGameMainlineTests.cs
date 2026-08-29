using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FairStrengthGameMainlineTests
{
    [Fact]
    public void ExactOneTokenStrategyIsTrainableAndNativeTimingExecutorIsCalibrationOnly()
    {
        var strategy = OptionCapabilityRegistrySource.GetRequired("festival.play_strength_game");
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.play_fair_strength_game");

        Assert.True(TrainingEligibilityPolicy.IsEligible(strategy));
        Assert.Contains(strategy.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(new[] { "EVD-295" }, strategy.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-295" }, strategy.OutputEvidenceIds);
        Assert.Contains("exact_one_token_stardrop_top_up", strategy.TrainingEvidenceScope, StringComparison.Ordinal);

        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, executor.TrainingEligibility);
        Assert.DoesNotContain(executor.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.OutputTrainingGate);
        Assert.Equal(new[] { "EVD-295" }, executor.RuntimeEvidenceIds);
    }

    [Fact]
    public void ExactOneTokenDemandCompilesToOneNativeMaximumPowerSession()
    {
        var snapshot = Snapshot("fair-strength-a", remainingDemand: 1);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.play_strength_game" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_fair_strength_game", candidate.Kind);
        AssertParameter(candidate.Parameters, "entry_fee_money", "0");
        AssertParameter(candidate.Parameters, "expected_reward_star_tokens", "1");
        AssertParameter(candidate.Parameters, "perfect_power_minimum", "99");
        AssertParameter(candidate.Parameters, "execution_strategy", "native_predictive_single_click_max_power");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_fair_strength_game", step.Kind);
        Assert.Contains("one_free_native_StrengthGame_session_only", step.SafetyConstraints);
        Assert.Contains("no_direct_power_speed_timer_festival_score_dialogue_or_player_animation_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_fair_strength_game", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_fair_strength_game", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void DemandOtherThanExactlyOneIsExcludedUpstream()
    {
        var snapshot = Snapshot(
            "fair-strength-deferred",
            remainingDemand: 2,
            gateStatus: "deferred_strength_game_not_exact_one_token_top_up");
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.play_strength_game" }, true).Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("fair_strength_requires_exact_one_token_stardrop_top_up", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsProjectionDriftBeforeRuntime()
    {
        var original = Snapshot("fair-strength-a", remainingDemand: 1);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "festival.play_strength_game" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("fair-strength-b", remainingDemand: 1);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("fair_strength_game_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeEntryClickAndSwingWithoutOutcomeWrites()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FairStrengthGame.cs"));

        Assert.Contains("active.Festival.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("game.receiveLeftClick(0, 0)", runtime, StringComparison.Ordinal);
        Assert.Contains("ProjectFairStrengthPower", runtime, StringComparison.Ordinal);
        Assert.Contains("Game1.player.FarmerSprite.isOnToolAnimation()", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("festivalScore +=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("festivalScore++", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.drawObjectDialogue", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("afterSwingAnimation(", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string fingerprint, int remainingDemand, string gateStatus = "ready")
    {
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":28,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":56,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "fair_strength_game":{"value":{
              "projection_status":"complete_current_festival_fall16_strength_game_context",
              "projection_fingerprint":"{{{fingerprint}}}",
              "gate_status":"{{{gateStatus}}}",
              "festival_id":"festival_fall16",
              "festival_location_id":"Town",
              "festival_score":1999,
              "stardrop_price_star_tokens":2000,
              "projected_unclaimed_grange_tokens":0,
              "remaining_star_token_demand":{{{remainingDemand}}},
              "entry_fee_money":0,
              "expected_reward_star_tokens":1,
              "perfect_power_minimum":99,
              "power_maximum":100,
              "required_player_tile_x":29,
              "swing_animation":{"start_frame":168,"interval_ms":80,"frame_count":8},
              "perfect_result_delay_ms":2000,
              "execution_strategy":"native_predictive_single_click_max_power",
              "native_contract":"Event.checkAction(festival_fall16_buildings_540,player_tile_x_29)->StrengthGame.receiveLeftClick->FarmerSprite.animateOnce(168,80ms,8)->StrengthGame.afterSwingAnimation->power>=99->festivalScore+1->native_result_dialogue_and_exit",
              "interaction_tiles":[{"tile_x":30,"tile_y":56,"tile_index":540,"stand_tile_x":29,"stand_tile_y":56,"required_player_tile_x":29}]
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
