using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static void RequireManagedCropDispatch(CropTeacherManagedSeedOwner? owner, CropTeacherManagedSeedOperation? operation)
    {
        if (owner is not null && (operation is null || !ReferenceEquals(operation.Owner, owner) || !ReferenceEquals(owner.Pending, operation)))
            throw new InvalidOperationException("An active managed crop owner cannot dispatch an unowned control queue; retain and bind its original control boundary first.");
    }

    private static async Task<JsonObject> ExecuteCropTeacherManagedOperationAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation,
        string queuePath, Func<Task<JsonObject>> executeOriginal)
    {
        if (operation.IsSupportingTransition)
        {
            var kind = operation.IsControlRecovery ? CropTeacherSupportingOperationKind.Control : operation.IsOvernight ? CropTeacherSupportingOperationKind.Overnight : operation.IsHarvest
                ? CropTeacherSupportingOperationKind.Harvest : operation.IsWateringMaintenance
                    ? CropTeacherSupportingOperationKind.Watering : CropTeacherSupportingOperationKind.Route;
            await ExecuteCropTeacherOwnedSupportingCoreAsync(http, executorHttp, options, iteration, owner, operation, kind).ConfigureAwait(false);
            return JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(
                Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json")))!.AsObject();
        }
        var result = await owner.ExecuteSeedAsync(operation, async runtime =>
        {
            runtime.ValidateDispatchQueue(queuePath);
            var execution = await executeOriginal().ConfigureAwait(false);
            await runtime.CompleteAsync(queuePath, ReadString(execution, "execution_path"), ReadString(execution, "after_snapshot_path"),
                ReadString(execution, "selected_candidate_id")).ConfigureAwait(false);
            return execution;
        }).ConfigureAwait(false);
        await owner.AcceptLiveAsync(operation, operation.PreparedRuntime!).ConfigureAwait(false);
        return result;
    }
}
