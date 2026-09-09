using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCollectionTeacherFrontierBuilder
{
    private const string MuseumSetId = "museum_collection";
    private const string CommunityCenterSetId = "community_center_standard";

    public static CurrentCollectionTeacherFrontier Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string snapshotPath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var rankingFullPath = Path.GetFullPath(rankingPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Acquisition route lowering");
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingFullPath,
            "Availability-aware ranking");
        using var snapshot = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));

        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryFullPath,
            inventory,
            lowering,
            "Collection frontier");
        var museumInventory = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            MuseumSetId,
            "requirement inventory");
        var museumLowering = CurrentTeacherFrontierSupport.SingleSet(
            lowering.RequirementSets,
            value => value.RequirementSetId,
            MuseumSetId,
            "acquisition lowering");
        var communityCenterInventory = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            CommunityCenterSetId,
            "requirement inventory");
        var communityCenterLowering = CurrentTeacherFrontierSupport.SingleSet(
            lowering.RequirementSets,
            value => value.RequirementSetId,
            CommunityCenterSetId,
            "acquisition lowering");
        ValidateMuseumSets(museumInventory, museumLowering);
        ValidateCommunityCenterSets(
            communityCenterInventory,
            communityCenterLowering);

        var sourceStateHash = CurrentTeacherFrontierSupport.RequiredString(
            snapshot.RootElement,
            "state_hash");
        if (!string.Equals(
                sourceStateHash,
                ranking.Availability.StateHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ranking availability state_hash does not match the live snapshot.");
        }

        var candidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(ranking);
        var museumProgress = ReadMuseumProgress(
            snapshot.RootElement,
            museumInventory);
        var communityCenterProgress = ReadCommunityCenterProgress(
            snapshot.RootElement,
            communityCenterInventory);
        var museum = BuildMuseumSet(
            museumInventory,
            museumLowering,
            museumProgress,
            candidates,
            out var museumBindings);
        var communityCenter = BuildCommunityCenterSet(
            communityCenterInventory,
            communityCenterLowering,
            communityCenterProgress,
            candidates,
            out var communityCenterBindings);
        var sets = new[] { museum, communityCenter };
        var bindings = museumBindings
            .Concat(communityCenterBindings)
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.RequirementSetId, StringComparer.Ordinal)
            .ThenBy(value => value.RequirementId, StringComparer.Ordinal)
            .ThenBy(value => value.AlternativeIndex)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();

        return new CurrentCollectionTeacherFrontier
        {
            Status = OverallStatus(sets),
            GoalId = inventory.GoalId,
            SourceStateHash = sourceStateHash,
            RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
                inventoryFullPath),
            AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
                loweringFullPath),
            RankingSha256 = CurrentTeacherFrontierSupport.HashFile(rankingFullPath),
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            TrainingLabelEligible = bindings.Length > 0,
            EmitsNegativeLabelsForUnavailableRoutes = false,
            RequirementSets = sets,
            CandidateBindings = bindings
        };
    }

    private static CurrentCollectionRequirementSet BuildMuseumSet(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering,
        CurrentCollectionProgress progress,
        PolicyEventCandidatePrediction[] candidates,
        out CurrentCollectionCandidateBinding[] bindings)
    {
        return BuildSet(
            inventory,
            lowering,
            progress,
            candidates,
            BuildMuseumBindings,
            out bindings);
    }

    private static CurrentCollectionRequirementSet BuildCommunityCenterSet(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering,
        CurrentCollectionProgress progress,
        PolicyEventCandidatePrediction[] candidates,
        out CurrentCollectionCandidateBinding[] bindings)
    {
        return BuildSet(
            inventory,
            lowering,
            progress,
            candidates,
            BuildCommunityCenterBindings,
            out bindings);
    }

    private static CurrentCollectionRequirementSet BuildSet(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering,
        CurrentCollectionProgress progress,
        PolicyEventCandidatePrediction[] candidates,
        RequirementBindingBuilder bindingBuilder,
        out CurrentCollectionCandidateBinding[] bindings)
    {
        var resultBindings = new List<CurrentCollectionCandidateBinding>();
        var loweringGroups = lowering.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        var requirements = inventory.Groups
            .OrderBy(value => value.RequirementId, StringComparer.Ordinal)
            .Select(group =>
            {
                var lowered = loweringGroups[group.RequirementId];
                if (!progress.Available)
                {
                    return UnknownRequirement(group, lowered, progress.BlockingReasons);
                }

                var current = progress.Requirements[group.RequirementId];
                var currentBindings = progress.BlockingReasons.Length == 0 &&
                        !current.Completed
                    ? bindingBuilder(
                        inventory.RequirementSetId,
                        group,
                        lowered,
                        current,
                        progress,
                        candidates)
                    : Array.Empty<CurrentCollectionCandidateBinding>();
                resultBindings.AddRange(currentBindings);
                var bindingsByAlternative = currentBindings
                    .GroupBy(value => value.AlternativeIndex)
                    .ToDictionary(
                        value => value.Key,
                        value => value.ToArray());
                var alternatives = BuildAlternatives(
                    group,
                    lowered,
                    current,
                    bindingsByAlternative,
                    inventory.RequirementSetId);
                var status = current.Completed
                    ? "completed"
                    : progress.BlockingReasons.Length > 0
                        ? "blocked_requirement_set"
                        : currentBindings.Length > 0
                            ? "current_positive_candidate"
                            : "deferred_no_current_exact_candidate";
                return new CurrentCollectionRequirement
                {
                    RequirementId = group.RequirementId,
                    SelectionRule = group.SelectionRule,
                    RequiredAlternativeCount = group.RequiredAlternativeCount,
                    CompletedAlternativeCount = current.CompletedAlternativeCount,
                    RemainingSlotCount = current.RemainingSlotCount,
                    Completed = current.Completed,
                    CurrentStatus = status,
                    Alternatives = alternatives,
                    DeferReasons = status switch
                    {
                        "blocked_requirement_set" => progress.BlockingReasons,
                        "deferred_no_current_exact_candidate" => new[]
                        {
                            "no_current_already_gated_exact_output_candidate",
                            "future_or_currently_unavailable_routes_are_not_negative_labels"
                        },
                        _ => Array.Empty<string>()
                    }
                };
            })
            .ToArray();
        bindings = resultBindings.ToArray();
        var completed = requirements.Count(value => value.Completed == true);
        var missing = requirements.Count(value => value.Completed == false);
        var matched = requirements.Count(value =>
            value.CurrentStatus == "current_positive_candidate");
        var status = !progress.Available || progress.BlockingReasons.Length > 0
            ? "blocked"
            : missing == 0
                ? "goal_already_satisfied"
                : bindings.Length > 0
                    ? "ready"
                    : "no_current_matching_candidate";
        return new CurrentCollectionRequirementSet
        {
            RequirementSetId = inventory.RequirementSetId,
            Status = status,
            TransparentStateReady = progress.Available,
            RequiredGroupCount = inventory.RequiredGroupCount,
            ObservedGroupCount = progress.Available
                ? progress.Requirements.Count
                : 0,
            CompletedGroupCount = completed,
            MissingGroupCount = missing,
            MatchedMissingGroupCount = matched,
            TrainingLabelEligible = bindings.Length > 0,
            Requirements = requirements,
            BlockingReasons = progress.BlockingReasons
        };
    }

    private static CurrentCollectionRequirement UnknownRequirement(
        GoalRequirementGroup group,
        AcquisitionRequirementGroupLowering lowering,
        string[] blockingReasons) => new()
    {
        RequirementId = group.RequirementId,
        SelectionRule = group.SelectionRule,
        RequiredAlternativeCount = group.RequiredAlternativeCount,
        CompletedAlternativeCount = null,
        RemainingSlotCount = null,
        Completed = null,
        CurrentStatus = "blocked_transparent_state_unavailable",
        Alternatives = group.Alternatives.Select((alternative, index) =>
            BuildAlternative(
                alternative,
                lowering.Alternatives[index],
                index,
                null,
                Array.Empty<CurrentCollectionCandidateBinding>(),
                group.RequirementId.StartsWith(
                    "community_center:",
                    StringComparison.Ordinal)
                    ? CommunityCenterSetId
                    : MuseumSetId)).ToArray(),
        DeferReasons = blockingReasons
    };

    private static CurrentCollectionAlternative[] BuildAlternatives(
        GoalRequirementGroup group,
        AcquisitionRequirementGroupLowering lowering,
        CurrentCollectionRequirementProgress progress,
        IReadOnlyDictionary<int, CurrentCollectionCandidateBinding[]> bindings,
        string requirementSetId) => group.Alternatives
        .Select((alternative, index) => BuildAlternative(
            alternative,
            lowering.Alternatives[index],
            index,
            progress.CompletedAlternatives[index],
            bindings.GetValueOrDefault(
                index,
                Array.Empty<CurrentCollectionCandidateBinding>()),
            requirementSetId))
        .ToArray();

    private static CurrentCollectionAlternative BuildAlternative(
        GoalRequirementAlternative alternative,
        AcquisitionRequirementAlternativeLowering lowering,
        int index,
        bool? completed,
        CurrentCollectionCandidateBinding[] bindings,
        string requirementSetId) => new(
        index,
        alternative.ItemId,
        alternative.QualifiedItemId,
        alternative.DisplayName,
        alternative.MatchKind,
        alternative.Amount,
        alternative.MinimumQuality,
        completed,
        ReservationSemantics(requirementSetId, alternative.MatchKind),
        bindings.Select(value => value.CandidateId)
            .Distinct(StringComparer.Ordinal)
            .ToArray(),
        lowering.Routes
            .Where(value => value.TeacherAdmissionReady)
            .SelectMany(value => value.EndpointOptionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray(),
        lowering.Routes
            .Where(value => value.TeacherAdmissionReady)
            .SelectMany(value => value.SupportingOptionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());

    private static string ReservationSemantics(
        string requirementSetId,
        string matchKind) => requirementSetId switch
    {
        MuseumSetId =>
            "reserve_exact_item_until_native_museum_donation",
        CommunityCenterSetId when matchKind == "money_payment" =>
            "reserve_money_until_exact_native_bundle_payment",
        CommunityCenterSetId =>
            "reserve_required_quantity_at_minimum_quality_until_native_bundle_donation",
        _ => "unknown"
    };

    private static string OverallStatus(
        IEnumerable<CurrentCollectionRequirementSet> sets)
    {
        var values = sets.ToArray();
        if (values.Any(value => value.Status == "ready"))
        {
            return values.Any(value => value.Status == "blocked")
                ? "ready_with_blocked_requirement_sets"
                : "ready";
        }
        if (values.Any(value => value.Status == "blocked"))
            return "contains_blocked_requirement_sets";
        if (values.All(value => value.Status == "goal_already_satisfied"))
            return "goal_already_satisfied";
        return "no_current_matching_candidate";
    }

    private delegate CurrentCollectionCandidateBinding[] RequirementBindingBuilder(
        string requirementSetId,
        GoalRequirementGroup group,
        AcquisitionRequirementGroupLowering lowering,
        CurrentCollectionRequirementProgress progress,
        CurrentCollectionProgress setProgress,
        PolicyEventCandidatePrediction[] candidates);
}
