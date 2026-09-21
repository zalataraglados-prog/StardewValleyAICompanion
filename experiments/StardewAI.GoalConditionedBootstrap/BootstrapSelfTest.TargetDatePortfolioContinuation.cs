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
        AcquisitionRoutePortfolioInitialCheckpointProof proof,
        string checkpointPath,
        string currentSnapshotPath,
        string currentLedgerPath,
        AcquisitionRoutePortfolioRolloutCheckpoint checkpoint)
    {
        var outputRoot = Path.Combine(
            Path.GetDirectoryName(checkpointPath)!,
            "continuation");
        Directory.CreateDirectory(outputRoot);
        var proposalPath = Path.Combine(outputRoot, "proposal.json");
        var currentInputs = BuildContinuationPortfolioInputs(
            priorInputs,
            currentSnapshotPath,
            currentLedgerPath,
            proposalPath,
            outputRoot);
        var priorManifestPath = Path.Combine(
            outputRoot,
            "prior-rollout-proof-manifest.json");
        Write(priorManifestPath,
            new AcquisitionRoutePortfolioRolloutProofManifest
            {
                InitialCheckpointProof = proof,
                InitialCheckpointPath = checkpointPath
            });
        var priorProofReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(priorManifestPath);
        Require(priorProofReceipt.ProofChainVerified &&
                !priorProofReceipt.PortfolioCompletionVerified &&
                priorProofReceipt.TransitionCount == 1 &&
                priorProofReceipt.ContinuationTransitionCount == 0 &&
                !priorProofReceipt.FormalTrainingAuthorized,
            "Initial incomplete rollout proof-chain receipt drifted.");
        var requestPath = Path.Combine(outputRoot, "request.json");
        var request = AcquisitionRoutePortfolioContinuationBuilder
            .BuildRequest(priorManifestPath, currentInputs);
        Write(requestPath, request);
        Require(request.TransitionCount == 2 &&
                request.PriorCheckpointSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(checkpointPath) &&
                request.CompletedRouteOccurrenceIds.SequenceEqual(
                    checkpoint.CompletedRouteOccurrenceIds,
                    StringComparer.Ordinal) &&
                request.ScopedProgress.Length == 1 &&
                !request.ScopedProgress[0].ScopeComplete &&
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
        var route = opportunity.Routes.Single(value =>
            value.RouteOccurrenceId ==
                checkpoint.PendingSelectedRouteOccurrenceIds.Single());
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
            QueueItemId = "queue-item.target-date-continuation.0",
            SourceActionId = "action.target-date-continuation.0",
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
                        StepId = "primitive.target-date-continuation.0",
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
            QueueId = "queue.target-date-continuation",
            SourceModelOutputId = "teacher.target-date-continuation",
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
            proof,
            checkpointPath,
            priorManifestPath,
            requestPath,
            bindingInputs,
            bindingPath,
            outputRoot);
    }

}
