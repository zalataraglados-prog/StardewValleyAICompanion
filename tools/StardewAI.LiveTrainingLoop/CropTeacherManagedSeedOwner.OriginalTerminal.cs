using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalTerminalCompilation(string ModelActionPath, string QueuePath,
    string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalTerminalAsync(CropTeacherTerminalShippingTarget target,
        Func<Task<CropTeacherOriginalTerminalCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original crop phase before terminal compilation.");
            RequireTerminalCursor(target);
            var value = await compileOriginal().ConfigureAwait(false);
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "terminal_shipping";
            record["terminal_execution_protocol"] = "original_loop.v1";
            record["terminal_target"] = JsonSerializer.SerializeToNode(target, JsonOptions);
            record["original_model_action_path"] = Path.GetFullPath(value.ModelActionPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("pending-original-kind", target.TotalDay,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath),
                Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["terminal_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalTerminalStart(record, requireNativeIdentity: false);
            record["terminal_transition"] = JsonSerializer.SerializeToNode(transition with
            { TransitionKind = start.Kind, SelectedCandidateId = start.CandidateId }, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                terminalStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalTerminalAsync(CropTeacherManagedSeedOperation operation, HttpClient http, Func<Task<JsonObject>> executeOriginal) =>
        ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Terminal);
    public Task RunTerminalShippingAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit, Func<string, string, Task<JsonObject>> post) =>
        RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.Terminal);

    private void RequireTerminalCursor(CropTeacherTerminalShippingTarget target)
    {
        if (terminalTarget is null ? target.AcceptedDepositCount != 0 : target != terminalTarget)
            throw new InvalidDataException("Terminal shipping target/cursor does not continue accepted owner outcomes.");
    }

    private CropTeacherTerminalShipping.Preparation OpenOriginalTerminalStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["terminal_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original terminal transition is missing.");
        var target = record["terminal_target"]?.Deserialize<CropTeacherTerminalShippingTarget>(JsonOptions)
            ?? throw new InvalidDataException("Original terminal target is missing.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        RequireTerminalCursor(target);
        if (record["operation_kind"]?.GetValue<string>() != "terminal_shipping" || record["step"] is not null ||
            record["prior_verified_snapshot_path"] is not null || record["route_transition"] is not null ||
            record["maintenance_transition"] is not null || record["harvest_transition"] is not null ||
            record["overnight_transition"] is not null || record["control_transition"] is not null ||
            record["terminal_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }
                .Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TotalDay != target.TotalDay ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original terminal changed its owner/run/executor/source/coverage.");
        var start = CropTeacherTerminalShipping.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"),
            headLedgerPath, active, Text(record, "support_step_id"), target, StoredPath(record, "original_model_action_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || requireNativeIdentity &&
            (transition.TransitionKind != start.Kind || transition.SelectedCandidateId != start.CandidateId))
            throw new InvalidDataException("Original terminal does not continue the accepted source/kind/candidate.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original terminal output overlaps retained inputs.");
        RequireOutsideNativeSaveSlot(new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath,
            StoredPath(record, "runtime_directory") }, options.NativeSaveBinding);
        return start;
    }

    private JsonObject CreateOriginalTerminalIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_owned_terminal_shipping.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["terminal_target"] = record["terminal_target"]!.DeepClone(), ["terminal_transition"] = record["terminal_transition"]!.DeepClone(),
        ["original_model_action_path"] = StoredPath(record, "original_model_action_path"),
        ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
        ["formal_product_training_authorized"] = false
    };

    private static void AddTerminalOutcomeEvidence(CropTeacherTerminalShipping scope, JsonObject document)
    {
        document["native_terminal_shipping_step_verified"] = true; document["terminal_kind"] = scope.Kind;
        document["accepted_deposit_count_before"] = scope.Target.AcceptedDepositCount;
        document["accepted_deposit_count_after"] = scope.AcceptedDepositCountAfter;
        document["terminal_pending_value_after"] = scope.PendingValueAfter;
        document["terminal_settlement_verified"] = false;
    }
}
