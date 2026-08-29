using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FairWheelSpinMainlineTests
{
    [Fact]
    public void BoundedGreenStrategyIsTrainableAndNativeRandomExecutorIsCalibrationOnly()
    {
        var strategy = OptionCapabilityRegistrySource.GetRequired("festival.spin_wheel");
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.spin_fair_wheel");

        Assert.True(TrainingEligibilityPolicy.IsEligible(strategy));
        Assert.Contains(strategy.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(new[] { "EVD-296" }, strategy.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-296" }, strategy.OutputEvidenceIds);
        Assert.Contains("22_of_30", strategy.TrainingEvidenceScope, StringComparison.Ordinal);

        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, executor.TrainingEligibility);
        Assert.DoesNotContain(executor.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.OutputTrainingGate);
    }

    [Fact]
    public void StardropDemandCompilesGreenZeroLuckKellyWagerCappedByDemand()
    {
        var snapshot = Snapshot("fair-wheel-a", score: 1000, remainingDemand: 1000);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.spin_wheel" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("spin_fair_wheel", candidate.Kind);
        AssertParameter(candidate.Parameters, "selected_color", "green");
        AssertParameter(candidate.Parameters, "wager_star_tokens", "466");
        AssertParameter(candidate.Parameters, "base_green_wins", "22");
        AssertParameter(candidate.Parameters, "base_orange_wins", "8");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("spin_fair_wheel", step.Kind);
        Assert.Contains("both_native_random_win_and_loss_are_valid_training_outputs", step.SafetyConstraints);
        Assert.Contains("no_direct_rng_rotation_velocity_wager_festival_score_result_text_or_menu_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.spin_fair_wheel", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("spin_fair_wheel", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ExactOneDemandAndInsufficientBankrollAreExcludedUpstream()
    {
        var exactOne = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("exact-one", 1000, 1,
                "deferred_exact_one_token_uses_free_strength_game"),
                new[] { "festival.spin_wheel" }, true).Options).EventCandidates);
        Assert.False(exactOne.Available);
        Assert.Contains("fair_wheel_requires_stardrop_demand_of_at_least_two_tokens", exactOne.BlockReasons);

        var noBankroll = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("no-bankroll", 1, 1000,
                "deferred_wheel_requires_two_wagerable_star_tokens"),
                new[] { "festival.spin_wheel" }, true).Options).EventCandidates);
        Assert.False(noBankroll.Available);
        Assert.Contains("fair_wheel_exact_zero_luck_kelly_wager_unavailable", noBankroll.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsProjectionDriftBeforeRuntime()
    {
        var original = Snapshot("fair-wheel-a", 1000, 1000);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "festival.spin_wheel" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("fair-wheel-b", 1000, 1000);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("fair_wheel_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeDialogueNumberMenuAndWheelWithoutOutcomeWrites()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FairWheelSpin.cs"));

        Assert.Contains("active.Festival.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.performHoverAction", runtime, StringComparison.Ordinal);
        Assert.Contains("textBox.Text = request.FairWheelWagerStarTokens", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(menu.okButton.bounds.Center.X", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.festivalScore +=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.festivalScore -=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("new WheelSpinGame", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("arrowRotationVelocity =", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        int score,
        int remainingDemand,
        string gateStatus = "ready")
    {
        var wager = remainingDemand >= 2 ? Math.Min(remainingDemand, score * 7 / 15) : 0;
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":28,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":56,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "luck_context":{"value":{"luck_level":0,"daily_luck":0.0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "fair_wheel_spin":{"value":{
              "projection_status":"complete_current_festival_fall16_wheel_context",
              "projection_fingerprint":"{{{fingerprint}}}",
              "gate_status":"{{{gateStatus}}}",
              "festival_id":"festival_fall16",
              "festival_location_id":"Town",
              "festival_score":{{{score}}},
              "stardrop_price_star_tokens":2000,
              "projected_unclaimed_grange_tokens":0,
              "remaining_star_token_demand":{{{remainingDemand}}},
              "selected_color":"green",
              "wager_star_tokens":{{{wager}}},
              "wager_policy":"green_zero_luck_kelly_7_of_15_capped_by_remaining_stardrop_demand",
              "effective_luck_level":0,
              "base_zero_luck_distribution":{"constructor_outcomes":30,"green_wins":22,"orange_wins":8},
              "prestart_duration_ms":1000,
              "result_duration_ms":2500,
              "dialogue_key":"wheelBet",
              "response_key":"Green",
              "native_contract":"Event.checkAction(festival_fall16_buildings_308_309)->DialogueBox(wheelBet:Green).receiveLeftClick->Event.answerDialogue(wheelBet,1)->NumberSelectionMenu(wager_1_to_festivalScore).receiveLeftClick(ok)->Event.betStarTokens->WheelSpinGame(1000ms,green)->native_random_spin->festivalScore+(win?wager:-wager)->native_result_text_and_exit",
              "interaction_tiles":[{"tile_x":29,"tile_y":56,"tile_index":308,"stand_tile_x":28,"stand_tile_y":56}]
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
