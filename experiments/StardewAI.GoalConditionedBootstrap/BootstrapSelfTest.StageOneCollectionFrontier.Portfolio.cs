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
        private AcquisitionRouteTargetDateUnlock VerifyPortfolio(
            AcquisitionRouteTargetDateOpportunityCostReport targetDateOpportunityCost,
            AcquisitionRouteTargetDateOpportunityCost targetDateOpportunityShop,
            AcquisitionRouteTargetDateOpportunityCost targetDateOpportunityFish)
        {
            var portfolioRequirement = TargetDateRequirementRoute(
                targetDateOpportunityShop);
            var portfolioProposal = new AcquisitionRoutePortfolioProposal
            {
                ProposalId = "self-test-shop-route",
                GoalId = targetDateOpportunityCost.GoalId,
                SnapshotStateHash = targetDateOpportunityCost.SnapshotStateHash,
                ExpectedLedgerRevision = 0,
                ScopedRequirements = new[]
                {
                    new AcquisitionRoutePortfolioRequirementScope(
                        portfolioRequirement.RequirementSetId,
                        portfolioRequirement.RequirementId)
                },
                SelectedRouteOccurrenceIds = new[]
                {
                    targetDateOpportunityShop.RouteOccurrenceId
                }
            };
            Write(targetDatePortfolioProposalPath, portfolioProposal);
            AcquisitionRoutePortfolioInputs PortfolioInputs(
                string? proposalPath = null) => new()
                {
                    RequirementInventoryPath = inventoryPath,
                    AcquisitionLoweringPath = loweringPath,
                    MasterAnglerWindowsPath = windowsPath,
                    CalendarResolutionPath = routeCalendarPath,
                    TargetDateCalendarPath = targetDateCalendarPath,
                    TargetDateUnlockPath = targetDateUnlockPath,
                    TargetDateFestivalPath = targetDateFestivalPath,
                    TargetDateLocationPath = targetDateLocationPath,
                    TargetDateFacilityPath = targetDateFacilityPath,
                    TargetDateResourcePath = targetDateResourcePath,
                    TargetDateCurrencyPath = targetDateCurrencyPath,
                    TargetDateReservationPath = targetDateReservationPath,
                    TargetDateProcessingPath = targetDateProcessingPath,
                    TargetDateFishingProbabilityPath =
                    targetDateFishingProbabilityPath,
                    TargetDateStochasticRetryPath = targetDateStochasticRetryPath,
                    TargetDateDailyTimeEnergyPath = targetDateDailyTimeEnergyPath,
                    TargetDateOpportunityCostPath = targetDateOpportunityCostPath,
                    FishingForecastManifestPath = fishingForecastManifestPath,
                    StrategyLedgerPath = strategyLedgerPath,
                    SnapshotPath = targetDateUnlockSnapshotPath,
                    RouteTimingCalibrationPath = targetDateRouteCalibrationPath,
                    ProposalPath = proposalPath ?? targetDatePortfolioProposalPath
                };
            var preferenceRequest =
                new AcquisitionRoutePortfolioTeacherPreferenceRequest
                {
                    RequestId = "self-test-shop-teacher",
                    GoalId = targetDateOpportunityCost.GoalId,
                    SnapshotStateHash =
                        targetDateOpportunityCost.SnapshotStateHash,
                    ExpectedLedgerRevision = 0,
                    ScopedRequirements = new[]
                    {
                        new AcquisitionRoutePortfolioRequirementScope(
                            portfolioRequirement.RequirementSetId,
                            portfolioRequirement.RequirementId)
                    }
                };
            Write(targetDatePortfolioPreferenceRequestPath, preferenceRequest);
            var portfolioPreference =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder.Build(
                    PortfolioInputs(),
                    targetDatePortfolioPreferenceRequestPath);
            Write(targetDatePortfolioPreferencePath, portfolioPreference);
            Require(portfolioPreference.Status ==
                        "ready_unique_strict_pareto_portfolio_teacher_preference" &&
                    portfolioPreference.CandidateDenominatorComplete &&
                    portfolioPreference.CandidateDenominatorCount == 1 &&
                    portfolioPreference.AdmittedCandidateCount == 1 &&
                    portfolioPreference.ParetoFrontierCount == 1 &&
                    portfolioPreference.TeacherPreferenceLabelEligible &&
                    !portfolioPreference.FormalTrainingAuthorized &&
                    !portfolioPreference.UsesLearnerRankOrScore &&
                    !portfolioPreference
                        .EmitsNegativeLabelsForUnavailablePortfolios &&
                    portfolioPreference.SelectedProposal is not null &&
                    portfolioPreference.SelectedAdmission is not null &&
                    portfolioPreference.SelectedProposal
                        .SelectedRouteOccurrenceIds.SequenceEqual(
                            new[]
                            {
                                targetDateOpportunityShop.RouteOccurrenceId
                            },
                            StringComparer.Ordinal),
                "Target-date portfolio Teacher preference drifted.");
            Write(
                targetDatePortfolioTeacherProposalPath,
                portfolioPreference.SelectedProposal!);
            Write(
                targetDatePortfolioTeacherAdmissionPath,
                portfolioPreference.SelectedAdmission!);
            var rebuiltTeacherAdmission = AcquisitionRoutePortfolioBuilder.Build(
                PortfolioInputs(targetDatePortfolioTeacherProposalPath));
            Require(JsonSerializer.Serialize(
                        rebuiltTeacherAdmission,
                        JsonDefaults.Options) == JsonSerializer.Serialize(
                        portfolioPreference.SelectedAdmission,
                        JsonDefaults.Options),
                "Written Teacher portfolio proposal did not rebuild its admission.");
            var portfolio = AcquisitionRoutePortfolioBuilder.Build(
                PortfolioInputs());
            Require(portfolio.Status ==
                        "admitted_pending_atomic_reservation_commit" &&
                    portfolio.SelectionRulesSatisfied &&
                    portfolio.AllRoutesOnCompleteParetoFrontier &&
                    portfolio.AtomicCommitRequired &&
                    portfolio.AtomicCommitPreflightPassed &&
                    portfolio.PortfolioAdmissionReady &&
                    !portfolio.FormalTrainingAuthorized &&
                    portfolio.AggregateCostVector is not null &&
                    portfolio.AtomicCommitRequest is not null &&
                    portfolio.AtomicCommitRequest.MaterialClaims.Length == 1 &&
                    portfolio.AtomicCommitRequest.CurrencyClaims.Length == 1,
                "Target-date route portfolio admission drifted.");
            Write(targetDatePortfolioAdmissionPath, portfolio);
            var teacherPortfolio = portfolioPreference.SelectedAdmission!;
            var portfolioSnapshot = CurrentTeacherFrontierSupport.Read<
                SnapshotEnvelope>(
                targetDateUnlockSnapshotPath,
                "Portfolio self-test snapshot");
            var portfolioBaseLedger = CurrentTeacherFrontierSupport.Read<
                StrategyCommitmentLedger>(
                strategyLedgerPath,
                "Portfolio self-test base ledger");
            var portfolioCommit = new ReservationPortfolioLedgerService().Commit(
                portfolioBaseLedger,
                portfolioSnapshot,
                teacherPortfolio.AtomicCommitRequest!,
                "2026-09-21T00:00:00Z");
            Require(portfolioCommit.Accepted &&
                    portfolioCommit.Ledger is not null,
                "Portfolio self-test atomic commit failed.");
            Write(targetDatePortfolioCommitResultPath, portfolioCommit);
            Write(targetDatePortfolioCommittedLedgerPath, portfolioCommit.Ledger!);
            var portfolioReceipt =
                AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                    PortfolioInputs(targetDatePortfolioTeacherProposalPath),
                    targetDatePortfolioTeacherAdmissionPath,
                    targetDatePortfolioCommittedLedgerPath,
                    targetDatePortfolioCommitResultPath);
            Require(portfolioReceipt.Status ==
                        "verified_atomic_reservation_portfolio_commit" &&
                    portfolioReceipt.AtomicMutationObserved &&
                    portfolioReceipt.ExactActiveClaimSetVerified &&
                    portfolioReceipt.SingleRevisionCommitVerified &&
                    portfolioReceipt.PortfolioCommitVerified &&
                    !portfolioReceipt.FormalTrainingAuthorized &&
                    portfolioReceipt.CommittedLedgerRevision ==
                        portfolioReceipt.BaseLedgerRevision + 1,
                "Atomic reservation portfolio receipt drifted.");
            Write(targetDatePortfolioCommitReceiptPath, portfolioReceipt);
            var tamperedPortfolioLedger = JsonSerializer.Deserialize<
                StrategyCommitmentLedger>(
                JsonSerializer.Serialize(
                    portfolioCommit.Ledger,
                    JsonDefaults.Options),
                JsonDefaults.Options)!;
            tamperedPortfolioLedger.MaterialReservations[0].Quantity--;
            Write(targetDatePortfolioTamperedLedgerPath, tamperedPortfolioLedger);
            var tamperedPortfolioReceipt =
                AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                    PortfolioInputs(targetDatePortfolioTeacherProposalPath),
                    targetDatePortfolioTeacherAdmissionPath,
                    targetDatePortfolioTamperedLedgerPath,
                    targetDatePortfolioCommitResultPath);
            Require(!tamperedPortfolioReceipt.PortfolioCommitVerified &&
                    tamperedPortfolioReceipt.BlockingReasons.Contains(
                        "atomic_commit_result_mismatch") &&
                    tamperedPortfolioReceipt.BlockingReasons.Any(reason =>
                        reason.StartsWith(
                            "material_claim_not_exactly_committed:",
                        StringComparison.Ordinal)),
                "A tampered committed portfolio ledger was accepted.");
            var coupledTamperedLedger = JsonSerializer.Deserialize<
                StrategyCommitmentLedger>(
                JsonSerializer.Serialize(
                    portfolioCommit.Ledger,
                    JsonDefaults.Options),
                JsonDefaults.Options)!;
            var unrelatedReservation = JsonSerializer.Deserialize<
                MaterialReservation>(
                JsonSerializer.Serialize(
                    coupledTamperedLedger.MaterialReservations[0],
                    JsonDefaults.Options),
                JsonDefaults.Options)!;
            unrelatedReservation.ReservationId =
                "reservation:coupled-tampered-unrelated";
            unrelatedReservation.SourceDecisionId =
                "unrelated-tampered-decision";
            coupledTamperedLedger.MaterialReservations =
                coupledTamperedLedger.MaterialReservations
                    .Append(unrelatedReservation)
                    .ToArray();
            var coupledTamperedResult = JsonSerializer.Deserialize<
                ReservationPortfolioCommitResult>(
                JsonSerializer.Serialize(
                    portfolioCommit,
                    JsonDefaults.Options),
                JsonDefaults.Options)!;
            coupledTamperedResult.Ledger = coupledTamperedLedger;
            Write(
                targetDatePortfolioCoupledTamperedLedgerPath,
                coupledTamperedLedger);
            Write(
                targetDatePortfolioCoupledTamperedResultPath,
                coupledTamperedResult);
            var coupledTamperedReceipt =
                AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                    PortfolioInputs(targetDatePortfolioTeacherProposalPath),
                    targetDatePortfolioTeacherAdmissionPath,
                    targetDatePortfolioCoupledTamperedLedgerPath,
                    targetDatePortfolioCoupledTamperedResultPath);
            Require(!coupledTamperedReceipt.PortfolioCommitVerified &&
                    coupledTamperedReceipt.BlockingReasons.Contains(
                        "atomic_commit_exact_replay_mismatch"),
                "Coupled result/ledger tampering bypassed exact commit replay.");
            portfolioProposal.ScopedRequirements = new[]
            {
                new AcquisitionRoutePortfolioRequirementScope(
                    portfolioRequirement.RequirementSetId,
                    portfolioRequirement.RequirementId + ":wrong")
            };
            Write(targetDatePortfolioProposalPath, portfolioProposal);
            var wrongScopePortfolio = AcquisitionRoutePortfolioBuilder.Build(
                PortfolioInputs());
            Require(!wrongScopePortfolio.PortfolioAdmissionReady &&
                    wrongScopePortfolio.BlockingReasons.Any(reason =>
                        reason.StartsWith(
                            "selected_route_outside_requirement_scope:",
                            StringComparison.Ordinal)),
                "A route outside the explicit portfolio scope was admitted.");
            portfolioProposal.ScopedRequirements = new[]
            {
                new AcquisitionRoutePortfolioRequirementScope(
                    portfolioRequirement.RequirementSetId,
                    portfolioRequirement.RequirementId)
            };
            Write(targetDatePortfolioProposalPath, portfolioProposal);
            portfolioProposal.PriorRolloutCheckpointSha256 = new string('a', 64);
            portfolioProposal.CompletedAlternatives = new[]
            {
                new AcquisitionRoutePortfolioCompletedAlternatives(
                    portfolioRequirement.RequirementSetId,
                    portfolioRequirement.RequirementId,
                    new[] { portfolioRequirement.AlternativeIndex })
            };
            Write(targetDatePortfolioProposalPath, portfolioProposal);
            var forgedContinuationPortfolio =
                AcquisitionRoutePortfolioBuilder.Build(PortfolioInputs());
            Require(!forgedContinuationPortfolio.PortfolioAdmissionReady &&
                    forgedContinuationPortfolio.BlockingReasons.Contains(
                        "unverified_portfolio_continuation_evidence",
                        StringComparer.Ordinal),
                "Caller-authored completed alternatives bypassed continuation proof.");
            portfolioProposal.PriorRolloutCheckpointSha256 = string.Empty;
            portfolioProposal.CompletedAlternatives =
                Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>();
            Write(targetDatePortfolioProposalPath, portfolioProposal);

            var fishRequirement = TargetDateRequirementRoute(
                targetDateOpportunityFish);
            var existingProposal = new AcquisitionRoutePortfolioProposal
            {
                ProposalId = "self-test-fish-route-no-reservation",
                GoalId = targetDateOpportunityCost.GoalId,
                SnapshotStateHash = targetDateOpportunityCost.SnapshotStateHash,
                ExpectedLedgerRevision = 0,
                ScopedRequirements = new[]
                {
                    new AcquisitionRoutePortfolioRequirementScope(
                        fishRequirement.RequirementSetId,
                        fishRequirement.RequirementId)
                },
                SelectedRouteOccurrenceIds = new[]
                {
                    targetDateOpportunityFish.RouteOccurrenceId
                }
            };
            Write(targetDatePortfolioProposalPath, existingProposal);
            var existingPortfolio = AcquisitionRoutePortfolioBuilder.Build(
                PortfolioInputs());
            Require(existingPortfolio.Status ==
                        "admitted_pending_atomic_reservation_commit" &&
                    existingPortfolio.AtomicCommitRequired &&
                    existingPortfolio.AtomicCommitRequest is
                    {
                        MaterialClaims.Length: 0,
                        CurrencyClaims.Length: 0,
                        ReleaseReservationIds.Length: 0
                    },
                "No-reservation route did not require an ownership marker commit.");
            Write(targetDatePortfolioExistingAdmissionPath, existingPortfolio);
            var existingCommit = new ReservationPortfolioLedgerService().Commit(
                portfolioBaseLedger,
                portfolioSnapshot,
                existingPortfolio.AtomicCommitRequest!,
                "2026-09-21T00:00:30Z");
            Require(existingCommit.Accepted && existingCommit.Ledger is not null,
                "No-reservation portfolio marker commit failed.");
            var existingCommitResultPath = Path.Combine(
                root,
                "target-date-portfolio-existing-commit-result.json");
            var existingCommittedLedgerPath = Path.Combine(
                root,
                "target-date-portfolio-existing-committed-ledger.json");
            Write(existingCommitResultPath, existingCommit);
            Write(existingCommittedLedgerPath, existingCommit.Ledger!);
            var existingReceipt =
                AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
                    PortfolioInputs(),
                    targetDatePortfolioExistingAdmissionPath,
                    existingCommittedLedgerPath,
                    existingCommitResultPath);
            Require(existingReceipt.Status ==
                        "verified_atomic_reservation_portfolio_commit" &&
                    existingReceipt.AtomicMutationObserved &&
                    existingReceipt.ExactActiveClaimSetVerified &&
                    existingReceipt.SingleRevisionCommitVerified &&
                    existingReceipt.PortfolioCommitVerified &&
                    existingReceipt.ActiveReservationIds.Length == 0 &&
                    existingReceipt.CommittedLedgerRevision == 1,
                "No-reservation portfolio marker receipt drifted.");
            Write(targetDatePortfolioProposalPath, portfolioProposal);

            var targetDateExecutionInputs =
                new AcquisitionRouteExecutionBindingInputs
                {
                    RequirementInventoryPath = inventoryPath,
                    AcquisitionLoweringPath = loweringPath,
                    MasterAnglerWindowsPath = windowsPath,
                    CalendarResolutionPath = routeCalendarPath,
                    TargetDateCalendarPath = targetDateCalendarPath,
                    TargetDateUnlockPath = targetDateUnlockPath,
                    TargetDateFestivalPath = targetDateFestivalPath,
                    TargetDateLocationPath = targetDateLocationPath,
                    TargetDateFacilityPath = targetDateFacilityPath,
                    TargetDateResourcePath = targetDateResourcePath,
                    TargetDateCurrencyPath = targetDateCurrencyPath,
                    TargetDateReservationPath = targetDateReservationPath,
                    TargetDateProcessingPath = targetDateProcessingPath,
                    TargetDateFishingProbabilityPath =
                        targetDateFishingProbabilityPath,
                    TargetDateStochasticRetryPath =
                        targetDateStochasticRetryPath,
                    TargetDateDailyTimeEnergyPath =
                        targetDateDailyTimeEnergyPath,
                    TargetDateOpportunityCostPath = targetDateOpportunityCostPath,
                    FishingForecastManifestPath = fishingForecastManifestPath,
                    StrategyLedgerPath = strategyLedgerPath,
                    BeforeSnapshotPath = targetDateUnlockSnapshotPath,
                    RouteTimingCalibrationPath = targetDateRouteCalibrationPath,
                    PortfolioProposalPath =
                        targetDatePortfolioTeacherProposalPath,
                    PortfolioAdmissionPath =
                        targetDatePortfolioTeacherAdmissionPath,
                    PortfolioPreferenceRequestPath =
                        targetDatePortfolioPreferenceRequestPath,
                    PortfolioTeacherPreferencePath =
                        targetDatePortfolioPreferencePath,
                    PortfolioCommitReceiptPath =
                        targetDatePortfolioCommitReceiptPath,
                    CommittedStrategyLedgerPath =
                        targetDatePortfolioCommittedLedgerPath,
                    PortfolioCommitResultPath =
                        targetDatePortfolioCommitResultPath,
                    ActionQueuePath = targetDateRouteQueuePath,
                    RouteOccurrenceId = targetDateOpportunityShop.RouteOccurrenceId
                };
            Write(
                Path.Combine(root, "target-date-execution-inputs.json"),
                targetDateExecutionInputs);
            VerifyTargetDateFreshTerminalReceipt(
                targetDateExecutionInputs,
                targetDateExecutionBindingPath,
                targetDateAfterSnapshotPath,
                targetDateExecutionReceiptPath,
                targetDateInsufficientAfterSnapshotPath);
            var machineCapacityCoverage =
                VerifyMachineCapacitySupportingTransitionProof(
                    targetDateExecutionInputs);
            var machineInputLoadCoverage =
                VerifyMachineInputLoadSupportingTransitionProof(
                    targetDateExecutionInputs);
            var machineMaterialTransferCoverage =
                VerifyMachineMaterialTransferSupportingTransitionProof(
                    targetDateExecutionInputs);
            var machineInputPurchaseCoverage =
                VerifyMachineInputPurchaseSupportingTransitionProof(
                    targetDateExecutionInputs);
            var cropPlantingCoverage =
                VerifyCropPlantingSupportingTransitionProof(
                    targetDateExecutionInputs);
            VerifySupportingTransitionTerminalCoverageMatrix(
                cropPlantingCoverage,
                machineInputLoadCoverage,
                machineMaterialTransferCoverage,
                machineInputPurchaseCoverage,
                machineCapacityCoverage);
            VerifyTargetDatePortfolioContinuationFixture(
                targetDateExecutionInputs,
                targetDateOpportunityShop.RouteOccurrenceId,
                targetDateOpportunityFish.RouteOccurrenceId);

            return portfolioRequirement;
        }
    }
}
