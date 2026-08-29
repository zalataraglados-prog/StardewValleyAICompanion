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

public sealed class FishPondManagementMainlineTests
{
    private const string OptionId = "fishing.manage_fish_pond";

    [Fact]
    public void FishPondManagementClosesFiveGatesButRemainsPlayerCommandOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-297" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-297" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-297" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-297" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-297" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void PolicyCannotPublishFishPondManagementAndExplicitCycleUsesNativeMenuPlan()
    {
        var snapshot = Snapshot();
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == OptionId);
        var policyAttempt = Assert.Single(evaluator.Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);
        Assert.False(policyAttempt.Available);
        Assert.Contains("player_command_only_option_requires_player_command_source", policyAttempt.BlockingReasons);

        var option = Assert.Single(EvaluatePlayerCommand(snapshot, "cycle_netting").Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        AssertParameter(candidate.Parameters, "expected_netting_style_before", "2");
        AssertParameter(candidate.Parameters, "expected_netting_style_after", "3");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("manage_fish_pond", planStep.Kind);
        Assert.Contains("not_enabled_for_autonomous_daily_planning", planStep.SafetyConstraints);
        Assert.Contains("native_PondQueryMenu_only", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(OptionId, item.OptionId);
        Assert.Equal("manage_fish_pond", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void EmptyPondRequiresOperationSpecificConfirmationAndCarriesExactClearReceipt()
    {
        var snapshot = Snapshot();
        var withoutConfirmation = Assert.Single(EvaluatePlayerCommand(
            snapshot, "empty_pond", confirmEmptyPond: false).Options);
        Assert.Empty(withoutConfirmation.EventCandidates);

        var candidate = Assert.Single(EvaluatePlayerCommand(
            snapshot, "empty_pond", confirmEmptyPond: true).Options.Single().EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        AssertParameter(candidate.Parameters, "expected_fish_count", "3");
        AssertParameter(candidate.Parameters, "expected_fish_debris_count", "3");
        AssertParameter(candidate.Parameters, "expected_fish_debris_qualified_item_id", "(O)698");
        AssertParameter(candidate.Parameters, "expected_fish_count_after", "0");
        AssertParameter(candidate.Parameters, "expected_maximum_occupants_after", "5");
        AssertParameter(candidate.Parameters, "expected_has_completed_request_after", "1");
        AssertParameter(candidate.Parameters, "expected_golden_animal_cracker_after", "0");
        AssertParameter(candidate.Parameters, "expected_netting_style_after", "2");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains("native_clear_pond_reset_and_preservation_verified=true",
            Assert.Single(item.NormalizedCommand.Steps).ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void CompilerRebindsAllMechanicalStateAndRejectsUnknownPondCoordinate()
    {
        var snapshot = Snapshot();
        var rebound = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("management_operation", "cycle_netting"), P("management_reason", "player requested visual netting change"),
            P("building_tile_x", "10"), P("building_tile_y", "10"),
            P("target_tile_x", "999"), P("stand_tile_x", "999"),
            P("expected_fish_count", "999"), P("expected_netting_style_before", "0"),
            P("expected_netting_style_after", "0"), P("safe_slot_index", "0"),
            P("native_contract", "forged")
        }), snapshot).Items);

        Assert.Empty(rebound.BlockingReasons);
        AssertParameter(rebound.NormalizedCommand.Parameters, "target_tile_x", "10");
        AssertParameter(rebound.NormalizedCommand.Parameters, "stand_tile_x", "9");
        AssertParameter(rebound.NormalizedCommand.Parameters, "expected_fish_count", "3");
        AssertParameter(rebound.NormalizedCommand.Parameters, "expected_netting_style_before", "2");
        AssertParameter(rebound.NormalizedCommand.Parameters, "expected_netting_style_after", "3");
        AssertParameter(rebound.NormalizedCommand.Parameters, "safe_slot_index", "1");
        Assert.Contains(rebound.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "native_contract" && parameter.Value.StartsWith("GameLocation.checkAction", StringComparison.Ordinal));

        var stale = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("management_operation", "cycle_netting"), P("management_reason", "player requested visual netting change"),
            P("building_tile_x", "99"), P("building_tile_y", "99")
        }), snapshot).Items);
        Assert.Contains("fish_pond_management_target_not_found_or_drifted", stale.BlockingReasons);
        Assert.Empty(stale.NormalizedCommand.Steps);
    }

    private static OptionAvailabilityEnvelope EvaluatePlayerCommand(
        SnapshotEnvelope snapshot,
        string operation,
        bool confirmEmptyPond = false) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = new[]
                {
                    P("management_operation", operation),
                    P("management_reason", "explicit player request for exact pond"),
                    P("building_tile_x", "10"),
                    P("building_tile_y", "10"),
                    P("confirm_empty_pond", confirmEmptyPond ? "true" : "false")
                }
            }
        }, includeExecutorCalibrationOptions: true);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = OptionId,
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
        ModelOutputId = "fish-pond-management.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.fish-pond-management",
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
                ActionId = "manage.pond",
                OptionId = OptionId,
                Rationale = "explicit player request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"),
                tile_x = Field(9),
                tile_y = Field(10),
                safe_item_context = Field(new
                {
                    current_tool_index = 2,
                    active_object_selected = false,
                    safe_slot_available = true,
                    safe_slot_index = 1,
                    safe_slot_kind = "empty",
                    policy = "prefer_empty_slot_then_tool_slot"
                })
            },
            farm = new
            {
                farm_identity = Field(new { location_id = "Farm" }),
                buildings = Field(new[]
                {
                    new
                    {
                        type = "Fish Pond",
                        runtime_type = "StardewValley.Buildings.FishPond",
                        tile_x = 10,
                        tile_y = 10,
                        fish_pond = new
                        {
                            status = "exact",
                            runtime_type = "StardewValley.Buildings.FishPond",
                            fish_type_item_id = "698",
                            fish_count = 3,
                            maximum_occupants = 5,
                            last_unlocked_population_gate = 4,
                            days_since_spawn = 7,
                            needed_item_qualified_item_id = "(O)72",
                            needed_item_count = 2,
                            has_completed_request = true,
                            golden_animal_cracker = true,
                            has_spawned_fish = true,
                            sign_qualified_item_id = "(O)698",
                            output_qualified_item_id_before_management = "",
                            netting_style = 2,
                            override_water_color_packed = 4286611711L,
                            preferred_target_tile_x = 10,
                            preferred_target_tile_y = 10,
                            preferred_stand_tile_x = 9,
                            preferred_stand_tile_y = 10,
                            management_status = "ready",
                            management_safe_slot_index = 1,
                            management_restore_slot_index = 2,
                            management_native_contract = "GameLocation.checkAction(right_click)->FishPond.doAction->PondQueryMenu.receiveLeftClick->changeNettingButton|emptyButton->yesButton->FishPond.ClearPond",
                            management_cycle_expected_netting_style_after = 3,
                            management_empty_expected_fish_debris_qualified_item_id = "(O)698",
                            management_empty_expected_fish_debris_count = 3,
                            management_empty_expected_fish_count_after = 0,
                            management_empty_expected_maximum_occupants_after = 5,
                            management_empty_expected_last_unlocked_population_gate_after = 0,
                            management_empty_expected_days_since_spawn_after = 0,
                            management_empty_expected_needed_item_count_after = -1,
                            management_empty_expected_has_completed_request_after = true,
                            management_empty_expected_golden_animal_cracker_after = false,
                            management_empty_expected_has_spawned_fish_after = false,
                            management_empty_expected_netting_style_after = 2
                        }
                    }
                })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
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

    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

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
