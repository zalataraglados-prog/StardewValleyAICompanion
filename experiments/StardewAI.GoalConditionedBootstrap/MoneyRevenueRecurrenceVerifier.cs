namespace StardewAI.GoalConditionedBootstrap;

internal static class MoneyRevenueRecurrenceVerifier
{
    private const int StageOneDeadlineExclusive = 224;

    internal static MoneyRevenueRecurrencePrefixEvidence VerifyPrefix(
        IReadOnlyList<MoneyRevenueTransitionEvidence> transitions)
    {
        if (transitions is null || transitions.Count == 0)
        {
            throw new InvalidDataException(
                "Money revenue recurrence prefix has no transitions.");
        }

        var rows = transitions.ToArray();
        var first = rows[0].Before;
        var saleCount = 0;
        var shippingCount = 0;
        var totalRevenue = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index] ?? throw new InvalidDataException(
                "Money revenue recurrence prefix contains a null transition.");
            ValidateTransition(row);
            Require(
                row.Before.SaveId == first.SaveId &&
                row.After.SaveId == first.SaveId &&
                row.Before.PlayerId == first.PlayerId &&
                row.After.PlayerId == first.PlayerId,
                "Money revenue recurrence actor or save identity drifted.");
            if (index > 0)
            {
                Require(
                    rows[index - 1].After == row.Before,
                    "Money revenue recurrence state chain is not contiguous.");
            }

            totalRevenue = checked(totalRevenue + row.MoneyDelta);
            if (row.TransitionKind ==
                MoneyRevenueTransitionKinds.ImmediateShopSale)
            {
                saleCount++;
            }
            else
            {
                shippingCount++;
            }
        }

        var final = rows[^1].After;
        Require(
            checked(final.Money - first.Money) == totalRevenue &&
            checked(final.TotalMoneyEarned - first.TotalMoneyEarned) ==
                (uint)totalRevenue,
            "Money revenue recurrence aggregate revenue drifted.");
        return new MoneyRevenueRecurrencePrefixEvidence(
            first.SaveId,
            first.PlayerId,
            first.StateHash,
            final.StateHash,
            first.TotalDay,
            final.TotalDay,
            first.Money,
            final.Money,
            first.TotalMoneyEarned,
            final.TotalMoneyEarned,
            totalRevenue,
            saleCount,
            shippingCount,
            rows);
    }

    private static void ValidateTransition(
        MoneyRevenueTransitionEvidence transition)
    {
        var before = transition.Before ?? throw new InvalidDataException(
            "Money revenue recurrence before checkpoint is missing.");
        var after = transition.After ?? throw new InvalidDataException(
            "Money revenue recurrence after checkpoint is missing.");
        Require(
            !string.IsNullOrWhiteSpace(before.StateHash) &&
            !string.IsNullOrWhiteSpace(after.StateHash) &&
            before.StateHash != after.StateHash &&
            !string.IsNullOrWhiteSpace(before.SaveId) &&
            !string.IsNullOrWhiteSpace(before.PlayerId) &&
            before.SaveId == after.SaveId &&
            before.PlayerId == after.PlayerId &&
            !string.IsNullOrWhiteSpace(before.ShippingScopeId) &&
            !string.IsNullOrWhiteSpace(after.ShippingScopeId) &&
            !string.IsNullOrWhiteSpace(before.ShippingContentsSignature) &&
            !string.IsNullOrWhiteSpace(after.ShippingContentsSignature) &&
            before.TotalDay >= 0 &&
            after.TotalDay >= before.TotalDay &&
            after.TotalDay < StageOneDeadlineExclusive &&
            before.Money >= 0 &&
            after.Money >= before.Money &&
            after.TotalMoneyEarned >= before.TotalMoneyEarned &&
            transition.MoneyDelta == checked(after.Money - before.Money) &&
            transition.TotalMoneyEarnedDelta == checked(
                after.TotalMoneyEarned - before.TotalMoneyEarned) &&
            transition.MoneyDelta > 0 &&
            transition.TotalMoneyEarnedDelta ==
                (uint)transition.MoneyDelta,
            "Money revenue recurrence transition invariants are invalid.");

        if (transition.TransitionKind ==
            MoneyRevenueTransitionKinds.ImmediateShopSale)
        {
            Require(
                before.TotalDay == after.TotalDay &&
                transition.ConsumedPendingShippingValue == 0 &&
                SameShippingState(before, after),
                "Money revenue recurrence shop-sale edge is invalid.");
            return;
        }

        Require(
            transition.TransitionKind ==
                MoneyRevenueTransitionKinds.NativeShippingDaySettlement &&
            after.TotalDay == before.TotalDay + 1 &&
            before.PendingShippingSettlementValue > 0 &&
            transition.ConsumedPendingShippingValue ==
                before.PendingShippingSettlementValue &&
            transition.MoneyDelta ==
                transition.ConsumedPendingShippingValue &&
            after.PendingShippingSettlementValue == 0 &&
            before.ShippingScopeId == after.ShippingScopeId,
            "Money revenue recurrence shipping-settlement edge is invalid.");
    }

    private static bool SameShippingState(
        MoneyRevenueCheckpoint left,
        MoneyRevenueCheckpoint right) =>
        left.ShippingScopeId == right.ShippingScopeId &&
        left.PendingShippingSettlementValue ==
            right.PendingShippingSettlementValue &&
        left.ShippingContentsSignature == right.ShippingContentsSignature;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
