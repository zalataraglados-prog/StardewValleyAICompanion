using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record DeveloperSavedSource(string SnapshotPath, string LedgerPath,
        CropTeacherReloadProcessObservation Process, DeveloperParentProgress Parent, string ReviewPath,
        string RebindRequestPath, string RebindReplyPath, IReadOnlyDictionary<string, string> InputHashes)
    {
        public CropTeacherManagedPlanningProgress Progress => Parent.Progress;
    }

    private static DeveloperSavedSource ReadDeveloperSavedSource(CropTeacherManagedSeedOwnerOptions options,
        MoneyRevenueCropNativeProductionRunPlan plan, SnapshotEnvelope initial)
    {
        var hashes = new Dictionary<string, string>(PathComparer);
        string Pin(string path, string? expected = null)
        {
            if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("Developer source input must be absolute.");
            path = Path.GetFullPath(path);
            RequireDirectoryPath(Path.GetDirectoryName(path)!);
            var hash = HashFile(path);
            if (expected is not null && hash != expected || hashes.TryGetValue(path, out var old) && old != hash)
                throw new InvalidDataException("Developer saved-source input changed.");
            hashes[path] = hash;
            return path;
        }
        var document = Read<JsonObject>(Pin(options.DeveloperSavedSourcePath!));
        if (Text(document, "schema_version") != "crop_teacher_developer_saved_source.v1" || Text(document, "run_id") != options.RunId ||
            document["developer_only"]?.GetValue<bool>() != true || document["exclude_from_formal_training"]?.GetValue<bool>() != true ||
            document["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Developer source requires explicit non-training provenance.");
        var declared = document["input_artifact_hashes"]?.Deserialize<Dictionary<string, string>>(JsonOptions)
            ?.ToDictionary(row => Path.GetFullPath(row.Key), row => row.Value, PathComparer)
            ?? throw new InvalidDataException("Developer saved-source hashes are missing.");
        string Input(string name)
        {
            var path = Path.GetFullPath(StoredPath(document, name));
            if (!declared.TryGetValue(path, out var expected)) throw new InvalidDataException("Developer source omitted an input hash.");
            return Pin(path, expected);
        }
        var parent = ReadDeveloperParentProgress(document, options, Pin);
        var snapshotPath = Input("snapshot_path");
        var ledgerPath = Input("ledger_path");
        var snapshot = Read<SnapshotEnvelope>(snapshotPath);
        var ledger = Read<StrategyCommitmentLedger>(ledgerPath);
        var saved = Read<SnapshotEnvelope>(parent.SnapshotPath);
        if (snapshot.SaveId.Value != initial.SaveId.Value || snapshot.PlayerId.Value != initial.PlayerId.Value ||
            snapshot.GameVersion != initial.GameVersion || SnapshotHash.ComputeStateHash(snapshot.State) != snapshot.StateHash ||
            SnapshotInt(snapshot, "time", "total_days") != SnapshotInt(saved, "time", "total_days"))
            throw new InvalidDataException("Developer source changed the original saved actor/game/day.");
        CropTeacherOwnedSeedSupply.Read(snapshot, ledger, plan.CoveredCommitmentIds);
        var reviewPath = Input("review_path");
        var review = Read<JsonObject>(reviewPath);
        if (Text(review, "status") != "developer_saved_day_readback_accepted" ||
            review["source_sequence"]?.GetValue<int>() != document["parent_accepted_sequence"]?.GetValue<int>() ||
            review["copied_file_bytes_match"]?.GetValue<bool>() != true || review["manual_fork_review"]?.GetValue<bool>() != true ||
            review["generic_native_reload_certificate_claimed"]?.GetValue<bool>() != false ||
            review["formal_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Developer source has no explicit saved-copy review.");
        var reviewInputs = review["input_sha256"]?.Deserialize<Dictionary<string, string>>(JsonOptions)
            ?.ToDictionary(row => Path.GetFullPath(row.Key), row => row.Value, PathComparer);
        if (reviewInputs is null || !reviewInputs.TryGetValue(snapshotPath, out var reviewed) || reviewed != hashes[snapshotPath])
            throw new InvalidDataException("Developer review belongs to another loaded snapshot.");
        var binding = document["save_binding"]?.Deserialize<CropTeacherNativeSaveBinding>(JsonOptions);
        if (binding is null || options.NativeSaveBinding is null ||
            !SamePath(binding.SaveIsolationPath, options.NativeSaveBinding.SaveIsolationPath) || binding.SaveSlot != options.NativeSaveBinding.SaveSlot)
            throw new InvalidDataException("Developer source changed its copied native save binding.");
        var process = document["process"]?.Deserialize<CropTeacherReloadProcessObservation>(JsonOptions)
            ?? throw new InvalidDataException("Developer source has no native process origin.");
        if (process.ProcessId <= 0 || !Path.IsPathFullyQualified(process.ExecutablePath) ||
            !Path.GetFileNameWithoutExtension(process.ExecutablePath).Equals("StardewModdingAPI", StringComparison.OrdinalIgnoreCase) ||
            !DateTimeOffset.TryParse(process.StartedAtUtc, out var started) ||
            !DateTimeOffset.TryParse(process.ObservedAtUtc, out var observed) ||
            !DateTimeOffset.TryParse(snapshot.RealTimestamp, out var captured) || started > captured || captured > observed)
            throw new InvalidDataException("Developer source native process identity/time is invalid.");
        Pin(process.ExecutablePath, process.ExecutableSha256);
        var requestPath = Input("rebind_request_path");
        var replyPath = Input("rebind_reply_path");
        var request = Read<ReservationPortfolioCommitRequest>(requestPath);
        var reply = Read<ReservationPortfolioCommitResult>(replyPath);
        var verified = CropTeacherDeveloperSavedSourceRebind.Verify(request, Read<StrategyCommitmentLedger>(parent.LedgerPath), snapshot, reply);
        StrategyLedgerEvidenceStore.RequireReconciledReadback(verified, ledger, snapshot, "Developer saved source");
        foreach (var (path, hash) in hashes)
            if (HashFile(path) != hash) throw new InvalidDataException("Developer source changed during admission.");
        return new(snapshotPath, ledgerPath, process, parent, reviewPath, requestPath, replyPath, hashes);
    }
}
