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

public sealed class QuestTypedExecutionTests
{
    [Fact]
    public void SocializeQuestCandidateUsesExactRemainingNpcProgress()
    {
        var quest = new QuestProgressRef
        {
            Id = "9",
            QuestType = 5,
            RuntimeType = "SocializeQuest",
            Accepted = true,
            PerTypeFields = new PerTypeQuestFields
            {
                Available = true,
                TotalToGreet = 28,
                WhoToGreet = new[] { "Robin", "Leah", "Linus" }
            }
        };

        var candidate = Assert.Single(QuestCandidateBuilder.BuildOrdinaryCandidates(new[] { quest }));

        Assert.Equal("greet_npcs", candidate.NextActionCategory);
        Assert.Equal(25, candidate.CurrentProgressCount);
        Assert.Equal(28, candidate.RequiredTargetCount);
    }

    [Fact]
    public void SpecialOrderSkipsFailOnCompletionObjective()
    {
        var order = new SpecialOrderProgressRef
        {
            QuestKey = "mixed",
            QuestState = "InProgress",
            SpecialRule = string.Empty,
            IsIslandOrder = 0,
            Objectives = new[]
            {
                new SpecialOrderObjectiveProgressRef
                {
                    RuntimeType = "SlayObjective",
                    CurrentCount = 0,
                    MaxCount = 1,
                    FailOnCompletion = true,
                    PerTypeFields = new PerTypeObjectiveFields { Available = true }
                },
                new SpecialOrderObjectiveProgressRef
                {
                    RuntimeType = "DeliverObjective",
                    CurrentCount = 2,
                    MaxCount = 10,
                    PerTypeFields = new PerTypeObjectiveFields
                    {
                        Available = true,
                        TargetName = "Robin",
                        AcceptableContextTagSets = new[] { "item_wood" }
                    }
                }
            },
            Rewards = new[]
            {
                new SpecialOrderRewardProgressRef { RuntimeType = "MoneyReward", Available = true }
            }
        };

        var candidate = Assert.Single(QuestCandidateBuilder.BuildSpecialOrderCandidates(new[] { order }));

        Assert.Equal(1, candidate.SelectedObjectiveIndex);
        Assert.Equal("deliver_to_target", candidate.NextActionCategory);
        Assert.DoesNotContain(candidate.BlockedDiagnostics, reason =>
            reason.Contains("fail_on_completion", StringComparison.Ordinal));
    }

