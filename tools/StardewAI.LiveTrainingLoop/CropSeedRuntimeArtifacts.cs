using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

internal sealed class CropSeedRuntimeArtifacts(IReadOnlyDictionary<string, string> initial)
{
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
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
