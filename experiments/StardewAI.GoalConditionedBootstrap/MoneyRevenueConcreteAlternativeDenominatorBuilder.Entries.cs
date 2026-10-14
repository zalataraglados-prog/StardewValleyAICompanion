namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueConcreteAlternativeDenominatorBuilder
{
    private static MoneyRevenueConcreteAlternative BuildDirect(
        MoneyRevenueAlternativeSkeleton skeleton,
        SourceCandidateRef source,
        string sourceStateHash,
        string sourceSnapshotPath,
        string sourceSnapshotSha256)
    {
        Require(!skeleton.RequiresAcquisitionOrProcessingRollout &&
                !skeleton.RequiresFreshTerminalCandidateRebind &&
                skeleton.ConcreteAlternativeIdentityComplete &&
                skeleton.SourceOptionId == skeleton.TerminalOptionId &&
                source.Candidate.ComparisonEligible,
            "Money Revenue direct skeleton is not concrete: " +
            skeleton.AlternativeId + ".");
        var identitySha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                new
                {
                    skeleton.AlternativeId,
                    skeleton.PathSha256,
                    SourceStateHash = sourceStateHash,
                    source.Candidate.CatalogCandidateId,
                    source.Candidate.CandidateSha256
                });
        return FinalizeAlternative(new MoneyRevenueConcreteAlternative
        {
            ConcreteAlternativeId =
                "revenue-concrete:" + identitySha256[..24],
            SkeletonAlternativeId = skeleton.AlternativeId,
            SkeletonPathSha256 = skeleton.PathSha256,
            SourceCatalogCandidateId = skeleton.SourceCatalogCandidateId,
            SourceNodeId = skeleton.SourceNodeId,
            SourceOptionId = skeleton.SourceOptionId,
            SourceCandidate = source.Candidate,
            TerminalOptionId = skeleton.TerminalOptionId,
            TerminalCandidate = source.Candidate,
            ProofShape = skeleton.ProofShape,
            RequiresAcquisitionOrProcessingRollout = false,
            RequiresNativeShopSaleProof =
                skeleton.RequiresNativeShopSaleProof,
            RequiresNativeShippingDepositProof =
                skeleton.RequiresNativeShippingDepositProof,
            RequiresNativeShippingSettlementProof =
                skeleton.RequiresNativeShippingSettlementProof,
            UsesFreshTerminalRebind = false,
            TerminalStateHash = sourceStateHash,
            TerminalSnapshotPath = sourceSnapshotPath,
            TerminalSnapshotSha256 = sourceSnapshotSha256,
            PathNodeIds = skeleton.PathNodeIds,
            PathEdges = skeleton.PathEdges
        });
    }

    private static MoneyRevenueConcreteAlternative BuildRebound(
        MoneyRevenueAlternativeSkeleton skeleton,
        SourceCandidateRef source,
        MoneyRevenueTerminalCandidateRebindEvidence evidence,
        MoneyRevenueTerminalCandidateRebindRequest request,
        MoneyRevenueReboundTerminalCandidate rebound)
    {
        Require(skeleton.RequiresAcquisitionOrProcessingRollout &&
                skeleton.RequiresFreshTerminalCandidateRebind &&
                !skeleton.ConcreteAlternativeIdentityComplete &&
                evidence.AlternativeId == skeleton.AlternativeId &&
                evidence.SkeletonPathSha256 == skeleton.PathSha256 &&
                evidence.SourceCatalogCandidateId ==
                    skeleton.SourceCatalogCandidateId &&
                evidence.SourceCandidateId == skeleton.SourceCandidateId &&
                evidence.TerminalOptionId == skeleton.TerminalOptionId &&
                evidence.ProofShape == skeleton.ProofShape &&
                request.AlternativeId == skeleton.AlternativeId &&
                IsSha256(evidence.EvidenceSha256) &&
                !string.IsNullOrWhiteSpace(evidence.TerminalSnapshotPath) &&
                IsSha256(evidence.TerminalSnapshotSha256) &&
                IsSha256(rebound.ConcreteAlternativeSha256) &&
                rebound.TerminalCandidate.ComparisonEligible,
            "Money Revenue rebound evidence drifted from its skeleton: " +
            skeleton.AlternativeId + ".");
        Require(rebound.TerminalCandidate.CatalogCandidateId.Length > 0 &&
                rebound.TerminalCandidate.CandidateSha256.Length == 64,
            "Money Revenue rebound terminal candidate identity is incomplete.");
        return FinalizeAlternative(new MoneyRevenueConcreteAlternative
        {
            ConcreteAlternativeId = rebound.ConcreteAlternativeId,
            SkeletonAlternativeId = skeleton.AlternativeId,
            SkeletonPathSha256 = skeleton.PathSha256,
            SourceCatalogCandidateId = skeleton.SourceCatalogCandidateId,
            SourceNodeId = skeleton.SourceNodeId,
            SourceOptionId = skeleton.SourceOptionId,
            SourceCandidate = source.Candidate,
            TerminalOptionId = skeleton.TerminalOptionId,
            TerminalCandidate = rebound.TerminalCandidate,
            ProofShape = skeleton.ProofShape,
            RequiresAcquisitionOrProcessingRollout = true,
            RequiresNativeShopSaleProof =
                skeleton.RequiresNativeShopSaleProof,
            RequiresNativeShippingDepositProof =
                skeleton.RequiresNativeShippingDepositProof,
            RequiresNativeShippingSettlementProof =
                skeleton.RequiresNativeShippingSettlementProof,
            UsesFreshTerminalRebind = true,
            TerminalStateHash = evidence.TerminalStateHash,
            TerminalSnapshotPath = evidence.TerminalSnapshotPath,
            TerminalSnapshotSha256 = evidence.TerminalSnapshotSha256,
            RebindEvidenceSha256 = evidence.EvidenceSha256,
            AcquisitionRolloutId = evidence.RolloutId,
            AcquisitionRolloutManifestPath = Path.GetFullPath(
                request.AcquisitionRolloutProofManifestPath),
            AcquisitionRolloutManifestSha256 =
                evidence.RolloutManifestSha256,
            AcquisitionRolloutReceiptPath = Path.GetFullPath(
                request.AcquisitionRolloutProofReceiptPath),
            AcquisitionRolloutReceiptSha256 =
                evidence.RolloutReceiptSha256,
            PathNodeIds = skeleton.PathNodeIds,
            PathEdges = skeleton.PathEdges
        });
    }

    private static MoneyRevenueConcreteAlternative FinalizeAlternative(
        MoneyRevenueConcreteAlternative alternative)
    {
        alternative.ConcreteAlternativeSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                alternative);
        return alternative;
    }
}
