using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class FullShipmentTerminalSettlementVerifier
{
    public static FullShipmentTerminalSettlementEvidence Verify(
        IReadOnlyCollection<string> requiredQualifiedItemIds,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var settlement = FullShipmentSettlementVerifier.Verify(
            requiredQualifiedItemIds,
            before,
            after);
        var reasons = settlement.BlockingReasons
            .Select(ToTerminalReason)
            .ToList();
        if (settlement.BeforeShippedItemCount !=
                settlement.RequiredItemCount - 1 ||
            settlement.BeforeMissingItemCount != 1)
        {
            reasons.Add(
                "full_shipment_terminal_before_state_not_single_missing_item");
        }
        if (!settlement.TerminalTransition ||
            settlement.AfterShippedItemCount != settlement.RequiredItemCount ||
            settlement.AfterMissingItemCount != 0)
        {
            reasons.Add("full_shipment_terminal_after_state_not_complete");
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new FullShipmentTerminalSettlementEvidence
        {
            RequiredItemCount = settlement.RequiredItemCount,
            TerminalItemId = settlement.SettledItemId,
            TerminalQualifiedItemId = settlement.SettledQualifiedItemId,
            BeforeShippedItemCount = settlement.BeforeShippedItemCount,
            AfterShippedItemCount = settlement.AfterShippedItemCount,
            BeforeTerminalShippedCount =
                settlement.BeforeSettledItemShippedCount,
            AfterTerminalShippedCount =
                settlement.AfterSettledItemShippedCount,
            BeforeTerminalBinCount = settlement.BeforeSettledItemBinCount,
            AfterTerminalBinCount = settlement.AfterSettledItemBinCount,
            BeforeTotalDay = settlement.BeforeTotalDay,
            AfterTotalDay = settlement.AfterTotalDay,
            Achievement34Before = settlement.Achievement34Before,
            Achievement34After = settlement.Achievement34After,
            Verified = distinct.Length == 0,
            BlockingReasons = distinct
        };
    }

    private static string ToTerminalReason(string reason)
    {
        if (reason ==
            "full_shipment_settlement_achievement_34_state_mismatch")
        {
            return
                "full_shipment_terminal_achievement_34_transition_missing";
        }
        const string sourcePrefix = "full_shipment_settlement_";
        return reason.StartsWith(sourcePrefix, StringComparison.Ordinal)
            ? "full_shipment_terminal_" + reason[sourcePrefix.Length..]
            : reason;
    }
}
