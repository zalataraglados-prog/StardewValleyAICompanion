using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    // Observe an already relaunched isolated game; this method never launches or stops any process.
    private static async Task<string> CaptureCropTeacherNativeReloadAsync(HttpClient http, string runId,
        string sourceSnapshotPath, string ledgerPath, string overnightSaveBoundaryPath, CropTeacherNativeSaveBinding saveBinding,
        int gameProcessId, string expectedExecutablePath, string gameLogPath, string bridgeSnapshotUrl, string outputDirectory)
    {
        var binding = CropTeacherManagedSeedOwner.NormalizeNativeSaveBinding(saveBinding)!;
        var output = Path.GetFullPath(outputDirectory);
        var paths = new[] { sourceSnapshotPath, ledgerPath, overnightSaveBoundaryPath, expectedExecutablePath, gameLogPath }
            .Select(Path.GetFullPath).ToArray();
        CropTeacherManagedSeedOwner.RequireOutsideNativeSaveSlot(new[] { output }, binding);
        if (Directory.Exists(output) || File.Exists(output) || paths.Any(path => path.StartsWith(output + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new IOException("Native reload capture requires separate new output, outside retained inputs/save slot.");
        var endpoint = new Uri(bridgeSnapshotUrl, UriKind.Absolute);
        if (!endpoint.IsLoopback || endpoint.AbsolutePath != "/api/v1/snapshot")
            throw new InvalidDataException("Native reload capture must bind this isolated local bridge endpoint.");
        var inputs = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        foreach (var path in paths.Take(4)) inputs.Pin(path);
        var source = inputs.Read<SnapshotEnvelope>(paths[0]);
        var saved = inputs.Read<JsonObject>(paths[2]);
        var canonicalSavedPath = saved["after_snapshot_path"]?.GetValue<string>() ?? "";
        if (!Path.IsPathFullyQualified(canonicalSavedPath)) throw new InvalidDataException("Native reload canonical saved snapshot is not absolute.");
        inputs.Pin(canonicalSavedPath);
        using var process = Process.GetProcessById(gameProcessId);
        var executable = process.MainModule?.FileName ?? throw new InvalidDataException("Native reload process executable unavailable.");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (process.HasExited || !Path.GetFullPath(executable).Equals(paths[3], comparison) ||
            process.StartTime.ToUniversalTime() <= DateTimeOffset.Parse(source.RealTimestamp).UtcDateTime)
            throw new InvalidDataException("Native reload capture requires a new exact original game process after the saved boundary.");
        var before = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(binding.SaveIsolationPath, binding.SaveSlot).ConfigureAwait(false);
        Directory.CreateDirectory(output);
        var snapshotPath = Path.Combine(output, "loaded-snapshot.json");
        await WritePortfolioJournalFileAsync(snapshotPath, await http.GetStringAsync(
            SnapshotUrlForProfile(bridgeSnapshotUrl, "full", forceRefresh: true)).ConfigureAwait(false)).ConfigureAwait(false);
        var eventsPath = Path.Combine(output, "bridge-events.json");
        var eventsUrl = new UriBuilder(endpoint) { Path = "/api/v1/events", Query = "after_sequence=0&limit=500" }.Uri;
        await WritePortfolioJournalFileAsync(eventsPath, await http.GetStringAsync(eventsUrl).ConfigureAwait(false)).ConfigureAwait(false);
        var logPath = Path.Combine(output, "native-load-log.txt");
        var logInfo = new FileInfo(paths[4]);
        if (logInfo.Length <= 0 || logInfo.Length > 8 * 1024 * 1024)
            throw new InvalidDataException("Native reload requires its bounded dedicated original game log.");
        var lastWrite = logInfo.LastWriteTimeUtc;
        using (var log = File.Open(paths[4], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var bytes = new byte[checked((int)logInfo.Length)];
            await log.ReadExactlyAsync(bytes).ConfigureAwait(false);
            await WritePortfolioJournalFileAsync(logPath, System.Text.Encoding.UTF8.GetString(bytes)).ConfigureAwait(false);
        }
        var after = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(binding.SaveIsolationPath, binding.SaveSlot).ConfigureAwait(false);
        foreach (var path in new[] { snapshotPath, eventsPath, logPath }) inputs.Pin(path);
        inputs.Validate();
        var observation = new CropTeacherNativeReloadObservation("crop_teacher_native_reload_observation.v1", runId,
            paths[0], paths[1], paths[2], binding, before, after,
            new(process.Id, paths[3], inputs.Hashes[paths[3]], process.StartTime.ToUniversalTime().ToString("O"), DateTimeOffset.UtcNow.ToString("O")),
            snapshotPath, eventsPath, logPath, paths[4], lastWrite.ToString("O"),
            new Dictionary<string, string>(inputs.Hashes));
        var result = Path.Combine(output, "native-reload-observation.json");
        await WritePortfolioJournalFileAsync(result, JsonSerializer.Serialize(observation, JsonOptions)).ConfigureAwait(false);
        _ = CropTeacherNativeSavedContentReadback.Open(result, "native-readback-capture:" + Guid.NewGuid().ToString("N"));
        inputs.Validate();
        return result;
    }
}
