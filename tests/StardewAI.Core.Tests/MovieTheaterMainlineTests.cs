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

public sealed class MovieTheaterMainlineTests
{
    private const string HighOption = "social.watch_movie";
    private const string LowOption = "executor.watch_movie";
    private const string NativeContract =
        "NPC_ticket_native_invite_then_Town_Theater_Entrance_yes_then_optional_MovieTheater_Concessions_ShopMenu_then_Theater_Doors_mutex_ready_native_MovieTheaterScreening_event_and_week_friendship_receipt";

    [Theory]
    [InlineData("invite", "watch_movie_invite_guest")]
    [InlineData("enter", "watch_movie_enter")]
    [InlineData("concession", "watch_movie_concession")]
    [InlineData("screening", "watch_movie_screening")]
    public void BoundObjectiveCompilesExactlyOneFreshNativeStage(string state, string expectedKind)
    {
        var snapshot = Snapshot(state);
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(expectedKind, candidate.Kind);
        AssertParameter(candidate.Parameters, "continuation.movie_objective_key", "spring_movie_0:Abigail:Popcorn");
        AssertParameter(candidate.Parameters, "native_contract", NativeContract);

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal(expectedKind == "watch_movie_wait_guest" ? "wait_ticks" : "watch_movie", planStep.Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(LowOption, item.OptionId);
        Assert.Equal("watch_movie", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void InsideTheaterDoesNotRequireAThirdTicketBeforeConcessionOrScreening()
    {
        foreach (var state in new[] { "concession", "screening" })
        {
            var candidate = Assert.Single(Assert.Single(Evaluate(Snapshot(state)).Options).EventCandidates);
            Assert.DoesNotContain("buy_shop_item", candidate.Kind, StringComparison.Ordinal);
            Assert.DoesNotContain(candidate.Parameters,
                parameter => parameter.Name == "continuation.shop_id" && parameter.Value == "BoxOffice");
        }
    }

    [Fact]
    public void ClosedHoursAndProjectionDriftFailBeforeRuntime()
    {
        Assert.Empty(Assert.Single(Evaluate(Snapshot("invite", timeOfDay: 2200)).Options).EventCandidates);

        var original = Snapshot("screening");
        var candidate = Assert.Single(Assert.Single(Evaluate(original).Options).EventCandidates);
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, original.StateHash);
        var drifted = Snapshot("screening", fingerprint: new string('b', 64));
        plan.StateHash = drifted.StateHash;
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, drifted).Items);
        Assert.Contains("movie_theater_projection_drifted_or_closed", item.BlockingReasons);
    }

