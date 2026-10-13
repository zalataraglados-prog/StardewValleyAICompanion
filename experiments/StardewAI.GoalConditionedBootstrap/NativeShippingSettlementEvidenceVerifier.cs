using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class NativeShippingSettlementEvidenceVerifier
{
    internal static NativeShippingSettlementEvidence Verify(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after,
        string runId,
        string executorVersion,
        string selectedCandidateId)
    {
        var queueReasons = QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                receipt,
                after,
                runId,
                executorVersion,
                selectedCandidateId,
                queue.StateHash,
                requireTeacherPreferenceStateRebound: false);
        var reasons = queueReasons.ToList();
        var nativeSleep = NativeDaySettlementSupport
            .IsSingleNativeSleepQueue(queue);
        if (!nativeSleep)
            reasons.Add("native_shipping_settlement_queue_not_single_sleep");

        MoneyRevenueTransitionEvidence? progression = null;
        try
        {
            progression = MoneyRevenueProgressionVerifier
                .VerifyShippingDaySettlement(before, after);
        }
        catch (InvalidDataException)
        {
            reasons.Add("native_shipping_settlement_money_progression_invalid");
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new NativeShippingSettlementEvidence(
            queueReasons.Length == 0,
            nativeSleep,
            progression,
            distinct);
    }
}
