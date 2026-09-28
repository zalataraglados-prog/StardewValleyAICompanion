using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private TargetDateAxes VerifyTargetDateAxes(
            AcquisitionRouteCalendarResolutionReport routeCalendar)
        {
            var targetDateCalendar = AcquisitionRouteTargetDateCalendarBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                0);
            Write(targetDateCalendarPath, targetDateCalendar);
            var targetDateShop = targetDateCalendar.Routes.Single(route =>
                route.RequirementId == "community_center:bundle:Pantry/5" &&
                route.RouteKind == "sells");
            Require(targetDateCalendar.Status ==
                        "complete_target_date_calendar_axis_downstream_pending" &&
                    targetDateCalendar.RouteOccurrenceInventoryComplete &&
                    targetDateCalendar.CalendarAxisResolutionComplete &&
                    !targetDateCalendar.TrainingLabelEligible &&
                    targetDateCalendar.TargetTotalDay == 0 &&
                    targetDateCalendar.RouteOccurrenceCount == 77 &&
                    targetDateCalendar.CalendarAxisResolvedCount == 77 &&
                    targetDateCalendar.StaticWindowMatchCount == 5 &&
                    targetDateCalendar.StaticWindowMissCount == 72 &&
                    targetDateCalendar.BlockedStaticSourceCount == 0 &&
                    targetDateCalendar.StaticCalendarResolutionSha256 ==
                        HashFile(routeCalendarPath) &&
                    targetDateShop.CalendarAxisResolved &&
                    targetDateShop.MatchKind == "item_id" &&
                    targetDateShop.RequiredAmount == 2 &&
                    targetDateShop.MinimumQuality == 1 &&
                    targetDateShop.StaticWindowMatchesTargetDate &&
                    targetDateShop.CalendarAxisStatus ==
                        "resolved_target_date_inside_static_window_downstream_pending" &&
                    targetDateShop.MatchingWindows.Length == 1 &&
                    targetDateShop.MatchingWindows[0].TimeWindows.Length == 1 &&
                    targetDateShop.MatchingWindows[0].TimeWindows[0].StartTime == 700 &&
                    targetDateShop.MatchingWindows[0].TimeWindows[0].EndTime == 1810 &&
                    targetDateShop.PendingDynamicConditions.SequenceEqual(new[]
                    {
                        "!IS_FESTIVAL_DAY",
                        "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY",
                        "DAYS_PLAYED 1 1",
                        "IS_ISLAND_NORTH_BRIDGE_FIXED",
                        "IS_PASSIVE_FESTIVAL_OPEN FixtureFest",
                        "PLAYER_HAS_MAIL Current fixtureGate",
                        "PLAYER_HAS_MAIL Host hostGate",
                        "PLAYER_SPECIAL_ORDER_ACTIVE Current Gunther",
                        "PLAYER_STAT Current Book_Woodcutting 1",
                        "SYNCED_RANDOM day fixture 0.25"
                    }),
                "Explicit target-date calendar-axis resolution drifted.");

            WriteStageOneCollectionSnapshot(
                targetDateUnlockSnapshotPath,
                "target-date-unlock-state",
                fish,
                totalDays: 0,
                timeOfDay: 700);
            var targetDateUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockSnapshotPath);
            Write(targetDateUnlockPath, targetDateUnlock);
            var targetDateUnlockShop = targetDateUnlock.Routes.Single(route =>
                route.RequirementId == "community_center:bundle:Pantry/5" &&
                route.RouteKind == "sells");
            Require(targetDateUnlock.Status ==
                        "complete_target_date_unlock_axis_downstream_pending" &&
                    targetDateUnlock.RouteOccurrenceInventoryComplete &&
                    targetDateUnlock.UnlockAxisResolutionComplete &&
                    !targetDateUnlock.TrainingLabelEligible &&
                    targetDateUnlock.TargetTotalDay == 0 &&
                    targetDateUnlock.RouteOccurrenceCount == 77 &&
                    targetDateUnlock.UnlockAxisResolvedCount == 77 &&
                    targetDateUnlock.UnlockStateMatchCount == 5 &&
                    targetDateUnlock.UnlockStateMissCount == 0 &&
                    targetDateUnlock.StaticWindowMissCount == 72 &&
                    targetDateUnlock.BlockedUpstreamCalendarCount == 0 &&
                    targetDateUnlock.BlockedUnlockEvidenceCount == 0 &&
                    targetDateUnlock.PendingCalendarConditionCount == 3 &&
                    targetDateUnlock.PendingStochasticConditionCount == 1 &&
                    targetDateUnlock.UnsupportedConditionCount == 0 &&
                    targetDateUnlockShop.UnlockAxisResolved &&
                    targetDateUnlockShop.MatchKind == "item_id" &&
                    targetDateUnlockShop.RequiredAmount == 2 &&
                    targetDateUnlockShop.MinimumQuality == 1 &&
                    targetDateUnlockShop.UnlockStateMatchesTargetDate == true &&
                    targetDateUnlockShop.SourceResolutionStatus ==
                        "resolved_static_source_window_target_date_pending" &&
                    targetDateUnlockShop.MatchingWindows.Length == 1 &&
                    targetDateUnlockShop.MatchingWindows[0].TimeWindows.Length == 1 &&
                    targetDateUnlockShop.MatchingWindows[0].TimeWindows[0]
                        .StartTime == 700 &&
                    targetDateUnlockShop.MatchingWindows[0].TimeWindows[0]
                        .EndTime == 1810 &&
                    targetDateUnlockShop.UnlockConditions.Length == 6 &&
                    targetDateUnlockShop.UnlockConditions.All(value =>
                        value.Status == "resolved_match" &&
                        value.ConditionMatches == true) &&
                    targetDateUnlockShop.PendingStochasticConditions
                        .SequenceEqual(new[]
                        {
                            "SYNCED_RANDOM day fixture 0.25"
                        }),
                "Explicit target-date unlock-state axis resolution drifted.");

            using (var unlockSnapshot = JsonDocument.Parse(
                       File.ReadAllText(targetDateUnlockSnapshotPath)))
            {
                var evaluator = new AcquisitionUnlockConditionEvaluator(
                    unlockSnapshot.RootElement);
                Require(
                    evaluator.Evaluate("PLAYER_HAS_MAIL Any anyGate")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL All allGate")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL 200 hostGate")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL Current pendingGate")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL Current pendingGate Tomorrow")
                        .ConditionMatches == false &&
                    evaluator.Evaluate("PLAYER_STAT Host Book_Woodcutting 0 0")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_STAT Current Book_Woodcutting 2")
                        .ConditionMatches == false &&
                    evaluator.Evaluate(
                            "PLAYER_HAS_MAIL Current fixtureGate Received ignored")
                        .ConditionMatches == true &&
                    evaluator.Evaluate(
                            "PLAYER_STAT Current Book_Woodcutting 1 1 ignored")
                        .ConditionMatches == true &&
                    evaluator.Evaluate(
                            "IS_ISLAND_NORTH_BRIDGE_FIXED ignored")
                        .ConditionMatches == true &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL Target fixtureGate")
                        .Status == "blocked" &&
                    evaluator.Evaluate("PLAYER_HAS_MAIL Target fixtureGate")
                        .BlockingReason == "target_player_context_unavailable" &&
                    evaluator.Evaluate("!!PLAYER_HAS_MAIL Current fixtureGate")
                        .Status == "blocked",
                    "Native Current/Host/Any/All/numeric player selection drifted.");

                var calendarState = AcquisitionCalendarSnapshotState.Read(
                    unlockSnapshot.RootElement);
                var calendarEvaluator = new AcquisitionCalendarConditionEvaluator(
                    calendarState);
                Require(
                    calendarEvaluator.Evaluate("DAYS_PLAYED 1 1 ignored")
                        .ConditionMatches == true &&
                    calendarEvaluator.Evaluate("IS_FESTIVAL_DAY")
                        .ConditionMatches == false &&
                    calendarEvaluator.Evaluate("!IS_FESTIVAL_DAY")
                        .ConditionMatches == true &&
                    calendarEvaluator.Evaluate("IS_FESTIVAL_DAY any 12")
                        .ConditionMatches == true &&
                    calendarEvaluator.Evaluate("IS_FESTIVAL_DAY Here")
                        .Status == "blocked" &&
                    calendarEvaluator.Evaluate(
                            "IS_PASSIVE_FESTIVAL_OPEN FixtureFest ignored")
                        .ConditionMatches == true &&
                    calendarEvaluator.Evaluate(
                            "IS_PASSIVE_FESTIVAL_OPEN MissingFestival")
                        .ConditionMatches == false &&
                    calendarEvaluator.Evaluate("!!IS_FESTIVAL_DAY")
                        .Status == "blocked",
                    "Native target-date calendar condition semantics drifted.");
            }

            var mismatchedCalendarTimeSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            mismatchedCalendarTimeSnapshot["state"]!["world_progress"]!
                ["game_state_query_calendar_state"]!["value"]!["time_of_day"] = 600;
            File.WriteAllText(
                mismatchedCalendarTimeSnapshotPath,
                mismatchedCalendarTimeSnapshot.ToJsonString(JsonDefaults.Options));
            var mismatchedCalendarTimeRejected = false;
            try
            {
                using var mismatchedCalendarTimeDocument = JsonDocument.Parse(
                    File.ReadAllText(mismatchedCalendarTimeSnapshotPath));
                _ = AcquisitionCalendarSnapshotState.Read(
                    mismatchedCalendarTimeDocument.RootElement);
            }
            catch (InvalidDataException)
            {
                mismatchedCalendarTimeRejected = true;
            }
            Require(mismatchedCalendarTimeRejected,
                "Calendar bridge time drift did not fail closed.");

            var targetDateFestival =
                AcquisitionRouteTargetDateFestivalBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateUnlockSnapshotPath);
            Write(targetDateFestivalPath, targetDateFestival);
            var targetDateFestivalShop = targetDateFestival.Routes.Single(route =>
                route.UpstreamRoute.RequirementId ==
                    "community_center:bundle:Pantry/5" &&
                route.UpstreamRoute.RouteKind == "sells");
            var sourceUnlockShopJson = JsonSerializer.Serialize(
                targetDateUnlockShop,
                JsonDefaults.Options);
            var carriedUnlockShopJson = JsonSerializer.Serialize(
                targetDateFestivalShop.UpstreamRoute,
                JsonDefaults.Options);
            Require(targetDateFestival.Status ==
                        "complete_target_date_festival_axis_downstream_pending" &&
                    targetDateFestival.RouteOccurrenceInventoryComplete &&
                    targetDateFestival.CalendarConditionAxisResolutionComplete &&
                    !targetDateFestival.TrainingLabelEligible &&
                    targetDateFestival.TargetTotalDay == 0 &&
                    targetDateFestival.RouteOccurrenceCount == 77 &&
                    targetDateFestival.CalendarConditionAxisResolvedCount == 77 &&
                    targetDateFestival.CalendarConditionMatchCount == 5 &&
                    targetDateFestival.CalendarConditionMissCount == 0 &&
                    targetDateFestival.NotApplicableStaticWindowCount == 72 &&
                    targetDateFestival.NotApplicableUnlockStateCount == 0 &&
                    targetDateFestival.BlockedUpstreamCount == 0 &&
                    targetDateFestival.BlockedCalendarEvidenceCount == 0 &&
                    targetDateFestival.PendingStochasticConditionCount == 1 &&
                    targetDateFestivalShop.CalendarConditionAxisResolved &&
                    targetDateFestivalShop.CalendarConditionsMatchTargetDate == true &&
                    carriedUnlockShopJson == sourceUnlockShopJson &&
                    targetDateFestivalShop.CalendarConditionEvaluations.Length == 3 &&
                    targetDateFestivalShop.CalendarConditionEvaluations.All(value =>
                        value.Status == "resolved_match" &&
                        value.ConditionMatches == true),
                "Explicit target-date festival-state axis resolution drifted.");

            Write(
                targetDateRouteCalibrationPath,
                StageOneRouteTimingCalibration(totalDays: 0));
            var targetDateLocation =
                AcquisitionRouteTargetDateLocationBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            Write(targetDateLocationPath, targetDateLocation);
            var targetDateLocationShop = targetDateLocation.Routes.Single(route =>
                route.UpstreamRoute.UpstreamRoute.RequirementId ==
                    "community_center:bundle:Pantry/5" &&
                route.UpstreamRoute.UpstreamRoute.RouteKind == "sells");
            var sourceFestivalShopJson = JsonSerializer.Serialize(
                targetDateFestivalShop,
                JsonDefaults.Options);
            var carriedFestivalShopJson = JsonSerializer.Serialize(
                targetDateLocationShop.UpstreamRoute,
                JsonDefaults.Options);
            Require(targetDateLocation.Status ==
                        "complete_target_date_location_route_axis_downstream_pending" &&
                    targetDateLocation.RouteOccurrenceInventoryComplete &&
                    targetDateLocation.LocationRouteAxisResolutionComplete &&
                    !targetDateLocation.TrainingLabelEligible &&
                    targetDateLocation.TargetTotalDay == 0 &&
                    targetDateLocation.RouteOccurrenceCount == 77 &&
                    targetDateLocation.LocationRouteAxisResolvedCount == 77 &&
                    targetDateLocation.LocationRouteMatchCount == 5 &&
                    targetDateLocation.LocationRouteMissCount == 0 &&
                    targetDateLocation.NotApplicableStaticWindowCount == 72 &&
                    targetDateLocation.NotApplicableUnlockStateCount == 0 &&
                    targetDateLocation.NotApplicableCalendarConditionCount == 0 &&
                    targetDateLocation.BlockedUpstreamCount == 0 &&
                    targetDateLocation.BlockedLocationEvidenceCount == 0 &&
                    carriedFestivalShopJson == sourceFestivalShopJson &&
                    targetDateLocationShop.LocationRouteMatchesTargetDate == true &&
                    targetDateLocationShop.TargetEvaluations.Length == 1 &&
                    targetDateLocationShop.TargetEvaluations[0].TargetLocationId ==
                        "FixtureShop" &&
                    targetDateLocationShop.TargetEvaluations[0]
                        .GuaranteedArrivalByTime == 902 &&
                    targetDateLocationShop.TargetEvaluations[0].Path.Length == 2,
                "Target-date location-route axis resolution drifted.");

            var targetDateFacility =
                AcquisitionRouteTargetDateFacilityBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            Write(targetDateFacilityPath, targetDateFacility);
            var targetDateFacilityShop = targetDateFacility.Routes.Single(route =>
                route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RequirementId ==
                    "community_center:bundle:Pantry/5" &&
                route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind ==
                    "sells");
            var targetDateFacilityCrops = targetDateFacility.Routes.Where(route =>
                    route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind ==
                        "harvests_as" &&
                    route.FacilityCapacityMatchesTargetDate == true)
                .ToArray();
            var sourceLocationShopJson = JsonSerializer.Serialize(
                targetDateLocationShop,
                JsonDefaults.Options);
            var carriedLocationShopJson = JsonSerializer.Serialize(
                targetDateFacilityShop.UpstreamRoute,
                JsonDefaults.Options);
            Require(targetDateFacility.Status ==
                        "complete_target_date_facility_capacity_axis_downstream_pending" &&
                    targetDateFacility.RouteOccurrenceInventoryComplete &&
                    targetDateFacility.FacilityCapacityAxisResolutionComplete &&
                    !targetDateFacility.TrainingLabelEligible &&
                    targetDateFacility.TargetTotalDay == 0 &&
                    targetDateFacility.StaticCalendarResolutionSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(routeCalendarPath) &&
                    targetDateFacility.RouteOccurrenceCount == 77 &&
                    targetDateFacility.FacilityCapacityAxisResolvedCount == 77 &&
                    targetDateFacility.FacilityCapacityMatchCount == 5 &&
                    targetDateFacility.FacilityCapacityMissCount == 0 &&
                    targetDateFacility.FacilityCapacityNotRequiredCount == 2 &&
                    targetDateFacility.NotApplicableUpstreamCount == 72 &&
                    targetDateFacility.BlockedUpstreamCount == 0 &&
                    targetDateFacility.BlockedFacilityEvidenceCount == 0 &&
                    targetDateFacilityCrops.Length == 3 &&
                    targetDateFacilityCrops.All(route =>
                        route.FacilityRequirementKind ==
                            "prepared_cultivation_slot" &&
                        route.TargetEvaluations.Single()
                            .OpenPreparedSoilSlotCount == 2) &&
                    targetDateFacilityCrops.Select(route =>
                            route.TargetEvaluations.Single()
                                .RequiredCropSlotCount)
                        .Order()
                        .SequenceEqual(new int?[] { 1, 1, 2 }) &&
                    targetDateFacilityShop.FacilityCapacityAxisStatus ==
                        "resolved_facility_capacity_not_required" &&
                    carriedLocationShopJson == sourceLocationShopJson,
                "Target-date facility-capacity axis resolution drifted.");

            var tamperedTargetDateLocation = JsonNode.Parse(
                File.ReadAllText(targetDateLocationPath))!.AsObject();
            tamperedTargetDateLocation["routes"]![0]!["upstream_route"]!
                ["upstream_route"]!["source_id"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateLocationPath,
                tamperedTargetDateLocation.ToJsonString(JsonDefaults.Options));
            var tamperedTargetDateLocationRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateFacilityBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    tamperedTargetDateLocationPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            }
            catch (InvalidDataException)
            {
                tamperedTargetDateLocationRejected = true;
            }
            Require(tamperedTargetDateLocationRejected,
                "A tampered target-date location report did not fail closed.");

            var missingFacilitySnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            var missingFacilityFarm = missingFacilitySnapshot["state"]!
                ["locations"]!["social_route_date_evidence"]!["value"]!
                ["locations"]!.AsArray()
                .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!;
            missingFacilityFarm.AsObject().Remove("cultivation_capacity");
            File.WriteAllText(
                missingFacilitySnapshotPath,
                missingFacilitySnapshot.ToJsonString(JsonDefaults.Options));
            var missingFacilityReport = BuildFacilityFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingFacilitySnapshotPath,
                targetDateRouteCalibrationPath,
                missingFacilityUnlockPath,
                missingFacilityFestivalPath,
                missingFacilityLocationPath);
            Require(missingFacilityReport.Status ==
                        "partial_target_date_facility_capacity_axis_blocks" &&
                    missingFacilityReport.RouteOccurrenceInventoryComplete &&
                    !missingFacilityReport.FacilityCapacityAxisResolutionComplete &&
                    missingFacilityReport.FacilityCapacityAxisResolvedCount == 74 &&
                    missingFacilityReport.FacilityCapacityMatchCount == 2 &&
                    missingFacilityReport.FacilityCapacityMissCount == 0 &&
                    missingFacilityReport.FacilityCapacityNotRequiredCount == 2 &&
                    missingFacilityReport.NotApplicableUpstreamCount == 72 &&
                    missingFacilityReport.BlockedFacilityEvidenceCount == 3 &&
                    missingFacilityReport.Routes.Where(route =>
                        route.FacilityCapacityAxisStatus ==
                            "blocked_facility_capacity_evidence").All(route =>
                        route.BlockingReasons.Contains(
                            "Farm:prepared_cultivation_capacity_missing")),
                "Missing prepared-soil capacity did not fail closed per crop route.");

            var zeroFacilitySnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            var zeroFacilityCapacity = zeroFacilitySnapshot["state"]!
                ["locations"]!["social_route_date_evidence"]!["value"]!
                ["locations"]!.AsArray()
                .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
                ["cultivation_capacity"]!.AsObject();
            zeroFacilityCapacity["total_prepared_soil_slot_count"] = 0;
            zeroFacilityCapacity["open_prepared_soil_slot_count"] = 0;
            File.WriteAllText(
                zeroFacilitySnapshotPath,
                zeroFacilitySnapshot.ToJsonString(JsonDefaults.Options));
            var zeroFacilityReport = BuildFacilityFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                zeroFacilitySnapshotPath,
                targetDateRouteCalibrationPath,
                zeroFacilityUnlockPath,
                zeroFacilityFestivalPath,
                zeroFacilityLocationPath);
            Require(zeroFacilityReport.Status ==
                        "complete_target_date_facility_capacity_axis_downstream_pending" &&
                    zeroFacilityReport.FacilityCapacityAxisResolutionComplete &&
                    zeroFacilityReport.FacilityCapacityAxisResolvedCount == 77 &&
                    zeroFacilityReport.FacilityCapacityMatchCount == 2 &&
                    zeroFacilityReport.FacilityCapacityMissCount == 3 &&
                    zeroFacilityReport.FacilityCapacityNotRequiredCount == 2 &&
                    zeroFacilityReport.BlockedFacilityEvidenceCount == 0 &&
                    zeroFacilityReport.Routes.Where(route =>
                        route.FacilityCapacityMatchesTargetDate == false).All(route =>
                        route.FacilityCapacityAxisStatus ==
                            "resolved_prepared_cultivation_capacity_miss" &&
                        route.BlockingReasons.Length == 0),
                "A complete zero-capacity fact was misclassified as missing evidence.");

            zeroFacilityCapacity["total_prepared_soil_slot_count"] = 2;
            zeroFacilityCapacity["occupied_crop_slot_count"] = 2;
            zeroFacilityCapacity["unresolved_harvest_item_slot_count"] = 2;
            zeroFacilityCapacity["occupied_harvest_items"] = new JsonArray(
                new JsonObject
                {
                    ["harvest_item_qualified_id"] = string.Empty,
                    ["is_garden_pot"] = false,
                    ["slot_count"] = 2
                });
            File.WriteAllText(
                zeroFacilitySnapshotPath,
                zeroFacilitySnapshot.ToJsonString(JsonDefaults.Options));
            var unresolvedCropIdentityReport = BuildFacilityFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                zeroFacilitySnapshotPath,
                targetDateRouteCalibrationPath,
                zeroFacilityUnlockPath,
                zeroFacilityFestivalPath,
                zeroFacilityLocationPath);
            Require(unresolvedCropIdentityReport.Status ==
                        "partial_target_date_facility_capacity_axis_blocks" &&
                    unresolvedCropIdentityReport.FacilityCapacityAxisResolvedCount ==
                        74 &&
                    unresolvedCropIdentityReport.FacilityCapacityMatchCount == 2 &&
                    unresolvedCropIdentityReport.BlockedFacilityEvidenceCount == 3 &&
                    unresolvedCropIdentityReport.Routes.Where(route =>
                        route.FacilityCapacityAxisStatus ==
                            "blocked_facility_capacity_evidence").All(route =>
                        route.BlockingReasons.Contains(
                            "Farm:prepared_crop_harvest_identity_unresolved")),
                "An unresolved occupied crop identity was treated as a capacity miss.");

            var targetDateResource =
                AcquisitionRouteTargetDateResourceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            Write(targetDateResourcePath, targetDateResource);
            var targetDateResourceCrop = targetDateResource.Routes.Single(route =>
                route.RouteOccurrenceId ==
                    "full_shipment:full_shipment:item:24:0:0");
            var targetDateResourceShop = targetDateResource.Routes.Single(route =>
                route.RouteOccurrenceId ==
                    "community_center_standard:community_center:bundle:Pantry/5:0:1");
            var targetDateResourceCommunityCrop = targetDateResource.Routes.Single(
                route => route.RouteOccurrenceId ==
                    "community_center_standard:community_center:bundle:Pantry/5:0:0");
            var targetDateResourceFish = targetDateResource.Routes.Single(route =>
                route.RouteOccurrenceId ==
                    "master_angler:master_angler:item:145:0:0");
            var sourceFacilityCrop = targetDateFacility.Routes.Single(route =>
                route.RouteOccurrenceId == targetDateResourceCrop.RouteOccurrenceId);
            Require(targetDateResource.Status ==
                        "complete_target_date_resource_inputs_axis_downstream_pending" &&
                    targetDateResource.RouteOccurrenceInventoryComplete &&
                    targetDateResource.ResourceInputAxisResolutionComplete &&
                    !targetDateResource.TrainingLabelEligible &&
                    targetDateResource.TargetTotalDay == 0 &&
                    targetDateResource.StaticCalendarResolutionSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(routeCalendarPath) &&
                    targetDateResource.RouteOccurrenceCount == 77 &&
                    targetDateResource.ResourceInputAxisResolvedCount == 77 &&
                    targetDateResource.ResourceInputMatchCount == 5 &&
                    targetDateResource.ResourceInputMissCount == 0 &&
                    targetDateResource.ResourceInputNotRequiredCount == 1 &&
                    targetDateResource.NotApplicableUpstreamCount == 72 &&
                    targetDateResource.BlockedUpstreamCount == 0 &&
                    targetDateResource.BlockedResourceEvidenceCount == 0 &&
                    targetDateResourceCrop.ResourceRequirementKind ==
                        "crop_seed_or_existing_crop" &&
                    targetDateResourceCrop.InputEvaluations.Single()
                        .QualifiedItemId == "(O)472" &&
                    targetDateResourceCrop.InputEvaluations.Single()
                        .RequiredQuantity == 1 &&
                    targetDateResourceCrop.InputEvaluations.Single()
                        .AvailableQuantity == 2 &&
                    targetDateResourceCommunityCrop.InputEvaluations.Single()
                        .RequiredQuantity == 2 &&
                    targetDateResourceCommunityCrop.InputEvaluations.Single()
                        .AvailableQuantity == 2 &&
                    targetDateResourceShop.ResourceRequirementKind ==
                        "shop_trade_item_or_currency_only" &&
                    targetDateResourceShop.InputEvaluations.Single()
                        .QualifiedItemId == "(O)388" &&
                    targetDateResourceShop.InputEvaluations.Single()
                        .RequiredQuantity == 10 &&
                    targetDateResourceShop.InputEvaluations.Single()
                        .AvailableQuantity == 10 &&
                    targetDateResourceFish.ResourceInputAxisStatus ==
                        "resolved_resource_inputs_not_required" &&
                    JsonSerializer.Serialize(
                        targetDateResourceCrop.UpstreamRoute,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        sourceFacilityCrop,
                        JsonDefaults.Options),
                "Target-date resource-input axis resolution drifted.");

            var targetDateCurrency =
                AcquisitionRouteTargetDateCurrencyBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            Write(targetDateCurrencyPath, targetDateCurrency);
            var targetDateCurrencyShop = targetDateCurrency.Routes.Single(route =>
                route.RouteOccurrenceId == targetDateResourceShop.RouteOccurrenceId);
            Require(targetDateCurrency.Status ==
                        "complete_target_date_currency_budget_axis_downstream_pending" &&
                    targetDateCurrency.RouteOccurrenceInventoryComplete &&
                    targetDateCurrency.CurrencyAxisResolutionComplete &&
                    !targetDateCurrency.TrainingLabelEligible &&
                    targetDateCurrency.RouteOccurrenceCount == 77 &&
                    targetDateCurrency.CurrencyAxisResolvedCount == 77 &&
                    targetDateCurrency.CurrencyBudgetMatchCount == 5 &&
                    targetDateCurrency.CurrencyBudgetMissCount == 0 &&
                    targetDateCurrency.CurrencyNotRequiredCount == 4 &&
                    targetDateCurrency.NotApplicableUpstreamCount == 72 &&
                    targetDateCurrency.BlockedUpstreamCount == 0 &&
                    targetDateCurrency.BlockedCurrencyEvidenceCount == 0 &&
                    targetDateCurrencyShop.CurrencyRequirementKind ==
                        "current_native_shop_purchase_quote" &&
                    targetDateCurrencyShop.CurrencyEvaluation is not null &&
                    targetDateCurrencyShop.CurrencyEvaluation.CurrencyId == 0 &&
                    targetDateCurrencyShop.CurrencyEvaluation.CurrencyKey ==
                        "money" &&
                    targetDateCurrencyShop.CurrencyEvaluation.RequiredAmount == 200 &&
                    targetDateCurrencyShop.CurrencyEvaluation.AvailableAmount == 500 &&
                    targetDateCurrencyShop.CurrencyEvaluation
                        .OutputStackPerPurchase == 1 &&
                    targetDateCurrencyShop.CurrencyEvaluation.OutputQuality == 1 &&
                    targetDateCurrencyShop.CurrencyEvaluation
                        .RequiredPurchaseCount == 2 &&
                    targetDateCurrencyShop.CurrencyEvaluation.PurchaseQuoteStatus ==
                        "current_native_shop_quote_available" &&
                    targetDateCurrency.StaticCalendarResolutionSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(routeCalendarPath) &&
                    targetDateCurrency.AcquisitionLoweringSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(loweringPath) &&
                    JsonSerializer.Serialize(
                        targetDateCurrencyShop.UpstreamRoute,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        targetDateResourceShop,
                        JsonDefaults.Options),
                "Target-date currency-budget axis resolution drifted.");

            WriteEmptyStrategyLedger(strategyLedgerPath, "target-date-unlock-state");
            AcquisitionRouteTargetDateReservationReport BuildReservation(
                string ledgerPath) =>
                AcquisitionRouteTargetDateReservationBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    ledgerPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            var targetDateReservation = BuildReservation(strategyLedgerPath);
            Write(targetDateReservationPath, targetDateReservation);
            var targetDateReservationShop = targetDateReservation.Routes.Single(
                route => route.RouteOccurrenceId ==
                    targetDateCurrencyShop.RouteOccurrenceId);
            Require(targetDateReservation.Status ==
                        "complete_target_date_inventory_reservation_axis_downstream_pending" &&
                    targetDateReservation.RouteOccurrenceInventoryComplete &&
                    targetDateReservation.ReservationAxisResolutionComplete &&
                    !targetDateReservation.TrainingLabelEligible &&
                    targetDateReservation.RouteOccurrenceCount == 77 &&
                    targetDateReservation.ReservationAxisResolvedCount == 77 &&
                    targetDateReservation.ReservationMatchCount == 5 &&
                    targetDateReservation.ReservationConflictCount == 0 &&
                    targetDateReservation.ReservationNotRequiredCount == 1 &&
                    targetDateReservation.ClaimProposedCount == 4 &&
                    targetDateReservation.ClaimCommittedCount == 0 &&
                    targetDateReservation.ClaimReplacementCount == 0 &&
                    targetDateReservation.MaterialClaimCount == 4 &&
                    targetDateReservation.CurrencyClaimCount == 1 &&
                    targetDateReservation.NotApplicableUpstreamCount == 72 &&
                    targetDateReservation.BlockedUpstreamCount == 0 &&
                    targetDateReservation.BlockedReservationEvidenceCount == 0 &&
                    targetDateReservation.StrategyLedgerRevision == 0 &&
                    targetDateReservation.StrategyLedgerSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(strategyLedgerPath) &&
                    targetDateReservationShop.ClaimDisposition == "claim_proposed" &&
                    targetDateReservationShop.ClaimSet is not null &&
                    targetDateReservationShop.ClaimSet.AtomicCommitRequired &&
                    !targetDateReservationShop.ClaimSet.ReplacementRequired &&
                    targetDateReservationShop.ClaimSet.ExpectedLedgerRevision == 0 &&
                    targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                        .QualifiedItemId == "(O)388" &&
                    targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                        .NodeId == "player:1" &&
                    targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                        .SlotIndex == 1 &&
                    targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                        .Quantity == 10 &&
                    targetDateReservationShop.ClaimSet.CurrencyClaims.Single()
                        .CurrencyId == NativeShopCurrencies.Money &&
                    targetDateReservationShop.ClaimSet.CurrencyClaims.Single()
                        .Amount == 200,
                "Target-date inventory-reservation axis resolution drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateReservation,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildReservation(strategyLedgerPath),
                        JsonDefaults.Options),
                "Target-date reservation claims are not deterministic.");

            AcquisitionRouteTargetDateProcessingReport BuildProcessing(
                string reservationPath,
                string ledgerPath,
                string snapshotPath) =>
                AcquisitionRouteTargetDateProcessingBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    reservationPath,
                    ledgerPath,
                    snapshotPath,
                    targetDateRouteCalibrationPath);
            var targetDateProcessing = BuildProcessing(
                targetDateReservationPath,
                strategyLedgerPath,
                targetDateUnlockSnapshotPath);
            Write(targetDateProcessingPath, targetDateProcessing);
            var targetDateProcessingCrops = targetDateProcessing.Routes.Where(
                    route => route.ProcessingLeadTimeRequirementKind ==
                        "crop_growth_or_ready_crop")
                .ToArray();
            var targetDateProcessingShop = targetDateProcessing.Routes.Single(
                route => route.RouteOccurrenceId ==
                    targetDateCurrencyShop.RouteOccurrenceId);
            Require(targetDateProcessing.Status ==
                        "complete_target_date_processing_lead_time_axis_downstream_pending" &&
                    targetDateProcessing.RouteOccurrenceInventoryComplete &&
                    targetDateProcessing
                        .ProcessingLeadTimeAxisResolutionComplete &&
                    !targetDateProcessing.TrainingLabelEligible &&
                    targetDateProcessing.RouteOccurrenceCount == 77 &&
                    targetDateProcessing.ProcessingLeadTimeAxisResolvedCount == 77 &&
                    targetDateProcessing.ProcessingLeadTimeMatchCount == 2 &&
                    targetDateProcessing.ProcessingLeadTimeMissCount == 3 &&
                    targetDateProcessing.ProcessingLeadTimeNotRequiredCount == 2 &&
                    targetDateProcessing.NotApplicableUpstreamCount == 72 &&
                    targetDateProcessing.BlockedUpstreamCount == 0 &&
                    targetDateProcessing.BlockedProcessingEvidenceCount == 0 &&
                    targetDateProcessingCrops.Length == 3 &&
                    targetDateProcessingCrops.All(route =>
                        route.ProcessingLeadTimeMatchesTargetDate == false &&
                        route.Evaluations.Single().ProductionStateKind ==
                            "new_crop_from_seed" &&
                        route.Evaluations.Single().AuthoritativeBaseGrowthDays == 4 &&
                        route.Evaluations.Single()
                            .ProvenLeadTimeDaysLowerBound == 1 &&
                        route.Evaluations.Single()
                            .ProvenNotBeforeTotalDay == 1) &&
                    targetDateProcessingShop.ProcessingLeadTimeAxisStatus ==
                        "resolved_processing_lead_time_not_required" &&
                    JsonSerializer.Serialize(
                        targetDateProcessingShop.UpstreamRoute,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        targetDateReservationShop,
                        JsonDefaults.Options),
                "Target-date processing-lead-time axis resolution drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateProcessing,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildProcessing(
                            targetDateReservationPath,
                            strategyLedgerPath,
                            targetDateUnlockSnapshotPath),
                        JsonDefaults.Options),
                "Target-date processing-lead-time resolution is not deterministic.");

            WriteFishingForecastSnapshot(fishingForecastSnapshotPath);
            Write(fishingForecastManifestPath, new FishingForecastSnapshotManifest
            {
                Snapshots = new[]
                {
                    new FishingForecastSnapshotReference(
                        "fixture:Beach:2",
                        "Beach",
                        2,
                        Path.GetFileName(fishingForecastSnapshotPath),
                        CurrentTeacherFrontierSupport.HashFile(
                            fishingForecastSnapshotPath))
                }
            });
            AcquisitionRouteTargetDateFishingProbabilityReport
                BuildFishingProbability() =>
                    AcquisitionRouteTargetDateFishingProbabilityBuilder.Build(
                        inventoryPath,
                        loweringPath,
                        windowsPath,
                        routeCalendarPath,
                        targetDateCalendarPath,
                        targetDateUnlockPath,
                        targetDateFestivalPath,
                        targetDateLocationPath,
                        targetDateFacilityPath,
                        targetDateResourcePath,
                        targetDateCurrencyPath,
                        targetDateReservationPath,
                        targetDateProcessingPath,
                        strategyLedgerPath,
                        targetDateUnlockSnapshotPath,
                        targetDateRouteCalibrationPath,
                        fishingForecastManifestPath);
            var targetDateFishingProbability = BuildFishingProbability();
            Write(targetDateFishingProbabilityPath, targetDateFishingProbability);
            var targetDateFishingRoute = targetDateFishingProbability.Routes.Single(
                route => route.RouteOccurrenceId.StartsWith(
                             "master_angler:",
                             StringComparison.Ordinal) &&
                         route.RouteKind == "native_location_fish_spawn" &&
                         route.TargetQualifiedItemId == "(O)145");
            Require(targetDateFishingProbability.Status ==
                        "complete_target_date_fishing_probability_axis_downstream_pending" &&
                    targetDateFishingProbability.RouteOccurrenceInventoryComplete &&
                    !targetDateFishingProbability.TrainingLabelEligible &&
                    targetDateFishingProbability.RouteOccurrenceCount == 77 &&
                    targetDateFishingProbability.FishingRouteCount == 1 &&
                    targetDateFishingProbability.PositiveProbabilityCount == 1 &&
                    targetDateFishingProbability.ZeroProbabilityCount == 0 &&
                    targetDateFishingProbability.BlockedProbabilityCount == 0 &&
                    targetDateFishingRoute.ProbabilityAxisResolved &&
                    targetDateFishingRoute.PositiveProbabilityAvailable == true &&
                    targetDateFishingRoute.SingleAttemptProbabilityLowerBound == 0.2d &&
                    targetDateFishingRoute.IndependentRetryLowerBoundProven ==
                        true &&
                    targetDateFishingRoute.RetryBlockingReasons.Length == 0 &&
                    targetDateFishingRoute.SelectedProjection is not null &&
                    targetDateFishingRoute.SelectedProjection.TargetLocationId ==
                        "Beach" &&
                    targetDateFishingRoute.SelectedProjection.RodSlotIndex == 2 &&
                    targetDateFishingRoute.ForecastSnapshotSha256.Single() ==
                        CurrentTeacherFrontierSupport.HashFile(
                            fishingForecastSnapshotPath),
                "Target-date fishing terminal probability axis drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateFishingProbability,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildFishingProbability(),
                        JsonDefaults.Options),
                "Target-date fishing terminal probability is not deterministic.");

            AcquisitionRouteTargetDateStochasticRetryReport BuildStochasticRetry(
                string processingPath,
                string? fishingProbabilityPath = null) =>
                AcquisitionRouteTargetDateStochasticRetryBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    targetDateReservationPath,
                    processingPath,
                    fishingProbabilityPath ?? targetDateFishingProbabilityPath,
                    fishingForecastManifestPath,
                    strategyLedgerPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            var targetDateStochasticRetry = BuildStochasticRetry(
                targetDateProcessingPath);
            Write(targetDateStochasticRetryPath, targetDateStochasticRetry);
            var targetDateStochasticShop = targetDateStochasticRetry.Routes.Single(
                route => route.RouteOccurrenceId ==
                    targetDateProcessingShop.RouteOccurrenceId);
            var targetDateStochasticFish = targetDateStochasticRetry.Routes.Single(
                route => route.RouteOccurrenceId.StartsWith(
                             "master_angler:",
                             StringComparison.Ordinal) &&
                         route.RetryBudgetKind ==
                             "independent_binomial_retry_budget");
            Require(targetDateStochasticRetry.Status ==
                        "complete_target_date_stochastic_retry_budget_axis_downstream_pending" &&
                    targetDateStochasticRetry.RouteOccurrenceInventoryComplete &&
                    targetDateStochasticRetry
                        .StochasticRetryAxisResolutionComplete &&
                    !targetDateStochasticRetry.TrainingLabelEligible &&
                    targetDateStochasticRetry.RouteOccurrenceCount == 77 &&
                    targetDateStochasticRetry
                        .StochasticRetryAxisResolvedCount == 77 &&
                    targetDateStochasticRetry.StochasticRetryMatchCount == 2 &&
                    targetDateStochasticRetry
                        .StochasticRetryNotRequiredCount == 1 &&
                    targetDateStochasticRetry.MaterializedOutputCount == 0 &&
                    targetDateStochasticRetry.NotApplicableUpstreamCount == 75 &&
                    targetDateStochasticRetry.BlockedUpstreamCount == 0 &&
                    targetDateStochasticRetry
                        .BlockedProbabilityEvidenceCount == 0 &&
                    targetDateStochasticRetry.BlockedRetryReservationCount == 0 &&
                    targetDateStochasticRetry.TargetSuccessProbability ==
                        StardewAI.Core.Infrastructure.StochasticRetryPolicy
                            .TargetSuccessProbability &&
                    targetDateStochasticShop.UncertaintyMode ==
                        "deterministic_fresh_receipt" &&
                    targetDateStochasticShop.StochasticRetryAxisStatus ==
                        "resolved_stochastic_retry_not_required" &&
                    targetDateStochasticShop.RequiredOutputQuantity == 2 &&
                    targetDateStochasticShop.MinimumOutputQuality == 1 &&
                    targetDateStochasticShop.SingleAttemptSuccessProbability is null &&
                    targetDateStochasticShop.RequiredAttemptCount == 0 &&
                    targetDateStochasticShop.AdditionalRetryCount == 0 &&
                    targetDateStochasticFish.UncertaintyMode ==
                        "native_outcome_domain_and_retry_bound" &&
                    targetDateStochasticFish.StochasticRetryAxisStatus ==
                        "resolved_independent_stochastic_retry_budget" &&
                    targetDateStochasticFish.StochasticRetryAxisResolved &&
                    targetDateStochasticFish
                        .StochasticRetryBudgetMatchesTargetDate == true &&
                    targetDateStochasticFish.SingleAttemptSuccessProbability ==
                        0.2d &&
                    targetDateStochasticFish.RequiredAttemptCount == 14 &&
                    targetDateStochasticFish.AdditionalRetryCount == 13 &&
                    !targetDateStochasticFish
                        .RetryExpandsReservedConsumables &&
                    targetDateStochasticFish
                        .RetryExpandedReservationRevalidated &&
                    targetDateStochasticFish.BlockingReasons.Length == 0 &&
                    JsonSerializer.Serialize(
                        targetDateStochasticShop.UpstreamRoute,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        targetDateProcessingShop,
                        JsonDefaults.Options),
                "Target-date stochastic-retry-budget axis resolution drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateStochasticRetry,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildStochasticRetry(targetDateProcessingPath),
                        JsonDefaults.Options),
                "Target-date stochastic-retry-budget resolution is not deterministic.");

            AcquisitionRouteTargetDateDailyTimeEnergyReport BuildDailyBudget(
                string stochasticPath) =>
                AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    targetDateReservationPath,
                    targetDateProcessingPath,
                    targetDateFishingProbabilityPath,
                    stochasticPath,
                    fishingForecastManifestPath,
                    strategyLedgerPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            var targetDateDailyBudget = BuildDailyBudget(
                targetDateStochasticRetryPath);
            Write(targetDateDailyTimeEnergyPath, targetDateDailyBudget);
            var targetDateDailyShop = targetDateDailyBudget.Routes.Single(route =>
                route.RouteOccurrenceId == targetDateStochasticShop.RouteOccurrenceId);
            var targetDateDailyFish = targetDateDailyBudget.Routes.Single(route =>
                route.RouteOccurrenceId == targetDateStochasticFish.RouteOccurrenceId);
            Require(targetDateDailyBudget.Status ==
                        "complete_target_date_daily_time_energy_budget_axis_downstream_pending" &&
                    targetDateDailyBudget.RouteOccurrenceInventoryComplete &&
                    targetDateDailyBudget.DailyTimeEnergyAxisResolutionComplete &&
                    !targetDateDailyBudget.TrainingLabelEligible &&
                    targetDateDailyBudget.RouteOccurrenceCount == 77 &&
                    targetDateDailyBudget.DailyTimeEnergyAxisResolvedCount == 77 &&
                    targetDateDailyBudget.DailyTimeEnergyMatchCount == 2 &&
                    targetDateDailyBudget.DailyTimeBudgetMissCount == 0 &&
                    targetDateDailyBudget.DailyEnergyBudgetMissCount == 0 &&
                    targetDateDailyBudget.NotApplicableUpstreamCount == 75 &&
                    targetDateDailyBudget.BlockedUpstreamCount == 0 &&
                    targetDateDailyBudget.BlockedBudgetEvidenceCount == 0 &&
                    targetDateDailyShop.DailyBudgetKind ==
                        "native_shop_rolling_purchase" &&
                    targetDateDailyShop.DailyTimeEnergyMatchesTargetDate == true &&
                    targetDateDailyShop.Evaluation is not null &&
                    targetDateDailyShop.Evaluation!.TargetLocationId ==
                        "FixtureShop" &&
                    targetDateDailyShop.Evaluation.TargetTileX == 4 &&
                    targetDateDailyShop.Evaluation.TargetTileY == 18 &&
                    targetDateDailyShop.Evaluation.RequiredAttemptCount == 2 &&
                    targetDateDailyShop.Evaluation.TerminalActionGameMinutes ==
                        StardewAI.Core.Execution.ShopPurchaseBudgetPolicy
                            .ConservativeGameMinutesForPurchases(2) &&
                    targetDateDailyFish.DailyBudgetKind ==
                        "native_fishing_retry_attempts" &&
                    targetDateDailyFish.DailyTimeEnergyMatchesTargetDate == true &&
                    targetDateDailyFish.Evaluation is not null &&
                    targetDateDailyFish.Evaluation.TargetLocationId == "Beach" &&
                    targetDateDailyFish.Evaluation.TargetTileX ==
                        targetDateDailyFish.Evaluation.StandTileX &&
                    targetDateDailyFish.Evaluation.TargetTileY ==
                        targetDateDailyFish.Evaluation.StandTileY &&
                    targetDateDailyFish.Evaluation.RequiredAttemptCount == 14 &&
                    targetDateDailyFish.Evaluation.EffectiveFishingLevel == 5 &&
                    targetDateDailyFish.Evaluation.AvailableEnergy == 270d &&
                    targetDateDailyFish.Evaluation.EnergyPerAttempt == 7.5d &&
                    targetDateDailyFish.Evaluation.RequiredEnergy == 105d &&
                    targetDateDailyFish.Evaluation.MinimumEnergyReserve == 1d &&
                    targetDateDailyFish.Evaluation.TerminalActionGameMinutes ==
                        StardewAI.Core.Execution.FishingAttemptBudgetPolicy
                            .ConservativeGameMinutesForAttempts(
                                14,
                                challengeBait: false) &&
                    targetDateDailyFish.Evaluation.TerminalExecutionAssumption ==
                        "perfect_lock_input_profile",
                "Target-date daily time/energy budget axis drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateDailyBudget,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildDailyBudget(targetDateStochasticRetryPath),
                        JsonDefaults.Options),
                "Target-date daily time/energy budget is not deterministic.");

            AcquisitionRouteTargetDateOpportunityCostReport BuildOpportunityCost(
                string dailyPath) =>
                AcquisitionRouteTargetDateOpportunityCostBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    targetDateFacilityPath,
                    targetDateResourcePath,
                    targetDateCurrencyPath,
                    targetDateReservationPath,
                    targetDateProcessingPath,
                    targetDateFishingProbabilityPath,
                    targetDateStochasticRetryPath,
                    dailyPath,
                    fishingForecastManifestPath,
                    strategyLedgerPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            var targetDateOpportunityCost = BuildOpportunityCost(
                targetDateDailyTimeEnergyPath);
            Write(targetDateOpportunityCostPath, targetDateOpportunityCost);
            var targetDateOpportunityShop = targetDateOpportunityCost.Routes.Single(
                route => route.RouteOccurrenceId ==
                    targetDateDailyShop.RouteOccurrenceId);
            var targetDateOpportunityFish = targetDateOpportunityCost.Routes.Single(
                route => route.RouteOccurrenceId ==
                    targetDateDailyFish.RouteOccurrenceId);
            Require(targetDateOpportunityCost.Status ==
                        "complete_target_date_opportunity_cost_axis_downstream_pending" &&
                    targetDateOpportunityCost.RouteOccurrenceInventoryComplete &&
                    targetDateOpportunityCost
                        .OpportunityCostAxisResolutionComplete &&
                    !targetDateOpportunityCost.TrainingLabelEligible &&
                    targetDateOpportunityCost.RouteOccurrenceCount == 77 &&
                    targetDateOpportunityCost
                        .OpportunityCostAxisResolvedCount == 77 &&
                    targetDateOpportunityCost.ParetoFrontierCount == 2 &&
                    targetDateOpportunityCost.ParetoDominatedCount == 0 &&
                    targetDateOpportunityCost.NotApplicableUpstreamCount == 75 &&
                    targetDateOpportunityCost.BlockedUpstreamCount == 0 &&
                    targetDateOpportunityCost.BlockedCostEvidenceCount == 0 &&
                    targetDateOpportunityShop.OpportunityCostAxisStatus ==
                        "resolved_opportunity_cost_pareto_frontier" &&
                    targetDateOpportunityShop.CostVector is not null &&
                    targetDateOpportunityShop.CostVector.RequiredEnergy == 0d &&
                    targetDateOpportunityShop.CostVector
                        .GuaranteedElapsedGameMinutes ==
                        StardewAI.Core.Execution.GameClockBudgetPolicy
                            .ClockMinutesBetween(
                                targetDateDailyShop.Evaluation!.SnapshotStartTime,
                                targetDateDailyShop.Evaluation
                                    .GuaranteedCompletionByTime!.Value) &&
                    targetDateOpportunityShop.CostVector
                        .MaterialTotalSaleValue == 20 &&
                    targetDateOpportunityShop.CostVector.MaterialCosts.Single()
                        .QualifiedItemId == "(O)388" &&
                    targetDateOpportunityShop.CostVector.MaterialCosts.Single()
                        .Quality == 0 &&
                    targetDateOpportunityShop.CostVector.MaterialCosts.Single()
                        .UnitSalePrice == 2 &&
                    targetDateOpportunityShop.CostVector.MaterialCosts.Single()
                        .Quantity == 10 &&
                    targetDateOpportunityShop.CostVector.CurrencyCosts.Single()
                        .CurrencyId == NativeShopCurrencies.Money &&
                    targetDateOpportunityShop.CostVector.CurrencyCosts.Single()
                        .CurrencyKey == "money" &&
                    targetDateOpportunityShop.CostVector.CurrencyCosts.Single()
                        .Amount == 200 &&
                    targetDateOpportunityFish.OpportunityCostAxisStatus ==
                        "resolved_opportunity_cost_pareto_frontier" &&
                    targetDateOpportunityFish.CostVector is not null &&
                    targetDateOpportunityFish.CostVector.RequiredEnergy == 105d &&
                    targetDateOpportunityFish.CostVector
                        .GuaranteedElapsedGameMinutes ==
                        StardewAI.Core.Execution.GameClockBudgetPolicy
                            .ClockMinutesBetween(
                                targetDateDailyFish.Evaluation!.SnapshotStartTime,
                                targetDateDailyFish.Evaluation
                                    .GuaranteedCompletionByTime!.Value) &&
                    targetDateOpportunityFish.CostVector.MaterialCosts.Length == 0 &&
                    targetDateOpportunityFish.CostVector.CurrencyCosts.Length == 0,
                "Target-date opportunity-cost axis resolution drifted.");
            Require(JsonSerializer.Serialize(
                        targetDateOpportunityCost,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        BuildOpportunityCost(targetDateDailyTimeEnergyPath),
                        JsonDefaults.Options),
                "Target-date opportunity-cost resolution is not deterministic.");

            return new TargetDateAxes(
                targetDateCalendar,
                targetDateOpportunityCost,
                targetDateOpportunityShop,
                targetDateOpportunityFish,
                targetDateCurrencyShop,
                targetDateReservationShop);
        }
    }
}
