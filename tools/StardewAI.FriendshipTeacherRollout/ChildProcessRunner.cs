using System.Diagnostics;

namespace StardewAI.FriendshipTeacherRollout;

public static class ChildProcessRunner
{
    public static async Task RunDotNetAsync(
        string workingDirectory,
        string assemblyPath,
        IReadOnlyList<string> arguments,
        string stdoutPath,
        string stderrPath,
        CancellationToken cancellationToken)
    {
        var isManagedAssembly = string.Equals(
            Path.GetExtension(assemblyPath),
            ".dll",
            StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo
        {
            FileName = isManagedAssembly ? "dotnet" : assemblyPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (isManagedAssembly)
            start.ArgumentList.Add(assemblyPath);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start LiveTrainingLoop.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        OperationCanceledException? cancellation = null;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException error)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            cancellation = error;
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        await File.WriteAllTextAsync(stdoutPath, stdout, CancellationToken.None);
        await File.WriteAllTextAsync(stderrPath, stderr, CancellationToken.None);
        if (cancellation is not null)
            throw cancellation;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "LiveTrainingLoop failed with exit code " + process.ExitCode +
                ". See " + stderrPath + ".");
        }
    }
}
