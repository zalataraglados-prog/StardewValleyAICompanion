using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineCapacitySupportingTransitionProof(
        AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "machine-capacity-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var priorRoot = Path.Combine(root, "placement-support");
        Directory.CreateDirectory(priorRoot);
        var beforeSnapshotPath = Path.Combine(
            priorRoot,
            "before-snapshot.json");
        var before = WriteMachineCapacityProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(priorRoot, "strategy-ledger.json");
        var intentId = "machine-support:acquisition-route:" +
            authority.MachineRouteOccurrenceId + ":(BC)12";
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.machine-capacity-file-backed",
            SaveId = before.SaveId.Value!,
            PlayerId = before.PlayerId.Value!,
            Revision = 3,
            UpdatedAt = "2026-09-27T03:00:00Z",
            SourceStateHash = before.StateHash,
            MachineSupportIntents = Array.Empty<MachineSupportIntent>()
        };
        Write(baseLedgerPath, baseLedger);
        var priorProposalPath = Path.Combine(priorRoot, "unused-proposal.json");
        var priorPortfolioInputs = BuildTargetDatePortfolioInputs(
            authority.Template,
            beforeSnapshotPath,
            baseLedgerPath,
            priorProposalPath,
            priorRoot,
            targetTotalDay: 0);
        var priorOpportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            priorPortfolioInputs.TargetDateOpportunityCostPath,
            "Machine-capacity proof prior opportunity cost");
        var priorMachineRoute = priorOpportunity.Routes.Single(route =>
            route.RouteOccurrenceId == authority.MachineRouteOccurrenceId);
        var priorReservationRoute = priorMachineRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var priorLocationRoute = priorReservationRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute;
        Require(priorMachineRoute.OpportunityCostMatchesTargetDate is null &&
                priorMachineRoute.CostVector is null &&
                priorLocationRoute.LocationRouteMatchesTargetDate == false &&
                priorLocationRoute.NonMatchingReasons.SequenceEqual(
                    new[]
                    {
                        "matching_machine_runtime_location_not_present"
                    },
                    StringComparer.Ordinal),
            "Missing machine capacity did not produce the exact nonterminal " +
            "capacity prerequisite: " +
            string.Join(",", priorLocationRoute.BlockingReasons.Concat(
                priorLocationRoute.NonMatchingReasons)));

        var rankingPath = Path.Combine(priorRoot, "ranking.json");
        Write(rankingPath, CollectionRanking(before.StateHash));
        var supportInputs = MachineCapacityProofSupportInputs(
            priorPortfolioInputs,
            rankingPath,
            authority.MachineRouteOccurrenceId);
        var supportRequestPath = Path.Combine(
            priorRoot,
            "support-request.json");
        var supportRequest = AcquisitionRouteSupportingTransitionRequestBuilder
            .Build(supportInputs);
        Require(supportRequest.SupportRequestReady &&
                supportRequest.SupportTransitionKind ==
                    "machine_capacity_establishment" &&
                supportRequest.MachineSupportIntentId == intentId &&
                supportRequest.MachineSupportIntentStage ==
                    MachineSupportIntentStages.PlacementBound,
            "File-backed machine placement request was not admitted: " +
            string.Join(",", supportRequest.BlockingReasons));
        Write(supportRequestPath, supportRequest);

        var service = new ReservationPortfolioLedgerService();
        var commitResult = service.Commit(
            baseLedger,
            before,
            supportRequest.AtomicCommitRequest!,
            "2026-09-27T03:00:01Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "File-backed machine placement commit failed: " +
            string.Join(",", commitResult.Errors));
        var commitResultPath = Path.Combine(priorRoot, "commit-result.json");
        var committedLedgerPath = Path.Combine(
            priorRoot,
            "committed-ledger.json");
        Write(commitResultPath, commitResult);
        Write(committedLedgerPath, commitResult.Ledger!);
        var commitReceiptPath = Path.Combine(priorRoot, "commit-receipt.json");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.Build(
                supportInputs,
                supportRequestPath,
                committedLedgerPath,
                commitResultPath);
        Require(commitReceipt.SupportReservationCommitVerified,
            "File-backed machine placement commit receipt failed: " +
            string.Join(",", commitReceipt.BlockingReasons));
        Write(commitReceiptPath, commitReceipt);

        var compilationPath = Path.Combine(priorRoot, "compilation.json");
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.Build(
                supportInputs,
                supportRequestPath,
                commitReceiptPath,
                committedLedgerPath,
                commitResultPath);
        Write(compilationPath, compilation);
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Select(row => row.OptionId)
                    .SequenceEqual(
                        new[]
                        {
                            "executor.move_to_tile",
                            "executor.place_machine"
                        },
                        StringComparer.Ordinal) == true,
            "File-backed machine placement did not compile through the shared queue: " +
            string.Join(",", compilation.BlockingReasons));

        var afterSnapshotPath = Path.Combine(
            priorRoot,
            "after-snapshot.json");
        var after = WriteMachineCapacityProofAfterSnapshot(
            beforeSnapshotPath,
            afterSnapshotPath);
        var executionReceiptPath = Path.Combine(
            priorRoot,
            "execution-receipt.json");
        var executionReceipt = MachineCapacityPlacementExecutionReceipt(
            compilation,
            before,
            after);
        Write(executionReceiptPath, executionReceipt);
        var transitionReceiptPath = Path.Combine(
            priorRoot,
            "supporting-transition-receipt.json");
        var transitionReceipt =
            AcquisitionRouteSupportingTransitionReceiptBuilder.Build(
                compilationPath,
                beforeSnapshotPath,
                executionReceiptPath,
                afterSnapshotPath,
                executionReceipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transitionReceipt.SupportingTransitionVerified &&
                transitionReceipt.MachineCapacityTransition is
                {
                    Verified: true,
                    Stage: MachineSupportIntentStages.PlacementBound,
                    BeforeTargetMachinePresent: false,
                    AfterTargetMachinePresent: true
                },
            "File-backed machine placement receipt failed: " +
            string.Join(",", transitionReceipt.BlockingReasons));
        Write(transitionReceiptPath, transitionReceipt);

        var supportSettlementRequestPath = Path.Combine(
            priorRoot,
            "settlement-request.json");
        var supportSettlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildRequest(
                    supportInputs,
                    supportRequestPath,
                    commitReceiptPath,
                    committedLedgerPath,
                    commitResultPath,
                    compilationPath,
                    executionReceiptPath,
                    afterSnapshotPath,
                    transitionReceiptPath,
                    executionReceipt.RunId,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Write(supportSettlementRequestPath, supportSettlementRequest);
        var supportSettlement = service.SettleSupportingTransition(
            commitResult.Ledger,
            after,
            supportSettlementRequest,
            "2026-09-27T03:00:03Z");
        Require(supportSettlement.Accepted &&
                supportSettlement.Ledger is not null,
            "File-backed machine placement settlement failed: " +
            string.Join(",", supportSettlement.Errors));
        var supportSettlementResultPath = Path.Combine(
            priorRoot,
            "settlement-result.json");
        var supportSettledLedgerPath = Path.Combine(
            priorRoot,
            "settled-ledger.json");
        Write(supportSettlementResultPath, supportSettlement);
        Write(supportSettledLedgerPath, supportSettlement.Ledger!);
        var supportSettlementReceiptPath = Path.Combine(
            priorRoot,
            "settlement-receipt.json");
        var supportSettlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildReceipt(
                    supportInputs,
                    supportRequestPath,
                    commitReceiptPath,
                    committedLedgerPath,
                    commitResultPath,
                    compilationPath,
                    executionReceiptPath,
                    afterSnapshotPath,
                    transitionReceiptPath,
                    executionReceipt.RunId,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    supportSettlementRequestPath,
                    supportSettlementResultPath,
                    supportSettledLedgerPath);
        Require(supportSettlementReceipt.ReservationLifecycleVerified &&
                supportSettlementReceipt.FreshReplanRequired &&
                !supportSettlementReceipt.TerminalReceiptEligible &&
                !supportSettlementReceipt.FormalTrainingAuthorized,
            "File-backed machine placement settlement receipt failed: " +
            string.Join(",", supportSettlementReceipt.BlockingReasons));
        Write(supportSettlementReceiptPath, supportSettlementReceipt);

        var proof = new AcquisitionRouteSupportingTransitionSettlementProof
        {
            SupportRequestPath = supportRequestPath,
            SupportCommitReceiptPath = commitReceiptPath,
            CommittedLedgerPath = committedLedgerPath,
            CommitResultPath = commitResultPath,
            CompilationPath = compilationPath,
            ExecutionReceiptPath = executionReceiptPath,
            AfterSnapshotPath = afterSnapshotPath,
            SupportingTransitionReceiptPath = transitionReceiptPath,
            RunId = executionReceipt.RunId,
            ExecutorVersion =
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            SettlementRequestPath = supportSettlementRequestPath,
            SettlementResultPath = supportSettlementResultPath,
            SettledLedgerPath = supportSettledLedgerPath,
            SettlementReceiptPath = supportSettlementReceiptPath
        };
        VerifyMachineCapacityProofTerminalRoute(
            authority,
            supportInputs,
            proof,
            afterSnapshotPath,
            supportSettledLedgerPath,
            root);
    }

    private static void VerifyMachineCapacityProofTerminalRoute(
        MachineCapacityProofAuthority authority,
        AcquisitionRouteSupportingTransitionRequestInputs supportInputs,
        AcquisitionRouteSupportingTransitionSettlementProof proof,
        string afterSupportSnapshotPath,
        string afterSupportLedgerPath,
        string root)
    {
        var terminalRoot = Path.Combine(root, "terminal-route");
        Directory.CreateDirectory(terminalRoot);
        var proposalPath = Path.Combine(terminalRoot, "proposal.json");
        var portfolioInputs = BuildTargetDatePortfolioInputs(
            authority.Template,
            afterSupportSnapshotPath,
            afterSupportLedgerPath,
            proposalPath,
            terminalRoot,
            targetTotalDay: 0);
        var replanPath = Path.Combine(terminalRoot, "replan-admission.json");
        var replan = AcquisitionRouteSupportingTransitionReplanBuilder.Build(
            supportInputs,
            proof,
            portfolioInputs);
        Require(replan.FreshTeacherRequestReady &&
                replan.PriorQueueInvalidated &&
                replan.NextTeacherPreferenceRequest is not null,
            "Machine placement did not produce a file-backed fresh replan: " +
            string.Join(",", replan.BlockingReasons));
        Write(replanPath, replan);
        var preferenceRequestPath = Path.Combine(
            terminalRoot,
            "teacher-preference-request.json");
        Write(preferenceRequestPath, replan.NextTeacherPreferenceRequest!);
        var preference = AcquisitionRouteSupportingTransitionPortfolioBuilder
            .BuildTeacherPreference(
                supportInputs,
                proof,
                portfolioInputs,
                replanPath,
                preferenceRequestPath);
        Require(preference.TeacherPreferenceLabelEligible &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                preference.SelectedProposal.SelectedRouteOccurrenceIds
                    .SequenceEqual(
                        new[] { authority.ShopRouteOccurrenceId },
                        StringComparer.Ordinal),
            "Fresh Teacher did not select the terminal Beer shop route: " +
            string.Join(",", preference.BlockingReasons));
        Write(proposalPath, preference.SelectedProposal!);
        var preferencePath = Path.Combine(
            terminalRoot,
            "teacher-preference.json");
        var admissionPath = Path.Combine(terminalRoot, "admission.json");
        Write(preferencePath, preference);
        Write(admissionPath, preference.SelectedAdmission!);

        var afterSupport = CurrentTeacherFrontierSupport.Read<
            StardewAI.Contracts.State.SnapshotEnvelope>(
            afterSupportSnapshotPath,
            "Machine-capacity proof terminal before snapshot");
        var afterSupportLedger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            afterSupportLedgerPath,
            "Machine-capacity proof terminal base ledger");
        var commit = new ReservationPortfolioLedgerService().Commit(
            afterSupportLedger,
            afterSupport,
            preference.SelectedAdmission!.AtomicCommitRequest!,
            "2026-09-27T03:01:00Z");
        Require(commit.Accepted && commit.Ledger is not null,
            "Machine-capacity proof terminal portfolio commit failed: " +
            string.Join(",", commit.Errors));
        var commitResultPath = Path.Combine(
            terminalRoot,
            "portfolio-commit-result.json");
        var committedLedgerPath = Path.Combine(
            terminalRoot,
            "portfolio-committed-ledger.json");
        Write(commitResultPath, commit);
        Write(committedLedgerPath, commit.Ledger!);
        var commitReceiptPath = Path.Combine(
            terminalRoot,
            "portfolio-commit-receipt.json");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionPortfolioBuilder
                .BuildCommitReceipt(
                    supportInputs,
                    proof,
                    portfolioInputs,
                    replanPath,
                    preferenceRequestPath,
                    preferencePath,
                    admissionPath,
                    committedLedgerPath,
                    commitResultPath);
        Require(commitReceipt.PortfolioCommitVerified,
            "Machine-capacity proof terminal commit receipt failed: " +
            string.Join(",", commitReceipt.BlockingReasons));
        Write(commitReceiptPath, commitReceipt);

        var queuePath = Path.Combine(terminalRoot, "action-queue.json");
        var executionInputs = MachineCapacityProofExecutionInputs(
            portfolioInputs,
            preferenceRequestPath,
            preferencePath,
            admissionPath,
            commitReceiptPath,
            committedLedgerPath,
            commitResultPath,
            queuePath,
            authority.ShopRouteOccurrenceId);
        var supportingTransition =
            new AcquisitionRoutePortfolioSupportingTransitionInitialProof
            {
                RequestInputs = supportInputs,
                SettlementProof = proof,
                ReplanAdmissionPath = replanPath
            };
        var bindingPath = Path.Combine(terminalRoot, "execution-binding.json");
        var terminalAfterPath = Path.Combine(
            terminalRoot,
            "after-snapshot.json");
        var executionReceiptPath = Path.Combine(
            terminalRoot,
            "execution-receipt.json");
        var insufficientAfterPath = Path.Combine(
            terminalRoot,
            "insufficient-after-snapshot.json");
        var terminal = VerifyTargetDateFreshTerminalReceipt(
            executionInputs,
            bindingPath,
            terminalAfterPath,
            executionReceiptPath,
            insufficientAfterPath,
            supportingTransition: supportingTransition);

        var manifestPath = Path.Combine(
            terminalRoot,
            "rollout-proof-manifest.json");
        Write(
            manifestPath,
            new AcquisitionRoutePortfolioRolloutProofManifest
            {
                InitialCheckpointProof =
                    new AcquisitionRoutePortfolioInitialCheckpointProof
                    {
                        SupportingTransition = supportingTransition,
                        ExecutionInputs = executionInputs,
                        ExecutionBindingPath = terminal.ExecutionBindingPath,
                        ExecutionReceiptPath = terminal.ExecutionReceiptPath,
                        AfterSnapshotPath = terminal.AfterSnapshotPath,
                        FreshTerminalReceiptPath =
                            terminal.FreshTerminalReceiptPath,
                        RunId = terminal.RunId,
                        ExecutorVersion = PolicyTrajectoryVersionPins
                            .RuntimeTestHarnessExecutor,
                        SettlementRequestPath =
                            terminal.SettlementRequestPath,
                        SettlementResultPath =
                            terminal.SettlementResultPath,
                        SettledLedgerPath = terminal.SettledLedgerPath,
                        SettlementReceiptPath =
                            terminal.SettlementReceiptPath
                    },
                InitialCheckpointPath = terminal.RolloutCheckpointPath
            });
        var proofReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestPath);
        Require(proofReceipt.ProofChainVerified &&
                proofReceipt.PortfolioCompletionVerified &&
                proofReceipt.TransitionCount == 1 &&
                proofReceipt.ContinuationTransitionCount == 0,
            "Machine-capacity support lineage did not reach a terminal proof receipt.");
        var proofReceiptPath = Path.Combine(
            terminalRoot,
            "rollout-proof-receipt.json");
        Write(proofReceiptPath, proofReceipt);
        var rolloutAdmission =
            AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
                manifestPath,
                proofReceiptPath);
        Require(rolloutAdmission.ControllerAdmissionGranted &&
                rolloutAdmission.TeacherTrainingEvidenceEligible &&
                !rolloutAdmission.FormalProductTrainingAuthorized,
            "Machine-capacity support terminal proof was not admitted.");
        var rolloutAdmissionPath = Path.Combine(
            terminalRoot,
            "rollout-admission.json");
        Write(rolloutAdmissionPath, rolloutAdmission);
        var dataset = AcquisitionRoutePortfolioSupervisionBuilder.Build(
            manifestPath,
            proofReceiptPath,
            rolloutAdmissionPath);
        var datasetPath = Path.Combine(
            terminalRoot,
            "supervision-dataset.json");
        Write(datasetPath, dataset);
        var verified = AcquisitionRoutePortfolioSupervisionBuilder.Verify(
            datasetPath,
            manifestPath,
            proofReceiptPath,
            rolloutAdmissionPath);
        Require(verified.TeacherTrainingEvidenceEligible &&
                !verified.FormalProductTrainingAuthorized &&
                verified.TransitionCount == 1 &&
                verified.NativeOutcomeCount == 1 &&
                verified.Rows.Single().Payload.NativeOutcome
                    .RouteOccurrenceId == authority.ShopRouteOccurrenceId &&
                verified.Rows.Single().Payload.NativeOutcome
                    .RouteOccurrenceId != authority.MachineRouteOccurrenceId,
            "Supporting machine placement leaked into terminal supervision.");
    }

    private static AcquisitionRouteSupportingTransitionRequestInputs
        MachineCapacityProofSupportInputs(
            AcquisitionRoutePortfolioInputs inputs,
            string rankingPath,
            string routeOccurrenceId) => new()
        {
            RequirementInventoryPath = inputs.RequirementInventoryPath,
            AcquisitionLoweringPath = inputs.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = inputs.MasterAnglerWindowsPath,
            CalendarResolutionPath = inputs.CalendarResolutionPath,
            TargetDateCalendarPath = inputs.TargetDateCalendarPath,
            TargetDateUnlockPath = inputs.TargetDateUnlockPath,
            TargetDateFestivalPath = inputs.TargetDateFestivalPath,
            TargetDateLocationPath = inputs.TargetDateLocationPath,
            TargetDateFacilityPath = inputs.TargetDateFacilityPath,
            TargetDateResourcePath = inputs.TargetDateResourcePath,
            TargetDateCurrencyPath = inputs.TargetDateCurrencyPath,
            TargetDateReservationPath = inputs.TargetDateReservationPath,
            TargetDateProcessingPath = inputs.TargetDateProcessingPath,
            StrategyLedgerPath = inputs.StrategyLedgerPath,
            SnapshotPath = inputs.SnapshotPath,
            RouteTimingCalibrationPath = inputs.RouteTimingCalibrationPath,
            RankingPath = rankingPath,
            RouteOccurrenceId = routeOccurrenceId,
            SupportDeadlineTotalDay = 2
        };

    private static AcquisitionRouteExecutionBindingInputs
        MachineCapacityProofExecutionInputs(
            AcquisitionRoutePortfolioInputs inputs,
            string preferenceRequestPath,
            string preferencePath,
            string admissionPath,
            string commitReceiptPath,
            string committedLedgerPath,
            string commitResultPath,
            string queuePath,
            string routeOccurrenceId) => new()
        {
            RequirementInventoryPath = inputs.RequirementInventoryPath,
            AcquisitionLoweringPath = inputs.AcquisitionLoweringPath,
            MasterAnglerWindowsPath = inputs.MasterAnglerWindowsPath,
            CalendarResolutionPath = inputs.CalendarResolutionPath,
            TargetDateCalendarPath = inputs.TargetDateCalendarPath,
            TargetDateUnlockPath = inputs.TargetDateUnlockPath,
            TargetDateFestivalPath = inputs.TargetDateFestivalPath,
            TargetDateLocationPath = inputs.TargetDateLocationPath,
            TargetDateFacilityPath = inputs.TargetDateFacilityPath,
            TargetDateResourcePath = inputs.TargetDateResourcePath,
            TargetDateCurrencyPath = inputs.TargetDateCurrencyPath,
            TargetDateReservationPath = inputs.TargetDateReservationPath,
            TargetDateProcessingPath = inputs.TargetDateProcessingPath,
            TargetDateFishingProbabilityPath =
                inputs.TargetDateFishingProbabilityPath,
            TargetDateStochasticRetryPath =
                inputs.TargetDateStochasticRetryPath,
            TargetDateDailyTimeEnergyPath =
                inputs.TargetDateDailyTimeEnergyPath,
            TargetDateOpportunityCostPath = inputs.TargetDateOpportunityCostPath,
            FishingForecastManifestPath = inputs.FishingForecastManifestPath,
            StrategyLedgerPath = inputs.StrategyLedgerPath,
            BeforeSnapshotPath = inputs.SnapshotPath,
            RouteTimingCalibrationPath = inputs.RouteTimingCalibrationPath,
            PortfolioProposalPath = inputs.ProposalPath,
            PortfolioAdmissionPath = admissionPath,
            PortfolioPreferenceRequestPath = preferenceRequestPath,
            PortfolioTeacherPreferencePath = preferencePath,
            PortfolioCommitReceiptPath = commitReceiptPath,
            CommittedStrategyLedgerPath = committedLedgerPath,
            PortfolioCommitResultPath = commitResultPath,
            ActionQueuePath = queuePath,
            RouteOccurrenceId = routeOccurrenceId
        };
}
