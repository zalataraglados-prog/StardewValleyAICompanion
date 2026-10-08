using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using System.Text.Json;

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

    internal static AcquisitionRouteDispatchCandidateMatch[]
        SelectSupportingCandidates(
            AcquisitionRouteTargetDateUnlock requirement,
            AcquisitionRequirementRouteLowering lowered,
            IEnumerable<PolicyEventCandidatePrediction> candidates,
            string expectedMachineQualifiedItemId = "") =>
        candidates
            .Where(candidate => lowered.EndpointOptionIds
                .Concat(lowered.SupportingOptionIds)
                .Contains(candidate.OptionId, StringComparer.Ordinal))
            .Select(candidate => TryMatchSupportingCandidate(
                requirement,
                candidate,
                expectedMachineQualifiedItemId))
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

    private static AcquisitionRouteDispatchCandidateMatch?
        TryMatchSupportingCandidate(
            AcquisitionRouteTargetDateUnlock requirement,
            PolicyEventCandidatePrediction candidate,
            string expectedMachineQualifiedItemId)
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

        var crop = TryMatchCropSupportingCandidate(requirement, candidate);
        if (crop is not null)
            return crop;

        var capacity = TryMatchMachineCapacitySupportingCandidate(
            requirement,
            candidate,
            expectedMachineQualifiedItemId);
        if (capacity is not null)
            return capacity;

        if (requirement.RouteKind is (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") &&
            candidate.OptionId == "economy.buy_supplies" &&
            !string.IsNullOrWhiteSpace(candidate.ShopId) &&
            !string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
        {
            return new AcquisitionRouteDispatchCandidateMatch(
                candidate,
                "candidate.shop_id+candidate.qualified_item_id+" +
                "candidate.purchase_continuation",
                "supporting_transition");
        }

        if (requirement.RouteKind is (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") &&
            candidate.OptionId == "inventory.transfer_item" &&
            candidate.Kind == "transfer_inventory_item")
        {
            return new AcquisitionRouteDispatchCandidateMatch(
                candidate,
                "deterministic_reserved_machine_input_material_staging",
                "supporting_transition");
        }

        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") ||
            candidate.Kind != "load_machine_input_tile" ||
            !TryReadUniqueParameter(
                candidate,
                "predicted_output_qualified_item_id",
                out var predictedOutputQualifiedItemId) ||
            !string.Equals(
                predictedOutputQualifiedItemId,
                requirement.QualifiedItemId,
                StringComparison.Ordinal) ||
            !CandidateDeclaresAuthoritativeRouteSource(
                candidate,
                requirement.RouteKind,
                requirement.SourceId,
                requirement.QualifiedItemId))
        {
            return null;
        }

        return new AcquisitionRouteDispatchCandidateMatch(
            candidate,
            "candidate.predicted_output_qualified_item_id+" +
            "candidate.authoritative_route_sources_json",
            "supporting_transition");
    }

    private static AcquisitionRouteDispatchCandidateMatch?
        TryMatchMachineCapacitySupportingCandidate(
            AcquisitionRouteTargetDateUnlock requirement,
            PolicyEventCandidatePrediction candidate,
            string expectedMachineQualifiedItemId)
    {
        if (requirement.RouteKind is not (
                "machine_output" or
                "native_machine_flavored_output" or
                "native_machine_item_query_output") ||
            string.IsNullOrWhiteSpace(expectedMachineQualifiedItemId) ||
            candidate.OptionId !=
                "farm.establish_supported_machine_capacity" ||
            candidate.Kind is not (
                "craft_machine_item" or "place_machine_item") ||
            !string.Equals(
                candidate.QualifiedItemId,
                expectedMachineQualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sourceParameter = candidate.Kind == "craft_machine_item"
            ? "machine_acquisition_route_support_json"
            : "machine_support_sources_json";
        if (!TryReadUniqueParameter(
                candidate,
                sourceParameter,
                out var supportJson) ||
            !TryReadMachineCapacitySupportSource(
                supportJson,
                requirement,
                expectedMachineQualifiedItemId) ||
            !TryReadUniqueParameter(
                candidate,
                "machine_support_intent_id",
                out _))
        {
            return null;
        }

        return new AcquisitionRouteDispatchCandidateMatch(
            candidate,
            "static_calendar.machine_source+" +
            "resolved_missing_runtime_machine_fleet+" +
            "candidate.machine_support_sources_json",
            "supporting_transition");
    }

    private static bool TryReadMachineCapacitySupportSource(
        string json,
        AcquisitionRouteTargetDateUnlock requirement,
        string expectedMachineQualifiedItemId)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind ==
                    System.Text.Json.JsonValueKind.Object &&
                ReadJsonString(root, "route_occurrence_id") ==
                    requirement.RouteOccurrenceId &&
                ReadJsonString(root, "route_kind") == requirement.RouteKind &&
                ReadJsonString(root, "source_id") == requirement.SourceId &&
                ReadJsonString(root, "output_qualified_item_id") ==
                    requirement.QualifiedItemId &&
                string.Equals(
                    ReadJsonString(root, "machine_qualified_item_id"),
                    expectedMachineQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(ReadJsonString(root, "goal_id"));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string ReadJsonString(
        System.Text.Json.JsonElement source,
        string name) =>
        source.TryGetProperty(name, out var value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static AcquisitionRouteDispatchCandidateMatch?
        TryMatchCropSupportingCandidate(
            AcquisitionRouteTargetDateUnlock requirement,
            PolicyEventCandidatePrediction candidate)
    {
        const string cropPrefix = "crop:";
        if (requirement.RouteKind != "harvests_as" ||
            !requirement.SourceId.StartsWith(cropPrefix, StringComparison.Ordinal) ||
            candidate.Kind != "plant_seed_tile" ||
            !TryReadUniqueParameter(
                candidate,
                "seed_id",
                out var seedId) ||
            !TryReadUniqueParameter(
                candidate,
                "harvest_source_seed_id",
                out var harvestSourceSeedId) ||
            !TryReadUniqueParameter(
                candidate,
                "harvest_item_qualified_id",
                out var harvestItemQualifiedId) ||
            !string.Equals(
                seedId,
                requirement.SourceId[cropPrefix.Length..],
                StringComparison.Ordinal) ||
            !string.Equals(seedId, harvestSourceSeedId,
                StringComparison.Ordinal) ||
            !string.Equals(harvestItemQualifiedId,
                requirement.QualifiedItemId,
                StringComparison.Ordinal) ||
            !string.Equals(candidate.ItemId, seedId, StringComparison.Ordinal) ||
            !string.Equals(candidate.QualifiedItemId, "(O)" + seedId,
                StringComparison.Ordinal))
        {
            return null;
        }

        return new AcquisitionRouteDispatchCandidateMatch(
            candidate,
            "candidate.seed_id+candidate.harvest_source_seed_id+" +
            "candidate.harvest_item_qualified_id",
            "supporting_transition");
    }

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
        var locationFishSpeciesStatus = requirement.RouteKind ==
                "native_location_fish_spawn"
            ? SnapshotFishCollectionSpeciesStatus(
                snapshot,
                requirement.QualifiedItemId)
            : FishCollectionSpeciesStatus.Unavailable;
        if (requirement.RouteKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Unavailable)
        {
            return false;
        }
        if (string.Equals(
                candidate.QualifiedItemId,
                requirement.QualifiedItemId,
                StringComparison.Ordinal) &&
            (requirement.RouteKind != "native_location_fish_spawn" ||
             locationFishSpeciesStatus == FishCollectionSpeciesStatus.Absent))
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

        if (requirement.RouteKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Absent &&
            TryMatchCompleteLocationFishingOutcome(
                requirement.SourceId,
                requirement.QualifiedItemId,
                candidate,
                out evidence))
        {
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
        out string evidence) => MatchesAuthoritativeSource(
            requirement.RouteKind,
            requirement.SourceId,
            requirement.QualifiedItemId,
            snapshot,
            candidate,
            out evidence);

    internal static bool MatchesAuthoritativeSource(
        string routeKind,
        string sourceId,
        string qualifiedItemId,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction candidate,
        out string evidence)
    {
        evidence = string.Empty;
        var sourceContract = DescribeAuthoritativeSourceContract(routeKind);
        if (sourceContract is null)
            return false;
        var locationFishSpeciesStatus = routeKind ==
                "native_location_fish_spawn"
            ? SnapshotFishCollectionSpeciesStatus(snapshot, qualifiedItemId)
            : FishCollectionSpeciesStatus.Unavailable;
        if (routeKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Unavailable)
        {
            return false;
        }

        if (routeKind == "sells" &&
            sourceId.StartsWith("shop:", StringComparison.Ordinal) &&
            string.Equals(
                candidate.ShopId,
                sourceId["shop:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "candidate.shop_id";
            return true;
        }
        if (routeKind == "harvests_as" &&
            sourceId.StartsWith("crop:", StringComparison.Ordinal) &&
            TryReadUniqueParameter(
                candidate,
                "harvest_source_seed_id",
                out var seedId) &&
            string.Equals(
                seedId,
                sourceId["crop:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "candidate.harvest_source_seed_id";
            return true;
        }
        if (routeKind == "native_location_fish_spawn" &&
            sourceId.StartsWith(
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
                sourceId["location_fish:".Length..],
                StringComparison.Ordinal))
        {
            evidence = "validated_master_angler_source_key";
            return true;
        }
        if (routeKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Absent &&
            TryMatchCompleteLocationFishingOutcome(
                sourceId,
                qualifiedItemId,
                candidate,
                out evidence))
        {
            return true;
        }
        if (routeKind == "native_mine_fishing_override" &&
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
                sourceId,
                StringComparison.Ordinal))
        {
            evidence = "validated_master_angler_mine_override";
            return true;
        }
        if (routeKind == "native_crab_pot_output" &&
            sourceId == "crab_pot_fish:" + ItemId(qualifiedItemId) &&
            candidate.Kind == "collect_crab_pot")
        {
            evidence = "exact_crab_pot_output_identity";
            return true;
        }

        var expected = FixedNativeSources.GetValueOrDefault(
            routeKind);
        if (expected is not null &&
            string.Equals(expected.SourceId, sourceId,
                StringComparison.Ordinal) &&
            expected.CandidateKinds.Contains(
                candidate.Kind,
                StringComparer.Ordinal))
        {
            evidence = "fixed_decompiled_native_source+candidate_kind";
            return true;
        }

        if (sourceContract.EvidenceMode ==
                "typed_authoritative_route_sources_json" &&
            CandidateDeclaresAuthoritativeRouteSource(
                candidate,
                routeKind,
                sourceId,
                qualifiedItemId))
        {
            evidence = "rebuilt_candidate.authoritative_route_sources";
            return true;
        }

        return false;
    }

    private static bool TryMatchCompleteLocationFishingOutcome(
        string sourceId,
        string qualifiedItemId,
        PolicyEventCandidatePrediction candidate,
        out string evidence)
    {
        evidence = string.Empty;
        const string sourcePrefix = "location_fish:";
        if (!sourceId.StartsWith(sourcePrefix, StringComparison.Ordinal) ||
            candidate.OptionId != "fishing.catch_fish" ||
            candidate.Kind != "catch_fish" ||
            !TryReadUniqueParameter(
                candidate,
                "outcome_distribution_complete",
                out var completeText) ||
            !bool.TryParse(completeText, out var complete) ||
            !complete ||
            !TryReadUniqueParameter(
                candidate,
                "outcome_distribution_json",
                out var distributionJson))
        {
            return false;
        }

        var sourceKey = sourceId[sourcePrefix.Length..];
        var separator = sourceKey.LastIndexOf(':');
        if (separator <= 0 ||
            !int.TryParse(sourceKey[(separator + 1)..], out var sourceIndex))
        {
            return false;
        }
        var expectedRuntimePrefix = "Data/Locations:" +
            sourceKey[..separator] + "#" + sourceIndex + ":";
        try
        {
            using var document = JsonDocument.Parse(distributionJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var matched = document.RootElement.EnumerateArray().Any(outcome =>
                ReadJsonString(outcome, "qualified_item_id") ==
                    qualifiedItemId &&
                ReadJsonString(outcome, "source_kind") == "rule" &&
                (ReadJsonString(outcome, "source_key") == sourceKey ||
                 ReadJsonString(outcome, "source_key").StartsWith(
                     expectedRuntimePrefix,
                     StringComparison.Ordinal)));
            if (!matched)
                return false;
            evidence = "complete_runtime_fishing_outcome_source";
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static FishCollectionSpeciesStatus
        SnapshotFishCollectionSpeciesStatus(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        if (!snapshot.State.TryGetValue("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty("fish_collection_progress", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !field.TryGetProperty("value", out var progress) ||
            progress.ValueKind != JsonValueKind.Object ||
            !progress.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return FishCollectionSpeciesStatus.Unavailable;
        }
        if (!progress.TryGetProperty(
                "eligible_species_count",
                out var eligibleSpeciesCount) ||
            !eligibleSpeciesCount.TryGetInt32(out var expectedCount) ||
            expectedCount < 0)
        {
            return FishCollectionSpeciesStatus.Unavailable;
        }
        var qualifiedItemIds = items.EnumerateArray()
            .Select(row => row.ValueKind == JsonValueKind.Object
                ? ReadJsonString(row, "qualified_item_id")
                : string.Empty)
            .ToArray();
        if (qualifiedItemIds.Length != expectedCount ||
            qualifiedItemIds.Any(string.IsNullOrWhiteSpace) ||
            qualifiedItemIds.Distinct(StringComparer.Ordinal).Count() !=
                qualifiedItemIds.Length)
        {
            return FishCollectionSpeciesStatus.Unavailable;
        }
        return qualifiedItemIds.Contains(
            qualifiedItemId,
            StringComparer.Ordinal)
                ? FishCollectionSpeciesStatus.Present
                : FishCollectionSpeciesStatus.Absent;
    }

    private enum FishCollectionSpeciesStatus
    {
        Unavailable,
        Present,
        Absent
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
