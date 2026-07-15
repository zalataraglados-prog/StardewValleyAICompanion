using System.Globalization;
using System.Text.Json.Serialization;

namespace StardewAI.TransparentBridge.Adapters;

public sealed record FishingRectangleRead
{
    [JsonPropertyName("x")]
    public int X { get; init; }

    [JsonPropertyName("y")]
    public int Y { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }

    public bool Contains(int x, int y)
    {
        return x >= X && y >= Y && x < X + Width && y < Y + Height;
    }
}

public sealed record FishingRuleEligibilitySpec
{
    public string? Season { get; init; }
    public string? FishAreaId { get; init; }
    public FishingRectangleRead? BobberPosition { get; init; }
    public FishingRectangleRead? PlayerPosition { get; init; }
    public int MinFishingLevel { get; init; }
    public int MinDistanceFromShore { get; init; }
    public int MaxDistanceFromShore { get; init; } = -1;
    public bool RequireMagicBait { get; init; }
    public bool ConditionMet { get; init; }
}

public sealed record FishingRuleEligibilityContext
{
    public string Season { get; init; } = string.Empty;
    public int PlayerTileX { get; init; }
    public int PlayerTileY { get; init; }
    public int FishingLevel { get; init; }
    public bool HasMagicBait { get; init; }
}

public sealed record FishingRuleEligibilityRead
{
    [JsonPropertyName("eligible_before_random_rolls")]
    public bool EligibleBeforeRandomRolls { get; init; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();

    [JsonPropertyName("eligible_tiles")]
    public FishingTileReadRow[] EligibleTiles { get; init; } = Array.Empty<FishingTileReadRow>();
}

public static class FishingSpawnRuleEvaluator
{
    public static FishingRuleEligibilityRead Evaluate(
        FishingRuleEligibilitySpec rule,
        FishingRuleEligibilityContext context,
        IReadOnlyList<FishingTileReadRow> fishableTiles)
    {
        var reasons = new List<string>();
        if (rule.Season is not null
            && !context.HasMagicBait
            && !string.Equals(rule.Season, context.Season, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("season_mismatch");
        }

        if (rule.PlayerPosition is not null
            && !rule.PlayerPosition.Contains(context.PlayerTileX, context.PlayerTileY))
        {
            reasons.Add("player_position_mismatch");
        }

        if (context.FishingLevel < rule.MinFishingLevel)
        {
            reasons.Add("fishing_level_too_low");
        }

        if (rule.RequireMagicBait && !context.HasMagicBait)
        {
            reasons.Add("magic_bait_required");
        }

        if (!rule.ConditionMet)
        {
            reasons.Add("game_state_query_false");
        }

        var eligibleTiles = fishableTiles
            .Where(tile => RuleAllowsTile(rule, tile))
            .ToArray();
        if (eligibleTiles.Length == 0)
        {
            reasons.Add("no_matching_fishable_tile");
        }

        return new FishingRuleEligibilityRead
        {
            EligibleBeforeRandomRolls = reasons.Count == 0,
            BlockingReasons = reasons.ToArray(),
            EligibleTiles = eligibleTiles
        };
    }

    private static bool RuleAllowsTile(FishingRuleEligibilitySpec rule, FishingTileReadRow tile)
    {
        if (rule.FishAreaId is not null
            && !string.Equals(rule.FishAreaId, tile.FishAreaId, StringComparison.Ordinal))
        {
            return false;
        }

        if (rule.BobberPosition is not null && !rule.BobberPosition.Contains(tile.TileX, tile.TileY))
        {
            return false;
        }

        return tile.WaterDepth >= rule.MinDistanceFromShore
            && (rule.MaxDistanceFromShore < 0 || tile.WaterDepth <= rule.MaxDistanceFromShore);
    }
}

public sealed record FishingTimeWindowRead
{
    [JsonPropertyName("start_time")]
    public int StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public int EndTime { get; init; }
}

public sealed record FishingDataFishRequirementsRead
{
    [JsonPropertyName("parse_status")]
    public string ParseStatus { get; init; } = "error";

    [JsonPropertyName("parse_errors")]
    public string[] ParseErrors { get; init; } = Array.Empty<string>();

    [JsonPropertyName("is_trap_fish")]
    public bool IsTrapFish { get; init; }

    [JsonPropertyName("difficulty")]
    public int? Difficulty { get; init; }

    [JsonPropertyName("time_windows")]
    public FishingTimeWindowRead[] TimeWindows { get; init; } = Array.Empty<FishingTimeWindowRead>();

    [JsonPropertyName("weather")]
    public string? Weather { get; init; }

    [JsonPropertyName("max_depth")]
    public int? MaxDepth { get; init; }

    [JsonPropertyName("base_chance")]
    public float? BaseChance { get; init; }

    [JsonPropertyName("depth_multiplier")]
    public float? DepthMultiplier { get; init; }

    [JsonPropertyName("min_fishing_level")]
    public int? MinFishingLevel { get; init; }

