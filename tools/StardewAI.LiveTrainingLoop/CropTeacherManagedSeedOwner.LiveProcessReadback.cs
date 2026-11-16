using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private MoneyRevenueCropAcceptedSavedReadbackReference? ReadAcceptedLiveProcessReadback(
        int processId, string executablePath, DateTimeOffset processStartedAt)
    {
        foreach (var (sequence, path, hash) in acceptedHistory.AsEnumerable().Reverse())
        {
            if (HashFile(path) != hash) throw new InvalidDataException("Same-live accepted process history changed.");
            var accepted = Read<JsonObject>(path);
            if (accepted["operation_kind"]?.GetValue<string>() != "saved_content_readback") continue;
            var admissionPath = RecordPath(sequence, "intent");
            var record = Read<JsonObject>(admissionPath);
            var observationPath = StoredPath(record, "reload_observation_path");
            var observation = Read<CropTeacherNativeReloadObservation>(observationPath);
            var process = observation.Process;
            // Only the latest accepted load can authorize this process, even after later native work.
            if (process.ProcessId != processId || !SamePath(process.ExecutablePath, executablePath) ||
                DateTimeOffset.Parse(process.StartedAtUtc) != processStartedAt)
                throw new InvalidDataException("Same-live process is not the latest original accepted native reload.");
            var hashes = new Dictionary<string, string>(record["input_artifact_hashes"]!
                .Deserialize<Dictionary<string, string>>(JsonOptions)!, PathComparer);
            foreach (var (input, expected) in observation.InputArtifactHashes) hashes[Path.GetFullPath(input)] = expected;
            hashes[path] = hash;
            hashes[admissionPath] = Text(accepted, "admission_sha256");
            hashes[observationPath] = Text(accepted, "reload_observation_sha256");
            hashes[StoredPath(accepted, "after_snapshot_path")] = Text(accepted, "after_snapshot_sha256");
            var reference = new MoneyRevenueCropAcceptedSavedReadbackReference(sequence, admissionPath, path, observationPath, hashes);
            _ = CropTeacherAcceptedSavedReadbackVerifier.Verify(StoredPath(record, "before_snapshot_path"),
                StoredPath(accepted, "after_snapshot_path"), options.RunId, [reference]);
            return reference;
        }
        return null;
    }
}
