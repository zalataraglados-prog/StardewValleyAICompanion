using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    public static AcquisitionRouteTargetDateReservationReport Build(
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
        string strategyLedgerPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateCurrencyPath);
        var ledgerPath = Path.GetFullPath(strategyLedgerPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateCurrencyReport>(
            sourcePath,
            "Acquisition route target-date currency budget");
        var recomputed = AcquisitionRouteTargetDateCurrencyBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticCalendarResolutionPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            targetDateFacilityPath,
            targetDateResourcePath,
            snapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date currency budget drifted from deterministic source compilation.");
        ValidateSource(source);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        Require(source.SnapshotStateHash == stateHash,
            "Target-date currency budget and snapshot state hash disagree.");
        var ledgerState = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshot);
        var resources = AcquisitionResourceInputSnapshotState.Read(snapshot);
        var currencies = new AcquisitionShopQuoteSnapshotState(
            RequiredObject(snapshot, "state"));
        AcquisitionReservationSupplyValidator.Validate(
            ledgerState,
            resources,
            currencies);
        var routes = source.Routes.Select(route => Evaluate(
                route,
                source.GoalId,
                stateHash,
                ledgerState,
                resources,
                currencies))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.ReservationAxisStatus ==
                "blocked_upstream_currency_budget_axis");
        var blockedReservation = routes.Count(route =>
            route.ReservationAxisStatus ==
                "blocked_inventory_reservation_evidence");
        return new AcquisitionRouteTargetDateReservationReport
        {
            Status = blockedUpstream == 0 && blockedReservation == 0
                ? "complete_target_date_inventory_reservation_axis_downstream_pending"
                : "partial_target_date_inventory_reservation_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateCurrencySha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            StrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            StrategyLedgerId = ledgerState.Ledger.LedgerId,
            StrategyLedgerRevision = ledgerState.Ledger.Revision,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            ReservationAxisResolvedCount = routes.Count(route =>
                route.ReservationAxisResolved),
            ReservationMatchCount = routes.Count(route =>
                route.InventoryReservationMatchesTargetDate == true),
            ReservationConflictCount = routes.Count(route =>
                route.InventoryReservationMatchesTargetDate == false),
            ReservationNotRequiredCount = routes.Count(route =>
                route.ClaimDisposition == "not_required"),
            ClaimProposedCount = routes.Count(route =>
                route.ClaimDisposition == "claim_proposed"),
            ClaimCommittedCount = routes.Count(route =>
                route.ClaimDisposition == "claim_already_committed"),
            ClaimReplacementCount = routes.Count(route =>
                route.ClaimDisposition == "claim_replacement_required"),
            MaterialClaimCount = routes.Sum(route =>
                route.ClaimSet?.MaterialClaims.Length ?? 0),
            CurrencyClaimCount = routes.Sum(route =>
                route.ClaimSet?.CurrencyClaims.Length ?? 0),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.ReservationAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedReservationEvidenceCount = blockedReservation,
            RouteOccurrenceInventoryComplete = true,
            ReservationAxisResolutionComplete =
                blockedUpstream == 0 && blockedReservation == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }
}
