using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void OrdinarySlayQuestBindsExactTargetToRollingMiningCombat()
    {
        var snapshot = QuestSlaySnapshot(
            activeQuests: """
            [{
              "id":"44","quest_type":4,"runtime_type":"SlayMonsterQuest","accepted":true,"completed":false,
              "per_type_fields":{
                "available":true,"monster_name":"Dust Spirit","target_npc":"Wizard",
                "number_to_kill":10,"number_killed":3,"target_count":10,"current_count":3,
                "ignore_farm_monsters":true
              }
            }]
            """,
            specialOrders: "[]",
            mineFamily: "ordinary_mines",
            monsters: """
            [
              {"runtime_identity":"near","runtime_type":"StardewValley.Monsters.Bat","name":"Bat","tile_x":2,"tile_y":2},
              {"runtime_identity":"quest-target","runtime_type":"StardewValley.Monsters.DustSpirit","name":"Dangerous Dust Spirit","tile_x":5,"tile_y":2}
            ]
            """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("mining_slay_monsters_plan_envelope", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "execution_option_id" && parameter.Value == "executor.combat_monster");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "target_runtime_identity" && parameter.Value == "quest-target");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_slay_target_step" && parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var queueItem = Assert.Single(queue.Items);
        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal("executor.combat_monster", queueItem.OptionId);
        Assert.Empty(queueItem.BlockingReasons);
    }

    [Fact]
    public void SpecialOrderSlayUsesAuthoritativeOrderMineFamily()
    {
        var snapshot = QuestSlaySnapshot(
            activeQuests: "[]",
            specialOrders: """
            [{
              "quest_key":"DesertFestivalMarlon1","quest_name":"Marlon's Challenge","quest_state":"InProgress",
              "special_rule":"","is_island_order":0,
              "objectives":[{
                "description":"Slay monsters","current_count":2,"max_count":10,"runtime_type":"SlayObjective",
                "fail_on_completion":false,"complete":false,
                "per_type_fields":{"available":true,"target_names":["Sludge"],"ignore_farm_monsters":false}
              }],
              "rewards":[{"runtime_type":"ObjectReward","available":true,"amount":35}]
            }]
            """,
            mineFamily: "skull_cavern",
            monsters: """
            [{"runtime_identity":"quest-target","runtime_type":"StardewValley.Monsters.GreenSlime","name":"Dangerous Sludge","tile_x":4,"tile_y":2}]
            """);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_target_location_family" && parameter.Value == "skull_cavern");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "target_name" && parameter.Value == "Dangerous Sludge");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_objective_index" && parameter.Value == "0");
    }

    [Fact]
    public void UnknownSpecialOrderSlayLocationFailsClosed()
    {
        Assert.Equal(
            string.Empty,
            MiningSlayMonsterCandidateBuilder.ResolveSpecialOrderLocationFamily("ModdedUnknownOrder"));
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope QuestSlaySnapshot(
        string activeQuests,
        string specialOrders,
        string mineFamily,
        string monsters)
    {
        return Snapshot(
            """
            {
              "player":{
                "location_id":{"value":"UndergroundMine","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "quests":{
                "active_quests":{"value":ACTIVE_QUESTS,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":SPECIAL_ORDERS,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completed_special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "mail_received":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "time":{
                "time":{"value":1200,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus":{
                "active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "world_progress":{
                "community_center":{"value":{},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "achievements":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "mining":{
                "current_mine":{"value":{"location_id":"UndergroundMine","mine_level":55,"mine_kind":"MINE_FAMILY"},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tiles":{"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":7,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":7,"height":5,"blocked_rows":["1111111","1000001","1000001","1000001","1111111"]},"exits":[],"ladders":[],"shafts":[],"elevators":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "objects":{"value":[{"tile_x":3,"tile_y":3,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "resource_clumps":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "monsters":{"value":MONSTERS,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "floor_objectives":{"value":{"must_kill_all_monsters_to_advance":false,"enemy_count":2,"ladder_has_spawned":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "reward_chests":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "player_resources":{"value":{"health":100,"max_health":100,"energy":200,"current_time":1200,"selected_slot_index":0,"food_slots":[],"bomb_slots":[],"cardinal_movement":{"tile_duration_ms":100}},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "completeness":{"value":{"status":"complete","unavailable_reasons":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """
            .Replace("ACTIVE_QUESTS", activeQuests, StringComparison.Ordinal)
            .Replace("SPECIAL_ORDERS", specialOrders, StringComparison.Ordinal)
            .Replace("MINE_FAMILY", mineFamily, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal));
    }
}
