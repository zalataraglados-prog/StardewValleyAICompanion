using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FluteBlockMainlineTests
{
    private const string OptionId = "world.tune_flute_block";

    [Fact]
    public void FluteBlockClosesFiveGatesButRemainsPlayerCommandOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-281" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-281" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-281" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-281" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-281" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Theory]
    [InlineData("0", 100)]
    [InlineData("2300", 2400)]
    [InlineData("2400", 0)]
    public void ExplicitCommandPublishesExactNextNativePitch(string currentRaw, int expectedNext)
    {
        var option = Assert.Single(EvaluatePlayerCommand(Snapshot(currentRaw)).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, p => p.Name == "flute_block_current_pitch_raw" && p.Value == currentRaw);
        Assert.Contains(candidate.Parameters, p => p.Name == "flute_block_next_pitch" && p.Value == expectedNext.ToString());
        Assert.Contains(candidate.Parameters, p => p.Name == "flute_block_pitch_state_count" && p.Value == "25");
        Assert.Contains(candidate.Parameters, p => p.Name == "flute_block_sound_cue" && p.Value == "flute");
    }

    [Fact]
    public void PolicyCannotPublishThePlayerCommandOnlyCandidate()
    {
        var snapshot = Snapshot("0");
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == OptionId);
        var policyAttempt = Assert.Single(evaluator.Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);
        Assert.False(policyAttempt.Available);
        Assert.Contains("player_command_only_option_requires_player_command_source", policyAttempt.BlockingReasons);
    }

    [Fact]
    public void CompilerRebindsPitchTargetStandAndSafeSlotFromFreshSnapshot()
    {
        var snapshot = Snapshot("2300", safeSlotKind: "empty");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "20"), P("target_tile_y", "20"),
            P("stand_tile_x", "999"), P("stand_tile_y", "999"),
            P("safe_slot_index", "0"), P("safe_slot_kind", "tool"),
            P("flute_block_current_pitch_raw", "0"), P("flute_block_next_pitch", "100"),
            P("native_contract", "forged")
        }), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_x" && p.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_y" && p.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "safe_slot_index" && p.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "flute_block_current_pitch_raw" && p.Value == "2300");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "flute_block_next_pitch" && p.Value == "2400");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "flute_block_expected_scale_y" && p.Value == "1.3");
        Assert.Equal("tune_flute_block", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void StaleCoordinateFailsClosedInsteadOfSubstitutingAnotherBlock()
    {
        var snapshot = Snapshot("0");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);

        Assert.Contains("flute_block_projection_drifted", item.BlockingReasons);
        Assert.Empty(item.NormalizedCommand.Steps);
    }

    private static OptionAvailabilityEnvelope EvaluatePlayerCommand(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true
            }
        }, includeExecutorCalibrationOptions: true);

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "flute-block.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.flute-block",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "tune.flute-block", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string currentPitchRaw, string safeSlotKind = "tool")
    {
        var current = int.TryParse(currentPitchRaw, out var parsed) ? parsed : 0;
        var next = current switch { 2300 => 2400, 2400 => 0, _ => (current + 100) % 2400 };
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"), tile_x = Field(18), tile_y = Field(19),
                safe_item_context = Field(new { current_tool_index = 2, active_object_selected = true, safe_slot_available = true, safe_slot_index = 5, safe_slot_kind = safeSlotKind, policy = "prefer_empty_slot_then_tool_slot" })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) },
            current_location = new
            {
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 20, tile_y = 20, item_id = "464", qualified_item_id = "(O)464", big_craftable = false,
                        name = "Flute Block", type = "StardewValley.Object", object_type = "Crafting",
                        flute_block_tuning = new
                        {
                            status = "ready", canonical_item_id = "464", canonical_qualified_item_id = "(O)464", target_runtime_type = "StardewValley.Object",
                            current_pitch_raw = currentPitchRaw, current_pitch_parsed = current, next_pitch = next,
                            pitch_min_inclusive = 0, pitch_max_inclusive = 2400, pitch_step = 100, pitch_state_count = 25,
                            sound_cue = "flute", held_object_sound_override_disabled_by_safe_slot = true,
                            expected_shake_timer_immediately_after_action = 200, expected_scale_y_immediately_after_action = 1.3f,
                            expected_native_location_action_return = true,
                            stand_tiles = new[] { new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = false, available = true } },
                            has_available_adjacent_stand = true, interaction_kind = "location_object", expected_action_type = "FluteBlock",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(O)464->CheckForActionOnFluteBlock->preservedParentSheetIndex_next_pitch->Game1.playSound_flute_pitch->shakeTimer_200->scaleY_1.3"
                        }
                    }
                })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope { StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1, RealTimestamp = "2026-08-29T00:00:00Z", Completeness = "complete", State = state };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
