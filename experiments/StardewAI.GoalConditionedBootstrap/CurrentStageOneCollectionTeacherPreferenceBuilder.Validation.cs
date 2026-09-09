using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherPreferenceBuilder
{
    private static readonly string[] RequiredSetIds =
    {
        "full_shipment",
        "master_angler",
        "museum_collection",
        "community_center_standard"
    };

    private static void ValidateInputs(
        CurrentStageOneCollectionTeacherFrontier frontier,
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        SnapshotEnvelope snapshot,
        string inventoryPath,
        string loweringPath,
        string rankingPath,
        string snapshotPath,
        string intentsPath)
    {
        if (frontier.SchemaVersion !=
                "current_stage_one_collection_teacher_frontier.v1" ||
            frontier.RequirementSetCount != RequiredSetIds.Length ||
            frontier.SelectionContract.ContractId !=
                "stage_one_collection_exact_current_candidate_union.v1" ||
            frontier.SelectionContract.MaximumSelectedCandidateCountPerDecision != 1 ||
            frontier.SelectionContract.SharedCandidateExecutionSemantics !=
                "execute_once_and_credit_all_exact_bindings" ||
            frontier.SelectionContract.UnavailableCandidateSemantics !=
                "defer_without_negative_label" ||
            frontier.TeacherPreferenceLabelEligible ||
            frontier.UsesLearnerRankOrScore ||
            frontier.EmitsNegativeLabelsForUnavailableRoutes ||
            !RequiredSetIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                frontier.SelectionContract.RequiredRequirementSetIds))
        {
            throw new InvalidDataException(
                "Current collection candidate contract policy drifted.");
        }
        if (string.IsNullOrWhiteSpace(frontier.GoalId) ||
            string.IsNullOrWhiteSpace(frontier.SourceStateHash) ||
            frontier.SourceStateHash != ranking.Availability.StateHash ||
            frontier.SourceStateHash != snapshot.StateHash)
        {
            throw new InvalidDataException(
                "Current collection preference inputs do not share one state and goal identity.");
        }

        RequireHash(
            frontier.FullShipment.RequirementInventorySha256,
            inventoryPath,
            "requirement inventory");
        RequireHash(
            frontier.FullShipment.AcquisitionLoweringSha256,
            loweringPath,
            "acquisition lowering");
        RequireHash(frontier.FullShipment.RankingSha256, rankingPath, "ranking");
        RequireHash(frontier.FullShipment.SnapshotSha256, snapshotPath, "snapshot");
        RequireHash(
            frontier.MasterAngler.TargetDateIntentsSha256,
            intentsPath,
            "Master Angler target-date intents");
        if (!SameHash(
                frontier.FullShipment.RequirementInventorySha256,
                frontier.MuseumAndCommunityCenter.RequirementInventorySha256) ||
            !SameHash(
                frontier.FullShipment.RequirementInventorySha256,
                frontier.MasterAngler.RequirementInventorySha256) ||
            !SameHash(
                frontier.FullShipment.AcquisitionLoweringSha256,
                frontier.MuseumAndCommunityCenter.AcquisitionLoweringSha256) ||
            !SameHash(
                frontier.FullShipment.AcquisitionLoweringSha256,
                frontier.MasterAngler.AcquisitionLoweringSha256) ||
            !SameHash(
                frontier.FullShipment.RankingSha256,
                frontier.MuseumAndCommunityCenter.RankingSha256) ||
            !SameHash(
                frontier.FullShipment.RankingSha256,
                frontier.MasterAngler.RankingSha256) ||
            !SameHash(
                frontier.FullShipment.SnapshotSha256,
                frontier.MuseumAndCommunityCenter.SnapshotSha256) ||
            !SameHash(
                frontier.FullShipment.SnapshotSha256,
                frontier.MasterAngler.SnapshotSha256))
        {
            throw new InvalidDataException(
                "Nested collection frontiers do not retain one authority input set.");
        }

        ValidateSelectionContract(frontier, ranking);
    }

    private static void ValidateSelectionContract(
        CurrentStageOneCollectionTeacherFrontier frontier,
        AvailabilityAwarePolicyPredictionEnvelope ranking)
    {
        var choices = frontier.SelectionContract.CandidateChoices;
        var groups = frontier.SelectionContract.SelectionGroups;
        if (frontier.CurrentCandidateMembershipEligible != (choices.Length > 0) ||
            choices.Any(value =>
                string.IsNullOrWhiteSpace(value.CandidateId) ||
                string.IsNullOrWhiteSpace(value.OptionId) ||
                string.IsNullOrWhiteSpace(value.Kind) ||
                value.ExecutionSemantics !=
                    "execute_once_and_credit_all_exact_bindings" ||
                value.RequirementCredits.Length == 0) ||
            choices.GroupBy(value => value.CandidateId, StringComparer.Ordinal)
                .Any(value => value.Count() != 1) ||
            groups.GroupBy(value => value.SelectionGroupId, StringComparer.Ordinal)
                .Any(value => value.Count() != 1))
        {
            throw new InvalidDataException(
                "Current collection candidate or selection-group identity is invalid.");
        }

        var currentCandidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(
            ranking).ToDictionary(value => value.CandidateId, StringComparer.Ordinal);
        var choicesById = choices.ToDictionary(
            value => value.CandidateId,
            StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            if (!currentCandidates.TryGetValue(choice.CandidateId, out var candidate) ||
                candidate.OptionId != choice.OptionId ||
                candidate.Kind != choice.Kind ||
                candidate.EstimatedTicks < 0 ||
                candidate.EnergyCost < 0)
            {
                throw new InvalidDataException(
                    "A current collection choice is absent, mismatched, or has an invalid deterministic cost: " +
                    choice.CandidateId);
            }
            if (choice.RequirementCredits.Distinct().Count() !=
                choice.RequirementCredits.Length)
            {
                throw new InvalidDataException(
                    "A current collection choice repeats an exact requirement credit: " +
                    choice.CandidateId);
            }
            foreach (var credit in choice.RequirementCredits)
            {
                var matches = groups.Where(value =>
                        value.RequirementSetId == credit.RequirementSetId &&
                        value.RequirementId == credit.RequirementId &&
                        value.CandidateIds.Contains(
                            choice.CandidateId,
                            StringComparer.Ordinal))
                    .ToArray();
                if (matches.Length != 1 ||
                    !RequiredSetIds.Contains(
                        credit.RequirementSetId,
                        StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        "A current collection credit does not map to exactly one selection group: " +
                        choice.CandidateId);
                }
            }
        }
        foreach (var group in groups)
        {
            if (!RequiredSetIds.Contains(
                    group.RequirementSetId,
                    StringComparer.Ordinal) ||
                group.MaximumSelectedCandidateCountThisDecision is < 0 or > 1 ||
                group.MaximumSelectedCandidateCountThisDecision !=
                    (group.CandidateIds.Length > 0 ? 1 : 0) ||
                group.CandidateIds.Any(value => !choicesById.ContainsKey(value)) ||
                group.CandidateIds.Distinct(StringComparer.Ordinal).Count() !=
                    group.CandidateIds.Length ||
                group.CandidateIds.Any(candidateId =>
                    choicesById[candidateId].RequirementCredits.Count(credit =>
                        credit.RequirementSetId == group.RequirementSetId &&
                        credit.RequirementId == group.RequirementId) != 1))
            {
                throw new InvalidDataException(
                    "A current collection selection group is malformed: " +
                    group.SelectionGroupId);
            }
        }
    }

    private static string[] BlockedRequirementSets(
        CurrentStageOneCollectionTeacherFrontier frontier) =>
        frontier.MuseumAndCommunityCenter.RequirementSets
            .Where(value => value.Status == "blocked" ||
                !value.TransparentStateReady)
            .Select(value => value.RequirementSetId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static void RequireHash(
        string expected,
        string path,
        string label)
    {
        if (!SameHash(expected, CurrentTeacherFrontierSupport.HashFile(path)))
            throw new InvalidDataException(label + " SHA-256 drifted.");
    }

    private static bool SameHash(string left, string right) => string.Equals(
        left,
        right,
        StringComparison.OrdinalIgnoreCase);
}
