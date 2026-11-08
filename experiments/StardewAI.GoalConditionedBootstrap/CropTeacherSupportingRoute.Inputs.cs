using System.Security.Cryptography;
using System.Text;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSupportingRoute
{
    public void ValidateInputsUnchanged() => inputs.Validate();

    private sealed class InputGuard
    {
        private readonly Dictionary<string, string> hashes;
        private readonly Dictionary<string, string> logicalHashes;
        internal InputGuard(string[] paths)
        {
            foreach (var path in paths) RequireRegularInput(path);
            hashes = paths.ToDictionary(path => path, CurrentTeacherFrontierSupport.HashFile, CropTeacherSeedTransferNativeOutcome.PathComparer);
            logicalHashes = paths.ToDictionary(path => path, LogicalHash, CropTeacherSeedTransferNativeOutcome.PathComparer);
        }
        internal IReadOnlyDictionary<string, string> ReadHashes() => new Dictionary<string, string>(hashes, CropTeacherSeedTransferNativeOutcome.PathComparer);
        internal void Validate()
        {
            // Never accept a warm materialization when validating a durable phase boundary.
            using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
            foreach (var (path, hash) in hashes)
            {
                RequireRegularInput(path);
                Require(CurrentTeacherFrontierSupport.HashFile(path) == hash && LogicalHash(path) == logicalHashes[path] &&
                        CurrentTeacherFrontierSupport.HashFile(path) == hash,
                    "Crop supporting route retained input changed: " + path);
            }
        }
    }

    private static string LogicalHash(string path) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(CurrentTeacherFrontierSupport.ReadArtifactText(path)))).ToLowerInvariant();

    private static void RequireRegularInput(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "Crop supporting route inputs must not cross reparse points.");
    }
}
