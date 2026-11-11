using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // This admission consumes an existing native outcome; it does not dispatch tools.
    public async Task<CropTeacherManagedSeedOperation> AdmitWateringMaintenanceAsync(string commitmentId,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "watering_maintenance";
            record["commitment_id"] = commitmentId;
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            record["maintenance_transition"] = JsonSerializer.SerializeToNode(transition with
            {
                QueuePath = Path.GetFullPath(transition.QueuePath), BeforeSnapshotPath = Path.GetFullPath(transition.BeforeSnapshotPath),
                ExecutionReceiptPath = Path.GetFullPath(transition.ExecutionReceiptPath), AfterSnapshotPath = Path.GetFullPath(transition.AfterSnapshotPath)
            }, JsonOptions);
            var scope = OpenWateringMaintenance(record);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, scope);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                watering: scope)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task RunWateringMaintenanceAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) => RunSupportingTransitionAsync(operation, http, commit, post, watering: true);

    private CropTeacherWateringMaintenance OpenWateringMaintenance(JsonObject record)
    {
        var transition = record["maintenance_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Managed crop maintenance lacks its original native transition.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            record["route_transition"] is not null || record["route_execution_protocol"] is not null || record["maintenance_execution_protocol"] is not null ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !plan.CoveredCommitmentIds.Contains(Text(record, "commitment_id"), StringComparer.Ordinal) ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion)
            throw new InvalidDataException("Managed crop maintenance changed its original owner/run/executor/active coverage boundary.");
        var scope = CropTeacherWateringMaintenance.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, Text(record, "commitment_id"), transition, Text(record, "support_step_id"));
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (scope.InputHashes[StoredPath(record, "before_snapshot_path")] != expected)
            throw new InvalidDataException("Managed crop maintenance source has no accepted continuity.");
        if (scope.InputHashes.Keys.Any(path => Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Managed crop maintenance runtime overlaps retained native inputs.");
        return scope;
    }

    private JsonObject CreateWateringMaintenanceIntent(JsonObject record, CropTeacherWateringMaintenance scope)
    {
        var intent = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_watering_maintenance.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
            ["commitment_id"] = Text(record, "commitment_id"), ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
            ["maintenance_transition"] = record["maintenance_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope.InputHashes, JsonOptions),
            ["rebind_request"] = JsonSerializer.SerializeToNode(scope.RebindRequest, JsonOptions),
            ["native_dispatch_performed_by_adapter"] = false, ["native_crop_lifecycle_verified"] = false,
            ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
        };
        AddWateringOutcomeEvidence(scope, intent);
        return intent;
    }

    private static void AddWateringOutcomeEvidence(CropTeacherWateringMaintenance scope, JsonObject document)
    {
        document["native_watering_prefix_verified"] = !scope.RefillControlVerified && !scope.RefillApproachVerified;
        if (scope.RefillControlVerified) document["native_refill_control_verified"] = true;
        if (scope.RefillApproachVerified) document["native_refill_approach_verified"] = true;
        document["applied_tile_count"] = scope.AppliedTileCount;
        document["complete_batch"] = scope.CompleteBatch;
        document["applied_tiles"] = JsonSerializer.SerializeToNode(scope.AppliedTiles, JsonOptions);
    }
}
