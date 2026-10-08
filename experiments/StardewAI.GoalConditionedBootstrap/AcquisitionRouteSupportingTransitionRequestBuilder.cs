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
        var calendar = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            Path.GetFullPath(inputs.CalendarResolutionPath),
            "Acquisition route calendar resolution");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition support snapshot");
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingPath,
            "Acquisition support live ranking");
        using var snapshotDocument = JsonDocument.Parse(
            CurrentTeacherFrontierSupport.ReadArtifactText(snapshotPath));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshotDocument.RootElement).Ledger;
        var route = ExactlyOne(
            processing.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Target-date processing route occurrence");
        var requirement = RequirementRoute(route.UpstreamRoute);
        var lowered = LoweredRoute(lowering, requirement);
        var staticRoute = ExactlyOne(
            calendar.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Static calendar route occurrence");
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
        var rankedOptionIds = optionIds.Where(optionId =>
                optionId != "inventory.transfer_item" &&
                optionId !=
                    "farm.establish_supported_machine_capacity")
            .ToArray();
        var candidates = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                processing.GoalId,
                requirement,
                rankedOptionIds,
                rankedCandidates,
                out var candidateReasons);
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(requirement, lowered, candidates);
        AcquisitionMachineCapacitySupportBinding? capacityBinding = null;
        var capacityReasons = Array.Empty<string>();
        if (MissingPlacedMachineProven(route.UpstreamRoute))
        {
            var capacityMatches = AcquisitionMachineCapacitySupport
                .BuildCandidates(
                    snapshot,
                    ledger,
                    processing.GoalId,
                    requirement,
                    route.UpstreamRoute,
                    staticRoute,
                    lowered,
                    string.Empty,
                    out capacityBinding,
                    out capacityReasons);
            matches = capacityMatches.Concat(matches).ToArray();
        }
        var purchase = PurchasePrerequisite(route);
        if (purchase is not null)
        {
            matches = matches.Where(match =>
                    PurchaseCandidateMatchesBinding(
                        match.Candidate,
                        purchase))
                .ToArray();
        }
        var stagingReasons = Array.Empty<string>();
        if (matches.Length == 0 && requirement.RouteKind is (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output"))
        {
            var stagingCandidates =
                AcquisitionMachineInputMaterialStaging.BuildCandidates(
                snapshot,
                route.UpstreamRoute,
                out stagingReasons);
            matches = AcquisitionRouteDispatchCompilationBuilder
                .SelectSupportingCandidates(
                    requirement,
                    lowered,
                    stagingCandidates);
        }
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
            artifactReasons.Concat(candidateReasons)
                .Concat(stagingReasons)
                .Concat(capacityReasons),
            CurrentTeacherFrontierSupport.HashFile(processingPath),
            CurrentTeacherFrontierSupport.HashFile(loweringPath),
            CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            CurrentTeacherFrontierSupport.HashFile(rankingPath),
            capacityBinding);
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

    private static bool MissingPlacedMachineProven(
        AcquisitionRouteTargetDateReservation reservation)
    {
        var location = reservation.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
        return location.LocationRouteAxisResolved &&
            location.LocationRouteMatchesTargetDate == false &&
            location.LocationRouteAxisStatus == "resolved_location_route_miss" &&
            location.BlockingReasons.Length == 0 &&
            location.NonMatchingReasons.SequenceEqual(
                new[] { "matching_machine_runtime_location_not_present" },
                StringComparer.Ordinal);
    }


}
