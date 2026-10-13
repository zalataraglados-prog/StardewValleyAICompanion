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
        "community_center.donate_bundle_items",
        "mining.reach_depth",
        "farm.collect_machine_outputs",
        "foraging.harvest_tree_product",
        "foraging.harvest_bushes",
        "foraging.excavate_artifact_spots",
        "fishing.service_fish_ponds",
        "processing.crack_geode"
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

    public bool HasRouteKindCandidate(string routeKind) =>
        Candidates.Any(value => value.RouteKind == routeKind);

    public static AcquisitionCurrentRouteCandidateIndex Read(
        string snapshotPath)
    {
        var fullPath = Path.GetFullPath(snapshotPath);
        return InvocationDeterministicBuildSession.GetOrBuildReference(
            "AcquisitionCurrentRouteCandidateIndex.Read",
            "v5",
            new[]
            {
                InvocationDeterministicBuildSession.File(
                    "SnapshotPath",
                    fullPath)
            },
            () => ReadCore(fullPath));
    }

    private static AcquisitionCurrentRouteCandidateIndex ReadCore(
        string snapshotPath)
    {
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
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
                    ReadIntParameter(candidate, "max_movement_tiles") ??
                        ReadIntParameter(candidate, "route_distance_tiles"),
                    ReadIntParameter(candidate, "route_distance_tiles"),
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
                    ReadStringParameter(candidate, "geode_qualified_item_id"),
                    ReadIntParameter(candidate, "geode_slot_index"),
                    ReadIntParameter(candidate, "geode_stack_before"),
                    ReadIntParameter(candidate, "geode_money_before"),
                    ReadIntParameter(candidate, "geode_price_gold"),
                    ReadIntParameter(candidate, "geode_expected_output_stack"),
                    ReadIntParameter(candidate, "geode_expected_output_quality"),
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

    public bool TryGetCurrentCalendar(
        out int totalDay,
        out string season)
    {
        totalDay = 0;
        season = string.Empty;
        if (!TryField("time", "total_days", out var totalDays) ||
            FieldStatus(totalDays) != "available" ||
            !FieldValue(totalDays).TryGetInt32(out totalDay) ||
            !TryField("time", "season", out var seasonField) ||
            FieldStatus(seasonField) != "available" ||
            FieldValue(seasonField).ValueKind != JsonValueKind.String)
        {
            return false;
        }
        season = FieldValue(seasonField).GetString() ?? string.Empty;
        return totalDay >= 0 && season is
            "spring" or "summer" or "fall" or "winter";
    }

    private bool CanConcludeCurrentCandidateSet(
        string routeKind,
        ICollection<string> reasons)
    {
        if (routeKind == "creates_reward_item")
        {
            if (TryField(
                    "world_progress",
                    "community_center",
                    out var progress) &&
                FieldStatus(progress) is "available" or "derived" &&
                FieldValue(progress).ValueKind == JsonValueKind.Object &&
                TryReadInt(
                    FieldValue(progress),
                    "bundle_data_row_count",
                    out var dataCount) &&
                TryReadInt(
                    FieldValue(progress),
                    "projected_bundle_row_count",
                    out var projectedCount) &&
                TryReadInt(
                    FieldValue(progress),
                    "unavailable_bundle_row_count",
                    out var unavailableCount) &&
                dataCount > 0 &&
                projectedCount == dataCount &&
                unavailableCount == 0 &&
                FieldValue(progress).TryGetProperty(
                    "bundle_rows",
                    out var rows) &&
                rows.ValueKind == JsonValueKind.Array &&
                rows.GetArrayLength() == dataCount &&
                rows.EnumerateArray().All(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadString(row, "projection_status") == "exact"))
            {
                return reasons.Count == 0;
            }
            reasons.Add(
                "current_community_center_reward_terminal_evidence_incomplete");
            return false;
        }
        if (routeKind is "native_bush_shake" or
            "native_tea_bush_harvest")
        {
            if (TryField(
                    "current_location",
                    "large_terrain_features",
                    out var features) &&
                FieldStatus(features) == "available" &&
                FieldValue(features).ValueKind == JsonValueKind.Array)
            {
                return reasons.Count == 0;
            }
            reasons.Add("current_bush_terminal_evidence_incomplete");
            return false;
        }
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
        if (routeKind == "native_location_artifact_spot")
        {
            if (TryField("current_location", "objects", out var objects) &&
                FieldStatus(objects) == "available" &&
                FieldValue(objects).ValueKind == JsonValueKind.Array)
            {
                return reasons.Count == 0;
            }
            reasons.Add(
                "current_location_artifact_spot_terminal_evidence_incomplete");
            return false;
        }
        if (routeKind == "native_geode_drop")
        {
            if (TryField("player", "geode_processing", out var geode) &&
                FieldStatus(geode) == "available" &&
                FieldValue(geode).ValueKind == JsonValueKind.Object &&
                ReadString(FieldValue(geode), "projection_status") ==
                    "complete_locked_base_1.6.15")
            {
                return reasons.Count == 0;
            }
            reasons.Add("current_geode_processing_terminal_evidence_incomplete");
            return false;
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

    private static bool TryReadInt(
        JsonElement value,
        string property,
        out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(property, out var field) &&
            field.TryGetInt32(out result);
    }

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
    int? RouteDistanceTiles,
    int? MaxToolSwings,
    int? MaxAttacks,
    double? EstimatedTargetCostMs,
    string SourceMatchStatus,
    double? TargetDropChancePreview,
    string TargetDropProbabilityStatus,
    int EstimatedTicks,
    int EnergyCost,
    string InputQualifiedItemId,
    int? InputSlotIndex,
    int? InputStackBefore,
    int? MoneyBefore,
    int? PriceGold,
    int? ExpectedOutputStack,
    int? ExpectedOutputQuality,
    string RouteKind,
    string SourceId,
    string QualifiedItemId);
