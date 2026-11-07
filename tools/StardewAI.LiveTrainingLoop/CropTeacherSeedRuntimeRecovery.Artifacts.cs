using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SamePath(string first, string second) => PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
    private static bool Contains(string parent, string child) => SamePath(parent, child) ||
        Path.GetFullPath(child).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed class Artifacts(IReadOnlyDictionary<string, string> initial)
    {
        internal Dictionary<string, string> Hashes { get; } = new(initial, PathComparer);
        internal string Pin(string path)
        {
            path = Path.GetFullPath(path);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Crop seed recovery input must be a regular file.");
            using var stream = File.OpenRead(path);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (Hashes.TryGetValue(path, out var previous) && previous != hash)
                throw new InvalidDataException("Crop seed recovery artifact changed: " + path);
            Hashes[path] = hash;
            return hash;
        }
        internal T Read<T>(string path)
        {
            Pin(path);
            var result = JsonSerializer.Deserialize<T>(ContentAddressedJsonArtifactReader.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Crop seed recovery artifact is empty: " + path);
            Pin(path);
            return result;
        }
        internal void Validate()
        {
            foreach (var path in Hashes.Keys.ToArray()) Pin(path);
        }
    }

    private static string RequiredPath(JsonObject node, string name)
    {
        var value = node[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidDataException("Original crop preparation lacks its retained " + name + ".");
        return Path.GetFullPath(value);
    }

    private static void ValidateRetainedLedger(Artifacts artifacts, JsonObject preparation, string name,
        string root, StrategyCommitmentLedger expected)
    {
        var path = RequiredPath(preparation, name);
        if (!SamePath(Path.GetDirectoryName(path)!, Path.Combine(root, "teacher-ledgers")))
            throw new InvalidDataException("Original crop preparation ledger escaped its evidence directory.");
        CropTeacherSeedRuntimeTransaction.RequireLedger(expected, artifacts.Read<StrategyCommitmentLedger>(path), name);
    }
}
