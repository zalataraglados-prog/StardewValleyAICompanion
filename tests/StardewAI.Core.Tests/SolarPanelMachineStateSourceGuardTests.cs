using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class SolarPanelMachineStateSourceGuardTests
{
    [Fact]
    public void SolarPanelStatePreservesNativeWeatherGates()
    {
        var stateSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.SolarPanelState.cs"));
        var dispatchSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.SpecialMachinePrediction.cs"));
        var worldSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "WorldReadAdapter.cs"));
        var semanticsSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.MachineExecutionSemantics.cs"));
        var machinesSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.Machines.cs"));

        Assert.Contains(
            "solar_panel_day_update_weather.v1",
            stateSource);
        Assert.Contains("(BC)231", stateSource);
        Assert.Contains("(O)787", stateSource);
        Assert.Contains(
            "machine.GetType() != typeof(StardewValley.Object)",
            stateSource);
        Assert.Contains(
            "LocationWeather.TryGetValue(",
            stateSource);
        Assert.Contains(
            "location.IsOutdoors",
            stateSource);
        Assert.Contains("\"Inside\"", stateSource);
        Assert.Contains("\"Rain\"", stateSource);
        Assert.Contains("-2400", stateSource);
        Assert.Contains(
            "weather_dependent_no_guessed_multi_day_completion",
            stateSource);
        Assert.DoesNotContain(
            "GetWeatherForLocation(",
            stateSource);
        Assert.DoesNotContain("Game1.random", stateSource);
        Assert.Contains(
            "ReadMachineOutputAuthoritativeRouteSources",
            stateSource);
        Assert.Contains(
            "dayUpdateOutputs.Length != 1",
            stateSource);
        Assert.Contains(
            "native_solar_panel_output",
            stateSource);
        Assert.Contains(
            "output_authoritative_route_sources",
            machinesSource);

        Assert.Contains(
            "ReadSolarPanelSpecialState(",
            dispatchSource);
        Assert.Contains(
            "[\"weather_for_tomorrow\"]",
            worldSource);
        Assert.Contains(
            "[\"location_context_weather\"]",
            worldSource);
        Assert.DoesNotContain(
            "GetWeatherForLocation(",
            worldSource);

        Assert.Contains(
            "all_custom_output_method_count",
            semanticsSource);
        Assert.Contains(
            "day_update_custom_output_methods",
            semanticsSource);
        Assert.Contains(
            "output_collected_custom_output_methods",
            semanticsSource);
        Assert.Contains(
            "machine_put_down_custom_output_methods",
            semanticsSource);
        Assert.Contains(
            "unvetted_non_input_custom_output_methods",
            semanticsSource);
        Assert.Contains(
            "blocked_unvetted_custom_callbacks",
            semanticsSource);
    }

    [Fact]
    public void ReadySolarPanelCandidateCarriesExactNativeCallbackSource()
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            """
            {
              "player":{
                "location_id":{"value":"Farm","status":"available"},
                "tile_x":{"value":63,"status":"available"},
                "tile_y":{"value":15,"status":"available"},
                "inventory_capacity":{"value":{"occupied_stacks":0,"empty_slots":12,"has_empty_slot":true},"status":"available"},
                "inventory":{"value":[],"status":"available"}
              },
              "farm":{"machines":{"value":[{
                "location_id":"Farm","location_kind":"farm_outdoor","tile_x":64,"tile_y":15,
                "qualified_item_id":"(BC)231","display_name":"Solar Panel","ready_for_harvest":true,"minutes_until_ready":0,
                "machine_has_input":false,"machine_is_incubator":false,
                "harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],
                "harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,
                "harvest_experience_projection_status":"exact_no_configured_experience",
                "output_authoritative_route_sources":[{"route_kind":"native_solar_panel_output","source_id":"machine:(BC)231:OutputSolarPanel","qualified_item_id":"(O)787"}],
                "held_item":{"item_id":"787","qualified_item_id":"(O)787","stack":1,"quality":0,"sale_price":62,"maximum_stack_size":999},
                "loadable_inputs":[]
              }],"status":"available"}},
              "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
              "current_location":{"map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available"}},
              "locations":{
                "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},
                "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
              }
            }
            """,
            JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };

        var candidate = Assert.Single(
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.collect_machine_outputs" })
                .Options.Single().EventCandidates);
        Assert.True(candidate.Available,
            string.Join(";", candidate.BlockReasons));
        var sources = candidate.Parameters.Single(parameter =>
            parameter.Name == "authoritative_route_sources_json").Value;
        Assert.Contains("native_solar_panel_output", sources,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine:(BC)231:OutputSolarPanel",
            sources,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(
        params string[] segments)
    {
        var current = new DirectoryInfo(
            AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                new[] { current.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
