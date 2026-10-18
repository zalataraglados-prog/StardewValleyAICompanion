namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRuntimeModIsolation()
    {
        var valid = MoneyRuntimeModFixture("valid");
        MoneyThresholdRuntimeModIsolationVerifier.Verify(valid);
        if (OperatingSystem.IsWindows())
        {
            var powerShellHash = PowerShellDirectoryHash(
                valid.IsolatedModsRootPath);
            Require(powerShellHash == valid.IsolatedModsSha256Before,
                "PowerShell and C# runtime Mod directory hashes diverged: " +
                powerShellHash + " != " + valid.IsolatedModsSha256Before);
        }

        var historical = MoneyRuntimeModFixture("historical");
        historical.SchemaVersion = "money_threshold_native_outcome_run_summary.v1";
        ExpectMoneyRuntimeModIsolationFailure(
            historical,
            "Historical Money run summary was silently upgraded.");

        var missing = MoneyRuntimeModFixture(
            "missing",
            includeHarness: false);
        ExpectMoneyRuntimeModIsolationFailure(
            missing,
            "Missing Money runtime Mod was admitted.");

        var extra = MoneyRuntimeModFixture(
            "extra",
            includeExtra: true);
        ExpectMoneyRuntimeModIsolationFailure(
            extra,
            "Extra Money runtime Mod was admitted.");

        var drifted = MoneyRuntimeModFixture("drifted");
        File.AppendAllText(
            Path.Combine(
                drifted.IsolatedModsRootPath,
                "StardewAI.TransparentBridge",
                "StardewAI.TransparentBridge.dll"),
            "drift");
        ExpectMoneyRuntimeModIsolationFailure(
            drifted,
            "Drifted Money runtime Mod content was admitted.");

        var mismatched = MoneyRuntimeModFixture("mismatched");
        mismatched.RuntimeModAllowlist[0] =
            mismatched.RuntimeModAllowlist[0] with
            {
                UniqueId = "Unexpected.GameplayMod"
            };
        ExpectMoneyRuntimeModIsolationFailure(
            mismatched,
            "Mismatched Money runtime Mod identity was admitted.");
    }

    private static MoneyThresholdNativeOutcomeRunSummary MoneyRuntimeModFixture(
        string suffix,
        bool includeHarness = true,
        bool includeExtra = false)
    {
        var root = TempArtifactRoot(
            "money-runtime-mod-isolation",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if (includeHarness)
        {
            WriteRuntimeModFixture(
                root,
                "StardewAI.RuntimeTestHarness",
                "StardewAI.RuntimeTestHarness");
        }
        WriteRuntimeModFixture(
            root,
            "StardewAI.TransparentBridge",
            "StardewAI.TransparentBridge");
        if (includeExtra)
        {
            WriteRuntimeModFixture(
                root,
                "UnexpectedGameplayMod",
                "Unexpected.GameplayMod");
        }

        var identities = new[]
            {
                "StardewAI.RuntimeTestHarness",
                "StardewAI.TransparentBridge"
            }
            .Where(directory => Directory.Exists(Path.Combine(root, directory)))
            .Select(directory =>
                MoneyThresholdRuntimeModIsolationVerifier.Describe(
                    root,
                    directory))
            .ToArray();
        var rootSha256 =
            MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(root);
        return new MoneyThresholdNativeOutcomeRunSummary
        {
            SchemaVersion = "money_threshold_native_outcome_run_summary.v2",
            IsolatedModsRootPath = root,
            IsolatedModsSha256Before = rootSha256,
            IsolatedModsSha256After = rootSha256,
            RuntimeModAllowlist = identities
        };
    }

    private static void WriteRuntimeModFixture(
        string root,
        string directoryName,
        string uniqueId)
    {
        var directory = Path.Combine(root, directoryName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "manifest.json"),
            $$"""
            {
              "UniqueID": "{{uniqueId}}",
              "Version": "0.1.0"
            }
            """);
        File.WriteAllText(
            Path.Combine(directory, directoryName + ".dll"),
            "fixture-build:" + uniqueId);
    }

    private static void ExpectMoneyRuntimeModIsolationFailure(
        MoneyThresholdNativeOutcomeRunSummary summary,
        string message)
    {
        var rejected = false;
        try
        {
            MoneyThresholdRuntimeModIsolationVerifier.Verify(summary);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or IOException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private static string PowerShellDirectoryHash(string root)
    {
        var repositoryRoot = Environment.CurrentDirectory;
        var hashScriptPath = Path.Combine(
            repositoryRoot,
            "scripts",
            "Get-RuntimeDirectoryContentHash.ps1");
        Require(File.Exists(hashScriptPath),
            "Runtime directory hash script is missing for compatibility test.");
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(hashScriptPath);
        process.StartInfo.ArgumentList.Add("-Path");
        process.StartInfo.ArgumentList.Add(root);
        Require(process.Start(), "Unable to start Windows PowerShell 5.1.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd();
        Require(process.WaitForExit(30_000) && process.ExitCode == 0,
            "Windows PowerShell directory hash failed: " + error);
        return output;
    }
}
