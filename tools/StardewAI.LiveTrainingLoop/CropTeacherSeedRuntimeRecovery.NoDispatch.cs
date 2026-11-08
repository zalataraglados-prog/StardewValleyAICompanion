using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    internal static async Task<CropTeacherSeedNoDispatchBoundary> RecoverNoDispatchAsync(HttpClient http,
        CropTeacherSeedRuntimeRecoveryOptions options, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        CropTeacherSeedRuntimeTransaction.RequireNoNativeStopArtifacts(options.RuntimeDirectory);
        var context = OpenOriginalPreparation(options, scopes, step);
        var prepared = await ReadOriginalPreparationAsync(options, context, post, write);
        var artifacts = context.Artifacts;
        var transitionPath = Path.Combine(context.Root, "controller-stop-transition.json");
        var transition = artifacts.Read<MoneyRevenueCropNativeLifecycleTransitionRequest>(transitionPath);
        foreach (var path in new[] { transition.QueuePath, transition.ExecutionReceiptPath, transition.BeforeSnapshotPath, transition.AfterSnapshotPath })
            artifacts.Pin(path);
        string ValidateQueue(string path) => CropTeacherSeedRuntimeTransaction.ValidatePreparedQueue(prepared.Marker,
            prepared.QueuePath, prepared.QueueSha256, prepared.Queue, path);
        await CropTeacherSeedRuntimeTransaction.ValidateNoDispatchContextAsync(
            new(options.BackendUrl, context.Root, options.RunId, options.ExecutorVersion), context.Binding,
            prepared.Marker.Ledger, prepared.Queue, ValidateQueue, transition);
        artifacts.Validate();
        var evidence = await StrategyLedgerEvidenceStore.ReadAsync(http, context.Origin, context.Output,
            prepared.Marker.Ledger.SourceStateHash, JsonOptions, "Recovered original zero-dispatch authority");
        CropTeacherSeedRuntimeTransaction.RequireLedger(prepared.Marker.Ledger, evidence.Ledger, "no-dispatch readback");
        artifacts.Pin(evidence.Path);
        void Validate()
        {
            scopes.ValidateStep(step);
            artifacts.Validate();
            CropTeacherSeedRuntimeTransaction.RequireNoNativeStopArtifacts(context.Root);
        }
        Validate();
        var preparation = new CropTeacherSeedPreparedBoundary(context.Root, options.BackendUrl, options.RunId,
            options.ExecutorVersion, context.Request.PortfolioId, context.Binding.CommitmentId,
            context.Binding.InputHashes, context.Request, context.Binding.PreparationDetails?.DeepClone().AsObject(),
            options.ExpectedIntentSha256, prepared.QueuePath, prepared.QueueSha256, Validate);
        await write(Path.Combine(context.Output, "no-dispatch-recovery.json"), new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_no_dispatch_recovery.v1", ["run_id"] = options.RunId,
            ["original_intent_sha256"] = options.ExpectedIntentSha256, ["runtime_directory"] = context.Root,
            ["controller_stop_verified"] = true, ["native_request_count"] = 0, ["native_outcome_verified"] = false,
            ["native_dispatch_performed"] = false, ["portfolio_mutation_requested"] = false,
            ["formal_product_training_authorized"] = false,
            ["artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions)
        }.ToJsonString(JsonOptions));
        Validate();
        return new(preparation, evidence, context.Binding.SnapshotPath, transitionPath,
            Path.GetFullPath(transition.ExecutionReceiptPath), Validate);
    }
}
