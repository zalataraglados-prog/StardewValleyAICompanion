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

public sealed class BobberSelectionMainlineTests
{
    private const string OptionId = "player.choose_bobber";
    private const string NativeContract =
        "FishShop_Bobbers_checkAction->ChooseFromIconsMenu(bobbers)->receiveLeftClick_exact_unlocked_icon->Farmer.bobberStyle_and_usingRandomizedBobber_receipt->native_close_button";

    [Theory]
    [InlineData(7)]
    [InlineData(-2)]
    public void ExplicitUnlockedStyleReachesOneTypedNativeQueue(int styleId)
    {
        var snapshot = Snapshot("FishShop", 20);
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, styleId).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("choose_bobber_style", candidate.Kind);
        AssertParameter(candidate.Parameters, "bobber_style_id", styleId.ToString());

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("choose_bobber_style", step.Kind);
        Assert.Contains("player_command_only_and_excluded_from_strategy_training", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.choose_bobber_style", item.OptionId);
        Assert.Equal("choose_bobber_style", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_random_after",
            (styleId == -2).ToString().ToLowerInvariant());
    }

    [Fact]
    public void LockedOrUnconfirmedStyleIsExcludedUpstream()
    {
        Assert.Empty(Assert.Single(Evaluate(Snapshot("FishShop", 2), 2).Options).EventCandidates);
        Assert.Empty(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(Snapshot("FishShop", 20), new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId, InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = false,
                Parameters = new[] { P("bobber_style_id", "7"), P("bobber_reason", "explicit request"), P("confirm_bobber_style", "false") }
            }
        }, true).Options).EventCandidates);
    }

    [Fact]
    public void CompilerOverwritesForgedUnlockEndpointAndPreferenceState()
    {
        var snapshot = Snapshot("FishShop", 20);
        var action = Action(snapshot, new[]
        {
            P("bobber_style_id", "7"), P("bobber_reason", "explicit request"), P("confirm_bobber_style", "true"),
            P("bobber_projection_fingerprint", "forged"), P("bobber_style_before", "38"),
            P("bobber_random_before", "true"), P("bobber_random_after", "true"),
            P("bobber_fish_caught_species_count", "999"), P("bobber_native_unlock_quotient", "999"),
            P("target_tile_x", "1"), P("target_tile_y", "1"), P("stand_tile_x", "1"), P("stand_tile_y", "2"),
            P("bobber_action_raw", "forged"), P("native_contract", "forged")
        });
        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_projection_fingerprint", new string('b', 64));
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_style_before", "0");
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_random_before", "false");
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_random_after", "false");
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_fish_caught_species_count", "20");
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_native_unlock_quotient", "10");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "10");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_y", "4");
        AssertParameter(item.NormalizedCommand.Parameters, "bobber_action_raw", "Bobbers");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void RemotePlayerCommandProducesOnlyRollingRouteContinuation()
    {
        var candidate = Assert.Single(Assert.Single(Evaluate(Snapshot("Farm", 20, true), 7).Options).EventCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        AssertParameter(candidate.Parameters, "continuation.option_id", OptionId);
        AssertParameter(candidate.Parameters, "continuation.bobber_style_id", "7");
        AssertParameter(candidate.Parameters, "continuation.confirm_bobber_style", "true");
    }

    [Fact]
    public void CapabilityAndRuntimeKeepCosmeticChoicePlayerOnlyAndNative()
    {
        foreach (var optionId in new[] { OptionId, "executor.choose_bobber_style" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-312" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-312" }, capability.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
            Assert.True(capability.PlayerConfirmationRequired);
            Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.choose_bobber_style"));
        var runtime = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.BobberSelection.cs"));
        Assert.Contains("checkAction(", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(center.X, center.Y);", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.bobberStyle.Value =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.usingRandomizedBobber =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random.Next", runtime, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, int styleId) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId, InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = new[] { P("bobber_style_id", styleId.ToString()), P("bobber_reason", "explicit request"), P("confirm_bobber_style", "true") }
            }
        }, true);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind,
        Available = candidate.Available, LocationId = candidate.LocationId, ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks, Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "bobber.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.bobber", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "choose.bobber", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string playerLocation, int fishCount, bool includeRoute = false)
    {
        var quotient = fishCount / 2;
        var styles = Enumerable.Range(0, 39).Select(style => new { style_id = style, unlocked = style <= quotient })
            .Cast<object>().Append(new { style_id = -2, unlocked = true }).ToArray();
        var projection = new
        {
            schema_version = "bobber_selection.v1", projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('b', 64), invocation_policy = "player_command_only",
            location_id = "FishShop", service_status = playerLocation == "FishShop" ? "ready" : "route_to_fish_shop_required",
            current_style_id = 0, using_randomized_bobber = false, fish_caught_species_count = fishCount,
            native_unlock_quotient = quotient, styles,
            action_tiles = new[] { new { tile_x = 10, tile_y = 4, action_raw = "Bobbers" } }, native_contract = NativeContract
        };
        var edges = includeRoute
            ? new object[] { new { kind = "warp", from_location = "Farm", from_x = 4, from_y = 4, target_location = "FishShop", target_x = 4, target_y = 12, resolved = true } }
            : Array.Empty<object>();
        var connectors = includeRoute
            ? new object[] { new { kind = "warp", tile_x = 4, tile_y = 4, target_location = "FishShop", target_x = 4, target_y = 12, resolved = true } }
            : Array.Empty<object>();
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field(playerLocation), tile_x = Field(includeRoute ? 3 : 10), tile_y = Field(includeRoute ? 4 : 5), energy = Field(270), bobber_selection = Field(projection) },
            locations = new
            {
                route_graph = Field(new { status = "complete", edges }),
                route_connectors = Field(new { location_id = playerLocation, connectors }),
                route_action_branch_coverage = Field(new { rows = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = playerLocation, width = 40, height = 30, notable_tiles = Array.Empty<object>() })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) => Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);
    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
