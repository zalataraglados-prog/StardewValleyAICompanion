using System.Text;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static string SupportingIntentPath(CropTeacherManagedSeedOperation operation) =>
        Path.Combine(operation.RuntimeDirectory, NativeSupportingRole(operation) + "-intent.json");

    private JsonObject CreateSupportingTransitionIntent(JsonObject record, ICropTeacherSupportingTransition? scope) =>
        record["operation_kind"]?.GetValue<string>() == "crop_harvest" ? CreateOriginalHarvestIntent(record) :
        record["maintenance_execution_protocol"]?.GetValue<string>() == "original_loop.v1" ? CreateOriginalWateringIntent(record) :
        scope is CropTeacherWateringMaintenance watering
            ? CreateWateringMaintenanceIntent(record, watering)
            : CreateSupportingRouteIntent(record, (CropTeacherSupportingRoute?)scope);

    private string[] ActiveCoveredCommitments() => Read<StrategyCommitmentLedger>(headLedgerPath).CropPlantingCommitments
        .Where(row => row.Status == StrategyCommitmentStatuses.Active && plan.CoveredCommitmentIds.Contains(row.CommitmentId, StringComparer.Ordinal))
        .Select(row => row.CommitmentId).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private string SupportingTransitionIntentHash(JsonObject record, ICropTeacherSupportingTransition? scope) =>
        PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(CreateSupportingTransitionIntent(record, scope).ToJsonString(JsonOptions)));

    private void ValidateSupportingTransition(CropTeacherManagedSeedOperation operation)
    {
        if (operation.IsSupportingRoute) ValidateSupportingRoute(operation);
        else if (operation.IsHarvest)
        {
            operation.HarvestStart!.ValidateInputsUnchanged();
            operation.Harvest?.ValidateInputsUnchanged();
            if (operation.Harvest is not null) operation.RuntimeArtifactGuard?.Invoke();
            if (SupportingTransitionIntentHash(operation.Record, operation.Harvest) != Text(operation.Record, "original_intent_sha256"))
                throw new InvalidDataException("Managed crop harvest changed its admitted original intent.");
        }
        else
        {
            if (operation.Watering is null && operation.WateringStart is null)
                throw new InvalidOperationException("Managed crop operation has no supporting transition.");
            operation.WateringStart?.ValidateInputsUnchanged();
            operation.Watering?.ValidateInputsUnchanged();
            if (operation.WateringStart is not null && operation.Watering is not null) operation.RuntimeArtifactGuard?.Invoke();
            var scope = operation.Watering;
            if (SupportingTransitionIntentHash(operation.Record, scope) != Text(operation.Record, "original_intent_sha256"))
                throw new InvalidDataException("Managed crop watering maintenance changed its admitted original intent.");
        }
    }

    private async Task RunSupportingTransitionAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post, CropTeacherSupportingOperationKind kind)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (!IsSupportingKind(operation, kind))
                throw new InvalidOperationException("Managed crop operation does not match the selected supporting phase.");
            var watering = kind == CropTeacherSupportingOperationKind.Watering;
            if (operation.OriginalStart is not null)
                await EnsureOriginalSupportingOutcomeAsync(operation).ConfigureAwait(false);
            await ClaimRuntimeInvocationAsync(operation).ConfigureAwait(false);
            var scope = operation.SupportingTransition!;
            var root = operation.RuntimeDirectory;
            if (operation.OriginalStart is null)
            {
                if (Directory.Exists(root) || File.Exists(root)) throw new IOException(watering
                    ? "Retained maintenance runtime requires recovery, not another mutation."
                    : "Retained route runtime requires recovery, not another mutation.");
                Directory.CreateDirectory(root);
                var intent = CreateSupportingTransitionIntent(operation.Record, scope);
                await write(SupportingIntentPath(operation), intent.ToJsonString(JsonOptions)).ConfigureAwait(false);
            }
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, root,
                scope.BaseLedger.SourceStateHash, JsonOptions, watering ? "Crop watering maintenance base" : "Crop supporting route base").ConfigureAwait(false);
            CropTeacherSeedRuntimeTransaction.RequireLedger(scope.BaseLedger, before.Ledger, watering ? "watering maintenance base" : "supporting route base");
            ValidateSupportingTransition(operation);
            var reply = await commit(new JsonObject(), scope.RebindRequest).ConfigureAwait(false);
            await write(Path.Combine(root, "marker-reply.json"), reply.ToJsonString(JsonOptions)).ConfigureAwait(false);
            // Local success still requires the original durable journal and full replay.
            var recovered = await ReadSupportingTransitionBoundaryAsync(http, operation, root, post).ConfigureAwait(false);
            await AdvanceAsync(operation, recovered.Evidence, recovered.SnapshotPath, recovered.Validate,
                operation.IsHarvest ? "live_crop_harvest" : watering ? "live_watering_maintenance" : "live_supporting_route").ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
