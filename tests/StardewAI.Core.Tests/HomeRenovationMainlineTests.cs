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

public sealed class HomeRenovationMainlineTests
{
    private const string OptionId = "housing.renovate";

    [Theory]
    [InlineData(OptionId)]
    [InlineData("executor.renovate_home")]
    public void HomeRenovationClosesFiveGatesButRemainsPlayerCommandOnly(string optionId)
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-301" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-301" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-301" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-301" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-301" }, declaration.OutputEvidenceIds);
        Assert.False(declaration.AutonomousCandidateEnabled);
        Assert.True(declaration.PlayerConfirmationRequired);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, declaration.TrainingExclusionReasons);
        Assert.DoesNotContain(optionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
    }

    [Fact]
    public void ExplicitDestructiveCommandCompilesThroughDailyPlanAndPolicyCannotPublishIt()
    {
        var snapshot = Snapshot();
        var evaluator = new CandidateOptionAvailabilityEvaluator();

        Assert.DoesNotContain(evaluator.Evaluate(snapshot, Array.Empty<string>()).Options,
            option => option.OptionId == OptionId);
        var policyAttempt = Assert.Single(evaluator.Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);
        Assert.False(policyAttempt.Available);
        Assert.Contains("player_command_only_option_requires_player_command_source", policyAttempt.BlockingReasons);

        var missingDestructiveConfirmation = Assert.Single(EvaluatePlayerCommand(snapshot, false).Options);
        Assert.Empty(missingDestructiveConfirmation.EventCandidates);

        var option = Assert.Single(EvaluatePlayerCommand(snapshot, true).Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        AssertParameter(candidate.Parameters, "renovation_id", "remove_crib");
        AssertParameter(candidate.Parameters, "stand_tile_x", "10");
        AssertParameter(candidate.Parameters, "stand_tile_y", "11");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("renovate_home", planStep.Kind);
        Assert.Contains("explicit_player_command_and_operation_confirmation_only", planStep.SafetyConstraints);
        Assert.Contains("native_Carpenter_Renovate_HouseRenovations_RenovateMenu_only", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.renovate_home", item.OptionId);
        Assert.Equal("renovate_home", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerFreshlyRebindsAllMechanicalStateAndDerivesReachableStand()
    {
        var snapshot = Snapshot();
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("renovation_id", "remove_crib"),
            P("selected_index", "0"),
            P("renovation_reason", "explicit test request"),
            P("confirm_renovation", "true"),
            P("confirm_destructive", "true"),
            P("stand_tile_x", "999"),
            P("stand_tile_y", "999"),
            P("expected_money_before", "1"),
            P("native_shop_index", "99"),
            P("requirements_json", "forged"),
            P("native_contract", "forged")
        }), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "stand_tile_x", "10");
        AssertParameter(item.NormalizedCommand.Parameters, "stand_tile_y", "11");
        AssertParameter(item.NormalizedCommand.Parameters, "expected_money_before", "1000000");
        AssertParameter(item.NormalizedCommand.Parameters, "native_shop_index", "0");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "requirements_json" && parameter.Value.Contains("cribStyle", StringComparison.Ordinal));
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "native_contract" && parameter.Value.StartsWith("GameLocation.checkAction", StringComparison.Ordinal));

        var stale = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, new[]
        {
            P("renovation_id", "unknown"),
            P("selected_index", "0"),
            P("renovation_reason", "explicit test request"),
            P("confirm_renovation", "true")
        }), snapshot).Items);
        Assert.Contains("home_renovation_target_not_found_or_projection_drifted", stale.BlockingReasons);
        Assert.Empty(stale.NormalizedCommand.Steps);
    }

    [Fact]
    public void RemotePlayerCommandCarriesExactRenovationAcrossTheRouteConnector()
    {
        var snapshot = Snapshot("FarmHouse", includeServiceRoute: true);
        var option = Assert.Single(EvaluatePlayerCommand(snapshot, true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("FarmHouse", candidate.LocationId);
        AssertParameter(candidate.Parameters, "continuation.option_id", OptionId);
        AssertParameter(candidate.Parameters, "continuation.renovation_id", "remove_crib");
        AssertParameter(candidate.Parameters, "continuation.selected_index", "0");
        AssertParameter(candidate.Parameters, "continuation.renovation_reason", "explicit test request");
        AssertParameter(candidate.Parameters, "continuation.confirm_renovation", "true");
        AssertParameter(candidate.Parameters, "continuation.confirm_destructive", "true");

        var resumed = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot(),
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = OptionId,
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true,
                    Parameters = candidate.Parameters
                }
            },
            includeExecutorCalibrationOptions: true).Options);
        var terminalCandidate = Assert.Single(resumed.EventCandidates);
        Assert.True(terminalCandidate.Available, string.Join(";", terminalCandidate.BlockReasons));
        Assert.Equal("renovate_home", terminalCandidate.Kind);
        AssertParameter(terminalCandidate.Parameters, "renovation_id", "remove_crib");
        AssertParameter(terminalCandidate.Parameters, "selected_index", "0");
    }

    private static OptionAvailabilityEnvelope EvaluatePlayerCommand(SnapshotEnvelope snapshot, bool confirmDestructive) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = new[]
                {
                    P("renovation_id", "remove_crib"),
                    P("selected_index", "0"),
                    P("renovation_reason", "explicit test request"),
                    P("confirm_renovation", "true"),
                    P("confirm_destructive", confirmDestructive ? "true" : "false")
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
        ModelOutputId = "home-renovation.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.home-renovation",
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
                ActionId = "renovate.home",
                OptionId = OptionId,
                Rationale = "explicit player request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string playerLocation = "ScienceHouse",
        bool includeServiceRoute = false)
    {
        var nativeContract = "GameLocation.checkAction Carpenter -> answerDialogue Renovate -> ShopMenu HouseRenovations exact row -> RenovateMenu hover and world-region click -> native validate, money/FirstPurchase, renovation actions, UpdateForRenovation, renovateEvent, animation and return; no direct money, mail, NetInt, map, furniture, menu, viewport or event mutation";
        var option = new
        {
            renovation_id = "remove_crib",
            display_name = "Remove Crib",
            price = 0,
            room_id = "remove_crib",
            animation_type = "destroy",
            is_destructive = true,
            check_for_obstructions = false,
            requirements = new[] { new { type = "Value", key = "cribStyle", value_expression = "!0", current_int_value = (int?)1, current_bool_value = (bool?)null, satisfied = true, projection_status = "exact" } },
            renovate_actions = new[] { new { type = "Value", key = "cribStyle", value_expression = "0", current_int_value = (int?)1, current_bool_value = (bool?)null, satisfied = true, projection_status = "exact" } },
            regions = new[] { new { selected_index = 0, rectangles = new[] { new { x = 30, y = 12, width = 3, height = 4 } }, obstruction_status = "native_obstruction_check_not_required" } },
            requirements_satisfied = true,
            native_menu_available = true,
            native_shop_index = (int?)0,
            first_purchase_mail_id = "FirstPurchase_remove_crib",
            first_purchase_mail_before = false,
            expected_first_purchase_mail_after = true,
            money_before = 1_000_000,
            expected_money_after = 1_000_000,
            refund_eligible = false,
            availability_status = "available_in_native_renovation_shop",
            availability_block_reasons = Array.Empty<string>(),
            projection_fingerprint = "test-remove-crib-fingerprint"
        };
        var routeEdges = includeServiceRoute
            ? new object[]
            {
                new
                {
                    kind = "warp",
                    from_location = "FarmHouse",
                    from_x = 2,
                    from_y = 3,
                    target_location = "ScienceHouse",
                    target_x = 8,
                    target_y = 20,
                    resolved = true
                }
            }
            : Array.Empty<object>();
        var routeConnectors = includeServiceRoute
            ? new object[]
            {
                new
                {
                    kind = "warp",
                    tile_x = 2,
                    tile_y = 3,
                    target_location = "ScienceHouse",
                    target_x = 8,
                    target_y = 20,
                    resolved = true
                }
            }
            : Array.Empty<object>();
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field(playerLocation),
                tile_x = Field(includeServiceRoute ? 1 : 10),
                tile_y = Field(includeServiceRoute ? 1 : 11),
                energy = Field(270),
                money = Field(1_000_000)
            },
            time = new { time = Field(900) },
            world_progress = new
            {
                marriage_house = Field(new
                {
                    home_renovations = new
                    {
                        projection_status = "complete_live_native_home_renovation_catalog",
                        data_payload_sha256 = "26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b",
                        data_contract_status = "exact_locked_base_1.6.15",
                        home_location_id = "FarmHouse",
                        home_runtime_type = "StardewValley.Locations.FarmHouse",
                        house_upgrade_level = 2,
                        service_location_id = "ScienceHouse",
                        service_action_raw = "Carpenter",
                        service_action_tile_x = (int?)10,
                        service_action_tile_y = (int?)10,
                        robin_present_at_service = true,
                        service_status = includeServiceRoute ? "route_to_carpenter_service_required" : "ready",
                        native_available_renovation_ids = new[] { "remove_crib" },
                        options = new[] { option },
                        native_contract = nativeContract
                    }
                })
            },
            locations = new
            {
                route_graph = Field(new { status = "complete", edges = routeEdges }),
                route_connectors = Field(new { location_id = playerLocation, connectors = routeConnectors }),
                route_action_branch_coverage = Field(new { rows = Array.Empty<object>() }),
                collision_grid = Field(new
                {
                    location_id = playerLocation,
                    width = 32,
                    height = 25,
                    notable_tiles = Array.Empty<object>()
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
