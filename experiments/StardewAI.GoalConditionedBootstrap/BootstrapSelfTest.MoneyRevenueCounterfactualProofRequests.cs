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
        var anchors = concrete.Alternatives.ToDictionary(
            row => row.ConcreteAlternativeId,
            row => row.RequiresAcquisitionOrProcessingRollout
                ? new MoneyRevenueTerminalStrategyLedgerAnchor(
                    "acquisition_rollout_latest_settlement",
                    "terminal-ledger-" + row.ConcreteAlternativeId + ".json",
                    new string('d', 64),
                    ledger.LedgerId,
                    ledger.Revision + 1,
                    row.TerminalStateHash)
                : new MoneyRevenueTerminalStrategyLedgerAnchor(
                    "source_strategy_ledger",
                    "strategy-ledger.json",
                    new string('c', 64),
                    ledger.LedgerId,
                    ledger.Revision,
                    concrete.SourceStateHash),
            StringComparer.Ordinal);
        var requests = MoneyRevenueCounterfactualProofRequestBuilder
            .BuildCatalog(
                new string('a', 64),
                "concrete-catalog.json",
                new string('b', 64),
                concrete,
                "strategy-ledger.json",
                new string('c', 64),
                ledger,
                anchors);
        Require(requests.RequestDenominatorAuthoritative &&
                requests.TerminalStrategyLedgerAnchorsVerified &&
                requests.TerminalStrategyLedgerAnchorCount == 8 &&
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
                    row.TerminalReservationClaimProofRequired &&
                    row.TerminalStrategyLedgerId == ledger.LedgerId &&
                    row.TerminalStrategyLedgerSourceStateHash ==
                        row.TerminalStateHash &&
                    row.RevenueRecurrenceProofRequired &&
                    row.RequestSha256.Length == 64) &&
                requests.CatalogSha256.Length == 64,
            "Revenue concrete alternatives did not emit one bounded proof request each.");
        Require(requests.Requests
                .Where(row => !row.AcquisitionRolloutProofRequired)
                .All(row =>
                    row.TerminalStrategyLedgerSourceKind ==
                        "source_strategy_ledger" &&
                    row.TerminalStrategyLedgerPath ==
                        row.SourceStrategyLedgerPath &&
                    row.TerminalStrategyLedgerSha256 ==
                        row.SourceStrategyLedgerSha256 &&
                    row.TerminalStrategyLedgerRevision ==
                        row.SourceStrategyLedgerRevision &&
                    string.IsNullOrEmpty(
                        row.AcquisitionRolloutManifestPath) &&
                    string.IsNullOrEmpty(
                        row.AcquisitionRolloutReceiptPath)),
            "Revenue direct proof requests fabricated acquisition rollouts.");
        Require(requests.Requests
                .Where(row => row.AcquisitionRolloutProofRequired)
                .All(row =>
                    row.TerminalStrategyLedgerSourceKind ==
                        "acquisition_rollout_latest_settlement" &&
                    row.TerminalStrategyLedgerRevision ==
                        ledger.Revision + 1 &&
                    row.TerminalStrategyLedgerPath.StartsWith(
                        "terminal-ledger-",
                        StringComparison.Ordinal)),
            "Revenue rebound proof requests lost their rollout terminal ledger.");

        var missingAnchors = anchors
            .Skip(1)
            .ToDictionary(row => row.Key, row => row.Value,
                StringComparer.Ordinal);
        var missingRejected = false;
        try
        {
            _ = MoneyRevenueCounterfactualProofRequestBuilder.BuildCatalog(
                new string('a', 64),
                "concrete-catalog.json",
                new string('b', 64),
                concrete,
                "strategy-ledger.json",
                new string('c', 64),
                ledger,
                missingAnchors);
        }
        catch (InvalidDataException)
        {
            missingRejected = true;
        }
        Require(missingRejected,
            "Revenue proof requests admitted a missing terminal ledger anchor.");

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
                ledger,
                anchors);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Revenue proof requests admitted a stale source strategy ledger.");
    }
}
