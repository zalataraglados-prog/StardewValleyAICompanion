using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task AcceptStoppedSeedAsync(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (!HasOwnedDispatchProtocol(operation) || operation.Dispatch is null ||
                !ReferenceEquals(operation.PreparedRuntime, runtime) || runtime is not ICropTeacherSeedManagedRuntime original)
                throw new InvalidOperationException("Owned controller-stop acceptance requires its claimed original runtime.");
            var stopped = original.ReadVerifiedStopBoundary();
            RequirePrepared(operation, stopped.Preparation.OriginalIntentSha256);
            RequireIdentity(operation, stopped.Preparation.RuntimeDirectory, stopped.Preparation.BackendUrl,
                stopped.Preparation.RunId, stopped.Preparation.ExecutorVersion, stopped.Preparation.SupportStepId);
            stopped.Validate();
            operation.VerifiedStop = stopped;
            await AdvanceAsync(operation, stopped.Evidence, stopped.SnapshotPath, stopped.Validate, "original_controller_stop").ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private void AddStoppedPhaseEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        var stopped = operation.VerifiedStop!;
        stopped.Validate();
        accepted["seed_boundary_kind"] = "controller_stop_no_native_dispatch";
        accepted["controller_stop_transition_sha256"] = HashFile(stopped.TransitionPath);
        accepted["controller_stop_receipt_path"] = stopped.ReceiptPath;
        accepted["controller_stop_receipt_sha256"] = HashFile(stopped.ReceiptPath);
        accepted["native_request_count"] = 0;
        accepted["native_outcome_verified"] = false;
    }

    private void ValidateStoppedPhaseEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (!HasOwnedDispatchProtocol(operation) || operation.Dispatch is null || accepted["original_proof_sha256"] is not null ||
            accepted["native_request_count"]?.GetValue<int>() != 0 || accepted["native_outcome_verified"]?.GetValue<bool>() != false ||
            Text(accepted, "controller_stop_transition_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "controller-stop-transition.json")) ||
            Text(accepted, "controller_stop_receipt_sha256") != HashFile(StoredPath(accepted, "controller_stop_receipt_path")))
            throw new InvalidDataException("Managed controller-stop acceptance evidence changed.");
        CropTeacherSeedRuntimeTransaction.RequireNoNativeStopArtifacts(operation.RuntimeDirectory);
        var transition = Read<MoneyRevenueCropNativeLifecycleTransitionRequest>(Path.Combine(operation.RuntimeDirectory, "controller-stop-transition.json"));
        if (!SamePath(transition.ExecutionReceiptPath, StoredPath(accepted, "controller_stop_receipt_path")))
            throw new InvalidDataException("Managed controller-stop acceptance changed its original receipt path.");
        operation.StopAccepted = true;
    }
}
