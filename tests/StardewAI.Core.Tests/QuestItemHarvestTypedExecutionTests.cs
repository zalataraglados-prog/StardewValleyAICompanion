using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void ItemHarvestQuestBindsMatchingGrabCropThroughExistingHarvestExecutor()
    {
        var snapshot = ItemHarvestQuestSnapshot("(O)24", "Grab", 0);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest_crop_tile", candidate.Kind);
        Assert.Equal("(O)24", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_acquisition_target_step" && parameter.Value == "true");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "quest_required_item_id" && parameter.Value == "(O)24");
    }

    [Fact]
    public void ItemHarvestQuestSupportsNativeCategoryRuleButRejectsScytheIntermediateStep()
    {
        var matchingGrab = ItemHarvestQuestSnapshot("-75", "Grab", -75);
        var grabCandidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(matchingGrab, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options).EventCandidates);
        Assert.True(grabCandidate.Available, string.Join(";", grabCandidate.BlockReasons));

        var scythe = ItemHarvestQuestSnapshot("-75", "Scythe", -75);
        var scytheCandidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(scythe, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options).EventCandidates);
        Assert.False(scytheCandidate.Available);
        Assert.Contains(
            "quest_matching_grab_harvest_not_ready_in_current_farm_projection",
            scytheCandidate.BlockReasons);
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope ItemHarvestQuestSnapshot(
        string requiredItemId,
        string harvestMethod,
        int harvestCategory)
    {
        return Snapshot(
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
                  "harvest_item_category":HARVEST_CATEGORY,
                  "harvest_context_tags":["category_vegetable"],
                  "harvest_min_stack":1,"harvest_method":"HARVEST_METHOD",
                  "harvest_experience_skill_id":"farming",
                  "harvest_experience_on_success_min":8,
                  "harvest_experience_on_success_max":8,
                  "harvest_experience_condition":"native_crop_harvest",
                  "harvest_experience_projection_status":"exact"
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "quests":{
                "active_quests":{"value":[{
                  "id":"91","quest_type":9,"runtime_type":"ItemHarvestQuest",
                  "accepted":true,"completed":false,
                  "per_type_fields":{"available":true,"item_id":"REQUIRED_ITEM_ID","target_count":5,"current_count":0}
                }],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "special_orders":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
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
              }
            }
            """
            .Replace("REQUIRED_ITEM_ID", requiredItemId, StringComparison.Ordinal)
            .Replace("HARVEST_METHOD", harvestMethod, StringComparison.Ordinal)
            .Replace("HARVEST_CATEGORY", harvestCategory.ToString(), StringComparison.Ordinal));
    }
}
