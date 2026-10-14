using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCounterfactualProofRequests(
        MoneyRevenueConcreteAlternativeDenominatorCatalog concrete)
    {
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.revenue.counterfactual",
            Revision = 7,
            SourceStateHash = concrete.SourceStateHash
        };
        var requests = MoneyRevenueCounterfactualProofRequestBuilder
            .BuildCatalog(
                new string('a', 64),
                "concrete-catalog.json",
                new string('b', 64),
                concrete,
                "strategy-ledger.json",
                new string('c', 64),
                ledger);
        Require(requests.RequestDenominatorAuthoritative &&
                requests.CounterfactualProofCollectionReady &&
                !requests.ProductionTeacherPreferenceLabelEligible &&
                !requests.FormalProductTrainingAuthorized &&
                requests.RequestCount == 8 &&
                requests.RequestIds.Length == 8 &&
                requests.Requests.Count(row =>
                    row.AcquisitionRolloutProofRequired) == 3 &&
                requests.Requests.Count(row =>
                    !row.AcquisitionRolloutProofRequired) == 5 &&
                requests.Requests.Count(row =>
                    row.NativeShopSaleProofRequired) == 5 &&
                requests.Requests.Count(row =>
                    row.NativeShippingDepositProofRequired &&
                    row.NativeShippingSettlementProofRequired) == 3 &&
                requests.Requests.All(row =>
                    row.TerminalOpportunityCostProofRequired &&
                    row.RevenueRecurrenceProofRequired &&
                    row.RequestSha256.Length == 64) &&
                requests.CatalogSha256.Length == 64,
            "Revenue concrete alternatives did not emit one bounded proof request each.");
        Require(requests.Requests
                .Where(row => !row.AcquisitionRolloutProofRequired)
                .All(row =>
                    string.IsNullOrEmpty(
                        row.AcquisitionRolloutManifestPath) &&
                    string.IsNullOrEmpty(
                        row.AcquisitionRolloutReceiptPath)),
            "Revenue direct proof requests fabricated acquisition rollouts.");

        ledger.SourceStateHash = "stale-revenue-state";
        var rejected = false;
        try
        {
            _ = MoneyRevenueCounterfactualProofRequestBuilder.BuildCatalog(
                new string('a', 64),
                "concrete-catalog.json",
                new string('b', 64),
                concrete,
                "strategy-ledger.json",
                new string('c', 64),
                ledger);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Revenue proof requests admitted a stale source strategy ledger.");
    }
}
