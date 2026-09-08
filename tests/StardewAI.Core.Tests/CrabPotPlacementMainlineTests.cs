using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CrabPotPlacementMainlineTests
{
    private const string Fingerprint = "crab-pot-placement-fingerprint";
    private const string ProductionSignature = "Beach|0.2|ocean|152,153";
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)710)->CrabPot.placementAction(owner=current_player)";

    [Fact]
    public void ExactWaterPlacementCompiles()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_crab_pot", step.StepType);
        Assert.Equal("Beach(12,10):slot2:(O)710", step.Target);
        Assert.Contains("StardewValley.Objects.CrabPot", step.ExpectedEffect);
        Assert.Contains("owner=current_player", step.ExpectedEffect);
    }

    [Fact]
    public void MasterAnglerLifecycleBuildsCapacityThroughExistingPlacementPrimitive()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" });
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("place_crab_pot", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "master_angler_target_qualified_item_ids_json" &&
            parameter.Value == "[\"(O)372\"]");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("place_crab_pot", Assert.Single(plan.Steps).Kind);
        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.place_crab_pot", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void RemoteMissingHabitatRoutesBeforeRebindingExactPlacement()
    {
        var snapshot = RemoteSnapshot(hasExistingPot: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" });
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "crab_pot_route_purpose" &&
            parameter.Value == "place_for_missing_species");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "crab_pot_target_location" &&
            parameter.Value == "Beach");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("traverse_connector", Assert.Single(plan.Steps).Kind);
        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void RemoteExistingPotSuppressesDuplicatePlacementAndRoutesToService()
    {
        var snapshot = RemoteSnapshot(hasExistingPot: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" });
        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "crab_pot_route_purpose" &&
            parameter.Value == "service_existing_crab_pot");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "crab_pot_remote_service_status" &&
            parameter.Value == "bait_required");
        Assert.DoesNotContain(
            Assert.Single(availability.Options).EventCandidates,
            row => row.Kind == "place_crab_pot");
    }

    [Fact]
    public void NearDeadlineExistingPotDoesNotSuppressAdditionalCapacity()
    {
        var snapshot = RemoteSnapshot(hasExistingPot: true, totalDays: 222);
        var candidates = Assert.Single(new CandidateOptionAvailabilityEvaluator()
                .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" })
                .Options)
            .EventCandidates
            .Where(candidate => candidate.Available)
            .ToArray();

        Assert.Contains(candidates, candidate => candidate.Parameters.Any(parameter =>
            parameter.Name == "crab_pot_route_purpose" &&
            parameter.Value == "service_existing_crab_pot"));
        Assert.Contains(candidates, candidate => candidate.Parameters.Any(parameter =>
            parameter.Name == "crab_pot_route_purpose" &&
            parameter.Value == "place_for_missing_species"));
    }

    [Theory]
    [InlineData(0, true, 1234, ProductionSignature, "place_crab_pot_inventory_identity_drifted")]
    [InlineData(2, false, 1234, ProductionSignature, "place_crab_pot_exact_water_tile_not_native_legal")]
    [InlineData(2, true, 5678, ProductionSignature, "place_crab_pot_owner_identity_drifted")]
    [InlineData(2, true, 1234, "drifted", "place_crab_pot_production_context_drifted")]
    public void StaleInventoryWaterOwnerOrProductionContextFailsClosed(
        int stack,
        bool legalTile,
        long ownerId,
        string productionSignature,
        string expectedReason)
    {
        var snapshot = Snapshot(stack, legalTile, ownerId);
        var request = Request(snapshot, productionSignature);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void ReasonNativeContractAndProductionSignatureAreMandatory()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "crab_pot_placement_reason" and not "native_contract" and not "production_signature")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("place_crab_pot_reason_required", item.BlockingReasons);
        Assert.Contains("place_crab_pot_production_signature_required", item.BlockingReasons);
        Assert.Contains("place_crab_pot_native_contract_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void PrimitiveIsCalibrationOnlyAndReusesPlacementMovementAndCollectSystems()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_crab_pot");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(
            ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_crab_pot").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPotPlacement.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var collect = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPots.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.CrabPotPlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility.tryToPlaceItem(", runtime, StringComparison.Ordinal);
        Assert.Contains("\"place_crab_pot\" => ExecutePlaceCrabPot", dispatcher, StringComparison.Ordinal);
        Assert.Contains("StartCrabPotCollect", collect, StringComparison.Ordinal);
        Assert.Contains("CrabPot.IsValidCrabPotLocationTile", projection, StringComparison.Ordinal);
        Assert.Contains("native_order_catch_rows", projection, StringComparison.Ordinal);
        Assert.Contains("JsonPropertyName(\"qualified_item_id\")", projection, StringComparison.Ordinal);
        Assert.Contains("JsonPropertyName(\"base_chance\")", projection, StringComparison.Ordinal);
        Assert.Contains("typeof(CrabPot).FullName", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeBaitLoadingIsRegisteredSeparatelyFromPlacement()
    {
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.load_crab_pot_bait", out _));
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.load_crab_pot_bait");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(ImplementationEngineIds.FarmMachine, OptionImplementationCatalog.GetRequired("executor.load_crab_pot_bait").PrimaryEngineId);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot, string productionSignature = ProductionSignature) => new()
    {
        ModelOutputId = "crab-pot-placement-test",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "test",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.test",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "place-crab-pot",
                OptionId = "executor.place_crab_pot",
                Rationale = "establish one purpose-bound ocean catch endpoint",
                Parameters = new[]
                {
                    P("target_location", "Beach"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("qualified_item_id", "(O)710"), P("expected_owner_player_id", "1234"),
                    P("placement_projection_fingerprint", Fingerprint), P("production_signature", productionSignature),
                    P("native_contract", NativeContract), P("crab_pot_placement_reason", "task_and_production_capacity")
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(int stack, bool legalTile, long ownerId)
    {
        var ranges = legalTile
            ? $$"""[{"y":10,"start_x":12,"end_x":12,"production_signature":"{{ProductionSignature}}","fish_area_id":"Beach","base_junk_chance":0.2,"native_order_catch_rows":[{"qualified_item_id":"(O)372","base_chance":0.25}],"conservative_serviced_probability_status":"complete_native_supported_bait_lower_bound","conservative_serviced_outcome_rows":[{"qualified_item_id":"(O)372","single_cycle_probability":0.25}]}]"""
            : "[]";
        var json = """
        {
          "time":{"total_days":{"value":0,"status":"available"}},
          "player":{
            "location_id":{"value":"Beach","status":"available"},
            "tile_x":{"value":11,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(O)710","stack":STACK}],"status":"available"},
            "crab_pot_placement":{"value":{
              "projection_status":"complete_inventory_crab_pots_across_loaded_persistent_locations",
              "static_projection_fingerprint":"FINGERPRINT","owner_player_id":OWNER,
              "native_runtime_contract":"NATIVE_CONTRACT",
              "rows":[{"inventory_slot_index":2,"qualified_item_id":"(O)710","stack":STACK,"locations":[{
                "location_id":"Beach","placement_probe_status":"native_legal_water_tiles_available","static_legal_tile_ranges":RANGES
              }]}]
            },"status":"available"},
            "crab_pot_network":{"value":{
              "projection_status":"complete_crab_pots_across_loaded_persistent_locations",
              "placed_pot_count":0,"rows":[]
            },"status":"available"}
          },
          "current_location":{"objects":{"value":[],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Beach","width":100,"height":100,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """
        .Replace("FINGERPRINT", Fingerprint)
        .Replace("OWNER", ownerId.ToString())
        .Replace("STACK", stack.ToString())
        .Replace("RANGES", ranges)
        .Replace("NATIVE_CONTRACT", NativeContract);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        var fishRows = Enumerable.Range(0, 72)
            .Select(index => new
            {
                item_id = index == 0 ? "372" : "test_" + index,
                qualified_item_id = index == 0
                    ? "(O)372"
                    : "(O)test_" + index,
                caught = index != 0
            })
            .ToArray();
        state["world_progress"] = JsonSerializer.SerializeToElement(new
        {
            fish_collection_progress = new
            {
                value = new
                {
                    eligible_species_count = 72,
                    caught_eligible_species_count = 71,
                    missing_species_count = 1,
                    missing_item_ids = new[] { "372" },
                    items = fishRows
                },
                status = "available"
            }
        });
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-22T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SnapshotEnvelope RemoteSnapshot(
        bool hasExistingPot,
        int totalDays = 0)
    {
        var local = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var root = JsonNode.Parse(JsonSerializer.Serialize(local.State))!
            .AsObject();
        root["player"]!["location_id"]!["value"] = "Farm";
        root["time"]!["total_days"]!["value"] = totalDays;
        root["player"]!["tile_x"]!["value"] = 1;
        root["player"]!["tile_y"]!["value"] = 5;
        root["current_location"]!["map"] = Field(new JsonObject
        {
            ["location_id"] = "Farm",
            ["width"] = 100,
            ["height"] = 100
        });
        root["locations"]!["collision_grid"]!["value"]!["location_id"] =
            "Farm";
        root["locations"]!["route_graph"] = Field(new JsonObject
        {
            ["edges"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "building_door",
                    ["from_location"] = "Farm",
                    ["from_x"] = 2,
                    ["from_y"] = 5,
                    ["target_location"] = "Beach",
                    ["target_x"] = 11,
                    ["target_y"] = 10,
                    ["resolved"] = true
                }
            }
        });
        root["locations"]!["route_action_branch_coverage"] = Field(
            new JsonObject { ["rows"] = new JsonArray() });
        root["locations"]!["route_connectors"] = Field(new JsonObject
        {
            ["location_id"] = "Farm",
            ["connectors"] = new JsonArray
            {
                new JsonObject
                {
                    ["tile_x"] = 2,
                    ["tile_y"] = 5,
                    ["kind"] = "building_door",
                    ["target_location"] = "Beach",
                    ["target_x"] = 11,
                    ["target_y"] = 10,
                    ["resolved"] = true
                }
            }
        });

        var networkRows = new JsonArray();
        if (hasExistingPot)
        {
            networkRows.Add(new JsonObject
            {
                ["location_id"] = "Beach",
                ["tile_x"] = 12,
                ["tile_y"] = 10,
                ["exact_base_crab_pot"] = true,
                ["service_status"] = "bait_required",
                ["current_output_collection_eligible"] = false,
                ["current_output_qualified_item_id"] = string.Empty,
                ["production_signature"] = ProductionSignature,
                ["possible_qualified_item_ids"] = new JsonArray("(O)372"),
                ["conservative_serviced_probability_status"] =
                    "complete_native_supported_bait_lower_bound",
                ["conservative_serviced_outcome_rows"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["qualified_item_id"] = "(O)372",
                        ["single_cycle_probability"] = 0.25
                    }
                },
                ["production_domain_complete"] = true
            });
        }
        root["player"]!["crab_pot_network"] = Field(new JsonObject
        {
            ["projection_status"] =
                "complete_crab_pots_across_loaded_persistent_locations",
            ["placed_pot_count"] = networkRows.Count,
            ["rows"] = networkRows
        });

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            root.ToJsonString())!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 2,
            RealTimestamp = "2026-09-07T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static JsonObject Field(JsonNode value) => new()
    {
        ["value"] = value,
        ["status"] = "available"
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
