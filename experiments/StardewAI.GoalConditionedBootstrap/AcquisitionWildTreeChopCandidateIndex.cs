using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionWildTreeChopCandidateIndex
{
    private const string OptionId = "foraging.chop_wild_tree";

    private AcquisitionWildTreeChopCandidateIndex(
        bool evidenceComplete,
        AcquisitionWildTreeChopCandidate[] candidates,
        string[] blockingReasons)
    {
        EvidenceComplete = evidenceComplete;
        Candidates = candidates;
        BlockingReasons = blockingReasons;
    }

    public bool EvidenceComplete { get; }

    public AcquisitionWildTreeChopCandidate[] Candidates { get; }

    public string[] BlockingReasons { get; }

    public static AcquisitionWildTreeChopCandidateIndex Read(
        string snapshotPath)
    {
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            Path.GetFullPath(snapshotPath),
            "Wild-tree chop target-date snapshot");
        if (!HasAvailableTerrainFeatureEvidence(snapshot))
        {
            return Blocked(
                "current_location_terrain_features_evidence_missing");
        }

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { OptionId },
            includeExecutorCalibrationOptions: true);
        var options = availability.Options
            .Where(value => value.OptionId == OptionId)
            .ToArray();
        if (options.Length != 1)
            return Blocked("wild_tree_chop_option_evaluation_ambiguous");

        var candidates = new List<AcquisitionWildTreeChopCandidate>();
        var reasons = new List<string>();
        foreach (var candidate in options[0].EventCandidates
                     .Where(value => value.Available &&
                         value.BlockReasons.Length == 0))
        {
            if (!TryParseCandidate(candidate, out var parsed, out var reason))
            {
                reasons.Add(candidate.CandidateId + ":" + reason);
                continue;
            }
            candidates.AddRange(parsed);
        }

        if (reasons.Count > 0)
        {
            return new AcquisitionWildTreeChopCandidateIndex(
                false,
                Array.Empty<AcquisitionWildTreeChopCandidate>(),
                reasons.Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }

        return new AcquisitionWildTreeChopCandidateIndex(
            true,
            candidates
                .OrderBy(value => value.EstimatedTicks)
                .ThenBy(value => value.TargetTileY)
                .ThenBy(value => value.TargetTileX)
                .ThenBy(value => value.SourceId, StringComparer.Ordinal)
                .ToArray(),
            Array.Empty<string>());
    }

    public AcquisitionWildTreeChopCandidate[] Find(
        string routeKind,
        string sourceId,
        string qualifiedItemId,
        int requiredAmount,
        int minimumQuality) => Candidates
        .Where(value =>
            value.RouteKind == routeKind &&
            value.SourceId == sourceId &&
            value.QualifiedItemId == qualifiedItemId &&
            value.GuaranteedQuantity >= requiredAmount &&
            value.GuaranteedQuality >= minimumQuality)
        .ToArray();

    private static bool HasAvailableTerrainFeatureEvidence(
        SnapshotEnvelope snapshot) =>
        snapshot.State.TryGetValue("current_location", out var location) &&
        location.ValueKind == JsonValueKind.Object &&
        location.TryGetProperty("terrain_features", out var field) &&
        field.ValueKind == JsonValueKind.Object &&
        field.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String &&
        status.GetString() == "available" &&
        field.TryGetProperty("value", out var value) &&
        value.ValueKind == JsonValueKind.Array;

    private static bool TryParseCandidate(
        EventCandidate candidate,
        out AcquisitionWildTreeChopCandidate[] parsed,
        out string reason)
    {
        parsed = Array.Empty<AcquisitionWildTreeChopCandidate>();
        reason = string.Empty;
        if (candidate.Kind != "clear_obstacle_tile" ||
            string.IsNullOrWhiteSpace(candidate.CandidateId) ||
            string.IsNullOrWhiteSpace(candidate.LocationId) ||
            !candidate.TileX.HasValue ||
            !candidate.TileY.HasValue ||
            candidate.EstimatedTicks <= 0 ||
            candidate.EnergyCost < 0 ||
            !TryUniqueParameter(
                candidate,
                "tree_chop_projection_status",
                out var projection) ||
            projection != "exact_live_tree_and_locked_wild_tree_chop_domain" ||
            !TryUniqueParameter(
                candidate,
                "tree_chop_output_domain_contract",
                out var outputContract) ||
            outputContract !=
                "complete_stochastic_native_branch_domain_no_rng_consumed" ||
            !TryUniqueIntParameter(
                candidate,
                "route_distance_tiles",
                out var routeDistance) ||
            routeDistance < 0)
        {
            reason = "wild_tree_chop_candidate_contract_invalid";
            return false;
        }

        if (!TryUniqueParameter(
                candidate,
                "authoritative_route_sources_json",
                out var sourceJson) ||
            !TryUniqueParameter(
                candidate,
                "tree_chop_guaranteed_minimum_outputs_json",
                out var outputJson) ||
            !TryReadSources(sourceJson, out var sources) ||
            !TryReadGuaranteedOutputs(outputJson, out var outputs))
        {
            reason = "wild_tree_chop_candidate_source_or_output_invalid";
            return false;
        }

        var rows = new List<AcquisitionWildTreeChopCandidate>();
        foreach (var source in sources)
        {
            var matchingOutputs = outputs
                .Where(value => value.QualifiedItemId ==
                    source.QualifiedItemId)
                .ToArray();
            if (matchingOutputs.Length != 1)
            {
                reason = "wild_tree_chop_guaranteed_output_identity_ambiguous";
                return false;
            }
            var output = matchingOutputs[0];
            rows.Add(new AcquisitionWildTreeChopCandidate(
                candidate.CandidateId,
                candidate.LocationId,
                candidate.TileX.Value,
                candidate.TileY.Value,
                candidate.EstimatedTicks,
                candidate.EnergyCost,
                routeDistance,
                source.RouteKind,
                source.SourceId,
                source.QualifiedItemId,
                output.Quality,
                output.QuantityMin));
        }
        parsed = rows.ToArray();
        return true;
    }

    private static bool TryReadSources(
        string json,
        out WildTreeRouteSource[] sources)
    {
        sources = Array.Empty<WildTreeRouteSource>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var rows = new List<WildTreeRouteSource>();
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !TryRequiredString(value, "route_kind", out var routeKind) ||
                    routeKind != "native_wild_tree_chop_drop" ||
                    !TryRequiredString(value, "source_id", out var sourceId) ||
                    !TryRequiredString(
                        value,
                        "qualified_item_id",
                        out var qualifiedItemId))
                {
                    return false;
                }
                rows.Add(new WildTreeRouteSource(
                    routeKind,
                    sourceId,
                    qualifiedItemId));
            }
            sources = rows.Distinct().ToArray();
            return sources.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadGuaranteedOutputs(
        string json,
        out WildTreeGuaranteedOutput[] outputs)
    {
        outputs = Array.Empty<WildTreeGuaranteedOutput>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var rows = new List<WildTreeGuaranteedOutput>();
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !TryRequiredString(
                        value,
                        "qualifiedItemId",
                        out var qualifiedItemId) ||
                    !value.TryGetProperty("quality", out var quality) ||
                    !quality.TryGetInt32(out var parsedQuality) ||
                    parsedQuality < 0 ||
                    !value.TryGetProperty("quantityMin", out var quantity) ||
                    !quantity.TryGetInt32(out var parsedQuantity) ||
                    parsedQuantity <= 0)
                {
                    return false;
                }
                rows.Add(new WildTreeGuaranteedOutput(
                    qualifiedItemId,
                    parsedQuality,
                    parsedQuantity));
            }
            outputs = rows.ToArray();
            return outputs.Length > 0 &&
                outputs.GroupBy(value => value.QualifiedItemId,
                        StringComparer.Ordinal)
                    .All(group => group.Count() == 1);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryRequiredString(
        JsonElement value,
        string property,
        out string result)
    {
        result = string.Empty;
        return value.TryGetProperty(property, out var field) &&
            field.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(result = field.GetString() ?? string.Empty);
    }

    private static bool TryUniqueParameter(
        EventCandidate candidate,
        string name,
        out string value)
    {
        value = string.Empty;
        var matches = candidate.Parameters
            .Where(parameter => parameter.Name == name)
            .Select(parameter => parameter.Value)
            .ToArray();
        return matches.Length == 1 &&
            !string.IsNullOrWhiteSpace(value = matches[0]);
    }

    private static bool TryUniqueIntParameter(
        EventCandidate candidate,
        string name,
        out int value)
    {
        value = 0;
        return TryUniqueParameter(candidate, name, out var text) &&
            int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static AcquisitionWildTreeChopCandidateIndex Blocked(
        string reason) => new(
            false,
            Array.Empty<AcquisitionWildTreeChopCandidate>(),
            new[] { reason });

    private sealed record WildTreeRouteSource(
        string RouteKind,
        string SourceId,
        string QualifiedItemId);

    private sealed record WildTreeGuaranteedOutput(
        string QualifiedItemId,
        int Quality,
        int QuantityMin);
}

internal sealed record AcquisitionWildTreeChopCandidate(
    string CandidateId,
    string LocationId,
    int TargetTileX,
    int TargetTileY,
    int EstimatedTicks,
    int EnergyCost,
    int RouteDistanceTiles,
    string RouteKind,
    string SourceId,
    string QualifiedItemId,
    int GuaranteedQuality,
    int GuaranteedQuantity);
