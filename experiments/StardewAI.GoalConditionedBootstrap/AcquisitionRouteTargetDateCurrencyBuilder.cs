using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateCurrencyBuilder
{
    public static AcquisitionRouteTargetDateCurrencyReport Build(
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
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateResourcePath);
        var loweringFullPath = Path.GetFullPath(loweringPath);
        var staticPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateResourceReport>(
            sourcePath,
            "Acquisition route target-date resource inputs");
        var recomputed = AcquisitionRouteTargetDateResourceBuilder.Build(
            inventoryPath,
            loweringFullPath,
            masterAnglerWindowIndexPath,
            staticPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            targetDateFacilityPath,
            snapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date resource inputs drifted from deterministic source compilation.");
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
        Require(source.SnapshotStateHash == stateHash,
            "Target-date resource inputs and snapshot state hash disagree.");
        var state = new AcquisitionShopQuoteSnapshotState(
            RequiredObject(snapshot, "state"));
        var routes = source.Routes.Select(route => Evaluate(
                route,
                staticRoutes[route.RouteOccurrenceId],
                state))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.CurrencyAxisStatus ==
                "blocked_upstream_resource_input_axis");
        var blockedCurrency = routes.Count(route =>
            route.CurrencyAxisStatus ==
                "blocked_currency_budget_evidence");
        return new AcquisitionRouteTargetDateCurrencyReport
        {
            Status = blockedUpstream == 0 && blockedCurrency == 0
                ? "complete_target_date_currency_budget_axis_downstream_pending"
                : "partial_target_date_currency_budget_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateResourceSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticPath),
            AcquisitionLoweringSha256 =
                CurrentTeacherFrontierSupport.HashFile(loweringFullPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            CurrencyAxisResolvedCount = routes.Count(route =>
                route.CurrencyAxisResolved),
            CurrencyBudgetMatchCount = routes.Count(route =>
                route.CurrencyBudgetMatchesTargetDate == true),
            CurrencyBudgetMissCount = routes.Count(route =>
                route.CurrencyBudgetMatchesTargetDate == false),
            CurrencyNotRequiredCount = routes.Count(route =>
                route.CurrencyAxisStatus == "resolved_currency_not_required"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.CurrencyAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedCurrencyEvidenceCount = blockedCurrency,
            RouteOccurrenceInventoryComplete = true,
            CurrencyAxisResolutionComplete =
                blockedUpstream == 0 && blockedCurrency == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }
}
