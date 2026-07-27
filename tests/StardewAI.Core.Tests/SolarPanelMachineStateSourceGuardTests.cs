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
}
