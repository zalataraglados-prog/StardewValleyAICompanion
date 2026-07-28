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
              }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.mine_stone", item.OptionId);
        Assert.Empty(item.BlockingReasons);
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

    private static StardewAI.Contracts.State.SnapshotEnvelope ResourceCollectionSnapshot(
        string domainState,
        string locationId = "Farm")
    {
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"LOCATION_ID","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":18,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":20,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              DOMAIN_STATE
              "quests":{
                "active_quests":{"value":[{
                  "id":"96","quest_type":10,"runtime_type":"ResourceCollectionQuest",
                  "accepted":true,"completed":false,
                  "per_type_fields":{
                    "available":true,"item_id":"(O)390","target_npc":"Robin",
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
            .Replace("DOMAIN_STATE", domainState, StringComparison.Ordinal));
    }
}
