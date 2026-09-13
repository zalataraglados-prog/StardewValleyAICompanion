using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

public sealed record FishingTerminalProbabilityProjectionResult
{
    public string Status { get; init; } = "blocked_snapshot_projection";
    public bool Resolved { get; init; }
    public string TargetLocationId { get; init; } = string.Empty;
    public int RodSlotIndex { get; init; }
    public int BobberTileIndex { get; init; }
    public int BobberTileX { get; init; }
    public int BobberTileY { get; init; }
    public int WaterDepth { get; init; }
    public int StandTileX { get; init; }
    public int StandTileY { get; init; }
    public FishingTerminalProbabilityResult? Probability { get; init; }
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();
}

public static class FishingTerminalProbabilitySnapshotProjector
{
    public static FishingTerminalProbabilityProjectionResult Project(
        JsonElement snapshot,
        string targetLocationId,
        int rodSlotIndex,
        string targetQualifiedItemId,
        int bobberTileIndex,
        int standTileX,
        int standTileY)
    {
        var blockers = new List<string>();
        if (!TryObject(snapshot, "state", out var state) ||
            !TryObject(state, "fishing", out var fishing))
        {
            blockers.Add("fishing_forecast_state_missing");
            return Blocked(targetLocationId, rodSlotIndex, bobberTileIndex,
                standTileX, standTileY, blockers);
        }
        if (!TryFieldValue(fishing, "forecast_request", out var forecastRequest) ||
            Bool(forecastRequest, "request_complete") != true ||
            !string.Equals(
                String(forecastRequest, "target_location_id"),
                targetLocationId,
                StringComparison.Ordinal) ||
            Int(forecastRequest, "rod_slot_index") != rodSlotIndex)
        {
            blockers.Add("fishing_forecast_request_identity_mismatch");
        }
        if (!TryFieldValue(fishing, "location_context", out var locationContext) ||
            !string.Equals(
                String(locationContext, "location_id"),
                targetLocationId,
                StringComparison.Ordinal) ||
            Int(locationContext, "rod_slot_index") != rodSlotIndex ||
            Bool(locationContext, "can_fish_here") != true)
        {
            blockers.Add("fishing_forecast_location_context_mismatch");
        }
        if (!TryFieldValue(fishing, "fishable_tiles", out var fishableTiles) ||
            fishableTiles.ValueKind != JsonValueKind.Array)
        {
            blockers.Add("fishing_forecast_fishable_tiles_missing");
        }
        if (!TryFieldValue(fishing, "spawn_rules", out var spawnRules) ||
            spawnRules.ValueKind != JsonValueKind.Object)
        {
            blockers.Add("fishing_forecast_spawn_rules_missing");
        }
        if (blockers.Count > 0)
        {
            return Blocked(targetLocationId, rodSlotIndex, bobberTileIndex,
                standTileX, standTileY, blockers);
        }

        var tiles = fishableTiles.EnumerateArray().ToArray();
        if (bobberTileIndex < 0 || bobberTileIndex >= tiles.Length)
        {
            blockers.Add("fishing_forecast_bobber_tile_index_out_of_range");
            return Blocked(targetLocationId, rodSlotIndex, bobberTileIndex,
                standTileX, standTileY, blockers);
        }
        var tile = tiles[bobberTileIndex];
        var bobberX = Int(tile, "tile_x") ?? int.MinValue;
        var bobberY = Int(tile, "tile_y") ?? int.MinValue;
        var waterDepth = Int(tile, "water_depth") ?? int.MinValue;
        var fishingLevel = TryObject(spawnRules, "evaluation_context", out var context)
            ? Int(context, "fishing_level")
            : null;
        var baseFishingLevel = context.ValueKind == JsonValueKind.Object
            ? Int(context, "base_fishing_level")
            : null;
        var repeatedCastEquipmentStable =
            fishingLevel.HasValue &&
            baseFishingLevel == fishingLevel &&
            context.ValueKind == JsonValueKind.Object &&
            context.TryGetProperty(
                "selected_bait_qualified_item_id",
                out var selectedBait) &&
            selectedBait.ValueKind == JsonValueKind.Null &&
            Bool(context, "has_magic_bait") == false &&
            Bool(context, "has_curiosity_lure") == false;
        if (bobberX == int.MinValue || bobberY == int.MinValue ||
            waterDepth < 0)
        {
            blockers.Add("fishing_forecast_bobber_tile_malformed");
        }
        if (!fishingLevel.HasValue || !LegalCastGeometry(
                fishingLevel.Value,
                bobberX,
                bobberY,
                standTileX,
                standTileY))
        {
            blockers.Add("fishing_forecast_terminal_cast_geometry_invalid");
        }
        var ruleElements = default(JsonElement);
        if (Bool(spawnRules, "inventory_complete") != true ||
            !TryArray(spawnRules, "rules", out ruleElements))
        {
            blockers.Add("fishing_forecast_rule_inventory_incomplete");
        }
        if (blockers.Count > 0)
        {
            return Blocked(
                targetLocationId,
                rodSlotIndex,
                bobberTileIndex,
                standTileX,
                standTileY,
                blockers,
                bobberX,
                bobberY,
                waterDepth);
        }

        var rules = ruleElements.EnumerateArray()
            .Select(rule => ProjectRule(
                rule,
                targetQualifiedItemId,
                bobberTileIndex,
                waterDepth,
                standTileX,
                standTileY,
                fishingLevel.GetValueOrDefault(),
                repeatedCastEquipmentStable))
            .ToArray();
        var probability = FishingTerminalProbabilityEvaluator.Evaluate(
            new FishingTerminalProbabilityRequest
            {
                TargetQualifiedItemId = targetQualifiedItemId,
                FishingPassIndex = 0,
                RuleInventoryComplete = true,
                Rules = rules
            });
        return new FishingTerminalProbabilityProjectionResult
        {
            Status = probability.Resolved
                ? "resolved_fishing_terminal_probability_projection"
                : "blocked_fishing_terminal_probability_projection",
            Resolved = probability.Resolved,
            TargetLocationId = targetLocationId,
            RodSlotIndex = rodSlotIndex,
            BobberTileIndex = bobberTileIndex,
            BobberTileX = bobberX,
            BobberTileY = bobberY,
            WaterDepth = waterDepth,
            StandTileX = standTileX,
            StandTileY = standTileY,
            Probability = probability,
            BlockingReasons = probability.BlockingReasons
        };
    }

