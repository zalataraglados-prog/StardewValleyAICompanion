using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // Admission consumes a completed original route. This API never dispatches movement.
    public async Task<CropTeacherManagedSeedOperation> AdmitSupportingRouteAsync(
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "supporting_route";
            record["route_transition"] = JsonSerializer.SerializeToNode(transition with
            {
                QueuePath = Path.GetFullPath(transition.QueuePath), BeforeSnapshotPath = Path.GetFullPath(transition.BeforeSnapshotPath),
                ExecutionReceiptPath = Path.GetFullPath(transition.ExecutionReceiptPath), AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath)
            }, JsonOptions);
            var scope = OpenSupportingRoute(record);
            record["original_intent_sha256"] = SupportingRouteIntentHash(record, scope);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                supportingRoute: scope)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task RunSupportingRouteAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) => RunSupportingTransitionAsync(operation, http, commit, post, watering: false);

    private CropTeacherSupportingRoute OpenSupportingRoute(JsonObject record)
    {
        if (record["route_execution_protocol"] is not null)
            throw new InvalidDataException("Completed-route admission cannot adopt an unknown native execution protocol.");
        var transition = record["route_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Managed crop supporting route lacks its original transition.");
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion)
            throw new InvalidDataException("Managed crop supporting route changed its original owner/run/executor boundary.");
        var scope = CropTeacherSupportingRoute.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            plan.CoveredCommitmentIds, transition, Text(record, "support_step_id"));
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (scope.InputHashes[StoredPath(record, "before_snapshot_path")] != expected)
            throw new InvalidDataException("Managed crop supporting route source has no accepted continuity.");
        if (scope.InputHashes.Keys.Any(path => Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Managed crop supporting route runtime overlaps retained route inputs.");
        return scope;
    }

    private JsonObject CreateSupportingRouteIntent(JsonObject record, CropTeacherSupportingRoute? scope) =>
        record["route_execution_protocol"]?.GetValue<string>() == "original_loop.v1" ? CreateOriginalRouteIntent(record) : new()
    {
        ["schema_version"] = "crop_teacher_managed_supporting_route.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["route_transition"] = record["route_transition"]!.DeepClone(),
        ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope!.InputHashes, JsonOptions),
        ["rebind_request"] = JsonSerializer.SerializeToNode(scope.RebindRequest, JsonOptions),
        ["native_dispatch_performed_by_adapter"] = false, ["native_crop_lifecycle_verified"] = false,
        ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
    };

    private string SupportingRouteIntentHash(JsonObject record, CropTeacherSupportingRoute? scope) =>
        PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(CreateSupportingRouteIntent(record, scope).ToJsonString(JsonOptions)));

    private void ValidateSupportingRoute(CropTeacherManagedSeedOperation operation)
    {
        operation.RouteStart?.ValidateInputsUnchanged();
        operation.SupportingRoute?.ValidateInputsUnchanged();
        if (operation.RouteStart is not null && operation.SupportingRoute is not null) operation.RuntimeArtifactGuard?.Invoke();
        if (SupportingRouteIntentHash(operation.Record, operation.SupportingRoute) != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException("Managed crop supporting route changed its admitted original intent.");
    }
}
