namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueConcreteAlternativeDenominator(
        MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition
            direction,
        MoneyRevenueCandidateDenominatorCatalog sourceCatalog,
        MoneyRevenueReboundTerminalCandidate[] sale,
        MoneyRevenueReboundTerminalCandidate[] shipping,
        string terminalStateHash)
    {
        var skeletons = MoneyRevenueAlternativeDenominatorBuilder.BuildCatalog(
            "source-candidate-catalog.json",
            new string('3', 64),
            sourceCatalog,
            direction);
        var saleSkeleton = skeletons.Alternatives.Single(row =>
            row.SourceCandidateId == "forage:fixture" &&
            row.TerminalOptionId == "economy.sell_items");
        var shippingSkeleton = skeletons.Alternatives.Single(row =>
            row.SourceCandidateId == "forage:fixture" &&
            row.TerminalOptionId == "economy.ship_items");
        var requests = new[]
        {
            RevenueRebindRequest(saleSkeleton.AlternativeId, "sale"),
            RevenueRebindRequest(shippingSkeleton.AlternativeId, "shipping")
        };
        var evidence = new[]
        {
            RevenueRebindEvidence(
                saleSkeleton,
                sale,
                terminalStateHash,
                '4'),
            RevenueRebindEvidence(
                shippingSkeleton,
                shipping,
                terminalStateHash,
                '5')
        };
        var rebindManifest = new MoneyRevenueTerminalCandidateRebindManifest
        {
            AlternativeDenominatorCatalogPath = "alternative-catalog.json",
            Rebinds = requests
        };
        var rebindReceipt = new MoneyRevenueTerminalCandidateRebindReceipt
        {
            Status = "verified_terminal_candidate_rebind_denominator",
            AlternativeDenominatorCatalogSha256 = skeletons.CatalogSha256,
            SourceStateHash = skeletons.SourceStateHash,
            RequiredRebindCount = evidence.Length,
            VerifiedRebindCount = evidence.Length,
            ConcreteTerminalCandidateCount = evidence.Sum(row =>
                row.TerminalCandidates.Length),
            TerminalRebindDenominatorComplete = true,
            Rebinds = evidence,
            BlockingReasons = new[]
            {
                "direct_and_rebound_concrete_alternative_union_not_built"
            }
        };
        var concrete = MoneyRevenueConcreteAlternativeDenominatorBuilder
            .BuildCatalog(
                "concrete-manifest.json",
                new string('6', 64),
                "alternative-catalog.json",
                new string('7', 64),
                skeletons,
                sourceCatalog,
                "rebind-manifest.json",
                new string('8', 64),
                rebindManifest,
                "rebind-receipt.json",
                new string('9', 64),
                rebindReceipt);
        Require(concrete.CurrentConcreteDenominatorAuthoritative &&
                !concrete.ProductionTeacherPreferenceLabelEligible &&
                !concrete.FormalProductTrainingAuthorized &&
                concrete.SkeletonCount == 7 &&
                concrete.DirectAlternativeCount == 5 &&
                concrete.ReboundAlternativeCount == 3 &&
                concrete.ConcreteAlternativeCount == 8 &&
                concrete.ConcreteAlternativeIds.Length == 8 &&
                concrete.Alternatives.Count(row =>
                    row.UsesFreshTerminalRebind) == 3 &&
                concrete.Alternatives.Count(row =>
                    !row.UsesFreshTerminalRebind) == 5 &&
                concrete.CatalogSha256.Length == 64,
            "Revenue direct and rebound alternatives did not form one complete concrete denominator.");

        rebindReceipt.Rebinds = evidence.Take(1).ToArray();
        var rejected = false;
        try
        {
            _ = MoneyRevenueConcreteAlternativeDenominatorBuilder.BuildCatalog(
                "concrete-manifest.json",
                new string('6', 64),
                "alternative-catalog.json",
                new string('7', 64),
                skeletons,
                sourceCatalog,
                "rebind-manifest.json",
                new string('8', 64),
                rebindManifest,
                "rebind-receipt.json",
                new string('9', 64),
                rebindReceipt);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Revenue concrete denominator admitted a missing rebound skeleton.");
    }

    private static MoneyRevenueTerminalCandidateRebindRequest
        RevenueRebindRequest(string alternativeId, string id) => new()
        {
            AlternativeId = alternativeId,
            AcquisitionRolloutProofManifestPath =
                "rollout-" + id + "-manifest.json",
            AcquisitionRolloutProofReceiptPath =
                "rollout-" + id + "-receipt.json"
        };

    private static MoneyRevenueTerminalCandidateRebindEvidence
        RevenueRebindEvidence(
            MoneyRevenueAlternativeSkeleton skeleton,
            MoneyRevenueReboundTerminalCandidate[] candidates,
            string terminalStateHash,
            char hashCharacter) => new()
            {
                AlternativeId = skeleton.AlternativeId,
                SkeletonPathSha256 = skeleton.PathSha256,
                SourceCatalogCandidateId = skeleton.SourceCatalogCandidateId,
                SourceCandidateId = skeleton.SourceCandidateId,
                TerminalOptionId = skeleton.TerminalOptionId,
                ProofShape = skeleton.ProofShape,
                RolloutId = "rollout." + hashCharacter,
                RolloutManifestSha256 = new string(hashCharacter, 64),
                RolloutReceiptSha256 = new string(hashCharacter, 64),
                TerminalStateHash = terminalStateHash,
                TerminalCandidates = candidates,
                EvidenceSha256 = new string(hashCharacter, 64)
            };
}
