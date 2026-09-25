using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    internal static AcquisitionRouteDispatchCandidateMatch[] SelectCandidates(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        SnapshotEnvelope snapshot,
        IEnumerable<PolicyEventCandidatePrediction> candidates) => candidates
        .Where(candidate => lowered.EndpointOptionIds.Contains(
            candidate.OptionId,
            StringComparer.Ordinal))
        .Select(candidate => TryMatchCandidate(
            requirement,
            snapshot,
            candidate))
        .Where(match => match is not null)
        .Cast<AcquisitionRouteDispatchCandidateMatch>()
        .OrderBy(match => match.Candidate.AllowedNow == true ? 0 : 1)
        .ThenBy(match => match.Candidate.TimelineStatus == "ready_now" ? 0 : 1)
        .ThenBy(match => match.Candidate.ScheduledWaitCost ?? int.MaxValue)
        .ThenBy(match => match.Candidate.EstimatedTicks <= 0
            ? int.MaxValue
            : match.Candidate.EstimatedTicks)
        .ThenBy(match => match.Candidate.EnergyCost)
        .ThenBy(match => match.Candidate.LocationId, StringComparer.Ordinal)
        .ThenBy(match => match.Candidate.TileX ?? int.MaxValue)
        .ThenBy(match => match.Candidate.TileY ?? int.MaxValue)
        .ThenBy(match => match.Candidate.CandidateId, StringComparer.Ordinal)
        .ToArray();

    private static AcquisitionRouteDispatchCandidateMatch? TryMatchCandidate(
        AcquisitionRouteTargetDateUnlock requirement,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.CandidateId) ||
            (candidate.Parameters ?? Array.Empty<
                StardewAI.Contracts.Execution.SmallModelActionParameter>())
            .Any(parameter => parameter.Name.StartsWith(
                "acquisition_",
                StringComparison.Ordinal)))
        {
            return null;
        }

        if (!CandidateCanProduce(requirement, snapshot, candidate,
                out var targetEvidence) ||
            !SourceMatches(requirement, snapshot, candidate,
                out var sourceEvidence))
        {
            return null;
        }

        return new AcquisitionRouteDispatchCandidateMatch(
            candidate,
            targetEvidence + "+" + sourceEvidence);
    }

    private static bool CandidateCanProduce(
        AcquisitionRouteTargetDateUnlock requirement,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction candidate,
        out string evidence)
    {
        evidence = string.Empty;
        if (string.Equals(
                candidate.QualifiedItemId,
                requirement.QualifiedItemId,
                StringComparison.Ordinal))
        {
            evidence = "candidate.qualified_item_id";
            return true;
        }

        if (requirement.RouteKind is "native_location_fish_spawn" or
            "native_mine_fishing_override" &&
            MasterAnglerCurrentCandidateMatcher.TryMatch(
                snapshot,
                candidate,
                out var match,
                out _) &&
            string.Equals(
                match.Intent.TargetQualifiedItemId,
                requirement.QualifiedItemId,
                StringComparison.Ordinal))
        {
            evidence = match.IdentityEvidence;
            return true;
        }

        if (requirement.RouteKind is "native_geode_drop" or
                "native_geode_default_drop" &&
            candidate.Kind == "crack_geode" &&
            TryReadUniqueParameter(
                candidate,
                "geode_expected_output_qid",
                out var geodeOutput) &&
            string.Equals(
                geodeOutput,
                requirement.QualifiedItemId,
                StringComparison.Ordinal))
        {
            evidence = "candidate.geode_expected_output_qid";
            return true;
        }

        if (string.IsNullOrWhiteSpace(candidate.QualifiedItemId) &&
            CandidateDeclaresUniqueAuthoritativeRouteItem(
                candidate,
                requirement.QualifiedItemId))
        {
            evidence = "rebuilt_candidate.authoritative_route_item";
            return true;
        }

        return false;
    }

    private static bool SourceMatches(
        AcquisitionRouteTargetDateUnlock requirement,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction candidate,
        out string evidence)
    {
        evidence = string.Empty;
        if (requirement.RouteKind == "sells" &&
            requirement.SourceId.StartsWith("shop:", StringComparison.Ordinal) &&
            string.Equals(
                candidate.ShopId,
                requirement.SourceId["shop:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "candidate.shop_id";
            return true;
        }
        if (requirement.RouteKind == "harvests_as" &&
            requirement.SourceId.StartsWith("crop:", StringComparison.Ordinal) &&
            TryReadUniqueParameter(
                candidate,
                "harvest_source_seed_id",
                out var seedId) &&
            string.Equals(
                seedId,
                requirement.SourceId["crop:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "candidate.harvest_source_seed_id";
            return true;
        }
        if (requirement.RouteKind == "native_location_fish_spawn" &&
            requirement.SourceId.StartsWith(
                "location_fish:",
                StringComparison.Ordinal) &&
            MasterAnglerCurrentCandidateMatcher.TryMatch(
                snapshot,
                candidate,
                out var fishing,
                out _) &&
            string.Equals(
                fishing.Intent.SourceKind,
                "location_rule",
                StringComparison.Ordinal) &&
            string.Equals(
                fishing.Intent.SourceKey,
                requirement.SourceId["location_fish:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "validated_master_angler_source_key";
            return true;
        }
        if (requirement.RouteKind == "native_mine_fishing_override" &&
            MasterAnglerCurrentCandidateMatcher.TryMatch(
                snapshot,
                candidate,
                out var mineFishing,
                out _) &&
            string.Equals(
                mineFishing.Intent.SourceKind,
                "mine_override",
                StringComparison.Ordinal) &&
            string.Equals(
                mineFishing.Intent.SourceKey,
                requirement.SourceId,
                StringComparison.Ordinal))
        {
            evidence = "validated_master_angler_mine_override";
            return true;
        }
        if (requirement.RouteKind == "native_crab_pot_output" &&
            requirement.SourceId ==
                "crab_pot_fish:" + ItemId(requirement.QualifiedItemId) &&
            candidate.Kind == "collect_crab_pot")
        {
            evidence = "exact_crab_pot_output_identity";
            return true;
        }

        var expected = FixedNativeSources.GetValueOrDefault(
            requirement.RouteKind);
        if (expected is not null &&
            string.Equals(expected.SourceId, requirement.SourceId,
                StringComparison.Ordinal) &&
            expected.CandidateKinds.Contains(
                candidate.Kind,
                StringComparer.Ordinal))
        {
            evidence = "fixed_decompiled_native_source+candidate_kind";
            return true;
        }

        if (CandidateDeclaresAuthoritativeRouteSource(
                candidate,
                requirement.RouteKind,
                requirement.SourceId,
                requirement.QualifiedItemId))
        {
            evidence = "rebuilt_candidate.authoritative_route_sources";
            return true;
        }

        return false;
    }

    private static bool CandidateDeclaresAuthoritativeRouteSource(
        PolicyEventCandidatePrediction candidate,
        string routeKind,
        string sourceId,
        string qualifiedItemId)
    {
        if (!TryReadAuthoritativeRouteSources(candidate, out var sources))
            return false;

        var targetSources = sources
            .Where(value => string.Equals(
                value.QualifiedItemId,
                qualifiedItemId,
                StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        return targetSources.Length == 1 &&
            string.Equals(
                targetSources[0].RouteKind,
                routeKind,
                StringComparison.Ordinal) &&
            string.Equals(
                targetSources[0].SourceId,
                sourceId,
                StringComparison.Ordinal);
    }

    private static bool CandidateDeclaresUniqueAuthoritativeRouteItem(
        PolicyEventCandidatePrediction candidate,
        string qualifiedItemId) =>
        TryReadAuthoritativeRouteSources(candidate, out var sources) &&
        sources
            .Where(value => string.Equals(
                value.QualifiedItemId,
                qualifiedItemId,
                StringComparison.Ordinal))
            .Distinct()
            .Count() == 1;

    private static bool TryReadAuthoritativeRouteSources(
        PolicyEventCandidatePrediction candidate,
        out AuthoritativeRouteSource[] sources)
    {
        sources = Array.Empty<AuthoritativeRouteSource>();
        if (!TryReadUniqueParameter(
                candidate,
                "authoritative_route_sources_json",
                out var json))
        {
            return false;
        }
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind !=
                System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<AuthoritativeRouteSource>();
            foreach (var value in document.RootElement.EnumerateArray())
            {
                if (value.ValueKind !=
                        System.Text.Json.JsonValueKind.Object ||
                    !value.TryGetProperty("route_kind", out var kind) ||
                    !value.TryGetProperty("source_id", out var source) ||
                    !value.TryGetProperty("qualified_item_id", out var item) ||
                    kind.ValueKind !=
                        System.Text.Json.JsonValueKind.String ||
                    source.ValueKind !=
                        System.Text.Json.JsonValueKind.String ||
                    item.ValueKind !=
                        System.Text.Json.JsonValueKind.String)
                {
                    return false;
                }
                parsed.Add(new AuthoritativeRouteSource(
                    kind.GetString() ?? string.Empty,
                    source.GetString() ?? string.Empty,
                    item.GetString() ?? string.Empty));
            }
            sources = parsed.ToArray();
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool TryReadUniqueParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        out string value)
    {
        var matches = (candidate.Parameters ?? Array.Empty<
                StardewAI.Contracts.Execution.SmallModelActionParameter>())
            .Where(parameter => string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal))
            .Select(parameter => parameter.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        value = matches.Length == 1 ? matches[0] : string.Empty;
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(value);
    }

    private static string ItemId(string qualifiedItemId) =>
        qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal)
            ? qualifiedItemId[3..]
            : qualifiedItemId;

    private static readonly IReadOnlyDictionary<string, FixedNativeSource>
        FixedNativeSources = new Dictionary<string, FixedNativeSource>(
            StringComparer.Ordinal)
        {
            ["native_bush_shake"] = new(
                "Bush.GetShakeOffItem",
                new[] { "harvest_bush" }),
            ["native_tea_bush_harvest"] = new(
                "Bush.GetShakeOffItem",
                new[] { "harvest_bush" }),
            ["native_spring_onion_harvest"] = new(
                "Crop.harvest",
                new[] { "harvest_crop_tile" }),
            ["native_ginger_harvest"] = new(
                "Crop.hitWithHoe",
                new[] { "harvest_ginger" }),
            ["native_tree_moss_harvest"] = new(
                "Tree.CreateMossItem",
                new[] { "harvest_tree_moss" })
        };

    private sealed record FixedNativeSource(
        string SourceId,
        string[] CandidateKinds);

    private sealed record AuthoritativeRouteSource(
        string RouteKind,
        string SourceId,
        string QualifiedItemId);
}
