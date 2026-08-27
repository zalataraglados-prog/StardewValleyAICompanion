using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class BuildingPaintMainlineTests
{
    [Fact]
    public void ExactMouseReachablePaintIntentUsesSharedAppearanceExecutor()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { Intent(180) }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.Equal("paint_building_region", candidate.Kind);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var item = Assert.Single(new StardewAI.Core.Execution.ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.change_building_skin", item.OptionId);
        Assert.True(item.Status == "pending", string.Join(";", item.BlockingReasons));
        Assert.Equal("paint_building_region", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MouseUnreachableHueIsExcludedUpstream()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(Snapshot(), new[] { Intent(181) }, true);
        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void ProductionExecutorHasOneSharedAppearanceStateMachineAndNoDirectPaintMutation()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.BuildingSkins.cs"));
        var dispatch = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        Assert.Contains("ActiveBuildingAppearanceChange", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveBuildingPaint", source, StringComparison.Ordinal);
        Assert.Equal(1, dispatch.Split("pending.Request.OptionId == \"executor.change_building_skin\"", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("netBuildingPaintColor.Value =", source, StringComparison.Ordinal);
    }

    private static OptionAvailabilityCandidate Intent(int hue) => new()
    {
        OptionId = "buildings.paint", ActorIsHost = true,
        InvocationSource = OptionInvocationSource.PlayerCommand,
        ExplicitConfirmationGranted = true,
        Parameters = new[]
        {
            P("building_location_id", "Farm"), P("building_type", "Farmhouse"), P("building_tile_x", "59"), P("building_tile_y", "12"),
            P("paint_region_id", "Building"), P("paint_target_mode", "custom"), P("target_hue", hue.ToString()),
            P("target_saturation", "37"), P("target_lightness", "-30"), P("appearance_reason", "explicit_test_appearance_choice")
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {"player":{"location_id":{"value":"ScienceHouse","status":"available"},"tile_x":{"value":8,"status":"available"},"tile_y":{"value":20,"status":"available"},
        "building_skin_catalog":{"value":{"projection_status":"complete_live_native_building_skin_catalog","rows":[]},"status":"available"},
        "building_paint_catalog":{"value":{"projection_status":"complete_live_native_building_paint_catalog","rows":[{
        "building_identity":"Farm:Farmhouse:59,12","building_location_id":"Farm","building_type":"Farmhouse","building_tile_x":59,"building_tile_y":12,
        "paint_data_key":"Farmhouse","paint_region_count":3,"paint_region_index":0,"paint_region_id":"Building","paint_region_display_name":"Building",
        "hue_min":0,"hue_max":360,"saturation_min":0,"saturation_max":75,"lightness_min":-100,"lightness_max":40,"native_slider_logical_width":284,
        "hue_mouse_reachable_values":[180],"saturation_mouse_reachable_values":[37],"lightness_mouse_reachable_values":[-30],
        "default_displayed_hue":0,"default_displayed_saturation":75,"default_displayed_lightness":-30,
        "current_default":true,"current_hue":0,"current_saturation":0,"current_lightness":0,
        "service_location_id":"ScienceHouse","service_action_raw":"Carpenter","service_action_tile_x":8,"service_action_tile_y":19,
        "action_status":"ready_for_native_building_paint","native_contract":"GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.region_navigation->native_slider_or_default_clicks->BuildingPaintMenu.Ok"}]},"status":"available"}},
        "time":{"time":{"value":1200,"status":"available"}},"locations":{"route_graph":{"value":{"edges":[]},"status":"available"},"collision_grid":{"value":{"location_id":"ScienceHouse","width":64,"height":64,"notable_tiles":[]},"status":"available"}},"menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}}
        """)!;
        return new SnapshotEnvelope { SchemaVersion = "snapshot.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1, RealTimestamp = "2026-08-12T00:00:00Z", Completeness = "complete", State = state };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
