using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal void ValidateOriginalSupportingExecutor(LiveTrainingOptions live, bool watering)
    {
        if (!live.UseRuntimeTestHarnessExecutor || !live.EmitQueueExecutionReceipt || live.RunId != options.RunId ||
            live.PolicyTrajectoryExecutorVersion != options.ExecutorVersion ||
            CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(live.BackendUrl) != options.BackendUrl ||
            !SamePath(live.SnapshotDir, options.PortfolioJournalDirectory))
            throw new InvalidDataException((watering ? "Original watering" : "Original route") + " compiler/executor requires receipt emission and this owner's original run/backend/journal identity.");
    }

    private static void RequireNewSupportingOutputs(MoneyRevenueCropNativeLifecycleTransitionRequest transition, bool watering)
    {
        if (new[] { transition.ExecutionReceiptPath, transition.AfterSnapshotPath }.Any(path => File.Exists(path) || Directory.Exists(path)))
            throw new IOException((watering ? "Original watering" : "Original route") + " requires new native receipt/snapshot paths before dispatch.");
    }

    private static string NativeSupportingRole(CropTeacherManagedSeedOperation operation) => operation.IsControlRecovery ? "control" : operation.IsOvernight ? "overnight" : operation.IsHarvest ? "harvest" : operation.IsWateringMaintenance ? "maintenance" : "route";
    private static string SupportingOperationTag(CropTeacherManagedSeedOperation operation) => operation.IsControlRecovery ? "control_recovery" : operation.IsOvernight ? "overnight_control" : operation.IsHarvest ? "crop_harvest" :
        operation.IsWateringMaintenance ? "watering_maintenance" : "supporting_route";
    private static bool IsSupportingKind(CropTeacherManagedSeedOperation operation, CropTeacherSupportingOperationKind kind) => kind switch
    {
        CropTeacherSupportingOperationKind.Route => operation.IsSupportingRoute,
        CropTeacherSupportingOperationKind.Watering => operation.IsWateringMaintenance,
        CropTeacherSupportingOperationKind.Harvest => operation.IsHarvest,
        CropTeacherSupportingOperationKind.Overnight => operation.IsOvernight,
        CropTeacherSupportingOperationKind.Control => operation.IsControlRecovery,
        _ => false
    };
    private static string NativeSupportingPath(CropTeacherManagedSeedOperation operation, string suffix) =>
        Path.Combine(operation.RuntimeDirectory, NativeSupportingRole(operation) + "-native-" + suffix + ".json");
    private static MoneyRevenueCropNativeLifecycleTransitionRequest OriginalSupportingTransition(CropTeacherManagedSeedOperation operation) =>
        operation.IsControlRecovery ? operation.Record["control_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! :
        operation.IsOvernight ? operation.Record["overnight_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! :
        operation.IsHarvest ? operation.Record["harvest_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! :
        operation.IsWateringMaintenance ? operation.Record["maintenance_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)! : RouteTransition(operation.Record);

    private async Task ExecuteOriginalSupportingAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<Task<JsonObject>> executeOriginal, CropTeacherSupportingOperationKind kind)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (!IsSupportingKind(operation, kind) || operation.OriginalStart is null)
                throw new InvalidOperationException("Original supporting execution requires its matching prepared admission.");
            var watering = kind == CropTeacherSupportingOperationKind.Watering;
            var start = operation.OriginalStart!;
            var root = operation.RuntimeDirectory;
            var claimPath = NativeSupportingPath(operation, "claim");
            var label = operation.IsControlRecovery ? "Original control recovery" : operation.IsOvernight ? "Original overnight" : operation.IsHarvest ? "Original harvest" : watering ? "Original watering" : "Original route";
            if (operation.Started is not null || File.Exists(claimPath))
                throw new InvalidOperationException(label + " native invocation is already claimed; verify retained outcome, never redispatch.");
            var transition = OriginalSupportingTransition(operation);
            RequireNewSupportingOutputs(transition, watering);
            RequireDirectoryPath(root);
            Directory.CreateDirectory(root);
            var intentPath = SupportingIntentPath(operation);
            if (Directory.EnumerateFileSystemEntries(root).Any(path => Path.GetFileName(path) != Path.GetFileName(intentPath) && Path.GetFileName(path) != "teacher-ledgers"))
                throw new IOException(watering ? "Unclaimed original watering runtime contains unrelated execution artifacts." : "Unclaimed original route runtime contains unrelated execution artifacts.");
            var intent = CreateSupportingTransitionIntent(operation.Record, operation.SupportingTransition);
            if (File.Exists(intentPath))
            {
                if (!JsonNode.DeepEquals(intent, Read<JsonObject>(intentPath)))
                    throw new InvalidDataException(label + " retained intent changed.");
            }
            else await write(intentPath, intent.ToJsonString(JsonOptions)).ConfigureAwait(false);
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, root,
                start.BaseLedger.SourceStateHash, JsonOptions, watering ? "Original crop watering dispatch authority" : "Original crop route dispatch authority").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(start.BaseLedger, before.Ledger, watering ? "original watering dispatch authority" : "original route dispatch authority");
            ValidateSupportingTransition(operation);
            await SaveAsync(claimPath, CreateOriginalSupportingNativeClaim(operation)).ConfigureAwait(false);
            var claimHash = HashFile(claimPath);
            operation.RuntimeArtifactGuard = () =>
            {
                if (HashFile(claimPath) != claimHash) throw new InvalidDataException(label + " invocation claim changed.");
            };
            operation.ExecutionActive = true;
            var execution = await executeOriginal().ConfigureAwait(false);
            if (!SamePath(Text(execution, "execution_path"), transition.ExecutionReceiptPath) ||
                !SamePath(Text(execution, "after_snapshot_path"), transition.AfterSnapshotPath))
                throw new InvalidDataException(label + " executor returned different receipt/snapshot paths.");
            await EnsureOriginalSupportingOutcomeAsync(operation).ConfigureAwait(false);
        }
        finally { operation.ExecutionActive = false; gate.Release(); }
    }

    private JsonObject CreateOriginalSupportingNativeClaim(CropTeacherManagedSeedOperation operation) => new()
    {
        ["schema_version"] = "crop_teacher_owned_" + NativeSupportingRole(operation) + "_native_claim.v1", ["admission_sha256"] = HashFile(operation.RecordPath),
        ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"), ["native_invocation_claimed"] = true,
        ["native_request_performed"] = null, ["formal_product_training_authorized"] = false
    };
}
