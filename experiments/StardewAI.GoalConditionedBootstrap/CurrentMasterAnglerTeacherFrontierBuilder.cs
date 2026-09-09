using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMasterAnglerTeacherFrontierBuilder
{
    private const string RequirementSetId = "master_angler";

    public static CurrentMasterAnglerTeacherFrontier Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string snapshotPath,
        string targetDateIntentsPath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var rankingFullPath = Path.GetFullPath(rankingPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var intentsFullPath = Path.GetFullPath(targetDateIntentsPath);
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
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotFullPath,
            "Live snapshot");
        var intents = CurrentTeacherFrontierSupport.Read<
            MasterAnglerTargetDateIntentSet>(
            intentsFullPath,
            "Master Angler target-date intents");

        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryFullPath,
            inventory,
            lowering,
            "Master Angler frontier");
        var inventorySet = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            RequirementSetId,
            "requirement inventory");
        var loweringSet = CurrentTeacherFrontierSupport.SingleSet(
            lowering.RequirementSets,
            value => value.RequirementSetId,
            RequirementSetId,
            "acquisition lowering");
        ValidateSets(inventorySet, loweringSet);
        if (!string.Equals(snapshot.StateHash, ranking.Availability.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(snapshot.StateHash, intents.SourceStateHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Snapshot, ranking, and Master Angler target-date intents do not share one state_hash.");
        }

        var progress = ReadExactProgress(snapshot, inventorySet);
        var provenance = ValidateIntentProvenance(
            inventoryFullPath,
            inventory,
            inventorySet,
            snapshot,
            intents,
            progress);
        var currentCandidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(
            ranking);
        var intentsByKey = provenance.ValidatedIntents.ToDictionary(
            value => IntentKey(value.Validation),
            StringComparer.Ordinal);
        var inventoryByQualifiedItemId = inventorySet.Groups.ToDictionary(
            value => value.Alternatives.Single().QualifiedItemId,
            StringComparer.Ordinal);
        var bindings = new List<CurrentMasterAnglerCandidateBinding>();
        var rejections = new List<CurrentMasterAnglerCandidateRejection>();
        foreach (var candidate in currentCandidates.Where(HasIntentParameters))
        {
            if (!MasterAnglerCurrentCandidateMatcher.TryMatch(
                    snapshot,
                    candidate,
                    out var match,
                    out var rejectionReason))
            {
                rejections.Add(new CurrentMasterAnglerCandidateRejection(
                    candidate.CandidateId,
                    rejectionReason));
                continue;
            }
            if (!intentsByKey.TryGetValue(
                    IntentKey(match.Intent),
                    out var validatedIntent))
            {
                rejections.Add(new CurrentMasterAnglerCandidateRejection(
                    candidate.CandidateId,
                    "master_angler_candidate_intent_not_in_current_intent_set"));
                continue;
            }
            if (!inventoryByQualifiedItemId.TryGetValue(
                    match.Intent.TargetQualifiedItemId,
                    out var requirement) ||
                progress[match.Intent.TargetQualifiedItemId])
            {
                rejections.Add(new CurrentMasterAnglerCandidateRejection(
                    candidate.CandidateId,
                    "master_angler_candidate_target_is_not_a_current_missing_requirement"));
                continue;
            }

            var alternative = requirement.Alternatives.Single();
            bindings.Add(new CurrentMasterAnglerCandidateBinding(
                requirement.RequirementId,
                alternative.ItemId,
                alternative.QualifiedItemId,
                validatedIntent.Intent.IntentId,
                candidate.CandidateId,
                candidate.OptionId,
                candidate.Kind,
                candidate.Rank,
                candidate.Kind is "catch_fish" or "collect_crab_pot"
                    ? "authoritative_window_terminal_attempt"
                    : "authoritative_window_rolling_route_step",
                match.Intent.TargetLocation,
                match.Intent.SourceKind,
                match.Intent.SourceKey,
                match.Intent.EffectiveStartTime,
                match.Intent.LastCastTimeExclusive,
                validatedIntent.Intent.DeadlineSlackDays,
                match.IdentityEvidence));
        }
        bindings = bindings
            .Distinct()
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.RequirementId, StringComparer.Ordinal)
            .ThenBy(value => value.IntentId, StringComparer.Ordinal)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToList();
        var bindingsByRequirement = bindings
            .GroupBy(value => value.RequirementId, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.ToArray(),
                StringComparer.Ordinal);
        var intentsByTarget = provenance.ValidatedIntents
            .GroupBy(value => value.Validation.TargetQualifiedItemId,
                StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.ToArray(),
                StringComparer.Ordinal);
        var loweringByRequirement = loweringSet.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        var requirements = inventorySet.Groups
            .OrderBy(value => value.RequirementId, StringComparer.Ordinal)
            .Select(group => BuildRequirement(
                group,
                loweringByRequirement[group.RequirementId],
                progress,
                intentsByTarget,
                bindingsByRequirement))
            .ToArray();
        var missing = requirements.Count(value => !value.Completed);
        var matched = requirements.Count(value =>
            value.CurrentStatus == "current_positive_candidate");

        return new CurrentMasterAnglerTeacherFrontier
        {
            Status = missing == 0
                ? "goal_already_satisfied"
                : bindings.Count > 0
                    ? "ready"
                    : "no_current_matching_candidate",
            GoalId = inventory.GoalId,
            SourceStateHash = snapshot.StateHash,
            RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
                inventoryFullPath),
            AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
                loweringFullPath),
            RankingSha256 = CurrentTeacherFrontierSupport.HashFile(rankingFullPath),
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            TargetDateIntentsSha256 = CurrentTeacherFrontierSupport.HashFile(
                intentsFullPath),
            WindowIndexSha256 = intents.WindowIndexSha256,
            OpportunityCatalogSha256 = provenance.OpportunityCatalogSha256,
            RouteTimingCalibrationSha256 = intents.RouteTimingCalibrationSha256,
            RequiredGroupCount = inventorySet.RequiredGroupCount,
            ObservedGroupCount = progress.Count,
            CompletedGroupCount = requirements.Length - missing,
            MissingGroupCount = missing,
            CurrentIntentCount = provenance.ValidatedIntents.Length,
            MatchedMissingGroupCount = matched,
            CurrentCandidateBindingCount = bindings.Count,
            TrainingLabelEligible = bindings.Count > 0,
            EmitsNegativeLabelsForUnavailableRoutes = false,
            Requirements = requirements,
            CandidateBindings = bindings.ToArray(),
            RejectedIntentCandidates = rejections
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .ThenBy(value => value.Reason, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static CurrentMasterAnglerRequirement BuildRequirement(
        GoalRequirementGroup group,
        AcquisitionRequirementGroupLowering lowering,
        IReadOnlyDictionary<string, bool> progress,
        IReadOnlyDictionary<string, ValidatedIntent[]> intentsByTarget,
        IReadOnlyDictionary<string, CurrentMasterAnglerCandidateBinding[]>
            bindingsByRequirement)
    {
        var alternative = group.Alternatives.Single();
        var completed = progress[alternative.QualifiedItemId];
        var intents = intentsByTarget.GetValueOrDefault(
            alternative.QualifiedItemId,
            Array.Empty<ValidatedIntent>());
        var bindings = bindingsByRequirement.GetValueOrDefault(
            group.RequirementId,
            Array.Empty<CurrentMasterAnglerCandidateBinding>());
        var status = completed
            ? "completed"
            : bindings.Length > 0
                ? "current_positive_candidate"
                : intents.Length > 0
                    ? "deferred_no_current_validated_candidate"
                    : "deferred_no_current_date_window_intent";
        var loweredAlternative = lowering.Alternatives.Single();
        return new CurrentMasterAnglerRequirement(
            group.RequirementId,
            alternative.ItemId,
            alternative.QualifiedItemId,
            alternative.DisplayName,
            completed,
            status,
            intents.Select(value => value.Intent.IntentId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            bindings.Select(value => value.CandidateId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            loweredAlternative.Routes
                .Where(value => value.TeacherAdmissionReady)
                .SelectMany(value => value.EndpointOptionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            loweredAlternative.Routes
                .Where(value => value.TeacherAdmissionReady)
                .SelectMany(value => value.SupportingOptionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            status switch
            {
                "deferred_no_current_validated_candidate" => new[]
                {
                    "no_current_candidate_passed_the_shared_window_route_and_outcome_matcher",
                    "future_or_currently_unavailable_routes_are_not_negative_labels"
                },
                "deferred_no_current_date_window_intent" => new[]
                {
                    "no_authoritative_current_date_window_intent",
                    "future_or_currently_unavailable_routes_are_not_negative_labels"
                },
                _ => Array.Empty<string>()
            });
    }

    private static bool HasIntentParameters(
        PolicyEventCandidatePrediction candidate) => candidate.Parameters.Any(
        parameter => parameter.Name is
            "master_angler_target_qualified_item_id" or
            "continuation.master_angler_target_qualified_item_id");

    private static string IntentKey(
        MasterAnglerWindowIntentValidation intent) => string.Join(
        "|",
        intent.TargetQualifiedItemId,
        intent.TargetLocation.ToUpperInvariant(),
        intent.SourceKind,
        intent.SourceKey,
        intent.WindowFirstTotalDay,
        intent.WindowLastTotalDay,
        intent.TargetTotalDay,
        intent.EffectiveStartTime,
        intent.LastCastTimeExclusive);
}
