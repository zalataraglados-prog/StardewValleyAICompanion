using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    public static AcquisitionRouteTargetDateResourceReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string targetDateFestivalPath,
        string targetDateLocationPath,
        string targetDateFacilityPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateFacilityPath);
        var staticPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var timingFullPath = Path.GetFullPath(routeTimingCalibrationPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateFacilityReport>(
            sourcePath,
            "Acquisition route target-date facility capacity");
        var recomputed = AcquisitionRouteTargetDateFacilityBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            snapshotFullPath,
            timingFullPath);
        Require(EqualJson(source, recomputed),
            "Target-date facility capacity drifted from deterministic source compilation.");
        ValidateSource(source);

        var staticSource = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            staticPath,
            "Acquisition route static calendar resolution");
        var staticRoutes = ValidateStaticSource(staticSource, source);
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
            "Target-date facility capacity and snapshot state hash disagree.");
        var state = AcquisitionResourceInputSnapshotState.Read(snapshot);
        var routes = source.Routes.Select(route => Evaluate(
                route,
                staticRoutes[route.RouteOccurrenceId],
                state))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.ResourceInputAxisStatus ==
                "blocked_upstream_facility_capacity_axis");
        var blockedResource = routes.Count(route =>
            route.ResourceInputAxisStatus ==
                "blocked_resource_input_evidence");
        return new AcquisitionRouteTargetDateResourceReport
        {
            Status = blockedUpstream == 0 && blockedResource == 0
                ? "complete_target_date_resource_inputs_axis_downstream_pending"
                : "partial_target_date_resource_inputs_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateFacilitySha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            ResourceInputAxisResolvedCount = routes.Count(route =>
                route.ResourceInputAxisResolved),
            ResourceInputMatchCount = routes.Count(route =>
                route.ResourceInputsMatchTargetDate == true),
            ResourceInputMissCount = routes.Count(route =>
                route.ResourceInputsMatchTargetDate == false),
            ResourceInputNotRequiredCount = routes.Count(route =>
                route.ResourceInputAxisStatus ==
                    "resolved_resource_inputs_not_required"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.ResourceInputAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedResourceEvidenceCount = blockedResource,
            RouteOccurrenceInventoryComplete = true,
            ResourceInputAxisResolutionComplete =
                blockedUpstream == 0 && blockedResource == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }
}
