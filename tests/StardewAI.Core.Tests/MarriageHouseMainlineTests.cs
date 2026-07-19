using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MarriageHouseMainlineTests
{
    [Theory]
    [InlineData(0, 1, 10000, "(O)388", 450, 20000, 500)]
    [InlineData(1, 2, 65000, "(O)709", 100, 80000, 125)]
    public void ExactFarmhouseUpgradeFlowsThroughCandidatePlanAndActionQueue(
        int levelBefore,
        int levelAfter,
        int price,
        string itemId,
        int requiredCount,
        int money,
        int inventoryCount)
    {
        var snapshot = Snapshot(levelBefore, levelAfter, price, itemId, requiredCount, money, inventoryCount, "ready", -1);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "housing.advance_farmhouse" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        Assert.Equal("purchase_farmhouse_upgrade", candidate.Kind);
        AssertParameter(candidate.Parameters, "expected_house_upgrade_level_before", levelBefore.ToString());
        AssertParameter(candidate.Parameters, "expected_house_upgrade_level_after_construction", levelAfter.ToString());
        AssertParameter(candidate.Parameters, "price", price.ToString());
        AssertParameter(candidate.Parameters, "required_stack", requiredCount.ToString());

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        Assert.Equal("purchase_farmhouse_upgrade", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.purchase_farmhouse_upgrade", item.OptionId);
        Assert.Equal("purchase_farmhouse_upgrade", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ConstructionInProgressIsExcludedUpstream()
    {
        var snapshot = Snapshot(1, 2, 65000, "(O)709", 100, 80000, 125, "farmhouse_upgrade_already_in_progress", 2);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "housing.advance_farmhouse" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("farmhouse_upgrade_already_in_progress", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsFarmhouseUpgradeWhenMoneyProjectionDrifts()
    {
        var original = Snapshot(0, 1, 10000, "(O)388", 450, 20000, 500, "ready", -1);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(original, new[] { "housing.advance_farmhouse" }, true);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot(0, 1, 10000, "(O)388", 450, 19999, 500, "ready", -1);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("farmhouse_upgrade_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CandidateRejectsNonNativeUpgradeTuple()
    {
        var snapshot = Snapshot(0, 1, 9000, "(O)388", 450, 20000, 500, "ready", -1);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "housing.advance_farmhouse" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("farmhouse_upgrade_native_tuple_invalid", candidate.BlockReasons);
    }

    [Fact]
    public void LevelThreeExpansionIsNotAGrandpaHouseAxisCandidate()
    {
        var snapshot = Snapshot(2, 3, 100000, "", 0, 150000, 0, "ready", -1);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "housing.advance_farmhouse" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        Assert.Equal("purchase_farmhouse_expansion", candidate.Kind);
        Assert.NotEqual("purchase_farmhouse_upgrade", candidate.Kind);
        Assert.Equal("transparent_native_farmhouse_upgrade_indirect_infrastructure", candidate.AvailabilityClass);
        AssertParameter(candidate.Parameters, "direct_grandpa_score_delta_after_construction", "0");
        AssertParameter(candidate.Parameters, "unlocks_cellar", "true");
        AssertParameter(candidate.Parameters, "unlocks_cask_recipe", "true");
        AssertParameter(candidate.Parameters, "adds_indoor_machine_placement_location", "true");
        AssertParameter(candidate.Parameters, "machine_capacity_projection_status", "cellar_static_map_capacity_available");
        AssertParameter(candidate.Parameters, "projected_cellar_static_placeable_tiles", "250");
        AssertParameter(candidate.Parameters, "projected_cellar_existing_machine_count", "33");
        AssertParameter(candidate.Parameters, "projected_cellar_machine_counts_by_qualified_id_json", "{\"(BC)163\":33}");
        AssertParameter(candidate.Parameters, "machine_fleet_projection_status", "complete_empty_machine_fleet");
        AssertParameter(candidate.Parameters, "machine_infrastructure_demand_semantics", "live_backlog_live_crop_and_versioned_committed_crop_wave_latest_build_window");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("executor.purchase_farmhouse_upgrade", Assert.Single(queue.Items).OptionId);
        Assert.Equal("pending", queue.Status);
    }

    [Fact]
    public void LevelThreeExpansionCarriesCompleteFleetAndRouteEvidenceWithoutInventingRemoteInputDemand()
    {
        const string machines = """
        [
          {"location_id":"Farm","minutes_until_ready":100,"ready_for_harvest":false,"machine_has_input":true,"machine_row_count_total":3,"machine_row_snapshot_status":"complete_no_row_truncation","machine_input_probe_eligible_count":0,"loadable_input_probe_status":"not_applicable_machine_not_idle","loadable_inputs":[]},
          {"location_id":"FarmHouse","minutes_until_ready":0,"ready_for_harvest":true,"machine_has_input":true,"machine_row_count_total":3,"machine_row_snapshot_status":"complete_no_row_truncation","machine_input_probe_eligible_count":0,"loadable_input_probe_status":"not_applicable_machine_not_idle","loadable_inputs":[]},
          {"location_id":"Cellar","minutes_until_ready":0,"ready_for_harvest":false,"machine_has_input":true,"machine_row_count_total":3,"machine_row_snapshot_status":"complete_no_row_truncation","machine_input_probe_eligible_count":0,"loadable_input_probe_status":"blocked_machine_location_not_current_requires_route_and_fresh_snapshot","loadable_inputs":[]}
        ]
        """;
        const string routeGraph = """
        {"edges":[
          {"from_location":"ScienceHouse","target_location":"Farm","resolved":true},
          {"from_location":"Farm","target_location":"FarmHouse","resolved":true},
          {"from_location":"FarmHouse","target_location":"Cellar","resolved":true}
        ]}
        """;
        const string machineCrafting = """
        {
          "projection_status":"complete_known_machine_recipe_projection",
          "unclassified_known_recipe_count":0,
          "rows":[{
            "output_is_cask":true,
            "output_qualified_item_id":"(BC)163",
            "output_count_per_craft":1,
            "craftable_count_from_player_inventory":12,
            "craftable_count_status":"exact_native_match_and_reverse_slot_consumption",
            "craft_candidate_status":"ready_for_native_personal_crafting_menu",
            "placement_location_rule":"Cellar_or_location_map_property_CanCaskHere"
          }]
        }
        """;
        var snapshot = Snapshot(2, 3, 100000, "", 0, 150000, 0, "ready", -1, machines, routeGraph, machineCrafting);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "housing.advance_farmhouse" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates.Where(row => row.Available));

        AssertParameter(candidate.Parameters, "machine_fleet_projection_status", "complete_machine_rows");
        AssertParameter(candidate.Parameters, "machine_fleet_total_count", "3");
        AssertParameter(candidate.Parameters, "machine_fleet_processing_count", "1");
        AssertParameter(candidate.Parameters, "machine_fleet_ready_output_count", "1");
        AssertParameter(candidate.Parameters, "machine_fleet_idle_manual_input_count", "1");
        AssertParameter(candidate.Parameters, "machine_fleet_actionable_service_count", "2");
        AssertParameter(candidate.Parameters, "machine_input_probe_status", "blocked_remote_idle_manual_inputs_require_route_and_fresh_snapshot");
        AssertParameter(candidate.Parameters, "machine_input_probe_loadable_alternative_count", "0");
        AssertParameter(candidate.Parameters, "machine_service_route_cost_status", "resolved_route_graph_hop_lower_bound");
        AssertParameter(candidate.Parameters, "machine_service_route_hop_lower_bound_total", "6");
        AssertParameter(candidate.Parameters, "machine_crafting_projection_status", "complete_known_machine_recipe_projection");
        AssertParameter(candidate.Parameters, "machine_crafting_cask_recipe_known", "true");
        AssertParameter(candidate.Parameters, "machine_crafting_cask_count_from_current_inventory", "12");
        AssertParameter(candidate.Parameters, "machine_crafting_cask_output_qualified_item_id", "(BC)163");

        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        Assert.Equal("pending", new ActionQueueCompiler().Compile(plan, snapshot).Status);
    }

    [Fact]
    public void TransparencyAndRuntimeUseNativeCarpenterLifecycleWithoutDirectProgressWrites()
    {
        var adapter = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.MarriageHouse.cs"));
        var machineCrafting = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MachineCrafting.cs"));
        var runtime = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.MarriageHouse.cs"));

        Assert.Contains("Upgrade(\"farmhouse_level_1\", 0, 1, 10000, \"(O)388\", 450)", adapter, StringComparison.Ordinal);
        Assert.Contains("Upgrade(\"farmhouse_level_2\", 1, 2, 65000, \"(O)709\", 100)", adapter, StringComparison.Ordinal);
        Assert.Contains("ConstructionDays = 3", adapter, StringComparison.Ordinal);
        Assert.Contains("UnlocksCellar", adapter, StringComparison.Ordinal);
        Assert.Contains("UnlocksCaskRecipe", adapter, StringComparison.Ordinal);
        Assert.Contains("CraftingRecipe.ItemMatchesForCrafting", machineCrafting, StringComparison.Ordinal);
        Assert.Contains("for (var slot = inventory.Count - 1;", machineCrafting, StringComparison.Ordinal);
        Assert.DoesNotContain("consumeIngredients(", machineCrafting, StringComparison.Ordinal);
        Assert.Contains("active.House.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("active.House.answerDialogue(response)", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money -=", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.ReduceId", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("daysUntilHouseUpgrade.Value = 3", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("HouseUpgradeLevel = request", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractKeepsImmediateConstructionAndEventualGrandpaStateSeparate()
    {
        var row = new MarriageHouseProgressRef
        {
            MarriedOrRoommate = false,
            Engaged = true,
            FarmhouseUpgradeLevel = 1,
            DaysUntilFarmhouseUpgrade = 3,
            GrandpaFactorSatisfied = false,
            CellarUnlocked = false,
            CellarInfrastructure = new CellarInfrastructureProgressRef
            {
                ProjectionStatus = "cellar_static_map_capacity_available",
                LocationId = "Cellar",
                MapWidth = 20,
                MapHeight = 20,
                StaticPlaceableTileCount = 250,
                OccupiedObjectCount = 33,
                MachineCount = 33
            },
            HouseUpgrade = new FarmhouseUpgradeProgressRef
            {
                LevelBefore = 1,
                LevelAfter = 2,
                ConstructionDays = 3,
                MeetsGrandpaHouseLevelAfterConstruction = true,
                GrandpaFactorSatisfiedAfterConstruction = false,
                DirectGrandpaScoreDeltaAfterConstruction = 0
            }
        };
        var json = JsonSerializer.Serialize(row, JsonOptions);

        Assert.Contains("\"engaged\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"days_until_farmhouse_upgrade\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"level_after\":2", json, StringComparison.Ordinal);
        Assert.Contains("\"grandpa_factor_satisfied\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"meets_grandpa_house_level_after_construction\":true", json, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        int levelBefore,
        int levelAfter,
        int price,
        string itemId,
        int requiredCount,
        int money,
        int inventoryCount,
        string status,
        int days,
        string machineRowsJson = "[]",
        string routeGraphJson = "{\"edges\":[]}",
        string machineCraftingJson = "{\"projection_status\":\"complete_known_machine_recipe_projection\",\"unclassified_known_recipe_count\":0,\"rows\":[]}")
    {
        var upgradeId = "farmhouse_level_" + levelAfter;
        var meetsGrandpaHouseLevel = levelAfter >= 2 ? "true" : "false";
        var unlocksCellar = levelBefore == 2 && levelAfter == 3 ? "true" : "false";
        var capacityStatus = unlocksCellar == "true"
            ? "cellar_static_map_capacity_available"
            : "no_new_machine_location_from_this_upgrade";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"ScienceHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"machine_crafting":{"value":{{{machineCraftingJson}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "marriage_house":{"value":{
              "location_accessible":true,
              "is_current_location":true,
              "carpenter_action_tile_x":10,
              "carpenter_action_tile_y":10,
              "carpenter_action_raw":"Carpenter",
              "is_master_game":true,
              "robin_present_at_counter":true,
              "building_under_construction":false,
              "married_or_roommate":false,
              "engaged":false,
              "spouse":"",
              "pending_roommate":false,
              "farmhouse_upgrade_level":{{{levelBefore}}},
              "days_until_farmhouse_upgrade":{{{days}}},
              "money":{{{money}}},
              "grandpa_factor_satisfied":false,
              "cellar_infrastructure":{
                "projection_status":"cellar_static_map_capacity_available",
                "location_id":"Cellar",
                "map_width":20,
                "map_height":20,
                "static_placeable_tile_count":250,
                "occupied_object_count":33,
                "machine_count":33,
                "machine_counts_by_qualified_id":{"(BC)163":33}
              },
              "house_upgrade":{
                "upgrade_id":"{{{upgradeId}}}",
                "level_before":{{{levelBefore}}},
                "level_after":{{{levelAfter}}},
                "price":{{{price}}},
                "required_item_id":"{{{itemId}}}",
                "required_item_count":{{{requiredCount}}},
                "inventory_item_count":{{{inventoryCount}}},
                "construction_days":3,
                "meets_grandpa_house_level_after_construction":{{{meetsGrandpaHouseLevel}}},
                "grandpa_factor_satisfied_after_construction":false,
                "direct_grandpa_score_delta_after_construction":0,
                "unlocks_cellar":{{{unlocksCellar}}},
                "unlocks_cask_recipe":{{{unlocksCellar}}},
                "adds_indoor_machine_placement_location":{{{unlocksCellar}}},
                "machine_capacity_projection_status":"{{{capacityStatus}}}",
                "action_status":"{{{status}}}"
              }
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines":{"value":{{{machineRowsJson}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time":{"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"ScienceHouse","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph":{"value":{{{routeGraphJson}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
