using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using Artifacts = StardewAI.LiveTrainingLoop.CropSeedRuntimeArtifacts;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SamePath(string first, string second) => PathComparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
    internal static bool Contains(string parent, string child) => SamePath(parent, child) ||
        Path.GetFullPath(child).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

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
