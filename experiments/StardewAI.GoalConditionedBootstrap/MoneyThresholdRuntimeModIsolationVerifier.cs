using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyThresholdRuntimeModIsolationVerifier
{
    private static readonly KeyValuePair<string, string>[] ExpectedMods =
    {
        new("StardewAI.RuntimeTestHarness", "StardewAI.RuntimeTestHarness"),
        new("StardewAI.TransparentBridge", "StardewAI.TransparentBridge")
    };

    internal static void Verify(MoneyThresholdNativeOutcomeRunSummary summary)
    {
        Require(summary.SchemaVersion ==
                "money_threshold_native_outcome_run_summary.v2",
            "Historical Money run summaries do not contain runtime Mod provenance.");
        var root = RequiredDirectory(
            summary.IsolatedModsRootPath,
            "Money runtime isolated Mods root");
        Require(IsSha256(summary.IsolatedModsSha256Before) &&
                summary.IsolatedModsSha256Before ==
                    summary.IsolatedModsSha256After,
            "Money runtime isolated Mods root changed during collection.");

        var rootFiles = Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.TopDirectoryOnly)
            .ToArray();
        var rootDirectories = Directory.EnumerateDirectories(
                root,
                "*",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        var allowlist = summary.RuntimeModAllowlist ??
            Array.Empty<MoneyThresholdRuntimeModIdentity>();
        Require(rootFiles.Length == 0 &&
                rootDirectories.Length == ExpectedMods.Length &&
                allowlist.Length == ExpectedMods.Length,
            "Money runtime isolated Mods root has a missing or extra entry.");

        for (var index = 0; index < ExpectedMods.Length; index++)
        {
            var expected = ExpectedMods[index];
            var identity = allowlist[index] ?? throw new InvalidDataException(
                "Money runtime Mod allowlist entry is missing.");
            Require(identity.DirectoryName == expected.Key &&
                    identity.UniqueId == expected.Value &&
                    Path.GetFileName(rootDirectories[index]) == expected.Key &&
                    IsSha256(identity.ManifestSha256) &&
                    IsSha256(identity.ContentSha256) &&
                    !string.IsNullOrWhiteSpace(identity.Version),
                "Money runtime Mod allowlist identity is invalid.");

            var modRoot = RequiredDirectory(
                Path.Combine(root, expected.Key),
                $"Money runtime Mod {expected.Key}");
            var manifestPath = RequiredFile(
                Path.Combine(modRoot, "manifest.json"),
                $"Money runtime Mod {expected.Key} manifest");
            var manifest = CurrentTeacherFrontierSupport.Read<RuntimeModManifest>(
                manifestPath,
                $"Money runtime Mod {expected.Key} manifest");
            Require(manifest.UniqueId == expected.Value &&
                    manifest.UniqueId == identity.UniqueId &&
                    manifest.Version == identity.Version &&
                    CurrentTeacherFrontierSupport.HashFile(manifestPath) ==
                        identity.ManifestSha256 &&
                    HashDirectory(modRoot) == identity.ContentSha256,
                "Money runtime Mod manifest or content identity drifted.");
        }

        Require(HashDirectory(root) == summary.IsolatedModsSha256Before,
            "Money runtime isolated Mods root content drifted.");
    }

    internal static MoneyThresholdRuntimeModIdentity Describe(
        string isolatedModsRoot,
        string directoryName)
    {
        var root = RequiredDirectory(isolatedModsRoot, "Runtime Mods root");
        var expected = ExpectedMods.SingleOrDefault(value =>
            value.Key == directoryName);
        Require(!string.IsNullOrEmpty(expected.Key),
            "Runtime Mod directory is not allowlisted.");
        var modRoot = RequiredDirectory(
            Path.Combine(root, directoryName),
            $"Runtime Mod {directoryName}");
        var manifestPath = RequiredFile(
            Path.Combine(modRoot, "manifest.json"),
            $"Runtime Mod {directoryName} manifest");
        var manifest = CurrentTeacherFrontierSupport.Read<RuntimeModManifest>(
            manifestPath,
            $"Runtime Mod {directoryName} manifest");
        Require(manifest.UniqueId == expected.Value &&
                !string.IsNullOrWhiteSpace(manifest.Version),
            "Runtime Mod manifest identity is invalid.");
        return new MoneyThresholdRuntimeModIdentity(
            directoryName,
            manifest.UniqueId,
            manifest.Version,
            CurrentTeacherFrontierSupport.HashFile(manifestPath),
            HashDirectory(modRoot));
    }

    internal static string HashDirectory(string path)
    {
        var root = RequiredDirectory(path, "Directory hash root")
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = root + Path.DirectorySeparatorChar;
        AssertNoReparseDirectories(root);
        var rows = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(file =>
            {
                Require(file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                    "File escaped directory hash root.");
                var info = new FileInfo(file);
                Require((info.Attributes & FileAttributes.ReparsePoint) == 0,
                    "Directory hash input contains a reparse point.");
                var relative = file[(root.Length + 1)..]
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                return string.Join(
                    '\t',
                    relative,
                    info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CurrentTeacherFrontierSupport.HashFile(file));
            });
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))))
            .ToLowerInvariant();
    }

    private static void AssertNoReparseDirectories(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            foreach (var directory in Directory.EnumerateDirectories(
                pending.Pop(),
                "*",
                SearchOption.TopDirectoryOnly))
            {
                Require((File.GetAttributes(directory) &
                        FileAttributes.ReparsePoint) == 0,
                    "Directory hash input contains a reparse-point directory.");
                pending.Push(directory);
            }
        }
    }

    private static string RequiredDirectory(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(label + " is missing: " + fullPath);
        Require((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0,
            label + " must not be a reparse point.");
        return fullPath;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(label + " is missing.", fullPath);
        Require((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0,
            label + " must not be a reparse point.");
        return fullPath;
    }

    private sealed class RuntimeModManifest
    {
        [JsonPropertyName("UniqueID")]
        public string UniqueId { get; set; } = string.Empty;

        [JsonPropertyName("Version")]
        public string Version { get; set; } = string.Empty;
    }
}
