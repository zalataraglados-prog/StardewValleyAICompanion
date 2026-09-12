using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CurrentTeacherFrontierSupport
{
    public static void ValidateAuthority(
        string inventoryPath,
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRouteOptionLoweringReport lowering,
        string label)
    {
        if (!inventory.DenominatorComplete ||
            !inventory.AcquisitionRoutesComplete ||
            !string.Equals(inventory.Status, "complete", StringComparison.Ordinal) ||
            !string.Equals(lowering.Status, "complete", StringComparison.Ordinal) ||
            !lowering.DependencyAxisInventoryComplete ||
            !StageOneCollectionRouteDependencyAxes.IsComplete(
                lowering.RequiredDownstreamDependencyAxes) ||
            lowering.RequirementSets
                .SelectMany(set => set.Groups)
                .SelectMany(group => group.Alternatives)
                .SelectMany(alternative => alternative.Routes)
                .Any(route => !StageOneCollectionRouteDependencyAxes.IsComplete(
                    route.RequiredDownstreamDependencyAxes)) ||
            !string.Equals(lowering.GoalId, inventory.GoalId, StringComparison.Ordinal) ||
            !string.Equals(
                lowering.RequirementInventorySha256,
                HashFile(inventoryPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                label + " authority inputs are incomplete or do not bind each other.");
        }
    }

    public static TSet SingleSet<TSet>(
        IEnumerable<TSet> sets,
        Func<TSet, string> id,
        string requirementSetId,
        string source)
    {
        var matches = sets.Where(value => string.Equals(
                id(value),
                requirementSetId,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Expected exactly one {requirementSetId} set in {source}.");
    }

    public static PolicyEventCandidatePrediction[] ReadCurrentCandidates(
        AvailabilityAwarePolicyPredictionEnvelope ranking)
    {
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
        return candidates;
    }

    public static IEnumerable<ExactItemCandidate> ExactItemCandidates(
        string qualifiedItemId,
        IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            yield break;

        foreach (var candidate in candidates)
        {
            var candidateQualifiedItemId = candidate.QualifiedItemId?.Trim() ??
                string.Empty;
            if (string.Equals(
                    candidateQualifiedItemId,
                    qualifiedItemId,
                    StringComparison.Ordinal))
            {
                yield return new ExactItemCandidate(
                    candidate,
                    "candidate.qualified_item_id");
            }
        }
    }

    public static CurrentRequirementRouteEvidence[] AdmittedEndpointRoutes(
        AcquisitionRequirementAlternativeLowering alternative,
        string optionId) => alternative.Routes
        .Where(value => value.TeacherAdmissionReady)
        .Where(value => value.EndpointOptionIds.Contains(
            optionId,
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

    public static bool TryReadUniqueParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        out string value)
    {
        value = string.Empty;
        var matches = (candidate.Parameters ?? Array.Empty<SmallModelActionParameter>())
            .Where(parameter => string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal))
            .Select(parameter => parameter.Value)
            .ToArray();
        if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0]))
            return false;
        value = matches[0];
        return true;
    }

    public static bool TryReadUniqueIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        out int value)
    {
        value = 0;
        return TryReadUniqueParameter(candidate, name, out var text) &&
            int.TryParse(text, out value);
    }

    public static T Read<T>(string path, string label) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options)
        ?? throw new InvalidDataException(label + " is null.");

    public static string RequiredString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var propertyValue) ||
            propertyValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(propertyValue.GetString()))
        {
            throw new InvalidDataException(
                $"Snapshot property {property} is missing.");
        }
        return propertyValue.GetString()!;
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool IsCurrentCandidate(PolicyEventCandidatePrediction value) =>
        value.Available &&
        value.AllowedToday != false &&
        !string.Equals(value.TimelineStatus, "blocked", StringComparison.Ordinal) &&
        (value.BlockReasons?.Length ?? 0) == 0;
}

internal sealed record ExactItemCandidate(
    PolicyEventCandidatePrediction Candidate,
    string IdentityEvidence);
