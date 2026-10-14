using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCounterfactualProofRequestBuilder
{
    private static void ValidateManifest(
        MoneyRevenueCounterfactualProofRequestManifest manifest)
    {
        Require(manifest.SchemaVersion ==
                    "money_revenue_counterfactual_proof_request_manifest.v1" &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(
                    manifest.ConcreteAlternativeCatalogPath) &&
                !string.IsNullOrWhiteSpace(
                    manifest.SourceStrategyLedgerPath),
            "Money Revenue counterfactual proof request manifest is invalid.");
    }

    private static void ValidateSourceLedger(
        StrategyCommitmentLedger ledger,
        SnapshotEnvelope snapshot,
        string sourceStateHash)
    {
        Require(ledger.SchemaVersion == "strategy_commitment_ledger.v1" &&
                !string.IsNullOrWhiteSpace(ledger.LedgerId) &&
                ledger.Revision >= 0 &&
                ledger.SourceStateHash == sourceStateHash &&
                FieldEnvelopeValidator.IsReadableStatus(
                    snapshot.SaveId.Status) &&
                FieldEnvelopeValidator.IsReadableStatus(
                    snapshot.PlayerId.Status) &&
                !string.IsNullOrWhiteSpace(snapshot.SaveId.Value) &&
                !string.IsNullOrWhiteSpace(snapshot.PlayerId.Value) &&
                ledger.SaveId == snapshot.SaveId.Value &&
                ledger.PlayerId == snapshot.PlayerId.Value,
            "Money Revenue source strategy ledger actor or state identity drifted.");
    }

    private static MoneyRevenueCounterfactualProofRequest BuildRequest(
        MoneyRevenueConcreteAlternative alternative,
        string sourceStateHash,
        string sourceSnapshotPath,
        string sourceSnapshotSha256,
        string ledgerPath,
        string ledgerSha256,
        string ledgerId,
        int ledgerRevision,
        MoneyRevenueTerminalStrategyLedgerAnchor terminalLedger)
    {
        ValidateAlternative(alternative, sourceStateHash, sourceSnapshotPath,
            sourceSnapshotSha256);
        ValidateTerminalLedgerAnchor(
            alternative,
            terminalLedger,
            ledgerPath,
            ledgerSha256,
            ledgerId,
            ledgerRevision,
            sourceStateHash);
        var proofKind = alternative.RequiresNativeShopSaleProof
            ? "native_shop_sale"
            : "native_shipping_deposit_and_day_settlement";
        var requestIdentity =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                new
                {
                    alternative.ConcreteAlternativeId,
                    alternative.ConcreteAlternativeSha256,
                    SourceStateHash = sourceStateHash,
                    SourceSnapshotSha256 = sourceSnapshotSha256,
                    SourceStrategyLedgerPath = ledgerPath,
                    SourceStrategyLedgerSha256 = ledgerSha256,
                    SourceStrategyLedgerId = ledgerId,
                    SourceStrategyLedgerRevision = ledgerRevision,
                    terminalLedger.SourceKind,
                    TerminalStrategyLedgerPath = terminalLedger.Path,
                    TerminalStrategyLedgerSha256 = terminalLedger.Sha256,
                    TerminalStrategyLedgerId = terminalLedger.LedgerId,
                    TerminalStrategyLedgerRevision = terminalLedger.Revision,
                    TerminalStrategyLedgerSourceStateHash =
                        terminalLedger.SourceStateHash,
                    alternative.TerminalStateHash,
                    alternative.TerminalSnapshotSha256,
                    alternative.TerminalOptionId,
                    alternative.TerminalCandidate.CatalogCandidateId,
                    alternative.TerminalCandidate.CandidateSha256,
                    ProofKind = proofKind
                });
        var request = new MoneyRevenueCounterfactualProofRequest
        {
            RequestId = "revenue-proof-request:" + requestIdentity[..24],
            ConcreteAlternativeId = alternative.ConcreteAlternativeId,
            ConcreteAlternativeSha256 =
                alternative.ConcreteAlternativeSha256,
            SourceStateHash = sourceStateHash,
            SourceSnapshotPath = sourceSnapshotPath,
            SourceSnapshotSha256 = sourceSnapshotSha256,
            SourceStrategyLedgerPath = ledgerPath,
            SourceStrategyLedgerSha256 = ledgerSha256,
            SourceStrategyLedgerId = ledgerId,
            SourceStrategyLedgerRevision = ledgerRevision,
            TerminalStrategyLedgerSourceKind = terminalLedger.SourceKind,
            TerminalStrategyLedgerPath = terminalLedger.Path,
            TerminalStrategyLedgerSha256 = terminalLedger.Sha256,
            TerminalStrategyLedgerId = terminalLedger.LedgerId,
            TerminalStrategyLedgerRevision = terminalLedger.Revision,
            TerminalStrategyLedgerSourceStateHash =
                terminalLedger.SourceStateHash,
            TerminalStateHash = alternative.TerminalStateHash,
            TerminalSnapshotPath = alternative.TerminalSnapshotPath,
            TerminalSnapshotSha256 = alternative.TerminalSnapshotSha256,
            TerminalOptionId = alternative.TerminalOptionId,
            TerminalCandidate = alternative.TerminalCandidate,
            ProofShape = alternative.ProofShape,
            RequiredRevenueTransitionProofKind = proofKind,
            AcquisitionRolloutProofRequired =
                alternative.RequiresAcquisitionOrProcessingRollout,
            AcquisitionRolloutManifestPath =
                alternative.AcquisitionRolloutManifestPath,
            AcquisitionRolloutManifestSha256 =
                alternative.AcquisitionRolloutManifestSha256,
            AcquisitionRolloutReceiptPath =
                alternative.AcquisitionRolloutReceiptPath,
            AcquisitionRolloutReceiptSha256 =
                alternative.AcquisitionRolloutReceiptSha256,
            TerminalOpportunityCostProofRequired = true,
            TerminalReservationClaimProofRequired = true,
            NativeShopSaleProofRequired =
                alternative.RequiresNativeShopSaleProof,
            NativeShippingDepositProofRequired =
                alternative.RequiresNativeShippingDepositProof,
            NativeShippingSettlementProofRequired =
                alternative.RequiresNativeShippingSettlementProof,
            RevenueRecurrenceProofRequired = true
        };
        request.RequestSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                request);
        return request;
    }

    private static void ValidateTerminalLedgerAnchor(
        MoneyRevenueConcreteAlternative alternative,
        MoneyRevenueTerminalStrategyLedgerAnchor terminalLedger,
        string sourceLedgerPath,
        string sourceLedgerSha256,
        string sourceLedgerId,
        int sourceLedgerRevision,
        string sourceStateHash)
    {
        var rebound = alternative.RequiresAcquisitionOrProcessingRollout;
        Require(!string.IsNullOrWhiteSpace(terminalLedger.Path) &&
                IsSha256(terminalLedger.Sha256) &&
                !string.IsNullOrWhiteSpace(terminalLedger.LedgerId) &&
                terminalLedger.Revision >= sourceLedgerRevision &&
                terminalLedger.LedgerId == sourceLedgerId &&
                terminalLedger.SourceStateHash ==
                    alternative.TerminalStateHash &&
                terminalLedger.SourceKind == (rebound
                    ? "acquisition_rollout_latest_settlement"
                    : "source_strategy_ledger"),
            "Money Revenue terminal strategy-ledger anchor is invalid: " +
            alternative.ConcreteAlternativeId + ".");
        if (rebound)
            return;
        Require(terminalLedger.Path == sourceLedgerPath &&
                terminalLedger.Sha256 == sourceLedgerSha256 &&
                terminalLedger.Revision == sourceLedgerRevision &&
                terminalLedger.SourceStateHash == sourceStateHash,
            "Money Revenue direct alternative did not retain the exact source strategy ledger.");
    }

    private static void ValidateAlternative(
        MoneyRevenueConcreteAlternative alternative,
        string sourceStateHash,
        string sourceSnapshotPath,
        string sourceSnapshotSha256)
    {
        var shop = alternative.TerminalOptionId == "economy.sell_items";
        var shipping = alternative.TerminalOptionId == "economy.ship_items";
        Require(IsSha256(alternative.ConcreteAlternativeSha256) &&
                IsSha256(alternative.TerminalCandidate.CandidateSha256) &&
                alternative.TerminalCandidate.ComparisonEligible &&
                !string.IsNullOrWhiteSpace(alternative.TerminalStateHash) &&
                !string.IsNullOrWhiteSpace(
                    alternative.TerminalSnapshotPath) &&
                IsSha256(alternative.TerminalSnapshotSha256) &&
                (shop || shipping) &&
                alternative.RequiresNativeShopSaleProof == shop &&
                alternative.RequiresNativeShippingDepositProof == shipping &&
                alternative.RequiresNativeShippingSettlementProof == shipping,
            "Money Revenue concrete alternative terminal proof shape is invalid: " +
            alternative.ConcreteAlternativeId + ".");
        if (alternative.RequiresAcquisitionOrProcessingRollout)
        {
            Require(alternative.UsesFreshTerminalRebind &&
                    !string.IsNullOrWhiteSpace(
                        alternative.AcquisitionRolloutManifestPath) &&
                    IsSha256(
                        alternative.AcquisitionRolloutManifestSha256) &&
                    !string.IsNullOrWhiteSpace(
                        alternative.AcquisitionRolloutReceiptPath) &&
                    IsSha256(
                        alternative.AcquisitionRolloutReceiptSha256),
                "Money Revenue rebound alternative lost acquisition proof identity.");
            return;
        }
        Require(!alternative.UsesFreshTerminalRebind &&
                alternative.TerminalStateHash == sourceStateHash &&
                alternative.TerminalSnapshotPath == sourceSnapshotPath &&
                alternative.TerminalSnapshotSha256 == sourceSnapshotSha256 &&
                string.IsNullOrEmpty(
                    alternative.AcquisitionRolloutManifestPath) &&
                string.IsNullOrEmpty(
                    alternative.AcquisitionRolloutManifestSha256) &&
                string.IsNullOrEmpty(
                    alternative.AcquisitionRolloutReceiptPath) &&
                string.IsNullOrEmpty(
                    alternative.AcquisitionRolloutReceiptSha256),
            "Money Revenue direct alternative fabricated acquisition proof identity.");
    }
}
