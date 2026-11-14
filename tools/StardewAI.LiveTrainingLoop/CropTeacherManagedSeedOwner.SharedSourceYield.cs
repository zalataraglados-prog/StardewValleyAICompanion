using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSharedSourceSelection(string DecisionSnapshotPath, string DecisionPlanPath,
    string DecisionRankingPath, string ModelPlanPath, string QueuePath, string EvidencePath, int SelectedQueueIndex,
    string[] OwnedCandidateIds);

public sealed record CropTeacherSharedExecutionClaim(int OwnerSequence, string SupportStepId,
    CropTeacherSharedSourceSelection Selection, MoneyRevenueCropNativeLifecycleTransitionRequest Transition);

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> AdmitSharedSourceYieldAsync(CropTeacherSharedSourceSelection selection,
        string receiptPath, string afterSnapshotPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "shared_source_return";
            record["shared_source_execution_protocol"] = "selected_work_yield.v1";
            record["shared_source_selection"] = JsonSerializer.SerializeToNode(selection, JsonOptions);
            var selected = ReadSharedSourceSelection(record);
            var snapshot = Read<SnapshotEnvelope>(headSnapshotPath);
            var day = SnapshotInt(snapshot, "time", "total_days");
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("shared_source_return", day,
                selection.QueuePath, headSnapshotPath, Path.GetFullPath(receiptPath), Path.GetFullPath(afterSnapshotPath),
                options.RunId, options.ExecutorVersion, selected.CandidateId);
            record["shared_source_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenSharedSourceStart(record);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                sharedSourceStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    // This claims one invocation; the global caller retains execution ownership and uses the original executor.
    public async Task<CropTeacherSharedExecutionClaim> ClaimSharedSourceExecutionAsync(
        CropTeacherManagedSeedOperation operation, HttpClient http)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (operation.SharedSourceStart is null || operation.Started is not null)
                throw new InvalidOperationException("Selected foreign execution requires an unstarted prepared source yield.");
            var claimPath = NativeSupportingPath(operation, "claim");
            if (File.Exists(claimPath))
                throw new InvalidOperationException("Selected foreign execution is already claimed; read its original outcome, never redispatch.");
            RequireNewSupportingOutputs(OriginalSupportingTransition(operation), watering: false);
            var root = operation.RuntimeDirectory;
            RequireDirectoryPath(root);
            Directory.CreateDirectory(root);
            var intentPath = SupportingIntentPath(operation);
            if (Directory.EnumerateFileSystemEntries(root).Any(path =>
                Path.GetFileName(path) != Path.GetFileName(intentPath) && Path.GetFileName(path) != "teacher-ledgers"))
                throw new IOException("Unclaimed shared source yield contains unrelated execution artifacts.");
            var intent = CreateSupportingTransitionIntent(operation.Record, null);
            if (File.Exists(intentPath))
            {
                if (!JsonNode.DeepEquals(intent, Read<JsonObject>(intentPath)))
                    throw new InvalidDataException("Selected foreign execution retained intent changed.");
            }
            else await write(intentPath, intent.ToJsonString(JsonOptions)).ConfigureAwait(false);
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, root,
                operation.SharedSourceStart.BaseLedger.SourceStateHash, JsonOptions, "Shared source yield authority").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(operation.SharedSourceStart.BaseLedger, before.Ledger, "shared source yield authority");
            ValidateSupportingTransition(operation);
            await SaveAsync(claimPath, CreateOriginalSupportingNativeClaim(operation)).ConfigureAwait(false);
            var claimHash = HashFile(claimPath);
            operation.RuntimeArtifactGuard = () =>
            {
                if (HashFile(claimPath) != claimHash) throw new InvalidDataException("Selected foreign execution claim changed.");
            };
            return new(operation.Sequence, operation.SupportStepId, SharedSourceSelection(operation.Record), OriginalSupportingTransition(operation));
        }
        finally { gate.Release(); }
    }

    public async Task VerifySharedSourceExecutionAsync(CropTeacherManagedSeedOperation operation)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (operation.SharedSourceStart is null)
                throw new InvalidOperationException("Observed-only source return does not own a selected native invocation.");
            await EnsureOriginalSupportingOutcomeAsync(operation).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static CropTeacherSharedSourceSelection SharedSourceSelection(JsonObject record) =>
        record["shared_source_selection"]?.Deserialize<CropTeacherSharedSourceSelection>(JsonOptions)
        ?? throw new InvalidDataException("Shared source yield lacks its original selected decision.");

    private SelectedQueueCandidateLock ReadSharedSourceSelection(JsonObject record)
    {
        var selection = SharedSourceSelection(record);
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var paths = new[] { selection.DecisionSnapshotPath, selection.DecisionPlanPath, selection.DecisionRankingPath,
            selection.ModelPlanPath, selection.QueuePath, selection.EvidencePath };
        if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)))
            throw new InvalidDataException("Shared source selected decision requires absolute original paths.");
        var original = artifacts.Read<SnapshotEnvelope>(selection.DecisionSnapshotPath);
        var current = artifacts.Read<SnapshotEnvelope>(headSnapshotPath);
        var decision = artifacts.Read<SmallModelPlanEnvelope>(selection.DecisionPlanPath);
        var ranking = artifacts.Read<JsonObject>(selection.DecisionRankingPath);
        var selected = SelectedQueueDecisionLease.CreateFromPlan(decision, ranking).CandidateAt(selection.SelectedQueueIndex);
        if (selection.OwnedCandidateIds is null || selection.OwnedCandidateIds.Contains(selected.CandidateId, StringComparer.Ordinal) ||
            decision.StateHash != original.StateHash || decision.ExecutionMode != "training_singleplayer" ||
            original.SaveId.Value != current.SaveId.Value || original.PlayerId.Value != current.PlayerId.Value || original.GameTick > current.GameTick ||
            SnapshotInt(original, "time", "total_days") != SnapshotInt(current, "time", "total_days"))
            throw new InvalidDataException("Shared source yield lost its original decision source or selected foreign ownership.");
        var evidence = artifacts.Read<JsonObject>(selection.EvidencePath);
        var model = artifacts.Read<SmallModelPlanEnvelope>(selection.ModelPlanPath);
        var queue = artifacts.Read<JsonObject>(selection.QueuePath);
        if (evidence["schema_version"]?.GetValue<string>() != "selected_queue_candidate_refresh.v1" ||
            evidence["state_hash"]?.GetValue<string>() != current.StateHash ||
            evidence["goal_id"]?.GetValue<string>() != decision.GoalId ||
            evidence["policy_model_invoked"]?.GetValue<bool>() != false || evidence["selection_locked"]?.GetValue<bool>() != true ||
            evidence["selected_queue_index"]?.GetValue<int>() != selected.QueueIndex ||
            evidence["selected_candidate_id"]?.GetValue<string>() != selected.CandidateId ||
            evidence["selected_option_id"]?.GetValue<string>() != selected.OptionId ||
            model.StateHash != current.StateHash || model.GoalId != decision.GoalId || model.ExecutionMode != decision.ExecutionMode)
            throw new InvalidDataException("Shared source yield differs from its original selected-candidate refresh.");
        var availability = evidence["availability"]?.Deserialize<OptionAvailabilityEnvelope>(JsonOptions)
            ?? throw new InvalidDataException("Shared source yield lacks original fresh availability.");
        if (availability.StateHash != current.StateHash)
            throw new InvalidDataException("Shared source yield availability belongs to another source.");
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, decision.GoalId);
        var materialized = JsonSerializer.SerializeToNode(rows, JsonOptions)!.AsArray();
        var matches = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(materialized, selected);
        var freshRanking = new JsonObject { ["ranked_event_candidates"] = matches };
        var fresh = SelectedQueueDecisionLease.CreateFromPlan(model, freshRanking);
        var mechanical = SelectedQueueDecisionLease.Create(queue, freshRanking);
        if (matches.Count != 1 || fresh.Candidates.Count != 1 || mechanical.Candidates.Count != 1 ||
            fresh.Candidates[0].CandidateId != mechanical.Candidates[0].CandidateId ||
            fresh.Candidates[0].OptionId != selected.OptionId)
            throw new InvalidDataException("Shared source yield does not contain exactly the original selected objective.");
        artifacts.Validate();
        return mechanical.Candidates[0];
    }

    private CropTeacherSharedSourcePreparation OpenSharedSourceStart(JsonObject record)
    {
        var selected = ReadSharedSourceSelection(record);
        var selection = SharedSourceSelection(record);
        var transition = record["shared_source_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
        if (record["shared_source_execution_protocol"]?.GetValue<string>() != "selected_work_yield.v1" ||
            record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !SamePath(transition.BeforeSnapshotPath, headSnapshotPath) || !SamePath(transition.QueuePath, selection.QueuePath) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion ||
            transition.SelectedCandidateId != selected.CandidateId)
            throw new InvalidDataException("Shared source yield changed its owner/run/executor/source boundary.");
        var pins = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        foreach (var path in new[] { selection.DecisionSnapshotPath, selection.DecisionPlanPath, selection.DecisionRankingPath,
            selection.ModelPlanPath, selection.QueuePath, selection.EvidencePath }) pins.Pin(path);
        var start = CropTeacherSharedSourcePreparation.Open(plan.RequestPath, headSnapshotPath, headLedgerPath,
            ActiveCoveredCommitments(), Text(record, "support_step_id"), selection.ModelPlanPath, transition, pins.Hashes);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || start.InputHashes.Keys.Any(path => Within(StoredPath(record, "runtime_directory"), path)))
            throw new InvalidDataException("Shared source yield lost its accepted source or overlaps its retained inputs.");
        RequireOutsideNativeSaveSlot(new[] { StoredPath(record, "runtime_directory"), transition.ExecutionReceiptPath,
            transition.AfterSnapshotPath }, options.NativeSaveBinding);
        return start;
    }

    private JsonObject CreateSharedSourceYieldIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_managed_shared_source_yield.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["shared_source_selection"] = record["shared_source_selection"]!.DeepClone(),
        ["shared_source_transition"] = record["shared_source_transition"]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_execution_owner"] = "global_selected_work", ["crop_production_credit_authorized"] = false,
        ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
    };

    private static int SnapshotInt(SnapshotEnvelope snapshot, string section, string field) =>
        snapshot.State[section].GetProperty(field).GetProperty("value").GetInt32();
}
