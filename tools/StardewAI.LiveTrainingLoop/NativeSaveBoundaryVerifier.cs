using System.Security.Cryptography;
using System.Text;

namespace StardewAI.LiveTrainingLoop;

public sealed record SaveFileFingerprint(
    string RelativePath,
    long Length,
    string Sha256);

public sealed record SaveDirectoryFingerprint(
    string SlotPath,
    int FileCount,
    long TotalBytes,
    string Sha256);

public sealed record NativeSaveBoundaryObservation(
    string InitialDayKey,
    string CurrentDayKey,
    bool DayAdvanced,
    bool SaveChanged,
    bool Verified,
    string InitialSaveSha256,
    string CurrentSaveSha256);

public static class NativeSaveBoundaryVerifier
{
    public static async Task<SaveDirectoryFingerprint> CaptureWithRetryAsync(
        string saveIsolationPath,
        string saveSlot,
        int maxAttempts = 20,
        int retryDelayMs = 250,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return Capture(saveIsolationPath, saveSlot);
            }
            catch (Exception ex) when (
                attempt < maxAttempts &&
                ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }
    }

    public static SaveDirectoryFingerprint Capture(
        string saveIsolationPath,
        string saveSlot)
    {
        if (string.IsNullOrWhiteSpace(saveIsolationPath) ||
            string.IsNullOrWhiteSpace(saveSlot) ||
            !string.Equals(Path.GetFileName(saveSlot), saveSlot, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "native_save_boundary_requires_exact_save_root_and_slot");
        }

        var root = Path.GetFullPath(saveIsolationPath);
        var slotPath = Path.GetFullPath(Path.Combine(root, saveSlot));
        var relativeSlot = Path.GetRelativePath(root, slotPath);
        if (relativeSlot == "." ||
            relativeSlot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeSlot) ||
            !Directory.Exists(slotPath))
        {
            throw new InvalidOperationException(
                "native_save_boundary_slot_not_found_under_isolation_root");
        }

        RejectReparsePoint(slotPath);
        foreach (var directory in Directory.EnumerateDirectories(
                     slotPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            RejectReparsePoint(directory);
        }

        var files = Directory.EnumerateFiles(
                slotPath,
                "*",
                SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(slotPath, path), StringComparer.Ordinal)
            .Select(path =>
            {
                RejectReparsePoint(path);
                using var stream = File.OpenRead(path);
                return new SaveFileFingerprint(
                    Path.GetRelativePath(slotPath, path).Replace('\\', '/'),
                    stream.Length,
                    Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
            })
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                "native_save_boundary_slot_contains_no_files");
        }

        return new SaveDirectoryFingerprint(
            slotPath,
            files.Length,
            files.Sum(file => file.Length),
            ComputeAggregateSha256(files));
    }

    public static string ComputeAggregateSha256(
        IEnumerable<SaveFileFingerprint> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(
                     item => item.RelativePath,
                     StringComparer.Ordinal))
        {
            Append(hash, file.RelativePath.Replace('\\', '/'));
            Append(hash, file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(hash, file.Sha256.ToLowerInvariant());
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static NativeSaveBoundaryObservation Evaluate(
        string initialDayKey,
        string currentDayKey,
        SaveDirectoryFingerprint initialSave,
        SaveDirectoryFingerprint currentSave)
    {
        var dayAdvanced = !string.IsNullOrWhiteSpace(initialDayKey) &&
            !string.IsNullOrWhiteSpace(currentDayKey) &&
            !string.Equals(initialDayKey, currentDayKey, StringComparison.Ordinal);
        var saveChanged = !string.Equals(
            initialSave.Sha256,
            currentSave.Sha256,
            StringComparison.Ordinal);
        return new NativeSaveBoundaryObservation(
            initialDayKey,
            currentDayKey,
            dayAdvanced,
            saveChanged,
            dayAdvanced && saveChanged,
            initialSave.Sha256,
            currentSave.Sha256);
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData(new byte[] { 0 });
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "native_save_boundary_reparse_point_rejected:" + path);
        }
    }
}
