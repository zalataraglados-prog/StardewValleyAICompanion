using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueAlternativeComparisonArtifact(
        AcquisitionRoutePortfolioSupervisionCorpusSource acquisitionSource,
        string fixtureRoot)
    {
        var root = Path.Combine(
            fixtureRoot,
            "money-revenue-alternative-comparison-artifact");
        Directory.CreateDirectory(root);
        var rolloutManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            acquisitionSource.ProofManifestPath,
            "Revenue comparison acquisition rollout manifest");
        var lastSnapshotPath = rolloutManifest.ContinuationTransitions.Length > 0
            ? rolloutManifest.ContinuationTransitions[^1].AfterSnapshotPath
            : rolloutManifest.InitialCheckpointProof.AfterSnapshotPath;
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            lastSnapshotPath,
            "Revenue comparison acquisition terminal snapshot");
        var higher = BuildMoneyRevenueArtifactAlternative(
            root,
            "higher",
            before,
            revenue: 20,
            acquisitionSource);
        var lower = BuildMoneyRevenueArtifactAlternative(
            root,
            "lower",
            before,
            revenue: 10,
            acquisitionSource);
        var manifest = new
            MoneyRevenueAlternativeComparisonProofManifest
            {
                CriterionId = "money_50000",
                Alternatives = new[] { higher.Proof, lower.Proof }
            };
        var manifestPath = Path.Combine(root, "comparison-manifest.json");
        Write(manifestPath, manifest);
        var receipt = MoneyRevenueAlternativeComparisonProofBuilder.Build(
            manifestPath);
        Require(
            receipt.Status ==
                "verified_non_authoritative_revenue_alternative_comparison" &&
            receipt.ArtifactProofVerified &&
            !receipt.CandidateDenominatorAuthoritative &&
            !receipt.ProductionTeacherPreferenceLabelEligible &&
            !receipt.FormalProductTrainingAuthorized &&
            receipt.Alternatives.Length == 2 &&
            receipt.Alternatives.All(row =>
                row.RouteTransitions.Length == 3) &&
            receipt.Comparison.SelectionDisposition ==
                MoneyRevenueAlternativeSelectionDisposition
                    .UniqueAuthoritativePreference &&
            receipt.Comparison.SelectedAlternativeId == "alternative.higher" &&
            receipt.Comparison.TeacherPreferenceLabelEligible &&
            !receipt.Comparison.FormalTrainingAuthorized,
            "Revenue comparison artifact binding drifted.");

        var tamperedReceipt = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
            higher.Proof.RevenueRecurrenceProofReceiptPath,
            "Revenue comparison recurrence receipt");
        tamperedReceipt.ProofVerified = false;
        var tamperedReceiptPath = Path.Combine(
            root,
            "tampered-revenue-receipt.json");
        Write(tamperedReceiptPath, tamperedReceipt);
        var tamperedManifestPath = Path.Combine(
            root,
            "tampered-comparison-manifest.json");
        higher.Proof.RevenueRecurrenceProofReceiptPath = tamperedReceiptPath;
        Write(tamperedManifestPath, new
            MoneyRevenueAlternativeComparisonProofManifest
            {
                CriterionId = "money_50000",
                Alternatives = new[] { higher.Proof, lower.Proof }
            });
        var rejected = false;
        try
        {
            MoneyRevenueAlternativeComparisonProofBuilder.Build(
                tamperedManifestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Revenue recurrence receipt entered artifact comparison.");
    }

    private static MoneyRevenueArtifactAlternativeFixture
        BuildMoneyRevenueArtifactAlternative(
            string root,
            string fixtureId,
            SnapshotEnvelope before,
            int revenue,
            AcquisitionRoutePortfolioSupervisionCorpusSource acquisitionSource)
    {
        var alternativeRoot = Path.Combine(root, fixtureId);
        Directory.CreateDirectory(alternativeRoot);
        var after = MoneyRevenueSaleAfterSnapshot(before, revenue);
        var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            before,
            after,
            "comparison-" + fixtureId);
        var recurrenceManifestPath = Path.Combine(
            alternativeRoot,
            "revenue-recurrence-manifest.json");
        Write(recurrenceManifestPath, RevenueRecurrenceManifest(
            new MoneyRevenueRecurrenceTransitionProof
            {
                TransitionId = "transition.shop-sale." + fixtureId,
                ProofKind = MoneyRevenueRecurrenceProofKinds.NativeShopSale,
                ShopSaleEpisodePath = episodePath
            }));
        var recurrenceReceipt = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        var recurrenceReceiptPath = Path.Combine(
            alternativeRoot,
            "revenue-recurrence-receipt.json");
        Write(recurrenceReceiptPath, recurrenceReceipt);
        return new MoneyRevenueArtifactAlternativeFixture(
            new MoneyRevenueAlternativeArtifactProof
            {
                AlternativeId = "alternative." + fixtureId,
                AcquisitionRolloutProofManifestPath =
                    acquisitionSource.ProofManifestPath,
                AcquisitionRolloutProofReceiptPath =
                    acquisitionSource.ProofReceiptPath,
                RevenueRecurrenceProofManifestPath = recurrenceManifestPath,
                RevenueRecurrenceProofReceiptPath = recurrenceReceiptPath
            });
    }

    private static SnapshotEnvelope MoneyRevenueSaleAfterSnapshot(
        SnapshotEnvelope before,
        int revenue)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(
            before,
            JsonDefaults.Options))!.AsObject();
        var player = root["state"]!["player"]!.AsObject();
        player["money"]!["value"] =
            player["money"]!["value"]!.GetValue<int>() + revenue;
        player["total_money_earned"]!["value"] =
            player["total_money_earned"]!["value"]!.GetValue<uint>() +
            (uint)revenue;
        root["game_tick"] = before.GameTick + 1;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(
            root.ToJsonString(JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Revenue comparison after snapshot clone failed.");
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private sealed record MoneyRevenueArtifactAlternativeFixture(
        MoneyRevenueAlternativeArtifactProof Proof);
}
