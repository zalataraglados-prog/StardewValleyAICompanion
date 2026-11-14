using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal void ValidateOriginalOvernightSaveBinding(LiveTrainingOptions live)
    {
        var binding = options.NativeSaveBinding;
        if (!live.RequireNativeSaveBoundary || binding is null || !SamePath(binding.SaveIsolationPath, live.SaveIsolationPath) ||
            !SamePath(Path.Combine(binding.SaveIsolationPath, binding.SaveSlot), Path.Combine(live.SaveIsolationPath, live.SaveSlot)))
            throw new InvalidDataException("Original overnight requires this owner's native save root/slot and save-boundary mode.");
    }

    internal static CropTeacherNativeSaveBinding? NormalizeNativeSaveBinding(CropTeacherNativeSaveBinding? value)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value.SaveIsolationPath) || string.IsNullOrWhiteSpace(value.SaveSlot) ||
            value.SaveSlot is "." or ".." || Path.GetFileName(value.SaveSlot) != value.SaveSlot)
            throw new ArgumentException("Owned overnight requires one original isolated save root/slot.");
        var result = value with { SaveIsolationPath = Path.GetFullPath(value.SaveIsolationPath) };
        RequireDirectoryPath(result.SaveIsolationPath);
        return result;
    }

    internal static void RequireOutsideNativeSaveSlot(IEnumerable<string> outputs, CropTeacherNativeSaveBinding? binding)
    {
        if (binding is null) return;
        var slot = Path.Combine(binding.SaveIsolationPath, binding.SaveSlot);
        if (outputs.Any(path => SamePath(path, slot) || Within(slot, path) || Within(path, slot)))
            throw new IOException("Owner evidence/output must not overlap or pollute the original native save slot.");
    }

    private SaveDirectoryFingerprint ReadOriginalBeforeSave(JsonObject record)
    {
        var binding = options.NativeSaveBinding ?? throw new InvalidDataException("Owned overnight save binding is missing.");
        var value = record["before_save_fingerprint"]?.Deserialize<SaveDirectoryFingerprint>(JsonOptions)
            ?? throw new InvalidDataException("Owned overnight before-save fingerprint is missing.");
        ValidateOriginalSaveFingerprint(value, binding);
        return value;
    }

    private static void ValidateOriginalSaveFingerprint(SaveDirectoryFingerprint value, CropTeacherNativeSaveBinding binding)
    {
        if (!SamePath(value.SlotPath, Path.Combine(binding.SaveIsolationPath, binding.SaveSlot)) || value.FileCount < 1 || value.TotalBytes < 0 ||
            value.Sha256 is null || value.Sha256.Length != 64 || value.Sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("Owned overnight fingerprint changed its original slot or hash shape.");
    }

    private static string OriginalOvernightSavePath(CropTeacherManagedSeedOperation operation) => Path.Combine(operation.RuntimeDirectory, "overnight-save-boundary.json");

    private async Task EnsureOriginalOvernightSaveEvidenceAsync(CropTeacherManagedSeedOperation operation, CropTeacherOvernight scope)
    {
        var path = OriginalOvernightSavePath(operation);
        if (File.Exists(path)) return;
        var binding = options.NativeSaveBinding ?? throw new InvalidOperationException("Owned overnight save binding is missing.");
        var after = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(binding.SaveIsolationPath, binding.SaveSlot).ConfigureAwait(false);
        var report = CreateOriginalOvernightSaveEvidence(operation, scope, after);
        scope.ValidateInputsUnchanged();
        await write(path, report.ToJsonString(JsonOptions)).ConfigureAwait(false);
    }

    private JsonObject CreateOriginalOvernightSaveEvidence(CropTeacherManagedSeedOperation operation, CropTeacherOvernight scope, SaveDirectoryFingerprint after)
    {
        var before = ReadOriginalBeforeSave(operation.Record);
        ValidateOriginalSaveFingerprint(after, options.NativeSaveBinding!);
        var transition = OriginalSupportingTransition(operation);
        var from = QueueReplanFilter.SnapshotDayKey(Read<JsonObject>(transition.BeforeSnapshotPath));
        var to = QueueReplanFilter.SnapshotDayKey(Read<JsonObject>(transition.AfterSnapshotPath));
        var observed = NativeSaveBoundaryVerifier.Evaluate(from, to, before, after);
        if (!observed.Verified) throw new InvalidDataException("Original overnight save is not complete; inspect retained sleep read-only, never dispatch it again.");
        return new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_overnight_save_boundary.v1", ["run_id"] = options.RunId,
            ["save_id"] = saveId, ["player_id"] = playerId,
            ["source_snapshot_path"] = Path.GetFullPath(transition.BeforeSnapshotPath), ["after_snapshot_path"] = scope.AfterSnapshotPath,
            ["from_total_day"] = scope.FromTotalDay, ["to_total_day"] = scope.ToTotalDay,
            ["from_day_key"] = from, ["to_day_key"] = to,
            ["before_save"] = JsonSerializer.SerializeToNode(before, JsonOptions), ["after_save"] = JsonSerializer.SerializeToNode(after, JsonOptions),
            ["native_save_boundary_verified"] = true, ["formal_product_training_authorized"] = false
        };
    }

    private void BindOriginalOvernightSaveEvidence(CropTeacherManagedSeedOperation operation, OriginalSupportingOutcome outcome)
    {
        var path = OriginalOvernightSavePath(operation);
        var report = outcome.Artifacts.Read<JsonObject>(path);
        var after = report["after_save"]?.Deserialize<SaveDirectoryFingerprint>(JsonOptions)
            ?? throw new InvalidDataException("Owned overnight retained save fingerprint is missing.");
        if (!JsonNode.DeepEquals(report, CreateOriginalOvernightSaveEvidence(operation, (CropTeacherOvernight)outcome.Scope, after)))
            throw new InvalidDataException("Owned overnight save evidence differs from its original source/slot/day boundary.");
        outcome.Result["native_save_boundary_verified"] = true;
        outcome.Result["overnight_save_boundary_sha256"] = outcome.Artifacts.Hashes[path];
        outcome.Result["input_artifact_hashes"] = JsonSerializer.SerializeToNode(outcome.Artifacts.Hashes, JsonOptions);
        outcome.Artifacts.Validate();
    }
}
