using System.Diagnostics;
using System.Text.Json;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyControlExecutionCli(AcquisitionRouteExecutionBindingInputs inputs, string outputPath,
        AcquisitionRouteExecutionBinding expected)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(BootstrapSelfTest).Assembly.Location);
        start.ArgumentList.Add("build-acquisition-route-execution-binding");
        foreach (var property in JsonSerializer.SerializeToElement(inputs, JsonDefaults.Options).EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString())) continue;
            start.ArgumentList.Add("--" + (property.Name == "before_snapshot_path" ? "snapshot" :
                property.Name.EndsWith("_path", StringComparison.Ordinal) ? property.Name[..^5].Replace('_', '-') : property.Name.Replace('_', '-')));
            start.ArgumentList.Add(property.Value.GetString()!);
        }
        start.ArgumentList.Add("--output");
        start.ArgumentList.Add(outputPath);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Control CLI process did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidOperationException("Control CLI self-test exceeded its one-minute bound.");
        }
        Require(process.ExitCode == 0, "Teacher-off control CLI failed: " + output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        var actual = CurrentTeacherFrontierSupport.Read<AcquisitionRouteExecutionBinding>(outputPath, "CLI control binding");
        Require(EqualJson(actual, expected), "The CLI must preserve the independently rebuilt control binding without requiring Teacher flags.");
    }
}
