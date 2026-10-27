using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropSeedReservationCommitBuilder
{
    public static ReservationPortfolioCommitRequest Build(
        string planPath,
        string commitmentId,
        string reservationSnapshotPath)
    {
        var context = ReadContext(
            planPath,
            commitmentId,
            reservationSnapshotPath);
        var graph = ReadGraph(context.Snapshot);
        var supply = new MaterialSupplyProjection().Project(
            graph,
            context.BaseLedger.MaterialReservations);
        Require(supply.Status == "available" &&
                supply.BlockingReasons.Length == 0,
            "Money Revenue crop seed supply is unavailable for reservation.");

        var remaining = context.Commitment.TileCount;
        var claims = new List<MaterialReservationUpsertRequest>();
        foreach (var slot in supply.Slots
                     .Where(row =>
                         row.QualifiedItemId ==
                            context.Commitment.SeedQualifiedItemId &&
                         row.AvailableQuantity > 0)
                     .OrderBy(row => row.NodeId, StringComparer.Ordinal)
                     .ThenBy(row => row.SlotIndex))
        {
            if (remaining == 0)
                break;
            var quantity = Math.Min(remaining, slot.AvailableQuantity);
            claims.Add(new MaterialReservationUpsertRequest
            {
                StateHash = context.Snapshot.StateHash,
                ExpectedLedgerRevision = context.BaseLedger.Revision,
                ReservationId = context.Commitment.CommitmentId +
                    ":seed:" + claims.Count,
                SourceDecisionId = context.CropCommitment.SourceDecisionId,
                GoalId = "earn_money",
                NodeId = slot.NodeId,
                SlotIndex = slot.SlotIndex,
                QualifiedItemId = context.Commitment.SeedQualifiedItemId,
                Quantity = quantity,
                Purpose = "crop_seed_supply:" +
                    context.Commitment.CommitmentId
            });
            remaining -= quantity;
        }
        Require(remaining == 0 && claims.Count > 0,
            "Money Revenue crop seed reservation cannot cover the exact tile denominator.");
        Require(claims.Select(row => row.ReservationId)
                .Distinct(StringComparer.Ordinal).Count() == claims.Count &&
                !context.BaseLedger.MaterialReservations.Any(existing =>
                    claims.Any(claim =>
                        claim.ReservationId == existing.ReservationId)),
            "Money Revenue crop seed reservation ids are not fresh and unique.");

        return new ReservationPortfolioCommitRequest
        {
            StateHash = context.Snapshot.StateHash,
            ExpectedLedgerRevision = context.BaseLedger.Revision,
            PortfolioId = context.Commitment.CommitmentId + ":portfolio",
            GoalId = "earn_money",
            SourceDecisionId = context.CropCommitment.SourceDecisionId,
            MaterialClaims = claims.ToArray()
        };
    }

    public static ReservationPortfolioCommitResult Replay(
        string planPath,
        string commitmentId,
        string reservationSnapshotPath,
        ReservationPortfolioCommitRequest request,
        string updatedAt)
    {
        var context = ReadContext(
            planPath,
            commitmentId,
            reservationSnapshotPath);
        var expected = Build(
            planPath,
            commitmentId,
            reservationSnapshotPath);
        Require(BootstrapArtifactGuard.EqualJson(expected, request),
            "Money Revenue crop seed-reservation commit request is not exact.");
        Require(DateTimeOffset.TryParse(updatedAt, out _),
            "Money Revenue crop seed-reservation commit timestamp is invalid.");
        var result = new ReservationPortfolioLedgerService().Commit(
            context.BaseLedger,
            context.Snapshot,
            request,
            updatedAt);
        Require(result.Accepted && result.Ledger is not null &&
                (result.Errors?.Length ?? 0) == 0,
            "Money Revenue crop seed-reservation atomic commit was rejected: " +
            string.Join(",", result.Errors ?? Array.Empty<string>()));
        return result;
    }

    private static SeedReservationContext ReadContext(
        string planPath,
        string commitmentId,
        string reservationSnapshotPath)
    {
        Require(!string.IsNullOrWhiteSpace(commitmentId),
            "Money Revenue crop seed-reservation commitment id is required.");
        var planFile = RequiredFile(planPath, "crop-production plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planFile,
                "crop-production plan");
        Require(BootstrapArtifactGuard.EqualJson(
                plan,
                MoneyRevenueCropProductionObligationPlanBuilder.Build(
                    plan.RequestPath)),
            "Money Revenue crop-production plan is not exact.");
        var commitment = plan.Commitments.SingleOrDefault(row =>
            row.CommitmentId == commitmentId);
        Require(commitment is not null &&
                plan.PendingEvidenceRequestIds.Contains(
                    "seed-reservation:" + commitmentId,
                    StringComparer.Ordinal),
            "Money Revenue crop seed-reservation request is outside the denominator.");
        var exactCommitment = commitment ?? throw new InvalidDataException(
            "Money Revenue crop seed-reservation commitment is missing.");

        var snapshotFile = RequiredFile(
            reservationSnapshotPath,
            "seed-reservation snapshot");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotFile,
            "seed-reservation snapshot");
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop seed-reservation snapshot is invalid.");
        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            plan.SnapshotPath,
            "crop-production source snapshot");
        Require(source.SaveId.Status == FieldStatus.Available &&
                source.PlayerId.Status == FieldStatus.Available &&
                snapshot.SaveId.Status == FieldStatus.Available &&
                snapshot.PlayerId.Status == FieldStatus.Available &&
                source.SaveId.Value == snapshot.SaveId.Value &&
                source.PlayerId.Value == snapshot.PlayerId.Value,
            "Money Revenue crop seed reservation changed actor identity.");

        var baseLedger = AcquisitionStrategyLedgerReader.Read(
            plan.StrategyLedgerPath,
            source).Ledger;
        var cropCommitment = baseLedger.CropPlantingCommitments
            .SingleOrDefault(row =>
                row.CommitmentId == exactCommitment.CommitmentId);
        Require(cropCommitment is not null &&
                cropCommitment.Status == StrategyCommitmentStatuses.Active &&
                cropCommitment.SeedQualifiedItemId ==
                    exactCommitment.SeedQualifiedItemId &&
                cropCommitment.TileCount == exactCommitment.TileCount,
            "Money Revenue crop seed reservation lost its source commitment.");
        return new SeedReservationContext(
            plan,
            exactCommitment,
            snapshot,
            baseLedger,
            cropCommitment!);
    }

    private static MaterialInventoryGraph ReadGraph(SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("farm", out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty("material_inventory_graph", out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                "Money Revenue crop material inventory graph is unavailable.");
        }
        return JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.GetRawText(),
                JsonDefaults.Options) ??
            throw new InvalidDataException(
                "Money Revenue crop material inventory graph is empty.");
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var full = Path.GetFullPath(path);
        Require(File.Exists(full), label + " is missing: " + full);
        return full;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record SeedReservationContext(
        MoneyRevenueCropProductionObligationPlan Plan,
        MoneyRevenueCropProductionCommitmentPlan Commitment,
        SnapshotEnvelope Snapshot,
        StrategyCommitmentLedger BaseLedger,
        CropPlantingCommitment CropCommitment);
}
