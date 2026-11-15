using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSharedTransferDecision(string SnapshotPath, string PlanPath, string RankingPath,
    int SelectedQueueIndex, string[] OwnedCandidateIds, CropTeacherDailyDecisionReference? DecisionReference = null)
{
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
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "shared_source_return";
            record["shared_source_execution_protocol"] = "selected_work_yield.v3";
            record["shared_transfer_decision"] = JsonSerializer.SerializeToNode(decision, JsonOptions);
            record["shared_transfer_request"] = JsonSerializer.SerializeToNode(request, JsonOptions);
            record["preparation_runtime_directory"] = StoredPath(record, "runtime_directory") + "-prepare";
            record["native_receipt_path"] = Path.GetFullPath(receiptPath);
            record["native_after_snapshot_path"] = Path.GetFullPath(afterSnapshotPath);
            var support = OpenSharedTransferSupport(record);
            var preparationRoot = StoredPath(record, "preparation_runtime_directory");
            if (Directory.Exists(preparationRoot) || File.Exists(preparationRoot) ||
                File.Exists(StoredPath(record, "native_receipt_path")) || Directory.Exists(StoredPath(record, "native_receipt_path")) ||
                File.Exists(StoredPath(record, "native_after_snapshot_path")) || Directory.Exists(StoredPath(record, "native_after_snapshot_path")))
                throw new IOException("Shared preparation requires new preparation/native outputs.");
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(support.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                sharedTransferSupport: support)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private CropTeacherSharedTransferSupport OpenSharedTransferSupport(JsonObject record)
    {
        var decision = record["shared_transfer_decision"]?.Deserialize<CropTeacherSharedTransferDecision>(JsonOptions)
            ?? throw new InvalidDataException("Shared preparation lacks its original selected decision.");
        var request = record["shared_transfer_request"]?.Deserialize<ReservationPortfolioCommitRequest>(JsonOptions)
            ?? throw new InvalidDataException("Shared preparation lacks its original transfer request.");
        if (!IsSharedTransferPreparation(record) || record["shared_source_selection"] is not null ||
            record["shared_source_transition"] is not null || record["original_transfer_preparation"] is not null ||
            record["shared_source_return_protocol"] is not null || record["original_transfer_settlement"] is not null ||
            record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath))
            throw new InvalidDataException("Shared preparation changed its owner/source/protocol boundary.");
        var pins = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var selected = ReadSharedSourceDecision(decision.WithOutputs("", "", ""), pins);
        if (selected.OptionId != "inventory.transfer_item")
            throw new InvalidDataException("Shared preparation requires an original selected material transfer.");
        if (decision.DecisionReference is not null) pins.Pin(decision.DecisionReference.Path);
        var support = CropTeacherSharedTransferSupport.Open(plan.RequestPath, headSnapshotPath, headLedgerPath,
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

    private JsonObject CreateSharedTransferPreparationIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_managed_shared_transfer_support.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["support_step_id"] = Text(record, "support_step_id"), ["runtime_directory"] = StoredPath(record, "runtime_directory"),
        ["preparation_runtime_directory"] = StoredPath(record, "preparation_runtime_directory"),
        ["native_receipt_path"] = StoredPath(record, "native_receipt_path"),
        ["native_after_snapshot_path"] = StoredPath(record, "native_after_snapshot_path"),
        ["shared_transfer_decision"] = record["shared_transfer_decision"]!.DeepClone(),
        ["shared_transfer_request"] = record["shared_transfer_request"]!.DeepClone(),
        ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_execution_owner"] = "global_selected_work", ["crop_production_credit_authorized"] = false,
        ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
    };
}
