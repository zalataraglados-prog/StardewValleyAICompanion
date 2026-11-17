using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record ManagedCropGlobalContinuation(string BoundaryPath, string AfterStateHash,
        NativeSaveBoundaryObservation SaveBoundary, SaveDirectoryFingerprint BeforeSave, SaveDirectoryFingerprint AfterSave);

    // Consume only the save fact from the original owner's verified handoff, not its unproved terminal/semantic flags.
    private static ManagedCropGlobalContinuation ReadManagedGlobalSaveBoundary(LiveTrainingOptions options,
        string boundaryPath, JsonObject boundary)
    {
        var savePath = ReadString(boundary, "overnight_save_boundary_path");
        var snapshotPath = ReadString(boundary, "accepted_snapshot_path");
        var acceptedPath = ReadString(boundary, "accepted_operation_path");
        var suppliedHashes = boundary["input_artifact_hashes"]?.Deserialize<Dictionary<string, string>>(JsonOptions);
        var hashes = suppliedHashes?.ToDictionary(row => Path.GetFullPath(row.Key), row => row.Value,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (hashes is null || new[] { savePath, snapshotPath, acceptedPath }.Any(path => !Path.IsPathFullyQualified(path) || !hashes.ContainsKey(Path.GetFullPath(path))) ||
            ReadString(boundary, "schema_version") != "crop_teacher_managed_global_continuation.v1" ||
            boundary["operational_handoff_ready"]?.GetValue<bool>() != true ||
            boundary["native_save_boundary_verified"]?.GetValue<bool>() != true || ReadString(boundary, "run_id") != options.RunId)
            throw new InvalidDataException("Managed global save lacks its original pinned operational handoff.");
        var inputs = new CropSeedRuntimeArtifacts(new[] { savePath, snapshotPath, acceptedPath }
            .ToDictionary(Path.GetFullPath, path => hashes[Path.GetFullPath(path)]));
        var save = inputs.Read<JsonObject>(savePath);
        var source = inputs.Read<JsonObject>(snapshotPath);
        var accepted = inputs.Read<JsonObject>(acceptedPath);
        if (ReadString(save, "schema_version") != "crop_teacher_owned_overnight_save_boundary.v1" ||
            save["native_save_boundary_verified"]?.GetValue<bool>() != true ||
            new[] { "run_id", "save_id", "player_id" }.Any(name => string.IsNullOrWhiteSpace(ReadString(save, name)) ||
                ReadString(save, name) != ReadString(boundary, name)) ||
            !SameGlobalSavePath(ReadString(save, "after_snapshot_path"), ReadString(boundary, "accepted_snapshot_path")) ||
            save["to_total_day"]?.GetValue<int>() != boundary["to_total_day"]?.GetValue<int>())
            throw new InvalidDataException("Managed global save differs from the original actor/day/accepted snapshot.");
        if (ReadString(boundary, "accepted_operation_sha256") != hashes[Path.GetFullPath(acceptedPath)] ||
            ReadString(accepted, "operation_kind") != "overnight_control" || accepted["native_save_boundary_verified"]?.GetValue<bool>() != true ||
            ReadString(accepted, "overnight_save_boundary_sha256") != hashes[Path.GetFullPath(savePath)] ||
            !SameGlobalSavePath(ReadString(accepted, "after_snapshot_path"), snapshotPath) ||
            ReadString(accepted, "after_snapshot_sha256") != hashes[Path.GetFullPath(snapshotPath)] ||
            ReadString(accepted, "after_state_hash") != ReadString(boundary, "after_state_hash") ||
            ReadString(source, "state_hash") != ReadString(boundary, "after_state_hash") ||
            QueueReplanFilter.SnapshotDayKey(source) != ReadString(save, "to_day_key"))
            throw new InvalidDataException("Managed global save differs from its accepted native result.");
        var before = save["before_save"]?.Deserialize<SaveDirectoryFingerprint>(JsonOptions);
        var after = save["after_save"]?.Deserialize<SaveDirectoryFingerprint>(JsonOptions);
        var binding = CropTeacherManagedSeedOwner.NormalizeNativeSaveBinding(new(options.SaveIsolationPath, options.SaveSlot))
            ?? throw new InvalidDataException("Managed global save lacks an isolated native slot.");
        var slot = Path.Combine(binding.SaveIsolationPath, binding.SaveSlot);
        if (before is null || after is null || !SameGlobalSavePath(before.SlotPath, slot) || !SameGlobalSavePath(after.SlotPath, slot))
            throw new InvalidDataException("Managed global save differs from the isolated native slot.");
        var observation = NativeSaveBoundaryVerifier.Evaluate(ReadString(save, "from_day_key"), ReadString(save, "to_day_key"), before, after);
        if (!observation.Verified || string.IsNullOrWhiteSpace(ReadString(boundary, "after_state_hash")))
            throw new InvalidDataException("Managed global save lacks the original day advance and durable save change.");
        inputs.Validate();
        return new(boundaryPath, ReadString(boundary, "after_state_hash"), observation, before, after);
    }

    private static bool SameGlobalSavePath(string left, string right) => Path.IsPathFullyQualified(left) && Path.IsPathFullyQualified(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
