using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropResourceReservationBuilder
{
    public const string Schema = "money_revenue_crop_resource_reservation_receipt.v1";

    public static MoneyRevenueCropResourceReservationReceipt Build(string decisionRequestPath) =>
        BuildWithDecision(decisionRequestPath).Resources;

    internal static (MoneyRevenueCropResourceReservationReceipt Resources,
        MoneyRevenueCropProductionDecisionReceipt Decision) BuildWithDecision(string decisionRequestPath)
    {
        var path = Path.GetFullPath(decisionRequestPath);
        var decision = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionDecisionRequest>(
            path, "crop resource reservation Teacher decision");
        Require(decision.SchemaVersion == "money_revenue_crop_production_decision_request.v2",
            "Crop resource reservation requires an independently rebuildable v2 Teacher decision.");
        var original = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(path);
        Require(original.AllCommitmentsAccepted && original.FinalLedger is not null &&
                !original.FormalProductTrainingAuthorized,
            "Crop resource reservation requires an accepted original crop commitment receipt.");
        var ledger = original.FinalLedger!;
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            original.SnapshotPath, "crop resource reservation snapshot");
        Require(CurrentTeacherFrontierSupport.HashFile(path) == original.RequestSha256 &&
                CurrentTeacherFrontierSupport.HashFile(original.SnapshotPath) == original.SnapshotSha256 &&
                snapshot.StateHash == original.SourceStateHash &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(snapshot.State) &&
                ledger.SourceStateHash == snapshot.StateHash && original.DecisionId == decision.DecisionId,
            "Crop resource reservation source changed after original decision verification.");
        var selected = decision.TeacherCandidateEvaluations.Single(row => row.Selected);
        var commitment = decision.Commitments.Single();
        Require(selected.Admitted && selected.TileCount == commitment.TileCount &&
                selected.SeedId == commitment.SeedId && selected.ExistingSeedQuantity >= 0,
            "Crop resource reservation selection drifted from its original commitment.");

        var reserved = Math.Min(selected.ExistingSeedQuantity, selected.TileCount);
        var shortfall = checked(selected.TileCount - reserved);
        var cost = checked(shortfall * selected.SeedUnitPrice);
        Require(cost >= 0 && cost == selected.SeedAcquisitionCost &&
                (shortfall == 0 || selected.SeedUnitPrice > 0),
            "Crop resource reservation purchase cost drifted from the Teacher shortfall.");
        var materials = reserved == 0 ? Array.Empty<MaterialReservationUpsertRequest>() :
            MoneyRevenueCropSeedReservationCommitBuilder.AllocateSeeds(snapshot, ledger,
                commitment.CommitmentId, decision.DecisionId, selected.SeedQualifiedItemId, reserved);
        var currencies = cost == 0 ? Array.Empty<CurrencyReservationUpsertRequest>() : new[]
        {
            new CurrencyReservationUpsertRequest
            {
                StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
                ReservationId = commitment.CommitmentId + ":seed-purchase:money",
                SourceDecisionId = decision.DecisionId, GoalId = decision.GoalId,
                CurrencyId = NativeShopCurrencies.Money, Amount = cost,
                Purpose = "crop_seed_purchase:" + commitment.CommitmentId
            }
        };
        var ids = materials.Select(row => row.ReservationId)
            .Concat(currencies.Select(row => row.ReservationId)).ToHashSet(StringComparer.Ordinal);
        Require(!ledger.MaterialReservations.Any(row => ids.Contains(row.ReservationId)) &&
                !ledger.CurrencyReservations.Any(row => ids.Contains(row.ReservationId)),
            "Crop resource reservation cannot overwrite an existing reservation identity.");
        var request = new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = commitment.CommitmentId + ":resource-portfolio",
            GoalId = decision.GoalId, SourceDecisionId = decision.DecisionId,
            MaterialClaims = materials, CurrencyClaims = currencies
        };
        var result = new ReservationPortfolioLedgerService().Commit(
            ledger, snapshot, request, decision.DecisionRecordedAt);
        Require(result.Accepted && result.Ledger is not null && result.Errors.Length == 0,
            "Crop resource reservation atomic commit rejected: " + string.Join(",", result.Errors));

        return (new MoneyRevenueCropResourceReservationReceipt
        {
            SchemaVersion = Schema, Status = "verified_crop_resource_reservation_replay",
            DecisionRequestPath = path, DecisionRequestSha256 = original.RequestSha256,
            SourceSnapshotPath = original.SnapshotPath, SourceSnapshotSha256 = original.SnapshotSha256,
            SourceStateHash = original.SourceStateHash, DecisionId = decision.DecisionId,
            CommitmentId = commitment.CommitmentId, SeedQualifiedItemId = selected.SeedQualifiedItemId,
            SeedReservedQuantity = reserved, SeedAcquisitionQuantity = shortfall, SeedAcquisitionCost = cost,
            ReservationRequest = request, ReservationResult = result,
            ResourceReservationReplayVerified = true
        }, original);
    }

    public static MoneyRevenueCropResourceReservationReceipt Verify(string receiptPath)
    {
        var receipt = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropResourceReservationReceipt>(
            Path.GetFullPath(receiptPath), "crop resource reservation receipt");
        Require(receipt.SchemaVersion == Schema && receipt.ResourceReservationReplayVerified &&
                !receipt.RuntimeLedgerInstalled && !receipt.FormalProductTrainingAuthorized,
            "Crop resource reservation receipt has invalid scope or authority.");
        Require(EqualJson(receipt, Build(receipt.DecisionRequestPath)),
            "Crop resource reservation receipt did not independently rebuild exactly.");
        return receipt;
    }
}
