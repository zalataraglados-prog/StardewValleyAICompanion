using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class InvocationDeterministicBuildSession
{
    private static DeterministicBuildIdentityPart IdentityPart(
        string name,
        object? value,
        Type type)
    {
        if (type == typeof(string) &&
            name.EndsWith("Path", StringComparison.Ordinal) &&
            value is string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? Value(name, path)
                : File(name, path);
        }
        return Value(name, value);
    }

    private static DeterministicBuildIdentity BuildIdentity(
        string producerId,
        string producerVersion,
        IEnumerable<DeterministicBuildIdentityPart> identity)
    {
        var builder = new StringBuilder()
            .Append(SessionVersion).Append('\n')
            .Append(producerId).Append('\n')
            .Append(producerVersion).Append('\n');
        var inputs = new List<ValidationTraceDependencyInput>();
        foreach (var part in identity)
        {
            builder.Append(part.Name).Append('=');
            if (part.IsFile)
            {
                var fullPath = Path.GetFullPath(part.Identity);
                builder.Append(fullPath).Append('#');
                if (System.IO.File.Exists(fullPath))
                {
                    var contentSha256 = InvocationReadSession.HashFile(
                        fullPath,
                        out _);
                    builder.Append(contentSha256);
                    inputs.Add(new ValidationTraceDependencyInput
                    {
                        Name = part.Name,
                        ArtifactId = "file:" + contentSha256,
                        ArtifactKind = FileArtifactKind(part.Name),
                        ContentSha256 = contentSha256,
                        Path = fullPath,
                        ReuseScope = FileReuseScope(part.Name)
                    });
                }
                else
                {
                    builder.Append("missing");
                    inputs.Add(new ValidationTraceDependencyInput
                    {
                        Name = part.Name,
                        ArtifactId = "missing-file:" + Sha256(
                            Encoding.UTF8.GetBytes(fullPath)),
                        ArtifactKind = FileArtifactKind(part.Name),
                        Path = fullPath,
                        ReuseScope = FileReuseScope(part.Name)
                    });
                }
            }
            else
            {
                builder.Append(part.Identity);
                var contentSha256 = Sha256(
                    Encoding.UTF8.GetBytes(part.Identity));
                inputs.Add(new ValidationTraceDependencyInput
                {
                    Name = part.Name,
                    ArtifactId = "value:" + contentSha256,
                    ArtifactKind = "context_identity",
                    ContentSha256 = contentSha256,
                    ReuseScope = "invocation"
                });
            }
            builder.Append('\n');
        }

        return new DeterministicBuildIdentity(
            Sha256(Encoding.UTF8.GetBytes(builder.ToString())),
            inputs.ToArray());
    }

    private static string FileArtifactKind(string name) =>
        name.Contains("Snapshot", StringComparison.Ordinal)
            ? "snapshot_fact"
            : name.Contains("Ledger", StringComparison.Ordinal)
                ? "ledger"
                : "authority_artifact";

    private static string FileReuseScope(string name) =>
        name.Contains("Snapshot", StringComparison.Ordinal)
            ? "snapshot"
            : "invocation";

    private static string ContentSha256<T>(T value)
    {
        try
        {
            return Sha256(JsonSerializer.SerializeToUtf8Bytes(
                value,
                JsonDefaults.Options));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record DeterministicBuildIdentity(
    string Key,
    ValidationTraceDependencyInput[] Inputs);
