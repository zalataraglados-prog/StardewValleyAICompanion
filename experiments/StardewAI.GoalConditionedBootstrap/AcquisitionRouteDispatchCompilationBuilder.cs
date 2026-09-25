using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    public static AcquisitionRouteDispatchCompilation Build(
        AcquisitionRouteExecutionBindingInputs inputs,
        string rankingPath)
    {
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            inputs.TargetDateOpportunityCostPath,
            "Target-date opportunity cost");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            inputs.AcquisitionLoweringPath,
            "Acquisition route lowering");
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            inputs.PortfolioTeacherPreferencePath,
            "Acquisition portfolio Teacher preference");
        var commit = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioCommitReceipt>(
            inputs.PortfolioCommitReceiptPath,
            "Acquisition portfolio commit receipt");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            inputs.BeforeSnapshotPath,
            "Acquisition route before snapshot");
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingPath,
            "Availability-aware live ranking");
        var ledger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            inputs.CommittedStrategyLedgerPath,
            "Committed strategy ledger");
        var route = opportunity.Routes.SingleOrDefault(value =>
            string.Equals(
                value.RouteOccurrenceId,
                inputs.RouteOccurrenceId,
                StringComparison.Ordinal));
        if (route is null)
        {
            throw new InvalidDataException(
                "Selected route occurrence is not present exactly once in the opportunity-cost report.");
        }

        var requirement = RequirementRoute(route);
        var lowered = LoweredRoute(lowering, requirement);
        var rankingHash = CurrentTeacherFrontierSupport.HashFile(rankingPath);
        var artifactReasons = ValidateArtifacts(
            opportunity,
            preference,
            commit,
            snapshot,
            ranking,
            ledger,
            requirement,
            lowered,
            inputs,
            rankingHash);
        if (artifactReasons.Length > 0)
        {
            return Blocked(
                opportunity.GoalId,
                requirement,
                snapshot.StateHash,
                rankingHash,
                artifactReasons);
        }

        var candidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(
            ranking);
        var matches = SelectCandidates(
            requirement,
            lowered,
            snapshot,
            candidates);
        if (matches.Length == 0)
        {
            return Blocked(
                opportunity.GoalId,
                requirement,
                snapshot.StateHash,
                rankingHash,
                new[] { "no_current_exact_source_bound_endpoint_candidate" });
        }

        var selected = matches[0];
        return Compile(
            opportunity.GoalId,
            requirement,
            lowered,
            selected,
            snapshot,
            ledger,
            commit.PortfolioId,
            commit.CommittedLedgerRevision,
            rankingHash);
    }

    private static string[] ValidateArtifacts(
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        AcquisitionRoutePortfolioTeacherPreference preference,
        AcquisitionRoutePortfolioCommitReceipt commit,
        SnapshotEnvelope snapshot,
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        AcquisitionRouteExecutionBindingInputs inputs,
        string rankingHash)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(rankingHash) ||
            !string.Equals(
                ranking.SchemaVersion,
                "availability_policy_prediction.v1",
                StringComparison.Ordinal) ||
            !string.Equals(
                ranking.Availability.StateHash,
                snapshot.StateHash,
                StringComparison.Ordinal))
        {
            reasons.Add("ranking_snapshot_identity_mismatch");
        }
        if (!preference.TeacherPreferenceLabelEligible ||
            preference.UsesLearnerRankOrScore ||
            preference.SelectedProposal is null ||
            !preference.SelectedProposal.SelectedRouteOccurrenceIds.Contains(
                requirement.RouteOccurrenceId,
                StringComparer.Ordinal))
        {
            reasons.Add("route_not_selected_by_independent_teacher_preference");
        }
        if (!commit.PortfolioCommitVerified ||
            !commit.SelectedRouteOccurrenceIds.Contains(
                requirement.RouteOccurrenceId,
                StringComparer.Ordinal) ||
            string.IsNullOrWhiteSpace(commit.PortfolioId) ||
            commit.CommittedLedgerRevision != ledger.Revision ||
            !string.Equals(
                commit.CommittedLedgerSha256,
                CurrentTeacherFrontierSupport.HashFile(
                    inputs.CommittedStrategyLedgerPath),
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("route_reservation_commit_not_verified");
        }
        if (!string.Equals(opportunity.SnapshotStateHash, snapshot.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(preference.SnapshotStateHash, snapshot.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(commit.SnapshotStateHash, snapshot.StateHash,
                StringComparison.Ordinal))
        {
            reasons.Add("route_dispatch_source_state_mismatch");
        }
        if (!lowered.RuntimeAdmissionReady ||
            !lowered.TeacherAdmissionReady ||
            lowered.EndpointOptionIds.Length == 0 ||
            requirement.BlockingReasons.Length != 0)
        {
            reasons.Add("route_lowering_not_dispatch_admitted");
        }
        return reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static AcquisitionRouteDispatchCompilation Blocked(
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        string stateHash,
        string rankingHash,
        IEnumerable<string> reasons) => new()
        {
            GoalId = goalId,
            RouteOccurrenceId = requirement.RouteOccurrenceId,
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            QualifiedItemId = requirement.QualifiedItemId,
            SourceStateHash = stateHash,
            RankingSha256 = rankingHash,
            Status = "blocked",
            DispatchReady = false,
            FormalTrainingAuthorized = false,
            UsesLearnerRankOrScore = false,
            BlockingReasons = reasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static AcquisitionRequirementRouteLowering LoweredRoute(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateUnlock requirement) =>
        lowering.RequirementSets.Single(value =>
                value.RequirementSetId == requirement.RequirementSetId)
            .Groups.Single(value =>
                value.RequirementId == requirement.RequirementId)
            .Alternatives[requirement.AlternativeIndex]
            .Routes[requirement.RouteIndex];
}
