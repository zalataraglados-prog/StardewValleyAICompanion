using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class StrategicPolicy
{
    private static AcquisitionRoutePortfolioTeacherPreferenceBuilder
        .AcquisitionRoutePortfolioTeacherScoringSet
        BuildAuthoritativeScoringSet(
            StrategicPolicySelectionRequest request,
            out int transitionIndex)
    {
        if (string.IsNullOrWhiteSpace(request.PriorRolloutProofManifestPath))
        {
            transitionIndex = 1;
            return AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildScoringSet(
                    request.CurrentInputs,
                    request.PreferenceRequestPath);
        }

        var verified = AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
            request.PriorRolloutProofManifestPath);
        transitionIndex = verified.Checkpoint.TransitionCount + 1;
        return AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .BuildContinuationScoringSet(
                verified,
                request.CurrentInputs,
                request.PreferenceRequestPath);
    }

    private void RunOptionalDeterministicShadowAudit(
        StrategicPolicySelectionRequest request,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set,
        AcquisitionRoutePortfolioTeacherPreference preference,
        int transitionIndex,
        AcquisitionRoutePortfolioTeacherCandidateEvaluation[] admitted,
        StrategicDecision decision)
    {
        decision.ShadowAuditAttempted = true;
        try
        {
            var verified = learnedPreferenceModel.LoadVerifiedModel(
                request.CheckpointPath,
                request.CorpusManifestPath);
            ValidateLiveVersionBinding(verified.Checkpoint, set);
            var scores = GoalMethodPairwiseRanker.ScoreLiveCandidates(
                verified,
                FeatureContext(preference, set, transitionIndex),
                admitted,
                preference.SelectedProposal?.ProposalId ?? string.Empty);
            BindModel(decision, verified, scores);
            decision.ShadowAuditSucceeded = true;
        }
        catch (Exception ex) when (IsModelAvailabilityFailure(ex))
        {
            decision.ShadowAuditSucceeded = false;
            decision.FallbackReasons = new[]
            {
                "optional_deterministic_shadow_audit_failed:" +
                ex.GetType().Name
            };
        }
        finally
        {
            decision.ModelInvoked = false;
        }
    }

    private static StrategicDecision BaseDecision(
        AcquisitionRoutePortfolioTeacherPreference preference,
        int transitionIndex) => new()
        {
            GoalId = preference.GoalId,
            SnapshotStateHash = preference.SnapshotStateHash,
            StrategyLedgerRevision = preference.ExpectedLedgerRevision,
            TransitionIndex = transitionIndex,
            CandidateDenominatorCount = preference.CandidateDenominatorCount,
            AdmittedCandidateCount = preference.AdmittedCandidateCount,
            ParetoFrontierCount = preference.ParetoFrontierCount,
            TeacherPreferenceSha256 =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ArtifactSha256(preference),
            DeterministicPreferenceStatus = preference.Status,
            DeterministicSelectionDisposition =
                preference.SelectionDisposition,
            DeterministicSelectedMethodId =
                preference.SelectedProposal?.ProposalId ?? string.Empty,
            PortfolioCommitAuthorized = false,
            FormalProductTrainingAuthorized = false,
            RuntimeModelAuthorityAuthorized = false
        };

    private static void BindSelection(
        StrategicDecision decision,
        AcquisitionRoutePortfolioProposal proposal,
        AcquisitionRoutePortfolioAdmission admission,
        string authority)
    {
        decision.SelectionAuthority = authority;
        decision.SelectedMethodId = proposal.ProposalId;
        decision.SelectedMethodSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                proposal);
        decision.SelectedProposal = proposal;
        decision.SelectedAdmission = admission;
        decision.BlockingReasons = Array.Empty<string>();
    }

    private static void BindModel(
        StrategicDecision decision,
        VerifiedGoalMethodPairwiseModel verified,
        GoalMethodPairwiseCandidateScore[] scores)
    {
        decision.ModelCheckpointId = verified.Checkpoint.CheckpointId;
        decision.ModelCheckpointSha256 = verified.CheckpointSha256;
        decision.ModelCorpusManifestSha256 = verified.CorpusManifestSha256;
        decision.ModelCandidateScores = scores;
    }

    private static StrategicDecision FinishBlocked(
        StrategicDecision decision,
        string status,
        IEnumerable<string> reasons,
        Func<StrategicDecision> finish)
    {
        decision.Status = status;
        decision.RuntimeSelectionAuthorized = false;
        decision.RuntimeModelAuthorityAuthorized = false;
        decision.PortfolioCommitAuthorized = false;
        decision.FormalProductTrainingAuthorized = false;
        decision.BlockingReasons = reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return finish();
    }

    private static GoalMethodPairwiseFeatureContext FeatureContext(
        AcquisitionRoutePortfolioTeacherPreference preference,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set,
        int transitionIndex)
    {
        var state = AcquisitionRoutePortfolioSupervisionBuilder
            .DecisionContext(set.Snapshot);
        return new GoalMethodPairwiseFeatureContext(
            preference.GoalId,
            state.Year,
            state.Season,
            state.Day,
            state.Time,
            state.TotalDay,
            transitionIndex,
            preference.ExpectedLedgerRevision,
            preference.CandidateDenominatorCount,
            preference.AdmittedCandidateCount,
            preference.ParetoFrontierCount,
            preference.SelectionPolicyId);
    }

    private static void ValidateLiveVersionBinding(
        GoalMethodPairwiseCheckpoint checkpoint,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set)
    {
        var state = AcquisitionRoutePortfolioSupervisionBuilder
            .DecisionContext(set.Snapshot);
        if (state.GameVersion != checkpoint.Versions.GameVersion ||
            state.BridgeVersion != checkpoint.Versions.BridgeVersion)
        {
            throw new InvalidDataException(
                "Strategic live state version differs from the checkpoint.");
        }
    }

    private static void ValidateDeterministicArtifacts(
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set)
    {
        var evaluations = set.Preference.CandidateEvaluations.ToDictionary(
            candidate => candidate.ProposalId,
            StringComparer.Ordinal);
        if (evaluations.Count != set.Candidates.Length)
        {
            throw new InvalidDataException(
                "Strategic candidate denominator drifted.");
        }

        foreach (var candidate in set.Candidates)
        {
            if (!evaluations.TryGetValue(
                    candidate.Proposal.ProposalId,
                    out var evaluation) ||
                candidate.ProposalSha256 != evaluation.ProposalSha256 ||
                candidate.ProposalSha256 !=
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .ArtifactSha256(candidate.Proposal) ||
                !candidate.Proposal.SelectedRouteOccurrenceIds.SequenceEqual(
                    evaluation.SelectedRouteOccurrenceIds,
                    StringComparer.Ordinal) ||
                candidate.Admission.PortfolioAdmissionReady !=
                    evaluation.AdmissionReady ||
                !EqualJson(
                    candidate.Admission.AggregateCostVector,
                    evaluation.AggregateCostVector))
            {
                throw new InvalidDataException(
                    "Strategic candidate artifact drifted from its authoritative evaluation.");
            }
        }
    }

    private static bool IsModelAvailabilityFailure(Exception exception) =>
        exception is ArgumentException or InvalidDataException or IOException or
            UnauthorizedAccessException or JsonException;

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Compact),
        JsonSerializer.Serialize(right, JsonDefaults.Compact),
        StringComparison.Ordinal);
}
