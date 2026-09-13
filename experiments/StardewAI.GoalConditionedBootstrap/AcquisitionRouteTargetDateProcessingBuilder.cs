using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    public static AcquisitionRouteTargetDateProcessingReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string targetDateFestivalPath,
        string targetDateLocationPath,
        string targetDateFacilityPath,
        string targetDateResourcePath,
        string targetDateCurrencyPath,
        string targetDateReservationPath,
        string strategyLedgerPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateReservationPath);
        var staticPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateReservationReport>(
            sourcePath,
            "Acquisition route target-date inventory reservation");
        var recomputed = AcquisitionRouteTargetDateReservationBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            targetDateFacilityPath,
            targetDateResourcePath,
            targetDateCurrencyPath,
            strategyLedgerPath,
            snapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date inventory reservation drifted from deterministic source compilation.");
        ValidateSource(source);

        var staticSource = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            staticPath,
            "Acquisition route static calendar resolution");
        ValidateStaticSource(staticSource, source);
        var staticRoutes = staticSource.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        Require(source.SnapshotStateHash == stateHash,
            "Target-date inventory reservation and snapshot state hash disagree.");
        var state = AcquisitionProcessingLeadTimeSnapshotState.Read(snapshot);
        var routes = source.Routes.Select(route => Evaluate(
                route,
                staticRoutes[route.RouteOccurrenceId],
                state,
                source.TargetTotalDay))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.ProcessingLeadTimeAxisStatus ==
                "blocked_upstream_inventory_reservation_axis");
        var blockedEvidence = routes.Count(route =>
            route.ProcessingLeadTimeAxisStatus ==
                "blocked_processing_lead_time_evidence");
        return new AcquisitionRouteTargetDateProcessingReport
        {
            Status = blockedUpstream == 0 && blockedEvidence == 0
                ? "complete_target_date_processing_lead_time_axis_downstream_pending"
                : "partial_target_date_processing_lead_time_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateInventoryReservationSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            ProcessingLeadTimeAxisResolvedCount = routes.Count(route =>
                route.ProcessingLeadTimeAxisResolved),
            ProcessingLeadTimeMatchCount = routes.Count(route =>
                route.ProcessingLeadTimeMatchesTargetDate == true),
            ProcessingLeadTimeMissCount = routes.Count(route =>
                route.ProcessingLeadTimeMatchesTargetDate == false),
            ProcessingLeadTimeNotRequiredCount = routes.Count(route =>
                route.ProcessingLeadTimeRequirementKind ==
                    NoDeterministicWait),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.ProcessingLeadTimeAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedProcessingEvidenceCount = blockedEvidence,
            RouteOccurrenceInventoryComplete = true,
            ProcessingLeadTimeAxisResolutionComplete =
                blockedUpstream == 0 && blockedEvidence == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }
}