    private static FishingTerminalProbabilityRule ProjectRule(
        JsonElement rule,
        string targetQualifiedItemId,
        int bobberTileIndex,
        int waterDepth,
        int standTileX,
        int standTileY,
        int fishingLevel,
        bool repeatedCastEquipmentStable)
    {
        var ruleKey = String(rule, "rule_key");
        var conditionResolved = Bool(rule, "condition_probability_resolved") == true;
        var conditionMet = Bool(rule, "condition_met_for_probability") == true;
        var fixedBlockingReasons = Strings(rule, "blocking_reasons")
            .Where(reason => reason is not "game_state_query_false" and
                not "player_position_mismatch")
            .ToArray();
        var eligibleTile = Ints(rule, "eligible_fishable_tile_indices")
            .Contains(bobberTileIndex);
        var standAllowed = !rule.TryGetProperty("player_position", out var rectangle) ||
                           rectangle.ValueKind == JsonValueKind.Null ||
                           RectangleContains(rectangle, standTileX, standTileY);

        var selectors = Selectors(rule);
        var targetSelectorCount = selectors.Count(selector =>
            string.Equals(
                NormalizeQualifiedObjectId(selector),
                targetQualifiedItemId,
                StringComparison.Ordinal));
        var producesTarget = targetSelectorCount > 0;
        var perItemConditionResolved =
            string.IsNullOrWhiteSpace(String(rule, "per_item_condition"));
        var outputProjection = ProjectTargetOutput(
            rule,
            targetQualifiedItemId,
            selectors.Length,
            targetSelectorCount,
            waterDepth,
            perItemConditionResolved);
        var spawnChanceResolved = Bool(
            rule,
            "spawn_chance_probability_resolved") == true;
        var seeded = Bool(rule, "use_fish_caught_seeded_random") == true;
        var retryContextStable = repeatedCastEquipmentStable &&
                                 string.IsNullOrWhiteSpace(
                                     String(rule, "condition")) &&
                                 string.IsNullOrWhiteSpace(
                                     String(rule, "per_item_condition")) &&
                                 Int(rule, "min_fishing_level") is int minimumLevel &&
                                 minimumLevel <= fishingLevel &&
                                 Int(rule, "catch_limit") == -1 &&
                                 string.IsNullOrWhiteSpace(
                                     String(rule, "set_flag_on_catch")) &&
                                 !seeded;

        return new FishingTerminalProbabilityRule
        {
            RuleKey = ruleKey,
            Precedence = Int(rule, "precedence") ?? 0,
            EligibilityResolved = conditionResolved,
            EligibleBeforeRandomRolls = conditionResolved && conditionMet &&
                                        fixedBlockingReasons.Length == 0 &&
                                        eligibleTile && standAllowed,
            ProducesTarget = producesTarget,
            OutputResolutionComplete = outputProjection.Resolved,
            RetryContextStable = retryContextStable,
            SpawnRollKind = seeded
                ? FishingSpawnRollKind.DeterministicFishCaughtSeed
                : FishingSpawnRollKind.IndependentRandom,
            SeededSpawnRollPassed = seeded &&
                                    Bool(rule, "seeded_spawn_roll_resolved") == true
                ? Bool(rule, "seeded_spawn_roll_passed")
                : null,
            SpawnChance = spawnChanceResolved
                ? Double(rule, "effective_spawn_chance_preview")
                : null,
            TargetOutputSelectionProbability = outputProjection.SelectionProbability,
            TargetGenericAcceptanceProbability = outputProjection.AcceptanceProbability
        };
    }

