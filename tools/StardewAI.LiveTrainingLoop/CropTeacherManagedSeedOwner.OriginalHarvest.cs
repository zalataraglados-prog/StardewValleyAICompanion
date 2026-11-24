using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalHarvestCompilation(CropTeacherRunStep Step, string ModelActionPath,
    string QueuePath, string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RetainSaleStock { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TransferableSaleStock { get; init; }
}

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalHarvestAsync(Func<Task<CropTeacherOriginalHarvestCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing crop phase before original harvest compilation.");
            var value = await compileOriginal().ConfigureAwait(false);
            if (value.TransferableSaleStock && !value.RetainSaleStock)
                throw new InvalidDataException("Transferable harvest stock requires the explicit retained-stock mode.");
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
            record["operation_kind"] = "crop_harvest";
            record["harvest_execution_protocol"] = "original_loop.v1";
            if (value.RetainSaleStock) record["harvest_output_reservation_protocol"] = value.TransferableSaleStock ?
                "native_sale_stock.v2" : "native_sale_stock.v1";
            record["commitment_id"] = value.Step.CommitmentId;
            record["harvest_step"] = JsonSerializer.SerializeToNode(value.Step, JsonOptions);
            record["original_model_action_path"] = Path.GetFullPath(value.ModelActionPath);
            record["active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
            var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("crop_actions", value.Step.TotalDay,
                Path.GetFullPath(value.QueuePath), headSnapshotPath, Path.GetFullPath(value.ReceiptPath),
                Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
            record["harvest_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
            var start = OpenOriginalHarvestStart(record, requireNativeIdentity: false);
            record["harvest_transition"] = JsonSerializer.SerializeToNode(transition with { SelectedCandidateId = start.CandidateId }, JsonOptions);
            RequireNewSupportingOutputs(transition, watering: false);
            record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, null);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                harvestStart: start)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task ExecuteOriginalHarvestAsync(CropTeacherManagedSeedOperation operation, HttpClient http, Func<Task<JsonObject>> executeOriginal) =>
        ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Harvest);

    internal Task ExecuteOriginalHarvestSettlingPickupAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<Task<JsonObject>> executeOriginal, Func<CropTeacherHarvest.NativeBatch, Task<CropTeacherHarvest>> settle) =>
        ExecuteOriginalSupportingAsync(operation, http, executeOriginal, CropTeacherSupportingOperationKind.Harvest, settle);

    internal static string HarvestPickupSettlementPath(CropTeacherManagedSeedOperation operation) =>
        Path.Combine(operation.RuntimeDirectory, "harvest-pickup", "settlement.json");

    internal static ICropTeacherSupportingTransition VerifyOriginalSupportingScope(CropTeacherManagedSeedOperation operation)
    {
        var transition = OriginalSupportingTransition(operation);
        var path = HarvestPickupSettlementPath(operation);
        return operation.HarvestStart is { } harvest && File.Exists(path) ? harvest.VerifyTransition(transition, path) :
            operation.OriginalStart!.VerifyTransition(transition);
    }

    public Task RunHarvestAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) => RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.Harvest);

    private CropTeacherHarvest.Preparation OpenOriginalHarvestStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = record["harvest_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
            ?? throw new InvalidDataException("Original harvest transition is missing.");
        var step = record["harvest_step"]?.Deserialize<CropTeacherRunStep>(JsonOptions)
            ?? throw new InvalidDataException("Original harvest step is missing.");
        var active = record["active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null || record["route_transition"] is not null ||
            record["maintenance_transition"] is not null || record["route_execution_protocol"] is not null || record["maintenance_execution_protocol"] is not null ||
            record["harvest_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => !Path.IsPathFullyQualified(path)) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion || transition.TransitionKind != "crop_actions" ||
            step.CommitmentId != Text(record, "commitment_id") || !OriginalCropBatchMatches(step, "executor.harvest_crop") ||
            active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original harvest changed its owner/run/executor/source or original due batch.");
        var outputProtocol = record["harvest_output_reservation_protocol"]?.GetValue<string>();
        if (outputProtocol is not null and not "native_sale_stock.v1" and not "native_sale_stock.v2")
            throw new InvalidDataException("Original harvest output reservation protocol is unknown.");
        var start = outputProtocol is null
            ? CropTeacherHarvest.Preparation.Open(options.NativeRunPlanPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
                active, step.CommitmentId, Text(record, "support_step_id"), step.TotalDay, step.ActionBatch!.BatchIndex,
                StoredPath(record, "original_model_action_path"), transition.QueuePath)
            : outputProtocol == "native_sale_stock.v2" ? CropTeacherHarvest.Preparation.OpenRetainingTransferableSaleStock(
                options.NativeRunPlanPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
                active, step.CommitmentId, Text(record, "support_step_id"), step.TotalDay, step.ActionBatch!.BatchIndex,
                StoredPath(record, "original_model_action_path"), transition.QueuePath)
            : CropTeacherHarvest.Preparation.OpenRetainingSaleStock(options.NativeRunPlanPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
                active, step.CommitmentId, Text(record, "support_step_id"), step.TotalDay, step.ActionBatch!.BatchIndex,
                StoredPath(record, "original_model_action_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected || transition.TotalDay != step.TotalDay || !start.Tiles.SequenceEqual(step.ActionBatch.Tiles) ||
            requireNativeIdentity && transition.SelectedCandidateId != start.CandidateId)
            throw new InvalidDataException("Original harvest does not continue its accepted source/candidate/day/targets.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) || start.InputHashes.Keys.Any(path =>
            SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) || Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original harvest native output overlaps its retained inputs.");
        return start;
    }

    private JsonObject CreateOriginalHarvestIntent(JsonObject record)
    {
        var intent = new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_harvest_execution.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
            ["commitment_id"] = Text(record, "commitment_id"), ["harvest_step"] = record["harvest_step"]!.DeepClone(),
            ["original_model_action_path"] = StoredPath(record, "original_model_action_path"),
            ["active_commitment_ids"] = record["active_commitment_ids"]!.DeepClone(),
            ["harvest_transition"] = record["harvest_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
            ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false
        };
        if (record["harvest_output_reservation_protocol"] is { } protocol)
            intent["harvest_output_reservation_protocol"] = protocol.DeepClone();
        return intent;
    }

    private static void AddHarvestOutcomeEvidence(CropTeacherHarvest scope, JsonObject document)
    {
        document["native_harvest_prefix_verified"] = true;
        document["applied_tile_count"] = scope.AppliedTileCount;
        document["complete_batch"] = scope.CompleteBatch;
        document["applied_tiles"] = JsonSerializer.SerializeToNode(scope.AppliedTiles, JsonOptions);
        document["harvest_wave_index"] = scope.WaveIndex;
        document["output_retention_key"] = scope.OutputRetentionKey;
        document["observed_retained_quantity_increase"] = scope.RetainedQuantityIncrease;
        if (scope.PickupSettlementPath is { } pickup)
        {
            document["harvest_pickup_settlement_path"] = pickup;
            document["harvest_settled_snapshot_path"] = scope.AfterSnapshotPath;
        }
        document["whole_wave_output_retention_verified"] = false;
        var saleClaims = scope.RebindRequest.MaterialClaims;
        if (saleClaims.Length > 0)
            document["sale_stock_reservation_ids"] = JsonSerializer.SerializeToNode(saleClaims.Select(row => row.ReservationId).ToArray(), JsonOptions);
    }
}
