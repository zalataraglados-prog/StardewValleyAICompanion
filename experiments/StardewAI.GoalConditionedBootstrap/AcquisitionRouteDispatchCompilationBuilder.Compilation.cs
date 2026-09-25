using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    internal static AcquisitionRouteDispatchCompilation Compile(
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        AcquisitionRouteDispatchCandidateMatch selected,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        string portfolioId,
        int committedLedgerRevision,
        string rankingHash)
    {
        var source = selected.Candidate;
        var routeOptionRole = selected.RouteOptionRole;
        var roleReasons = routeOptionRole is
                "terminal_transition" or "supporting_transition"
            ? Array.Empty<string>()
            : new[] { "route_option_role_invalid" };
        var candidate = NeutralizeLearnerSignals(
            source,
            AcquisitionRouteExecutionBindingBuilder.SelectedCandidateId(
                requirement.RouteOccurrenceId));
        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash,
            goalId,
            ExecutionTargetProfiles.TrainingSingleplayer,
            maxCandidates: 1);
        plan.SourceModel =
            "deterministic_teacher.acquisition_route_dispatch.v1";

        var lineage = AcquisitionRouteExecutionBindingBuilder
            .RouteBindingParameters(
                requirement,
                portfolioId,
                committedLedgerRevision,
                routeOptionRole)
            .Concat(new[]
            {
                Parameter("acquisition_endpoint_option_id", source.OptionId),
                Parameter("acquisition_source_candidate_id", source.CandidateId),
                Parameter("acquisition_source_ranking_sha256", rankingHash),
                Parameter(
                    "acquisition_source_binding_evidence",
                    selected.IdentityEvidence)
            })
            .ToArray();
        var annotationReasons = AnnotatePlan(plan, lineage);
        var queue = annotationReasons.Length == 0
            ? new ActionQueueCompiler().Compile(plan, snapshot, ledger)
            : null;
        var reasons = annotationReasons
            .Concat(roleReasons)
            .Concat(PlanReasons(plan))
            .Concat(queue is null
                ? Array.Empty<string>()
                : AcquisitionRouteExecutionBindingBuilder.ValidateQueue(
                    queue,
                    goalId,
                    snapshot.StateHash,
                    candidate.CandidateId,
                    requirement,
                    lowered,
                    portfolioId,
                    committedLedgerRevision,
                    routeOptionRole))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ready = reasons.Length == 0 && queue is not null;
        return new AcquisitionRouteDispatchCompilation
        {
            Status = ready
                ? routeOptionRole == "supporting_transition"
                    ? "ready_for_supporting_transition_dispatch"
                    : "ready_for_native_action_queue_dispatch"
                : "blocked",
            GoalId = goalId,
            RouteOccurrenceId = requirement.RouteOccurrenceId,
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            QualifiedItemId = requirement.QualifiedItemId,
            SourceStateHash = snapshot.StateHash,
            RankingSha256 = rankingHash,
            SourceCandidateId = source.CandidateId,
            SelectedCandidateId = candidate.CandidateId,
            EndpointOptionId = source.OptionId,
            SelectedRouteOptionRole = routeOptionRole,
            TerminalReceiptEligible =
                routeOptionRole == "terminal_transition",
            FreshReplanRequiredAfterSuccess =
                routeOptionRole == "supporting_transition",
            SourceBindingEvidence = selected.IdentityEvidence,
            UsesLearnerRankOrScore = false,
            CompiledPlan = plan,
            ActionQueue = queue,
            DispatchReady = ready,
            FormalTrainingAuthorized = false,
            BlockingReasons = reasons
        };
    }

    private static string[] AnnotatePlan(
        SmallModelPlanEnvelope plan,
        SmallModelActionParameter[] lineage)
    {
        var reasons = new List<string>();
        foreach (var step in plan.Steps)
        {
            var parameters = step.Parameters ??
                Array.Empty<SmallModelActionParameter>();
            var names = parameters.Select(value => value.Name)
                .ToHashSet(StringComparer.Ordinal);
            var collisions = lineage
                .Where(value => names.Contains(value.Name))
                .Select(value => value.Name)
                .ToArray();
            if (collisions.Length > 0)
            {
                reasons.AddRange(collisions.Select(value =>
                    "route_lineage_parameter_collision:" + value));
                continue;
            }
            step.Parameters = parameters.Concat(lineage.Select(Clone)).ToArray();
        }
        return reasons.ToArray();
    }

    private static string[] PlanReasons(
        SmallModelPlanEnvelope plan)
    {
        var audit = plan.CandidateAudit ??
            Array.Empty<SmallModelPlanCandidateAudit>();
        var accepted = audit.Where(value =>
                value.Decision == "accepted")
            .ToArray();
        var reasons = audit
            .Where(value => value.Decision != "accepted")
            .SelectMany(value => value.Reasons ?? Array.Empty<string>())
            .ToList();
        if (plan.Steps.Length == 0)
            reasons.Add("selected_route_candidate_compiled_no_plan_steps");
        if (accepted.Length != 1)
            reasons.Add("selected_route_candidate_not_uniquely_accepted");
        return reasons.ToArray();
    }

    private static PolicyEventCandidatePrediction NeutralizeLearnerSignals(
        PolicyEventCandidatePrediction source,
        string selectedCandidateId)
    {
        var clone = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Selected acquisition route candidate could not be cloned.");
        clone.CandidateId = selectedCandidateId;
        clone.Rank = 1;
        clone.Score = 0;
        clone.ModelScore = null;
        clone.ExpectedReward = 0;
        clone.PolicyModelSource =
            "deterministic_teacher.acquisition_route_dispatch.v1";
        return clone;
    }

    private static SmallModelActionParameter Clone(
        SmallModelActionParameter value) => new()
        {
            Name = value.Name,
            Value = value.Value
        };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
