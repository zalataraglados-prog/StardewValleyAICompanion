using System.IO.Compression;
using System.Security.Cryptography;

namespace StardewAI.LiveTrainingLoop;

public static class NativeSaveCheckpointArchive
{
    public static void Retain(SaveDirectoryFingerprint certified, string archivePath)
    {
        if (!Path.IsPathFullyQualified(certified.SlotPath) || !Path.IsPathFullyQualified(archivePath))
            throw new ArgumentException("Native checkpoint requires absolute paths.");
        var slot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(certified.SlotPath));
        archivePath = Path.GetFullPath(archivePath);
        var relative = Path.GetRelativePath(slot, archivePath);
        if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("Native checkpoint cannot write inside the observed save slot.");
        for (string? parent = Path.GetDirectoryName(archivePath); parent is not null; parent = Path.GetDirectoryName(parent))
            RejectReparsePoint(parent);

        if (!File.Exists(archivePath))
        {
            var current = NativeSaveBoundaryVerifier.Capture(Path.GetDirectoryName(slot)!, Path.GetFileName(slot));
            RequireCertified(certified, current.FileCount, current.TotalBytes, current.Sha256);
            // CreateFromDirectory never overwrites an existing file. A failed attempt is retained for diagnosis.
            ZipFile.CreateFromDirectory(slot, archivePath, CompressionLevel.Fastest, includeBaseDirectory: false);
        }
        Verify(certified, archivePath);
    }

    public static void Verify(SaveDirectoryFingerprint certified, string archivePath)
    {
        RejectReparsePoint(archivePath);
        using var archive = ZipFile.OpenRead(archivePath);
        var files = new List<SaveFileFingerprint>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.TrimEnd('/');
            if (name.Split('/').Any(part => part.Length == 0 || part is "." or ".." || part.Contains('\\') || part.Contains(':')) || !names.Add(name))
                throw new InvalidDataException("Native checkpoint has non-canonical or duplicate entry names.");
            if (entry.Name.Length == 0) continue;
            using var bytes = entry.Open();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            files.Add(new(name, entry.Length, hash));
        }
        RequireCertified(certified, files.Count, files.Sum(row => row.Length), NativeSaveBoundaryVerifier.ComputeAggregateSha256(files));
    }

    private static void RequireCertified(SaveDirectoryFingerprint certified, int count, long length, string hash)
    {
        if (certified.FileCount != count || certified.TotalBytes != length || !string.Equals(certified.Sha256, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native checkpoint bytes differ from the accepted saved boundary.");
    }

    private static void RejectReparsePoint(string path)
    {
        if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException("Native checkpoint must not access reparse points.");
    }
}
