using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    public const string SelectionPolicy =
        "authoritative_transparent_crop_net_revenue_after_verified_refill_budget_then_earliest_terminal_v3";

    public static MoneyRevenueCropProductionDecisionRequest Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop-production Teacher request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionTeacherRequest>(
                fullRequestPath,
                "Money Revenue crop-production Teacher request");
        ValidateRequest(request);

        var snapshotPath = RequiredFile(
            request.SnapshotPath,
            "Money Revenue crop-production Teacher snapshot");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Money Revenue crop-production Teacher snapshot");
        ValidateSnapshot(snapshot);

        var layouts = ReadLayoutSnapshots(
            request.FarmLayoutSnapshotPaths,
            snapshot);
        Require(layouts.Select(value => (
                    value.Binding.MapWidth,
                    value.Binding.MapHeight))
                .Distinct().Count() == 1,
            "Money Revenue crop-production Teacher layout map dimensions drifted.");
        var layoutTiles = layouts
            .SelectMany(value => value.Tiles)
            .GroupBy(value => new { value.TileX, value.TileY })
            .Select(group =>
            {
                Require(group.Count() == 1,
                    "Money Revenue crop-production Teacher layout snapshots overlap at " +
                    $"Farm({group.Key.TileX},{group.Key.TileY}).");
                return group.Single();
            })
            .ToArray();
        var mapWidth = layouts[0].Binding.MapWidth;
        var mapHeight = layouts[0].Binding.MapHeight;
        Require(layoutTiles.Length == checked(mapWidth * mapHeight),
            "Money Revenue crop-production Teacher layout snapshots do not cover the complete Farm map.");
        var viableTiles = layoutTiles
            .Where(value => value.Status is
                "already_prepared" or "ready_to_till" or
                "requires_single_clearance_then_till")
            .ToArray();
        Require(viableTiles.Length > 0,
            "Money Revenue crop-production Teacher found no supported Farm tiles.");
        var anchor = ReadFarmAnchor(snapshot, viableTiles);
        var wateringRefillRouteVerified = HasVerifiedWateringRefillRoute(
            layoutTiles,
            viableTiles,
            mapWidth,
            mapHeight,
            anchor.TileX,
            anchor.TileY);

        var evaluations = EvaluateCandidates(
            snapshot,
            request.MaximumTileCount,
            viableTiles.Length,
            wateringRefillRouteVerified);
        var selected = evaluations
            .Where(value => value.Admitted)
            .OrderByDescending(value => value.MinimumNetRevenue)
            .ThenBy(value => value.LastHarvestTotalDay)
            .ThenBy(value => value.RequiredWateringActionCount)
            .ThenBy(value => value.SeedId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected is null)
        {
            var rejectionSummary = evaluations
                .SelectMany(value => value.BlockReasons)
                .GroupBy(value => value, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}");
            throw new InvalidDataException(
                "Money Revenue crop-production Teacher found no admitted " +
                $"crop candidate among {evaluations.Length} evaluations. " +
                "Rejections: " + string.Join(",", rejectionSummary));
        }
        var selectedEvaluations = evaluations.Select(value => value with
        {
            Selected = value.SeedId == selected.SeedId &&
                value.ShopId == selected.ShopId
        }).ToArray();
        var tiles = SelectCompactTiles(
            viableTiles,
            selected.TileCount,
            anchor.TileX,
            anchor.TileY);
        var year = ReadStateInt(snapshot, "time", "year");
        var season = ReadStateString(snapshot, "time", "season");
        var day = ReadStateInt(snapshot, "time", "day");
        var commitmentId = string.Join(
            ":",
            "teacher-crop",
            selected.SeedId,
            ReadStateInt(snapshot, "time", "total_days"),
            request.DecisionId);

        return new MoneyRevenueCropProductionDecisionRequest
        {
            SchemaVersion =
                "money_revenue_crop_production_decision_request.v2",
            SnapshotPath = snapshotPath,
            BaseStrategyLedgerPath = string.IsNullOrWhiteSpace(
                request.BaseStrategyLedgerPath)
                    ? string.Empty
                    : RequiredFile(
                        request.BaseStrategyLedgerPath,
                        "Money Revenue crop-production Teacher base ledger"),
            DecisionId = request.DecisionId,
            DecisionRecordedAt = request.DecisionRecordedAt,
            GoalId = "earn_money",
            DeadlineTotalDayExclusive = 224,
            TargetTotalMoneyEarned = 1_000_000,
            Commitments = new[]
            {
                new MoneyRevenueCropProductionDecisionCommitment
                {
                    CommitmentId = commitmentId,
                    SeedId = selected.SeedId,
                    TileCount = selected.TileCount,
                    PlantingYear = year,
                    PlantingSeason = season,
                    PlantingDayOfMonth = day,
                    LocationContext = "outdoor_seasonal",
                    LocationId = "Farm",
                    Tiles = tiles
                }
            },
            MaxQueueItemCount = request.MaxQueueItemCount,
            FormalProductTrainingAuthorized = false,
            TeacherRequestPath = fullRequestPath,
            TeacherRequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            TeacherLayoutSnapshots = layouts.Select(value => value.Binding)
                .OrderBy(value => value.MinY)
                .ThenBy(value => value.MinX)
                .ToArray(),
            TeacherCandidateEvaluations = selectedEvaluations,
            TeacherSelectionPolicy = SelectionPolicy
        };
    }

    private static void ValidateRequest(
        MoneyRevenueCropProductionTeacherRequest request)
    {
        Require(request.SchemaVersion ==
                    "money_revenue_crop_production_teacher_request.v1" &&
                !string.IsNullOrWhiteSpace(request.DecisionId) &&
                request.FarmLayoutSnapshotPaths is { Length: > 0 } &&
                request.FarmLayoutSnapshotPaths.All(value =>
                    !string.IsNullOrWhiteSpace(value)) &&
                request.FarmLayoutSnapshotPaths
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    request.FarmLayoutSnapshotPaths.Length &&
                request.MaximumTileCount is >= 1 and <= 64 &&
                request.MaxQueueItemCount is >= 1 and <= 8 &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop-production Teacher request is invalid.");
        Require(DateTimeOffset.TryParse(
                    request.DecisionRecordedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var recordedAt) &&
                recordedAt.Offset == TimeSpan.Zero,
            "Money Revenue crop-production Teacher timestamp must be explicit UTC.");
    }

    private static void ValidateSnapshot(SnapshotEnvelope snapshot)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
                !string.IsNullOrWhiteSpace(snapshot.SaveId.Value) &&
                !string.IsNullOrWhiteSpace(snapshot.PlayerId.Value) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop-production Teacher snapshot identity is invalid.");
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
