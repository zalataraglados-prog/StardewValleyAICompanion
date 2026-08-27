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

public sealed class HousePlantRotationMainlineTests
{
    [Fact]
    public void HousePlantRotationClosesFiveGatesWithoutEnteringAutonomousDailyPlanning()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("world.rotate_house_plant");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-271" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-271" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-271" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-271" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-271" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void HousePlantRotationRequiresAnExplicitPlayerCommandAndNeverEntersDefaultCandidates()
    {
        var snapshot = Snapshot(3, safeSlotKind: "empty");
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(
            evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == "world.rotate_house_plant");

        var policyAttempt = Assert.Single(evaluator.Evaluate(
            snapshot,
            new[] { "world.rotate_house_plant" },
            includeExecutorCalibrationOptions: true).Options);
        Assert.False(policyAttempt.Available);
        Assert.Contains("player_command_only_option_requires_player_command_source", policyAttempt.BlockingReasons);

        var explicitCommand = Assert.Single(EvaluatePlayerCommand(snapshot).Options);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, explicitCommand.InvocationPolicy);
        Assert.Equal("authorized", explicitCommand.ExecutionAuthorization);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(2, 3, 1)]
    [InlineData(3, 4, 1)]
    [InlineData(4, 5, 1)]
    [InlineData(5, 6, 1)]
    [InlineData(6, 7, 1)]
    [InlineData(7, 1, 2)]
    public void AllEightVisualFramesCompileToTheExactNativeLocationResult(
        int currentSprite,
        int expectedSprite,
        int expectedObjectCalls)
    {
        var snapshot = Snapshot(currentSprite, safeSlotKind: "empty");
        var candidate = Assert.Single(EvaluatePlayerCommand(snapshot).Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "house_plant_expected_sprite_index" && parameter.Value == expectedSprite.ToString());
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "house_plant_expected_object_action_calls" && parameter.Value == expectedObjectCalls.ToString());
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "5");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("rotate_house_plant", planStep.Kind);
        Assert.Contains("not_enabled_for_autonomous_daily_planning", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("rotate_house_plant", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "house_plant_expected_sprite_index" && parameter.Value == expectedSprite.ToString());
        Assert.Contains("parent_sheet_index=" + expectedSprite, item.NormalizedCommand.Steps[0].ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void CompilerRebindsMechanicalFrameStandHandAndIdentityForTheSelectedPlant()
    {
        var snapshot = Snapshot(7, safeSlotKind: "empty");
        var action = Action(snapshot, new[]
        {
            P("target_tile_x", "20"),
            P("target_tile_y", "20"),
            P("stand_tile_x", "999"),
            P("house_plant_current_sprite_index", "0"),
            P("house_plant_expected_sprite_index", "0"),
            P("safe_slot_index", "0"),
            P("qualified_item_id", "forged"),
            P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "house_plant_current_sprite_index" && parameter.Value == "7");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "house_plant_expected_sprite_index" && parameter.Value == "1");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "house_plant_expected_object_action_calls" && parameter.Value == "2");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "restore_slot_index" && parameter.Value == "2");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == "(BC)0");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "native_contract" && parameter.Value.StartsWith("GameLocation.checkAction", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownOrStalePlantCoordinateFailsClosedInsteadOfChoosingAnotherDecoration()
    {
        var snapshot = Snapshot(3, safeSlotKind: "empty");
        var action = Action(snapshot, new[] { P("target_tile_x", "99"), P("target_tile_y", "99") });
        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);

        Assert.Contains("house_plant_projection_drifted", item.BlockingReasons);
        Assert.Empty(item.NormalizedCommand.Steps);
    }

    [Fact]
    public void ToolFallbackIsNotAcceptedBecauseTheNativeDoubleCallRequiresKnownEmptyHandState()
    {
        var snapshot = Snapshot(7, safeSlotKind: "tool");
        var candidate = Assert.Single(EvaluatePlayerCommand(snapshot).Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("house_plant_empty_toolbar_slot_required", candidate.BlockReasons);
    }

    [Fact]
    public void FourCardinalObjectTrapStandIsExcludedBeforeNativeInteraction()
    {
        var snapshot = Snapshot(3, safeSlotKind: "empty", standsAvailable: false);
        var candidate = Assert.Single(EvaluatePlayerCommand(snapshot).Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("house_plant_no_reachable_adjacent_stand", candidate.BlockReasons);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "world.rotate_house_plant",
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
                    OptionId = "world.rotate_house_plant",
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true
                }
            },
            includeExecutorCalibrationOptions: true);

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "house-plant.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.decoration",
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
                ActionId = "rotate.plant",
                OptionId = "world.rotate_house_plant",
                Rationale = "explicit decoration request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot(int currentSprite, string safeSlotKind, bool standsAvailable = true)
    {
        var expectedSprite = currentSprite == 7 ? 1 : currentSprite + 1;
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"),
                tile_x = Field(18),
                tile_y = Field(19),
                safe_item_context = Field(new
                {
                    current_tool_index = 2,
                    active_object_selected = true,
                    safe_slot_available = true,
                    safe_slot_index = 5,
                    safe_slot_kind = safeSlotKind,
                    policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            menus = new
            {
                active_menu = Field(new { is_open = false, type = "none" })
            },
            current_location = new
            {
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 20,
                        tile_y = 20,
                        item_id = "0",
                        qualified_item_id = "(BC)0",
                        parent_sheet_index = currentSprite,
                        big_craftable = true,
                        name = "House Plant",
                        type = "StardewValley.Object",
                        object_type = "Crafting",
                        house_plant_rotation = new
                        {
                            status = "ready",
                            canonical_item_id = "0",
                            canonical_qualified_item_id = "(BC)0",
                            current_sprite_index = currentSprite,
                            expected_sprite_index_after_native_location_action = expectedSprite,
                            expected_object_check_for_action_call_count = currentSprite == 7 ? 2 : 1,
                            expected_native_location_action_return = true,
                            item_id_unchanged = true,
                            qualified_item_id_unchanged = true,
                            target_runtime_type = "StardewValley.Object",
                            stand_tiles = new[]
                            {
                                new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable },
                                new { tile_x = 19, tile_y = 20, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable }
                            },
                            has_available_adjacent_stand = true,
                            interaction_kind = "location_object",
                            expected_action_type = "HousePlant",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)0..7->CheckForActionOnHousePlant;empty_hand;location_calls_object_twice_only_when_first_returns_false"
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
            RealTimestamp = "2026-08-27T00:00:00Z",
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
