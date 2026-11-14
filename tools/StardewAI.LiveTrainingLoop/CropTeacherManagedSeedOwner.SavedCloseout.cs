using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedSavedCloseoutEvidence(CropTeacherManagedTerminalEvidence Terminal,
    string NativeSnapshotPath, string NativeLedgerPath, string NativeAcceptancePath,
    string ReadbackAcceptancePath, string ObservationPath, string CurrentSnapshotPath, string CurrentLedgerPath,
    IReadOnlyDictionary<string, string> InputHashes)
{
    public void ValidateInputsUnchanged() => new CropSeedRuntimeArtifacts(InputHashes).Validate();
}

public sealed partial class CropTeacherManagedSeedOwner
{
    internal async Task<CropTeacherManagedSavedCloseoutEvidence> ReadSavedTerminalCloseoutAsync(string terminalManifestPath)
    {
        var trace = await ReadProductionTraceAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            var readback = lastAcceptedOperation?.SavedReadback;
            if (Pending is not null || HeadRecoveryRequired || readback is null || !HasCompletedTerminalDeposits ||
                !SamePath(trace.AcceptedSnapshotPath, headSnapshotPath) || !SamePath(trace.AcceptedLedgerPath, headLedgerPath))
                throw new InvalidOperationException("Saved closeout requires this owner's accepted load and completed original terminal deposits.");
            terminalManifestPath = Path.GetFullPath(terminalManifestPath);
            var output = Path.GetDirectoryName(terminalManifestPath)!;
            if (!Within(options.EvidenceDirectory, output) || SamePath(options.EvidenceDirectory, output) || ReservedRuntimePath(output) ||
                Path.GetFileName(terminalManifestPath) != "terminal-evidence.json" || !Directory.Exists(output))
                throw new IOException("Saved closeout requires the existing original owned terminal evidence directory.");
            RequireDirectoryPath(output);
            RequireOutsideNativeSaveSlot(new[] { output }, options.NativeSaveBinding);
            var artifacts = new CropSeedRuntimeArtifacts(trace.InputHashes);
            var native = trace.Transitions.LastOrDefault();
            if (native?.OperationKind != "overnight_control" || native.NativeTransition.TransitionKind != "native_day_rollover")
                throw new InvalidDataException("Saved closeout has no original final native overnight.");
            var nativeAcceptancePath = RecordPath(native.OwnerSequence, "accepted");
            var nativeAdmissionPath = RecordPath(native.OwnerSequence, "intent");
            RequireTraceReference(nativeAcceptancePath); RequireTraceReference(nativeAdmissionPath);
            var accepted = artifacts.Read<JsonObject>(nativeAcceptancePath);
            var admission = artifacts.Read<JsonObject>(nativeAdmissionPath);
            var nativeSnapshotPath = StoredPath(accepted, "after_snapshot_path");
            var nativeLedgerPath = StoredPath(accepted, "after_ledger_path");
            var savePath = Path.Combine(StoredPath(admission, "runtime_directory"), "overnight-save-boundary.json");
            RequireTraceReference(nativeSnapshotPath); RequireTraceReference(nativeLedgerPath); RequireTraceReference(savePath);
            if (Text(accepted, "operation_kind") != "overnight_control" || accepted["native_save_boundary_verified"]?.GetValue<bool>() != true ||
                !SamePath(nativeSnapshotPath, native.NativeTransition.AfterSnapshotPath) ||
                Text(accepted, "after_snapshot_sha256") != artifacts.Hashes[nativeSnapshotPath] ||
                Text(accepted, "after_ledger_sha256") != artifacts.Hashes[nativeLedgerPath] ||
                !SamePath(savePath, readback.Observation.OvernightSaveBoundaryPath) ||
                Text(accepted, "overnight_save_boundary_sha256") != artifacts.Hashes[savePath] ||
                !readback.InputHashes.TryGetValue(savePath, out var savedHash) || savedHash != artifacts.Hashes[savePath])
                throw new InvalidDataException("Saved closeout load differs from the accepted original native save boundary.");
            var manifest = artifacts.Read<JsonObject>(terminalManifestPath);
            if (!SamePath(StoredPath(manifest, "accepted_snapshot_path"), nativeSnapshotPath) ||
                !SamePath(StoredPath(manifest, "accepted_ledger_path"), nativeLedgerPath))
                throw new InvalidDataException("Saved closeout certificate must retain its original native head, not the reset-tick load head.");
            var productionPath = StoredPath(manifest, "production_manifest_path");
            RequireTraceReference(productionPath);
            var productionManifest = artifacts.Read<JsonObject>(productionPath);
            var production = new CropTeacherManagedProductionEvidence(productionPath,
                ProofPaths("planting_layout_proof_paths"), ProofPaths("daily_capacity_proof_paths"),
                ProofPaths("native_lifecycle_proof_paths"), ProofPaths("output_retention_proof_paths"), artifacts.Hashes[productionPath]);
            var selection = terminalTarget?.NativeVariants is null ? new CropTeacherTerminalShippingSelection(production, options.NativeRunPlanPath,
                StoredPath(productionManifest, "accepted_snapshot_path"), StoredPath(productionManifest, "accepted_ledger_path")) :
                CropTeacherTerminalShippingSelection.OpenNativeVariants(production, options.NativeRunPlanPath,
                StoredPath(productionManifest, "accepted_snapshot_path"), StoredPath(productionManifest, "accepted_ledger_path"));
            if (selection.CreateTarget(AcceptedTerminalDepositCount) != terminalTarget)
                throw new InvalidDataException("Saved closeout changed this owner's original selected terminal target.");
            // This is an original native context descriptor, not a synthetic load queue or copied ledger.
            var nativeTrace = new CropTeacherManagedProductionTrace(trace.NativeRunPlanPath, nativeSnapshotPath, nativeLedgerPath,
                trace.Transitions.ToArray(), trace.InputHashes);
            var terminal = await CropTeacherTerminalSettlementEvidence.ReadAsync(nativeTrace, production, terminalTarget!, output).ConfigureAwait(false);
            if (!SamePath(terminal.ManifestPath, terminalManifestPath) || artifacts.Pin(terminalManifestPath) != terminal.ManifestSha256)
                throw new InvalidDataException("Saved closeout terminal certificate changed during original replay.");
            var terminalInputs = manifest["input_artifact_hashes"]?.Deserialize<Dictionary<string, string>>(JsonOptions)
                ?? throw new InvalidDataException("Saved closeout terminal certificate lacks its replay inputs.");
            foreach (var (path, hash) in terminalInputs)
                if (artifacts.Pin(path) != hash) throw new InvalidDataException("Saved closeout terminal replay input changed.");
            artifacts.Pin(lastAcceptedPath!);
            foreach (var (path, hash) in readback.InputHashes)
                if (artifacts.Pin(path) != hash) throw new InvalidDataException("Saved closeout readback inputs changed.");
            ValidateState(); trace.ValidateInputsUnchanged(); selection.ValidateInputsUnchanged(); readback.ValidateInputsUnchanged(); artifacts.Validate();
            return new(terminal, nativeSnapshotPath, nativeLedgerPath, nativeAcceptancePath, lastAcceptedPath!,
                StoredPath(lastAcceptedOperation!.Record, "reload_observation_path"), headSnapshotPath, headLedgerPath,
                new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(artifacts.Hashes, PathComparer)));

            void RequireTraceReference(string path)
            {
                if (!trace.InputHashes.TryGetValue(path, out var hash) || artifacts.Pin(path) != hash)
                    throw new InvalidDataException("Saved closeout source is outside the actual accepted owner trace: " + path);
            }
            string[] ProofPaths(string name) => productionManifest[name]?.Deserialize<string[]>(JsonOptions)
                ?? throw new InvalidDataException("Saved closeout production certificate lacks " + name + ".");
        }
        finally { gate.Release(); }
    }
}