    private static TargetOutputProjection ProjectTargetOutput(
        JsonElement rule,
        string targetQualifiedItemId,
        int selectorCount,
        int targetSelectorCount,
        int waterDepth,
        bool perItemConditionResolved)
    {
        if (targetSelectorCount == 0)
        {
            return new TargetOutputProjection(true, 0d, 0d);
        }
        if (!perItemConditionResolved || selectorCount <= 0 ||
            !TryArray(rule, "outputs", out var outputs))
        {
            return new TargetOutputProjection(false, null, null);
        }

        var matching = outputs.EnumerateArray()
            .Where(output => string.Equals(
                String(output, "qualified_item_id"),
                targetQualifiedItemId,
                StringComparison.Ordinal))
            .ToArray();
        if (matching.Length != targetSelectorCount ||
            matching.Any(output => Bool(output, "resolution_complete") != true))
        {
            return new TargetOutputProjection(false, null, null);
        }

        var acceptance = new List<double>();
        foreach (var output in matching)
        {
            if (Bool(output, "output_eligible_before_random_rolls") != true)
            {
                acceptance.Add(0d);
                continue;
            }
            if (Bool(output, "data_fish_chance_roll_pending") != true)
            {
                acceptance.Add(1d);
                continue;
            }
            if (Bool(output, "data_fish_chance_probability_resolved") != true ||
                !TryArray(output, "data_fish_chance_by_water_depth", out var chances))
            {
                return new TargetOutputProjection(false, null, null);
            }
            var chance = chances.EnumerateArray()
                .Where(row => Int(row, "water_depth") == waterDepth)
                .Select(row => Double(row, "chance_preview"))
                .FirstOrDefault(value => value.HasValue);
            if (!chance.HasValue || double.IsNaN(chance.Value) ||
                double.IsInfinity(chance.Value))
            {
                return new TargetOutputProjection(false, null, null);
            }
            acceptance.Add(Clamp(chance.Value));
        }

        return new TargetOutputProjection(
            true,
            (double)targetSelectorCount / selectorCount,
            acceptance.Average());
    }

