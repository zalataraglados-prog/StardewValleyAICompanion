using System.Globalization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropProductionDecisionReceiptBuilder
{
    public static MoneyRevenueCropProductionDecisionReceipt Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop-production decision request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionDecisionRequest>(
                fullRequestPath,
                "Money Revenue crop-production decision request");
        ValidateRequest(request);
        if (request.SchemaVersion ==
            "money_revenue_crop_production_decision_request.v2")
        {
            var rebuilt =
                MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                    request.TeacherRequestPath);
            Require(BootstrapArtifactGuard.EqualJson(request, rebuilt),
                "Money Revenue crop-production Teacher decision does not rebuild exactly.");
        }

        var snapshotPath = RequiredFile(
            request.SnapshotPath,
            "Money Revenue crop-production decision snapshot");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Money Revenue crop-production decision snapshot");
        ValidateSnapshot(snapshot);

        StrategyCommitmentLedger? ledger = null;
        var baseLedgerPath = string.Empty;
        var baseLedgerSha256 = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.BaseStrategyLedgerPath))
        {
            baseLedgerPath = RequiredFile(
                request.BaseStrategyLedgerPath,
                "Money Revenue crop-production base strategy ledger");
            ledger = AcquisitionStrategyLedgerReader.Read(
                baseLedgerPath,
                snapshot).Ledger;
            Require(ledger.SourceStateHash == snapshot.StateHash,
                "Money Revenue crop-production base ledger is not anchored to the source snapshot.");
            baseLedgerSha256 = CurrentTeacherFrontierSupport.HashFile(
                baseLedgerPath);
        }

        var normalized = ValidateAndNormalizeCommitments(
            request.Commitments);
        var requestedCommitmentIds = normalized.Select(value =>
                value.CommitmentId)
            .ToHashSet(StringComparer.Ordinal);
        var existingActiveCommitmentIds = (ledger?.CropPlantingCommitments ??
                Array.Empty<CropPlantingCommitment>())
            .Where(value => value.Status == StrategyCommitmentStatuses.Active)
            .Select(value => value.CommitmentId)
            .ToArray();
        Require(existingActiveCommitmentIds.All(
                requestedCommitmentIds.Contains),
            "Money Revenue crop-production decision must restate every active crop commitment from its base ledger.");
        var mutations = new List<
            MoneyRevenueCropProductionDecisionMutationReceipt>();
        var service = new CropCommitmentLedgerService();
        foreach (var item in normalized)
        {
            var upsert = new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = ledger?.Revision ?? 0,
                CommitmentId = item.CommitmentId,
                SourceDecisionId = request.DecisionId,
                SeedId = item.SeedId,
                TileCount = item.TileCount,
                PlantingYear = item.PlantingYear,
                PlantingSeason = item.PlantingSeason,
                PlantingDayOfMonth = item.PlantingDayOfMonth,
                LocationContext = item.LocationContext
            };
            var result = service.Upsert(
                ledger,
                snapshot,
                upsert,
                request.DecisionRecordedAt);
            var resultingCommitment = result.Ledger?
                .CropPlantingCommitments.SingleOrDefault(value =>
                    value.CommitmentId == item.CommitmentId);
            mutations.Add(new MoneyRevenueCropProductionDecisionMutationReceipt(
                mutations.Count + 1,
                upsert,
                result.Accepted,
                result.Errors ?? Array.Empty<string>(),
                result.Ledger?.Revision,
                resultingCommitment?.Revision));
            if (!result.Accepted || result.Ledger is null)
                break;
            ledger = result.Ledger;
        }

        var finalActiveCommitmentIds = (ledger?.CropPlantingCommitments ??
                Array.Empty<CropPlantingCommitment>())
            .Where(value => value.Status == StrategyCommitmentStatuses.Active)
            .Select(value => value.CommitmentId)
            .ToHashSet(StringComparer.Ordinal);
        var allAccepted = mutations.Count == normalized.Length &&
            mutations.All(value => value.Accepted) &&
            ledger is not null &&
            finalActiveCommitmentIds.SetEquals(requestedCommitmentIds);
        return new MoneyRevenueCropProductionDecisionReceipt
        {
            SchemaVersion = request.SchemaVersion.EndsWith(
                ".v2",
                StringComparison.Ordinal)
                    ? "money_revenue_crop_production_decision_receipt.v2"
                    : "money_revenue_crop_production_decision_receipt.v1",
            Status = allAccepted
                ? "crop_production_decision_committed"
                : "crop_production_decision_rejected",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            SnapshotPath = snapshotPath,
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                snapshotPath),
            BaseStrategyLedgerPath = baseLedgerPath,
            BaseStrategyLedgerSha256 = baseLedgerSha256,
            SourceStateHash = snapshot.StateHash,
            DecisionId = request.DecisionId,
            DecisionRecordedAt = request.DecisionRecordedAt,
            GoalId = request.GoalId,
            DeadlineTotalDayExclusive = request.DeadlineTotalDayExclusive,
            TargetTotalMoneyEarned = request.TargetTotalMoneyEarned,
            Mutations = mutations.ToArray(),
            FinalLedger = ledger,
            LayoutIntents = normalized.Select(value =>
                new MoneyRevenueCropNativeProductionLayoutIntent(
                    value.CommitmentId,
                    value.LocationId,
                    value.Tiles,
                    value.WateringRefillPreparationTiles)).ToArray(),
            MaxQueueItemCount = request.MaxQueueItemCount,
            AllCommitmentsAccepted = allAccepted,
            FormalProductTrainingAuthorized = false,
            TeacherRequestPath = request.TeacherRequestPath,
            TeacherRequestSha256 = request.TeacherRequestSha256,
            TeacherSelectionPolicy = request.TeacherSelectionPolicy
        };
    }

    private static void ValidateRequest(
        MoneyRevenueCropProductionDecisionRequest request)
    {
        var supportedSchema = request.SchemaVersion is
            "money_revenue_crop_production_decision_request.v1" or
            "money_revenue_crop_production_decision_request.v2";
        Require(supportedSchema &&
                !string.IsNullOrWhiteSpace(request.DecisionId) &&
                request.GoalId == "earn_money" &&
                request.DeadlineTotalDayExclusive == 224 &&
                request.TargetTotalMoneyEarned == 1_000_000 &&
                request.Commitments is { Length: > 0 } &&
                request.MaxQueueItemCount is >= 1 and <= 8 &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop-production decision request is invalid.");
        if (request.SchemaVersion ==
            "money_revenue_crop_production_decision_request.v2")
        {
            Require(!string.IsNullOrWhiteSpace(request.TeacherRequestPath) &&
                    request.TeacherRequestSha256 is { Length: 64 } &&
                    request.TeacherLayoutSnapshots is { Length: > 0 } &&
                    request.TeacherCandidateEvaluations is { Length: > 0 } &&
                    request.TeacherCandidateEvaluations.Count(value =>
                        value.Selected) == 1 &&
                    request.TeacherSelectionPolicy ==
                        MoneyRevenueCropProductionTeacherDecisionBuilder
                            .SelectionPolicy,
                "Money Revenue crop-production Teacher binding is invalid.");
        }
        Require(DateTimeOffset.TryParse(
                    request.DecisionRecordedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var recordedAt) &&
                recordedAt.Offset == TimeSpan.Zero,
            "Money Revenue crop-production decision timestamp must be an explicit UTC timestamp.");
    }

    private static MoneyRevenueCropProductionDecisionCommitment[]
        ValidateAndNormalizeCommitments(
            MoneyRevenueCropProductionDecisionCommitment[] commitments)
    {
        Require(commitments.All(value => value is not null) &&
                commitments.Select(value => value.CommitmentId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    commitments.Length,
            "Money Revenue crop-production decision commitment IDs are duplicated.");
        var occupied = new HashSet<MoneyRevenueCropPlantingTile>();
        var preparationOnly = new HashSet<MoneyRevenueCropPlantingTile>();
        foreach (var item in commitments)
        {
            var tiles = item.Tiles ?? throw new InvalidDataException(
                "Money Revenue crop-production decision layout is missing: " +
                item.CommitmentId);
            var refillPreparationTiles =
                item.WateringRefillPreparationTiles ??
                Array.Empty<MoneyRevenueCropPlantingTile>();
            var completePreparationLayout = tiles
                .Concat(refillPreparationTiles)
                .ToArray();
            Require(!string.IsNullOrWhiteSpace(item.CommitmentId) &&
                    !string.IsNullOrWhiteSpace(item.SeedId) &&
                    item.TileCount > 0 &&
                    item.PlantingYear >= 1 &&
                    item.PlantingDayOfMonth is >= 1 and <= 28 &&
                    !string.IsNullOrWhiteSpace(item.PlantingSeason) &&
                    item.LocationContext == "outdoor_seasonal" &&
                    item.LocationId == "Farm" &&
                    tiles.Length == item.TileCount &&
                    tiles.All(tile =>
                        tile.TileX >= 0 && tile.TileY >= 0) &&
                    tiles.Distinct().Count() == tiles.Length &&
                    refillPreparationTiles.Length <=
                        MoneyRevenueCropProductionTeacherDecisionBuilder
                            .MaxWateringRefillPreparationTileCount &&
                    refillPreparationTiles.All(tile =>
                        tile.TileX >= 0 && tile.TileY >= 0) &&
                    refillPreparationTiles.Distinct().Count() ==
                        refillPreparationTiles.Length &&
                    completePreparationLayout.Distinct().Count() ==
                        completePreparationLayout.Length &&
                    FitsFarmLayoutProjection(completePreparationLayout),
                "Money Revenue crop-production decision commitment is invalid: " +
                item.CommitmentId);
            Require(tiles.All(tile =>
                    !preparationOnly.Contains(tile) && occupied.Add(tile)),
                "Money Revenue crop-production decision layouts overlap.");
            Require(refillPreparationTiles.All(tile =>
                    !occupied.Contains(tile)),
                "Money Revenue crop-production refill preparation overlaps a crop layout.");
            preparationOnly.UnionWith(refillPreparationTiles);
            item.Tiles = tiles.OrderBy(tile => tile.TileY)
                .ThenBy(tile => tile.TileX).ToArray();
            item.WateringRefillPreparationTiles = refillPreparationTiles
                .OrderBy(tile => tile.TileY)
                .ThenBy(tile => tile.TileX)
                .ToArray();
        }
        return commitments;
    }

    private static bool FitsFarmLayoutProjection(
        MoneyRevenueCropPlantingTile[] tiles)
    {
        var minX = tiles.Min(value => value.TileX);
        var maxX = tiles.Max(value => value.TileX);
        var minY = tiles.Min(value => value.TileY);
        var maxY = tiles.Max(value => value.TileY);
        return (long)maxX - minX + 1 <= 32 &&
            (long)maxY - minY + 1 <= 32;
    }

    private static void ValidateSnapshot(SnapshotEnvelope snapshot)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop-production decision snapshot identity is invalid.");
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
