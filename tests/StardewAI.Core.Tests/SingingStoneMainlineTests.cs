using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class SingingStoneMainlineTests
{
    [Fact]
    public void SingingStoneClosesFiveGatesButRemainsPlayerCommandOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("world.play_singing_stone");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-274" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-274" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-274" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-274" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-274" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void OnlyAnExplicitConfirmedPlayerCommandCanPublishTheCandidate()
    {
        var snapshot = Snapshot("tool");
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(
            evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == "world.play_singing_stone");

        var policyAttempt = Assert.Single(evaluator.Evaluate(
            snapshot,
            new[] { "world.play_singing_stone" },
            includeExecutorCalibrationOptions: true).Options);
        Assert.False(policyAttempt.Available);
        Assert.Contains("player_command_only_option_requires_player_command_source", policyAttempt.BlockingReasons);

        var explicitCommand = Assert.Single(EvaluatePlayerCommand(snapshot).Options);
        Assert.Equal("authorized", explicitCommand.ExecutionAuthorization);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, explicitCommand.InvocationPolicy);
    }

    [Fact]
    public void ExplicitCommandCompilesTheCompleteDistributionWithoutGuessingSharedRngState()
    {
        var snapshot = Snapshot("tool");
        var candidate = Assert.Single(EvaluatePlayerCommand(snapshot).Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, p => p.Name == "singing_stone_pitch_min" && p.Value == "0");
        Assert.Contains(candidate.Parameters, p => p.Name == "singing_stone_pitch_max" && p.Value == "2300");
        Assert.Contains(candidate.Parameters, p => p.Name == "singing_stone_pitch_step" && p.Value == "100");
        Assert.Contains(candidate.Parameters, p => p.Name == "singing_stone_pitch_outcome_count" && p.Value == "24");
        Assert.Contains(candidate.Parameters, p =>
            p.Name == "singing_stone_exact_next_pitch_status" &&
            p.Value == "unavailable_shared_rng_state_not_consumed");
        Assert.DoesNotContain(candidate.Parameters, p => p.Name == "singing_stone_exact_next_pitch");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("play_singing_stone", planStep.Kind);
        Assert.Contains("not_enabled_for_autonomous_daily_planning_or_policy_training", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("play_singing_stone", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains("pitch_distribution=uniform_0_2300_step_100", item.NormalizedCommand.Steps[0].ExpectedEffect);
    }

    [Fact]
    public void CompilerRebindsTargetStandSafeSlotIdentityAndDistribution()
    {
        var snapshot = Snapshot("empty");
        var action = Action(snapshot, new[]
        {
            P("target_tile_x", "20"), P("target_tile_y", "20"),
            P("stand_tile_x", "999"), P("stand_tile_y", "999"),
            P("safe_slot_index", "0"), P("safe_slot_kind", "tool"),
            P("qualified_item_id", "(F)1300"),
            P("singing_stone_pitch_max", "9999"),
            P("singing_stone_exact_next_pitch_status", "guessed"),
            P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_x" && p.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_y" && p.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "safe_slot_index" && p.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "safe_slot_kind" && p.Value == "empty");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "restore_slot_index" && p.Value == "2");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "qualified_item_id" && p.Value == "(BC)94");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "singing_stone_pitch_max" && p.Value == "2300");
        Assert.Contains(item.NormalizedCommand.Parameters, p =>
            p.Name == "singing_stone_exact_next_pitch_status" &&
            p.Value == "unavailable_shared_rng_state_not_consumed");
        Assert.Contains(item.NormalizedCommand.Parameters, p =>
            p.Name == "native_contract" && p.Value.StartsWith("GameLocation.checkAction", StringComparison.Ordinal));
    }

    [Fact]
    public void StaleCoordinateFailsClosedInsteadOfSubstitutingAnotherStone()
    {
        var snapshot = Snapshot("empty");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);

        Assert.Contains("singing_stone_projection_drifted", item.BlockingReasons);
        Assert.Empty(item.NormalizedCommand.Steps);
    }

    [Fact]
    public void MissingSafeSlotOrStandBlocksBeforeNativeInteraction()
    {
        var noSlot = Assert.Single(EvaluatePlayerCommand(Snapshot("unavailable")).Options.Single().EventCandidates);
        Assert.False(noSlot.Available);
        Assert.Contains("singing_stone_safe_toolbar_slot_required", noSlot.BlockReasons);

        var noStand = Assert.Single(EvaluatePlayerCommand(Snapshot("empty", standsAvailable: false)).Options.Single().EventCandidates);
        Assert.False(noStand.Available);
        Assert.Contains("singing_stone_no_reachable_adjacent_stand", noStand.BlockReasons);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "world.play_singing_stone",
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static OptionAvailabilityEnvelope EvaluatePlayerCommand(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "world.play_singing_stone",
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true
                }
            },
            includeExecutorCalibrationOptions: true);

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "singing-stone.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.singing-stone",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.main",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "play.singing-stone",
                OptionId = "world.play_singing_stone",
                Rationale = "explicit player request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot(string safeSlotKind, bool standsAvailable = true)
    {
        var safeSlotAvailable = safeSlotKind is "empty" or "tool";
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"), tile_x = Field(18), tile_y = Field(19),
                safe_item_context = Field(new
                {
                    current_tool_index = 2,
                    active_object_selected = true,
                    safe_slot_available = safeSlotAvailable,
                    safe_slot_index = safeSlotAvailable ? 5 : -1,
                    safe_slot_kind = safeSlotKind,
                    policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) },
            current_location = new
            {
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 20, tile_y = 20, item_id = "94", qualified_item_id = "(BC)94",
                        parent_sheet_index = 94, big_craftable = true, name = "Singing Stone",
                        type = "StardewValley.Object", object_type = "Crafting",
                        singing_stone_interaction = new
                        {
                            status = standsAvailable ? "ready" : "blocked_no_adjacent_stand",
                            canonical_item_id = "94", canonical_qualified_item_id = "(BC)94",
                            target_runtime_type = "StardewValley.Object",
                            sound_name = "crystal", pitch_rng_source = "Game1.random_shared_unread",
                            exact_next_pitch_status = "unavailable_shared_rng_state_not_consumed",
                            pitch_min_inclusive = 0, pitch_max_inclusive = 2300, pitch_step = 100,
                            pitch_outcome_count = 24, pitch_distribution = "uniform_over_0_to_2300_step_100",
                            expected_shake_timer_immediately_after_action = 100,
                            expected_native_location_action_return = true,
                            item_id_unchanged = true, qualified_item_id_unchanged = true,
                            stand_tiles = new[]
                            {
                                new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable },
                                new { tile_x = 19, tile_y = 20, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable }
                            },
                            has_available_adjacent_stand = standsAvailable,
                            interaction_kind = "location_object", expected_action_type = "SingingStone",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)94->CheckForActionOnSingingStone->Game1.random.Next(2400)_floor_to_100->Game1.playSound_crystal_pitch->shakeTimer_100"
                        }
                    }
                })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-28T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
