using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherOriginalRouteCompilation(string Kind, string ModelPlanPath, string QueuePath,
    string ReceiptPath, string AfterSnapshotPath, string RuntimeDirectory);

public sealed partial class CropTeacherManagedSeedOwner
{
    internal void ValidateOriginalRouteExecutor(LiveTrainingOptions live)
    {
        if (!live.UseRuntimeTestHarnessExecutor || !live.EmitQueueExecutionReceipt || live.RunId != options.RunId ||
            live.PolicyTrajectoryExecutorVersion != options.ExecutorVersion ||
            CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(live.BackendUrl) != options.BackendUrl ||
            !SamePath(live.SnapshotDir, options.PortfolioJournalDirectory))
            throw new InvalidDataException("Original route compiler/executor requires receipt emission and this owner's original run/backend/journal identity.");
    }

    public async Task<CropTeacherManagedSeedOperation> AdmitOriginalRouteAsync(string kind, string modelPlanPath, string queuePath,
        string receiptPath, string afterSnapshotPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await AdmitOriginalRouteCoreAsync(new(kind, modelPlanPath, queuePath, receiptPath, afterSnapshotPath, runtimeDirectory)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<CropTeacherManagedSeedOperation> CompileAndAdmitOriginalRouteAsync(
        Func<Task<CropTeacherOriginalRouteCompilation>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the existing crop phase before original route compilation.");
            return await AdmitOriginalRouteCoreAsync(await compileOriginal().ConfigureAwait(false)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task<CropTeacherManagedSeedOperation> AdmitOriginalRouteCoreAsync(CropTeacherOriginalRouteCompilation value)
    {
        var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, value.RuntimeDirectory);
        record["operation_kind"] = "supporting_route";
        record["route_execution_protocol"] = "original_loop.v1";
        record["original_model_plan_path"] = Path.GetFullPath(value.ModelPlanPath);
        record["route_active_commitment_ids"] = JsonSerializer.SerializeToNode(ActiveCoveredCommitments(), JsonOptions);
        var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest(value.Kind, 0, Path.GetFullPath(value.QueuePath), headSnapshotPath,
            Path.GetFullPath(value.ReceiptPath), Path.GetFullPath(value.AfterSnapshotPath), options.RunId, options.ExecutorVersion, "pending-original-candidate");
        record["route_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
        var start = OpenOriginalRouteStart(record, requireNativeIdentity: false);
        transition = transition with { TotalDay = start.TotalDay, SelectedCandidateId = start.CandidateId };
        record["route_transition"] = JsonSerializer.SerializeToNode(transition, JsonOptions);
        RequireNewRouteOutputs(transition);
        record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
        record["original_intent_sha256"] = SupportingRouteIntentHash(record, null);
        return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null, routeStart: start)).ConfigureAwait(false);
    }

    public async Task ExecuteOriginalRouteAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<Task<JsonObject>> executeOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            var start = operation.RouteStart ?? throw new InvalidOperationException("Original route execution requires its prepared route admission.");
            var root = operation.RuntimeDirectory;
            var claimPath = Path.Combine(root, "route-native-claim.json");
            if (operation.Started is not null || File.Exists(claimPath))
                throw new InvalidOperationException("Original route native invocation is already claimed; verify retained outcome, never redispatch.");
            var transition = RouteTransition(operation.Record);
            RequireNewRouteOutputs(transition);
            RequireDirectoryPath(root);
            Directory.CreateDirectory(root);
            if (Directory.EnumerateFileSystemEntries(root).Any(path => Path.GetFileName(path) is not ("route-intent.json" or "teacher-ledgers")))
                throw new IOException("Unclaimed original route runtime contains unrelated execution artifacts.");
            var intentPath = Path.Combine(root, "route-intent.json");
            var intent = CreateOriginalRouteIntent(operation.Record);
            if (File.Exists(intentPath))
            {
                if (!JsonNode.DeepEquals(intent, Read<JsonObject>(intentPath)))
                    throw new InvalidDataException("Original route retained intent changed.");
            }
            else await write(intentPath, intent.ToJsonString(JsonOptions)).ConfigureAwait(false);
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, root,
                start.BaseLedger.SourceStateHash, JsonOptions, "Original crop route dispatch authority").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(start.BaseLedger, before.Ledger, "original route dispatch authority");
            ValidateSupportingRoute(operation);
            await SaveAsync(claimPath, CreateOriginalRouteNativeClaim(operation)).ConfigureAwait(false);
            var claimHash = HashFile(claimPath);
            operation.RuntimeArtifactGuard = () =>
            {
                if (HashFile(claimPath) != claimHash) throw new InvalidDataException("Original route invocation claim changed.");
            };
            operation.ExecutionActive = true;
            var execution = await executeOriginal().ConfigureAwait(false);
            if (!SamePath(Text(execution, "execution_path"), transition.ExecutionReceiptPath) ||
                !SamePath(Text(execution, "after_snapshot_path"), transition.AfterSnapshotPath))
                throw new InvalidDataException("Original route executor returned different receipt/snapshot paths.");
            await EnsureOriginalRouteOutcomeAsync(operation).ConfigureAwait(false);
        }
        finally { operation.ExecutionActive = false; gate.Release(); }
    }

    private CropTeacherSupportingRoute.Preparation OpenOriginalRouteStart(JsonObject record, bool requireNativeIdentity = true)
    {
        var transition = RouteTransition(record);
        if (new[] { transition.QueuePath, transition.BeforeSnapshotPath, transition.ExecutionReceiptPath, transition.AfterSnapshotPath }
            .Any(path => !Path.IsPathFullyQualified(path)))
            throw new InvalidDataException("Original route retained artifact paths must be absolute.");
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            record["route_execution_protocol"]?.GetValue<string>() != "original_loop.v1" ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            !SamePath(transition.BeforeSnapshotPath, StoredPath(record, "before_snapshot_path")) ||
            transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion)
            throw new InvalidDataException("Original route changed its owner/run/executor/source identity.");
        var active = record["route_active_commitment_ids"]?.Deserialize<string[]>(JsonOptions);
        if (active is null || !active.SequenceEqual(ActiveCoveredCommitments(), StringComparer.Ordinal))
            throw new InvalidDataException("Original route active coverage differs from its retained original ledger/plan.");
        var start = CropTeacherSupportingRoute.Preparation.Open(plan.RequestPath, StoredPath(record, "before_snapshot_path"), headLedgerPath,
            active, Text(record, "support_step_id"), transition.TransitionKind,
            StoredPath(record, "original_model_plan_path"), transition.QueuePath);
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (start.InputHashes[start.SourcePath] != expected ||
            requireNativeIdentity && (transition.TotalDay != start.TotalDay || transition.SelectedCandidateId != start.CandidateId))
            throw new InvalidDataException("Original route does not continue its accepted source/candidate/day.");
        if (SamePath(transition.ExecutionReceiptPath, transition.AfterSnapshotPath) ||
            start.InputHashes.Keys.Any(path => SamePath(path, transition.ExecutionReceiptPath) || SamePath(path, transition.AfterSnapshotPath) ||
                Within(StoredPath(record, "runtime_directory"), path)))
            throw new IOException("Original route native output overlaps its retained inputs.");
        return start;
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest RouteTransition(JsonObject record) =>
        record["route_transition"]?.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)
        ?? throw new InvalidDataException("Original route transition is missing.");

    private static void RequireNewRouteOutputs(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        if (new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => File.Exists(path) || Directory.Exists(path)))
            throw new IOException("Original route requires new native receipt/snapshot paths before dispatch.");
    }

    private JsonObject CreateOriginalRouteIntent(JsonObject record) => new()
    {
        ["schema_version"] = "crop_teacher_owned_route_execution.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["original_model_plan_path"] = StoredPath(record, "original_model_plan_path"),
        ["route_active_commitment_ids"] = record["route_active_commitment_ids"]!.DeepClone(),
        ["route_transition"] = record["route_transition"]!.DeepClone(), ["input_artifact_hashes"] = record["input_artifact_hashes"]!.DeepClone(),
        ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
        ["formal_product_training_authorized"] = false
    };

    private JsonObject CreateOriginalRouteNativeClaim(CropTeacherManagedSeedOperation operation) => new()
    {
        ["schema_version"] = "crop_teacher_owned_route_native_claim.v1", ["admission_sha256"] = HashFile(operation.RecordPath),
        ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"), ["native_invocation_claimed"] = true,
        ["native_request_performed"] = null, ["formal_product_training_authorized"] = false
    };
}
