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

public sealed class SlimeBallCollectionMainlineTests
{
    [Fact]
    public void SlimeBallIsARegisteredAutonomousNativeAction()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("farming.collect_slime_ball");

        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PlayerConfirmationRequired);
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-272" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-272" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-272" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-272" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-272" }, declaration.OutputEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void ExactSeededProjectionCompilesThroughDailyPlanAndQueue()
    {
        var snapshot = Snapshot(standsAvailable: true, safeSlotKind: "empty");
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "farming.collect_slime_ball" }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "slime_ball_seed_unique_game_id" && parameter.Value == "123456789");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "slime_ball_expected_slime_quantity" && parameter.Value == "17");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "slime_ball_expected_petrified_slime_quantity" && parameter.Value == "2");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        Assert.Equal("collect_slime_ball", Assert.Single(plan.Steps).Kind);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Equal("collect_slime_ball", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains("conserved_output[(O)766]+=17", item.NormalizedCommand.Steps[0].ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "5");
    }

    [Fact]
    public void CompilerRebindsSeedOutputsStandIdentityAndEmptySlot()
    {
        var snapshot = Snapshot(standsAvailable: true, safeSlotKind: "empty");
        var action = Action(snapshot, new[]
        {
            P("target_tile_x", "20"), P("target_tile_y", "20"),
            P("stand_tile_x", "999"), P("required_fragility", "0"),
            P("slime_ball_seed_unique_game_id", "0"),
            P("slime_ball_expected_slime_quantity", "0"), P("safe_slot_index", "0"),
            P("qualified_item_id", "forged"), P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "20");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "19");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "required_fragility" && parameter.Value == "2");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "slime_ball_seed_unique_game_id" && parameter.Value == "123456789");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "slime_ball_expected_slime_quantity" && parameter.Value == "17");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == "(BC)56");
    }

    [Fact]
    public void StaleExactCoordinateFailsClosedInsteadOfSelectingAnotherBall()
    {
        var snapshot = Snapshot(standsAvailable: true, safeSlotKind: "empty");
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);

        Assert.Contains("slime_ball_projection_drifted", item.BlockingReasons);
        Assert.Empty(item.NormalizedCommand.Steps);
    }

    [Theory]
    [InlineData(false, "empty", "slime_ball_no_reachable_adjacent_stand")]
    [InlineData(true, "tool", "slime_ball_empty_toolbar_slot_required")]
    public void UnsafeStandOrNonemptyHandContextIsRejectedUpstream(
        bool standsAvailable,
        string safeSlotKind,
        string expectedReason)
    {
        var snapshot = Snapshot(standsAvailable, safeSlotKind);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "farming.collect_slime_ball" }, includeExecutorCalibrationOptions: true)
            .Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(expectedReason, candidate.BlockReasons);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "farming.collect_slime_ball",
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "slime-ball.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.collect.slime-ball",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "collect.slime-ball",
                OptionId = "farming.collect_slime_ball",
                Rationale = "collect current deterministic Slime Hutch output",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot(bool standsAvailable, string safeSlotKind)
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("SlimeHutch"), tile_x = Field(18), tile_y = Field(19),
                inventory = Field(Array.Empty<object>()),
                safe_item_context = Field(new
                {
                    current_tool_index = 2, active_object_selected = true, safe_slot_available = true,
                    safe_slot_index = 5, safe_slot_kind = safeSlotKind, policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) },
            current_location = new
            {
                debris = Field(Array.Empty<object>()),
                objects = Field(new[]
                {
                    new
                    {
                        tile_x = 20, tile_y = 20, item_id = "56", qualified_item_id = "(BC)56",
                        parent_sheet_index = 56, big_craftable = true, fragility = 2, name = "Slime Ball",
                        type = "StardewValley.Object", object_type = "Crafting",
                        slime_ball_collection = new
                        {
                            status = standsAvailable ? "ready" : "blocked_no_adjacent_stand",
                            source_kind = "natural_slime_hutch_day_update_output",
                            target_runtime_type = "StardewValley.Object",
                            canonical_item_id = "56", canonical_qualified_item_id = "(BC)56", required_fragility = 2,
                            day_seed_days_played = 140, day_seed_unique_game_id = 123456789L,
                            expected_slime_quantity = 17, expected_petrified_slime_quantity = 2,
                            expected_native_location_action_return = true,
                            stand_tiles = new[]
                            {
                                new { tile_x = 20, tile_y = 19, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable },
                                new { tile_x = 19, tile_y = 20, on_map = true, collision_blocked = false, object_trap_blocked = !standsAvailable, available = standsAvailable }
                            },
                            interaction_kind = "location_object", expected_action_type = "SlimeBall",
                            native_contract = "GameLocation.checkAction->Object.checkForAction_(BC)56->CheckForActionOnSlimeBall->remove_object->seeded_(O)766_debris_10_20->seeded_geometric_(O)557_debris"
                        }
                    }
                })
            }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-27T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
