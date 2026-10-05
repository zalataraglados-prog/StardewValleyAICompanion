using System.Security.Cryptography;
using System.Text;
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
        string rankingHash,
        SmallModelActionParameter[]? additionalLineage = null,
        AcquisitionRouteTargetDateOpportunityCost? selectedOpportunity = null)
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
        var teacherBudgetReasons = BindSelectedOpportunityBudget(
            candidate,
            selectedOpportunity);
        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash,
            goalId,
            ExecutionTargetProfiles.TrainingSingleplayer,
            maxCandidates: 1);
        plan.SourceModel =
            "deterministic_teacher.acquisition_route_dispatch.v1";

        var deferredPickupReasons = AppendDeferredNativeDropPickup(
            plan,
            requirement,
            source,
            snapshot);

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
            .Concat(additionalLineage ??
                Array.Empty<SmallModelActionParameter>())
            .ToArray();
        var compilationIdentity = CompilationIdentity(
            goalId,
            requirement,
            source,
            candidate,
            snapshot,
            ledger,
            portfolioId,
            committedLedgerRevision,
            rankingHash,
            routeOptionRole,
            lineage);
        plan.PlanId = "acquisition_route_plan." + compilationIdentity;
        var annotationReasons = AnnotatePlan(plan, lineage);
        var queue = annotationReasons.Length == 0
            ? new ActionQueueCompiler().Compile(plan, snapshot, ledger)
            : null;
        if (queue is not null)
            StabilizeQueueIdentity(queue, compilationIdentity);
        var reasons = annotationReasons
            .Concat(deferredPickupReasons)
            .Concat(roleReasons)
            .Concat(teacherBudgetReasons)
            .Concat(PlanReasons(plan))
            .Concat(TeacherRoutePlanReasons(plan, selectedOpportunity))
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

    private static string[] BindSelectedOpportunityBudget(
        PolicyEventCandidatePrediction candidate,
        AcquisitionRouteTargetDateOpportunityCost? opportunity)
    {
        if (opportunity is null || candidate.Kind != "clear_obstacle_tile")
            return Array.Empty<string>();

        var daily = opportunity.UpstreamRoute;
        var evaluation = daily.Evaluation;
        var vector = opportunity.CostVector;
        if (evaluation is null ||
            daily.DailyTimeEnergyMatchesTargetDate != true ||
            vector is null ||
            !evaluation.StandTileX.HasValue ||
            !evaluation.StandTileY.HasValue ||
            !evaluation.TargetTileX.HasValue ||
            !evaluation.TargetTileY.HasValue ||
            !evaluation.GuaranteedArrivalByTime.HasValue ||
            !evaluation.TerminalActionGameMinutes.HasValue ||
            evaluation.TerminalActionGameMinutes.Value <= 0 ||
            !string.Equals(
                candidate.LocationId,
                evaluation.TargetLocationId,
                StringComparison.OrdinalIgnoreCase) ||
            candidate.TileX != evaluation.TargetTileX ||
            candidate.TileY != evaluation.TargetTileY)
        {
            return new[] { "selected_teacher_route_budget_binding_incomplete" };
        }

        var movementMinutes = GameClockBudgetPolicy.ClockMinutesBetween(
            evaluation.SnapshotStartTime,
            evaluation.GuaranteedArrivalByTime.Value);
        var terminalMinutes = evaluation.TerminalActionGameMinutes.Value;
        if (movementMinutes <= 0 ||
            movementMinutes + terminalMinutes !=
                vector.GuaranteedElapsedGameMinutes)
        {
            return new[] { "selected_teacher_route_budget_vector_mismatch" };
        }

        const string prefix = "teacher_route.";
        var parameters = candidate.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        if (parameters.Any(parameter => parameter.Name.StartsWith(
                prefix,
                StringComparison.Ordinal)))
        {
            return new[] { "selected_candidate_declares_teacher_route_budget" };
        }
        candidate.Parameters = parameters.Concat(new[]
        {
            Parameter(
                prefix + "stand_tile_x",
                evaluation.StandTileX.Value.ToString()),
            Parameter(
                prefix + "stand_tile_y",
                evaluation.StandTileY.Value.ToString()),
            Parameter(
                prefix + "movement_game_minutes",
                movementMinutes.ToString()),
            Parameter(
                prefix + "terminal_action_game_minutes",
                terminalMinutes.ToString()),
            Parameter(
                prefix + "guaranteed_elapsed_game_minutes",
                vector.GuaranteedElapsedGameMinutes.ToString()),
            Parameter(
                prefix + "timing_evidence_id",
                evaluation.TimingEvidenceId)
        }).ToArray();
        return Array.Empty<string>();
    }

    private static string[] TeacherRoutePlanReasons(
        SmallModelPlanEnvelope plan,
        AcquisitionRouteTargetDateOpportunityCost? opportunity)
    {
        if (opportunity is null ||
            plan.Steps.Length == 0 ||
            plan.Steps.All(step => step.Kind != "clear_obstacle"))
        {
            return Array.Empty<string>();
        }

        var evaluation = opportunity.UpstreamRoute.Evaluation;
        var vector = opportunity.CostVector;
        if (evaluation is null || vector is null)
            return new[] { "teacher_route_plan_budget_evidence_missing" };
        var estimatedMinutes = plan.Steps.Sum(step =>
            step.EstimatedMinutes ?? 0);
        var move = plan.Steps.SingleOrDefault(step =>
            step.Kind == "move_to_tile");
        var terminal = plan.Steps.SingleOrDefault(step =>
            step.Kind == "clear_obstacle");
        if (estimatedMinutes != vector.GuaranteedElapsedGameMinutes ||
            move is null ||
            terminal is null ||
            move.TargetTileX != evaluation.StandTileX ||
            move.TargetTileY != evaluation.StandTileY ||
            terminal.TargetTileX != evaluation.TargetTileX ||
            terminal.TargetTileY != evaluation.TargetTileY)
        {
            return new[] { "compiled_queue_teacher_route_budget_mismatch" };
        }
        return Array.Empty<string>();
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

    private static string CompilationIdentity(
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        PolicyEventCandidatePrediction source,
        PolicyEventCandidatePrediction candidate,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        string portfolioId,
        int committedLedgerRevision,
        string rankingHash,
        string routeOptionRole,
        SmallModelActionParameter[] lineage)
    {
        var value = JsonSerializer.Serialize(new
        {
            goalId,
            requirement.RouteOccurrenceId,
            SourceCandidateId = source.CandidateId,
            SelectedCandidateId = candidate.CandidateId,
            snapshot.StateHash,
            ledger.LedgerId,
            LedgerRevision = ledger.Revision,
            portfolioId,
            committedLedgerRevision,
            rankingHash,
            routeOptionRole,
            lineage
        }, JsonDefaults.Compact);
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static void StabilizeQueueIdentity(
        ActionQueueEnvelope queue,
        string compilationIdentity)
    {
        queue.QueueId = "acquisition_route_queue." + compilationIdentity;
        for (var index = 0; index < queue.Items.Length; index++)
        {
            var item = queue.Items[index];
            item.QueueItemId =
                "acquisition_route_queue_item." + compilationIdentity + "." +
                index;
            var steps = item.NormalizedCommand?.Steps ??
                Array.Empty<CompiledActionStep>();
            for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                steps[stepIndex].StepId =
                    "acquisition_route_primitive." + compilationIdentity +
                    "." + index + "." + stepIndex;
            }
        }
    }
}
