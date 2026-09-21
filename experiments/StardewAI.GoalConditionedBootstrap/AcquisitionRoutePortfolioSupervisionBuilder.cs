using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioSupervisionBuilder
{
    public static AcquisitionRoutePortfolioSupervisionDataset Build(
        string proofManifestPath,
        string proofReceiptPath,
        string rolloutAdmissionReceiptPath)
    {
        var manifestFullPath = Path.GetFullPath(proofManifestPath);
        var proofReceiptFullPath = Path.GetFullPath(proofReceiptPath);
        var admissionFullPath = Path.GetFullPath(
            rolloutAdmissionReceiptPath);
        var expectedProof = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestFullPath);
        var actualProof = Read<AcquisitionRoutePortfolioRolloutProofReceipt>(
            proofReceiptFullPath,
            "Acquisition route portfolio rollout proof receipt");
        Require(EqualJson(actualProof, expectedProof),
            "Rollout proof receipt does not equal controller recomputation.");
        var expectedAdmission =
            AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
                manifestFullPath,
                proofReceiptFullPath);
        var actualAdmission = Read<
            AcquisitionRoutePortfolioRolloutAdmissionReceipt>(
            admissionFullPath,
            "Acquisition route portfolio rollout admission receipt");
        Require(EqualJson(actualAdmission, expectedAdmission),
            "Rollout admission receipt does not equal controller recomputation.");
        Require(expectedAdmission.ControllerAdmissionGranted &&
                expectedAdmission.TeacherTrainingEvidenceEligible &&
                !expectedAdmission.FormalProductTrainingAuthorized &&
                expectedProof.ProofChainVerified &&
                expectedProof.PortfolioCompletionVerified,
            "Only an admitted terminal rollout may emit supervision rows.");

        var manifest = Read<AcquisitionRoutePortfolioRolloutProofManifest>(
            manifestFullPath,
            "Acquisition route portfolio rollout proof manifest");
        var sources = Sources(manifest).ToArray();
        Require(sources.Length == expectedProof.TransitionCount,
            "Rollout proof transition count does not match its manifest.");

        var rows = new List<AcquisitionRoutePortfolioSupervisionRow>(
            sources.Length);
        var priorRowSha256 = string.Empty;
        for (var index = 0; index < sources.Length; index++)
        {
            var payload = BuildPayload(sources[index], index + 1);
            var rowSha256 = HashRow(priorRowSha256, payload);
            rows.Add(new AcquisitionRoutePortfolioSupervisionRow
            {
                RowId = expectedProof.RolloutId + ":transition:" +
                    (index + 1).ToString(
                        "D4",
                        System.Globalization.CultureInfo.InvariantCulture),
                TransitionIndex = index + 1,
                PriorRowSha256 = priorRowSha256,
                RowSha256 = rowSha256,
                Payload = payload
            });
            priorRowSha256 = rowSha256;
        }

        return new AcquisitionRoutePortfolioSupervisionDataset
        {
            Status = "ready_verified_portfolio_supervision_dataset",
            RolloutId = expectedProof.RolloutId,
            GoalId = expectedProof.GoalId,
            ProofManifestSha256 = CurrentTeacherFrontierSupport.HashFile(
                manifestFullPath),
            ProofReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                proofReceiptFullPath),
            RolloutAdmissionReceiptSha256 =
                CurrentTeacherFrontierSupport.HashFile(admissionFullPath),
            TransitionCount = rows.Count,
            TeacherPreferenceCount = rows.Count,
            NativeOutcomeCount = rows.Count,
            StudentObservationCount = 0,
            RowChainTipSha256 = priorRowSha256,
            Rows = rows.ToArray(),
            TeacherTrainingEvidenceEligible = true,
            FormalProductTrainingAuthorized = false
        };
    }

    public static AcquisitionRoutePortfolioSupervisionDataset Verify(
        string datasetPath,
        string proofManifestPath,
        string proofReceiptPath,
        string rolloutAdmissionReceiptPath)
    {
        var actual = Read<AcquisitionRoutePortfolioSupervisionDataset>(
            Path.GetFullPath(datasetPath),
            "Acquisition route portfolio supervision dataset");
        var expected = Build(
            proofManifestPath,
            proofReceiptPath,
            rolloutAdmissionReceiptPath);
        Require(EqualJson(actual, expected),
            "Portfolio supervision dataset does not equal controller recomputation.");
        return actual;
    }

    private static AcquisitionRoutePortfolioSupervisionPayload BuildPayload(
        TransitionSource source,
        int transitionIndex)
    {
        var inputs = source.ExecutionInputs;
        var preferencePath = Path.GetFullPath(
            inputs.PortfolioTeacherPreferencePath);
        var preference = Read<AcquisitionRoutePortfolioTeacherPreference>(
            preferencePath,
            "Acquisition route portfolio Teacher preference");
        var proposal = preference.SelectedProposal ??
            throw new InvalidDataException(
                "Verified Teacher preference has no selected proposal.");
        var admission = preference.SelectedAdmission ??
            throw new InvalidDataException(
                "Verified Teacher preference has no selected admission.");
        var selectedEvaluation = preference.CandidateEvaluations
            .Where(row => row.ProposalId == proposal.ProposalId)
            .ToArray();
        var binding = Read<AcquisitionRouteExecutionBinding>(
            Path.GetFullPath(source.ExecutionBindingPath),
            "Acquisition route execution binding");
        var execution = Read<QueueExecutionReceiptEnvelope>(
            Path.GetFullPath(source.ExecutionReceiptPath),
            "Acquisition route execution receipt");
        var fresh = Read<AcquisitionRouteFreshTerminalReceiptAdmission>(
            Path.GetFullPath(source.FreshTerminalReceiptPath),
            "Acquisition route fresh terminal receipt");
        var settlement = Read<AcquisitionRoutePortfolioSettlementReceipt>(
            Path.GetFullPath(source.SettlementReceiptPath),
            "Acquisition route portfolio settlement receipt");
        var checkpoint = Read<AcquisitionRoutePortfolioRolloutCheckpoint>(
            Path.GetFullPath(source.CheckpointPath),
            "Acquisition route portfolio rollout checkpoint");
        var snapshotPath = Path.GetFullPath(inputs.BeforeSnapshotPath);
        var ledgerPath = Path.GetFullPath(inputs.StrategyLedgerPath);
        var preferenceRequestSha256 =
            CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                inputs.PortfolioPreferenceRequestPath));
        var preferenceSha256 = CurrentTeacherFrontierSupport.HashFile(
            preferencePath);
        var executionBindingSha256 = CurrentTeacherFrontierSupport.HashFile(
            Path.GetFullPath(source.ExecutionBindingPath));
        var freshSha256 = CurrentTeacherFrontierSupport.HashFile(
            Path.GetFullPath(source.FreshTerminalReceiptPath));
        var settlementSha256 = CurrentTeacherFrontierSupport.HashFile(
            Path.GetFullPath(source.SettlementReceiptPath));
        Require(preference.SchemaVersion ==
                    "acquisition_route_portfolio_teacher_preference.v1" &&
                preference.TeacherPreferenceLabelEligible &&
                preference.CandidateDenominatorComplete &&
                !preference.FormalTrainingAuthorized &&
                !preference.UsesLearnerRankOrScore &&
                !preference.EmitsNegativeLabelsForUnavailablePortfolios &&
                preference.PreferenceRequestSha256 ==
                    preferenceRequestSha256 &&
                selectedEvaluation.Length == 1 &&
                selectedEvaluation[0].AdmissionReady &&
                binding.DispatchBindingReady &&
                binding.PortfolioTeacherPreferenceVerified &&
                !binding.FormalTrainingAuthorized &&
                binding.GoalId == preference.GoalId &&
                binding.RouteOccurrenceId == source.RouteOccurrenceId &&
                binding.BeforeStateHash == preference.SnapshotStateHash &&
                binding.PortfolioTeacherPreferenceSha256 ==
                    preferenceSha256 &&
                proposal.SelectedRouteOccurrenceIds.Contains(
                    source.RouteOccurrenceId,
                    StringComparer.Ordinal) &&
                proposal.GoalId == preference.GoalId &&
                proposal.SnapshotStateHash == preference.SnapshotStateHash &&
                admission.ProposalId == proposal.ProposalId &&
                admission.GoalId == preference.GoalId &&
                admission.SnapshotStateHash == preference.SnapshotStateHash &&
                admission.PortfolioAdmissionReady &&
                !admission.FormalTrainingAuthorized,
            "Teacher supervision channel is not an exact eligible preference.");
        Require(execution.SchemaVersion == "queue_execution_receipt.v1" &&
                execution.RunId == source.RunId &&
                execution.SourceStateHash == binding.BeforeStateHash &&
                execution.AfterStateHash == settlement.AfterStateHash &&
                execution.Success &&
                execution.SelectedCandidateCompleted &&
                execution.AfterSnapshotFresh &&
                execution.ExecutedItemCount == execution.PlannedItemCount &&
                execution.FinalPendingItemCount == 0 &&
                fresh.RouteOccurrenceId == source.RouteOccurrenceId &&
                fresh.GoalId == preference.GoalId &&
                fresh.ExecutionBindingSha256 == executionBindingSha256 &&
                fresh.ExecutionReceiptSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(
                        Path.GetFullPath(source.ExecutionReceiptPath)) &&
                fresh.AfterSnapshotSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(
                        Path.GetFullPath(source.AfterSnapshotPath)) &&
                fresh.QueueExecutionVerified &&
                fresh.FreshTerminalReceiptVerified &&
                fresh.RouteTrainingEvidenceEligible &&
                !fresh.FormalTrainingAuthorized &&
                fresh.TerminalTransition is { Verified: true } &&
                settlement.RouteOccurrenceId == source.RouteOccurrenceId &&
                settlement.GoalId == preference.GoalId &&
                settlement.PortfolioId ==
                    "target-date-acquisition-portfolio:" +
                    proposal.ProposalId &&
                settlement.AfterStateHash == execution.AfterStateHash &&
                settlement.ExecutionBindingSha256 == executionBindingSha256 &&
                settlement.FreshTerminalReceiptSha256 == freshSha256 &&
                settlement.FreshTerminalReceiptVerified &&
                settlement.ExactSettlementReplayVerified &&
                settlement.ReservationLifecycleVerified &&
                !settlement.FormalTrainingAuthorized &&
                checkpoint.TransitionCount == transitionIndex &&
                checkpoint.CheckpointVerified &&
                checkpoint.GoalId == preference.GoalId &&
                checkpoint.CurrentProposalId == proposal.ProposalId &&
                checkpoint.CurrentPortfolioId == settlement.PortfolioId &&
                checkpoint.CurrentTeacherPreferenceSha256 ==
                    preferenceSha256 &&
                checkpoint.LatestSettlementReceiptSha256 == settlementSha256 &&
                checkpoint.LatestStateHash == settlement.AfterStateHash &&
                checkpoint.LatestLedgerRevision ==
                    settlement.SettledLedgerRevision &&
                checkpoint.LatestLedgerSha256 ==
                    settlement.SettledLedgerSha256 &&
                !checkpoint.FormalTrainingAuthorized,
            "Native outcome channel is not an exact verified transition.");
        Require(preference.SnapshotSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(snapshotPath) &&
                preference.StrategyLedgerSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(ledgerPath) &&
                preference.ExpectedLedgerRevision ==
                    admission.StrategyLedgerRevision &&
                preference.StrategyLedgerSha256 ==
                    admission.StrategyLedgerSha256,
            "Decision-state provenance drifted before supervision export.");

        return new AcquisitionRoutePortfolioSupervisionPayload
        {
            DecisionStateHash = preference.SnapshotStateHash,
            DecisionSnapshotSha256 = preference.SnapshotSha256,
            DecisionLedgerRevision = preference.ExpectedLedgerRevision,
            DecisionLedgerSha256 = preference.StrategyLedgerSha256,
            TeacherPreference = new AcquisitionRoutePortfolioTeacherSupervision
            {
                Status = "verified_independent_teacher_preference",
                PreferenceRequestSha256 =
                    preference.PreferenceRequestSha256,
                TeacherPreferenceSha256 = preferenceSha256,
                SelectionPolicyId = preference.SelectionPolicyId,
                CandidateDenominatorCount =
                    preference.CandidateDenominatorCount,
                CandidateDenominatorComplete = true,
                AdmittedCandidateCount = preference.AdmittedCandidateCount,
                ParetoFrontierCount = preference.ParetoFrontierCount,
                CandidateEvaluations = preference.CandidateEvaluations,
                SelectedProposalId = proposal.ProposalId,
                SelectedProposalSha256 =
                    selectedEvaluation[0].ProposalSha256,
                SelectedRouteOccurrenceIds =
                    proposal.SelectedRouteOccurrenceIds,
                SelectedAggregateCostVector =
                    admission.AggregateCostVector,
                PairwisePreferences = preference.PairwisePreferences,
                UsesLearnerRankOrScore = false,
                EmitsNegativeLabelsForUnavailablePortfolios = false
            },
            NativeOutcome = new AcquisitionRoutePortfolioNativeOutcomeSupervision
            {
                Status = "verified_fresh_native_outcome",
                RouteOccurrenceId = source.RouteOccurrenceId,
                RunId = execution.RunId,
                QueueId = execution.QueueId,
                BeforeGameTick = execution.BeforeGameTick,
                AfterGameTick = execution.AfterGameTick,
                ExecutionBindingSha256 = executionBindingSha256,
                ExecutionReceiptSha256 = fresh.ExecutionReceiptSha256,
                AfterSnapshotSha256 = fresh.AfterSnapshotSha256,
                FreshTerminalReceiptSha256 = freshSha256,
                SettlementReceiptSha256 = settlementSha256,
                RolloutCheckpointSha256 =
                    CurrentTeacherFrontierSupport.HashFile(
                        Path.GetFullPath(source.CheckpointPath)),
                AfterStateHash = execution.AfterStateHash,
                TerminalTransition = fresh.TerminalTransition!,
                QueueExecutionVerified = true,
                FreshTerminalReceiptVerified = true,
                ReservationLifecycleVerified = true
            },
            StudentObservation = new
                AcquisitionRoutePortfolioStudentObservationSupervision()
        };
    }

    private static IEnumerable<TransitionSource> Sources(
        AcquisitionRoutePortfolioRolloutProofManifest manifest)
    {
        var initial = manifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Rollout proof manifest has no initial proof.");
        yield return new TransitionSource(
            initial.ExecutionInputs,
            initial.ExecutionBindingPath,
            initial.ExecutionReceiptPath,
            initial.AfterSnapshotPath,
            initial.FreshTerminalReceiptPath,
            initial.RunId,
            initial.SettlementReceiptPath,
            manifest.InitialCheckpointPath,
            initial.ExecutionInputs.RouteOccurrenceId);
        foreach (var transition in manifest.ContinuationTransitions)
        {
            yield return new TransitionSource(
                transition.ExecutionInputs,
                transition.ExecutionBindingPath,
                transition.ExecutionReceiptPath,
                transition.AfterSnapshotPath,
                transition.FreshTerminalReceiptPath,
                transition.RunId,
                transition.SettlementReceiptPath,
                transition.CheckpointPath,
                transition.ExecutionInputs.RouteOccurrenceId);
        }
    }

    private static string HashRow(
        string priorRowSha256,
        AcquisitionRoutePortfolioSupervisionPayload payload)
    {
        var value = priorRowSha256 + "\n" +
            JsonSerializer.Serialize(payload, JsonDefaults.Compact);
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static T Read<T>(string path, string label) =>
        CurrentTeacherFrontierSupport.Read<T>(path, label);

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record TransitionSource(
        AcquisitionRouteExecutionBindingInputs ExecutionInputs,
        string ExecutionBindingPath,
        string ExecutionReceiptPath,
        string AfterSnapshotPath,
        string FreshTerminalReceiptPath,
        string RunId,
        string SettlementReceiptPath,
        string CheckpointPath,
        string RouteOccurrenceId);
}
