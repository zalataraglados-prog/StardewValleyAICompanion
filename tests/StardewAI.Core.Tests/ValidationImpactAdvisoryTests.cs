using System.Diagnostics;
using System.Text.Json;

namespace StardewAI.Core.Tests;

public sealed class ValidationImpactAdvisoryTests
{
    [Fact]
    public void RepresentativeDiffsKeepConservativeAdvisoryBoundaries()
    {
        var root = RepositoryRoot();
        var start = new ProcessStartInfo(
            OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        if (OperatingSystem.IsWindows())
        {
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
        }
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(
            root,
            "scripts",
            "Test-ValidationImpactAdvisory.ps1"));
        start.ArgumentList.Add("-ProjectRoot");
        start.ArgumentList.Add(root);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Unable to start PowerShell.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"Validation impact advisory tests failed: {error}");

        using var report = JsonDocument.Parse(output);
        Assert.Equal(
            "passed",
            report.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            9,
            report.RootElement.GetProperty("scenario_count").GetInt32());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
