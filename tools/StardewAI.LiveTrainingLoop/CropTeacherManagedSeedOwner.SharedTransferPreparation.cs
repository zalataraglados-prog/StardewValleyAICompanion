using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSharedTransferDecision(string SnapshotPath, string PlanPath, string RankingPath,
    int SelectedQueueIndex, string[] OwnedCandidateIds, CropTeacherDailyDecisionReference? DecisionReference = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalLedgerPath { get; init; }
    internal CropTeacherSharedSourceSelection WithOutputs(string plan, string queue, string evidence) =>
        new(SnapshotPath, PlanPath, RankingPath, plan, queue, evidence, SelectedQueueIndex, OwnedCandidateIds)
            { DecisionReference = DecisionReference };
}

public sealed partial class CropTeacherManagedSeedOwner
{
    private static bool IsSharedTransferPreparation(JsonObject record) =>
        record["shared_source_execution_protocol"]?.GetValue<string>() == "selected_work_yield.v3";

    // Own the original request BEFORE any backend preparation; the original admission is never replaced.
    public async Task<CropTeacherManagedSeedOperation> AdmitSharedTransferPreparationAsync(CropTeacherSharedTransferDecision decision,
        ReservationPortfolioCommitRequest request, string receiptPath, string afterSnapshotPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await AdmitSharedTransferPreparationCoreAsync(decision, request, receiptPath, afterSnapshotPath, runtimeDirectory).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task<CropTeacherManagedSeedOperation> AdmitSharedTransferPreparationCoreAsync(CropTeacherSharedTransferDecision decision,
        ReservationPortfolioCommitRequest request, string receiptPath, string afterSnapshotPath, string runtimeDirectory) =>
        await AdmitSharedPortfolioPreparationCoreAsync(JsonSerializer.SerializeToNode(decision, JsonOptions)!, request,
            receiptPath, afterSnapshotPath, runtimeDirectory, SharedPreparationMode.Transfer).ConfigureAwait(false);

    public async Task<CropTeacherManagedSeedOperation> AdmitSharedCookingPreparationAsync(CropTeacherSharedPreparationDecision decision,
        ReservationPortfolioCommitRequest request, string receiptPath, string afterSnapshotPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await AdmitSharedPortfolioPreparationCoreAsync(JsonSerializer.SerializeToNode(decision, JsonOptions)!, request,
                receiptPath, afterSnapshotPath, runtimeDirectory, SharedPreparationMode.Consumption).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task<CropTeacherManagedSeedOperation> AdmitSharedPortfolioPreparationCoreAsync(JsonNode decision,
        ReservationPortfolioCommitRequest request, string receiptPath, string afterSnapshotPath, string runtimeDirectory, SharedPreparationMode mode)
    {
        var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
        record["operation_kind"] = "shared_source_return";
        record["shared_source_execution_protocol"] = mode.AdmissionProtocol;
        record[mode.DecisionKey] = decision;
        record[mode.RequestKey] = JsonSerializer.SerializeToNode(request, JsonOptions);
        record["preparation_runtime_directory"] = StoredPath(record, "runtime_directory") + "-prepare";
        record["native_receipt_path"] = Path.GetFullPath(receiptPath);
        record["native_after_snapshot_path"] = Path.GetFullPath(afterSnapshotPath);
        var support = OpenSharedPortfolioSupport(record);
        var preparationRoot = StoredPath(record, "preparation_runtime_directory");
        if (Directory.Exists(preparationRoot) || File.Exists(preparationRoot) ||
            File.Exists(StoredPath(record, "native_receipt_path")) || Directory.Exists(StoredPath(record, "native_receipt_path")) ||
            File.Exists(StoredPath(record, "native_after_snapshot_path")) || Directory.Exists(StoredPath(record, "native_after_snapshot_path")))
            throw new IOException("Shared preparation requires new preparation/native outputs.");
        record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(support.InputHashes, JsonOptions);
        record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
        return await PersistAdmissionAsync(record, CreateSharedPreparationOperation(record,
            RecordPath(Sequence(record), "intent"), support)).ConfigureAwait(false);
    }

    private CropTeacherManagedSeedOperation CreateSharedPreparationOperation(JsonObject record, string path,
        ICropTeacherSharedPortfolioPreparation support) => support is CropTeacherSharedTransferSupport transfer
        ? new(this, record, path, null, sharedTransferSupport: transfer)
        : new(this, record, path, null) { SharedCookingSupport = (CropTeacherSharedCookingSupport)support };

    private ICropTeacherSharedPortfolioPreparation OpenSharedPortfolioSupport(JsonObject record)
    {
        var mode = PreparationMode(record);
        var decision = record[mode.DecisionKey]?.Deserialize<CropTeacherSharedPreparationDecision>(JsonOptions)
            ?? throw new InvalidDataException("Shared preparation lacks its original selected decision.");
        var request = record[mode.RequestKey]?.Deserialize<ReservationPortfolioCommitRequest>(JsonOptions)
            ?? throw new InvalidDataException("Shared preparation lacks its original transfer request.");
        var other = mode.Cooking ? SharedPreparationMode.Transfer : SharedPreparationMode.Consumption;
        if (record[other.DecisionKey] is not null || record[other.RequestKey] is not null || record[other.ReferenceKey] is not null ||
            record["original_consumption_preparation"] is not null || record["original_consumption_settlement"] is not null ||
            record["shared_source_selection"] is not null ||
            record["shared_source_transition"] is not null || record["original_transfer_preparation"] is not null ||
            record["shared_source_return_protocol"] is not null || record["original_transfer_settlement"] is not null ||
            record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath))
            throw new InvalidDataException("Shared preparation changed its owner/source/protocol boundary.");
        var pins = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var selected = ReadSharedSourceDecision(decision.WithOutputs("", "", ""), pins);
        if (selected.OptionId != (mode.Cooking ? "crafting.cook_recipe" : "inventory.transfer_item"))
            throw new InvalidDataException("Shared preparation differs from its original selected option.");
        if (decision.DecisionReference is not null) pins.Pin(decision.DecisionReference.Path);
        if (decision.OriginalLedgerPath is not null)
        {
            if (!SamePath(decision.OriginalLedgerPath, ReadSharedDecisionLedgerPath(new(decision.SnapshotPath, decision.PlanPath,
                    decision.RankingPath, decision.SelectedQueueIndex, decision.OwnedCandidateIds, decision.DecisionReference)
                    { OriginalLedgerPath = decision.OriginalLedgerPath })))
                throw new InvalidDataException("Shared selected preparation changed its original ledger source.");
            pins.Pin(decision.OriginalLedgerPath);
        }
        ICropTeacherSharedPortfolioPreparation support = mode.Cooking
            ? CropTeacherSharedCookingSupport.Open(plan.RequestPath, headSnapshotPath, headLedgerPath,
                ActiveCoveredCommitments(), Text(record, "support_step_id"), request, pins.Hashes)
            : CropSaleStockTransferLeaseContract.IsLeaseRequest(request)
            ? CropTeacherSharedTransferSupport.OpenInventoryTransferLease(plan.RequestPath, headSnapshotPath, headLedgerPath,
                ActiveCoveredCommitments(), Text(record, "support_step_id"), request, pins.Hashes)
            : request.MaterialClaims is { Length: > 0 }
            ? CropTeacherSharedTransferSupport.OpenPartitionedSaleStock(plan.RequestPath, headSnapshotPath, headLedgerPath,
                ActiveCoveredCommitments(), Text(record, "support_step_id"), request, pins.Hashes)
            : CropTeacherSharedTransferSupport.Open(plan.RequestPath, headSnapshotPath, headLedgerPath,
                ActiveCoveredCommitments(), Text(record, "support_step_id"), request, pins.Hashes);
        var runtime = StoredPath(record, "runtime_directory");
        var preparation = StoredPath(record, "preparation_runtime_directory");
        var outputs = new[] { runtime, preparation, StoredPath(record, "native_receipt_path"), StoredPath(record, "native_after_snapshot_path") };
        if (!SamePath(preparation, runtime + "-prepare") || outputs.Distinct(PathComparer).Count() != outputs.Length ||
            support.InputHashes.Keys.Any(path => outputs.Any(output => Within(output, path))) ||
            Within(runtime, preparation) || Within(preparation, runtime) ||
            outputs.Skip(2).Any(path => Within(preparation, path)))
            throw new IOException("Shared preparation overlaps its original inputs or native runtime.");
        RequireDirectoryPath(preparation);
        RequireOutsideNativeSaveSlot(outputs, options.NativeSaveBinding);
        return support;
    }

    private JsonObject CreateSharedTransferPreparationIntent(JsonObject record) => CreateSharedPortfolioPreparationIntent(record);

    private JsonObject CreateSharedPortfolioPreparationIntent(JsonObject record)
    {
        var mode = PreparationMode(record);
        return new()
        {
        ["schema_version"] = mode.IntentSchema, ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["support_step_id"] = Text(record, "support_step_id"), ["runtime_directory"] = StoredPath(record, "runtime_directory"),
        ["preparation_runtime_directory"] = StoredPath(record, "preparation_runtime_directory"),
        ["native_receipt_path"] = StoredPath(record, "native_receipt_path"),
        ["native_after_snapshot_path"] = StoredPath(record, "native_after_snapshot_path"),
        [mode.DecisionKey] = record[mode.DecisionKey]!.DeepClone(),
        [mode.RequestKey] = record[mode.RequestKey]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_execution_owner"] = "global_selected_work", ["crop_production_credit_authorized"] = false,
        ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
        };
    }
}
