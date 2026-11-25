using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class RouteRepairToolPreconditionTests
{
    [Theory]
    [InlineData("weeds")]
    [InlineData("stone")]
    public void Summer11ClearanceRejectsWateringCanAndFishingRod(string obstacle)
    {
        var snapshot = Snapshot(obstacle);
        var queue = new ActionQueueCompiler().Compile(ClearAction(snapshot), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons,
            reason => reason.StartsWith("clear_obstacle_matching_tool_missing:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("weeds", "StardewValley.Tools.Pickaxe", "(T)Pickaxe")]
    [InlineData("weeds", "StardewValley.Tools.Axe", "(T)Axe")]
    [InlineData("weeds", "StardewValley.Tools.Hoe", "(T)Hoe")]
    [InlineData("weeds", "StardewValley.Tools.MeleeWeapon", "(W)0")]
    [InlineData("weeds", "StardewValley.Tools.MeleeWeapon", "(W)47")]
    [InlineData("stone", "StardewValley.Tools.Pickaxe", "(T)Pickaxe")]
    [InlineData("weeds", "StardewValley.Tools.Hoe", "(T)SteelHoe")]
    [InlineData("weeds", "StardewValley.Tools.Axe", "(T)GoldAxe")]
    [InlineData("stone", "StardewValley.Tools.Pickaxe", "(T)CopperPickaxe")]
    [InlineData("stone", "StardewValley.Tools.Pickaxe", "(T)SteelPickaxe")]
    [InlineData("stone", "StardewValley.Tools.Pickaxe", "(T)GoldPickaxe")]
    [InlineData("stone", "StardewValley.Tools.Pickaxe", "(T)IridiumPickaxe")]
    public void NativeCompatibleToolsKeepOrdinaryClearanceAvailable(string obstacle, string runtimeType, string itemId)
    {
        var snapshot = Snapshot(obstacle, runtimeType, itemId);
        var queue = new ActionQueueCompiler().Compile(ClearAction(snapshot), snapshot);

        Assert.True(queue.Status == "pending", Reasons(queue));
    }

    [Theory]
    [InlineData("weeds")]
    [InlineData("stone")]
    public void RefillApproachCannotPromiseAnUnavailableTool(string obstacle)
    {
        var snapshot = Snapshot(obstacle);
        var queue = new ActionQueueCompiler().Compile(MovePlan(snapshot), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.SelectMany(item => item.BlockingReasons),
            reason => reason.StartsWith("clear_obstacle_matching_tool_missing:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("weeds")]
    [InlineData("stone")]
    public void RefillApproachWithPickaxeKeepsExplicitBoundedRepair(string obstacle)
    {
        var snapshot = Snapshot(obstacle, "StardewValley.Tools.Pickaxe", "(T)Pickaxe");
        var queue = new ActionQueueCompiler().Compile(MovePlan(snapshot), snapshot);

        Assert.True(queue.Status == "pending", Reasons(queue));
        Assert.Equal(3, queue.Items.Length);
        var clear = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.clear_obstacle"));
        Assert.Contains(clear.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "tool_slot_index" && parameter.Value == "3");
        Assert.Contains(clear.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "required_tool_kind" && parameter.Value == "pickaxe");
    }

    [Theory]
    [InlineData("(W)47")]
    [InlineData("(W)53")]
    [InlineData("(W)66")]
    public void RefillApproachBindsTheNativeScytheKind(string itemId)
    {
        var snapshot = Snapshot("weeds", "StardewValley.Tools.MeleeWeapon", itemId);
        var queue = new ActionQueueCompiler().Compile(MovePlan(snapshot), snapshot);
        Assert.True(queue.Status == "pending", Reasons(queue));
        var clear = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.clear_obstacle"));
        Assert.Contains(clear.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "required_tool_kind" && parameter.Value == "scythe");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("wrong_shape")]
    [InlineData("unavailable")]
    [InlineData("missing_runtime_type")]
    [InlineData("wrong_runtime_type")]
    [InlineData("wrong_item_id")]
    [InlineData("wrong_slot")]
    [InlineData("empty_tool_row")]
    [InlineData("duplicate_slot")]
    [InlineData("zero_stack")]
    public void UnprovenInventoryCannotAuthorizeClearance(string mutation)
    {
        var snapshot = Snapshot("weeds", "StardewValley.Tools.Pickaxe", "(T)Pickaxe");
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        var inventory = player["inventory"]!.AsObject();
        if (mutation == "missing") player.Remove("inventory");
        if (mutation == "null") inventory["value"] = null;
        if (mutation == "wrong_shape") inventory["value"] = new JsonObject();
        if (mutation == "unavailable") inventory["status"] = "unavailable";
        var tool = inventory["value"] is JsonArray rows ? rows[2]!.AsObject() : null;
        if (mutation == "missing_runtime_type") tool!.Remove("runtime_type");
        if (mutation == "wrong_runtime_type") tool!["runtime_type"] = "StardewValley.Tools.WateringCan";
        if (mutation == "wrong_item_id") tool!["qualified_item_id"] = "(O)343";
        if (mutation == "wrong_slot") tool!["slot_index"] = -1;
        if (mutation == "empty_tool_row") tool!["is_empty"] = true;
        if (mutation == "duplicate_slot") tool!["slot_index"] = 2;
        if (mutation == "zero_stack") tool!["stack"] = 0;
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);

        var queue = new ActionQueueCompiler().Compile(ClearAction(snapshot), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons,
            reason => reason.StartsWith("clear_obstacle_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("StardewValley.Tools.Axe", "(T)Axe")]
    [InlineData("StardewValley.Tools.Hoe", "(T)Hoe")]
    [InlineData("StardewValley.Tools.MeleeWeapon", "(W)47")]
    public void StoneSpecificallyRequiresPickaxe(string runtimeType, string itemId)
    {
        var snapshot = Snapshot("stone", runtimeType, itemId);
        var queue = new ActionQueueCompiler().Compile(ClearAction(snapshot), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("clear_obstacle_matching_tool_missing:stone", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void ExplicitWrongToolSlotCannotUseAnotherAvailablePickaxe()
    {
        var snapshot = Snapshot("weeds", "StardewValley.Tools.Pickaxe", "(T)Pickaxe");
        var action = ClearAction(snapshot);
        action.Actions[0].Parameters = action.Actions[0].Parameters.Concat(
            new[] { Parameter("tool_slot_index", "2"), Parameter("required_tool_kind", "pickaxe") }).ToArray();

        var queue = new ActionQueueCompiler().Compile(action, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("clear_obstacle_tool_slot_incompatible", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void RequiredToolKindCannotContradictTheBoundInventorySlot()
    {
        var snapshot = Snapshot("weeds", "StardewValley.Tools.Pickaxe", "(T)Pickaxe");
        var action = ClearAction(snapshot);
        action.Actions[0].Parameters = action.Actions[0].Parameters.Concat(
            new[] { Parameter("tool_slot_index", "3"), Parameter("required_tool_kind", "axe") }).ToArray();
        var queue = new ActionQueueCompiler().Compile(action, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("clear_obstacle_required_tool_kind_incompatible", queue.Items.Single().BlockingReasons);
    }

    [Theory]
    [InlineData("twig", false)]
    [InlineData("twig", true)]
    [InlineData("artifact_spot", false)]
    [InlineData("artifact_spot", true)]
    public void ExistingTypedClearanceStillBindsItsExactOutput(string kind, bool changeOutput)
    {
        var snapshot = Snapshot("weeds", kind == "twig" ? "StardewValley.Tools.Axe" : "StardewValley.Tools.Hoe",
            kind == "twig" ? "(T)Axe" : "(T)Hoe");
        var location = JsonNode.Parse(snapshot.State["current_location"].GetRawText())!.AsObject();
        var obstacle = location["objects"]!["value"]![0]!.AsObject();
        var toolKind = kind == "twig" ? "axe" : "hoe";
        var experience = kind == "twig" ? "1" : "15";
        // Synthetic typed-projection binding, not a native loot/run claim.
        var output = JsonSerializer.Serialize(new[] { new
            { qualified_item_id = kind == "twig" ? "(O)388" : "(O)330", quantity = 1 } });
        obstacle["qualified_item_id"] = kind == "twig" ? "(O)295" : "(O)590";
        obstacle["name"] = kind == "twig" ? "Twig" : "Artifact Spot";
        obstacle["clear_kind"] = kind;
        obstacle["clear_obstacle_executor_status"] = "ready";
        obstacle["required_tool_kind"] = toolKind;
        obstacle["tool_slot_index"] = 3;
        obstacle["expected_tool_hits_to_clear"] = 1;
        obstacle["harvest_experience_projection_status"] = "exact";
        obstacle["harvest_experience_skill_id"] = "foraging";
        obstacle["harvest_experience_on_success_min"] = int.Parse(experience);
        obstacle["harvest_experience_on_success_max"] = int.Parse(experience);
        obstacle["clear_output_projection_status"] = "exact";
        obstacle["clear_output_items_json"] = output;
        snapshot.State["current_location"] = JsonSerializer.SerializeToElement(location);
        var action = ClearAction(snapshot);
        action.Actions[0].Parameters = action.Actions[0].Parameters.Concat(new[]
        {
            Parameter("tool_slot_index", "3"), Parameter("required_tool_kind", toolKind),
            Parameter("skill_experience_projection_status", "exact"), Parameter("skill_experience_skill_id", "foraging"),
            Parameter("skill_experience_on_success_min", experience), Parameter("skill_experience_on_success_max", experience),
            Parameter("clear_output_projection_status", "exact"), Parameter("clear_output_items_json", changeOutput ? "[]" : output)
        }).ToArray();

        var queue = new ActionQueueCompiler().Compile(action, snapshot);

        Assert.True(queue.Status == (changeOutput ? "blocked" : "pending"), Reasons(queue));
        Assert.Equal(changeOutput, queue.Items.Single().BlockingReasons.Contains("object_clear_output_projection_drifted"));
    }

    [Fact]
    public void TakingToolFromChestRemainsASeparateLegalPrefixBeforeFreshClearance()
    {
        var source = MaterialTransferTestFixture.Snapshot(locked: false);
        var farm = JsonNode.Parse(source.State["farm"].GetRawText())!.AsObject();
        var graph = farm["material_inventory_graph"]!["value"]!.AsObject();
        var nodes = graph["inventory_nodes"]!.AsArray();
        nodes[0]!["slots"] = new JsonArray();
        var chestTool = nodes[1]!["slots"]![0]!.AsObject();
        chestTool["qualified_item_id"] = "(T)Pickaxe";
        chestTool["runtime_type"] = "StardewValley.Tools.Pickaxe";
        chestTool["stack"] = 1;
        chestTool["maximum_stack_size"] = 1;
        source.State["farm"] = JsonSerializer.SerializeToElement(farm);
        var transfer = new SmallModelActionEnvelope
        {
            StateHash = source.StateHash, SourceModel = "tool-prefix.test", GoalId = "daily.closed_loop",
            ExecutionMode = "training_singleplayer", Actor = Actor(),
            Actions = [new() { ActionId = "take-tool", OptionId = "executor.transfer_material",
                Parameters = MaterialTransferTestFixture.Parameters(withdraw: true).Select(parameter =>
                    parameter.Name switch
                    {
                        "qualified_item_id" => Parameter(parameter.Name, "(T)Pickaxe"),
                        "quantity" or "expected_source_stack" => Parameter(parameter.Name, "1"),
                        _ => parameter
                    }).ToArray() }]
        };
        var prefix = new ActionQueueCompiler().Compile(transfer, source);
        Assert.True(prefix.Status == "pending", Reasons(prefix));
        Assert.Equal("executor.transfer_material", prefix.Items.Single().OptionId);

        // Compile never invents the result of a preceding native transfer. The
        // existing executor refresh/recompile boundary supplies the actual slot.
        var fresh = Snapshot("stone", "StardewValley.Tools.Pickaxe", "(T)Pickaxe");
        Assert.Equal("pending", new ActionQueueCompiler().Compile(ClearAction(fresh), fresh).Status);
    }

    private static SmallModelActionEnvelope ClearAction(SnapshotEnvelope snapshot) => new()
    {
        StateHash = snapshot.StateHash, SourceModel = "native-clearance.test", GoalId = "daily.closed_loop",
        ExecutionMode = "training_singleplayer", Actor = Actor(),
        Actions = [new() { ActionId = "clear-native-obstacle", OptionId = "executor.clear_obstacle",
            Parameters = [Parameter("target_location", "Farm"), Parameter("target_tile_x", "68"),
                Parameter("target_tile_y", "25"), Parameter("max_tool_swings", "8")] }]
    };

    private static SmallModelPlanEnvelope MovePlan(SnapshotEnvelope snapshot) => new()
    {
        StateHash = snapshot.StateHash, SourceModel = "native-refill-approach.test", GoalId = "daily.closed_loop",
        ExecutionMode = "training_singleplayer", Actor = Actor(),
        Steps = [new() { StepId = "bounded-refill-approach", Kind = "move_to_tile", TargetLocation = "Farm",
            TargetTileX = 70, TargetTileY = 25, EstimatedMinutes = 12,
            Parameters = [Parameter("max_route_repair_clears", "4"), Parameter("max_route_repair_minutes", "12")] }]
    };

    private static SnapshotEnvelope Snapshot(string obstacle, string? toolType = null, string? toolId = null)
    {
        var objects = new[] { new { tile_x = 68, tile_y = 25, qualified_item_id = obstacle == "weeds" ? "(O)676" : "(O)343",
            name = obstacle == "weeds" ? "Weeds" : "Stone", type = "StardewValley.Object",
            clear_kind = "", clear_obstacle_executor_status = "not_applicable" } };
        var inventory = new JsonArray
        {
            JsonSerializer.SerializeToNode(new { slot_index = 2, qualified_item_id = "(T)WateringCan", runtime_type = "StardewValley.Tools.WateringCan", stack = 1 }),
            JsonSerializer.SerializeToNode(new { slot_index = 8, qualified_item_id = "(T)BambooPole", runtime_type = "StardewValley.Tools.FishingRod", stack = 1 })
        };
        if (toolType is not null) inventory.Add(JsonSerializer.SerializeToNode(new
            { slot_index = 3, qualified_item_id = toolId, runtime_type = toolType, stack = 1 }));
        var state = new JsonObject
        {
            ["player"] = new JsonObject { ["location_id"] = Field("Farm"), ["tile_x"] = Field(67), ["tile_y"] = Field(25),
                ["energy"] = Field(266.2), ["inventory"] = Field(inventory) },
            ["current_location"] = new JsonObject { ["objects"] = Field(objects), ["terrain_features"] = Field(Array.Empty<object>()),
                ["map"] = Field(new { id = "Farm", width = 80, height = 65 }) },
            ["locations"] = new JsonObject
            {
                ["collision_grid"] = Field(new { location_id = "Farm", width = 80, height = 65,
                    notable_tiles = Enumerable.Range(0, 80).SelectMany(x => Enumerable.Range(0, 65)
                        .Where(y => y != 25 || x == 68).Select(y => new { tile_x = x, tile_y = y, collision_blocked = true })).ToArray() }),
                ["route_action_branch_coverage"] = Field(new { rows = Array.Empty<object>() })
            },
            ["menus"] = new JsonObject { ["active_menu"] = Field(new { is_open = false, type = "none" }) }
        };
        return new SnapshotEnvelope { StateHash = "native-summer11-tool-fixture", GameTick = 1, Completeness = "complete",
            State = state.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value)) };
    }

    private static JsonObject Field(object value) => new() { ["value"] = JsonSerializer.SerializeToNode(value), ["status"] = "available" };
    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
    private static ActionActorRef Actor() => new() { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" };
    private static string Reasons(ActionQueueEnvelope queue) => string.Join(";", queue.Items.SelectMany(item => item.BlockingReasons));
}
