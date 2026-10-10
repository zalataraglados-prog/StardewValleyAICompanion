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
        "foraging.harvest_tree_product",
        "fishing.service_fish_ponds"
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
            reasons,
            excludedRouteKind: "native_monster_drop_table");
        AddCandidateSources(
            BuildCurrentMonsterDropCandidates(snapshot),
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

    private static EventCandidate[] BuildCurrentMonsterDropCandidates(
        SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("mining", out var mining) ||
            mining.ValueKind != JsonValueKind.Object ||
            !mining.TryGetProperty("monsters", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            FieldStatus(field) != "available" ||
            FieldValue(field).ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var sources = FieldValue(field).EnumerateArray()
            .Where(monster => monster.ValueKind == JsonValueKind.Object)
            .SelectMany(monster =>
                monster.TryGetProperty(
                    "authoritative_route_sources",
                    out var rows) &&
                rows.ValueKind == JsonValueKind.Array
                    ? rows.EnumerateArray().ToArray()
                    : Array.Empty<JsonElement>())
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new CurrentRouteSource(
                ReadString(row, "route_kind"),
                ReadString(row, "source_id"),
                ReadString(row, "qualified_item_id")))
            .Where(source =>
                source.RouteKind == "native_monster_drop_table" &&
                source.SourceId.StartsWith("monster:",
                    StringComparison.Ordinal) &&
                source.SourceId.Length > "monster:".Length &&
                !string.IsNullOrWhiteSpace(source.QualifiedItemId))
            .Distinct()
            .OrderBy(source => source.SourceId, StringComparer.Ordinal)
            .ThenBy(source => source.QualifiedItemId,
                StringComparer.Ordinal)
            .ToArray();
        return sources.SelectMany(source =>
                MiningReachDepthCandidateBuilder.Build(
                    snapshot,
                    BuildRollingMiningParameters(
                        snapshot,
                        source.RouteKind,
                        source.SourceId,
                        source.QualifiedItemId)))
            .ToArray();
    }

    internal static SmallModelActionParameter[] BuildRollingMiningParameters(
        SnapshotEnvelope snapshot,
        string acquisitionRouteKind = "",
        string acquisitionSourceId = "",
        string acquisitionQualifiedItemId = "")
    {
        if (!TryReadCurrentMine(snapshot, out var depth, out var family))
            return Array.Empty<SmallModelActionParameter>();
        var parameters = new List<SmallModelActionParameter>
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
        if (!string.IsNullOrWhiteSpace(acquisitionRouteKind) ||
            !string.IsNullOrWhiteSpace(acquisitionSourceId) ||
            !string.IsNullOrWhiteSpace(acquisitionQualifiedItemId))
        {
            parameters.Add(new SmallModelActionParameter
            {
                Name = "acquisition_target_route_kind",
                Value = acquisitionRouteKind
            });
            parameters.Add(new SmallModelActionParameter
            {
                Name = "acquisition_target_source_id",
                Value = acquisitionSourceId
            });
            parameters.Add(new SmallModelActionParameter
            {
                Name = "acquisition_target_qualified_item_id",
                Value = acquisitionQualifiedItemId
            });
        }
        return parameters.ToArray();
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
        ICollection<string> reasons,
        string excludedRouteKind = "")
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
                if (source.RouteKind == excludedRouteKind)
                    continue;
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
                    ReadIntParameter(candidate, "max_attacks"),
                    ReadDoubleParameter(candidate, "estimated_target_cost_ms"),
                    ReadStringParameter(candidate, "source_match_status"),
                    ReadDoubleParameter(candidate, "target_drop_chance_preview"),
                    ReadStringParameter(
                        candidate,
                        "target_drop_probability_status"),
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

    private static double? ReadDoubleParameter(
        EventCandidate candidate,
        string name)
    {
        var values = candidate.Parameters
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return values.Length == 1 && double.TryParse(
            values[0],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }

    private static string ReadStringParameter(
        EventCandidate candidate,
        string name)
    {
        var values = candidate.Parameters
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
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
        if (routeKind == "native_fish_pond_output")
        {
            if (TryField("farm", "buildings", out var buildings) &&
                FieldStatus(buildings) == "available" &&
                FieldValue(buildings).ValueKind == JsonValueKind.Array)
            {
                return reasons.Count == 0;
            }
            reasons.Add("current_fish_pond_terminal_evidence_incomplete");
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
    int? MaxAttacks,
    double? EstimatedTargetCostMs,
    string SourceMatchStatus,
    double? TargetDropChancePreview,
    string TargetDropProbabilityStatus,
    int EstimatedTicks,
    int EnergyCost,
    string RouteKind,
    string SourceId,
    string QualifiedItemId);
