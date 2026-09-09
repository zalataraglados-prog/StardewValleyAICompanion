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
        var inventory = CurrentTeacherFrontierSupport.Read<AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Acquisition route lowering");
        var ranking = CurrentTeacherFrontierSupport.Read<AvailabilityAwarePolicyPredictionEnvelope>(
            rankingFullPath,
            "Availability-aware ranking");
        using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotFullPath));

        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryFullPath,
            inventory,
            lowering,
            "Full Shipment frontier");
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

        var snapshotRoot = snapshot.RootElement;
        var sourceStateHash = CurrentTeacherFrontierSupport.RequiredString(
            snapshotRoot,
            "state_hash");
        if (!string.Equals(
                sourceStateHash,
                ranking.Availability.StateHash,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ranking availability state_hash does not match the live snapshot.");
        }

        var progress = ReadFullShipmentProgress(snapshotRoot, inventorySet);
        var candidates = CurrentTeacherFrontierSupport.ReadCurrentCandidates(ranking);
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
            RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
                inventoryFullPath),
            AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
                loweringFullPath),
            RankingSha256 = CurrentTeacherFrontierSupport.HashFile(rankingFullPath),
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
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

                foreach (var match in CurrentTeacherFrontierSupport.ExactItemCandidates(
                             alternative.QualifiedItemId,
                             candidates))
                {
                    var candidate = match.Candidate;

                    var directCompletion =
                        candidate.FullShipmentKnown == true &&
                        candidate.FullShipmentEligible == true &&
                        candidate.FullShipmentAlreadyShipped == false &&
                        candidate.FullShipmentCurrentShippedCount == 0 &&
                        candidate.FullShipmentContributes == true;
                    var routes = CurrentTeacherFrontierSupport.AdmittedEndpointRoutes(
                        alternative,
                        candidate.OptionId);
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
                            ? match.IdentityEvidence + "+native_full_shipment_contribution_flags"
                            : match.IdentityEvidence,
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

}
