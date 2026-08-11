using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MineElevatorMainlineTests
{
    [Fact]
    public void OfferedCheckpointCompilesThroughExistingCloseMenuPrimitive()
    {
        var snapshot = Snapshot("ordinary_mines", 25);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.use_elevator",
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "target_depth", Value = "25" },
                        new SmallModelActionParameter { Name = "target_location_family", Value = "ordinary_mines" }
                    }
                }
            },
            true);

        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("select_mine_elevator_floor", candidate.Kind);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("close_menu", step.Kind);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.close_menu", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, row => row.Name == "expected_mine_level_after" && row.Value == "25");
        Assert.Contains(item.NormalizedCommand.Parameters, row => row.Name == "target_runtime_identity" && row.Value == "identity");
    }

    [Fact]
    public void ReachDepthReusesElevatorChainAndPreservesFinalObjective()
    {
        var snapshot = Snapshot("ordinary_mines", 27);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.reach_depth",
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "target_depth", Value = "27" },
                        new SmallModelActionParameter { Name = "target_location_family", Value = "ordinary_mines" }
                    }
                }
            },
            true);

        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("select_mine_elevator_floor", candidate.Kind);
        Assert.StartsWith("mining.reach_depth:elevator:", candidate.CandidateId, StringComparison.Ordinal);
        Assert.Contains(candidate.Parameters, row => row.Name == "target_depth" && row.Value == "25");
        Assert.Contains(candidate.Parameters, row => row.Name == "continuation.option_id" && row.Value == "mining.reach_depth");
        Assert.Contains(candidate.Parameters, row => row.Name == "continuation.target_depth" && row.Value == "27");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("close_menu", step.Kind);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.close_menu", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, row => row.Name == "expected_mine_level_after" && row.Value == "25");
    }

    [Theory]
    [InlineData("ordinary_mines", "ordinary_mines", 130, "ordinary_mine_target_depth_out_of_range")]
    [InlineData("ordinary_mines", "skull_cavern", 130, "target_location_family_mismatch_current_mine")]
    public void ReachDepthNeverUsesOrdinaryElevatorForInvalidOrDifferentMineFamily(
        string currentFamily,
        string requestedFamily,
        int target,
        string reason)
    {
        var snapshot = Snapshot(currentFamily, target);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.reach_depth",
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "target_depth", Value = target.ToString() },
                        new SmallModelActionParameter { Name = "target_location_family", Value = requestedFamily }
                    }
                }
            },
            true);

        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("mining_reach_depth_plan_envelope", candidate.Kind);
        Assert.Contains(reason, candidate.BlockReasons);
        Assert.DoesNotContain("elevator", candidate.CandidateId, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ordinary_mines", 23, "mine_elevator_target_must_be_zero_or_multiple_of_five_through_120")]
    [InlineData("skull_cavern", 25, "mine_elevator_requires_ordinary_mines_family")]
    [InlineData("ordinary_mines", 45, "mine_elevator_target_beyond_unlocked_checkpoint")]
    public void InvalidTargetIsExcludedBeforeRanking(string family, int target, string reason)
    {
        var snapshot = Snapshot(family, target);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.use_elevator",
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "target_depth", Value = target.ToString() },
                        new SmallModelActionParameter { Name = "target_location_family", Value = family }
                    }
                }
            },
            true);
        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        Assert.Contains(reason, Assert.Single(option.EventCandidates).BlockReasons);
    }

    [Fact]
    public void RuntimeUsesOnlyNativeElevatorMenuClick()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MineElevator.cs"));
        var interact = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Interact.cs"));
        var adapter = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.cs"));
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("mine.getTileIndexAt(target.X, target.Y, \"Buildings\", \"mine\") == 112", interact, StringComparison.Ordinal);
        Assert.Contains("MineShaft.checkAction Buildings/mine tile index 112", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.enterMine", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ridingMineElevator =", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string requestedFamily, int target)
    {
        object Field(object value) => new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 1,
            confidence = 1
        };
        var raw = new Dictionary<string, object>
        {
            ["player"] = new Dictionary<string, object>
            {
                ["location_id"] = Field("UndergroundMine"),
                ["tile_x"] = Field(4),
                ["tile_y"] = Field(5),
                ["deepest_mine_level"] = Field(40),
                ["current_mine_level"] = Field(10)
            },
            ["current_location"] = new Dictionary<string, object>
            {
                ["mine_elevator_action_tiles"] = Field(Array.Empty<object>())
            },
            ["locations"] = new Dictionary<string, object>
            {
                ["collision_grid"] = Field(new { }),
                ["route_graph"] = Field(new { }),
                ["route_connectors"] = Field(Array.Empty<object>())
            },
            ["menus"] = new Dictionary<string, object>
            {
                ["active_menu"] = Field(new { is_open = true, type = "MineElevatorMenu" }),
                ["sleep_prompt_context"] = Field(new { prompt_open = false }),
                ["menu_specific_state"] = Field(new
                {
                    kind = "mine_elevator",
                    current_mine_level = 10,
                    lowest_level_reached = 40,
                    is_current_location_mineshaft = true,
                    entries = new[]
                    {
                        new { floor = 0, visible = true, selectable = true },
                        new { floor = 25, visible = true, selectable = true }
                    },
                    menu_identity_sha256 = "identity"
                })
            },
            ["mining"] = new Dictionary<string, object>
            {
                ["current_mine"] = Field(new
                {
                    location_id = "UndergroundMine",
                    mine_level = 10,
                    mine_area = 0,
                    mine_kind = requestedFamily,
                    is_loaded_current_location = true,
                    is_skull_cavern = false,
                    is_quarry_mine = false,
                    is_dangerous = false,
                    additional_difficulty = 0
                }),
                ["tiles"] = Field(new
                {
                    player_tile = new { tile_x = 4, tile_y = 5 },
                    map = new { width = 12, height = 12, status = "loaded_field_only" },
                    collision_context = new
                    {
                        status = "available",
                        encoding = "row_major_strings_1_blocked_0_passable",
                        width = 12,
                        height = 12,
                        blocked_rows = Enumerable.Repeat("000000000000", 12).ToArray()
                    },
                    exits = Array.Empty<object>(),
                    ladders = Array.Empty<object>(),
                    shafts = Array.Empty<object>(),
                    elevators = Array.Empty<object>()
                }),
                ["objects"] = Field(new[] { new { tile_x = 7, tile_y = 5, qualified_item_id = "(O)32", is_breakable_stone = true, best_pickaxe_hits_remaining = 1 } }),
                ["resource_clumps"] = Field(Array.Empty<object>()),
                ["monsters"] = Field(Array.Empty<object>()),
                ["floor_objectives"] = Field(new { must_kill_all_monsters_to_advance = false, enemy_count = 0, ladder_has_spawned = false }),
                ["reward_chests"] = Field(Array.Empty<object>()),
                ["player_resources"] = Field(new
                {
                    health = 100,
                    max_health = 100,
                    energy = 220,
                    max_energy = 270,
                    mining_level = 5,
                    combat_level = 4,
                    current_time = 1200,
                    deepest_mine_level = 40,
                    staircase_count = 0,
                    selected_slot_index = 0,
                    food_slots = Array.Empty<object>(),
                    cardinal_movement = new { tile_duration_ms = 100.0, status = "exact_mine_cardinal_input_without_collision_delay" }
                }),
                ["completeness"] = Field(new { status = "complete", unavailable_reasons = Array.Empty<string>() })
            },
            ["time"] = new Dictionary<string, object> { ["time"] = Field(600) }
        };
        var json = JsonSerializer.Serialize(raw, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-12T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