    private static string[] Selectors(JsonElement rule)
    {
        if (string.Equals(
                String(rule, "item_selection_mode"),
                "random_item_id",
                StringComparison.Ordinal))
        {
            return Strings(rule, "random_item_ids");
        }
        var direct = String(rule, "item_id");
        return string.IsNullOrWhiteSpace(direct)
            ? Array.Empty<string>()
            : new[] { direct };
    }

    private static bool LegalCastGeometry(
        int fishingLevel,
        int bobberX,
        int bobberY,
        int standX,
        int standY)
    {
        var dx = Math.Abs(bobberX - standX);
        var dy = Math.Abs(bobberY - standY);
        if ((dx == 0) == (dy == 0))
        {
            return false;
        }
        var distance = dx + dy;
        if (distance < 2)
        {
            return false;
        }
        var addedDistance = fishingLevel >= 15 ? 4 :
            fishingLevel >= 8 ? 3 :
            fishingLevel >= 4 ? 2 :
            fishingLevel >= 1 ? 1 : 0;
        var maximum = dx > 0 ? addedDistance + 4 : addedDistance + 3;
        return distance <= maximum;
    }

    private static bool RectangleContains(
        JsonElement rectangle,
        int x,
        int y)
    {
        var left = Int(rectangle, "x");
        var top = Int(rectangle, "y");
        var width = Int(rectangle, "width");
        var height = Int(rectangle, "height");
        return left.HasValue && top.HasValue && width > 0 && height > 0 &&
               x >= left.Value && y >= top.Value &&
               x < left.Value + width.Value &&
               y < top.Value + height.Value;
    }

    private static string NormalizeQualifiedObjectId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        return value.StartsWith("(", StringComparison.Ordinal)
            ? value
            : "(O)" + value;
    }

    private static FishingTerminalProbabilityProjectionResult Blocked(
        string locationId,
        int rodSlotIndex,
        int bobberTileIndex,
        int standTileX,
        int standTileY,
        IEnumerable<string> blockers,
        int bobberTileX = 0,
        int bobberTileY = 0,
        int waterDepth = 0) => new()
        {
            TargetLocationId = locationId,
            RodSlotIndex = rodSlotIndex,
            BobberTileIndex = bobberTileIndex,
            BobberTileX = bobberTileX,
            BobberTileY = bobberTileY,
            WaterDepth = waterDepth,
            StandTileX = standTileX,
            StandTileY = standTileY,
            BlockingReasons = blockers
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray()
        };

    private static bool TryFieldValue(
        JsonElement section,
        string property,
        out JsonElement value)
    {
        value = default;
        return TryObject(section, property, out var field) &&
               string.Equals(String(field, "status"), "available", StringComparison.Ordinal) &&
               field.TryGetProperty("value", out value);
    }

    private static bool TryObject(
        JsonElement owner,
        string property,
        out JsonElement value)
    {
        value = default;
        return owner.ValueKind == JsonValueKind.Object &&
               owner.TryGetProperty(property, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryArray(
        JsonElement owner,
        string property,
        out JsonElement value)
    {
        value = default;
        return owner.ValueKind == JsonValueKind.Object &&
               owner.TryGetProperty(property, out value) &&
               value.ValueKind == JsonValueKind.Array;
    }

    private static string String(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int? Int(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static double? Double(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.TryGetDouble(out var parsed)
            ? parsed
            : null;

    private static bool? Bool(JsonElement owner, string property)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(property, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.True ? true :
            value.ValueKind == JsonValueKind.False ? false : null;
    }

    private static string[] Strings(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray()
            : Array.Empty<string>();

    private static int[] Ints(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.TryGetInt32(out _))
                .Select(item => item.GetInt32())
                .ToArray()
            : Array.Empty<int>();

    private static double Clamp(double value) =>
        value <= 0d ? 0d : value >= 1d ? 1d : value;

    private sealed record TargetOutputProjection(
        bool Resolved,
        double? SelectionProbability,
        double? AcceptanceProbability);
}
