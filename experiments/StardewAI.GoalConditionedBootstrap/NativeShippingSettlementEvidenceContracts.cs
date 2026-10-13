namespace StardewAI.GoalConditionedBootstrap;

internal sealed record NativeShippingSettlementEvidence(
    bool QueueReceiptVerified,
    bool NativeSleepQueueVerified,
    MoneyRevenueTransitionEvidence? Progression,
    string[] BlockingReasons)
{
    internal bool Verified => BlockingReasons.Length == 0 &&
        QueueReceiptVerified &&
        NativeSleepQueueVerified &&
        Progression is not null;
}
