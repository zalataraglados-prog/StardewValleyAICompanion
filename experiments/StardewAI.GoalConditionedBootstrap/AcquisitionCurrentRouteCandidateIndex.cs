using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionCurrentRouteCandidateIndex
{
    private static readonly string[] OptionIds =
    {
        "mining.reach_depth",
        "farm.collect_machine_outputs",
        "foraging.harvest_tree_product"
    };

    private AcquisitionCurrentRouteCandidateIndex(
        SnapshotEnvelope snapshot,
        AcquisitionCurrentRouteCandidate[] candidates,
        string[] blockingReasons)
    {
        Snapshot = snapshot;
        Candidates = candidates;
        BlockingReasons = blockingReasons;
    }

    private SnapshotEnvelope Snapshot { get; }

    private AcquisitionCurrentRouteCandidate[] Candidates { get; }

    public string[] BlockingReasons { get; }

    public static AcquisitionCurrentRouteCandidateIndex Read(
        string snapshotPath)
    {
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            Path.GetFullPath(snapshotPath),
            "Current authoritative-route candidate snapshot");
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            OptionIds,
            includeExecutorCalibrationOptions: true);
        var candidates = new List<AcquisitionCurrentRouteCandidate>();
        var reasons = new List<string>();
        foreach (var optionId in OptionIds)
        {
            var options = availability.Options
                .Where(value => value.OptionId == optionId)
                .ToArray();
            if (options.Length != 1)
            {
                reasons.Add(
                    "current_route_option_evaluation_ambiguous:" + optionId);
                continue;
            }
            AddCandidateSources(
                options[0].EventCandidates,
                optionId,
                candidates,
                reasons);
        }

        // Parameterized mining needs a concrete one-floor envelope to expose
        // the shared current mechanical candidate for route-source matching.
        AddCandidateSources(
            BuildRollingMiningCandidates(snapshot),
            "mining.reach_depth",
            candidates,
            reasons);
        return new AcquisitionCurrentRouteCandidateIndex(
            snapshot,
            candidates
                .Distinct()
                .OrderBy(value => value.EstimatedTicks)
                .ThenBy(value => value.LocationId, StringComparer.Ordinal)
                .ThenBy(value => value.TargetTileY)
                .ThenBy(value => value.TargetTileX)
                .ToArray(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static EventCandidate[] BuildRollingMiningCandidates(
        SnapshotEnvelope snapshot)
    {
        var parameters = BuildRollingMiningParameters(snapshot);
        if (parameters.Length == 0)
            return Array.Empty<EventCandidate>();
        return MiningReachDepthCandidateBuilder.Build(snapshot, parameters);
    }

    internal static SmallModelActionParameter[] BuildRollingMiningParameters(
        SnapshotEnvelope snapshot)
    {
        if (!TryReadCurrentMine(snapshot, out var depth, out var family))
            return Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            new SmallModelActionParameter
            {
                Name = "target_depth",
                Value = (depth + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            },
            new SmallModelActionParameter
            {
                Name = "target_location_family",
                Value = family
            }
        };
    }

    private static bool TryReadCurrentMine(
        SnapshotEnvelope snapshot,
        out int depth,
        out string family)
    {
        depth = 0;
        family = string.Empty;
        if (!snapshot.State.TryGetValue("mining", out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty("current_mine", out var field) ||
            FieldStatus(field) != "available")
        {
            return false;
        }
        var value = FieldValue(field);
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("mine_level", out var level) ||
            !level.TryGetInt32(out depth))
        {
            return false;
        }
        family = ReadString(value, "mine_kind");
        return depth >= 0 && !string.IsNullOrWhiteSpace(family);
    }

    private static void AddCandidateSources(
        IEnumerable<EventCandidate> eventCandidates,
        string optionId,
        ICollection<AcquisitionCurrentRouteCandidate> candidates,
        ICollection<string> reasons)
    {
        foreach (var candidate in eventCandidates.Where(value =>
                     value.Available && value.BlockReasons.Length == 0))
        {
            var parameters = candidate.Parameters
                .Where(value => value.Name ==
                    "authoritative_route_sources_json")
                .Select(value => value.Value)
                .ToArray();
            if (parameters.Length == 0)
                continue;
            if (parameters.Length != 1 ||
                !TryReadSources(parameters[0], out var sources))
            {
                reasons.Add(
                    "current_route_candidate_source_invalid:" +
                    candidate.CandidateId);
                continue;
            }
            foreach (var source in sources)
            {
                candidates.Add(new AcquisitionCurrentRouteCandidate(
                    candidate.CandidateId,
                    optionId,
                    candidate.LocationId,
                    ReadIntParameter(candidate, "target_tile_x") ??
                        candidate.TileX,
                    ReadIntParameter(candidate, "target_tile_y") ??
                        candidate.TileY,
                    ReadIntParameter(candidate, "stand_tile_x"),
                    ReadIntParameter(candidate, "stand_tile_y"),
                    ReadIntParameter(candidate, "max_movement_tiles"),
                    ReadIntParameter(candidate, "max_tool_swings"),
                    candidate.EstimatedTicks,
                    candidate.EnergyCost,
                    source.RouteKind,
                    source.SourceId,
                    source.QualifiedItemId));
            }
        }
    }

    private static int? ReadIntParameter(
        EventCandidate candidate,
        string name)
    {
        var values = candidate.Parameters
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return values.Length == 1 && int.TryParse(
            values[0],
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }

    public bool TryFind(
        string routeKind,
        string sourceId,
        string qualifiedItemId,
        out AcquisitionCurrentRouteCandidate[] candidates,
        out string[] blockingReasons)
    {
        candidates = Array.Empty<AcquisitionCurrentRouteCandidate>();
        var reasons = new List<string>(BlockingReasons);
        if (!CanConcludeCurrentCandidateSet(routeKind, reasons))
        {
            blockingReasons = reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return false;
        }
        candidates = Candidates.Where(value =>
                value.RouteKind == routeKind &&
                value.SourceId == sourceId &&
                value.QualifiedItemId == qualifiedItemId)
            .ToArray();
        blockingReasons = Array.Empty<string>();
        return true;
    }

    private bool CanConcludeCurrentCandidateSet(
        string routeKind,
        ICollection<string> reasons)
    {
        if (routeKind == "native_monster_drop_table")
        {
            return CanConcludeMineArray(
                "monsters",
                "current_mine_monster_terminal_evidence_incomplete",
                reasons);
        }
        if (routeKind == "native_radioactive_ore_node")
        {
            return CanConcludeMineArray(
                "objects",
                "current_mine_object_terminal_evidence_incomplete",
                reasons);
        }
        if (routeKind == "native_wild_tree_tapper_output")
        {
            var terrainComplete = TryField(
                    "current_location",
                    "terrain_features",
                    out var terrain) &&
                FieldStatus(terrain) == "available" &&
                FieldValue(terrain).ValueKind == JsonValueKind.Array;
            var machineComplete = TryField("farm", "machines", out var machines) &&
                FieldStatus(machines) == "available" &&
                FieldValue(machines).ValueKind == JsonValueKind.Array;
            if (terrainComplete && machineComplete)
                return reasons.Count == 0;
            reasons.Add("current_tree_tapper_terminal_evidence_incomplete");
            return false;
        }
        reasons.Add("current_route_candidate_kind_not_supported:" + routeKind);
        return false;
    }

    private bool CanConcludeMineArray(
        string fieldName,
        string incompleteReason,
        ICollection<string> reasons)
    {
        if (TryField("mining", fieldName, out var field) &&
            ((FieldStatus(field) == "available" &&
              FieldValue(field).ValueKind == JsonValueKind.Array) ||
             (FieldStatus(field) == "unavailable" &&
              ReadString(field, "reason") == "not_loaded_mineshaft")))
        {
            return reasons.Count == 0;
        }
        reasons.Add(incompleteReason);
        return false;
    }

    private bool TryField(
        string sectionName,
        string fieldName,
        out JsonElement field)
    {
        field = default;
        return Snapshot.State.TryGetValue(sectionName, out var section) &&
            section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty(fieldName, out field) &&
            field.ValueKind == JsonValueKind.Object;
    }

    private static string FieldStatus(JsonElement field) =>
        ReadString(field, "status");

    private static JsonElement FieldValue(JsonElement field) =>
        field.TryGetProperty("value", out var value) ? value : default;

    private static string ReadString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryReadSources(
        string json,
        out CurrentRouteSource[] sources)
    {
        sources = Array.Empty<CurrentRouteSource>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var result = new List<CurrentRouteSource>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var routeKind = ReadString(row, "route_kind");
                var sourceId = ReadString(row, "source_id");
                var qualifiedItemId = ReadString(row, "qualified_item_id");
                if (string.IsNullOrWhiteSpace(routeKind) ||
                    string.IsNullOrWhiteSpace(sourceId) ||
                    string.IsNullOrWhiteSpace(qualifiedItemId))
                {
                    return false;
                }
                result.Add(new CurrentRouteSource(
                    routeKind,
                    sourceId,
                    qualifiedItemId));
            }
            sources = result.Distinct().ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record CurrentRouteSource(
        string RouteKind,
        string SourceId,
        string QualifiedItemId);
}

internal sealed record AcquisitionCurrentRouteCandidate(
    string CandidateId,
    string OptionId,
    string LocationId,
    int? TargetTileX,
    int? TargetTileY,
    int? StandTileX,
    int? StandTileY,
    int? MaxMovementTiles,
    int? MaxToolSwings,
    int EstimatedTicks,
    int EnergyCost,
    string RouteKind,
    string SourceId,
    string QualifiedItemId);
