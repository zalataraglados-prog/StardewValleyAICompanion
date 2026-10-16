using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropSeedReservationProofBuilder
{
    public static MoneyRevenueCropSeedReservationProof Build(string requestPath)
    {
        var requestFile = RequiredFile(requestPath, "seed-reservation proof request");
        var request = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropSeedReservationProofRequest>(requestFile, "seed-reservation proof request");
        Require(request.SchemaVersion == "money_revenue_crop_seed_reservation_proof_request.v1" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-reservation proof request is invalid.");

        var planFile = RequiredFile(request.CropProductionPlanPath, "crop-production plan");
        var plan = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropProductionObligationPlan>(planFile, "crop-production plan");
        Require(BootstrapArtifactGuard.EqualJson(plan, MoneyRevenueCropProductionObligationPlanBuilder.Build(plan.RequestPath)),
            "Money Revenue crop-production plan is not exact.");
        var commitment = plan.Commitments.SingleOrDefault(row => row.CommitmentId == request.CommitmentId);
        Require(commitment is not null && plan.PendingEvidenceRequestIds.Contains("seed-reservation:" + request.CommitmentId, StringComparer.Ordinal),
            "Money Revenue crop seed-reservation request is outside the denominator.");
        var exactCommitment = commitment ?? throw new InvalidDataException(
            "Money Revenue crop seed-reservation commitment is missing.");

        var snapshotFile = RequiredFile(request.ReservationSnapshotPath, "seed-reservation snapshot");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotFile, "seed-reservation snapshot");
        Require(snapshot.SchemaVersion == "snapshot.v1" && snapshot.GameVersion == "1.6.15" &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(snapshot.State),
            "Money Revenue crop seed-reservation snapshot is invalid.");
        var source = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(plan.SnapshotPath, "crop-production source snapshot");
        Require(source.SaveId.Status == FieldStatus.Available &&
                source.PlayerId.Status == FieldStatus.Available &&
                snapshot.SaveId.Status == FieldStatus.Available &&
                snapshot.PlayerId.Status == FieldStatus.Available &&
                source.SaveId.Value == snapshot.SaveId.Value &&
                source.PlayerId.Value == snapshot.PlayerId.Value &&
                ReadInt(snapshot, "time", "total_days") <= exactCommitment.SeedSupplyDeadlineTotalDay,
            "Money Revenue crop seed reservation changed actor or missed its deadline.");

        var ledgerFile = RequiredFile(request.CommittedStrategyLedgerPath, "committed strategy ledger");
        var ledger = AcquisitionStrategyLedgerReader.Read(ledgerFile, snapshot).Ledger;
        var purpose = "crop_seed_supply:" + exactCommitment.CommitmentId;
        var claims = ledger.MaterialReservations.Where(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                row.Purpose == purpose &&
                row.QualifiedItemId == exactCommitment.SeedQualifiedItemId)
            .OrderBy(row => row.ReservationId, StringComparer.Ordinal)
            .ToArray();
        Require(claims.Length > 0 && claims.Select(row => row.ReservationId).Distinct(StringComparer.Ordinal).Count() == claims.Length &&
                claims.All(row => row.SourceStateHash == snapshot.StateHash && row.Quantity > 0) &&
                claims.Sum(row => row.Quantity) == exactCommitment.TileCount,
            "Money Revenue crop seed reservation is not an exact full-quantity active claim set.");

        var graphElement = ReadValue(snapshot, "farm", "material_inventory_graph");
        var graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(graphElement.GetRawText(), JsonDefaults.Options)
            ?? throw new InvalidDataException("Money Revenue crop material inventory graph is empty.");
        var supply = new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations);
        Require(supply.Status == "available" && supply.BlockingReasons.Length == 0 && claims.All(claim =>
                supply.Slots.Any(slot => slot.NodeId == claim.NodeId && slot.SlotIndex == claim.SlotIndex &&
                    slot.QualifiedItemId == claim.QualifiedItemId && slot.ReservedQuantity >= claim.Quantity)),
            "Money Revenue crop seed reservations do not bind valid transparent inventory slots.");

        return new MoneyRevenueCropSeedReservationProof
        {
            RequestPath = requestFile,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(requestFile),
            CropProductionPlanPath = planFile,
            CropProductionPlanSha256 = CurrentTeacherFrontierSupport.HashFile(planFile),
            EvidenceRequestId = "seed-reservation:" + exactCommitment.CommitmentId,
            CommitmentId = exactCommitment.CommitmentId,
            ReservationSnapshotPath = snapshotFile,
            ReservationSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotFile),
            CommittedStrategyLedgerPath = ledgerFile,
            CommittedStrategyLedgerSha256 = CurrentTeacherFrontierSupport.HashFile(ledgerFile),
            ReservationStateHash = snapshot.StateHash,
            SeedQualifiedItemId = exactCommitment.SeedQualifiedItemId,
            RequiredQuantity = exactCommitment.TileCount,
            ReservedQuantity = claims.Sum(row => row.Quantity),
            ReservationIds = claims.Select(row => row.ReservationId).ToArray(),
            SeedReservationVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static JsonElement ReadValue(
        SnapshotEnvelope snapshot,
        string domain,
        string field)
    {
        if (!snapshot.State.TryGetValue(domain, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                "Money Revenue crop seed-reservation field is unavailable: " +
                domain + "." + field);
        }
        return value;
    }
    private static int ReadInt(SnapshotEnvelope snapshot, string domain, string field) =>
        ReadValue(snapshot, domain, field).GetInt32();
    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var full = Path.GetFullPath(path);
        Require(File.Exists(full), label + " is missing: " + full);
        return full;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
