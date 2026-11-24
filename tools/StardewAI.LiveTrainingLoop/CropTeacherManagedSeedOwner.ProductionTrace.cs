using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherManagedProductionTransition(int OwnerSequence, string OperationKind,
    MoneyRevenueCropNativeLifecycleTransitionRequest NativeTransition);

public sealed record CropTeacherManagedSharedSourceReturn(int OwnerSequence, string SupportStepId,
    MoneyRevenueCropNativeLifecycleTransitionRequest NativeTransition)
{
    public MoneyRevenueCropNativeSharedSourceReference? SourceReference { get; init; }
}

public sealed record CropTeacherManagedSavedReadback(int OwnerSequence, MoneyRevenueCropAcceptedSavedReadbackReference Reference);

public sealed record CropTeacherManagedDeveloperProductionBoundary(
    string ManifestPath, string ParentAcceptancePath, string ParentSaveBoundaryPath, int ParentAcceptedSequence,
    string LoadedSnapshotPath, string ReboundLedgerPath, string ReviewPath, string RebindRequestPath, string RebindReplyPath,
    CropTeacherManagedJointProductionTrace Parent)
{
    public bool StrictNativeSavedReadbackVerified => false;
    public bool ExcludeFromFormalTraining => true;
}

public sealed record CropTeacherManagedJointProductionTrace(CropTeacherManagedProductionTrace CropTrace,
    IReadOnlyList<CropTeacherManagedSharedSourceReturn> SharedSourceReturns)
{
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

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
    public bool RetainedReplayOnly { get; internal init; }
    public CropTeacherManagedDeveloperProductionBoundary? DeveloperSavedBoundary { get; internal init; }
    internal IReadOnlyList<CropTeacherManagedSharedSourceReturn> SharedSourceReturnReferences { get; init; } = [];
    public IReadOnlyList<CropTeacherManagedSavedReadback> SavedContentReadbacks { get; internal init; } = [];
    public void ValidateInputsUnchanged() => new CropSeedRuntimeArtifacts(InputHashes).Validate();

    internal void RequireOriginalNativeProductionLineage()
    {
        if (DeveloperSavedBoundary is not null)
            throw new InvalidDataException("Developer saved-source trace is segmented evidence, not continuous native production authority.");
    }
}

public sealed partial class CropTeacherManagedSeedOwner
{
    public Task<CropTeacherManagedProductionTrace> ReadProductionTraceAsync() => ReadProductionTraceCoreAsync(retainedReplay: false);

    public Task<CropTeacherManagedProductionTrace> ReadRetainedProductionTraceAsync() => ReadProductionTraceCoreAsync(retainedReplay: true);

    public Task<CropTeacherManagedJointProductionTrace> ReadJointProductionTraceAsync() => ReadJointProductionTraceCoreAsync(retainedReplay: false);

    public Task<CropTeacherManagedJointProductionTrace> ReadRetainedJointProductionTraceAsync() => ReadJointProductionTraceCoreAsync(retainedReplay: true);

    private async Task<CropTeacherManagedJointProductionTrace> ReadJointProductionTraceCoreAsync(bool retainedReplay)
    {
        var trace = await ReadProductionTraceCoreAsync(retainedReplay, includeSharedReturns: true).ConfigureAwait(false);
        return new(trace, trace.SharedSourceReturnReferences);
    }

