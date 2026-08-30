using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CraneGameMainlineTests
{
    [Fact]
    public void ExplicitSessionFlowsFromTransparentCandidateToFreshNativeExecutor()
    {
        var snapshot = Snapshot("crane-a");
        var availability = EvaluatePlayerCommand(snapshot);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_crane_game", candidate.Kind);
        AssertParameter(candidate.Parameters, "crane_fee_gold", "500");
        AssertParameter(candidate.Parameters, "crane_attempts", "3");
        AssertParameter(candidate.Parameters, "crane_exit_policy", "finish_three_attempts_then_collect_all_native_rewards");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_crane_game", step.Kind);
        Assert.Contains("native_right_and_down_input_only", step.SafetyConstraints);
        Assert.Contains("no_direct_rng_money_prize_position_state_or_inventory_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_crane_game", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_crane_game", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void PlayerCommandOnlySessionNeverPublishesToAutonomousPolicy()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("minigame.play_crane_game");
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, capability.TrainingExclusionReasons);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("crane-command"), new[] { "minigame.play_crane_game" });
        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        Assert.Contains(option.BlockingReasons, reason => reason.StartsWith("player_command_only_option_requires_player_command", StringComparison.Ordinal));
    }

    [Fact]
    public void OccupiedMachineIsExcludedUpstream()
    {
        var availability = EvaluatePlayerCommand(
            Snapshot("crane-occupied", gateStatus: "blocked_crane_game_occupied", occupied: true));
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("blocked_crane_game_occupied", candidate.BlockReasons);
    }

    [Fact]
    public void MalformedProjectionFingerprintFailsClosedWithoutThrowing()
    {
        var availability = EvaluatePlayerCommand(Snapshot(string.Empty));
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("crane_game_typed_projection_invalid", candidate.BlockReasons);
    }

    [Fact]
    public void FreshCompilerRejectsProjectionDrift()
    {
        var original = Snapshot("crane-a");
        var availability = EvaluatePlayerCommand(original);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("crane-b", money: 9500);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("crane_game_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CapabilityAndRuntimeOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "minigame.play_crane_game", "executor.play_crane_game" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-305" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-305" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-305" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-305" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-305" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CraneGame.cs"));
        var runtimeEntry = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var smoke = File.ReadAllText(Path.Combine(
            root, "scripts", "Invoke-RuntimeCraneGameSmoke.ps1"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.D", runtime, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.S", runtime, StringComparison.Ordinal);
        Assert.Contains("ApplyCraneGameInput(activeCraneGame", runtimeEntry, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"", smoke, StringComparison.Ordinal);
        Assert.Contains("$request.crane_fee_gold = 500", smoke, StringComparison.Ordinal);
        Assert.Contains("$request.crane_attempts = 3", smoke, StringComparison.Ordinal);
        Assert.Contains("evidence_id = \"EVD-305\"", smoke, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"Game1\.player\.Money\s*[+\-]?=(?!=)"), runtime);
        Assert.DoesNotContain("Game1.random", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"(prize|claw)\.position\s*="), runtime);
        Assert.DoesNotContain("SetState(", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        string gateStatus = "ready",
        bool occupied = false,
        int money = 10000)
    {
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"MovieTheater","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money":{"value":{{{money}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "crane_game":{"value":{
              "schema_version":"crane_game.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"{{{fingerprint}}}000000000000000000000000000000000000000000000000000000000",
              "gate_status":"{{{gateStatus}}}","invocation_policy":"player_command_only",
              "location_id":"MovieTheater","is_current_location":true,"machine_occupied":{{{occupied.ToString().ToLowerInvariant()}}},
              "fee_gold":500,"money":{{{money}}},"inventory_empty_slots":12,
              "attempts_per_session":3,"timer_ticks_per_attempt":900,
              "selection_policy":"best_reachable_live_prize_nonlarge_stationary_then_distance;refresh_each_attempt",
              "interaction_tiles":[{"tile_x":2,"tile_y":8,"action_raw":"CraneGame","action_token":"CraneGame"}],
              "native_contract":"MovieTheater_CraneGame_checkAction_then_yes_500g_then_native_CraneGame_directional_input_then_native_ItemGrabMenu_rewards"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"MovieTheater","width":32,"height":24,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
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

    private static OptionAvailabilityEnvelope EvaluatePlayerCommand(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "minigame.play_crane_game",
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true
                }
            },
            includeExecutorCalibrationOptions: true);

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