    [JsonPropertyName("tutorial_fish")]
    public bool TutorialFish { get; init; }
}

public sealed record FishingDataFishEligibilityContext
{
    public int TimeOfDay { get; init; }
    public bool IsRaining { get; init; }
    public int FishingLevel { get; init; }
    public bool HasMagicBait { get; init; }
    public bool UsesTrainingRod { get; init; }
    public bool IsTutorialCatch { get; init; }
}

public sealed record FishingDataFishEligibilityRead
{
    [JsonPropertyName("eligible_before_random_roll")]
    public bool EligibleBeforeRandomRoll { get; init; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();
}

public static class FishingDataFishRuleParser
{
    public static FishingDataFishRequirementsRead Parse(string raw)
    {
        var fields = raw.Split('/');
        var errors = new List<string>();
        var type = Get(fields, 1);
        if (string.Equals(type, "trap", StringComparison.Ordinal))
        {
            return new FishingDataFishRequirementsRead
            {
                ParseStatus = "trap",
                ParseErrors = Array.Empty<string>(),
                IsTrapFish = true,
                TimeWindows = Array.Empty<FishingTimeWindowRead>()
            };
        }

        var difficulty = ParseInt(fields, 1, "difficulty", errors);
        var windows = ParseTimeWindows(Get(fields, 5), errors);
        var maxDepth = ParseInt(fields, 9, "max_depth", errors);
        var baseChance = ParseFloat(fields, 10, "base_chance", errors);
        var depthMultiplier = ParseFloat(fields, 11, "depth_multiplier", errors);
        var minFishingLevel = ParseInt(fields, 12, "min_fishing_level", errors);
        var tutorialFish = ParseOptionalBool(fields, 13, "tutorial_fish", errors);

        return new FishingDataFishRequirementsRead
        {
            ParseStatus = errors.Count == 0 ? "parsed" : "error",
            ParseErrors = errors.ToArray(),
            IsTrapFish = false,
            Difficulty = difficulty,
            TimeWindows = windows,
            Weather = Get(fields, 7),
            MaxDepth = maxDepth,
            BaseChance = baseChance,
            DepthMultiplier = depthMultiplier,
            MinFishingLevel = minFishingLevel,
            TutorialFish = tutorialFish
        };
    }

    public static FishingDataFishEligibilityRead Evaluate(
        FishingDataFishRequirementsRead requirements,
        FishingDataFishEligibilityContext context,
        bool? canUseTrainingRod,
        bool ignoreFishDataRequirements)
    {
        var reasons = new List<string>();
        if (requirements.ParseStatus == "error")
        {
            reasons.Add("invalid_data_fish_row");
            return Result(reasons);
        }

        if (requirements.IsTrapFish)
        {
            if (context.IsTutorialCatch)
            {
                reasons.Add("trap_not_allowed_for_tutorial_catch");
            }
            return Result(reasons);
        }

        if (context.UsesTrainingRod)
        {
            if (canUseTrainingRod == false)
            {
                reasons.Add("training_rod_disallowed_by_spawn_rule");
            }
            else if (canUseTrainingRod is null && requirements.Difficulty >= 50)
            {
                reasons.Add("training_rod_difficulty_limit");
            }
        }

        if (context.IsTutorialCatch && !requirements.TutorialFish)
        {
            reasons.Add("not_tutorial_fish");
        }

        if (ignoreFishDataRequirements)
        {
            return Result(reasons);
        }

        if (!context.HasMagicBait
            && !requirements.TimeWindows.Any(window => context.TimeOfDay >= window.StartTime && context.TimeOfDay < window.EndTime))
        {
            reasons.Add("outside_data_fish_time_window");
        }

        if (!context.HasMagicBait)
        {
            if (string.Equals(requirements.Weather, "rainy", StringComparison.Ordinal)
                && !context.IsRaining)
            {
                reasons.Add("rain_required");
            }
            else if (string.Equals(requirements.Weather, "sunny", StringComparison.Ordinal)
                && context.IsRaining)
            {
                reasons.Add("clear_weather_required");
            }
        }

        if (requirements.MinFishingLevel.HasValue
            && context.FishingLevel < requirements.MinFishingLevel.Value)
        {
            reasons.Add("data_fish_level_too_low");
        }

        return Result(reasons);
    }

    private static FishingDataFishEligibilityRead Result(List<string> reasons)
    {
        return new FishingDataFishEligibilityRead
        {
            EligibleBeforeRandomRoll = reasons.Count == 0,
            BlockingReasons = reasons.ToArray()
        };
    }

    private static string? Get(string[] fields, int index)
    {
        return index >= 0 && index < fields.Length ? fields[index] : null;
    }

    private static int? ParseInt(string[] fields, int index, string name, List<string> errors)
    {
        var raw = Get(fields, index);
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{name}_invalid");
        return null;
    }

    private static float? ParseFloat(string[] fields, int index, string name, List<string> errors)
    {
        var raw = Get(fields, index);
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{name}_invalid");
        return null;
    }

    private static bool ParseOptionalBool(string[] fields, int index, string name, List<string> errors)
    {
        var raw = Get(fields, index);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out var value))
        {
            return value;
        }

        errors.Add($"{name}_invalid");
        return false;
    }

    private static FishingTimeWindowRead[] ParseTimeWindows(string? raw, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<FishingTimeWindowRead>();
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length % 2 != 0)
        {
            errors.Add("time_windows_unpaired");
            return Array.Empty<FishingTimeWindowRead>();
        }

        var windows = new List<FishingTimeWindowRead>();
        for (var index = 0; index < parts.Length; index += 2)
        {
            if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)
                || !int.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
            {
                errors.Add("time_windows_invalid");
                return Array.Empty<FishingTimeWindowRead>();
            }

            windows.Add(new FishingTimeWindowRead { StartTime = start, EndTime = end });
        }

        return windows.ToArray();
    }
}
