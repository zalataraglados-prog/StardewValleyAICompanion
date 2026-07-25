using System.Security.Cryptography;

namespace StardewAI.KnowledgeExporter;

internal static class ContentInventory
{
    public static List<ContentFileRecord> Build(string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Game content root not found: {contentRoot}");
        }

        return Directory.EnumerateFiles(contentRoot, "*.xnb", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildRecord(contentRoot, path))
            .ToList();
    }

    private static ContentFileRecord BuildRecord(string contentRoot, string path)
    {
        var relativePath = Path.GetRelativePath(contentRoot, path).Replace('\\', '/');
        var assetName = relativePath[..^Path.GetExtension(relativePath).Length];
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        return new ContentFileRecord(assetName, relativePath, stream.Length, hash);
    }
}