    [Fact]
    public void EndpointStageNeverBindsAnUnavailableAdjacentStand()
    {
        var snapshot = Snapshot("concession", reachableEndpointStand: false);
        Assert.Empty(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
    }

    [Fact]
    public void RuntimePathUsesOnlyNativeMutationEntryPoints()
    {
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(LowOption));
        Assert.False(PendingSemanticActionCatalog.TryGet(HighOption, out _));
        Assert.False(PendingSemanticActionCatalog.TryGet(LowOption, out _));
        var high = OptionCapabilityRegistrySource.GetRequired(HighOption);
        var low = OptionCapabilityRegistrySource.GetRequired(LowOption);
        Assert.Equal(new[] { "EVD-321" }, high.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-321" }, high.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-321" }, low.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-321" }, low.RuntimeEvidenceIds);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(low));
        Assert.Contains(HighOption, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(LowOption, OptionCapabilityRegistrySource.TrainingAllowlist);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MovieTheater.cs"));
        var dispatch = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var supported = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SupportedOptions.cs"));
        var bridge = File.ReadAllText(Path.Combine(
            root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MovieTheater.cs"));
        Assert.Contains("debug.setup_movie_theater", dispatch, StringComparison.Ordinal);
        Assert.Contains("debug.setup_movie_theater", supported, StringComparison.Ordinal);
        Assert.Contains("invitedNpcsByName", bridge, StringComparison.Ordinal);
        Assert.Contains("Game1.getCharacterFromName", bridge, StringComparison.Ordinal);
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("shop.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("dialogue.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("skipEvent(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("MovieTheater.Invite(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("movieInvitations.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("lastSeenMovieWeek.Set", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("changeFriendship", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("movieMutex.ReleaseLock", runtime, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = HighOption,
                InvocationSource = OptionInvocationSource.Policy,
                Parameters = new[]
                {
                    P("continuation.movie_id", "spring_movie_0"),
                    P("continuation.movie_guest_name", "Abigail"),
                    P("continuation.movie_concession_id", "Popcorn")
                }
            }
        }, includeExecutorCalibrationOptions: true);

    private static SnapshotEnvelope Snapshot(
        string stage,
        int timeOfDay = 1000,
        string? fingerprint = null,
        bool reachableEndpointStand = true)
    {
        fingerprint ??= new string('a', 64);
        var inside = stage is "concession" or "screening";
        var invitation = stage == "invite"
            ? null
            : new
            {
                farmer_id = 50L,
                guest_name = "Abigail",
                fulfilled = inside,
                purchased_concession_id = stage == "screening" ? "Popcorn" : string.Empty
            };
        var projection = new
        {
            schema_version = "movie_theater.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            invocation_policy = "autonomous_social_value_with_explicit_alone_variant",
            native_contract = NativeContract,
            theater_unlocked = true,
            festival_day = false,
            time_of_day = timeOfDay,
            total_week = 8,
            player_last_seen_movie_week = 7,
            player_watched_this_week = false,
            movie_ticket_count = stage switch { "invite" => 2, "enter" => 1, _ => 0 },
            movie_ticket_slots = stage switch
            {
                "invite" => new[] { new { slot_index = 0, stack = 2 } },
                "enter" => new[] { new { slot_index = 0, stack = 1 } },
                _ => Array.Empty<object>()
            },
            movie_id = "spring_movie_0",
            movie_mutex_locked = false,
            movie_mutex_held_by_local_player = false,
            current_invitation = invitation,
            guest_options = new[]
            {
                new
                {
                    guest_name = "Abigail", location_id = "Town", tile_x = 11, tile_y = 10,
                    can_invite_now = true, movie_friendship_effective = 100,
                    friendship_points_before = 1000, last_seen_movie_week = 7,
                    blocked_reasons = Array.Empty<string>(),
                    concessions = new[]
                    {
                        new { concession_id = "Popcorn", friendship_effective = 50, price = 120 }
                    }
                }
            },
            entrance_action_tiles = new[]
            {
                new
                {
                    tile_x = 11, tile_y = 10, action_raw = "Theater_Entrance", action_token = "Theater_Entrance",
                    stand_tiles = new[]
                    {
                        new { tile_x = 10, tile_y = 10, map_passable = true, occupied = false,
                            path_reachable = reachableEndpointStand, path_length = 0, available = reachableEndpointStand }
                    }
                }
            },
            concession_action_tiles = new[]
            {
                new
                {
                    tile_x = 14, tile_y = 10, action_raw = "Concessions", action_token = "Concessions",
                    stand_tiles = new[]
                    {
                        new { tile_x = 13, tile_y = 10, map_passable = true, occupied = false,
                            path_reachable = reachableEndpointStand, path_length = 3, available = reachableEndpointStand }
                    }
                }
            },
            screening_door_action_tiles = new[]
            {
                new
                {
                    tile_x = 14, tile_y = 10, action_raw = "Theater_Doors", action_token = "Theater_Doors",
                    stand_tiles = new[]
                    {
                        new { tile_x = 13, tile_y = 10, map_passable = true, occupied = false,
                            path_reachable = reachableEndpointStand, path_length = 3, available = reachableEndpointStand }
                    }
                }
            }
        };
        var location = inside ? "MovieTheater" : "Town";
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field(location),
                tile_x = Field(10),
                tile_y = Field(10),
                money = Field(5000),
                inventory = Field(Array.Empty<object>()),
                movie_theater = Field(projection)
            },
            npcs = new
            {
                friendships = Field(Array.Empty<object>())
            },
            locations = new
            {
                collision_grid = Field(new
                {
                    location_id = location,
                    width = 40,
                    height = 30,
                    notable_tiles = Array.Empty<object>()
                }),
                route_connectors = Field(Array.Empty<object>()),
                route_graph = Field(new { status = "complete", edges = Array.Empty<object>() }),
                shops = Field(Array.Empty<object>())
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        }, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static object Field(object? value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = HighOption,
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
