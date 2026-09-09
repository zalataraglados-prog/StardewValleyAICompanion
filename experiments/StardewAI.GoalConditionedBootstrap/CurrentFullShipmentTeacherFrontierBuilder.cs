using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentFullShipmentTeacherFrontierBuilder
{
    private const string RequirementSetId = "full_shipment";

    public static CurrentFullShipmentTeacherFrontier Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string snapshotPath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var rankingFullPath = Path.GetFullPath(rankingPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var inventory = Read<AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Authoritative requirement inventory");
        var lowering = Read<AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Acquisition route lowering");
        var ranking = Read<AvailabilityAwarePolicyPredictionEnvelope>(
            rankingFullPath,
            "Availability-aware ranking");
        using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotFullPath));

        ValidateAuthority(inventoryFullPath, inventory, lowering);
        var inventorySet = SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            "requirement inventory");
        var loweringSet = SingleSet(
            lowering.RequirementSets,
            value => value.RequirementSetId,
            "acquisition lowering");
        ValidateSets(inventorySet, loweringSet);

        var snapshotRoot = snapshot.RootElement;
        var sourceStateHash = RequiredString(snapshotRoot, "state_hash");
        if (!string.Equals(
                sourceStateHash,
                ranking.Availability.StateHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ranking availability state_hash does not match the live snapshot.");
        }

        var progress = ReadFullShipmentProgress(snapshotRoot, inventorySet);
        var candidates = (ranking.RankedEventCandidates ??
                Array.Empty<PolicyEventCandidatePrediction>())
            .Where(IsCurrentCandidate)
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Any(value => string.IsNullOrWhiteSpace(value.CandidateId)) ||
            candidates.GroupBy(value => value.CandidateId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidDataException(
                "Current ranked candidates require unique non-empty candidate IDs.");
        }
        var bindings = BuildBindings(loweringSet, progress, candidates);
        var bindingsByRequirement = bindings
            .GroupBy(value => value.RequirementId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        var loweringGroups = loweringSet.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        var requirements = inventorySet.Groups
            .OrderBy(value => value.RequirementId, StringComparer.Ordinal)
            .Select(group =>
            {
                var alternative = group.Alternatives.Single();
                var completed = progress[alternative.QualifiedItemId];
                var matches = bindingsByRequirement.GetValueOrDefault(
                    group.RequirementId,
                    Array.Empty<CurrentRequirementCandidateBinding>());
                var lowered = loweringGroups[group.RequirementId];
                var endpointIds = lowered.Alternatives
                    .SelectMany(value => value.Routes)
                    .Where(value => value.TeacherAdmissionReady)
                    .SelectMany(value => value.EndpointOptionIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var supportingIds = lowered.Alternatives
                    .SelectMany(value => value.Routes)
                    .Where(value => value.TeacherAdmissionReady)
                    .SelectMany(value => value.SupportingOptionIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                var status = completed
                    ? "completed"
                    : matches.Length > 0
                        ? "current_positive_candidate"
                        : "deferred_no_current_exact_candidate";
                return new CurrentFullShipmentRequirement(
                    group.RequirementId,
                    alternative.ItemId,
                    alternative.QualifiedItemId,
                    alternative.DisplayName,
                    completed,
                    status,
                    matches.Select(value => value.CandidateId)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    endpointIds,
                    supportingIds,
                    completed || matches.Length > 0
                        ? Array.Empty<string>()
                        : new[]
                        {
                            "no_current_already_gated_exact_output_candidate",
                            "future_or_currently_unavailable_routes_are_not_negative_labels"
                        });
            })
            .ToArray();
        var missing = requirements.Count(value => !value.Completed);
        var matched = requirements.Count(value =>
            value.CurrentStatus == "current_positive_candidate");
        var status = missing == 0
            ? "goal_already_satisfied"
            : bindings.Length > 0
                ? "ready"
                : "no_current_matching_candidate";

        return new CurrentFullShipmentTeacherFrontier
        {
            Status = status,
            GoalId = inventory.GoalId,
            SourceStateHash = sourceStateHash,
            RequirementInventorySha256 = HashFile(inventoryFullPath),
            AcquisitionLoweringSha256 = HashFile(loweringFullPath),
            RankingSha256 = HashFile(rankingFullPath),
            SnapshotSha256 = HashFile(snapshotFullPath),
            RequiredGroupCount = requirements.Length,
            CompletedGroupCount = requirements.Length - missing,
            MissingGroupCount = missing,
            MatchedMissingGroupCount = matched,
            CurrentCandidateBindingCount = bindings.Length,
            TrainingLabelEligible = bindings.Length > 0,
            EmitsNegativeLabelsForUnavailableRoutes = false,
            Requirements = requirements,
            CandidateBindings = bindings,
            Limitations = new[]
            {
                "This slice emits Full Shipment completion and authoritative acquisition-endpoint labels only.",
                "Supporting options require exact dependency-source binding before they may supervise the Teacher.",
                "The remaining collection requirement sets retain their existing typed completion adapters and are not inferred by this report."
            }
        };
    }

    private static CurrentRequirementCandidateBinding[] BuildBindings(
        AcquisitionRequirementSetLowering lowering,
        IReadOnlyDictionary<string, bool> progress,
        IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var result = new List<CurrentRequirementCandidateBinding>();
        foreach (var group in lowering.Groups)
        {
            foreach (var alternative in group.Alternatives)
            {
                if (progress[alternative.QualifiedItemId] ||
                    !alternative.TeacherAdmissionReady)
                {
                    continue;
                }

                foreach (var candidate in candidates)
                {
                    if (!TryReadExactOutputIdentity(
                            candidate,
                            out var candidateQualifiedItemId,
                            out var identityEvidence) ||
                        !string.Equals(
                            candidateQualifiedItemId,
                            alternative.QualifiedItemId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var directCompletion =
                        candidate.FullShipmentKnown == true &&
                        candidate.FullShipmentEligible == true &&
                        candidate.FullShipmentAlreadyShipped == false &&
                        candidate.FullShipmentCurrentShippedCount == 0 &&
                        candidate.FullShipmentContributes == true;
                    var routes = alternative.Routes
                        .Where(value => value.TeacherAdmissionReady)
                        .Where(value => value.EndpointOptionIds.Contains(
                            candidate.OptionId,
                            StringComparer.Ordinal))
                        .Select(value => new CurrentRequirementRouteEvidence(
                            value.RouteKind,
                            value.SourceId,
                            value.SourceAsset,
                            value.SourcePath))
                        .Distinct()
                        .OrderBy(value => value.RouteKind, StringComparer.Ordinal)
                        .ThenBy(value => value.SourceId, StringComparer.Ordinal)
                        .ToArray();
                    if (!directCompletion && routes.Length == 0)
                        continue;

                    result.Add(new CurrentRequirementCandidateBinding(
                        group.RequirementId,
                        alternative.ItemId,
                        alternative.QualifiedItemId,
                        candidate.CandidateId,
                        candidate.OptionId,
                        candidate.Kind,
                        candidate.Rank,
                        directCompletion
                            ? "native_full_shipment_completion"
                            : "authoritative_acquisition_endpoint",
                        directCompletion
                            ? identityEvidence + "+native_full_shipment_contribution_flags"
                            : identityEvidence,
                        routes));
                }
            }
        }
        return result
            .OrderBy(value => value.Rank)
            .ThenBy(value => value.RequirementId, StringComparer.Ordinal)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryReadExactOutputIdentity(
        PolicyEventCandidatePrediction candidate,
        out string qualifiedItemId,
        out string evidence)
    {
        qualifiedItemId = candidate.QualifiedItemId?.Trim() ?? string.Empty;
        if (qualifiedItemId.Length > 0)
        {
            evidence = "candidate.qualified_item_id";
            return true;
        }

        evidence = string.Empty;
        return false;
    }

    private static bool IsCurrentCandidate(PolicyEventCandidatePrediction value) =>
        value.Available &&
        value.AllowedToday != false &&
        !string.Equals(value.TimelineStatus, "blocked", StringComparison.Ordinal) &&
        (value.BlockReasons?.Length ?? 0) == 0;

}
