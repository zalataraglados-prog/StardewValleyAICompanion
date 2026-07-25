using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void
        ApplyStoragePlacementRequestFields(
            TrainingExecutionRequest request,
            JsonObject? item)
    {
        if (!string.Equals(
                request.OptionId,
                "executor.place_storage",
                StringComparison.Ordinal))
        {
            return;
        }

        request.NativeStorageBranch =
            ReadQueueParameterString(
                item,
                "native_storage_branch");
        request.SpecialChestType =
            ReadQueueParameterString(
                item,
                "special_chest_type");
        request.ExpectedStorageCapacity =
            ReadQueueParameterInt(
                item,
                "actual_capacity");
        request.StorageRole =
            ReadQueueParameterString(
                item,
                "storage_role");
    }
}
