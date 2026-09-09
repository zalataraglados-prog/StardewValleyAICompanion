using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherPreferenceBuilder
{
    public static CurrentStageOneCollectionTeacherPreferenceLabel Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string snapshotPath,
        string masterAnglerTargetDateIntentsPath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var rankingFullPath = Path.GetFullPath(rankingPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var intentsFullPath = Path.GetFullPath(masterAnglerTargetDateIntentsPath);
        var frontier = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
            inventoryFullPath,
            loweringFullPath,
            rankingFullPath,
            snapshotFullPath,
            intentsFullPath);
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingFullPath,
            "Availability-aware ranking");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotFullPath,
            "Live snapshot");

        ValidateInputs(
            frontier,
            ranking,
            snapshot,
            inventoryFullPath,
            loweringFullPath,
            rankingFullPath,
            snapshotFullPath,
            intentsFullPath);
        var label = BaseLabel(
            frontier,
            inventoryFullPath,
            loweringFullPath,
            rankingFullPath,
            snapshotFullPath,
            intentsFullPath);
        var blockedSets = BlockedRequirementSets(frontier);
        if (blockedSets.Length > 0)
        {
            label.Status = "blocked_incomplete_current_collection_denominator";
            label.BlockingReasons = blockedSets
                .Select(value => "blocked_requirement_set:" + value)
                .ToArray();
            return label;
        }

        if (!frontier.CurrentCandidateMembershipEligible ||
            frontier.SelectionContract.CandidateChoices.Length == 0)
        {
            label.Status = "blocked_no_current_collection_candidate";
            label.BlockingReasons = new[]
            {
                "unified_current_collection_candidate_membership_is_empty"
            };
            return label;
        }

        var currentCandidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(
            ranking);
        var candidatesById = currentCandidates.ToDictionary(
            value => value.CandidateId,
            StringComparer.Ordinal);
        var evaluations = frontier.SelectionContract.CandidateChoices
            .Select(choice => Evaluate(
                choice,
                candidatesById[choice.CandidateId],
                frontier))
            .OrderByDescending(value => value.HasAuthoritativeCurrentDayDeadline)
            .ThenBy(value => value.MinimumDeadlineSlackDays ?? int.MaxValue)
            .ThenBy(value =>
                value.EarliestActionDeadlineTimeExclusive ?? int.MaxValue)
            .ThenByDescending(value => value.TerminalTransitionCreditCount)
            .ThenByDescending(value =>
                value.DistinctRequirementSetCreditCount)
            .ThenByDescending(value => value.ExactRequirementCreditCount)
            .ThenBy(value =>
                value.RarestCreditedSelectionGroupCandidateCount)
            .ThenByDescending(value => value.EstimatedTicksKnown)
            .ThenBy(value => value.EstimatedTicks ?? int.MaxValue)
            .ThenBy(value => value.EnergyCost)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < evaluations.Length; index++)
            evaluations[index].SelectionOrder = index + 1;
        label.OrderedCandidateEvaluations = evaluations;

        var selectedEvaluation = evaluations[0];
        var authoritativeTies = evaluations
            .Skip(1)
            .Where(value => FirstDifferingCriterion(
                selectedEvaluation,
                value) is null)
            .Select(value => value.CandidateId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (authoritativeTies.Length > 0)
        {
            label.Status = "blocked_authoritatively_tied_top_candidates";
            label.BlockingReasons = authoritativeTies
                .Select(value => "authoritatively_tied_with_top_candidate:" + value)
                .ToArray();
            return label;
        }

        var selectedChoice = frontier.SelectionContract.CandidateChoices.Single(
            value => value.CandidateId == selectedEvaluation.CandidateId);
        var selectedSource = candidatesById[selectedChoice.CandidateId];
        var selectionReason = evaluations.Length == 1
            ? "only_current_exact_candidate"
            : FirstDifferingCriterion(selectedEvaluation, evaluations[1])!;
        label.SelectedCandidate = new CurrentCollectionTeacherSelectedCandidate(
            selectedChoice.CandidateId,
            selectedChoice.OptionId,
            selectedChoice.Kind,
            selectedSource.LocationId,
            selectedSource.TileX,
            selectedSource.TileY,
            selectedChoice.RequirementCredits,
            selectionReason);
        label.PairwisePreferences = evaluations
            .Skip(1)
            .Select(value => new CurrentCollectionTeacherPairwisePreference(
                selectedChoice.CandidateId,
                value.CandidateId,
                FirstDifferingCriterion(selectedEvaluation, value)!,
                "preferred_over_current_eligible_counterfactual"))
            .ToArray();

        var neutralCandidate = NeutralizeLearnerSignals(selectedSource);
        var compiledPlan = new DailyPlanCompiler().Compile(
            new[] { neutralCandidate },
            frontier.SourceStateHash,
            goalId: frontier.GoalId,
            maxCandidates: 1);
        var compiledQueue = new ActionQueueCompiler().Compile(
            compiledPlan,
            snapshot);
        label.CompiledPlan = compiledPlan;
        label.CompiledQueue = compiledQueue;
        var compilationReasons = CompilationReasons(compiledPlan, compiledQueue);
        if (compiledPlan.Steps.Length == 0 ||
            compiledQueue.Status != "pending" ||
            compiledQueue.Items.Length == 0 ||
            compiledQueue.Items.Any(value => value.Status != "pending") ||
            compilationReasons.Length > 0)
        {
            label.Status = "blocked_selected_preference_not_dispatchable";
            label.BlockingReasons = compilationReasons
                .DefaultIfEmpty(
                    "selected_teacher_preference_did_not_compile_to_a_pending_queue")
                .ToArray();
            return label;
        }

        label.Status = "ready";
        label.TeacherPreferenceLabelEligible = true;
        return label;
    }

    private static CurrentStageOneCollectionTeacherPreferenceLabel BaseLabel(
        CurrentStageOneCollectionTeacherFrontier frontier,
        string inventoryPath,
        string loweringPath,
        string rankingPath,
        string snapshotPath,
        string intentsPath) => new()
    {
        GoalId = frontier.GoalId,
        SourceStateHash = frontier.SourceStateHash,
        FormalTrainingAuthorized = false,
        UsesLearnerRankOrScore = false,
        EmitsNegativeLabelsForUnavailableRoutes = false,
        RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
            inventoryPath),
        AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
            loweringPath),
        RankingSha256 = CurrentTeacherFrontierSupport.HashFile(rankingPath),
        SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotPath),
        MasterAnglerTargetDateIntentsSha256 =
            CurrentTeacherFrontierSupport.HashFile(intentsPath),
        CandidateMembership = frontier
    };

    private static CurrentCollectionTeacherCandidateEvaluation Evaluate(
        CurrentCollectionCandidateChoice choice,
        PolicyEventCandidatePrediction candidate,
        CurrentStageOneCollectionTeacherFrontier frontier)
    {
        var masterBindings = frontier.MasterAngler.CandidateBindings
            .Where(value => value.CandidateId == choice.CandidateId)
            .ToArray();
        var creditedGroups = frontier.SelectionContract.SelectionGroups
            .Where(value => value.CandidateIds.Contains(
                choice.CandidateId,
                StringComparer.Ordinal))
            .ToArray();
        var estimatedTicksKnown = candidate.EstimatedTicks > 0;
        return new CurrentCollectionTeacherCandidateEvaluation
        {
            CandidateId = choice.CandidateId,
            OptionId = choice.OptionId,
            Kind = choice.Kind,
            HasAuthoritativeCurrentDayDeadline = masterBindings.Length > 0,
            MinimumDeadlineSlackDays = masterBindings.Length == 0
                ? null
                : masterBindings.Min(value => value.DeadlineSlackDays),
            EarliestActionDeadlineTimeExclusive = masterBindings.Length == 0
                ? null
                : masterBindings.Min(value => value.LastCastTimeExclusive),
            TerminalTransitionCreditCount = choice.RequirementCredits.Count(
                value => IsTerminalTransition(value.BindingKind)),
            DistinctRequirementSetCreditCount = choice.RequirementCredits
                .Select(value => value.RequirementSetId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ExactRequirementCreditCount = choice.RequirementCredits
                .Select(value => value.RequirementSetId + "|" + value.RequirementId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RarestCreditedSelectionGroupCandidateCount = creditedGroups.Min(
                value => value.CandidateIds.Length),
            EstimatedTicksKnown = estimatedTicksKnown,
            EstimatedTicks = estimatedTicksKnown
                ? candidate.EstimatedTicks
                : null,
            EnergyCost = candidate.EnergyCost,
            Reasons = new[]
            {
                masterBindings.Length > 0
                    ? "authoritative_current_day_master_angler_window"
                    : "no_authoritative_current_day_deadline",
                "terminal_transition_credits=" + choice.RequirementCredits.Count(
                    value => IsTerminalTransition(value.BindingKind)),
                "distinct_requirement_set_credits=" + choice.RequirementCredits
                    .Select(value => value.RequirementSetId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                "exact_requirement_credits=" + choice.RequirementCredits.Length,
                "rarest_credited_group_candidate_count=" + creditedGroups.Min(
                    value => value.CandidateIds.Length),
                estimatedTicksKnown
                    ? "deterministic_estimated_ticks=" + candidate.EstimatedTicks
                    : "deterministic_estimated_ticks_unknown",
                "deterministic_energy_cost=" + candidate.EnergyCost
            }
        };
    }

    private static bool IsTerminalTransition(string bindingKind) =>
        bindingKind is
            "native_full_shipment_completion" or
            "native_museum_donation_completion" or
            "native_community_center_payment_completion" or
            "native_community_center_donation_completion" or
            "authoritative_window_terminal_attempt";

    private static string? FirstDifferingCriterion(
        CurrentCollectionTeacherCandidateEvaluation preferred,
        CurrentCollectionTeacherCandidateEvaluation alternative)
    {
        if (preferred.HasAuthoritativeCurrentDayDeadline !=
            alternative.HasAuthoritativeCurrentDayDeadline)
        {
            return "authoritative_current_day_deadline";
        }
        if (preferred.MinimumDeadlineSlackDays !=
            alternative.MinimumDeadlineSlackDays)
        {
            return "minimum_deadline_slack_days";
        }
        if (preferred.EarliestActionDeadlineTimeExclusive !=
            alternative.EarliestActionDeadlineTimeExclusive)
        {
            return "earliest_action_deadline_time_exclusive";
        }
        if (preferred.TerminalTransitionCreditCount !=
            alternative.TerminalTransitionCreditCount)
        {
            return "terminal_transition_credit_count";
        }
        if (preferred.DistinctRequirementSetCreditCount !=
            alternative.DistinctRequirementSetCreditCount)
        {
            return "distinct_requirement_set_credit_count";
        }
        if (preferred.ExactRequirementCreditCount !=
            alternative.ExactRequirementCreditCount)
        {
            return "exact_requirement_credit_count";
        }
        if (preferred.RarestCreditedSelectionGroupCandidateCount !=
            alternative.RarestCreditedSelectionGroupCandidateCount)
        {
            return "rarest_credited_selection_group_candidate_count";
        }
        if (preferred.EstimatedTicksKnown != alternative.EstimatedTicksKnown)
            return "deterministic_estimated_ticks_known";
        if (preferred.EstimatedTicks != alternative.EstimatedTicks)
            return "deterministic_estimated_ticks";
        return preferred.EnergyCost != alternative.EnergyCost
            ? "deterministic_energy_cost"
            : null;
    }

    private static PolicyEventCandidatePrediction NeutralizeLearnerSignals(
        PolicyEventCandidatePrediction source)
    {
        var clone = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Selected current collection candidate could not be cloned.");
        clone.Rank = 1;
        clone.Score = 0;
        clone.ModelScore = null;
        clone.ExpectedReward = 0;
        clone.PolicyModelSource =
            "deterministic_teacher.current_stage_one_collection.v1";
        return clone;
    }

    private static string[] CompilationReasons(
        SmallModelPlanEnvelope plan,
        ActionQueueEnvelope queue) => plan.CandidateAudit
        .Where(value => value.Decision != "accepted")
        .SelectMany(value => value.Reasons)
        .Concat(queue.CompilerDiagnostics)
        .Concat(queue.Items.SelectMany(value =>
            value.BlockingReasons.Concat(value.MissingStateFactors)))
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