    [Fact]
    public void DailyPlanCompilesQuestNpcCandidateToMoveAndNativeTerminal()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "quest:3:ItemDeliveryQuest:bound:social:talk:Robin",
            Kind = "quest_npc_interaction",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "ScienceHouse",
            TileX = 10,
            TileY = 11,
            EstimatedTicks = 180,
            EnergyCost = 0,
            Parameters = new[]
            {
                Parameter("npc_name", "Robin"),
                Parameter("npc_tile_x", "10"),
                Parameter("npc_tile_y", "10"),
                Parameter("stand_tile_x", "10"),
                Parameter("stand_tile_y", "11"),
                Parameter("route_distance_tiles", "4"),
                Parameter("quest_interaction_kind", "offer_item"),
                Parameter("slot_index", "2"),
                Parameter("qualified_item_id", "(O)388"),
                Parameter("quest_candidate_id", "quest:3:ItemDeliveryQuest"),
                Parameter("quest_family", "ordinary_quest"),
                Parameter("quest_id", "3"),
                Parameter("quest_runtime_type", "ItemDeliveryQuest"),
                Parameter("quest_objective_index", "-1"),
                Parameter("quest_expected_current_count", "0"),
                Parameter("quest_expected_target_count", "25")
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.quest");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("quest_npc_interact", plan.Steps[1].Kind);
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "quest_interaction_kind" && parameter.Value == "offer_item");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "quest_candidate_id" && parameter.Value == "quest:3:ItemDeliveryQuest");
    }

    [Fact]
    public void QuestExecutorIsRegisteredAndStructuredRequestRoundTrips()
    {
        var spec = new StardewAI.Core.OptionRegistry.OptionRegistry()
            .GetRequired("executor.quest_npc_interact");
        Assert.Equal("quest", spec.Domain);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.quest_npc_interact"));

        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.quest_npc_interact",
            QuestCandidateId = "special_order:RobinOrder",
            QuestFamily = "special_order",
            QuestKey = "RobinOrder",
            QuestRuntimeType = "SpecialOrder",
            QuestInteractionKind = "offer_item",
            QuestObjectiveIndex = 0,
            QuestExpectedCurrentCount = 2,
            QuestExpectedTargetCount = 10
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal("special_order:RobinOrder", roundTrip!.QuestCandidateId);
        Assert.Equal(0, roundTrip.QuestObjectiveIndex);
        Assert.Equal(10, roundTrip.QuestExpectedTargetCount);
    }

    [Fact]
    public void DailyPlanCompilesDropBoxCandidateToMoveAndNativeMenuTerminal()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "special_order:CropOrder:bound:quest_drop_box:CropOrder:0",
            Kind = "quest_drop_box_donation",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 10,
            TileY = 10,
            EstimatedTicks = 180,
            Parameters = new[]
            {
                Parameter("quest_drop_box_id", "crop_box"),
                Parameter("target_tile_x", "10"),
                Parameter("target_tile_y", "10"),
                Parameter("stand_tile_x", "10"),
                Parameter("stand_tile_y", "11"),
                Parameter("route_distance_tiles", "4"),
                Parameter("slot_index", "2"),
                Parameter("qualified_item_id", "(O)24"),
                Parameter("item_stack_before", "5"),
                Parameter("quest_drop_box_expected_accepted_count", "5"),
                Parameter("quest_candidate_id", "special_order:CropOrder"),
                Parameter("quest_family", "special_order"),
                Parameter("quest_key", "CropOrder"),
                Parameter("quest_runtime_type", "SpecialOrder"),
                Parameter("quest_objective_index", "0"),
                Parameter("quest_expected_current_count", "0"),
                Parameter("quest_expected_target_count", "10")
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.dropbox");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("quest_drop_box_donate", plan.Steps[1].Kind);
        Assert.Equal(10, plan.Steps[1].TargetTileX);
        Assert.Equal(10, plan.Steps[1].TargetTileY);
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "quest_drop_box_id" && parameter.Value == "crop_box");
    }

    [Fact]
    public void DropBoxExecutorIsRegisteredAndStructuredRequestRoundTrips()
    {
        var spec = new StardewAI.Core.OptionRegistry.OptionRegistry()
            .GetRequired("executor.quest_drop_box_donate");
        Assert.Equal("quest", spec.Domain);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.quest_drop_box_donate"));

        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.quest_drop_box_donate",
            QuestCandidateId = "special_order:CropOrder",
            QuestFamily = "special_order",
            QuestKey = "CropOrder",
            QuestRuntimeType = "SpecialOrder",
            QuestObjectiveIndex = 0,
            QuestExpectedCurrentCount = 2,
            QuestExpectedTargetCount = 10,
            QuestDropBoxId = "crop_box",
            QuestDropBoxSlotIndex = 4,
            QuestDropBoxQualifiedItemId = "(O)24",
            QuestDropBoxExpectedStackBefore = 5,
            QuestDropBoxExpectedAcceptedCount = 5
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal("crop_box", roundTrip!.QuestDropBoxId);
        Assert.Equal(4, roundTrip.QuestDropBoxSlotIndex);
        Assert.Equal(5, roundTrip.QuestDropBoxExpectedAcceptedCount);
    }

    [Fact]
    public void RuntimeQuestTerminalUsesNativeProbeAndNativeCheckAction()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("FirstNativeQuestOfferReceiver(npc, offeredItem)", source, StringComparison.Ordinal);
        Assert.Contains("quest.OnItemOfferedToNpc(npc, item, probe: true)", source, StringComparison.Ordinal);
        Assert.Contains("OnNpcSocialized(npc, probe: true)", source, StringComparison.Ordinal);
        Assert.Contains("callback(Game1.player, npc, item, true)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".currentCount.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".completed.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".donatedItems.Add", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDropBoxTerminalUsesNativeMenuWithoutDirectQuestMutation()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("StartQuestDropBoxDonation", source, StringComparison.Ordinal);
        Assert.Contains("GameLocation.checkAction_DropBox_handled", source, StringComparison.Ordinal);
        Assert.Contains("Game1.activeClickableMenu is not QuestContainerMenu menu", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y)", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("objective.currentCount.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("order.donatedItems.Add", source, StringComparison.Ordinal);
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }
}

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void ItemDeliveryQuestBindsToTypedNpcTerminalInsteadOfBlanketBlocker()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"ScienceHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":2,"item_id":"388","qualified_item_id":"(O)388","stack":25,"is_empty":false,"context_tags":["item_wood"]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "active_quests": {"value":[{
              "id":"3","quest_type":3,"runtime_type":"ItemDeliveryQuest","accepted":true,"completed":false,
              "per_type_fields":{"available":true,"target_npc":"Robin","item_id":"388","target_count":25,"current_count":0}
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "completed_special_orders": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "accepted_special_order_types": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "community_center": {"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "social_interaction": {"value":[{
              "name":"Robin","location_id":"ScienceHouse","tile_x":10,"tile_y":10,
              "master_data_present":true,"current_instance_loaded":true,"current_route_window_complete":true,
              "can_socialize_complete":true,"can_socialize":true,"is_villager":true,"is_monster":false,
              "is_invisible":false,"is_sleeping":false,"is_busy":false,"has_controller":false
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("quest_npc_interaction", candidate.Kind);
        Assert.DoesNotContain("quest_native_executor_not_implemented", candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_interaction_kind" && parameter.Value == "offer_item");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "slot_index" && parameter.Value == "2");
    }

    [Fact]
    public void DonateObjectiveBindsExactMapActionAndInventoryToTypedDropBoxTerminal()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":2,"item_id":"24","qualified_item_id":"(O)24","stack":5,"is_empty":false,"context_tags":["item_parsnip","category_vegetable"]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "drop_box_action_tiles": {"value":[{"tile_x":10,"tile_y":10,"action":"DropBox crop_box","action_type":"DropBox","box_id":"crop_box"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "active_quests": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders": {"value":[{
              "quest_key":"CropOrder","quest_name":"Crop Order","quest_state":"InProgress","special_rule":"","is_island_order":0,
              "objectives":[{
                "description":"Donate crops","current_count":0,"max_count":10,"runtime_type":"DonateObjective","fail_on_completion":false,"complete":false,
                "per_type_fields":{
                  "available":true,"acceptable_context_tag_sets":["category_vegetable"],"drop_box":"crop_box",
                  "drop_box_game_location":"Farm","resolved_drop_box_game_location":"Farm","drop_box_tile_x":10,"drop_box_tile_y":10,
                  "minimum_capacity":9,"confirmed":false
                }
              }],
              "rewards":[{"runtime_type":"MoneyReward","available":true,"amount":500}]
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "completed_special_orders": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "accepted_special_order_types": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "community_center": {"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("quest_drop_box_donation", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal(2, candidate.SlotIndex);
        Assert.Equal(5, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_drop_box_id" && parameter.Value == "crop_box");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "10");
    }

    [Fact]
    public void LostItemQuestBindsExactNativeSpawnedObjectPickup()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":20,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{
              "tile_x":20,"tile_y":20,"item_id":"788","qualified_item_id":"(O)788",
              "is_spawned_object":true,"spawned_object_pickup_status":"ready",
              "projected_total_quantity":1,"projected_harvest_quality":0,
              "projected_gatherer_duplicate":false,
              "foraging_experience_on_success_min":0,"foraging_experience_on_success_max":0,
              "farming_experience_on_success_min":0,"farming_experience_on_success_max":0,
              "harvest_experience_status":"exact","harvest_experience_basis":"quest_item_native_pickup"
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Forest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "active_quests": {"value":[{
              "id":"102","quest_type":9,"runtime_type":"LostItemQuest","accepted":true,"completed":false,
              "per_type_fields":{"available":true,"npc_name":"Linus","location_of_item":"Forest","item_id":"(O)788","tile_x":20,"tile_y":20,"item_found":false}
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "completed_special_orders": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "accepted_special_order_types": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "community_center": {"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var option = availability.Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("collect_spawned_object", candidate.Kind);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(20, candidate.TileX);
        Assert.Equal(20, candidate.TileY);
        Assert.Equal("(O)788", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "quest_next_action" && parameter.Value == "find_lost_item");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var queueItem = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.collect_spawned_object", queueItem.OptionId);
        Assert.Empty(queueItem.BlockingReasons);
    }
}
