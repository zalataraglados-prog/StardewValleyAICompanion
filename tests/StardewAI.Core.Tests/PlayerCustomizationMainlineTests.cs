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

public sealed class PlayerCustomizationMainlineTests
{
    private const string OptionId = "player.customize";
    private const string NativeContract =
        "wizard_shrine:shared_route->WizardShrine_checkAction->answerDialogue(Yes)->CharacterCustomization(Source.Wizard)_native_controls->OK;desert_makeover:shared_route->walk_onto_DesertMakeover_TouchAction->native_skippable_Event->onEventFinished_ReceiveMakeOver";

    [Fact]
    public void WizardExactTargetReachesOneTypedNativeQueue()
    {
        var snapshot = Snapshot("WizardHouseBasement");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, WizardIntent()).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("customize_player", candidate.Kind);
        AssertParameter(candidate.Parameters, "customization_skin_index", "3");
        AssertParameter(candidate.Parameters, "customization_name", "Test Farmer");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Contains("player_command_only_and_excluded_from_strategy_training", step.SafetyConstraints);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.customize_player", item.OptionId);
        Assert.Equal("customize_player", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "customization_price_gold", "500");
        AssertParameter(item.NormalizedCommand.Parameters, "customization_money_before", "1000");
    }

    [Fact]
    public void DesertProjectedOutfitReachesSameTypedPrimitive()
    {
        var snapshot = Snapshot("DesertFestival");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, DesertIntent()).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        AssertParameter(candidate.Parameters, "customization_expected_hat_qid", "(H)42");
        AssertParameter(candidate.Parameters, "customization_expected_shirt_qid", "(S)1199");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.customize_player", item.OptionId);
        AssertParameter(item.NormalizedCommand.Parameters, "customization_expected_outfit_index", "7");
        AssertParameter(item.NormalizedCommand.Parameters, "customization_stylist_name", "Emily");
    }

    [Fact]
    public void MissingConfirmationNoChangeOrUnavailableFestivalIsExcludedUpstream()
    {
        var noChange = new[]
        {
            P("customization_mode", "wizard_shrine"), P("customization_reason", "explicit request"),
            P("confirm_customization", "true"), P("customization_skin_index", "2")
        };
        Assert.Empty(Assert.Single(Evaluate(Snapshot("WizardHouseBasement"), noChange).Options).EventCandidates);
        var unconfirmed = WizardIntent().Where(parameter => parameter.Name != "confirm_customization")
            .Concat(new[] { P("confirm_customization", "false") }).ToArray();
        Assert.Empty(Assert.Single(Evaluate(Snapshot("WizardHouseBasement"), unconfirmed).Options).EventCandidates);
        Assert.Empty(Assert.Single(Evaluate(Snapshot("DesertFestival", desertReady: false), DesertIntent()).Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerOverwritesForgedEndpointPriceAndDesertOutfit()
    {
        var wizard = Snapshot("WizardHouseBasement");
        var wizardCandidate = Assert.Single(Assert.Single(Evaluate(wizard, WizardIntent()).Options).EventCandidates);
        var forged = wizardCandidate.Parameters.Concat(new[]
        {
            P("customization_projection_fingerprint", "forged"), P("customization_price_gold", "1"),
            P("customization_money_before", "1"), P("target_tile_x", "99"), P("target_tile_y", "99"),
            P("stand_tile_x", "98"), P("stand_tile_y", "99"), P("customization_action_raw", "forged"),
            P("native_contract", "forged")
        }).ToArray();
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(wizard, forged), wizard).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "customization_projection_fingerprint", new string('c', 64));
        AssertParameter(item.NormalizedCommand.Parameters, "customization_price_gold", "500");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "12");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_y", "4");
        AssertParameter(item.NormalizedCommand.Parameters, "customization_action_raw", "WizardShrine");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);

        var desert = Snapshot("DesertFestival");
        var desertForged = DesertIntent().Concat(new[] { P("customization_expected_hat_qid", "(H)forged"),
            P("customization_stylist_name", "Sandy"), P("customization_expected_outfit_index", "99") }).ToArray();
        var desertItem = Assert.Single(new ActionQueueCompiler().Compile(Action(desert, desertForged), desert).Items);
        Assert.Empty(desertItem.BlockingReasons);
        AssertParameter(desertItem.NormalizedCommand.Parameters, "customization_expected_hat_qid", "(H)42");
        AssertParameter(desertItem.NormalizedCommand.Parameters, "customization_stylist_name", "Emily");
        AssertParameter(desertItem.NormalizedCommand.Parameters, "customization_expected_outfit_index", "7");
    }

    [Fact]
    public void RemoteCommandProducesRollingContinuationAndCapabilityStaysPlayerOnly()
    {
        var candidate = Assert.Single(Assert.Single(Evaluate(Snapshot("Farm", includeRoute: true), WizardIntent()).Options).EventCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        AssertParameter(candidate.Parameters, "continuation.option_id", OptionId);
        AssertParameter(candidate.Parameters, "continuation.customization_mode", "wizard_shrine");
        AssertParameter(candidate.Parameters, "continuation.customization_skin_index", "3");
        foreach (var optionId in new[] { OptionId, "executor.customize_player" })
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-314" }, declaration.ReadEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
            Assert.True(declaration.PlayerConfirmationRequired);
            Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.customize_player"));
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = OptionId, InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true, Parameters = parameters }
        }, true);

    private static SmallModelActionParameter[] WizardIntent() => new[]
    {
        P("customization_mode", "wizard_shrine"), P("customization_reason", "explicit request"),
        P("confirm_customization", "true"), P("customization_name", "Test Farmer"),
        P("customization_skin_index", "3")
    };

    private static SmallModelActionParameter[] DesertIntent() => new[]
    {
        P("customization_mode", "desert_makeover"), P("customization_reason", "accept exact projected outfit"),
        P("confirm_customization", "true")
    };

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind, Available = candidate.Available,
        LocationId = candidate.LocationId, ExpectedEffect = candidate.ExpectedEffect, EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "customization.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.customization", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "customize.player", OptionId = OptionId,
            Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string playerLocation, bool desertReady = true, bool includeRoute = false)
    {
        var projection = new
        {
            schema_version = "player_customization.v1", projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('c', 64), invocation_policy = "player_command_only",
            current = new { Name = "Farmer", favorite_thing = "Parsnips", gender = "male", skin_index = 2,
                hair_style_id = 4, accessory_index = -1, eye_hsv = new { hue = 10, saturation = 20, value = 90 },
                hair_hsv = new { hue = 30, saturation = 40, value = 50 } },
            wizard_shrine = new { location_id = "WizardHouseBasement", price_gold = 500, money_before = 1000,
                service_status = playerLocation == "WizardHouseBasement" ? "ready" : "route_to_wizard_shrine_required",
                hair_style_ids = new[] { 0, 4, 8, 16 },
                action_tiles = new[] { new { tile_x = 12, tile_y = 4, action_raw = "WizardShrine", action_token = "WizardShrine" } } },
            desert_makeover = new { location_id = "DesertFestival", passive_festival_day = 1, stylist_name = "Emily",
                already_styled_today = false, equipped_item_count = 3, free_inventory_slots = 5,
                service_status = !desertReady ? "blocked_desert_festival_inactive" : playerLocation == "DesertFestival" ? "ready" : "route_to_desert_makeover_required",
                touch_tiles = new[] { new { tile_x = 26, tile_y = 51, action_raw = "DesertMakeover", action_token = "DesertMakeover" } },
                expected_outfit_index = 7, uses_player_seed = true, special_laurel_outfit = false,
                expected_outfit_available = true, expected_parts = new[]
                {
                    new { slot = "hat", qualified_item_id = "(H)42", color = "" },
                    new { slot = "shirt", qualified_item_id = "(S)1199", color = "" },
                    new { slot = "pants", qualified_item_id = "(P)3", color = "247 245 205" }
                } },
            native_contract = NativeContract
        };
        var targetLocation = playerLocation == "Farm" ? "WizardHouseBasement" : playerLocation;
        var edges = includeRoute ? new object[] { new { kind = "warp", from_location = "Farm", from_x = 4,
            from_y = 4, target_location = targetLocation, target_x = 11, target_y = 4, resolved = true } } : Array.Empty<object>();
        var connectors = includeRoute ? new object[] { new { kind = "warp", tile_x = 4, tile_y = 4,
            target_location = targetLocation, target_x = 11, target_y = 4, resolved = true } } : Array.Empty<object>();
        var playerX = playerLocation == "DesertFestival" ? 26 : playerLocation == "WizardHouseBasement" ? 11 : 3;
        var playerY = playerLocation == "DesertFestival" ? 50 : 4;
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field(playerLocation), tile_x = Field(playerX), tile_y = Field(playerY),
                energy = Field(270), customization = Field(projection), inventory = Field(new { free_slots = 5 }) },
            locations = new { route_graph = Field(new { status = "complete", edges }),
                route_connectors = Field(new { location_id = playerLocation, connectors }),
                route_action_branch_coverage = Field(new { rows = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = playerLocation, width = 80, height = 80, notable_tiles = Array.Empty<object>() }) },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope { SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1, RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state };
    }

    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) => Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);
    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
