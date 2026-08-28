using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class DrumBlockMainlineTests
{
    private const string OptionId = "world.tune_drum_block";

    [Fact]
    public void DrumBlockClosesFiveGatesButRemainsPlayerCommandOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-282" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-282" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-282" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-282" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-282" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("6", 0)]
    public void ExplicitCommandPublishesExactNextNativeTone(string currentRaw, int expectedNext)
    {
        var option = Assert.Single(EvaluatePlayerCommand(Snapshot(currentRaw)).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, p => p.Name == "drum_block_current_tone_raw" && p.Value == currentRaw);
        Assert.Contains(candidate.Parameters, p => p.Name == "drum_block_next_tone" && p.Value == expectedNext.ToString());
        Assert.Contains(candidate.Parameters, p => p.Name == "drum_block_tone_state_count" && p.Value == "7");
        Assert.Contains(candidate.Parameters, p => p.Name == "drum_block_sound_cue" && p.Value == "drumkit" + expectedNext);
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
    public void CompilerRebindsToneTargetStandAndSafeSlotFromFreshSnapshot()
    {
        var snapshot = Snapshot("6", safeSlotKind: "empty");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "20"), P("target_tile_y", "20"),
            P("stand_tile_x", "999"), P("stand_tile_y", "999"),
            P("safe_slot_index", "0"), P("safe_slot_kind", "tool"),
            P("drum_block_current_tone_raw", "0"), P("drum_block_next_tone", "1"),
            P("native_contract", "forged")
        }), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_x" && p.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_y" && p.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "safe_slot_index" && p.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "drum_block_current_tone_raw" && p.Value == "6");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "drum_block_next_tone" && p.Value == "0");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "drum_block_sound_cue" && p.Value == "drumkit0");
        Assert.Equal("tune_drum_block", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void StaleCoordinateFailsClosedInsteadOfSubstitutingAnotherBlock()
    {
        var snapshot = Snapshot("0");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);

        Assert.Contains("drum_block_projection_drifted", item.BlockingReasons);
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
        ModelOutputId = "drum-block.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.drum-block",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "tune.drum-block", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string currentToneRaw, string safeSlotKind = "tool")
    {
        var current = int.TryParse(currentToneRaw, out var parsed) ? parsed : 0;
        var next = (current + 1) % 7;
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
                        tile_x = 20, tile_y = 20, item_id = "463", qualified_item_id = "(O)463", big_craftable = false,
                        name = "Drum Block", type = "StardewValley.Object", object_type = "Crafting",
                        drum_block_tuning = new
                        {
                            status = "ready", canonical_item_id = "463", canonical_qualified_item_id = "(O)463", target_runtime_type = "StardewValley.Object",
                            current_tone_raw = currentToneRaw, current_tone_parsed = current, next_tone = next,
                            tone_min_inclusive = 0, tone_max_inclusive = 6, tone_step = 1, tone_state_count = 7,
                            sound_cue = "drumkit" + next,
                            expected_shake_timer_immediately_after_action = 200, expected_scale_y_immediately_after_action = 1.3f,
                            expected_native_location_action_return = true,
                            stand_tiles = new[] { new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = false, available = true } },
                            has_available_adjacent_stand = true, interaction_kind = "location_object", expected_action_type = "DrumBlock",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(O)463->CheckForActionOnDrumBlock->preservedParentSheetIndex_next_tone->Game1.playSound_drumkitN->shakeTimer_200->scaleY_1.3"
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
