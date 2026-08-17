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

public sealed class AnimalManagementMainlineTests
{
    [Fact]
    public void ExplicitRenameFlowsThroughOneNativeAnimalQueryPrimitive()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { Intent("rename", P("target_name", "Hazel")) },
            includeExecutorCalibrationOptions: true);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("manage_animal", candidate.Kind);
        Assert.Contains(candidate.Parameters, row => row.Name == "animal_id" && row.Value == "42");
        Assert.Contains(candidate.Parameters, row => row.Name == "target_name" && row.Value == "Hazel");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("manage_animal", step.Kind);
        Assert.Contains("native_FarmAnimal_pet_and_AnimalQueryMenu_lifecycle_only", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.manage_animal", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("manage_animal", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void SaleWithoutIrreversibleConfirmationIsExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot(),
            new[] { Intent("sell") },
            includeExecutorCalibrationOptions: true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void RuntimeUsesNativeAnimalMenuWithoutDirectAnimalOrMoneyMutation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.AnimalManagement.cs"));

        Assert.Contains("CheckPetAnimal", source, StringComparison.Ordinal);
        Assert.Contains("CheckInspectAnimal", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("animal_management_target_name_exceeds_native_textbox_width", source, StringComparison.Ordinal);
        Assert.DoesNotContain("animal.Name =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("allowReproduction.Value = request", source, StringComparison.Ordinal);
        Assert.DoesNotContain("health.Value = -1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money +=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("adoptAnimal(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRuntimeRequestRoundTripsAllFourIntentFields()
    {
        var request = new TrainingExecutionRequest
        {
            AnimalManagementIntent = "move_home",
            AnimalManagementReason = "capacity_rebalance",
            ManagedAnimalId = 42,
            ExpectedAnimalNameBefore = "Bessie",
            TargetAnimalHomeBuildingType = "Barn",
            TargetAnimalHomeBuildingTileX = 20,
            TargetAnimalHomeBuildingTileY = 14,
            ExpectedTargetAnimalHomeOccupantCountBefore = 3,
            ExpectedTargetAnimalHomeCapacity = 8,
            ConfirmIrreversibleAnimalSale = false
        };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("move_home", roundTrip.AnimalManagementIntent);
        Assert.Equal(42, roundTrip.ManagedAnimalId);
        Assert.Equal(20, roundTrip.TargetAnimalHomeBuildingTileX);
        Assert.Equal(8, roundTrip.ExpectedTargetAnimalHomeCapacity);
    }

    [Fact]
    public void FourBranchRuntimeEvidenceAdmitsHighLevelAndNativePrimitive()
    {
        var highLevel = OptionCapabilityRegistrySource.GetRequired("animals.manage_animal");
        var primitive = OptionCapabilityRegistrySource.GetRequired("executor.manage_animal");

        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, highLevel.OutputTrainingGate);
        Assert.Contains("animals.manage_animal", OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(new[] { "EVD-252" }, primitive.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-252" }, primitive.OutputEvidenceIds);
    }

    private static OptionAvailabilityCandidate Intent(
        string managementIntent,
        params SmallModelActionParameter[] extra)
    {
        return new OptionAvailabilityCandidate
        {
            OptionId = "animals.manage_animal",
            ExplicitConfirmationGranted = true,
            Parameters = new[]
            {
                P("animal_id", "42"),
                P("management_intent", managementIntent),
                P("management_reason", "explicit_test_management")
            }.Concat(extra).ToArray()
        };
    }

    private static SmallModelActionParameter P(string name, string value) =>
        new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "time":{"time":{"value":1000,"status":"available"}},
              "player":{
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":9,"status":"available"},
                "tile_y":{"value":10,"status":"available"},
                "money":{"value":5000,"status":"available"}
              },
              "farm":{"animals":{"value":[{
                "animal_id":42,"runtime_type":"StardewValley.FarmAnimal","location_id":"Farm",
                "name":"Bessie","display_name":"Bessie","tile_x":10,"tile_y":10,
                "management_safe_slot_index":0,"management_query_status":"ready",
                "management_requires_initial_pet":false,"management_sell_price":1560,
                "management_can_toggle_reproduction":true,"management_allow_reproduction":true,
                "management_home_building_type":"Barn","management_home_building_tile_x":12,
                "management_home_building_tile_y":8,"management_home_indoor_location_id":"Barn1",
                "management_compatible_move_homes":[{
                  "building_type":"Barn","building_tile_x":20,"building_tile_y":14,
                  "indoor_location_id":"Barn2","is_under_construction":false,
                  "occupant_count":3,"capacity":8,"available_slots":5
                }]
              }],"status":"available"}},
              "locations":{
                "route_graph":{"value":{"edges":[]},"status":"available"},
                "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"}
              },
              "menus":{
                "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"},
                "menu_specific_state":{"value":{},"status":"available"}
              }
            }
            """)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-16T00:00:00Z",
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
}
