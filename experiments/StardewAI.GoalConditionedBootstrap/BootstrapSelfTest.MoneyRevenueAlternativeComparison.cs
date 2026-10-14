using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueAlternativeComparison(
        MoneyRevenueRecurrencePrefixEvidence higherRevenue,
        MoneyRevenueTransitionEvidence immediateSale)
    {
        const int ledgerRevision = 7;
        var lowerRevenue = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
            new[] { immediateSale });
        var sourceStateHash = higherRevenue.InitialStateHash;
        var emptyCost = RevenueOpportunityCost();
        var higher = new MoneyRevenueAlternativeCandidate(
            "alternative.higher",
            higherRevenue,
            emptyCost,
            true,
            null);
        var lower = new MoneyRevenueAlternativeCandidate(
            "alternative.lower",
            lowerRevenue,
            emptyCost,
            true,
            null);
        var comparison = MoneyRevenueAlternativeComparer.Compare(
            "money_50000",
            sourceStateHash,
            ledgerRevision,
            new[] { higher.AlternativeId, lower.AlternativeId },
            new[] { higher, lower });
        Require(
            comparison.SchemaVersion ==
                "money_revenue_reserve_aware_alternative_comparison.v1" &&
            comparison.SelectionPolicy ==
                "authoritative_threshold_then_reserve_aware_strict_pareto.v1" &&
            comparison.CandidateDenominatorComplete &&
            comparison.CandidateDenominatorCount == 2 &&
            comparison.SelectionDisposition ==
                MoneyRevenueAlternativeSelectionDisposition
                    .UniqueAuthoritativePreference &&
            comparison.SelectedAlternativeId == higher.AlternativeId &&
            comparison.TeacherPreferenceLabelEligible &&
            comparison.PairwisePreferences.Length == 1 &&
            !comparison.FormalTrainingAuthorized &&
            !comparison.UsesLearnerScore,
            "Revenue target comparison did not select the sole satisfying alternative.");

        var artifactComparison = MoneyRevenueAlternativeComparer
            .CompareVerifiedArtifacts(
                "money_50000",
                sourceStateHash,
                ledgerRevision,
                new[] { higher.AlternativeId, lower.AlternativeId },
                new[]
                {
                    new MoneyRevenueVerifiedArtifactAlternative(
                        higher.AlternativeId,
                        higher.RevenuePrefix,
                        higher.OpportunityCost,
                        true,
                        Array.Empty<string>()),
                    new MoneyRevenueVerifiedArtifactAlternative(
                        lower.AlternativeId,
                        lower.RevenuePrefix,
                        lower.OpportunityCost,
                        true,
                        Array.Empty<string>())
                });
        Require(
            artifactComparison.SelectedAlternativeId ==
                higher.AlternativeId &&
            artifactComparison.TeacherPreferenceLabelEligible &&
            !artifactComparison.FormalTrainingAuthorized,
            "Artifact-verified Revenue alternatives drifted during comparison.");

        var reservedHigher = higher with
        {
            OpportunityCost = RevenueOpportunityCost(
                materialQuantity: 1),
            ReservationNotRequired = false,
            ReservationClaimSet = RevenueMaterialClaimSet(
                sourceStateHash,
                ledgerRevision,
                quantity: 1)
        };
        var tradeoff = MoneyRevenueAlternativeComparer.Compare(
            "money_100000",
            sourceStateHash,
            ledgerRevision,
            new[] { reservedHigher.AlternativeId, lower.AlternativeId },
            new[] { reservedHigher, lower });
        Require(
            tradeoff.SelectionDisposition ==
                MoneyRevenueAlternativeSelectionDisposition.IncomparableFrontier &&
            !tradeoff.TeacherPreferenceLabelEligible &&
            tradeoff.SelectedAlternativeId.Length == 0 &&
            tradeoff.Evaluations.Count(row => row.OnParetoFrontier) == 2 &&
            tradeoff.PairwisePreferences.Length == 0,
            "Revenue progress/material trade-off produced a fabricated label.");

        RequireRevenueComparisonRejected(
            sourceStateHash,
            ledgerRevision,
            new[] { higher.AlternativeId, lower.AlternativeId },
            new[] { higher },
            "An incomplete Revenue alternative denominator was admitted.");
        RequireRevenueComparisonRejected(
            sourceStateHash,
            ledgerRevision,
            new[] { reservedHigher.AlternativeId, lower.AlternativeId },
            new[]
            {
                reservedHigher with
                {
                    ReservationClaimSet = RevenueMaterialClaimSet(
                        "stale-state",
                        ledgerRevision,
                        quantity: 1)
                },
                lower
            },
            "A stale Revenue reservation claim was admitted.");
        RequireRevenueComparisonRejected(
            sourceStateHash,
            ledgerRevision,
            new[] { reservedHigher.AlternativeId, lower.AlternativeId },
            new[]
            {
                reservedHigher with
                {
                    OpportunityCost = RevenueOpportunityCost(
                        materialQuantity: 2)
                },
                lower
            },
            "A Revenue claim/cost quantity mismatch was admitted.");
    }

    private static AcquisitionOpportunityCostVector RevenueOpportunityCost(
        int materialQuantity = 0) => new(
        GuaranteedElapsedGameMinutes: 10,
        RequiredEnergy: 0d,
        MaterialTotalSaleValue: materialQuantity * 2,
        MaterialCosts: materialQuantity == 0
            ? Array.Empty<AcquisitionOpportunityMaterialCost>()
            : new[]
            {
                new AcquisitionOpportunityMaterialCost(
                    "(O)388",
                    Quality: 0,
                    UnitSalePrice: 2,
                    Quantity: materialQuantity,
                    TotalSaleValue: materialQuantity * 2)
            },
        CurrencyCosts: Array.Empty<AcquisitionOpportunityCurrencyCost>(),
        EvidencePaths: new[] { "self_test.reserve_aware_cost" });

    private static AcquisitionRouteReservationClaimSet RevenueMaterialClaimSet(
        string stateHash,
        int ledgerRevision,
        int quantity)
    {
        const string decisionId = "decision.revenue.higher";
        return new AcquisitionRouteReservationClaimSet(
            decisionId,
            stateHash,
            ledgerRevision,
            AtomicCommitRequired: true,
            ReplacementRequired: false,
            ExistingActiveReservationIds: Array.Empty<string>(),
            MaterialClaims: new[]
            {
                new MaterialReservationUpsertRequest
                {
                    StateHash = stateHash,
                    ExpectedLedgerRevision = ledgerRevision,
                    ReservationId = "reservation.revenue.wood",
                    SourceDecisionId = decisionId,
                    GoalId = "goal.economy.earn_money",
                    NodeId = "inventory.player",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = quantity,
                    Purpose = "revenue_alternative"
                }
            },
            CurrencyClaims: Array.Empty<CurrencyReservationUpsertRequest>());
    }

    private static void RequireRevenueComparisonRejected(
        string sourceStateHash,
        int ledgerRevision,
        IReadOnlyList<string> expectedIds,
        IReadOnlyList<MoneyRevenueAlternativeCandidate> alternatives,
        string message)
    {
        var rejected = false;
        try
        {
            MoneyRevenueAlternativeComparer.Compare(
                "money_100000",
                sourceStateHash,
                ledgerRevision,
                expectedIds,
                alternatives);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }
}
