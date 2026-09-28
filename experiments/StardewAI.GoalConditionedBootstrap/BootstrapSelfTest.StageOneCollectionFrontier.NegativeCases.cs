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
        private void VerifyNegativeCases(
            AcquisitionRouteCalendarResolutionReport routeCalendar,
            AcquisitionRouteTargetDateCurrency targetDateCurrencyShop,
            AcquisitionRouteTargetDateReservation targetDateReservationShop)
        {
            File.Copy(
                targetDateDailyTimeEnergyPath,
                tamperedTargetDateDailyTimeEnergyPath,
                overwrite: true);
            var tamperedDailyTimeEnergy = JsonNode.Parse(File.ReadAllText(
                tamperedTargetDateDailyTimeEnergyPath))!.AsObject();
            tamperedDailyTimeEnergy["routes"]![0]![
                "daily_time_energy_axis_status"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateDailyTimeEnergyPath,
                tamperedDailyTimeEnergy.ToJsonString(JsonDefaults.Options));
            var tamperedDailyTimeEnergyRejected = false;
            try
            {
                _ = BuildOpportunityCost(tamperedTargetDateDailyTimeEnergyPath);
            }
            catch (InvalidDataException)
            {
                tamperedDailyTimeEnergyRejected = true;
            }
            Require(tamperedDailyTimeEnergyRejected,
                "Target-date daily time/energy tampering was not rejected by opportunity cost.");

            File.Copy(
                targetDateStochasticRetryPath,
                tamperedTargetDateStochasticRetryPath,
                overwrite: true);
            var tamperedStochasticRetry = JsonNode.Parse(File.ReadAllText(
                tamperedTargetDateStochasticRetryPath))!.AsObject();
            tamperedStochasticRetry["routes"]![0]![
                "stochastic_retry_axis_status"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateStochasticRetryPath,
                tamperedStochasticRetry.ToJsonString(JsonDefaults.Options));
            var tamperedStochasticRetryRejected = false;
            try
            {
                _ = BuildDailyBudget(tamperedTargetDateStochasticRetryPath);
            }
            catch (InvalidDataException)
            {
                tamperedStochasticRetryRejected = true;
            }
            Require(tamperedStochasticRetryRejected,
                "Target-date stochastic-retry tampering was not rejected by daily budgeting.");

            File.Copy(
                targetDateProcessingPath,
                tamperedTargetDateProcessingPath,
                overwrite: true);
            var tamperedProcessing = JsonNode.Parse(File.ReadAllText(
                tamperedTargetDateProcessingPath))!.AsObject();
            tamperedProcessing["routes"]![0]![
                "processing_lead_time_axis_status"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateProcessingPath,
                tamperedProcessing.ToJsonString(JsonDefaults.Options));
            var tamperedProcessingRejected = false;
            try
            {
                _ = BuildStochasticRetry(tamperedTargetDateProcessingPath);
            }
            catch (InvalidDataException)
            {
                tamperedProcessingRejected = true;
            }
            Require(tamperedProcessingRejected,
                "Target-date processing artifact tampering was not rejected.");

            File.Copy(
                targetDateFishingProbabilityPath,
                tamperedTargetDateFishingProbabilityPath,
                overwrite: true);
            var tamperedFishingProbability = JsonNode.Parse(File.ReadAllText(
                tamperedTargetDateFishingProbabilityPath))!.AsObject();
            tamperedFishingProbability["routes"]![0]![
                "probability_axis_resolved"] = false;
            File.WriteAllText(
                tamperedTargetDateFishingProbabilityPath,
                tamperedFishingProbability.ToJsonString(JsonDefaults.Options));
            var tamperedFishingProbabilityRejected = false;
            try
            {
                _ = BuildStochasticRetry(
                    targetDateProcessingPath,
                    tamperedTargetDateFishingProbabilityPath);
            }
            catch (InvalidDataException)
            {
                tamperedFishingProbabilityRejected = true;
            }
            Require(tamperedFishingProbabilityRejected,
                "Target-date fishing probability tampering was not rejected.");

            JsonElement CrabPotNetwork(
                bool ready,
                bool readyStateConsistent,
                string currentOutput,
                string serviceStatus,
                string bait) => JsonSerializer.SerializeToElement(new
                {
                    schema_version = "crab_pot_network.v1",
                    projection_status =
                    "complete_crab_pots_across_loaded_persistent_locations",
                    rows = new[]
                {
                    new
                    {
                        location_id = "Beach",
                        exact_base_crab_pot = true,
                        production_domain_complete = true,
                        ready_state_consistent = readyStateConsistent,
                        ready_for_harvest = ready,
                        current_output_qualified_item_id = currentOutput,
                        current_output_stack = currentOutput.Length > 0 ? 1 : 0,
                        current_output_quality = 0,
                        service_status = serviceStatus,
                        bait_qualified_item_id = bait,
                        owner_has_luremaster = false,
                        possible_qualified_item_ids = new[] { "(O)715" }
                    }
                }
                }, JsonDefaults.Options);
            var readyCrabPot = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                CrabPotNetwork(
                    ready: true,
                    readyStateConsistent: true,
                    currentOutput: "(O)715",
                    serviceStatus: "ready_for_collection",
                    bait: string.Empty),
                new[] { "Beach" },
                "(O)715",
                0);
            var waitingCrabPot = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                CrabPotNetwork(
                    ready: false,
                    readyStateConsistent: true,
                    currentOutput: string.Empty,
                    serviceStatus: "producing_or_waiting",
                    bait: "(O)685"),
                new[] { "Beach" },
                "(O)715",
                0);
            var inconsistentCrabPot =
                AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                    CrabPotNetwork(
                        ready: true,
                        readyStateConsistent: false,
                        currentOutput: "(O)715",
                        serviceStatus: "ready_for_collection",
                        bait: string.Empty),
                    new[] { "Beach" },
                    "(O)715",
                    0);
            var inconsistentServiceCrabPot =
                AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                    CrabPotNetwork(
                        ready: false,
                        readyStateConsistent: true,
                        currentOutput: string.Empty,
                        serviceStatus: "bait_required",
                        bait: "(O)685"),
                    new[] { "Beach" },
                    "(O)715",
                    0);
            Require(readyCrabPot.EvidenceAvailable &&
                    readyCrabPot.Evaluations.Single()
                        .OutputReadyOnTargetDate == true &&
                    readyCrabPot.Evaluations.Single()
                        .ProvenOutputQuantityLowerBound == 1 &&
                    readyCrabPot.Evaluations.Single()
                        .ProvenMinimumQuality == 0 &&
                    AcquisitionOutputProof.ReadyQuantity(
                        readyCrabPot.Evaluations,
                        0) == 1 &&
                    AcquisitionOutputProof.ReadyQuantity(
                        readyCrabPot.Evaluations,
                        1) == 0 &&
                    AcquisitionQuantityMath.DivideRoundUp(5, 2) == 3 &&
                    AcquisitionQuantityMath.Multiply(5, 2) == 10 &&
                    readyCrabPot.Evaluations.Single()
                        .ProvenLeadTimeDaysLowerBound == 0 &&
                    waitingCrabPot.EvidenceAvailable &&
                    waitingCrabPot.Evaluations.Single()
                        .OutputReadyOnTargetDate == false &&
                    waitingCrabPot.Evaluations.Single()
                        .ProvenNotBeforeTotalDay == 1 &&
                    !inconsistentCrabPot.EvidenceAvailable &&
                    inconsistentCrabPot.BlockingReasons.Contains(
                        "crab_pot_runtime_row_invalid_or_incomplete") &&
                    !inconsistentServiceCrabPot.EvidenceAvailable &&
                    inconsistentServiceCrabPot.BlockingReasons.Contains(
                        "crab_pot_service_state_does_not_prove_next_production"),
                "Native crab-pot lead-time classification drifted.");

            var readyCropSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            readyCropSnapshot["state_hash"] = "target-date-ready-crop-state";
            var readyCropCapacity = readyCropSnapshot["state"]!["locations"]!
                ["social_route_date_evidence"]!["value"]!["locations"]!.AsArray()
                .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
                ["cultivation_capacity"]!.AsObject();
            readyCropCapacity["total_prepared_soil_slot_count"] = 3;
            readyCropCapacity["open_prepared_soil_slot_count"] = 0;
            readyCropCapacity["occupied_crop_slot_count"] = 3;
            readyCropCapacity["unresolved_harvest_item_slot_count"] = 0;
            readyCropCapacity["occupied_harvest_items"] = new JsonArray(
                new JsonObject
                {
                    ["harvest_item_qualified_id"] = "(O)24",
                    ["is_garden_pot"] = false,
                    ["slot_count"] = 2
                },
                new JsonObject
                {
                    ["harvest_item_qualified_id"] = "(O)188",
                    ["is_garden_pot"] = false,
                    ["slot_count"] = 1
                });
            readyCropSnapshot["state"]!["farm"]!["crops"]!["value"] =
                new JsonArray(
                    new JsonObject
                    {
                        ["location_id"] = "Farm",
                        ["tile_x"] = 1,
                        ["tile_y"] = 1,
                        ["harvest_item_qualified_id"] = "(O)24",
                        ["harvest_item_projection_status"] =
                            "exact_from_live_index_of_harvest",
                        ["dead"] = false,
                        ["ready_for_harvest"] = true,
                        ["days_until_next_harvest_if_watered"] = 0
                    },
                    new JsonObject
                    {
                        ["location_id"] = "Farm",
                        ["tile_x"] = 2,
                        ["tile_y"] = 1,
                        ["harvest_item_qualified_id"] = "(O)24",
                        ["harvest_item_projection_status"] =
                            "exact_from_live_index_of_harvest",
                        ["dead"] = false,
                        ["ready_for_harvest"] = true,
                        ["days_until_next_harvest_if_watered"] = 0
                    },
                    new JsonObject
                    {
                        ["location_id"] = "Farm",
                        ["tile_x"] = 3,
                        ["tile_y"] = 1,
                        ["harvest_item_qualified_id"] = "(O)188",
                        ["harvest_item_projection_status"] =
                            "exact_from_live_index_of_harvest",
                        ["dead"] = false,
                        ["ready_for_harvest"] = true,
                        ["days_until_next_harvest_if_watered"] = 0
                    });
            var readyCropRoot = Path.Combine(root, "ready-crop-processing");
            Directory.CreateDirectory(readyCropRoot);
            var readyCropSnapshotPath = Path.Combine(
                readyCropRoot,
                "snapshot.json");
            var readyCropLedgerPath = Path.Combine(
                readyCropRoot,
                "strategy-ledger.json");
            File.WriteAllText(
                readyCropSnapshotPath,
                readyCropSnapshot.ToJsonString(JsonDefaults.Options));
            WriteEmptyStrategyLedger(
                readyCropLedgerPath,
                "target-date-ready-crop-state");
            var readyCropProcessing = BuildProcessingFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                readyCropSnapshotPath,
                targetDateRouteCalibrationPath,
                readyCropLedgerPath,
                readyCropRoot);
            var readyCropEvaluations = readyCropProcessing.Routes.Where(route =>
                    route.ProcessingLeadTimeRequirementKind ==
                        "crop_growth_or_ready_crop")
                .Select(route => route.Evaluations.Single())
                .ToArray();
            Require(readyCropProcessing.ProcessingLeadTimeAxisResolutionComplete &&
                    readyCropProcessing.ProcessingLeadTimeMatchCount == 5 &&
                    readyCropProcessing.ProcessingLeadTimeMissCount == 0 &&
                    readyCropEvaluations.Length == 3 &&
                    readyCropEvaluations.All(evaluation =>
                        evaluation.Status ==
                            "resolved_existing_crop_ready_on_target_date") &&
                    readyCropEvaluations.Select(evaluation =>
                            evaluation.ProvenOutputQuantityLowerBound)
                        .Order()
                        .SequenceEqual(new int?[] { 1, 2, 2 }) &&
                    readyCropEvaluations.Select(evaluation =>
                            evaluation.ProvenMinimumQuality)
                        .Order()
                        .SequenceEqual(new int?[] { 0, 1, 1 }),
                "A harvest-ready live crop did not satisfy same-day lead time.");

            var missingLiveCropSnapshot = JsonNode.Parse(
                readyCropSnapshot.ToJsonString())!.AsObject();
            missingLiveCropSnapshot["state_hash"] =
                "target-date-missing-live-crop-state";
            missingLiveCropSnapshot["state"]!["farm"]!.AsObject()
                .Remove("crops");
            var missingLiveCropRoot = Path.Combine(
                root,
                "missing-live-crop-processing");
            Directory.CreateDirectory(missingLiveCropRoot);
            var missingLiveCropSnapshotPath = Path.Combine(
                missingLiveCropRoot,
                "snapshot.json");
            var missingLiveCropLedgerPath = Path.Combine(
                missingLiveCropRoot,
                "strategy-ledger.json");
            File.WriteAllText(
                missingLiveCropSnapshotPath,
                missingLiveCropSnapshot.ToJsonString(JsonDefaults.Options));
            WriteEmptyStrategyLedger(
                missingLiveCropLedgerPath,
                "target-date-missing-live-crop-state");
            var missingLiveCropProcessing = BuildProcessingFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingLiveCropSnapshotPath,
                targetDateRouteCalibrationPath,
                missingLiveCropLedgerPath,
                missingLiveCropRoot);
            Require(missingLiveCropProcessing.Status ==
                        "partial_target_date_processing_lead_time_axis_blocks" &&
                    !missingLiveCropProcessing
                        .ProcessingLeadTimeAxisResolutionComplete &&
                    missingLiveCropProcessing
                        .ProcessingLeadTimeAxisResolvedCount == 74 &&
                    missingLiveCropProcessing.ProcessingLeadTimeMatchCount == 2 &&
                    missingLiveCropProcessing.ProcessingLeadTimeMissCount == 0 &&
                    missingLiveCropProcessing.BlockedProcessingEvidenceCount == 3 &&
                    missingLiveCropProcessing.Routes.Where(route =>
                        route.ProcessingLeadTimeAxisStatus ==
                            "blocked_processing_lead_time_evidence").All(route =>
                        route.BlockingReasons.Contains(
                            "state.farm.crops.value:missing_or_unavailable")),
                "Missing per-tile crop state did not fail closed.");

            File.Copy(
                targetDateReservationPath,
                tamperedTargetDateReservationPath,
                overwrite: true);
            var tamperedReservation = JsonNode.Parse(File.ReadAllText(
                tamperedTargetDateReservationPath))!.AsObject();
            tamperedReservation["routes"]![0]!["claim_disposition"] =
                "tampered";
            File.WriteAllText(
                tamperedTargetDateReservationPath,
                tamperedReservation.ToJsonString(JsonDefaults.Options));
            var tamperedReservationRejected = false;
            try
            {
                _ = BuildProcessing(
                    tamperedTargetDateReservationPath,
                    strategyLedgerPath,
                    targetDateUnlockSnapshotPath);
            }
            catch (InvalidDataException)
            {
                tamperedReservationRejected = true;
            }
            Require(tamperedReservationRejected,
                "A tampered inventory-reservation report was accepted.");

            StrategyCommitmentLedger FixtureLedger(int revision) => new()
            {
                LedgerId = "strategy-ledger:fixture-save:1",
                SaveId = "fixture-save",
                PlayerId = "1",
                Revision = revision,
                UpdatedAt = "2026-09-10T00:00:00Z",
                SourceStateHash = "target-date-unlock-state"
            };
            var materialReservedLedger = FixtureLedger(1);
            materialReservedLedger.MaterialReservations = new[]
            {
                new MaterialReservation
                {
                    ReservationId = "other-route-material",
                    Revision = 1,
                    Status = StrategyCommitmentStatuses.Active,
                    SourceDecisionId = "other-route",
                    SourceStateHash = "earlier-state",
                    GoalId = "grandpa_score_21",
                    OwnerPlayerId = 1,
                    NodeId = "player:1",
                    SlotIndex = 1,
                    QualifiedItemId = "(O)388",
                    Quantity = 1,
                    Purpose = "reserve another route"
                }
            };
            Write(materialReservedLedgerPath, materialReservedLedger);
            var materialReservedReport =
                BuildReservation(materialReservedLedgerPath);
            var materialConflict = materialReservedReport.Routes.Single(route =>
                route.InventoryReservationMatchesTargetDate == false);
            Require(materialReservedReport.ReservationAxisResolutionComplete &&
                    materialReservedReport.ReservationMatchCount == 4 &&
                    materialReservedReport.ReservationConflictCount == 1 &&
                    materialReservedReport.ClaimProposedCount == 3 &&
                    materialReservedReport.MaterialClaimCount == 3 &&
                    materialReservedReport.CurrencyClaimCount == 0 &&
                    materialConflict.RouteOccurrenceId ==
                        targetDateCurrencyShop.RouteOccurrenceId &&
                    materialConflict.NonMatchingReasons.Contains(
                        "unreserved_material_quantity_unavailable:(O)388"),
                "An active material reservation did not prevent cross-route spending.");
            materialReservedLedger.MaterialReservations[0].Quantity = 11;
            Write(materialReservedLedgerPath, materialReservedLedger);
            var overbookedMaterialLedgerRejected = false;
            try
            {
                _ = BuildReservation(materialReservedLedgerPath);
            }
            catch (InvalidDataException)
            {
                overbookedMaterialLedgerRejected = true;
            }
            Require(overbookedMaterialLedgerRejected,
                "A globally overbooked material ledger was accepted.");

            var currencyReservedLedger = FixtureLedger(1);
            currencyReservedLedger.CurrencyReservations = new[]
            {
                new CurrencyReservation
                {
                    ReservationId = "other-route-currency",
                    Revision = 1,
                    Status = StrategyCommitmentStatuses.Active,
                    SourceDecisionId = "other-route",
                    SourceStateHash = "earlier-state",
                    GoalId = "grandpa_score_21",
                    OwnerPlayerId = 1,
                    CurrencyId = NativeShopCurrencies.Money,
                    CurrencyKey = "money",
                    Amount = 450,
                    Purpose = "reserve another route"
                }
            };
            Write(currencyReservedLedgerPath, currencyReservedLedger);
            var currencyReservedReport =
                BuildReservation(currencyReservedLedgerPath);
            var currencyConflict = currencyReservedReport.Routes.Single(route =>
                route.InventoryReservationMatchesTargetDate == false);
            Require(currencyReservedReport.ReservationAxisResolutionComplete &&
                    currencyReservedReport.ReservationMatchCount == 4 &&
                    currencyReservedReport.ReservationConflictCount == 1 &&
                    currencyReservedReport.ClaimProposedCount == 3 &&
                    currencyReservedReport.MaterialClaimCount == 3 &&
                    currencyReservedReport.CurrencyClaimCount == 0 &&
                    currencyConflict.RouteOccurrenceId ==
                        targetDateCurrencyShop.RouteOccurrenceId &&
                    currencyConflict.NonMatchingReasons.Contains(
                        "unreserved_currency_amount_unavailable:money"),
                "An active currency reservation did not prevent cross-route spending.");
            currencyReservedLedger.CurrencyReservations[0].Amount = 501;
            Write(currencyReservedLedgerPath, currencyReservedLedger);
            var overbookedCurrencyLedgerRejected = false;
            try
            {
                _ = BuildReservation(currencyReservedLedgerPath);
            }
            catch (InvalidDataException)
            {
                overbookedCurrencyLedgerRejected = true;
            }
            Require(overbookedCurrencyLedgerRejected,
                "A globally overbooked currency ledger was accepted.");

            var cancelledLedger = FixtureLedger(1);
            cancelledLedger.MaterialReservations = new[]
            {
                new MaterialReservation
                {
                    ReservationId = "cancelled-material",
                    Revision = 2,
                    Status = StrategyCommitmentStatuses.Cancelled,
                    SourceDecisionId = "other-route",
                    SourceStateHash = "earlier-state",
                    GoalId = "grandpa_score_21",
                    OwnerPlayerId = 1,
                    NodeId = "player:1",
                    SlotIndex = 1,
                    QualifiedItemId = "(O)388",
                    Quantity = 5,
                    Purpose = "reserve another route",
                    CancelReason = "route_replanned"
                }
            };
            cancelledLedger.CurrencyReservations = new[]
            {
                new CurrencyReservation
                {
                    ReservationId = "cancelled-currency",
                    Revision = 2,
                    Status = StrategyCommitmentStatuses.Cancelled,
                    SourceDecisionId = "other-route",
                    SourceStateHash = "earlier-state",
                    GoalId = "grandpa_score_21",
                    OwnerPlayerId = 1,
                    CurrencyId = NativeShopCurrencies.Money,
                    CurrencyKey = "money",
                    Amount = 500,
                    Purpose = "reserve another route",
                    CancelReason = "route_replanned"
                }
            };
            Write(cancelledReservationLedgerPath, cancelledLedger);
            var cancelledReservationReport =
                BuildReservation(cancelledReservationLedgerPath);
            Require(cancelledReservationReport.ReservationMatchCount == 5 &&
                    cancelledReservationReport.ReservationConflictCount == 0 &&
                    cancelledReservationReport.ClaimProposedCount == 4,
                "Cancelled reservations incorrectly reduced available supply.");

            var proposedShopClaims = targetDateReservationShop.ClaimSet!;
            var committedLedger = FixtureLedger(2);
            committedLedger.MaterialReservations = proposedShopClaims.MaterialClaims
                .Select(claim => new MaterialReservation
                {
                    ReservationId = claim.ReservationId,
                    Revision = 1,
                    Status = StrategyCommitmentStatuses.Active,
                    SourceDecisionId = claim.SourceDecisionId,
                    SourceStateHash = claim.StateHash,
                    GoalId = claim.GoalId,
                    OwnerPlayerId = 1,
                    NodeId = claim.NodeId,
                    SlotIndex = claim.SlotIndex,
                    QualifiedItemId = claim.QualifiedItemId,
                    Quantity = claim.Quantity,
                    Purpose = claim.Purpose
                }).ToArray();
            committedLedger.CurrencyReservations = proposedShopClaims.CurrencyClaims
                .Select(claim => new CurrencyReservation
                {
                    ReservationId = claim.ReservationId,
                    Revision = 1,
                    Status = StrategyCommitmentStatuses.Active,
                    SourceDecisionId = claim.SourceDecisionId,
                    SourceStateHash = claim.StateHash,
                    GoalId = claim.GoalId,
                    OwnerPlayerId = 1,
                    CurrencyId = claim.CurrencyId,
                    CurrencyKey = "money",
                    Amount = claim.Amount,
                    Purpose = claim.Purpose
                }).ToArray();
            Write(committedReservationLedgerPath, committedLedger);
            var committedReservationReport =
                BuildReservation(committedReservationLedgerPath);
            var committedShop = committedReservationReport.Routes.Single(route =>
                route.RouteOccurrenceId == targetDateCurrencyShop.RouteOccurrenceId);
            Require(committedReservationReport.ReservationMatchCount == 5 &&
                    committedReservationReport.ClaimProposedCount == 3 &&
                    committedReservationReport.ClaimCommittedCount == 1 &&
                    committedShop.ClaimDisposition == "claim_already_committed" &&
                    committedShop.ClaimSet is not null &&
                    !committedShop.ClaimSet.ReplacementRequired &&
                    committedShop.ClaimSet.ExistingActiveReservationIds.Length == 2,
                "An exact committed route claim was not recognized idempotently.");

            var mismatchedLedger = FixtureLedger(0);
            mismatchedLedger.PlayerId = "2";
            Write(mismatchedStrategyLedgerPath, mismatchedLedger);
            var mismatchedLedgerRejected = false;
            try
            {
                _ = BuildReservation(mismatchedStrategyLedgerPath);
            }
            catch (InvalidDataException)
            {
                mismatchedLedgerRejected = true;
            }
            Require(mismatchedLedgerRejected,
                "A strategy ledger for another player was accepted.");

            var tamperedTargetDateFacility = JsonNode.Parse(
                File.ReadAllText(targetDateFacilityPath))!.AsObject();
            tamperedTargetDateFacility["routes"]![0]!["upstream_route"]!
                ["upstream_route"]!["upstream_route"]!["source_id"] =
                "tampered";
            File.WriteAllText(
                tamperedTargetDateFacilityPath,
                tamperedTargetDateFacility.ToJsonString(JsonDefaults.Options));
            var tamperedTargetDateFacilityRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateResourceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    targetDateFestivalPath,
                    targetDateLocationPath,
                    tamperedTargetDateFacilityPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            }
            catch (InvalidDataException)
            {
                tamperedTargetDateFacilityRejected = true;
            }
            Require(tamperedTargetDateFacilityRejected,
                "A tampered target-date facility report was accepted.");

            var missingResourceSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingResourceSnapshot["state"]!["farm"]!.AsObject()
                .Remove("material_inventory_graph");
            File.WriteAllText(
                missingResourceSnapshotPath,
                missingResourceSnapshot.ToJsonString(JsonDefaults.Options));
            var missingResourceReport = BuildResourceFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingResourceSnapshotPath,
                targetDateRouteCalibrationPath,
                missingResourceUnlockPath,
                missingResourceFestivalPath,
                missingResourceLocationPath,
                missingResourceFacilityPath);
            Require(missingResourceReport.Status ==
                        "partial_target_date_resource_inputs_axis_blocks" &&
                    missingResourceReport.RouteOccurrenceInventoryComplete &&
                    !missingResourceReport.ResourceInputAxisResolutionComplete &&
                    missingResourceReport.ResourceInputAxisResolvedCount == 73 &&
                    missingResourceReport.ResourceInputMatchCount == 1 &&
                    missingResourceReport.ResourceInputMissCount == 0 &&
                    missingResourceReport.ResourceInputNotRequiredCount == 1 &&
                    missingResourceReport.NotApplicableUpstreamCount == 72 &&
                    missingResourceReport.BlockedResourceEvidenceCount == 4 &&
                    missingResourceReport.Routes.Where(route =>
                        route.ResourceInputAxisStatus ==
                            "blocked_resource_input_evidence").All(route =>
                        route.BlockingReasons.Contains(
                            "material_inventory_graph_missing_or_unavailable")),
                "Missing canonical material evidence did not fail closed per input route.");

            var insufficientSeedSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            var insufficientSeedGraph = insufficientSeedSnapshot["state"]!["farm"]!
                ["material_inventory_graph"]!["value"]!;
            var insufficientSeedSlots = insufficientSeedGraph["inventory_nodes"]![0]!
                ["slots"]!.AsArray();
            insufficientSeedSlots.Remove(insufficientSeedSlots.Single(row =>
                row!["qualified_item_id"]!.GetValue<string>() == "(O)472"));
            var insufficientSeedQuantities = insufficientSeedGraph["quantity_rows"]!
                .AsArray();
            insufficientSeedQuantities.Remove(insufficientSeedQuantities.Single(row =>
                row!["qualified_item_id"]!.GetValue<string>() == "(O)472"));
            File.WriteAllText(
                insufficientSeedSnapshotPath,
                insufficientSeedSnapshot.ToJsonString(JsonDefaults.Options));
            var insufficientSeedReport = BuildResourceFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                insufficientSeedSnapshotPath,
                targetDateRouteCalibrationPath,
                insufficientSeedUnlockPath,
                insufficientSeedFestivalPath,
                insufficientSeedLocationPath,
                insufficientSeedFacilityPath);
            Require(insufficientSeedReport.Status ==
                        "complete_target_date_resource_inputs_axis_downstream_pending" &&
                    insufficientSeedReport.ResourceInputAxisResolutionComplete &&
                    insufficientSeedReport.ResourceInputAxisResolvedCount == 77 &&
                    insufficientSeedReport.ResourceInputMatchCount == 3 &&
                    insufficientSeedReport.ResourceInputMissCount == 2 &&
                    insufficientSeedReport.ResourceInputNotRequiredCount == 1 &&
                    insufficientSeedReport.BlockedResourceEvidenceCount == 0 &&
                    insufficientSeedReport.Routes.Where(route =>
                        route.ResourceInputsMatchTargetDate == false).All(route =>
                        route.ResourceRequirementKind ==
                            "crop_seed_or_existing_crop" &&
                        route.NonMatchingReasons.Contains(
                            "required_resource_quantity_unavailable:(O)472")),
                "An exact zero-seed fact was not retained as a resolved resource miss.");

            var existingCropCapacity = insufficientSeedSnapshot["state"]!
                ["locations"]!["social_route_date_evidence"]!["value"]!
                ["locations"]!.AsArray()
                .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
                ["cultivation_capacity"]!.AsObject();
            existingCropCapacity["total_prepared_soil_slot_count"] = 3;
            existingCropCapacity["open_prepared_soil_slot_count"] = 1;
            existingCropCapacity["occupied_crop_slot_count"] = 2;
            existingCropCapacity["unresolved_harvest_item_slot_count"] = 0;
            existingCropCapacity["occupied_harvest_items"] = new JsonArray(
                new JsonObject
                {
                    ["harvest_item_qualified_id"] = "(O)24",
                    ["is_garden_pot"] = false,
                    ["slot_count"] = 2
                });
            File.WriteAllText(
                insufficientSeedSnapshotPath,
                insufficientSeedSnapshot.ToJsonString(JsonDefaults.Options));
            var existingCropReport = BuildResourceFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                insufficientSeedSnapshotPath,
                targetDateRouteCalibrationPath,
                insufficientSeedUnlockPath,
                insufficientSeedFestivalPath,
                insufficientSeedLocationPath,
                insufficientSeedFacilityPath);
            var existingCropRoutes = existingCropReport.Routes.Where(route =>
                    route.ResourceRequirementKind ==
                        "crop_seed_or_existing_crop" &&
                    route.ResourceInputsMatchTargetDate == true &&
                    route.InputEvaluations.Single().InputKind ==
                        "existing_target_crop")
                .ToArray();
            var existingCropBeanRoute = existingCropReport.Routes.Single(route =>
                route.ResourceRequirementKind ==
                    "crop_seed_or_existing_crop" &&
                route.InputEvaluations.Single().QualifiedItemId == "(O)473");
            Require(existingCropReport.ResourceInputAxisResolutionComplete &&
                    existingCropReport.ResourceInputMatchCount == 5 &&
                    existingCropReport.ResourceInputMissCount == 0 &&
                    existingCropReport.ResourceInputNotRequiredCount == 3 &&
                    existingCropRoutes.Length == 2 &&
                    existingCropRoutes.All(route =>
                        route.InputEvaluations.Single().InputKind ==
                            "existing_target_crop" &&
                        route.InputEvaluations.Single().RequiredQuantity == 0 &&
                        route.InputEvaluations.Single().AvailableQuantity == 2) &&
                    existingCropBeanRoute.InputEvaluations.Single().InputKind ==
                        "crop_seed" &&
                    existingCropBeanRoute.InputEvaluations.Single()
                        .RequiredQuantity == 1 &&
                    existingCropBeanRoute.InputEvaluations.Single()
                        .AvailableQuantity == 1,
                "An existing target crop incorrectly required a new seed.");

            var missingShopQuoteSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingShopQuoteSnapshot["state"]!["locations"]!.AsObject()
                .Remove("shops");
            File.WriteAllText(
                missingShopQuoteSnapshotPath,
                missingShopQuoteSnapshot.ToJsonString(JsonDefaults.Options));
            var missingShopQuoteCurrency = BuildCurrencyFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingShopQuoteSnapshotPath,
                targetDateRouteCalibrationPath,
                currencyVariantUnlockPath,
                currencyVariantFestivalPath,
                currencyVariantLocationPath,
                currencyVariantFacilityPath,
                currencyVariantResourcePath);
            Require(missingShopQuoteCurrency.Status ==
                        "partial_target_date_currency_budget_axis_blocks" &&
                    missingShopQuoteCurrency.CurrencyAxisResolvedCount == 76 &&
                    missingShopQuoteCurrency.CurrencyBudgetMatchCount == 4 &&
                    missingShopQuoteCurrency.CurrencyBudgetMissCount == 0 &&
                    missingShopQuoteCurrency.CurrencyNotRequiredCount == 4 &&
                    missingShopQuoteCurrency.NotApplicableUpstreamCount == 72 &&
                    missingShopQuoteCurrency.BlockedUpstreamCount == 1 &&
                    missingShopQuoteCurrency.BlockedCurrencyEvidenceCount == 0 &&
                    missingShopQuoteCurrency.Routes.Single(route =>
                        route.CurrencyAxisStatus ==
                            "blocked_upstream_resource_input_axis")
                        .BlockingReasons.Contains(
                            "current_native_shop_quotes_missing_or_incomplete"),
                "Missing native shop quotes did not block the exact shop chain.");

            var missingCurrencySnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingCurrencySnapshot["state"]!["player"]!.AsObject()
                .Remove("shop_currency_balances");
            File.WriteAllText(
                missingCurrencySnapshotPath,
                missingCurrencySnapshot.ToJsonString(JsonDefaults.Options));
            var missingCurrencyReport = BuildCurrencyFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingCurrencySnapshotPath,
                targetDateRouteCalibrationPath,
                currencyVariantUnlockPath,
                currencyVariantFestivalPath,
                currencyVariantLocationPath,
                currencyVariantFacilityPath,
                currencyVariantResourcePath);
            Require(missingCurrencyReport.CurrencyAxisResolvedCount == 76 &&
                    missingCurrencyReport.CurrencyBudgetMatchCount == 4 &&
                    missingCurrencyReport.CurrencyNotRequiredCount == 4 &&
                    missingCurrencyReport.NotApplicableUpstreamCount == 72 &&
                    missingCurrencyReport.BlockedUpstreamCount == 0 &&
                    missingCurrencyReport.BlockedCurrencyEvidenceCount == 1 &&
                    missingCurrencyReport.Routes.Single(route =>
                        route.CurrencyAxisStatus ==
                            "blocked_currency_budget_evidence")
                        .BlockingReasons.Contains(
                            "shop_currency_balances_missing_or_incomplete"),
                "Missing native currency balances did not fail closed per shop route.");

            var insufficientCurrencySnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            insufficientCurrencySnapshot["state"]!["player"]!["money"]!["value"] =
                50;
            var currencyRows = insufficientCurrencySnapshot["state"]!["player"]!
                ["shop_currency_balances"]!["value"]!["rows"]!.AsArray();
            currencyRows.Single(row =>
                row!["currency_id"]!.GetValue<int>() == 0)!["balance"] = 50;
            File.WriteAllText(
                insufficientCurrencySnapshotPath,
                insufficientCurrencySnapshot.ToJsonString(JsonDefaults.Options));
            var insufficientCurrencyReport = BuildCurrencyFixtureChain(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                insufficientCurrencySnapshotPath,
                targetDateRouteCalibrationPath,
                currencyVariantUnlockPath,
                currencyVariantFestivalPath,
                currencyVariantLocationPath,
                currencyVariantFacilityPath,
                currencyVariantResourcePath);
            var insufficientCurrencyRoute = insufficientCurrencyReport.Routes
                .Single(route => route.CurrencyBudgetMatchesTargetDate == false);
            Require(insufficientCurrencyReport.CurrencyAxisResolutionComplete &&
                    insufficientCurrencyReport.CurrencyAxisResolvedCount == 77 &&
                    insufficientCurrencyReport.CurrencyBudgetMatchCount == 4 &&
                    insufficientCurrencyReport.CurrencyBudgetMissCount == 1 &&
                    insufficientCurrencyReport.CurrencyNotRequiredCount == 4 &&
                    insufficientCurrencyReport.BlockedCurrencyEvidenceCount == 0 &&
                    insufficientCurrencyRoute.CurrencyEvaluation is not null &&
                    insufficientCurrencyRoute.CurrencyEvaluation.RequiredAmount ==
                        200 &&
                    insufficientCurrencyRoute.CurrencyEvaluation.AvailableAmount ==
                        50 &&
                    insufficientCurrencyRoute.NonMatchingReasons.Contains(
                        "required_currency_amount_unavailable:money"),
                "An exact insufficient-money fact was not retained as a currency miss.");

            var tamperedTargetDateFestival = JsonNode.Parse(
                File.ReadAllText(targetDateFestivalPath))!.AsObject();
            tamperedTargetDateFestival["routes"]![0]!["upstream_route"]!
                ["source_id"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateFestivalPath,
                tamperedTargetDateFestival.ToJsonString(JsonDefaults.Options));
            var tamperedTargetDateFestivalRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateLocationBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    targetDateUnlockPath,
                    tamperedTargetDateFestivalPath,
                    targetDateUnlockSnapshotPath,
                    targetDateRouteCalibrationPath);
            }
            catch (InvalidDataException)
            {
                tamperedTargetDateFestivalRejected = true;
            }
            Require(tamperedTargetDateFestivalRejected,
                "A tampered target-date festival report did not fail closed.");

            var missingRouteSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingRouteSnapshot["state"]!["locations"]!.AsObject()
                .Remove("social_route_date_evidence");
            File.WriteAllText(
                missingRouteSnapshotPath,
                missingRouteSnapshot.ToJsonString(JsonDefaults.Options));
            var missingRouteUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingRouteSnapshotPath);
            Write(missingRouteUnlockPath, missingRouteUnlock);
            var missingRouteFestival =
                AcquisitionRouteTargetDateFestivalBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    missingRouteUnlockPath,
                    missingRouteSnapshotPath);
            Write(missingRouteFestivalPath, missingRouteFestival);
            var missingRouteReport =
                AcquisitionRouteTargetDateLocationBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    missingRouteUnlockPath,
                    missingRouteFestivalPath,
                    missingRouteSnapshotPath,
                    targetDateRouteCalibrationPath);
            Require(missingRouteReport.Status ==
                        "partial_target_date_location_route_axis_blocks" &&
                    missingRouteReport.RouteOccurrenceInventoryComplete &&
                    !missingRouteReport.LocationRouteAxisResolutionComplete &&
                    !missingRouteReport.TrainingLabelEligible &&
                    missingRouteReport.LocationRouteAxisResolvedCount == 72 &&
                    missingRouteReport.LocationRouteMatchCount == 0 &&
                    missingRouteReport.LocationRouteMissCount == 0 &&
                    missingRouteReport.NotApplicableStaticWindowCount == 72 &&
                    missingRouteReport.BlockedLocationEvidenceCount == 5 &&
                    missingRouteReport.Routes.Where(route =>
                        route.LocationRouteAxisStatus ==
                            "blocked_location_route_evidence").All(route =>
                        route.BlockingReasons.Contains(
                            "location_route_date_evidence_missing")),
                "Missing transparent route evidence did not fail closed per active route.");

            WriteStageOneCollectionSnapshot(
                deniedRouteSnapshotPath,
                "denied-route-state",
                fish,
                totalDays: 0,
                timeOfDay: 700,
                shopDoorAllowed: false);
            var deniedRouteUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                deniedRouteSnapshotPath);
            Write(deniedRouteUnlockPath, deniedRouteUnlock);
            var deniedRouteFestival =
                AcquisitionRouteTargetDateFestivalBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    deniedRouteUnlockPath,
                    deniedRouteSnapshotPath);
            Write(deniedRouteFestivalPath, deniedRouteFestival);
            var deniedRouteReport =
                AcquisitionRouteTargetDateLocationBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    deniedRouteUnlockPath,
                    deniedRouteFestivalPath,
                    deniedRouteSnapshotPath,
                    targetDateRouteCalibrationPath);
            var deniedShop = deniedRouteReport.Routes.Single(route =>
                route.UpstreamRoute.UpstreamRoute.RequirementId ==
                    "community_center:bundle:Pantry/5" &&
                route.UpstreamRoute.UpstreamRoute.RouteKind == "sells");
            Require(deniedRouteReport.Status ==
                        "complete_target_date_location_route_axis_downstream_pending" &&
                    deniedRouteReport.LocationRouteAxisResolutionComplete &&
                    deniedRouteReport.LocationRouteAxisResolvedCount == 77 &&
                    deniedRouteReport.LocationRouteMatchCount == 4 &&
                    deniedRouteReport.LocationRouteMissCount == 1 &&
                    deniedRouteReport.BlockedLocationEvidenceCount == 0 &&
                    deniedShop.LocationRouteAxisStatus ==
                        "resolved_location_route_miss" &&
                    deniedShop.LocationRouteMatchesTargetDate == false &&
                    deniedShop.TargetEvaluations.Single().Status ==
                        "resolved_route_unavailable_on_target_date" &&
                    deniedShop.BlockingReasons.Length == 0,
                "A complete negative route fact was misclassified as missing evidence.");

            var missingCalendarSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingCalendarSnapshot["state"]!["world_progress"]!.AsObject()
                .Remove("game_state_query_calendar_state");
            File.WriteAllText(
                missingCalendarStateSnapshotPath,
                missingCalendarSnapshot.ToJsonString(JsonDefaults.Options));
            var missingCalendarUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingCalendarStateSnapshotPath);
            Write(missingCalendarStateUnlockPath, missingCalendarUnlock);
            var missingCalendarReport =
                AcquisitionRouteTargetDateFestivalBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    missingCalendarStateUnlockPath,
                    missingCalendarStateSnapshotPath);
            Require(missingCalendarReport.Status ==
                        "partial_target_date_festival_axis_blocks" &&
                    !missingCalendarReport.CalendarConditionAxisResolutionComplete &&
                    missingCalendarReport.CalendarConditionAxisResolvedCount == 76 &&
                    missingCalendarReport.CalendarConditionMatchCount == 4 &&
                    missingCalendarReport.BlockedCalendarEvidenceCount == 1 &&
                    missingCalendarReport.Routes.Single(route =>
                        route.UpstreamRoute.RequirementId ==
                            "community_center:bundle:Pantry/5" &&
                        route.UpstreamRoute.RouteKind == "sells").BlockingReasons
                        .Contains("game_state_query_calendar_state_missing"),
                "Missing transparent calendar state did not fail closed per route.");

            var tamperedTargetDateUnlock = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockPath))!.AsObject();
            tamperedTargetDateUnlock["routes"]![0]!["source_id"] = "tampered";
            File.WriteAllText(
                tamperedTargetDateUnlockPath,
                tamperedTargetDateUnlock.ToJsonString(JsonDefaults.Options));
            var tamperedTargetDateUnlockRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateFestivalBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    tamperedTargetDateUnlockPath,
                    targetDateUnlockSnapshotPath);
            }
            catch (InvalidDataException)
            {
                tamperedTargetDateUnlockRejected = true;
            }
            Require(tamperedTargetDateUnlockRejected,
                "A tampered target-date unlock report did not fail closed.");

            var missingUnlockSnapshot = JsonNode.Parse(
                File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
            missingUnlockSnapshot["state"]!["world_progress"]!.AsObject()
                .Remove("game_state_query_unlock_state");
            File.WriteAllText(
                missingUnlockStateSnapshotPath,
                missingUnlockSnapshot.ToJsonString(JsonDefaults.Options));
            var missingUnlockReport =
                AcquisitionRouteTargetDateUnlockBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    missingUnlockStateSnapshotPath);
            Require(missingUnlockReport.Status ==
                        "partial_target_date_unlock_axis_blocks" &&
                    !missingUnlockReport.UnlockAxisResolutionComplete &&
                    missingUnlockReport.BlockedUnlockEvidenceCount == 1 &&
                    missingUnlockReport.UnlockStateMatchCount == 4 &&
                    missingUnlockReport.Routes.Single(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/5" &&
                        route.RouteKind == "sells").BlockingReasons.Contains(
                            "game_state_query_unlock_state_missing"),
                "Missing transparent unlock state did not fail closed per route.");

            WriteStageOneCollectionSnapshot(
                mismatchedUnlockDateSnapshotPath,
                "mismatched-target-date-unlock-state",
                fish,
                totalDays: 1);
            var mismatchedUnlockDateRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateUnlockBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    targetDateCalendarPath,
                    mismatchedUnlockDateSnapshotPath);
            }
            catch (InvalidDataException)
            {
                mismatchedUnlockDateRejected = true;
            }
            Require(mismatchedUnlockDateRejected,
                "A snapshot from a different day did not fail closed.");

            var tamperedTargetDatePath = Path.Combine(
                root,
                "tampered-target-date-calendar.json");
            var tamperedTargetDate = JsonNode.Parse(
                File.ReadAllText(targetDateCalendarPath))!.AsObject();
            tamperedTargetDate["routes"]![0]!["source_id"] = "tampered";
            File.WriteAllText(
                tamperedTargetDatePath,
                tamperedTargetDate.ToJsonString(JsonDefaults.Options));
            var tamperedTargetDateRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateUnlockBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    tamperedTargetDatePath,
                    targetDateUnlockSnapshotPath);
            }
            catch (InvalidDataException)
            {
                tamperedTargetDateRejected = true;
            }
            Require(tamperedTargetDateRejected,
                "A tampered target-date calendar did not fail closed.");

            var secondYearCalendar = AcquisitionRouteTargetDateCalendarBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                112);
            var secondYearShop = secondYearCalendar.Routes.Single(route =>
                route.RequirementId == "community_center:bundle:Pantry/5" &&
                route.RouteKind == "sells");
            Require(secondYearCalendar.StaticWindowMatchCount == 5 &&
                    secondYearShop.CalendarAxisResolved &&
                    !secondYearShop.StaticWindowMatchesTargetDate &&
                    secondYearShop.CalendarAxisStatus ==
                        "resolved_target_date_outside_static_window" &&
                    secondYearShop.MatchingWindows.Length == 0 &&
                    secondYearShop.PendingDynamicConditions.Length == 0,
                "Negated shop year condition did not reject the second year.");

            var tamperedRouteCalendarPath = Path.Combine(
                root,
                "tampered-route-calendar-resolution.json");
            var tamperedRouteCalendar = JsonNode.Parse(
                File.ReadAllText(routeCalendarPath))!.AsObject();
            tamperedRouteCalendar["routes"]![0]!["source_id"] = "tampered";
            File.WriteAllText(
                tamperedRouteCalendarPath,
                tamperedRouteCalendar.ToJsonString(JsonDefaults.Options));
            var tamperedRouteCalendarRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateCalendarBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    tamperedRouteCalendarPath,
                    0);
            }
            catch (InvalidDataException)
            {
                tamperedRouteCalendarRejected = true;
            }
            Require(tamperedRouteCalendarRejected,
                "A tampered static calendar report did not fail closed.");

            var outOfHorizonTargetRejected = false;
            try
            {
                _ = AcquisitionRouteTargetDateCalendarBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    routeCalendarPath,
                    routeCalendar.DeadlineTotalDayExclusive);
            }
            catch (InvalidDataException)
            {
                outOfHorizonTargetRejected = true;
            }
            Require(outOfHorizonTargetRejected,
                "An out-of-horizon target date did not fail closed.");

            var originalCropEvidence = File.ReadAllText(cropsPath);
            var staleCropEvidenceRejected = false;
            try
            {
                File.AppendAllText(cropsPath, " ");
                _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath);
            }
            catch (InvalidDataException)
            {
                staleCropEvidenceRejected = true;
            }
            finally
            {
                File.WriteAllText(cropsPath, originalCropEvidence);
            }
            Require(staleCropEvidenceRejected,
                "Stale runtime Data/Crops evidence did not fail closed.");

            var originalShopEvidence = File.ReadAllText(shopsPath);
            var staleShopEvidenceRejected = false;
            try
            {
                File.AppendAllText(shopsPath, " ");
                _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath);
            }
            catch (InvalidDataException)
            {
                staleShopEvidenceRejected = true;
            }
            finally
            {
                File.WriteAllText(shopsPath, originalShopEvidence);
            }
            Require(staleShopEvidenceRejected,
                "Stale runtime Data/Shops evidence did not fail closed.");

            var originalAccessEvidence = File.ReadAllText(accessConstraintPath);
            var staleAccessEvidenceRejected = false;
            try
            {
                File.AppendAllText(accessConstraintPath, " ");
                _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath);
            }
            catch (InvalidDataException)
            {
                staleAccessEvidenceRejected = true;
            }
            finally
            {
                File.WriteAllText(accessConstraintPath, originalAccessEvidence);
            }
            Require(staleAccessEvidenceRejected,
                "Stale access constraint evidence did not fail closed.");

            var unknownCalendar = NativeCalendarConstraintNormalizer.Normalize(
                NativeCalendarConstraintNormalizer.AllSeasons,
                NativeCalendarConstraintNormalizer.AllDay,
                NativeCalendarConstraintNormalizer.AllWeatherModes,
                string.Empty,
                "UNKNOWN_NATIVE_PREDICATE Current value");
            Require(unknownCalendar.ParseStatus == "unparsed_dynamic_condition" &&
                    unknownCalendar.StaticCalendarPossible &&
                    unknownCalendar.UnparsedConditions.SequenceEqual(
                        new[] { "UNKNOWN_NATIVE_PREDICATE Current value" }),
                "Unknown native calendar predicates did not remain explicit.");

            var tamperedWindowsPath = Path.Combine(root, "tampered-master-angler-windows.json");
            var tamperedWindows = JsonNode.Parse(File.ReadAllText(windowsPath))!.AsObject();
            tamperedWindows["catalog_sha256"] = new string('0', 64);
            File.WriteAllText(
                tamperedWindowsPath,
                tamperedWindows.ToJsonString(JsonDefaults.Options));
            var tamperedCalendarSourceRejected = false;
            try
            {
                _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    tamperedWindowsPath);
            }
            catch (InvalidDataException)
            {
                tamperedCalendarSourceRejected = true;
            }
            Require(tamperedCalendarSourceRejected,
                "A tampered calendar source chain did not fail closed.");

            var tamperedWindowContentPath = Path.Combine(
                root,
                "tampered-master-angler-window-content.json");
            var tamperedWindowContent = JsonNode.Parse(File.ReadAllText(windowsPath))!
                .AsObject();
            tamperedWindowContent["species"]![0]!["windows"]![0]!["source_key"] =
                "Beach:999";
            File.WriteAllText(
                tamperedWindowContentPath,
                tamperedWindowContent.ToJsonString(JsonDefaults.Options));
            var tamperedWindowContentRejected = false;
            try
            {
                _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    tamperedWindowContentPath);
            }
            catch (InvalidDataException)
            {
                tamperedWindowContentRejected = true;
            }
            Require(tamperedWindowContentRejected,
                "Tampered compiled calendar windows did not fail closed.");

            Write(calibrationPath, StageOneRouteTimingCalibration());
            WriteStageOneCollectionSnapshot(snapshotPath, stateHash, fish);
        }
    }
}
