using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class WildTreeChopAcquisitionMainlineTests
{
    private const string OptionId = "foraging.chop_wild_tree";

    [Fact]
    public void ReadyWildTreeReusesClearObstacleNativeAxeQueue()
    {
        var snapshot = Snapshot(StateJson("ready", hasSeed: false, hasMoss: false));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { OptionId },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, value => value.Name == "clear_completion_mode" && value.Value == "wild_tree_removed");
        Assert.Contains(candidate.Parameters, value => value.Name == "required_tool_kind" && value.Value == "axe");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Contains(plan.Steps, step => step.Kind == "clear_obstacle");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.True(
            queue.Status == "pending",
            queue.Status + ":" + string.Join(";", queue.Items.SelectMany(value => value.BlockingReasons)));
        var item = Assert.Single(queue.Items, value => value.OptionId == "executor.clear_obstacle");
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, value => value.Name == "tree_chop_tree_type" && value.Value == "1");
        Assert.Contains(item.NormalizedCommand.Parameters, value => value.Name == "expected_trees_chopped_after" && value.Value == "26");
        Assert.Contains("present=false", Assert.Single(item.NormalizedCommand.Steps).ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false, "blocked_tree_seed_must_be_harvested_first")]
    [InlineData(false, true, "blocked_tree_moss_must_be_harvested_first")]
    public void PendingTreeProductsAreExcludedUpstream(bool hasSeed, bool hasMoss, string status)
    {
        var snapshot = Snapshot(StateJson(status, hasSeed, hasMoss));
        var candidate = Assert.Single(Assert.Single(
            new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { OptionId }, true).Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(status, candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsFreshOutputAndStatProjectionDrift()
    {
        var initial = Snapshot(StateJson("ready", hasSeed: false, hasMoss: false));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { OptionId }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson(
            "ready",
            hasSeed: false,
            hasMoss: false,
            minimumWood: 18,
            treesChoppedAfter: 27));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        var item = Assert.Single(queue.Items, value => value.OptionId == "executor.clear_obstacle");
        Assert.Contains("wild_tree_chop_output_domain_drifted", item.BlockingReasons);
        Assert.Contains("wild_tree_chop_stat_or_experience_projection_drifted", item.BlockingReasons);
    }

    [Fact]
    public void RuntimeAdmissionAndTypedFieldsAreExplicit()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(OptionId);
        Assert.Equal(OptionRuntimeStatus.RuntimeVerified, declaration.RuntimeEvidenceStatus);
        Assert.Contains(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);

        var request = new TrainingExecutionRequest
        {
            TreeChopTreeType = "1",
            TreeChopProjectionStatus = "exact_live_tree_and_locked_wild_tree_chop_domain",
            ExpectedTreePresentAfter = false,
            ExpectedTreesChoppedBefore = 25,
            ExpectedTreesChoppedAfter = 26
        };
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Equal("1", roundTrip.TreeChopTreeType);
        Assert.False(roundTrip.ExpectedTreePresentAfter);
        Assert.Equal(25, roundTrip.ExpectedTreesChoppedBefore);
        Assert.Equal(26, roundTrip.ExpectedTreesChoppedAfter);
    }

    [Fact]
    public void RuntimeSourceKeepsOneClearObstacleStateMachineAndNativeReceipts()
    {
        var root = FindRepositoryRoot();
        var clearance = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MovementSleep.ObstacleClearance.cs"));
        var chop = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.WildTreeChop.cs"));
        var verification = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.WildTreeChop.Verification.cs"));
        var terrain = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.TerrainExperience.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeClearObstacleSmoke.ps1"));

        Assert.Contains("ValidateWildTreeChopExecutionRequest", clearance, StringComparison.Ordinal);
        Assert.Contains("WildTreeChopWaitingForFall", clearance, StringComparison.Ordinal);
        Assert.Contains("TreeToolTracePatch.Begin", clearance, StringComparison.Ordinal);
        Assert.Contains("TreesChopped", chop, StringComparison.Ordinal);
        Assert.Contains("complete_stochastic_native_branch_domain_no_rng_consumed", chop, StringComparison.Ordinal);
        Assert.Contains("data.SeedItemId != expected.SeedItemId", chop, StringComparison.Ordinal);
        Assert.Contains("lumberjack_profession", chop, StringComparison.Ordinal);
        Assert.Contains("Game1.IsMultiplayer ? 4 : 5 + deterministicExtra", chop, StringComparison.Ordinal);
        Assert.Contains("row.MaxStack >= row.MinStack", chop, StringComparison.Ordinal);
        Assert.Contains("row.QualityModifiers is null", chop, StringComparison.Ordinal);
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.WildTreeChop.cs"));
        Assert.Contains("data.SeedItemId != expected.SeedItemId", bridge, StringComparison.Ordinal);
        Assert.Contains("lumberjack_profession", bridge, StringComparison.Ordinal);
        Assert.Contains("Game1.IsMultiplayer ? 4 : 5 + deterministicExtra", bridge, StringComparison.Ordinal);
        Assert.Contains("row.MaxStack >= row.MinStack", bridge, StringComparison.Ordinal);
        Assert.Contains("row.QualityModifiers is null", bridge, StringComparison.Ordinal);
        Assert.Contains("axe.GetType() == typeof(Axe)", terrain, StringComparison.Ordinal);
        Assert.Contains("OrderBy(row => row.Key, StringComparer.Ordinal)", verification, StringComparison.Ordinal);
        Assert.Contains("row.Value.ToString(CultureInfo.InvariantCulture)", verification, StringComparison.Ordinal);
        var fixture = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.ClearObstacleFixture.cs"));
        Assert.Contains("\"pine_professions\" => \"3\"", fixture, StringComparison.Ordinal);
        Assert.Contains("Game1.player.professions.Add(14)", fixture, StringComparison.Ordinal);
        Assert.Contains("fixture_wild_tree_chop_profile_unknown", fixture, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_DISABLE_EXTERNAL_GOD_TOOL", smoke, StringComparison.Ordinal);
    }

    private static string StateJson(
        string status,
        bool hasSeed,
        bool hasMoss,
        int minimumWood = 17,
        long treesChoppedAfter = 26) => $$$"""
    {
      "player": {
        "location_id":{"value":"Farm","status":"available"}, "tile_x":{"value":10,"status":"available"}, "tile_y":{"value":10,"status":"available"},
        "energy":{"value":270,"status":"available"}, "inventory":{"value":[],"status":"available"},
        "skills_detail":{"value":{"foraging":{"level":4,"experience":620}},"status":"available"}
      },
      "time":{"time":{"value":900,"status":"available"}},
      "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
      "current_location":{"map":{"value":{"id":"Farm"},"status":"available"},"debris":{"value":[],"status":"available"},"objects":{"value":[],"status":"available"},"terrain_features":{"value":[{
        "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.Tree","runtime_type":"StardewValley.TerrainFeatures.Tree","tree_type":"1",
        "growth_stage":5,"health":10,"stump":false,"tapped":false,"falling":false,"has_moss":{{{hasMoss.ToString().ToLowerInvariant()}}},"has_seed":{{{hasSeed.ToString().ToLowerInvariant()}}},"max_shake":0,
        "tree_chop_acquisition_status":"{{{status}}}","tree_chop_data_contract_status":"exact_locked_base_1.6.15_chop","tree_chop_protection_status":"unprotected",
        "tree_chop_completion_mode":"wild_tree_removed","tree_chop_tool_slot_index":2,"tree_chop_required_tool_kind":"axe","tree_chop_expected_tool_swings":6,"tree_chop_energy_cost":7.2,
        "tree_chop_guaranteed_minimum_outputs":[{"qualifiedItemId":"(O)388","quality":0,"quantityMin":{{{minimumWood}}}},{"qualifiedItemId":"(O)92","quality":0,"quantityMin":6}],
        "tree_chop_optional_output_domain":[{"kind":"exact","qualified_item_id":"(O)388","quality":0,"quantity_max":null,"branch":"quantity_above_guaranteed"}],
        "tree_chop_output_distribution_status":"complete_stochastic_native_branch_domain_no_rng_consumed","tree_chop_projection_status":"exact_live_tree_and_locked_wild_tree_chop_domain",
        "tree_chop_foraging_experience_before":620,"tree_chop_foraging_experience_delta":16,"tree_chop_foraging_experience_after":636,
        "tree_chop_trees_chopped_before":25,"tree_chop_trees_chopped_delta":1,"tree_chop_trees_chopped_after":{{{treesChoppedAfter}}},
        "tree_chop_expected_tree_present_after":false,
        "tree_chop_native_contract":"Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation"
      }],"status":"available"}},
      "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},"route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}}
    }
    """;

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-09T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
