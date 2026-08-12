using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class BuildingConstructionMainlineTests
{
    [Fact]
    public void MissingPurposeBoundIntentProducesNoConstructionCandidate()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot(),
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "buildings.construct",
                    ActorIsHost = true
                }
            },
            true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void PurposeBoundIntentCompilesToSharedConstructionExecutor()
    {
        var intent = new OptionAvailabilityCandidate
        {
            OptionId = "buildings.construct",
            ActorIsHost = true,
            Parameters = new[]
            {
                Parameter("building_type", "Coop"),
                Parameter("placement_location_id", "Farm"),
                Parameter("construction_reason", "animal_capacity")
            }
        };
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { intent },
            true);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("construct_building", candidate.Kind);

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        Assert.Equal("construct_building", Assert.Single(plan.Steps).Kind);
        var queue = new StardewAI.Core.Execution.ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.construct_building", item.OptionId);
        Assert.Equal("pending", item.Status, ignoreCase: false);
        Assert.Equal("construct_building", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(":Farm:", Assert.Single(item.NormalizedCommand.Steps).Target);
    }

    [Fact]
    public void ConstructionCapabilityIsBoundedByItsRuntimeEvidence()
    {
        var highLevel = StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.GetRequired("buildings.construct");
        var executor = StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.GetRequired("executor.construct_building");

        Assert.Contains("EVD-248", highLevel.RuntimeEvidenceIds);
        Assert.Contains("EVD-248", executor.RuntimeEvidenceIds);
        Assert.Contains("buildings.construct", StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("executor.construct_building", StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Contains("Coop_on_Farm", highLevel.TrainingEvidenceScope);
    }

    [Fact]
    public void RemoteConstructionWithInsufficientResourcesIsExcludedBeforeRouting()
    {
        var snapshot = Snapshot();
        var root = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(snapshot.State))!.AsObject();
        root["player"]!["location_id"]!["value"] = "Farm";
        root["player"]!["building_construction_catalog"]!["value"]!["rows"]![0]!["action_status"] = "insufficient_money";
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.ToJsonString())!;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(state);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "buildings.construct",
                    ActorIsHost = true,
                    Parameters = new[]
                    {
                        Parameter("building_type", "Coop"),
                        Parameter("placement_location_id", "Farm"),
                        Parameter("construction_reason", "animal_capacity")
                    }
                }
            },
            true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "player":{
            "location_id":{"value":"ScienceHouse","status":"available"},
            "tile_x":{"value":8,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "money":{"value":9000,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "building_construction_catalog":{"value":{"projection_status":"complete_live_native_building_catalog","rows":[{
              "building_type":"Coop","display_name":"Coop","builder":"Robin","build_condition_met":true,
              "build_days":3,"build_cost":4000,"expected_money_before":9000,"expected_money_after":5000,
              "build_materials":[
                {"qualified_item_id":"(O)388","required_count":300,"available_count":350,"satisfied":true},
                {"qualified_item_id":"(O)390","required_count":100,"available_count":125,"satisfied":true}
              ],
              "service_location_id":"ScienceHouse","service_action_raw":"Carpenter","service_action_tile_x":10,"service_action_tile_y":10,
              "placement_location_id":"Farm","placement_tile_x":7,"placement_tile_y":12,
              "placement_verification":"static_native_predicates_passed_runtime_recheck_required","action_status":"ready_for_native_construction",
              "native_contract":"GameLocation.checkAction->ShowConstructOptions->CarpenterMenu.receiveLeftClick->tryToBuild->ConsumeResources->Building.FinishConstruction"
            }]},"status":"available"}
          },
          "time":{"time":{"value":900,"status":"available"}},
          "locations":{"route_graph":{"value":{"edges":[]},"status":"available"},"collision_grid":{"value":{"location_id":"ScienceHouse","width":64,"height":64,"notable_tiles":[]},"status":"available"}},
          "farm":{"material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","owner_player_id":123,"ownership_class":"actor_owned","actor_use_authorized":true,"slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":350},{"slot_index":1,"item_id":"390","qualified_item_id":"(O)390","stack":125}]}]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-12T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}
