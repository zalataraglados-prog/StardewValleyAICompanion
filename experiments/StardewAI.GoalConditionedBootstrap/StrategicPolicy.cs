using System.Diagnostics;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class StrategicPolicy
{
    private readonly GoalMethodPairwiseRanker learnedPreferenceModel;

    public StrategicPolicy()
        : this(new GoalMethodPairwiseRanker())
    {
    }

    internal StrategicPolicy(GoalMethodPairwiseRanker learnedPreferenceModel)
    {
        this.learnedPreferenceModel = learnedPreferenceModel;
    }

    public StrategicDecision SelectMethod(
        StrategicPolicySelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var set = BuildAuthoritativeScoringSet(request, out var transitionIndex);
        return SelectFromAuthoritativeScoringSet(
            request,
            set,
            transitionIndex,
            stopwatch);
    }

    internal StrategicDecision SelectFromAuthoritativeScoringSet(
        StrategicPolicySelectionRequest request,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set,
        int transitionIndex) => SelectFromAuthoritativeScoringSet(
            request,
            set,
            transitionIndex,
            Stopwatch.StartNew());

    private StrategicDecision SelectFromAuthoritativeScoringSet(
        StrategicPolicySelectionRequest request,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set,
        int transitionIndex,
        Stopwatch stopwatch)
    {
        var preference = set.Preference;
        var decision = BaseDecision(preference, transitionIndex);
        var strategicInputSha256 = StrategicInputIdentityBuilder.Build(
            request,
            set);
        decision.StrategicInputSha256 = strategicInputSha256;

        StrategicDecision Finish()
        {
            decision.DecisionLatencyMilliseconds = stopwatch.ElapsedMilliseconds;
            return decision;
        }

        var replan = StrategicReplanPolicy.Evaluate(
            request.Replan,
            preference.GoalId,
            preference.SnapshotStateHash,
            preference.ExpectedLedgerRevision,
            strategicInputSha256);
        decision.ReplanRequired = replan.ReplanRequired;
        decision.ReplanDeduplicated = replan.Deduplicated;
        decision.ReplanTriggerKinds = replan.TriggerKinds;
        decision.ReplanFingerprint = replan.Fingerprint;
        if (replan.BlockingReasons.Length > 0)
        {
            return FinishBlocked(
                decision,
                "blocked_strategic_replan",
                replan.BlockingReasons,
                Finish);
        }
        if (replan.Deduplicated)
        {
            decision.Status = "deduplicated_strategic_replan";
            return Finish();
        }

        if (!preference.CandidateDenominatorComplete ||
            set.Candidates.Length == 0)
        {
            return FinishBlocked(
                decision,
                "blocked_strategic_decision",
                preference.BlockingReasons.Append(
                    "strategic_candidate_denominator_unavailable"),
                Finish);
        }

        ValidateDeterministicArtifacts(set);
        var admitted = preference.CandidateEvaluations.Where(candidate =>
                candidate.AdmissionReady &&
                candidate.AggregateCostVector is not null)
            .ToArray();
        if (admitted.Length == 0 ||
            admitted.Length != preference.AdmittedCandidateCount)
        {
            throw new InvalidDataException(
                "Strategic admitted candidate count drifted.");
        }
        var frontier = admitted.Where(candidate =>
                candidate.DominatedByProposalIds.Length == 0)
            .OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();
        if (frontier.Length != preference.ParetoFrontierCount)
        {
            throw new InvalidDataException(
                "Strategic Pareto frontier count drifted.");
        }

        decision.CandidateDenominatorSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                preference.CandidateEvaluations.OrderBy(
                    candidate => candidate.ProposalId,
                    StringComparer.Ordinal).ToArray());
        decision.ParetoFrontierSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                frontier);

        if (preference.SelectionDisposition ==
            AcquisitionRoutePortfolioSelectionDisposition.UniqueStrictPareto)
        {
            if (!preference.TeacherPreferenceLabelEligible ||
                preference.SelectedProposal is null ||
                preference.SelectedAdmission is null)
            {
                throw new InvalidDataException(
                    "Unique strict-Pareto disposition lacks a selected portfolio.");
            }
            if (frontier.Length != 1 ||
                frontier[0].ProposalId !=
                    preference.SelectedProposal.ProposalId)
            {
                throw new InvalidDataException(
                    "Deterministic strategic selection drifted from the unique Pareto frontier.");
            }

            BindSelection(
                decision,
                preference.SelectedProposal,
                preference.SelectedAdmission,
                StrategicSelectionAuthorities
                    .DeterministicUniqueStrictPareto);
            decision.Status =
                "ready_deterministic_unique_strict_pareto_selection";
            decision.RuntimeSelectionAuthorized = true;
            decision.ModelInvoked = false;
            if (request.EnableDeterministicShadowAudit)
            {
                RunOptionalDeterministicShadowAudit(
                    request,
                    set,
                    preference,
                    transitionIndex,
                    admitted,
                    decision);
            }
            return Finish();
        }

        if (preference.SelectionDisposition !=
                AcquisitionRoutePortfolioSelectionDisposition
                    .IncomparableFrontier ||
            frontier.Length < 2)
        {
            return FinishBlocked(
                decision,
                "blocked_strategic_decision",
                preference.BlockingReasons.Append(
                    "strategic_selection_not_permitted"),
                Finish);
        }

        decision.ModelInvoked = true;
        VerifiedGoalMethodPairwiseModel verified;
        try
        {
            verified = learnedPreferenceModel.LoadVerifiedModel(
                request.CheckpointPath,
                request.CorpusManifestPath);
            ValidateLiveVersionBinding(verified.Checkpoint, set);
        }
        catch (Exception ex) when (IsModelAvailabilityFailure(ex))
        {
            decision.ModelInvoked = false;
            return FinishBlocked(
                decision,
                "blocked_strategic_decision",
                new[]
                {
                    "strategic_runtime_model_required_for_incomparable_frontier",
                    "strategic_runtime_model_unavailable_or_invalid:" +
                    ex.GetType().Name
                },
                Finish);
        }

        var featureContext = FeatureContext(
            preference,
            set,
            transitionIndex);
        var scores = GoalMethodPairwiseRanker.ScoreLiveCandidates(
            verified,
            featureContext,
            frontier,
            string.Empty);
        if (scores.Length != frontier.Length || scores.Length < 2 ||
            scores.Any(candidate =>
                !candidate.OnDeterministicParetoFrontier))
        {
            throw new InvalidDataException(
                "Learned strategic selection scored outside the admitted Pareto frontier.");
        }

        var selected = set.Candidates.Single(candidate =>
            candidate.Proposal.ProposalId == scores[0].ProposalId);
        if (!selected.Admission.PortfolioAdmissionReady)
        {
            throw new InvalidDataException(
                "Learned strategic selection chose a blocked candidate.");
        }
        BindSelection(
            decision,
            selected.Proposal,
            selected.Admission,
            StrategicSelectionAuthorities
                .LearnedIncomparableFrontierPreference);
        BindModel(decision, verified, scores);
        decision.Status =
            "ready_learned_incomparable_frontier_shadow_selection";
        decision.RuntimeSelectionAuthorized = false;
        decision.RuntimeModelAuthorityAuthorized = false;
        decision.FallbackReasons = new[]
        {
            "runtime_model_authority_not_promoted"
        };
        return Finish();
    }
}
