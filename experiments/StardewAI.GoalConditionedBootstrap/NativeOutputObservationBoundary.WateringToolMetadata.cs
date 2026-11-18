using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private const string WateringCanRuntimeType = "StardewValley.Tools.WateringCan";
    private const string FishingRodRuntimeType = "StardewValley.Tools.FishingRod";
    private const int MaxWateringToolMetadataCalls = 512;

    private static IVerifiedPickupToolMetadata VerifyWateringToolMetadata(
        ActionOriginObservationReceipt receipt, NativeInventoryGraphSummary graph,
        int toolSlot, string toolId, int expectedWaterBefore, int expectedWaterLeft)
    {
        var context = ReadWateringToolMetadataContext(receipt, graph, toolSlot, toolId);
        Require(receipt.WateringToolMetadataBefore!.Tools!.Single(row => row.SlotIndex == toolSlot).WaterLeft == expectedWaterBefore,
            "watering tool XML baseline differs from the original can resource projection");
        return new VerifiedWateringToolMetadata(context, expectedWaterLeft);
    }
}
