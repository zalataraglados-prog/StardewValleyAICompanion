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

public sealed class JukeboxSelectionMainlineTests
{
    private const string OptionId = "player.choose_jukebox_track";
    private const string NativeContract =
        "Saloon_Jukebox_checkAction->ChooseFromListMenu(default_index_0)->receiveLeftClick_forward_exact_index->receiveLeftClick_ok->Game1_default_music_request_receipt->receiveLeftClick_cancel";

    [Fact]
    public void ExplicitUnlockedTrackReachesOneTypedNativeQueue()
    {
        var snapshot = Snapshot("Saloon");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, "spring1").Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("choose_jukebox_track", candidate.Kind);
        AssertParameter(candidate.Parameters, "jukebox_track_index", "1");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("choose_jukebox_track", step.Kind);
        Assert.Contains("player_command_only_and_excluded_from_strategy_training", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.choose_jukebox_track", item.OptionId);
        Assert.Equal("choose_jukebox_track", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_track_id", "spring1");
    }

    [Fact]
    public void UnknownUnconfirmedOrGreenRainBlockedTrackIsExcludedUpstream()
    {
        Assert.Empty(Assert.Single(Evaluate(Snapshot("Saloon"), "unknown").Options).EventCandidates);
        Assert.Empty(Assert.Single(Evaluate(Snapshot("Saloon", greenRain: true), "spring1").Options).EventCandidates);
        Assert.Single(Assert.Single(Evaluate(Snapshot("Saloon", greenRain: true), "rain").Options).EventCandidates);
        Assert.Empty(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(Snapshot("Saloon"), new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = false,
                Parameters = new[] { P("jukebox_track_id", "spring1"), P("jukebox_reason", "explicit request"), P("confirm_jukebox_track", "false") }
            }
        }, true).Options).EventCandidates);
    }

    [Fact]
    public void CompilerOverwritesForgedCatalogIndexEndpointAndMusicState()
    {
        var snapshot = Snapshot("Saloon");
        var action = Action(snapshot, new[]
        {
            P("jukebox_track_id", "spring1"), P("jukebox_reason", "explicit request"), P("confirm_jukebox_track", "true"),
            P("jukebox_projection_fingerprint", "forged"), P("jukebox_track_index", "99"),
            P("jukebox_unlocked_track_count", "999"), P("jukebox_default_track_before", "forged"),
            P("jukebox_requested_track_before", "forged"), P("jukebox_current_song_before", "forged"),
            P("jukebox_green_rain_override", "true"), P("target_tile_x", "99"), P("target_tile_y", "99"),
            P("stand_tile_x", "98"), P("stand_tile_y", "99"), P("jukebox_action_raw", "forged"),
            P("native_contract", "forged")
        });
        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_projection_fingerprint", new string('j', 64));
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_track_index", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_unlocked_track_count", "3");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_default_track_before", "summer1");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_requested_track_before", "summer1");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_current_song_before", "summer1");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_green_rain_override", "false");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_y", "17");
        AssertParameter(item.NormalizedCommand.Parameters, "jukebox_action_raw", "Jukebox");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void RemotePlayerCommandProducesOnlyRollingRouteContinuation()
    {
        var candidate = Assert.Single(Assert.Single(Evaluate(Snapshot("Farm", includeRoute: true), "spring1").Options).EventCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        AssertParameter(candidate.Parameters, "continuation.option_id", OptionId);
        AssertParameter(candidate.Parameters, "continuation.jukebox_track_id", "spring1");
        AssertParameter(candidate.Parameters, "continuation.confirm_jukebox_track", "true");
    }

    [Fact]
    public void CapabilityAndRuntimeKeepMusicChoicePlayerOnlyNativeAndSeparateFromMiniJukebox()
    {
        foreach (var optionId in new[] { OptionId, "executor.choose_jukebox_track" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-313" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-313" }, capability.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
            Assert.True(capability.PlayerConfirmationRequired);
            Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.choose_jukebox_track"));
        var runtime = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.JukeboxSelection.cs"));
        Assert.Contains("checkAction(", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.changeMusicTrack(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("miniJukeboxTrack", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("songsHeard.Add", runtime, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, string trackId) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = new[] { P("jukebox_track_id", trackId), P("jukebox_reason", "explicit request"), P("confirm_jukebox_track", "true") }
            }
        }, true);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = OptionId,
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "jukebox.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.jukebox",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "choose.jukebox", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string playerLocation, bool greenRain = false, bool includeRoute = false)
    {
        var projection = new
        {
            schema_version = "jukebox_selection.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('j', 64),
            invocation_policy = "player_command_only",
            location_id = "Saloon",
            service_status = playerLocation == "Saloon" ? "ready" : "route_to_saloon_required",
            green_rain_native_override_active = greenRain,
            default_music_track = "summer1",
            requested_music_track = "summer1",
            current_song_name = "summer1",
            unlocked_track_count = 3,
            tracks = new[]
            {
                new { track_id = "summer1", track_index = 0, selectable_now = !greenRain },
                new { track_id = "spring1", track_index = 1, selectable_now = !greenRain },
                new { track_id = "rain", track_index = 2, selectable_now = true }
            },
            action_tiles = new[]
            {
                new { tile_x = 1, tile_y = 17, action_raw = "Jukebox" },
                new { tile_x = 2, tile_y = 17, action_raw = "Jukebox" }
            },
            native_contract = NativeContract
        };
        var edges = includeRoute
            ? new object[] { new { kind = "warp", from_location = "Farm", from_x = 4, from_y = 4, target_location = "Saloon", target_x = 10, target_y = 18, resolved = true } }
            : Array.Empty<object>();
        var connectors = includeRoute
            ? new object[] { new { kind = "warp", tile_x = 4, tile_y = 4, target_location = "Saloon", target_x = 10, target_y = 18, resolved = true } }
            : Array.Empty<object>();
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field(playerLocation),
                tile_x = Field(includeRoute ? 3 : 1),
                tile_y = Field(includeRoute ? 4 : 18),
                energy = Field(270),
                jukebox_selection = Field(projection)
            },
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
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
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
