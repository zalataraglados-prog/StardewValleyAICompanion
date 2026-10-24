using System.Text.Json;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSitePreparationPlanBuilder
{
    public static MoneyRevenueCropSitePreparationPlan Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop site-preparation plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSitePreparationPlanRequest>(
                fullRequestPath,
                "Money Revenue crop site-preparation plan request");
        Require(request.SchemaVersion is
                    "money_revenue_crop_site_preparation_plan_request.v1" or
                    "money_revenue_crop_site_preparation_plan_request.v2" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                !string.IsNullOrWhiteSpace(request.LocationId) &&
                request.Tiles is not null &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop site-preparation plan request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var productionPlan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            productionPlan.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(productionPlan, rebuiltPlan) &&
                !productionPlan.FutureCropProductionProofReady &&
                !productionPlan.FreshSaveRevenueScheduleReady &&
                !productionPlan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");
        var commitment = productionPlan.Commitments.SingleOrDefault(value =>
            value.CommitmentId == request.CommitmentId);
        Require(commitment is not null,
            "Money Revenue crop site-preparation commitment is not in the plan.");

        var sourceSnapshot = ReadSnapshot(
            productionPlan.SnapshotPath,
            "Money Revenue crop-production source snapshot");
        Require(CurrentTeacherFrontierSupport.HashFile(
                    productionPlan.SnapshotPath) ==
                    productionPlan.SnapshotSha256 &&
                sourceSnapshot.StateHash == productionPlan.SourceStateHash,
            "Money Revenue crop-production source snapshot drifted.");
        var preparationSnapshotPath = RequiredFile(
            request.PreparationSnapshotPath,
            "Money Revenue crop site-preparation snapshot");
        var preparationSnapshot = ReadSnapshot(
            preparationSnapshotPath,
            "Money Revenue crop site-preparation snapshot");
        Require(SameActor(sourceSnapshot, preparationSnapshot),
            "Money Revenue crop site-preparation snapshot changed save or player identity.");
        var preparationTotalDay = ReadStateInt(
            preparationSnapshot,
            "time",
            "total_days");
        Require(preparationTotalDay <= commitment!.PlantingTotalDay &&
                ReadStateString(preparationSnapshot, "player", "location_id") ==
                    request.LocationId &&
                string.Equals(request.LocationId, "Farm", StringComparison.Ordinal),
            "Money Revenue crop site preparation is outside its Farm/date boundary.");

        var requestedTiles = (request.Tiles ??
                throw new InvalidDataException(
                    "Money Revenue crop site-preparation tiles are missing."))
            .OrderBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .ToArray();
        var refillPreparationTiles =
            request.WateringRefillPreparationTiles ??
            Array.Empty<MoneyRevenueCropPlantingTile>();
        Require(request.SchemaVersion.EndsWith(".v2", StringComparison.Ordinal) ||
                refillPreparationTiles.Length == 0,
            "Money Revenue crop site-preparation v1 request cannot add refill preparation tiles.");
        var allPreparationTiles = requestedTiles
            .Concat(refillPreparationTiles)
            .OrderBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .ToArray();
        Require(requestedTiles.Length == commitment.TileCount &&
                requestedTiles.Distinct().Count() == requestedTiles.Length &&
                refillPreparationTiles.Length <=
                    MoneyRevenueCropProductionTeacherDecisionBuilder
                        .MaxWateringRefillPreparationTileCount &&
                refillPreparationTiles.Distinct().Count() ==
                    refillPreparationTiles.Length &&
                allPreparationTiles.Distinct().Count() ==
                    allPreparationTiles.Length &&
                allPreparationTiles.All(value =>
                    value.TileX >= 0 && value.TileY >= 0),
            "Money Revenue crop site-preparation layout does not contain the exact unique tile count.");
        var context = ReadStateValue(
            preparationSnapshot,
            "current_location",
            "site_preparation_context");
        Require(context.ValueKind == JsonValueKind.Object &&
                ReadString(context, "projection_status") ==
                    "complete_bounded_current_farm_region" &&
                ReadString(context, "location_id") == request.LocationId,
            "Money Revenue crop site-preparation context is not a complete bounded Farm region.");
        Require(context.TryGetProperty("tiles", out var contextTiles) &&
                contextTiles.ValueKind == JsonValueKind.Array,
            "Money Revenue crop site-preparation context has no bounded tile rows.");

        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var availability = evaluator.Evaluate(
            preparationSnapshot,
            new[]
            {
                "executor.clear_obstacle",
                "executor.break_farm_resource_clump",
                "executor.till_soil"
            },
            includeExecutorCalibrationOptions: true);
        var clearCandidates = Candidates(
            availability.Options,
            "executor.clear_obstacle");
        var clumpCandidates = Candidates(
            availability.Options,
            "executor.break_farm_resource_clump");
        var tillCandidates = Candidates(
            availability.Options,
            "executor.till_soil");
        var steps = allPreparationTiles.Select(tile => BuildStep(
                tile,
                contextTiles,
                clearCandidates,
                clumpCandidates,
                tillCandidates))
            .ToArray();
        var blockedCount = steps.Count(step => step.PlanStatus == "blocked");
        return new MoneyRevenueCropSitePreparationPlan
        {
            Status = blockedCount == 0
                ? "site_preparation_plan_ready"
                : "site_preparation_plan_blocked",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = CurrentTeacherFrontierSupport.HashFile(
                planPath),
            CommitmentId = commitment.CommitmentId,
            PreparationSnapshotPath = preparationSnapshotPath,
            PreparationSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                preparationSnapshotPath),
            SourceStateHash = sourceSnapshot.StateHash,
            PreparationStateHash = preparationSnapshot.StateHash,
            PreparationTotalDay = preparationTotalDay,
            PlantingTotalDay = commitment.PlantingTotalDay,
            LocationId = request.LocationId,
            TileCount = steps.Length,
            PlantingTileCount = requestedTiles.Length,
            WateringRefillPreparationTileCount =
                refillPreparationTiles.Length,
            AlreadyPreparedCount = steps.Count(step =>
                step.PlanStatus == "already_prepared"),
            DirectTillCount = steps.Count(step =>
                step.PlanStatus == "direct_till_ready"),
            ClearThenRebindCount = steps.Count(step =>
                step.PlanStatus == "clear_then_fresh_snapshot_rebind"),
            BlockedCount = blockedCount,
            Steps = steps,
            SitePreparationPlanReady = blockedCount == 0,
            PlantingLayoutProofStillRequired = true,
            FormalProductTrainingAuthorized = false
        };
    }
}
