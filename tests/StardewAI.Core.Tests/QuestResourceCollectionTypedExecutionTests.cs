using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void ResourceCollectionQuestBindsDirectSpawnedObjectReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "current_location":{
              "objects":{"value":[{
                "tile_x":20,"tile_y":20,"item_id":"390","qualified_item_id":"(O)390",
                "is_spawned_object":true,"spawned_object_pickup_status":"ready",
                "projected_total_quantity":1,"projected_harvest_quality":0,
                "projected_gatherer_duplicate":false,
                "foraging_experience_on_success_min":0,"foraging_experience_on_success_max":0,
                "farming_experience_on_success_min":0,"farming_experience_on_success_max":0,
                "harvest_experience_status":"exact","harvest_experience_basis":"native_pickup"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_spawned_object", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Equal("executor.collect_spawned_object", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestTreatsMineStoneAsSourceNotReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "mining":{
              "current_mine":{"value":{"location_id":"UndergroundMine","mine_level":25,"mine_kind":"ordinary_mines"},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "tiles":{"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":7,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":7,"height":5,"blocked_rows":["1111111","1000001","1000001","1000001","1111111"]},"exits":[],"ladders":[],"shafts":[],"elevators":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "objects":{"value":[{
                "tile_x":3,"tile_y":2,"qualified_item_id":"(O)751",
                "is_breakable_stone":true,"best_pickaxe_hits_remaining":1,
                "guaranteed_drop_qualified_item_ids":["(O)390"],
                "possible_drop_qualified_item_ids":["(O)390"]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "resource_clumps":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "monsters":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "floor_objectives":{"value":{"must_kill_all_monsters_to_advance":false,"enemy_count":0,"ladder_has_spawned":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "reward_chests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "player_resources":{"value":{"health":100,"max_health":100,"energy":200,"current_time":1200,"selected_slot_index":0,"food_slots":[],"bomb_slots":[],"cardinal_movement":{"tile_duration_ms":100}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "completeness":{"value":{"status":"complete","unavailable_reasons":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            locationId: "UndergroundMine");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("mining_collect_quest_resource_plan_envelope", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "false");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Equal("executor.mine_stone", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsMatchingMonsterDropAsSource()
    {
        var snapshot = ResourceCollectionSnapshot(
            MonsterDropMiningDomainState(),
            locationId: "UndergroundMine",
            requiredItemId: "(O)768");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "execution_option_id" &&
            parameter.Value == "executor.combat_monster");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" &&
            parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Equal("executor.combat_monster", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "qualified_item_id" &&
            parameter.Value == "(O)768");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "combat_terminal_state" &&
            parameter.Value == "defeat");
    }

    [Fact]
    public void SpecialOrderCollectBindsMatchingMonsterDropContextTagsAsSource()
    {
        var snapshot = SpecialOrderCollectionSnapshot(
            MonsterDropMiningDomainState(),
            locationId: "UndergroundMine",
            acceptableContextTagSetsJson: "\"monster_drop\"");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "execution_option_id" &&
            parameter.Value == "executor.combat_monster");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" &&
            parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.combat_monster", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_acceptable_context_tag_sets_json" &&
            parameter.Value == """["monster_drop"]""");
    }

    [Fact]
    public void SpecialOrderCollectBindsOnlyMatchingGrabCropContextTags()
    {
        var snapshot = Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"Farm","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory_capacity":{"value":{"has_empty_slot":true,"empty_slots":12},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "farm":{
                "crops":{"value":[{
                  "tile_x":7,"tile_y":8,"ready_for_harvest":true,
                  "harvest_item_id":"24","harvest_item_qualified_id":"(O)24",
                  "harvest_item_category":-75,
                  "harvest_context_tags":["category_vegetable","color_green"],
                  "harvest_min_stack":1,"harvest_method":"Grab",
                  "harvest_experience_skill_id":"farming",
                  "harvest_experience_on_success_min":8,
                  "harvest_experience_on_success_max":8,
                  "harvest_experience_condition":"native_crop_harvest",
                  "harvest_experience_projection_status":"exact"
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "quests":{
                "active_quests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":[{
                  "quest_key":"CropOrder","quest_name":"Crop Order","quest_state":"InProgress",
                  "objectives":[{
                    "description":"Harvest vegetables","current_count":2,"max_count":25,
                    "runtime_type":"CollectObjective","fail_on_completion":false,"complete":false,
                    "per_type_fields":{"available":true,"acceptable_context_tag_sets":["category_vegetable"]}
                  }],"rewards":[]
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "time":{"time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "menus":{"active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "world_progress":{
                "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_crop_tile", candidate.Kind);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsReadyMachineOutputReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(MachineCollectionDomainState());

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_machine_output_tile", candidate.Kind);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.collect_machine_output"));

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectBindsMatchingMachineOutputContextTags()
    {
        var snapshot = Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"Farm","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":18,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":20,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "energy":{"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory_capacity":{"value":{"occupied_stacks":0,"empty_slots":12,"has_empty_slot":true},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              DOMAIN_STATE
              "quests":{
                "active_quests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":[{
                  "quest_key":"MachineOrder","quest_name":"Machine Order","quest_state":"InProgress",
                  "objectives":[{
                    "description":"Collect resources","current_count":2,"max_count":25,
                    "runtime_type":"CollectObjective","fail_on_completion":false,"complete":false,
                    "per_type_fields":{"available":true,"acceptable_context_tag_sets":["category_resource"]}
                  }],"rewards":[]
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "time":{"time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "menus":{"active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "world_progress":{
                "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """.Replace("DOMAIN_STATE", MachineCollectionDomainState(), StringComparison.Ordinal));

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_machine_output_tile", candidate.Kind);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.collect_machine_output"));

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsFarmDebrisAsReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(FarmDebrisDomainState());

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("pickup_debris_item", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var pickupStep = Assert.Single(plan.Steps.Where(step =>
            step.Kind == "pickup_debris"));
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "quest_candidate_id");
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "quest_next_action" &&
            parameter.Value == "collect_resources");
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" &&
            parameter.Value == "true");
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.pickup_debris"));

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectBindsFarmDebrisContextTags()
    {
        var snapshot = SpecialOrderCollectionSnapshot(FarmDebrisDomainState());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("pickup_debris_item", candidate.Kind);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var pickupStep = Assert.Single(plan.Steps.Where(step =>
            step.Kind == "pickup_debris"));
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "quest_next_action" &&
            parameter.Value == "collect_items");
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "quest_acceptable_context_tag_sets_json");
        Assert.Contains(pickupStep.Parameters, parameter =>
            parameter.Name == "debris_context_tags_json");
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.pickup_debris"));

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestUsesOnlyMiningOwnerForMineDebris()
    {
        var miningState = MonsterDropMiningDomainState()
            .Replace(
                "\"debris\":{\"value\":[],\"status\":\"available\"",
                "\"debris\":{\"value\":[{" +
                "\"debris_index\":0,\"item_id\":\"390\"," +
                "\"qualified_item_id\":\"(O)390\",\"item_quality\":0," +
                "\"chunk_count\":1,\"chunks\":[{" +
                "\"chunk_index\":0,\"tile_x\":3,\"tile_y\":2}]}]," +
                "\"status\":\"available\"",
                StringComparison.Ordinal) +
            """
            "current_location":{
              "debris":{"value":[{
                "debris_index":0,"item_id":"390","qualified_item_id":"(O)390",
                "item_quality":0,"chunk_count":1,
                "item":{"item_id":"390","qualified_item_id":"(O)390","context_tags":["category_resource","id_o_390"]},
                "chunks":[{"chunk_index":0,"tile_x":3,"tile_y":2}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """;
        var snapshot = ResourceCollectionSnapshot(
            miningState,
            locationId: "UndergroundMine");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "quest.advance" },
                includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.Equal(
            "mining_collect_quest_resource_plan_envelope",
            candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "debris_index" && parameter.Value == "0");
    }

    [Fact]
    public void ResourceCollectionQuestTreatsCurrentLocationBushAsSourceNotReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "current_location":{
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "large_terrain_features":{"value":[{
                "tile_x":20,"tile_y":20,"runtime_type":"StardewValley.TerrainFeatures.Bush",
                "bounding_tile_width":2,"bounding_tile_height":1,"is_bush":true,
                "bush_size":1,"bush_kind":"ordinary_berry","ready_for_harvest":true,
                "in_bloom":true,"tile_sheet_offset_before":1,"tile_sheet_offset_expected_after":0,
                "bush_harvest_status":"ready","bush_projection_status":"exact_from_native_bush_shake",
                "bush_output_qualified_item_id":"(O)390","bush_output_quantity_min":1,
                "bush_output_quantity_max":1,"bush_output_quality":0,
                "bush_foraging_experience_on_success_min":1,
                "bush_foraging_experience_on_success_max":1,
                "bush_nut_key":"","bush_nut_collected_before":false,
                "bush_nut_collected_expected_after":false
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            locationId: "IslandWest");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("harvest_bush", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "false");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_bush", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsCurrentLocationDebrisOutsideFarm()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "current_location":{
              "debris":{"value":[{
                "debris_index":0,"item_id":"829","qualified_item_id":"(O)829",
                "item_quality":0,"chunk_count":1,
                "item":{"item_id":"829","qualified_item_id":"(O)829","context_tags":["id_o_829"]},
                "chunks":[{"chunk_index":0,"tile_x":12,"tile_y":10}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "map":{"value":{"location_id":"IslandWest","width":100,"height":100},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            locationId: "IslandWest",
            requiredItemId: "(O)829");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("pickup_debris_item", candidate.Kind);
        Assert.Equal("IslandWest", candidate.LocationId);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.pickup_debris"));

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(queueItem => queueItem.BlockingReasons)));
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("IslandWest(12,10):debris_index=0", step.Target);
        Assert.Contains(
            "current_location.debris[0].chunk_count_decreases_or_removed=true",
            step.ExpectedEffect);
    }

    [Fact]
    public void ResourceCollectionQuestTreatsGingerHarvestAsSourceNotReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "current_location":{
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "terrain_features":{"value":[{
                "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.HoeDirt",
                "hoe_dirt_state":1,"has_crop":true,"crop_is_forage":true,
                "forage_crop_id":"2","is_ginger":true,"ginger_harvest_status":"ready",
                "ginger_required_tool_kind":"Hoe","ginger_tool_slot_index":3,
                "ginger_energy_cost":1.4,"ginger_output_qualified_item_id":"(O)829",
                "ginger_output_quality":0,"ginger_output_quantity_min":1,
                "ginger_output_quantity_max":1,
                "ginger_foraging_experience_on_success_min":7,
                "ginger_foraging_experience_on_success_max":7,
                "ginger_hoe_dirt_state_expected_after":0,
                "ginger_projection_status":"exact_from_native_crop_hit_with_hoe"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            locationId: "IslandWest",
            requiredItemId: "(O)829");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("harvest_ginger", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "false");
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_ginger", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectTreatsCurrentLocationBushAsSourceNotReceipt()
    {
        var snapshot = SpecialOrderCollectionSnapshot(
            BushSourceDomainState(),
            locationId: "IslandWest");

        var bushAvailability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "foraging.harvest_bushes" }, includeExecutorCalibrationOptions: true);
        var bushOption = Assert.Single(bushAvailability.Options);
        Assert.True(
            bushOption.EventCandidates.Length > 0,
            string.Join(";", bushOption.BlockingReasons) + "|missing=" +
            string.Join(",", bushOption.MissingStateFactors));
        var bushCandidate = Assert.Single(bushOption.EventCandidates);
        Assert.True(
            bushCandidate.Available,
            string.Join(";", bushCandidate.BlockReasons));
        Assert.Contains(bushCandidate.Parameters, parameter =>
            parameter.Name == "bush_output_context_tags_json" &&
            parameter.Value.Contains("category_resource", StringComparison.Ordinal));

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(
            candidate.Kind == "harvest_bush",
            candidate.Kind + ":" + string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "false");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_bush", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectTreatsGingerAsSourceNotReceipt()
    {
        var snapshot = SpecialOrderCollectionSnapshot(
            GingerSourceDomainState(),
            locationId: "IslandWest",
            acceptableContextTagSetsJson: "\"id_o_829\"");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.Equal("harvest_ginger", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "false");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_ginger", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectExcludesMismatchedGingerSourceUpstream()
    {
        var snapshot = SpecialOrderCollectionSnapshot(
            GingerSourceDomainState(),
            locationId: "IslandWest",
            acceptableContextTagSetsJson: "\"category_fish\"");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "special_order_matching_collect_action_not_ready_in_current_projection",
            candidate.BlockReasons);
    }

    [Fact]
    public void SpecialOrderCollectBlocksBushSourceWhenLiveOutputTagsDrift()
    {
        var initial = SpecialOrderCollectionSnapshot(
            BushSourceDomainState(),
            locationId: "IslandWest");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(initial, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);

        var drifted = SpecialOrderCollectionSnapshot(
            BushSourceDomainState("\"category_fish\""),
            locationId: "IslandWest");
        plan.StateHash = drifted.StateHash;
        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "special_order_collect_source_context_tags_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestTreatsScytheCropAsSourceBeforeDebrisReceipt()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "farm":{
              "crops":{"value":[{
                "tile_x":7,"tile_y":8,"ready_for_harvest":true,
                "harvest_item_id":"771","harvest_item_qualified_id":"(O)771",
                "harvest_item_category":-81,
                "harvest_context_tags":["category_greens","id_o_771"],
                "harvest_min_stack":1,"harvest_method":"Scythe",
                "harvest_experience_skill_id":"foraging",
                "harvest_experience_on_success_min":3,
                "harvest_experience_on_success_max":3,
                "harvest_experience_condition":"native_crop_harvest",
                "harvest_experience_projection_status":"exact"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            requiredItemId: "(O)771");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_crop_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderCollectTreatsMatchingScytheCropAsSource()
    {
        var snapshot = SpecialOrderCollectionSnapshot(
            """
            "farm":{
              "crops":{"value":[{
                "tile_x":7,"tile_y":8,"ready_for_harvest":true,
                "harvest_item_id":"771","harvest_item_qualified_id":"(O)771",
                "harvest_item_category":-81,
                "harvest_context_tags":["category_greens","id_o_771"],
                "harvest_min_stack":1,"harvest_method":"Scythe",
                "harvest_experience_skill_id":"foraging",
                "harvest_experience_on_success_min":3,
                "harvest_experience_on_success_max":3,
                "harvest_experience_condition":"native_crop_harvest",
                "harvest_experience_projection_status":"exact"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            acceptableContextTagSetsJson: "\"category_greens\"");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsGuaranteedGiantCropOutputAsSource()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "farm":{
              "resource_clumps":{"value":[{
                "tile_x":7,"tile_y":8,"width":3,"height":3,"health":3,
                "is_giant_crop":true,"giant_crop_id":"Pumpkin",
                "giant_crop_guaranteed_outputs_json":"[{\"qualified_item_id\":\"(O)276\",\"context_tags\":[\"category_vegetable\",\"id_o_276\"],\"quantity_min\":15,\"quantity_max\":21}]",
                "giant_crop_output_projection_status":"exact_unconditional_direct_outputs",
                "harvest_experience_skill_id":"luck",
                "harvest_experience_on_success_min":50,
                "harvest_experience_on_success_max":50,
                "harvest_experience_condition":"native_giant_crop_destroy",
                "harvest_experience_projection_status":"exact"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            requiredItemId: "(O)276");

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_giant_crop", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void ResourceCollectionQuestBindsGreenRainCoreOutputAsSource()
    {
        var snapshot = ResourceCollectionSnapshot(
            """
            "current_location":{
              "resource_clumps":{"value":[{
                "location_id":"Farm","tile_x":20,"tile_y":20,
                "runtime_type":"StardewValley.TerrainFeatures.ResourceClump",
                "parent_sheet_index":44,"width":2,"height":2,"health":3,
                "clear_kind":"green_rain_bush","clear_obstacle_executor_status":"ready",
                "tool_slot_index":0,"expected_tool_hits_to_clear":3,
                "expected_foraging_experience_delta":15,
                "expected_core_output_items_json":"[{\"qualified_item_id\":\"(O)Moss\",\"quantity\":2},{\"qualified_item_id\":\"(O)771\",\"quantity\":3}]",
                "expected_core_output_context_tag_sets_json":"[{\"qualified_item_id\":\"(O)Moss\",\"context_tags\":[\"id_o_moss\"]},{\"qualified_item_id\":\"(O)771\",\"context_tags\":[\"category_greens\"]}]",
                "output_distribution_status":"exact_seeded_core_no_secret_note_possible",
                "possible_secret_note_qualified_item_id":"(O)79",
                "unseen_secret_note_count":0,"total_secret_note_count":0,
                "secret_note_outer_roll_probability":0,
                "secret_note_inner_roll_probability":0,
                "secret_note_combined_probability":0,
                "secret_note_projection_status":"exact_no_unseen_secret_note",
                "native_contract":"axe_DoFunction_to_GameLocation.performToolAction_then_ResourceClump.destroy"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """,
            requiredItemId: "(O)Moss");

        var directCandidate = Assert.Single(Assert.Single(
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "foraging.clear_green_rain_bushes" },
                    includeExecutorCalibrationOptions: true)
                .Options).EventCandidates);
        Assert.True(directCandidate.Available, string.Join(";", directCandidate.BlockReasons));

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.break_current_location_resource_clump", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_acquisition_source_step" && parameter.Value == "true");
        Assert.Empty(item.BlockingReasons);
    }

    private static string BushSourceDomainState(
        string outputContextTagsJson = "\"category_resource\",\"id_o_390\"")
    {
        return
            """
            "current_location":{
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "large_terrain_features":{"value":[{
                "tile_x":20,"tile_y":20,"runtime_type":"StardewValley.TerrainFeatures.Bush",
                "bounding_tile_width":2,"bounding_tile_height":1,"is_bush":true,
                "bush_size":1,"bush_kind":"ordinary_berry","ready_for_harvest":true,
                "in_bloom":true,"tile_sheet_offset_before":1,"tile_sheet_offset_expected_after":0,
                "bush_harvest_status":"ready","bush_projection_status":"exact_from_native_bush_shake",
                "bush_output_qualified_item_id":"(O)390",
                "bush_output_context_tags":[BUSH_OUTPUT_CONTEXT_TAGS],
                "bush_output_quantity_min":1,"bush_output_quantity_max":1,
                "bush_output_quality":0,"bush_foraging_experience_on_success_min":1,
                "bush_foraging_experience_on_success_max":1,
                "bush_nut_key":"","bush_nut_collected_before":false,
                "bush_nut_collected_expected_after":false
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """
            .Replace(
                "BUSH_OUTPUT_CONTEXT_TAGS",
                outputContextTagsJson,
                StringComparison.Ordinal);
    }

    private static string GingerSourceDomainState()
    {
        return
            """
            "current_location":{
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "terrain_features":{"value":[{
                "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.HoeDirt",
                "hoe_dirt_state":1,"has_crop":true,"crop_is_forage":true,
                "forage_crop_id":"2","is_ginger":true,"ginger_harvest_status":"ready",
                "ginger_required_tool_kind":"Hoe","ginger_tool_slot_index":3,
                "ginger_energy_cost":1.4,"ginger_output_qualified_item_id":"(O)829",
                "ginger_output_context_tags":["id_o_829"],
                "ginger_output_quality":0,"ginger_output_quantity_min":1,
                "ginger_output_quantity_max":1,
                "ginger_foraging_experience_on_success_min":7,
                "ginger_foraging_experience_on_success_max":7,
                "ginger_hoe_dirt_state_expected_after":0,
                "ginger_projection_status":"exact_from_native_crop_hit_with_hoe"
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"IslandWest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """;
    }

    private static string MachineCollectionDomainState()
    {
        return
            """
            "farm":{
              "machines":{"value":[{
                "location_id":"Farm","location_kind":"farm_outdoor",
                "tile_x":20,"tile_y":20,"qualified_item_id":"(BC)12",
                "display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,
                "harvest_experience_raw":"","harvest_experience_entries":[],
                "harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]",
                "harvest_mastery_experience_delta":0,
                "harvest_experience_projection_status":"exact_no_configured_experience",
                "held_item":{
                  "item_id":"390","qualified_item_id":"(O)390","stack":1,"quality":0,
                  "sale_price":2,"maximum_stack_size":999,
                  "context_tags":["category_resource","id_o_390"]
                }
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "current_location":{
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """;
    }

    private static string FarmDebrisDomainState()
    {
        return
            """
            "farm":{
              "debris":{"value":[{
                "debris_index":0,"item_id":"390","qualified_item_id":"(O)390",
                "item_quality":0,"chunk_count":1,
                "item":{"item_id":"390","qualified_item_id":"(O)390","context_tags":["category_resource","id_o_390"]},
                "chunks":[{"chunk_index":0,"tile_x":20,"tile_y":20}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "current_location":{
              "debris":{"value":[{
                "debris_index":0,"item_id":"390","qualified_item_id":"(O)390",
                "item_quality":0,"chunk_count":1,
                "item":{"item_id":"390","qualified_item_id":"(O)390","context_tags":["category_resource","id_o_390"]},
                "chunks":[{"chunk_index":0,"tile_x":20,"tile_y":20}]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            "locations":{
              "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """;
    }

    private static string MonsterDropMiningDomainState()
    {
        return
            """
            "mining":{
              "current_mine":{"value":{"location_id":"UndergroundMine","mine_level":45,"mine_kind":"ordinary_mines"},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "tiles":{"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":7,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":7,"height":5,"blocked_rows":["1111111","1000001","1000001","1000001","1111111"]},"exits":[],"ladders":[],"shafts":[],"elevators":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "objects":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "resource_clumps":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "debris":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "monsters":{"value":[{
                "runtime_identity":"A1B2C3D4",
                "runtime_type":"StardewValley.Monsters.GreenSlime",
                "name":"Green Slime","tile_x":3,"tile_y":2,
                "possible_drop_qualified_item_ids":["(O)768"],
                "possible_drop_items":[{
                  "qualified_item_id":"(O)768",
                  "context_tags":["id_o_768","monster_drop"],
                  "context_tag_status":"exact_item_get_context_tags"
                }],
                "conditional_drop_catalog_keys":[],
                "guaranteed_drop_qualified_item_ids":["(O)768"],
                "drop_probability_rules":[{
                  "qualified_item_ids":["(O)768"],
                  "per_identity_chance":1.0,
                  "expected_quantity_per_kill":1.0,
                  "probability_status":"exact_current_state_formula",
                  "item_selection_status":"independent"
                }],
                "melee_attack_projections":[{
                  "slot_index":2,"can_defeat_with_this_weapon":true,
                  "terminal_effect":"defeat",
                  "expected_attacks_to_defeat":2.0,
                  "expected_active_damage_duration_ms":600.0,
                  "duration_status":"exact_active_melee_phase_excluding_movement"
                }]
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "monster_drop_catalogs":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "floor_objectives":{"value":{"must_kill_all_monsters_to_advance":false,"enemy_count":1,"ladder_has_spawned":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "reward_chests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "player_resources":{"value":{"health":100,"max_health":100,"energy":200,"current_time":1200,"selected_slot_index":0,"food_slots":[],"bomb_slots":[],"cardinal_movement":{"tile_duration_ms":100}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
              "completeness":{"value":{"status":"complete","unavailable_reasons":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            },
            """;
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope SpecialOrderCollectionSnapshot(
        string domainState,
        string locationId = "Farm",
        string acceptableContextTagSetsJson = "\"category_resource\"")
    {
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"LOCATION_ID","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":18,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":20,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "energy":{"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "skills_detail":{"value":{"foraging":{"level":8}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory_capacity":{"value":{"occupied_stacks":0,"empty_slots":12,"has_empty_slot":true},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              DOMAIN_STATE
              "quests":{
                "active_quests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":[{
                  "quest_key":"DebrisOrder","quest_name":"Debris Order","quest_state":"InProgress",
                  "objectives":[{
                    "description":"Collect resources","current_count":2,"max_count":25,
                    "runtime_type":"CollectObjective","fail_on_completion":false,"complete":false,
                    "per_type_fields":{"available":true,"acceptable_context_tag_sets":[ACCEPTABLE_CONTEXT_TAG_SETS]}
                  }],"rewards":[]
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "time":{"time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "menus":{"active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "world_progress":{
                "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """
            .Replace("LOCATION_ID", locationId, StringComparison.Ordinal)
            .Replace("ACCEPTABLE_CONTEXT_TAG_SETS", acceptableContextTagSetsJson, StringComparison.Ordinal)
            .Replace("DOMAIN_STATE", domainState, StringComparison.Ordinal));
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope ResourceCollectionSnapshot(
        string domainState,
        string locationId = "Farm",
        string requiredItemId = "(O)390")
    {
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"LOCATION_ID","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":18,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":20,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "energy":{"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "skills_detail":{"value":{"foraging":{"level":8}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory_capacity":{"value":{"occupied_stacks":0,"empty_slots":12,"has_empty_slot":true},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              DOMAIN_STATE
              "quests":{
                "active_quests":{"value":[{
                  "id":"96","quest_type":10,"runtime_type":"ResourceCollectionQuest",
                  "accepted":true,"completed":false,
                  "per_type_fields":{
                    "available":true,"item_id":"REQUIRED_ITEM_ID","target_npc":"Robin",
                    "number_collected":2,"number_required":10,
                    "target_count":10,"current_count":2
                  }
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "time":{"time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "menus":{"active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
              "world_progress":{
                "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """
            .Replace("LOCATION_ID", locationId, StringComparison.Ordinal)
            .Replace("REQUIRED_ITEM_ID", requiredItemId, StringComparison.Ordinal)
            .Replace("DOMAIN_STATE", domainState, StringComparison.Ordinal));
    }
}
