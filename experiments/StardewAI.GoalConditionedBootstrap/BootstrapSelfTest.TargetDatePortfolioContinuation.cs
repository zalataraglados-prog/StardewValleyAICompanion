using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioContinuation(
        AcquisitionRouteExecutionBindingInputs priorInputs,
        string priorManifestPath,
        string currentSnapshotPath,
        string currentLedgerPath,
        ICollection<AcquisitionRoutePortfolioSupervisionCorpusSource>
            completedSupervisionSources)
    {
        var latest = AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
            priorManifestPath);
        var checkpoint = latest.Checkpoint;
        Require(checkpoint.TransitionCount < 8,
            "Portfolio continuation fixture exceeded its finite bound.");
        var outputRoot = Path.Combine(
            Path.GetDirectoryName(latest.CheckpointPath)!,
            "continuation-" + (checkpoint.TransitionCount + 1)
                .ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(outputRoot);
        var proposalPath = Path.Combine(outputRoot, "proposal.json");
        var currentInputs = BuildContinuationPortfolioInputs(
            priorInputs,
            currentSnapshotPath,
            currentLedgerPath,
            proposalPath,
            outputRoot);
        var priorProofReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(priorManifestPath);
        var priorProofReceiptPath = Path.Combine(
            outputRoot,
            "prior-rollout-proof-receipt.json");
        Write(priorProofReceiptPath, priorProofReceipt);
        Require(priorProofReceipt.ProofChainVerified &&
                !priorProofReceipt.PortfolioCompletionVerified &&
                priorProofReceipt.TransitionCount == checkpoint.TransitionCount &&
                priorProofReceipt.ContinuationTransitionCount ==
                    checkpoint.TransitionCount - 1 &&
                !priorProofReceipt.FormalTrainingAuthorized,
            "Initial incomplete rollout proof-chain receipt drifted.");
        var blockedAdmission =
            AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
                priorManifestPath,
                priorProofReceiptPath);
        var blockedAdmissionPath = Path.Combine(
            outputRoot,
            "blocked-rollout-admission-receipt.json");
        Write(blockedAdmissionPath, blockedAdmission);
        Require(!blockedAdmission.ControllerAdmissionGranted &&
                !blockedAdmission.TeacherTrainingEvidenceEligible &&
                !blockedAdmission.FormalProductTrainingAuthorized &&
                blockedAdmission.BlockingReasons.Contains(
                    "rollout_portfolio_not_complete",
                    StringComparer.Ordinal),
            "Incomplete rollout proof unexpectedly crossed controller admission.");
        var incompleteSupervisionRejected = false;
        try
        {
            AcquisitionRoutePortfolioSupervisionBuilder.Build(
                priorManifestPath,
                priorProofReceiptPath,
                blockedAdmissionPath);
        }
        catch (InvalidDataException)
        {
            incompleteSupervisionRejected = true;
        }
        Require(incompleteSupervisionRejected,
            "Incomplete rollout emitted portfolio supervision rows.");
        var requestPath = Path.Combine(outputRoot, "request.json");
        var request = AcquisitionRoutePortfolioContinuationBuilder
            .BuildRequest(priorManifestPath, currentInputs);
        Write(requestPath, request);
        Require(request.TransitionCount == checkpoint.TransitionCount + 1 &&
                request.PriorCheckpointSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(
                        latest.CheckpointPath) &&
                request.CompletedRouteOccurrenceIds.SequenceEqual(
                    checkpoint.CompletedRouteOccurrenceIds,
                    StringComparer.Ordinal) &&
                request.ScopedProgress.Length > 0 &&
                request.ScopedProgress.All(row => !row.ScopeComplete) &&
                !request.FormalTrainingAuthorized,
            "Continuation Teacher request drifted.");

        var preferencePath = Path.Combine(outputRoot, "preference.json");
        var preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .BuildContinuation(
                priorManifestPath,
                currentInputs,
                requestPath);
        Write(preferencePath, preference);
        Require(preference.TeacherPreferenceLabelEligible &&
                preference.CandidateDenominatorComplete &&
                preference.CandidateDenominatorCount == 1 &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                preference.SelectedProposal.SelectedRouteOccurrenceIds
                    .SequenceEqual(
                        checkpoint.PendingSelectedRouteOccurrenceIds,
                        StringComparer.Ordinal) &&
                preference.SelectedProposal.PriorRolloutCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                !preference.FormalTrainingAuthorized,
            "Continuation Teacher preference drifted.");
        Write(proposalPath, preference.SelectedProposal!);
        var admissionPath = Path.Combine(outputRoot, "admission.json");
        Write(admissionPath, preference.SelectedAdmission!);

        var committedLedgerPath = Path.Combine(
            outputRoot,
            "committed-ledger.json");
        var currentLedger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            currentLedgerPath,
            "Continuation current strategy ledger");
        var currentSnapshot = CurrentTeacherFrontierSupport.Read<
            SnapshotEnvelope>(
            currentSnapshotPath,
            "Continuation current snapshot");
        var commit = new ReservationPortfolioLedgerService().Commit(
            currentLedger,
            currentSnapshot,
            preference.SelectedAdmission!.AtomicCommitRequest!,
            "2026-09-21T00:03:00Z");
        Require(commit.Accepted && commit.Ledger is not null,
            "Continuation portfolio marker commit failed.");
        var commitResultPath = Path.Combine(outputRoot, "commit-result.json");
        Write(commitResultPath, commit);
        Write(committedLedgerPath, commit.Ledger!);
        var commitReceiptPath = Path.Combine(
            outputRoot,
            "commit-receipt.json");
        var commitReceipt = AcquisitionRoutePortfolioCommitReceiptBuilder
            .BuildContinuation(
                priorManifestPath,
                currentInputs,
                requestPath,
                preferencePath,
                admissionPath,
                committedLedgerPath,
                commitResultPath);
        Write(commitReceiptPath, commitReceipt);
        Require(commitReceipt.PortfolioCommitVerified &&
                commitReceipt.AtomicMutationObserved &&
                commitReceipt.BaseLedgerRevision ==
                    checkpoint.LatestLedgerRevision &&
                commitReceipt.CommittedLedgerRevision ==
                    checkpoint.LatestLedgerRevision + 1 &&
                commitReceipt.PriorRolloutCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                !commitReceipt.FormalTrainingAuthorized,
            "Continuation portfolio commit receipt drifted.");

        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            currentInputs.TargetDateOpportunityCostPath,
            "Continuation opportunity cost");
        var routeOccurrenceId = preference.SelectedProposal!
            .SelectedRouteOccurrenceIds
            .Order(StringComparer.Ordinal)
            .First();
        var route = opportunity.Routes.Single(value =>
            value.RouteOccurrenceId == routeOccurrenceId);
        var requirement = TargetDateRequirementRoute(route);
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            currentInputs.AcquisitionLoweringPath,
            "Continuation route lowering");
        var lowered = lowering.RequirementSets.Single(set =>
                set.RequirementSetId == requirement.RequirementSetId)
            .Groups.Single(group =>
                group.RequirementId == requirement.RequirementId)
            .Alternatives[requirement.AlternativeIndex]
            .Routes[requirement.RouteIndex];
        var optionId = lowered.EndpointOptionIds.First();
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            currentSnapshotPath,
            "Continuation before snapshot");
        var actor = ExecutionTargetProfiles.CreateActor(
            ExecutionTargetProfiles.TrainingSingleplayer);
        var candidateId = AcquisitionRouteExecutionBindingBuilder
            .SelectedCandidateId(route.RouteOccurrenceId);
        var queueItem = new ActionQueueItem
        {
            QueueItemId = "queue-item.target-date-continuation." +
                request.TransitionCount,
            SourceActionId = "action.target-date-continuation." +
                request.TransitionCount,
            OptionId = optionId,
            Status = "pending",
            PermissionRequired = "executor",
            BehaviorCategory = "mechanical",
            CompilerResponsibility = "deterministic",
            TrainingRole = "strategy_value",
            NormalizedCommand = new NormalizedCommand
            {
                CommandType = "option_request",
                OptionId = optionId,
                BehaviorCategory = "mechanical",
                CompilerResponsibility = "deterministic",
                TrainingRole = "strategy_value",
                StateHash = before.StateHash,
                ExecutionMode =
                    ExecutionTargetProfiles.TrainingSingleplayer,
                Actor = actor,
                Parameters = AcquisitionRouteExecutionBindingBuilder
                    .RouteBindingParameters(
                        requirement,
                        commitReceipt.PortfolioId,
                        commitReceipt.CommittedLedgerRevision),
                Steps = new[]
                {
                    new CompiledActionStep
                    {
                        StepId = "primitive.target-date-continuation." +
                            request.TransitionCount,
                        StepType = "fixture_native_route",
                        Target = requirement.QualifiedItemId,
                        ExpectedEffect =
                            "exact_inventory_quantity_increase",
                        EstimatedTicks = 1
                    }
                }
            }
        };
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.target-date-continuation." +
                request.TransitionCount,
            SourceModelOutputId = "teacher.target-date-continuation." +
                request.TransitionCount,
            SourceModel = "deterministic_teacher.fixture",
            StateHash = before.StateHash,
            GoalId = opportunity.GoalId,
            ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
            Actor = actor,
            Status = "pending",
            Items = new[] { queueItem },
            CandidateAudit = new[]
            {
                new SmallModelPlanCandidateAudit
                {
                    CandidateId = candidateId,
                    Kind = "target_date_acquisition_route",
                    Decision = "accepted"
                }
            }
        };
        var queuePath = Path.Combine(outputRoot, "queue.json");
        Write(queuePath, queue);
        var bindingInputs = ContinuationBindingInputs(
            currentInputs,
            admissionPath,
            requestPath,
            preferencePath,
            commitReceiptPath,
            committedLedgerPath,
            commitResultPath,
            queuePath,
            route.RouteOccurrenceId);
        var binding = AcquisitionRouteExecutionBindingBuilder
            .BuildContinuation(
                priorManifestPath,
                requestPath,
                bindingInputs);
        var bindingPath = Path.Combine(outputRoot, "execution-binding.json");
        Write(bindingPath, binding);
        Require(binding.DispatchBindingReady &&
                binding.PortfolioTeacherPreferenceVerified &&
                binding.PortfolioReservationCommitVerified &&
                binding.PriorRolloutCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                binding.RouteOccurrenceId == route.RouteOccurrenceId &&
                !binding.FormalTrainingAuthorized &&
                binding.BlockingReasons.Length == 0,
            "Continuation route execution binding drifted.");

        VerifyTargetDatePortfolioContinuationCompletion(
            priorManifestPath,
            requestPath,
            bindingInputs,
            bindingPath,
            outputRoot,
            completedSupervisionSources);
    }

}
