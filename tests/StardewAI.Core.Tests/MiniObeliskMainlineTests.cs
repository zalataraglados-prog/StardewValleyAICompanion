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

public sealed class MiniObeliskMainlineTests
{
    [Fact]
    public void MiniObeliskClosesFiveGatesButStaysOutOfStrategyTraining()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("movement.use_mini_obelisk");

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-279" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-279" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-279" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-279" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-279" }, declaration.OutputEvidenceIds);
        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.False(declaration.PolicyTrainingCandidate);
        Assert.DoesNotContain(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(declaration.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(declaration.OptionId, out _));
    }

    [Fact]
    public void CalibrationCandidateCompilesNativePairDestinationAndLanding()
    {
        var snapshot = Snapshot();
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(
            evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == "movement.use_mini_obelisk");

        var option = Assert.Single(evaluator.Evaluate(
            snapshot,
            new[] { "movement.use_mini_obelisk" },
            includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.TileX == 10));
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, p => p.Name == "mini_obelisk_pair_first_tile_x" && p.Value == "10");
        Assert.Contains(candidate.Parameters, p => p.Name == "mini_obelisk_destination_tile_x" && p.Value == "30");
        Assert.Contains(candidate.Parameters, p => p.Name == "mini_obelisk_landing_tile_y" && p.Value == "31");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("use_mini_obelisk", planStep.Kind);
        Assert.Contains("never_directly_mutate_player_position_in_production", planStep.SafetyConstraints);

        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("use_mini_obelisk", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
        Assert.Contains("player.tile=30,31", queueItem.NormalizedCommand.Steps[0].ExpectedEffect);
    }

    [Fact]
    public void CompilerRebindsForgedPairAndLandingFromFreshProjection()
    {
        var snapshot = Snapshot();
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "10"), P("target_tile_y", "10"),
            P("stand_tile_x", "999"), P("stand_tile_y", "999"),
            P("mini_obelisk_pair_first_tile_x", "999"),
            P("mini_obelisk_destination_tile_x", "999"),
            P("mini_obelisk_landing_tile_y", "999"),
            P("native_contract", "forged")
        }), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_x" && p.Value == "10");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "stand_tile_y" && p.Value == "9");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "mini_obelisk_pair_first_tile_x" && p.Value == "10");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "mini_obelisk_destination_tile_x" && p.Value == "30");
        Assert.Contains(item.NormalizedCommand.Parameters, p => p.Name == "mini_obelisk_landing_tile_y" && p.Value == "31");
        Assert.Contains(item.NormalizedCommand.Parameters, p =>
            p.Name == "native_contract" && p.Value.StartsWith("GameLocation.checkAction", StringComparison.Ordinal));
    }

    [Fact]
    public void StaleSourceFailsClosedAndRuntimeNeverWritesPlayerPositionDirectly()
    {
        var snapshot = Snapshot();
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("target_tile_x", "99"), P("target_tile_y", "99")
        }), snapshot).Items);
        Assert.Contains("mini_obelisk_pair_destination_or_landing_projection_drifted", item.BlockingReasons);
        Assert.Empty(item.NormalizedCommand.Steps);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MiniObelisk.cs"));
        Assert.Contains("AdvanceNativeObjectInteractionMovement(active, \"mini_obelisk\"", source, StringComparison.Ordinal);
        Assert.Contains("active.Location.checkAction(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("setTileLocation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", source, StringComparison.Ordinal);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = "movement.use_mini_obelisk",
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "mini-obelisk.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.executor-calibration.mini-obelisk",
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
                ActionId = "use.mini-obelisk",
                OptionId = "movement.use_mini_obelisk",
                Rationale = "executor calibration",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var contract =
            "GameLocation.checkAction->Object.checkForAction_(BC)238->CheckForActionOnMiniObelisk;native_first_two_nonzero_pair;farther_from_interaction_stand;landing_order_down_left_right_up;IsTileBlockedBy_All_ignorePassables_All;fade_delay_50ms";
        object Projection(int member, int sourceX, int sourceY, int standX, int standY,
            int destinationX, int destinationY, int landingX, int landingY) => new
        {
            status = "ready",
            canonical_item_id = "238",
            canonical_qualified_item_id = "(BC)238",
            target_runtime_type = "StardewValley.Object",
            native_pair_member_index = member,
            native_pair_first_tile_x = 10,
            native_pair_first_tile_y = 10,
            native_pair_second_tile_x = 30,
            native_pair_second_tile_y = 30,
            native_pair_exact_base = true,
            expected_native_location_action_return = true,
            expected_delay_milliseconds = 50,
            stand_tiles = new[]
            {
                new
                {
                    tile_x = standX, tile_y = standY, on_map = true,
                    collision_blocked = false, object_trap_blocked = false,
                    native_destination_is_other_endpoint = true,
                    native_destination_tile_x = destinationX,
                    native_destination_tile_y = destinationY,
                    native_landing_tile_x = landingX,
                    native_landing_tile_y = landingY,
                    native_landing_available = true,
                    available = true
                }
            },
            has_available_source_stand = true,
            interaction_kind = "location_object",
            expected_action_type = "MiniObelisk",
            native_contract = contract
        };

        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"), tile_x = Field(9), tile_y = Field(9),
                safe_item_context = Field(new
                {
                    current_tool_index = 2,
                    active_object_selected = false,
                    safe_slot_available = true,
                    safe_slot_index = 5,
                    safe_slot_kind = "empty",
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
                        tile_x = 10, tile_y = 10, item_id = "238", qualified_item_id = "(BC)238",
                        parent_sheet_index = 238, big_craftable = true, name = "Mini-Obelisk",
                        type = "StardewValley.Object", object_type = "Crafting",
                        mini_obelisk_use = Projection(0, 10, 10, 10, 9, 30, 30, 30, 31)
                    },
                    new
                    {
                        tile_x = 30, tile_y = 30, item_id = "238", qualified_item_id = "(BC)238",
                        parent_sheet_index = 238, big_craftable = true, name = "Mini-Obelisk",
                        type = "StardewValley.Object", object_type = "Crafting",
                        mini_obelisk_use = Projection(1, 30, 30, 30, 29, 10, 10, 10, 11)
                    }
                })
            }
        });
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

    private static SmallModelActionParameter P(string name, string value) =>
        new() { Name = name, Value = value };

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate StardewAI repository root.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
