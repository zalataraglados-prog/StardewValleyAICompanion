using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("zero_dispatch")]
    [InlineData("success")]
    [InlineData("two_blocked")]
    [InlineData("completed")]
    public async Task RefillControlRequiresExactSingleBlockedNativeAttempt(string changed)
    {
        using var f = new WateringFixture(water: 0);
        await f.Emit(applied: 0, failedSuffix: true, refillOnlySuffix: true, receiptChange: receipt =>
        {
            if (changed == "zero_dispatch")
            {
                receipt.StepResults = Array.Empty<QueueExecutionStepReceipt>();
                receipt.ExecutedItemCount = 0;
                receipt.FinalPendingItemCount = receipt.PlannedItemCount;
            }
            if (changed == "success") receipt.Success = true;
            if (changed == "two_blocked") receipt.StepResults = new[] { receipt.StepResults[0], receipt.StepResults[0] };
            if (changed == "completed") receipt.SelectedCandidateCompleted = true;
        });
        Assert.Throws<InvalidDataException>(() => OpenRefillControl(f));
    }
}
