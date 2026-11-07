using System;
namespace StardewAI.Contracts.Training;

public static class CropSeedPlantingInventoryContract
{
    public const int MaximumBatchSize = 8;

    public static bool IsPresent(TrainingExecutionRequest request) =>
        request.CropSeedPlantingVersion.HasValue || request.SeedInventoryQuality.HasValue;

    public static bool ValidShape(TrainingExecutionRequest request) =>
        request.CropSeedPlantingVersion == 1 && request.InventorySlotIndex >= 0 &&
        request.SeedInventoryQuality >= 0 && request.ExpectedStackBefore > 0 &&
        request.ExpectedStackAfter == request.ExpectedStackBefore - 1 &&
        !string.IsNullOrWhiteSpace(request.QualifiedItemId) && !string.IsNullOrWhiteSpace(request.SeedId);

    public static bool MatchesNativeSlot(TrainingExecutionRequest request, int index, string qualifiedItemId,
        string itemId, string resolvedSeedId, int quality, int stack) => ValidShape(request) &&
        request.InventorySlotIndex == index && request.QualifiedItemId == qualifiedItemId &&
        request.SeedInventoryQuality == quality && request.ExpectedStackBefore == stack &&
        (string.Equals(resolvedSeedId, request.SeedId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(itemId, request.SeedId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(qualifiedItemId, "(O)" + request.SeedId, StringComparison.OrdinalIgnoreCase));
}
