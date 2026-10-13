namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueRecurrencePrefix(
        MoneyRevenueTransitionEvidence sale,
        MoneyRevenueTransitionEvidence settlement)
    {
        var prefix = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
            new[] { sale, settlement });
        Require(
            prefix.SchemaVersion ==
                "money_revenue_recurrence_prefix.v1" &&
            prefix.CoversBothRevenueKinds &&
            prefix.ImmediateShopSaleCount == 1 &&
            prefix.NativeShippingSettlementCount == 1 &&
            prefix.TotalRevenue == 320 &&
            prefix.InitialTotalMoneyEarned == 49_990 &&
            prefix.FinalTotalMoneyEarned == 50_310 &&
            prefix.InitialTotalDay == 10 &&
            prefix.FinalTotalDay == 11,
            "Money revenue recurrence prefix totals or kind coverage drifted.");

        RequireMoneyRecurrenceRejected(
            new[]
            {
                sale,
                settlement with
                {
                    Before = settlement.Before with
                    {
                        StateHash = "disconnected-state"
                    }
                }
            },
            "A disconnected Money revenue recurrence was admitted.");
        RequireMoneyRecurrenceRejected(
            new[]
            {
                sale with
                {
                    MoneyDelta = sale.MoneyDelta + 1
                }
            },
            "A Money recurrence with a forged delta was admitted.");
        RequireMoneyRecurrenceRejected(
            new[]
            {
                sale with
                {
                    After = sale.After with
                    {
                        PlayerId = "different-player"
                    }
                }
            },
            "A Money recurrence with actor drift was admitted.");
        RequireMoneyRecurrenceRejected(
            new[]
            {
                settlement with
                {
                    After = settlement.After with
                    {
                        TotalDay = 224
                    }
                }
            },
            "A Money recurrence beyond the Stage-1 deadline was admitted.");
    }

    private static void RequireMoneyRecurrenceRejected(
        IReadOnlyList<MoneyRevenueTransitionEvidence> transitions,
        string message)
    {
        var rejected = false;
        try
        {
            MoneyRevenueRecurrenceVerifier.VerifyPrefix(transitions);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }
}
