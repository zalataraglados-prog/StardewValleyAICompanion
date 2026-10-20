using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueCropTerminalSettlementModeVerifier
{
    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}

internal sealed record MoneyRevenueCropTerminalSettlementVerification(
    string TerminalKind,
    string TerminalActionBeforeStateHash,
    string TerminalActionAfterStateHash,
    MoneyRevenueTerminalQueueTransitionEvidence[] RouteTransitions,
    string ShopSaleEpisodePath,
    string ShopSaleEpisodeSha256,
    string ShopId,
    MoneyRevenueCropShippingDepositEvidence[] ShippingDeposits,
    MoneyRevenueTerminalQueueTransitionEvidence[] SettlementRouteTransitions,
    int SlotIndex,
    int SettledQuantity,
    int UnitPrice,
    int SettledRevenue,
    string RevenueRecurrenceManifestPath,
    string RevenueRecurrenceManifestSha256,
    string RevenueRecurrenceReceiptPath,
    string RevenueRecurrenceReceiptSha256);

internal sealed record MoneyRevenueCropShippingDepositBatchVerification(
    SnapshotEnvelope TerminalActionBefore,
    SnapshotEnvelope After,
    MoneyRevenueTerminalQueueTransitionEvidence[] RouteTransitions,
    MoneyRevenueCropShippingDepositEvidence[] Deposits,
    int SlotIndex,
    int UnitPrice,
    int SettledQuantity,
    long PendingValueBefore,
    long PendingValueAfter);

internal sealed record MoneyRevenueCropShippingRecurrenceVerification(
    string FinalStateHash,
    string ManifestPath,
    string ManifestSha256,
    string ReceiptPath,
    string ReceiptSha256);