    private async Task<CropTeacherManagedProductionTrace> ReadProductionTraceCoreAsync(bool retainedReplay, bool includeSharedReturns = false)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired && !retainedReplay)
                throw new InvalidOperationException("Recover the original crop phase before reading its production trace.");
            var hashes = new Dictionary<string, string>(fixedHashes, PathComparer);
            foreach (var (path, hash) in frontierHashes) hashes[path] = hash;
            CropTeacherManagedDeveloperProductionBoundary? boundary = null;
            if (developerSavedSource is { } source)
            {
                var parent = source.Parent;
                if (parent.HasDeveloperSource)
                    throw new InvalidDataException("Nested developer saved-source history requires its earlier boundary; it cannot be flattened.");
                var last = parent.History[^1];
                var parentTrace = ReadProductionTraceSegment(new(parent.Directory, parent.HeaderHash, last.Hash,
                    parent.SnapshotPath, parent.LedgerPath, parent.BackendUrl, parent.JournalDirectory, parent.History),
                    hashes, retainedReplay: true, includeSharedReturns: includeSharedReturns);
                boundary = new(options.DeveloperSavedSourcePath!, last.Path, parent.SaveBoundaryPath, last.Sequence,
                    source.SnapshotPath, source.LedgerPath, source.ReviewPath, source.RebindRequestPath, source.RebindReplyPath,
                    new(parentTrace, parentTrace.SharedSourceReturnReferences));
            }
            var result = ReadProductionTraceSegment(new(options.EvidenceDirectory, headerHash, lastAcceptedHash,
                headSnapshotPath, headLedgerPath, options.BackendUrl, options.PortfolioJournalDirectory, acceptedHistory),
                hashes, retainedReplay, includeSharedReturns, boundary);
            ValidateState();
            result.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }

    private sealed record ProductionTraceChain(string Directory, string HeaderHash, string LastAcceptedHash,
        string SnapshotPath, string LedgerPath, string BackendUrl, string JournalDirectory,
        IReadOnlyList<(int Sequence, string Path, string Hash)> History);

    private CropTeacherManagedProductionTrace ReadProductionTraceSegment(ProductionTraceChain chain, Dictionary<string, string> hashes,
        bool retainedReplay, bool includeSharedReturns, CropTeacherManagedDeveloperProductionBoundary? boundary = null)
    {
        void Pin(string path, string? expected = null)
        {
            path = Path.GetFullPath(path);
            var actual = HashFile(path);
            if (expected is not null && actual != expected || hashes.TryGetValue(path, out var prior) && prior != actual)
                throw new InvalidDataException("Managed crop production history changed: " + path);
            hashes[path] = actual;
        }
        var transitions = new List<CropTeacherManagedProductionTransition>();
        var sharedReturns = new List<CropTeacherManagedSharedSourceReturn>();
        var readbacks = new List<CropTeacherManagedSavedReadback>();
        string SegmentRecordPath(int sequence, string kind) => Path.Combine(chain.Directory, $"operation-{sequence:D6}-{kind}.json");
        var previous = chain.HeaderHash;
        foreach (var (sequence, path, hash) in chain.History)
        {
            Pin(path, hash);
            var accepted = Read<JsonObject>(path);
            var admission = SegmentRecordPath(sequence, "intent");
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
                var phasePath = SegmentRecordPath(sequence, phase);
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
            if (record["observed_terminal_selection"] is JsonObject observed &&
                observed["production_reference"]?.Deserialize<CropTeacherTerminalProductionReference>(JsonOptions) is { } productionReference)
                Pin(productionReference.ManifestPath, productionReference.ManifestSha256);
            previous = hash;
            if (kind == "shared_source_return")
            {
                if (!includeSharedReturns)
                    throw new InvalidDataException("Shared source return requires the explicit joint trace; it is not a crop production action.");
                Reference("original_intent_sha256", Path.Combine(runtime, "shared-source-intent.json"));
                Reference("shared-source_native_claim_sha256", Path.Combine(runtime, "shared-source-native-claim.json"));
                var sharedResult = Path.Combine(runtime, "shared-source-native-result.json");
                Reference("shared-source_native_result_sha256", sharedResult);
                if (accepted["shared-source_native_result_sha256"] is not null)
                    foreach (var (input, expected) in Read<JsonObject>(sharedResult)["input_artifact_hashes"]!
                                 .Deserialize<Dictionary<string, string>>(JsonOptions)!) Pin(input, expected);
                if (IsSharedPortfolioPreparation(record))
                    Reference(PreparationMode(record).AcceptedPreparationHashKey, SegmentRecordPath(sequence, "prepared"));
                var shared = SharedExecutionRecord(record)["shared_source_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
                if (!SamePath(shared.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
                    !SamePath(shared.AfterSnapshotPath, StoredPath(accepted, "after_snapshot_path")) ||
                    shared.RunId != options.RunId || shared.ExecutorVersion != options.ExecutorVersion ||
                    accepted["native_shared_source_return_verified"]?.GetValue<bool>() != true ||
                    accepted["crop_production_credit_authorized"]?.GetValue<bool>() != false)
                    throw new InvalidDataException("Shared source return trace differs from the accepted original boundary.");
                foreach (var input in new[] { shared.QueuePath, shared.BeforeSnapshotPath, shared.ExecutionReceiptPath, shared.AfterSnapshotPath }) Pin(input);
                var sourceLedgerPath = StoredPath(record, "before_ledger_path");
                var sourceLedger = Read<StardewAI.Contracts.Strategy.StrategyCommitmentLedger>(sourceLedgerPath);
                var covered = sourceLedger.CropPlantingCommitments.Where(row =>
                        row.Status == StardewAI.Contracts.Strategy.StrategyCommitmentStatuses.Active &&
                        plan.CoveredCommitmentIds.Contains(row.CommitmentId, StringComparer.Ordinal))
                    .Select(row => row.CommitmentId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var scope = OpenRecordedSharedSourceScope(record, shared.BeforeSnapshotPath, sourceLedgerPath, covered, shared, chain.BackendUrl);
                CropTeacherSharedSourceMarkerReference? marker = null;
                if (scope.ReleasesTransferLease)
                {
                    var journal = PortfolioMutationRequestJournal.Describe(chain.BackendUrl, chain.JournalDirectory,
                        "commit", scope.RebindRequest, JsonOptions);
                    marker = new(journal.RequestPath, Path.Combine(runtime, "marker-reply.json"), journal.JournalPath,
                        chain.BackendUrl, StoredPath(accepted, "after_ledger_path"));
                    scope.BindAcceptedMarker(marker);
                }
                foreach (var (input, expected) in scope.InputHashes) Pin(input, expected);
                sharedReturns.Add(new(sequence, Text(record, "support_step_id"), shared)
                {
                    SourceReference = new(plan.RequestPath, sourceLedgerPath, covered, Text(record, "support_step_id"), scope.InputHashes)
                        { SettledMaterialTransfer = scope.TransferSettlement, AcceptedMarker = marker,
                            AppliedNativeWorkQueue = scope.AppliedNativeWorkQueue,
                            SettledInventoryConsumption = scope.ConsumptionSettlement,
                            SettledConsumptionReferencePath = IsSharedCookingPreparation(record) ? SharedConsumptionReferencePath(record) : null,
                            SettledTransferReferencePath = record["shared_source_execution_protocol"]?.GetValue<string>() is "selected_work_yield.v2" or "selected_work_yield.v3"
                                ? SharedTransferReferencePath(record) : null }
                });
                continue;
            }
            if (kind == "saved_content_readback")
            {
                Reference("original_intent_sha256", Path.Combine(runtime, "saved-readback-intent.json"));
                var observationPath = StoredPath(record, "reload_observation_path");
                Reference("reload_observation_sha256", observationPath);
                var retained = new Dictionary<string, string>(inputs, PathComparer);
                foreach (var (input, expected) in Read<CropTeacherNativeReloadObservation>(observationPath).InputArtifactHashes)
                { Pin(input, expected); retained[Path.GetFullPath(input)] = expected; }
                foreach (var input in new[] { path, admission, observationPath, StoredPath(accepted, "after_snapshot_path") })
                { Pin(input); retained[Path.GetFullPath(input)] = hashes[Path.GetFullPath(input)]; }
                readbacks.Add(new(sequence, new(sequence, admission, path, observationPath, retained)));
                // An observed load is not a native production action or a fabricated queue.
                continue;
            }
            if (kind == "lifecycle" || accepted["seed_boundary_kind"]?.GetValue<string>() == "controller_stop_no_native_dispatch") continue;
            var role = kind switch
            {
                "site_clearance" => "clearance", "site_till" => "till", "supporting_route" => "route", "watering_maintenance" => "maintenance", "crop_harvest" => "harvest",
                "overnight_control" => "overnight", "control_recovery" => "control", "terminal_shipping" => "terminal", "seed" => null,
                _ => throw new InvalidDataException("Accepted crop history contains an unsupported production role.")
            };
            JsonObject? nativeResult = null;
            Dictionary<string, string>? nativeInputs = null;
            if (role is not null)
            {
                if (role == "terminal" && record["terminal_production_manifest_path"] is not null)
                    Reference("terminal_production_manifest_sha256", StoredPath(record, "terminal_production_manifest_path"));
                Reference("original_intent_sha256", Path.Combine(runtime, role + "-intent.json"));
                Reference(role + "_native_claim_sha256", Path.Combine(runtime, role + "-native-claim.json"));
                var resultPath = Path.Combine(runtime, role + "-native-result.json");
                Reference(role + "_native_result_sha256", resultPath);
                if (accepted[role + "_native_result_sha256"] is not null)
                {
                    nativeResult = Read<JsonObject>(resultPath);
                    nativeInputs = nativeResult["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonOptions)!;
                    foreach (var (input, expected) in nativeInputs) Pin(input, expected);
                }
            }
            var transition = role is not null ? record[role + "_transition"]!
                .Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! : ReadSeedProductionTransition(runtime, Pin);
            if (role == "harvest" && nativeResult?["harvest_pickup_settlement_path"] is not null)
            {
                var manifest = StoredPath(nativeResult, "harvest_pickup_settlement_path");
                var settled = StoredPath(nativeResult, "harvest_settled_snapshot_path");
                if (!SamePath(manifest, Path.Combine(runtime, "harvest-pickup", "settlement.json")) ||
                    !nativeInputs!.ContainsKey(manifest) || !nativeInputs.ContainsKey(settled) ||
                    !SamePath(manifest, StoredPath(accepted, "harvest_pickup_settlement_path")) ||
                    !SamePath(settled, StoredPath(accepted, "harvest_settled_snapshot_path")))
                    throw new InvalidDataException("Accepted harvest pickup escaped its original native result inputs.");
                var step = record["harvest_step"]!.Deserialize<CropTeacherRunStep>(JsonOptions)!;
                transition = transition with { NativeHarvestSettlement = new(options.NativeRunPlanPath,
                    StoredPath(record, "before_ledger_path"), record["active_commitment_ids"]!.Deserialize<string[]>(JsonOptions)!,
                    Text(record, "commitment_id"), Text(record, "support_step_id"), step.ActionBatch!.BatchIndex,
                    StoredPath(record, "original_model_action_path"), manifest, settled) };
            }
            if (!SamePath(transition.EffectiveAfterSnapshotPath, StoredPath(accepted, "after_snapshot_path")) ||
                transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion)
                throw new InvalidDataException("Managed crop production transition differs from its accepted native boundary.");
            foreach (var nativePath in new[] { transition.QueuePath, transition.BeforeSnapshotPath,
                         transition.ExecutionReceiptPath, transition.AfterSnapshotPath }) Pin(nativePath);
            transitions.Add(new(sequence, role is not null ? kind : transition.TransitionKind, transition));
        }
        if (previous != chain.LastAcceptedHash)
            throw new InvalidDataException("Managed crop production history does not reach the current accepted head.");
        return new CropTeacherManagedProductionTrace(options.NativeRunPlanPath, chain.SnapshotPath, chain.LedgerPath,
            transitions.ToArray(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(hashes))
            { RetainedReplayOnly = retainedReplay, DeveloperSavedBoundary = boundary, SharedSourceReturnReferences = Array.AsReadOnly(sharedReturns.ToArray()),
                SavedContentReadbacks = Array.AsReadOnly(readbacks.ToArray()) };
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
