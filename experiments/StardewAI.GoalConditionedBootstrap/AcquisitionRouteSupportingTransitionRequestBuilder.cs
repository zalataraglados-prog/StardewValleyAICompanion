using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    public static AcquisitionRouteSupportingTransitionRequest Build(
        AcquisitionRouteSupportingTransitionRequestInputs inputs)
    {
        var processingPath = Path.GetFullPath(inputs.TargetDateProcessingPath);
        var loweringPath = Path.GetFullPath(inputs.AcquisitionLoweringPath);
        var snapshotPath = Path.GetFullPath(inputs.SnapshotPath);
        var ledgerPath = Path.GetFullPath(inputs.StrategyLedgerPath);
        var rankingPath = Path.GetFullPath(inputs.RankingPath);
        var processing = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateProcessingReport>(
            processingPath,
            "Acquisition target-date processing report");
        var recomputed = AcquisitionRouteTargetDateProcessingBuilder.Build(
            inputs.RequirementInventoryPath,
            loweringPath,
            inputs.MasterAnglerWindowsPath,
            inputs.CalendarResolutionPath,
            inputs.TargetDateCalendarPath,
            inputs.TargetDateUnlockPath,
            inputs.TargetDateFestivalPath,
            inputs.TargetDateLocationPath,
            inputs.TargetDateFacilityPath,
            inputs.TargetDateResourcePath,
            inputs.TargetDateCurrencyPath,
            inputs.TargetDateReservationPath,
            ledgerPath,
            snapshotPath,
            inputs.RouteTimingCalibrationPath);
        Require(EqualJson(processing, recomputed),
            "Target-date processing report drifted from deterministic source compilation.");

        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringPath,
            "Acquisition route lowering");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition support snapshot");
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingPath,
            "Acquisition support live ranking");
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshotDocument.RootElement).Ledger;
        var route = ExactlyOne(
            processing.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Target-date processing route occurrence");
        var requirement = RequirementRoute(route.UpstreamRoute);
        var lowered = LoweredRoute(lowering, requirement);
        var artifactReasons = ValidateArtifacts(
            processing,
            snapshot,
            ranking,
            ledger,
            inputs.RouteOccurrenceId);
        var rankedCandidates = CurrentTeacherFrontierSupport
            .ReadCurrentCandidates(ranking);
        var optionIds = lowered.EndpointOptionIds
            .Concat(lowered.SupportingOptionIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var candidates = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                processing.GoalId,
                requirement,
                optionIds,
                rankedCandidates,
                out var candidateReasons);
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(requirement, lowered, candidates);
        return BuildCore(
            processing.GoalId,
            requirement,
            lowered,
            route.UpstreamRoute,
            route,
            snapshot,
            ledger,
            matches,
            inputs.SupportDeadlineTotalDay,
            artifactReasons.Concat(candidateReasons),
            CurrentTeacherFrontierSupport.HashFile(processingPath),
            CurrentTeacherFrontierSupport.HashFile(loweringPath),
            CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            CurrentTeacherFrontierSupport.HashFile(rankingPath));
    }

    private static string[] ValidateArtifacts(
        AcquisitionRouteTargetDateProcessingReport processing,
        SnapshotEnvelope snapshot,
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        StrategyCommitmentLedger ledger,
        string routeOccurrenceId)
    {
        var reasons = new List<string>();
        if (processing.SchemaVersion !=
                "acquisition_route_target_date_processing_lead_time.v1" ||
            !processing.RouteOccurrenceInventoryComplete ||
            processing.TrainingLabelEligible ||
            processing.Routes.Count(value =>
                value.RouteOccurrenceId == routeOccurrenceId) != 1)
        {
            reasons.Add("support_processing_artifact_invalid");
        }
        if (snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State) ||
            processing.SnapshotStateHash != snapshot.StateHash)
        {
            reasons.Add("support_snapshot_identity_mismatch");
        }
        if (ranking.SchemaVersion != "availability_policy_prediction.v1" ||
            ranking.Availability.StateHash != snapshot.StateHash)
        {
            reasons.Add("support_ranking_snapshot_identity_mismatch");
        }
        return reasons.ToArray();
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateReservation route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static AcquisitionRequirementRouteLowering LoweredRoute(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateUnlock requirement) =>
        lowering.RequirementSets.Single(value =>
                value.RequirementSetId == requirement.RequirementSetId)
            .Groups.Single(value =>
                value.RequirementId == requirement.RequirementId)
            .Alternatives[requirement.AlternativeIndex]
            .Routes[requirement.RouteIndex];

    private static T ExactlyOne<T>(
        IEnumerable<T> values,
        Func<T, bool> predicate,
        string label)
    {
        var matches = values.Where(predicate).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(label + " is not unique.");
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
