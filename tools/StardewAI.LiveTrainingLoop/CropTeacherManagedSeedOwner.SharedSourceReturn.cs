using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // Foreign execution has already returned. This phase owns only its original marker/rebind.
    public Task<CropTeacherManagedSeedOperation> AdmitSharedSourceReturnAsync(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory) =>
        AdmitSharedSourceReturnCoreAsync(transition, runtimeDirectory, null);

    public Task<CropTeacherManagedSeedOperation> AdmitSettledSharedTransferReturnAsync(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory,
        CropTeacherSharedTransferSettlementReference transfer) =>
        AdmitSharedSourceReturnCoreAsync(transition, runtimeDirectory, transfer ?? throw new ArgumentNullException(nameof(transfer)));

    public Task<CropTeacherManagedSeedOperation> AdmitPreparedSharedTransferReturnAsync(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory,
        CropTeacherSharedTransferSettlementReference transfer) =>
        AdmitSharedSourceReturnCoreAsync(transition, runtimeDirectory, transfer ?? throw new ArgumentNullException(nameof(transfer)), includePreparation: true);

    private async Task<CropTeacherManagedSeedOperation> AdmitSharedSourceReturnCoreAsync(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory,
        CropTeacherSharedTransferSettlementReference? transfer, bool includePreparation = false)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "shared_source_return";
            record["shared_source_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            if (transfer is not null)
            {
                if (includePreparation != (transfer.Preparation is not null))
                    throw new InvalidDataException("Shared transfer return requires its explicitly selected preparation mode.");
                record["shared_source_return_protocol"] = includePreparation ? "settled_material_transfer.v2" : "settled_material_transfer.v1";
                record["original_transfer_settlement"] = JsonSerializer.SerializeToNode(transfer, JsonOptions);
            }
            var scope = OpenSharedSourceReturn(record);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, scope);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                sharedSourceReturn: scope)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task RunSharedSourceReturnAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) =>
        RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.SharedSourceReturn);

    private CropTeacherSharedSourceReturn OpenSharedSourceReturn(JsonObject record)
    {
        var transition = record["shared_source_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Managed shared source return lacks its original native boundary.");
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            record["shared_source_execution_protocol"] is not null || record["shared_source_selection"] is not null ||
            !SamePath(StoredPath(record, "before_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !SamePath(transition.BeforeSnapshotPath, headSnapshotPath) || transition.RunId != options.RunId ||
            transition.ExecutorVersion != options.ExecutorVersion)
            throw new InvalidDataException("Managed shared source return differs from this owner's original source/run/executor.");
        var scope = OpenRecordedSharedSourceScope(record, headSnapshotPath, headLedgerPath, ActiveCoveredCommitments(), transition);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (scope.InputHashes[headSnapshotPath] != expected)
            throw new InvalidDataException("Managed shared source return lost its accepted original source.");
        var runtime = StoredPath(record, "runtime_directory");
        if (scope.InputHashes.Keys.Any(path => Within(runtime, path)))
            throw new IOException("Managed shared source return runtime overlaps retained original inputs.");
        RequireOutsideNativeSaveSlot(new[] { runtime }, options.NativeSaveBinding);
        return scope;
    }

    private CropTeacherSharedSourceReturn OpenRecordedSharedSourceScope(JsonObject record, string sourcePath, string ledgerPath,
        string[] covered, MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var productYield = IsSharedTransferPreparation(record) && transition.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor;
        record = SharedExecutionRecord(record);
        var protocol = record["shared_source_return_protocol"]?.GetValue<string>();
        if (record["shared_source_execution_protocol"]?.GetValue<string>() == "selected_work_yield.v2")
        {
            var yieldPreparation = record["original_transfer_preparation"]?.Deserialize<CropTeacherSharedTransferPreparationReference>(JsonOptions);
            var path = SharedTransferReferencePath(record);
            var reference = Read<CropTeacherSharedTransferSettlementReference>(path);
            if (protocol is not null || record["original_transfer_settlement"] is not null || yieldPreparation is null ||
                yieldPreparation.BackendUrl != options.BackendUrl || reference.Preparation != yieldPreparation || reference.BackendUrl != options.BackendUrl)
                throw new InvalidDataException("Shared transfer yield changed its original preparation or settlement protocol.");
            return productYield
                ? CropTeacherSharedSourceReturn.OpenAfterPreparedProductTransferYield(plan.RequestPath, sourcePath, ledgerPath,
                    covered, transition, Text(record, "support_step_id"), reference, path)
                : CropTeacherSharedSourceReturn.OpenAfterPreparedTransferYield(plan.RequestPath, sourcePath, ledgerPath,
                    covered, transition, Text(record, "support_step_id"), reference, path);
        }
        if (protocol is null && record["original_transfer_settlement"] is null)
            return CropTeacherSharedSourceReturn.Open(plan.RequestPath, sourcePath, ledgerPath, covered,
                transition, Text(record, "support_step_id"));
        var transfer = record["original_transfer_settlement"]?.Deserialize<CropTeacherSharedTransferSettlementReference>(JsonOptions);
        var includePreparation = protocol == "settled_material_transfer.v2";
        if (protocol is not "settled_material_transfer.v1" and not "settled_material_transfer.v2" ||
            transfer is null || transfer.BackendUrl != options.BackendUrl ||
            includePreparation != (transfer.Preparation is not null) ||
            transfer.Preparation is { } preparation && preparation.BackendUrl != options.BackendUrl ||
            record["shared_source_execution_protocol"] is not null)
            throw new InvalidDataException("Shared transfer return changed its original recorded protocol or backend.");
        if (includePreparation)
            return CropTeacherSharedSourceReturn.OpenAfterPreparedAndSettledMaterialTransfer(plan.RequestPath, sourcePath,
                ledgerPath, covered, transition, Text(record, "support_step_id"), transfer);
        return CropTeacherSharedSourceReturn.OpenAfterSettledMaterialTransfer(plan.RequestPath, sourcePath, ledgerPath,
            covered, transition, Text(record, "support_step_id"), transfer);
    }

    private JsonObject CreateSharedSourceReturnIntent(JsonObject record, CropTeacherSharedSourceReturn scope)
    {
        var result = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_shared_source_return.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
            ["native_transition"] = record["shared_source_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope.InputHashes, JsonOptions),
            ["rebind_request"] = JsonSerializer.SerializeToNode(scope.RebindRequest, JsonOptions),
            ["native_dispatch_performed_by_adapter"] = false, ["native_crop_lifecycle_verified"] = false,
            ["crop_production_credit_authorized"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false
        };
        if (scope.TransferSettlement is not null)
        {
            result["shared_source_return_protocol"] = record["shared_source_return_protocol"]!.DeepClone();
            result["original_transfer_settlement"] = JsonSerializer.SerializeToNode(scope.TransferSettlement, JsonOptions);
        }
        return result;
    }

    private static void AddSharedSourceReturnEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.SharedSourceReturn is not { } scope) return;
        scope.ValidateInputsUnchanged();
        accepted["native_shared_source_return_verified"] = true;
        accepted["shared_source_state_hash"] = scope.SourceStateHash;
        accepted["shared_after_state_hash"] = scope.AfterStateHash;
        accepted["shared_verified_native_step_count"] = scope.VerifiedNativeStepCount;
        accepted["crop_production_credit_authorized"] = false;
        if (scope.TransferSettlement is { } transfer)
        {
            accepted["native_shared_material_transfer_settlement_verified"] = true;
            accepted["shared_material_transfer_request_sha256"] = scope.InputHashes[transfer.RequestPath];
            accepted["shared_material_transfer_reply_sha256"] = scope.InputHashes[transfer.ReplyPath];
            if (transfer.Preparation is { } preparation)
            {
                accepted["shared_material_transfer_preparation_request_sha256"] = scope.InputHashes[preparation.RequestPath];
                accepted["shared_material_transfer_preparation_reply_sha256"] = scope.InputHashes[preparation.ReplyPath];
            }
        }
    }

    private void ValidateSharedSourceReturnEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        var fields = new[] { "native_shared_source_return_verified", "shared_source_state_hash", "shared_after_state_hash",
            "shared_verified_native_step_count", "crop_production_credit_authorized",
            "shared-source_native_claim_sha256", "shared-source_native_result_sha256",
            "native_shared_material_transfer_settlement_verified", "shared_material_transfer_request_sha256",
            "shared_material_transfer_reply_sha256", "shared_material_transfer_preparation_request_sha256",
            "shared_material_transfer_preparation_reply_sha256" };
        if (!operation.IsSharedSourceReturn)
        {
            if (fields.Any(name => accepted[name] is not null))
                throw new InvalidDataException("A different phase cannot claim shared source return evidence.");
            return;
        }
        if (operation.SharedSourceStart is null &&
            new[] { "shared-source_native_claim_sha256", "shared-source_native_result_sha256" }.Any(name => accepted[name] is not null))
            throw new InvalidDataException("Observed-only source return cannot claim ownership of a native invocation.");
        if (operation.SharedSourceStart is not null) LoadOriginalSupportingOutcome(operation);
        if (operation.SharedSourceReturn!.TransferSettlement is null &&
            new[] { "native_shared_material_transfer_settlement_verified", "shared_material_transfer_request_sha256",
                "shared_material_transfer_reply_sha256" }.Any(name => accepted[name] is not null))
            throw new InvalidDataException("A marker-only source return cannot claim a material transfer settlement.");
        if (operation.SharedSourceReturn.TransferSettlement?.Preparation is null &&
            new[] { "shared_material_transfer_preparation_request_sha256", "shared_material_transfer_preparation_reply_sha256" }
                .Any(name => accepted[name] is not null))
            throw new InvalidDataException("A source return cannot claim an unbound material transfer preparation.");
        var expected = new JsonObject(); AddSharedSourceReturnEvidence(operation, expected);
        if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])) ||
            new[] { "route_target_reached", "native_watering_prefix_verified", "native_harvest_prefix_verified",
                "native_overnight_control_verified", "native_control_recovery_verified", "native_terminal_shipping_step_verified",
                "native_saved_content_readback_verified", "route_native_claim_sha256", "route_native_result_sha256" }
                .Any(name => accepted[name] is not null))
            throw new InvalidDataException("Managed shared source return changed or claimed unrelated native progress.");
    }
}
