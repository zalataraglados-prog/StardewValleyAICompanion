using System;

namespace StardewAI.Contracts.Execution;

public static class AuditedShopPurchasePolicy
{
    public const string BambooPoleQualifiedItemId = "(T)BambooPole";

    public static bool IsSideEffectFreeNonObjectPurchase(
        string? qualifiedItemId) =>
        string.Equals(
            qualifiedItemId,
            BambooPoleQualifiedItemId,
            StringComparison.Ordinal);
}
