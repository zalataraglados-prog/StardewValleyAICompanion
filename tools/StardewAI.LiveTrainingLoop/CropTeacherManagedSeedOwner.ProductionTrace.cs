using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedProductionTransition(int OwnerSequence, string OperationKind,
    MoneyRevenueCropNativeLifecycleTransitionRequest NativeTransition);

public sealed class CropTeacherManagedProductionTrace
{
    internal CropTeacherManagedProductionTrace(string planPath, string snapshotPath, string ledgerPath,
        CropTeacherManagedProductionTransition[] transitions, IReadOnlyDictionary<string, string> hashes)
    {
        NativeRunPlanPath = planPath; AcceptedSnapshotPath = snapshotPath; AcceptedLedgerPath = ledgerPath;
        Transitions = Array.AsReadOnly(transitions); InputHashes = hashes;
    }
    public string NativeRunPlanPath { get; }
    public string AcceptedSnapshotPath { get; }
    public string AcceptedLedgerPath { get; }
    public IReadOnlyList<CropTeacherManagedProductionTransition> Transitions { get; }
    public IReadOnlyDictionary<string, string> InputHashes { get; }
    public void ValidateInputsUnchanged() => new CropSeedRuntimeArtifacts(InputHashes).Validate();
}

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedProductionTrace> ReadProductionTraceAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original crop phase before reading its production trace.");
            var hashes = new Dictionary<string, string>(fixedHashes, PathComparer);
            foreach (var (path, hash) in frontierHashes) hashes[path] = hash;
            void Pin(string path, string? expected = null)
            {
                path = Path.GetFullPath(path);
                var actual = HashFile(path);
                if (expected is not null && actual != expected || hashes.TryGetValue(path, out var prior) && prior != actual)
                    throw new InvalidDataException("Managed crop production history changed: " + path);
                hashes[path] = actual;
            }
            var transitions = new List<CropTeacherManagedProductionTransition>();
            var previous = headerHash;
            foreach (var (sequence, path, hash) in acceptedHistory)
            {
                Pin(path, hash);
                var accepted = Read<JsonObject>(path);
                var admission = RecordPath(sequence, "intent");
                Pin(admission, Text(accepted, "admission_sha256"));
                var record = Read<JsonObject>(admission);
                var runtime = StoredPath(record, "runtime_directory");
                var kind = record["operation_kind"]?.GetValue<string>() ?? "seed";
                if (Text(accepted, "previous_accepted_sha256") != previous ||
                    Text(record, "previous_accepted_sha256") != previous)
                    throw new InvalidDataException("Managed crop production history is not the accepted owner chain.");
                // These immutable acceptance hashes were validated by the original owner/index.
                // Retain only references, not every historical scope and its full snapshot graph.
                void Reference(string name, string artifact)
                {
                    if (accepted[name] is not null) Pin(artifact, Text(accepted, name));
                }
                foreach (var phase in new[] { "prepared", "started", "dispatch", "continuation" })
                {
                    var phasePath = RecordPath(sequence, phase);
                    var key = phase == "started" ? "runtime_start_sha256" : phase + "_sha256";
                    Reference(key, phasePath);
                    if (File.Exists(phasePath)) Pin(phasePath);
                }
                Reference("original_proof_sha256", Path.Combine(runtime, "support-proof.json"));
                Reference("original_marker_reply_sha256", Path.Combine(runtime, "marker-reply.json"));
                Reference("original_closure_sha256", Path.Combine(runtime, "verified-closure-ledger.json"));
                Reference("overnight_save_boundary_sha256", Path.Combine(runtime, "overnight-save-boundary.json"));
                if (kind is "seed" or "lifecycle")
                    Reference("original_intent_sha256", Path.Combine(runtime, kind == "seed" ? "support-intent.json" : "lifecycle-intent.json"));
                Pin(StoredPath(accepted, "after_snapshot_path"), Text(accepted, "after_snapshot_sha256"));
                Pin(StoredPath(accepted, "after_ledger_path"), Text(accepted, "after_ledger_sha256"));
                var inputs = record["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonOptions)!;
                foreach (var (input, expected) in inputs) Pin(input, expected);
                previous = hash;
                if (kind == "lifecycle" || accepted["seed_boundary_kind"]?.GetValue<string>() == "controller_stop_no_native_dispatch") continue;
                var role = kind switch
                {
                    "supporting_route" => "route", "watering_maintenance" => "maintenance", "crop_harvest" => "harvest",
                    "overnight_control" => "overnight", "control_recovery" => "control", "seed" => null,
                    _ => throw new InvalidDataException("Accepted crop history contains an unsupported production role.")
                };
                if (role is not null)
                {
                    Reference("original_intent_sha256", Path.Combine(runtime, role + "-intent.json"));
                    Reference(role + "_native_claim_sha256", Path.Combine(runtime, role + "-native-claim.json"));
                    var resultPath = Path.Combine(runtime, role + "-native-result.json");
                    Reference(role + "_native_result_sha256", resultPath);
                    if (accepted[role + "_native_result_sha256"] is not null)
                        foreach (var (input, expected) in Read<JsonObject>(resultPath)["input_artifact_hashes"]!
                                     .Deserialize<Dictionary<string, string>>(JsonOptions)!) Pin(input, expected);
                }
                var transition = role is not null ? record[role + "_transition"]!
                    .Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! : ReadSeedProductionTransition(runtime, Pin);
                if (!SamePath(transition.AfterSnapshotPath, StoredPath(accepted, "after_snapshot_path")) ||
                    transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion)
                    throw new InvalidDataException("Managed crop production transition differs from its accepted native boundary.");
                foreach (var nativePath in new[] { transition.QueuePath, transition.BeforeSnapshotPath,
                             transition.ExecutionReceiptPath, transition.AfterSnapshotPath }) Pin(nativePath);
                transitions.Add(new(sequence, role is not null ? kind : transition.TransitionKind, transition));
            }
            if (previous != lastAcceptedHash)
                throw new InvalidDataException("Managed crop production history does not reach the current accepted head.");
            ValidateState();
            var result = new CropTeacherManagedProductionTrace(options.NativeRunPlanPath, headSnapshotPath, headLedgerPath,
                transitions.ToArray(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(hashes));
            result.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest ReadSeedProductionTransition(
        string runtime, Action<string, string?> pin)
    {
        var path = Path.Combine(runtime, "native-transition-request.json");
        pin(path, null);
        var transition = Read<MoneyRevenueCropNativeLifecycleTransitionRequest>(path);
        var proofPath = Path.Combine(runtime, "support-proof.json");
        var proof = Read<JsonObject>(proofPath);
        var artifacts = proof["artifact_hashes"]?.Deserialize<Dictionary<string, string>>(JsonOptions);
        if (artifacts is not null) foreach (var (input, expected) in artifacts) pin(input, expected);
        foreach (var (key, nativePath) in new[] { ("queue", transition.QueuePath), ("before_snapshot", transition.BeforeSnapshotPath),
                     ("execution_receipt", transition.ExecutionReceiptPath), ("after_snapshot", transition.AfterSnapshotPath) })
        {
            if (artifacts is not null)
            {
                var match = artifacts.SingleOrDefault(row => SamePath(row.Key, nativePath));
                if (match.Key is null) throw new InvalidDataException("Original crop seed transition is outside its accepted proof.");
                pin(nativePath, match.Value);
            }
            else
            {
                if (!SamePath(Text(proof, key + "_path"), nativePath))
                    throw new InvalidDataException("Original crop purchase transition is outside its accepted proof.");
                pin(nativePath, Text(proof, key + "_sha256"));
            }
        }
        var receipt = Read<QueueExecutionReceiptEnvelope>(transition.ExecutionReceiptPath);
        if (receipt.RunId != transition.RunId || receipt.SelectedCandidateId != transition.SelectedCandidateId ||
            proof["run_id"] is not null && Text(proof, "run_id") != transition.RunId ||
            proof["executor_version"] is not null && Text(proof, "executor_version") != transition.ExecutorVersion ||
            proof["selected_candidate_id"] is not null && Text(proof, "selected_candidate_id") != transition.SelectedCandidateId)
            throw new InvalidDataException("Original crop seed transition changed its verified native identity.");
        return transition;
    }
}
