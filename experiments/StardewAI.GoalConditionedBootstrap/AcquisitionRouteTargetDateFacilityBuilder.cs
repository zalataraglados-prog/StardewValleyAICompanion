using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    public static AcquisitionRouteTargetDateFacilityReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string targetDateFestivalPath,
        string targetDateLocationPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateLocationPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var timingFullPath = Path.GetFullPath(routeTimingCalibrationPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateLocationReport>(
            sourcePath,
            "Acquisition route target-date location route");
        var recomputed = AcquisitionRouteTargetDateLocationBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticCalendarResolutionPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            snapshotFullPath,
            timingFullPath);
        Require(EqualJson(source, recomputed),
            "Target-date location route drifted from deterministic source compilation.");
        ValidateSource(source);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        Require(string.Equals(
                source.SnapshotStateHash,
                stateHash,
                StringComparison.Ordinal),
            "Target-date location route and snapshot state hash disagree.");
        var state = AcquisitionLocationRouteSnapshotState.Read(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay,
            timingFullPath);
        var routes = source.Routes
            .Select(route => Evaluate(route, state))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.FacilityCapacityAxisStatus ==
                "blocked_upstream_location_route_axis");
        var blockedFacility = routes.Count(route =>
            route.FacilityCapacityAxisStatus ==
                "blocked_facility_capacity_evidence");
        return new AcquisitionRouteTargetDateFacilityReport
        {
            Status = blockedUpstream == 0 && blockedFacility == 0
                ? "complete_target_date_facility_capacity_axis_downstream_pending"
                : "partial_target_date_facility_capacity_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateLocationSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            FacilityCapacityAxisResolvedCount = routes.Count(route =>
                route.FacilityCapacityAxisResolved),
            FacilityCapacityMatchCount = routes.Count(route =>
                route.FacilityCapacityMatchesTargetDate == true),
            FacilityCapacityMissCount = routes.Count(route =>
                route.FacilityCapacityMatchesTargetDate == false),
            FacilityCapacityNotRequiredCount = routes.Count(route =>
                route.FacilityCapacityAxisStatus ==
                    "resolved_facility_capacity_not_required"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.FacilityCapacityAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedFacilityEvidenceCount = blockedFacility,
            RouteOccurrenceInventoryComplete = true,
            FacilityCapacityAxisResolutionComplete =
                blockedUpstream == 0 && blockedFacility == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }
}
