using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record DeveloperParentProgress(string SnapshotPath, string LedgerPath, CropTeacherManagedPlanningProgress Progress);

    private static DeveloperParentProgress ReadDeveloperParentProgress(JsonObject document, CropTeacherManagedSeedOwnerOptions options,
        Func<string, string?, string> pin)
    {
        var directory = StoredPath(document, "parent_owner_directory");
        if (Within(options.EvidenceDirectory, directory) || Within(directory, options.EvidenceDirectory))
            throw new InvalidDataException("Developer fork must have a separate owner directory.");
        var headerPath = pin(Path.Combine(directory, "owner-intent.json"), Text(document, "parent_header_sha256"));
        var header = Read<JsonObject>(headerPath);
        if (Text(header, "schema_version") != "crop_teacher_managed_seed_owner.v1" || Text(header, "run_id") != options.RunId ||
            Text(header, "executor_version") != options.ExecutorVersion || !SamePath(StoredPath(header, "native_run_plan_path"), options.NativeRunPlanPath) ||
            header["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Developer parent belongs to another original plan/run/executor.");
        var originalSave = header["native_save_binding"]?.Deserialize<CropTeacherNativeSaveBinding>(JsonOptions);
        if (options.NativeSaveBinding is null || originalSave is null ||
            SamePath(options.NativeSaveBinding.SaveIsolationPath, originalSave.SaveIsolationPath) && options.NativeSaveBinding.SaveSlot == originalSave.SaveSlot)
            throw new InvalidDataException("Developer fork cannot own the original native save slot.");
        var sequence = document["parent_accepted_sequence"]?.GetValue<int>() ?? 0;
        if (sequence <= 0) throw new InvalidDataException("Developer source requires a positive accepted saved prefix.");
        var previous = Text(document, "parent_header_sha256");
        var harvest = new HashSet<string>(StringComparer.Ordinal);
        var nights = new HashSet<string>(StringComparer.Ordinal);
        JsonObject? last = null;
        // Reuse accepted small certificates; do not replay their historical snapshot graphs.
        for (var index = 1; index <= sequence; index++)
        {
            var path = pin(Path.Combine(directory, $"operation-{index:D6}-accepted.json"),
                index == sequence ? Text(document, "parent_acceptance_sha256") : null);
            var accepted = Read<JsonObject>(path);
            if (Sequence(accepted) != index || Text(accepted, "run_id") != options.RunId ||
                Text(accepted, "previous_accepted_sha256") != previous || accepted["original_boundary_verified"]?.GetValue<bool>() != true ||
                accepted["execution_authorized"]?.GetValue<bool>() != false || accepted["formal_product_training_authorized"]?.GetValue<bool>() != false)
                throw new InvalidDataException("Developer parent acceptance prefix is changed or reordered.");
            var intent = pin(Path.Combine(directory, $"operation-{index:D6}-intent.json"), Text(accepted, "admission_sha256"));
            AddAcceptedPlanningProgress(accepted, intent, harvest, nights);
            previous = HashFile(path);
            last = accepted;
        }
        if (last is null || Text(last, "operation_kind") != "overnight_control" || last["native_save_boundary_verified"]?.GetValue<bool>() != true ||
            last["native_overnight_control_verified"]?.GetValue<bool>() != true)
            throw new InvalidDataException("Developer parent prefix does not end at an accepted native save.");
        var snapshot = pin(StoredPath(last, "after_snapshot_path"), Text(last, "after_snapshot_sha256"));
        var ledger = pin(StoredPath(last, "after_ledger_path"), Text(last, "after_ledger_sha256"));
        var savedPath = pin(Path.Combine(StoredPath(Read<JsonObject>(Path.Combine(directory, $"operation-{sequence:D6}-intent.json")), "runtime_directory"),
            "overnight-save-boundary.json"), Text(last, "overnight_save_boundary_sha256"));
        var saved = Read<JsonObject>(savedPath);
        if (Text(saved, "run_id") != options.RunId || saved["native_save_boundary_verified"]?.GetValue<bool>() != true ||
            !SamePath(StoredPath(saved, "after_snapshot_path"), snapshot) ||
            saved["to_total_day"]?.GetValue<int>() != last["to_total_day"]?.GetValue<int>())
            throw new InvalidDataException("Developer parent save does not match its accepted source/day.");
        return new(snapshot, ledger, new(harvest, nights));
    }
}
