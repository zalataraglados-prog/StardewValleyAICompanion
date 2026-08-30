using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PrairieKingMainlineTests
{
    [Fact]
    public void MissingNoDeathCompletionFlowsThroughTimedEquivalentCompiler()
    {
        var snapshot = Snapshot("prairie-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "minigame.play_prairie_king" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("play_prairie_king", candidate.Kind);
        Assert.Equal(108000, candidate.EstimatedTicks);
        AssertParameter(candidate.Parameters, "prairie_king_completion_goal", "complete_without_dying");
        AssertParameter(candidate.Parameters, "prairie_king_equivalent_duration_ticks", "108000");
        AssertParameter(candidate.Parameters, "prairie_king_equivalent_acceleration", "60");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("play_prairie_king", step.Kind);
        Assert.Equal(30, step.EstimatedMinutes);
        Assert.Contains("ai_actor_only_timed_equivalent", step.SafetyConstraints);
        Assert.Contains("native_perfect_proxy_is_post_training_player_command_only", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.play_prairie_king", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_prairie_king", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompletedNoDeathObjectiveIsRemovedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("prairie-complete", completed: 1, completedWithoutDying: 1,
                gateStatus: "complete_prairie_king_without_dying"),
                new[] { "minigame.play_prairie_king" });

        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsProjectionDrift()
    {
        var original = Snapshot("prairie-a");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "minigame.play_prairie_king" });
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("prairie-b", completed: 1);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("prairie_king_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void EquivalentRuntimeAndFutureNativeProxyRemainSeparate()
    {
        foreach (var optionId in new[] { "minigame.play_prairie_king", "executor.play_prairie_king" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.True(capability.HarnessDispatchSupported || !optionId.StartsWith("executor.", StringComparison.Ordinal));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
            Assert.Equal(new[] { "EVD-307" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-307" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-307" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-307" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-307" }, capability.OutputEvidenceIds);
        }
        Assert.Contains("minigame.play_prairie_king", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("executor.play_prairie_king", OptionCapabilityRegistrySource.TrainingAllowlist);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.PrairieKing.cs"));
        var adapter = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.PrairieKing.cs"));
        var junimo = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.JunimoKart.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimePrairieKingSmoke.ps1"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("active.Game!.usePowerup(-3)", runtime, StringComparison.Ordinal);
        Assert.Contains("PrimitiveVerificationStatus = \"simulated_equivalent\"", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("stats.Increment", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AddMissedMailAndRecipes", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldPressPrairieKing", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("OverrideButton", runtime, StringComparison.Ordinal);
        Assert.Contains("AbigailGame.startTimer = int.MaxValue", runtime, StringComparison.Ordinal);
        Assert.Contains("not_native_perfect_proxy_play", runtime, StringComparison.Ordinal);
        Assert.Contains("post_core_training_player_command_only", adapter, StringComparison.Ordinal);
        Assert.Contains("coop_companion", junimo, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_ai", junimo, StringComparison.Ordinal);
        Assert.DoesNotContain("timed_equivalent_training_singleplayer_only", junimo, StringComparison.Ordinal);
        Assert.Contains("evidence_id = \"EVD-307\"", smoke, StringComparison.Ordinal);
        Assert.Contains("primitive_verification_status -ne \"simulated_equivalent\"", smoke, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string fingerprint,
        long completed = 0,
        long completedWithoutDying = 0,
        string gateStatus = "ready")
    {
        var hash = fingerprint.PadRight(64, '0')[..64];
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Saloon","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":14,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "prairie_king":{"value":{
              "schema_version":"prairie_king.v1","projection_status":"complete_locked_base_1.6.15",
              "projection_fingerprint":"{{{hash}}}","gate_status":"{{{gateStatus}}}",
              "invocation_policy":"autonomous_timed_equivalent",
              "native_proxy_policy":"post_core_training_player_command_only",
              "location_id":"Saloon","completed_before":{{{completed}}},
              "completed_without_dying_before":{{{completedWithoutDying}}},
              "completion_goal":"complete_without_dying","dialogue_key":"none","dialogue_response_key":"none",
              "equivalent_duration_ticks":108000,"equivalent_acceleration":60,
              "native_completion_trigger":"AbigailGame.usePowerup(-3)",
              "interaction_tiles":[{"tile_x":14,"tile_y":4,"action_raw":"Arcade_Prairie","action_token":"Arcade_Prairie"}],
              "equivalent_contract":"Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Saloon","width":40,"height":32,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
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
