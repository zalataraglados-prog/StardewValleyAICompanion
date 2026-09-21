using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    internal const int MaxCandidateCount = 4096;

    public static AcquisitionRoutePortfolioTeacherPreference Build(
        AcquisitionRoutePortfolioInputs inputs,
        string requestPath)
    {
        var requestFullPath = Path.GetFullPath(requestPath);
        var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            requestFullPath,
            "Acquisition route portfolio Teacher preference request");
        var result = BaseResult(
            context,
            request,
            CurrentTeacherFrontierSupport.HashFile(requestFullPath));
        var reasons = ValidateRequest(context, request);
        if (reasons.Count > 0)
            return Block(result, reasons);

        var denominatorCount = CountCandidates(context, request, reasons);
        result.CandidateDenominatorCount = denominatorCount;
        if (reasons.Count > 0)
            return Block(result, reasons);
        if (denominatorCount == 0)
        {
            reasons.Add("portfolio_teacher_candidate_denominator_empty");
            return Block(result, reasons);
        }
        if (denominatorCount > MaxCandidateCount)
        {
            reasons.Add(
                "portfolio_teacher_candidate_denominator_exceeds_limit:" +
                denominatorCount);
            return Block(result, reasons);
        }

        var proposals = EnumerateProposals(context, request).ToArray();
        if (proposals.LongLength != denominatorCount)
        {
            throw new InvalidDataException(
                "Portfolio Teacher candidate enumeration count drifted.");
        }
        if (proposals.Select(proposal => proposal.ProposalId)
                .Distinct(StringComparer.Ordinal).Count() != proposals.Length)
        {
            throw new InvalidDataException(
                "Portfolio Teacher candidate enumeration produced duplicate proposal identities.");
        }
        result.CandidateDenominatorComplete = true;
        var candidates = proposals.Select(proposal =>
        {
            var proposalSha256 = ArtifactSha256(proposal);
            var admission = AcquisitionRoutePortfolioBuilder.Build(
                context,
                proposal,
                proposalSha256);
            return new PortfolioCandidate(proposal, proposalSha256, admission);
        }).ToArray();
        var admitted = candidates.Where(candidate =>
                candidate.Admission.PortfolioAdmissionReady &&
                candidate.Admission.AggregateCostVector is not null)
            .ToArray();
        result.AdmittedCandidateCount = admitted.Length;
        var pareto = EvaluatePareto(admitted.Select(candidate =>
            new PortfolioCostCandidate(
                candidate.Proposal.ProposalId,
                candidate.Admission.AggregateCostVector!)).ToArray());
        var evaluations = candidates.Select(candidate =>
        {
            var dominators = candidate.Admission.PortfolioAdmissionReady &&
                candidate.Admission.AggregateCostVector is not null
                ? pareto.DominatedByProposalIds[
                    candidate.Proposal.ProposalId]
                : Array.Empty<string>();
            return new AcquisitionRoutePortfolioTeacherCandidateEvaluation(
                candidate.Proposal.ProposalId,
                candidate.ProposalSha256,
                candidate.Proposal.SelectedRouteOccurrenceIds.ToArray(),
                candidate.Admission.PortfolioAdmissionReady,
                candidate.Admission.AggregateCostVector,
                dominators,
                candidate.Admission.BlockingReasons.ToArray());
        }).OrderBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();
        result.CandidateEvaluations = evaluations;
        var frontier = evaluations.Where(candidate =>
                pareto.FrontierProposalIds.Contains(
                    candidate.ProposalId,
                    StringComparer.Ordinal))
            .ToArray();
        result.ParetoFrontierCount = frontier.Length;
        if (admitted.Length == 0)
        {
            reasons.Add("portfolio_teacher_no_admitted_candidate");
            return Block(result, reasons);
        }
        if (frontier.Length != 1)
        {
            reasons.Add(frontier.Length == 0
                ? "portfolio_teacher_frontier_empty"
                : "portfolio_teacher_frontier_incomparable_or_equal:" +
                  frontier.Length);
            return Block(result, reasons);
        }

        var selected = candidates.Single(candidate =>
            candidate.Proposal.ProposalId == frontier[0].ProposalId);
        var nonDominatedAlternative = admitted.FirstOrDefault(candidate =>
            candidate.Proposal.ProposalId != selected.Proposal.ProposalId &&
            !AcquisitionRouteTargetDateOpportunityCostBuilder
                .OpportunityCostDominates(
                    selected.Admission.AggregateCostVector!,
                    candidate.Admission.AggregateCostVector!));
        if (nonDominatedAlternative is not null)
        {
            throw new InvalidDataException(
                "Unique portfolio frontier member did not dominate an admitted alternative.");
        }
        result.SelectedProposal = selected.Proposal;
        result.SelectedAdmission = selected.Admission;
        result.PairwisePreferences = admitted
            .Where(candidate => candidate.Proposal.ProposalId !=
                selected.Proposal.ProposalId)
            .OrderBy(candidate => candidate.Proposal.ProposalId,
                StringComparer.Ordinal)
            .Select(candidate =>
                new AcquisitionRoutePortfolioPairwisePreference(
                    selected.Proposal.ProposalId,
                    candidate.Proposal.ProposalId,
                    "strict_aggregate_opportunity_cost_pareto_dominance",
                    "preferred_over_admitted_counterfactual"))
            .ToArray();
        result.Status =
            "ready_unique_strict_pareto_portfolio_teacher_preference";
        result.TeacherPreferenceLabelEligible = true;
        result.BlockingReasons = Array.Empty<string>();
        return result;
    }

    internal static string ArtifactSha256(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Options) +
            Environment.NewLine;
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static AcquisitionRoutePortfolioTeacherPreference BaseResult(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request,
        string requestSha256) => new()
        {
            RequestId = request.RequestId,
            GoalId = request.GoalId,
            SnapshotStateHash = request.SnapshotStateHash,
            ExpectedLedgerRevision = request.ExpectedLedgerRevision,
            PreferenceRequestSha256 = requestSha256,
            RequirementInventorySha256 = context.RequirementInventorySha256,
            OpportunityCostSha256 = context.OpportunityCostSha256,
            StrategyLedgerSha256 = context.StrategyLedgerSha256,
            SnapshotSha256 = context.SnapshotSha256,
            CandidateLimit = MaxCandidateCount,
            FormalTrainingAuthorized = false,
            UsesLearnerRankOrScore = false,
            EmitsNegativeLabelsForUnavailablePortfolios = false
        };

    private static AcquisitionRoutePortfolioTeacherPreference Block(
        AcquisitionRoutePortfolioTeacherPreference result,
        IEnumerable<string> reasons)
    {
        result.Status = "blocked_portfolio_teacher_preference";
        result.TeacherPreferenceLabelEligible = false;
        result.FormalTrainingAuthorized = false;
        result.BlockingReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return result;
    }

    private sealed record PortfolioCandidate(
        AcquisitionRoutePortfolioProposal Proposal,
        string ProposalSha256,
        AcquisitionRoutePortfolioAdmission Admission);
}
