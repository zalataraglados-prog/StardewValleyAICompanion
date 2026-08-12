using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class BuildingSkinMainlineTests
{
    [Fact]
    public void MissingExactAppearanceIntentProducesNoCandidate()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot(),
            new[] { new OptionAvailabilityCandidate { OptionId = "buildings.change_skin", ActorIsHost = true } },
            true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void ExactAppearanceIntentCompilesToSharedNativeExecutor()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { Intent() },
            true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("change_building_skin", candidate.Kind);

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        Assert.Equal("change_building_skin", Assert.Single(plan.Steps).Kind);
        var item = Assert.Single(new StardewAI.Core.Execution.ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.change_building_skin", item.OptionId);
        Assert.Equal("pending", item.Status);
        Assert.Equal("change_building_skin", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains("Farm:Pet Bowl:49,40:skin:Stone Pet Bowl", Assert.Single(item.NormalizedCommand.Steps).Target);
    }

    [Fact]
    public void BuildingSkinCapabilityIsBoundedByRuntimeEvidence()
    {
        var highLevel = StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.GetRequired("buildings.change_skin");
        var executor = StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.GetRequired("executor.change_building_skin");

        Assert.Contains("EVD-249", highLevel.RuntimeEvidenceIds);
        Assert.Contains("EVD-249", executor.RuntimeEvidenceIds);
        Assert.Contains("buildings.change_skin", StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain("executor.change_building_skin", StardewAI.Contracts.Capabilities.OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Contains("Pet_Bowl_default_to_Stone", highLevel.TrainingEvidenceScope);
    }

    [Fact]
    public void ProductionExecutorUsesOnlyNativeMenuInput()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.BuildingSkins.cs"));

        Assert.DoesNotContain("skinId.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".SetSkin(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Color1Default.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Color2Default.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Color3Default.Value =", source, StringComparison.Ordinal);
    }

    private static OptionAvailabilityCandidate Intent() => new()
    {
        OptionId = "buildings.change_skin",
        ActorIsHost = true,
        Parameters = new[]
        {
            Parameter("building_location_id", "Farm"),
            Parameter("building_type", "Pet Bowl"),
            Parameter("building_tile_x", "49"),
            Parameter("building_tile_y", "40"),
            Parameter("target_skin_key", "Stone Pet Bowl"),
            Parameter("appearance_reason", "explicit_test_appearance_choice")
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "player":{
            "location_id":{"value":"ScienceHouse","status":"available"},
            "tile_x":{"value":8,"status":"available"},
            "tile_y":{"value":20,"status":"available"},
            "building_paint_catalog":{"value":{"projection_status":"complete_live_native_building_paint_catalog","rows":[]},"status":"available"},
            "building_skin_catalog":{"value":{"projection_status":"complete_live_native_building_skin_catalog","rows":[{
              "building_identity":"Farm:Pet Bowl:49,40","building_location_id":"Farm","building_type":"Pet Bowl",
              "building_tile_x":49,"building_tile_y":40,"permission_to_change_appearance":true,"can_be_painted":false,
              "entry_route":"direct_building_skin_menu","current_skin_key":"__default__","current_skin_id":"","current_skin_index":0,
              "target_skin_key":"Stone Pet Bowl","target_skin_id":"Stone Pet Bowl","target_skin_index":1,"target_skin_name":"Stone Pet Bowl",
              "available_skin_count":3,"available_skin_keys":["__default__","Stone Pet Bowl","Hay Pet Bowl"],
              "shortest_click_direction":"next","shortest_click_count":1,"skin_change_resets_all_paint_colors_to_default":true,
              "service_location_id":"ScienceHouse","service_action_raw":"Carpenter","service_action_tile_x":8,"service_action_tile_y":19,
              "action_status":"ready_for_native_skin_change",
              "native_contract":"GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.appearance(optional)->BuildingSkinMenu.shortest_exact_clicks->BuildingSkinMenu.Ok"
            }]} ,"status":"available"}
          },
          "time":{"time":{"value":1200,"status":"available"}},
          "locations":{"route_graph":{"value":{"edges":[]},"status":"available"},"collision_grid":{"value":{"location_id":"ScienceHouse","width":64,"height":64,"notable_tiles":[]},"status":"available"}},
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
