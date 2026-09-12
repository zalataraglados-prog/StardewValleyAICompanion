using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private const string NativeCropTexture = @"TileSheets\crops";

    private static readonly IReadOnlyDictionary<string, string[]> WildSeedOutputs =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["495"] = new[] { "(O)16", "(O)18", "(O)20", "(O)22" },
            ["496"] = new[] { "(O)396", "(O)398", "(O)402" },
            ["497"] = new[] { "(O)404", "(O)406", "(O)408", "(O)410" },
            ["498"] = new[] { "(O)412", "(O)414", "(O)416", "(O)418" }
        };

    private static CalendarSourceResolution ResolveCropWindows(
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        JsonElement crops,
        int deadlineTotalDayExclusive)
    {
        if (!TryParseCropSource(route, out var seedItemId) ||
            !crops.TryGetProperty(seedItemId, out var crop) ||
            crop.ValueKind != JsonValueKind.Object)
        {
            return BlockedCrop(
                "blocked_authoritative_crop_row_not_found",
                "exact_crop_route_source_identity_not_found");
        }

        var seasons = ReadCropSeasons(crop, seedItemId);
        var daysInPhase = ReadRequiredNonNegativeIntArray(
            crop,
            "DaysInPhase",
            seedItemId);
        Require(daysInPhase.Any(days => days > 0),
            "Crop has no positive growth phase: " + seedItemId);
        var baseGrowthDays = checked(daysInPhase.Sum());
        var regrowDays = ReadRequiredCropInt(crop, "RegrowDays", seedItemId);
        Require(regrowDays >= -1,
            "Crop RegrowDays is invalid: " + seedItemId);
        var needsWatering = ReadRequiredCropBool(crop, "NeedsWatering", seedItemId);
        var isPaddyCrop = ReadRequiredCropBool(crop, "IsPaddyCrop", seedItemId);
        var harvestItemId = ReadRequiredCropString(
            crop,
            "HarvestItemId",
            seedItemId);
        var dataHarvestQualifiedItemId = QualifyObjectId(harvestItemId);
        var texture = ReadRequiredCropString(crop, "Texture", seedItemId);
        var spriteIndex = ReadRequiredCropInt(crop, "SpriteIndex", seedItemId);
        var wildByNativeShape = spriteIndex == 23 &&
            string.Equals(texture, NativeCropTexture, StringComparison.Ordinal);
        var knownWildSeed = WildSeedOutputs.TryGetValue(
            seedItemId,
            out var wildOutputs);
        Require(wildByNativeShape == knownWildSeed,
            "Native wild-seed crop identity drifted: " + seedItemId);
        var possibleOutputs = knownWildSeed
            ? wildOutputs!
            : new[] { dataHarvestQualifiedItemId };
        Require(possibleOutputs.Contains(qualifiedItemId, StringComparer.Ordinal),
            "Crop route target is not a possible native harvest: " +
            seedItemId + ":" + qualifiedItemId);
        if (knownWildSeed)
        {
            Require(possibleOutputs.Contains(
                    dataHarvestQualifiedItemId,
                    StringComparer.Ordinal),
                "Wild-seed representative HarvestItemId drifted: " + seedItemId);
        }

        var plantableRules = ReadPlantableLocationRules(crop, seedItemId);
        var cropEvidence = new AcquisitionCropSourceEvidence(
            seedItemId,
            dataHarvestQualifiedItemId,
            possibleOutputs,
            seasons,
            daysInPhase,
            baseGrowthDays,
            regrowDays,
            needsWatering,
            isPaddyCrop,
            knownWildSeed,
            texture,
            spriteIndex,
            plantableRules,
            true,
            needsWatering,
            isPaddyCrop,
            plantableRules.Length > 0);
        var windows = ExpandCropWindows(
            seedItemId,
            seasons,
            knownWildSeed,
            deadlineTotalDayExclusive);
        return windows.Length > 0
            ? new CalendarSourceResolution(
                ResolvedStatus,
                knownWildSeed
                    ? "runtime_crop_wild_seed_calendar_and_outcome_domain"
                    : "runtime_crop_native_season_window",
                windows,
                Array.Empty<string>(),
                cropEvidence)
            : new CalendarSourceResolution(
                "blocked_crop_calendar_after_deadline",
                string.Empty,
                windows,
                new[] { "crop_calendar_has_no_window_before_deadline" },
                cropEvidence);
    }

    private static CalendarSourceResolution BlockedCrop(
        string status,
        string reason) => new(
            status,
            string.Empty,
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            new[] { reason });

    private static bool TryParseCropSource(
        AcquisitionRequirementRouteLowering route,
        out string seedItemId)
    {
        seedItemId = string.Empty;
        const string prefix = "crop:";
        if (route.RouteKind != "harvests_as" ||
            route.SourceAsset != "Data/Crops" ||
            !route.SourceId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        seedItemId = route.SourceId[prefix.Length..];
        return seedItemId.Length > 0 &&
            route.SourcePath == "payload." + seedItemId + ".HarvestItemId";
    }

    private static string[] ReadCropSeasons(JsonElement crop, string seedItemId)
    {
        Require(crop.TryGetProperty("Seasons", out var value) &&
                value.ValueKind == JsonValueKind.Array,
            "Crop Seasons is missing: " + seedItemId);
        var result = value.EnumerateArray().Select(season =>
        {
            var index = -1;
            Require(season.ValueKind == JsonValueKind.Number &&
                    season.TryGetInt32(out index) &&
                    index is >= 0 and <= 3,
                "Crop season is invalid: " + seedItemId);
            return NativeCalendarConstraintNormalizer.AllSeasons[index];
        }).ToArray();
        Require(result.Length > 0 &&
                result.Distinct(StringComparer.Ordinal).Count() == result.Length,
            "Crop Seasons is empty or duplicated: " + seedItemId);
        return result;
    }

    private static int[] ReadRequiredNonNegativeIntArray(
        JsonElement row,
        string property,
        string sourceId)
    {
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Array,
            "Crop " + property + " is missing: " + sourceId);
        var result = value.EnumerateArray().Select(item =>
        {
            var number = -1;
            Require(item.ValueKind == JsonValueKind.Number &&
                    item.TryGetInt32(out number) &&
                    number >= 0,
                "Crop " + property + " is invalid: " + sourceId);
            return number;
        }).ToArray();
        Require(result.Length > 0,
            "Crop " + property + " is empty: " + sourceId);
        return result;
    }

    private static int ReadRequiredCropInt(
        JsonElement row,
        string property,
        string sourceId)
    {
        var result = 0;
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out result),
            "Crop " + property + " is invalid: " + sourceId);
        return result;
    }

    private static bool ReadRequiredCropBool(
        JsonElement row,
        string property,
        string sourceId)
    {
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Crop " + property + " is invalid: " + sourceId);
        return value.GetBoolean();
    }

    private static string ReadRequiredCropString(
        JsonElement row,
        string property,
        string sourceId)
    {
        Require(row.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()),
            "Crop " + property + " is invalid: " + sourceId);
        return value.GetString()!;
    }

    private static AcquisitionCropPlantableLocationRule[] ReadPlantableLocationRules(
        JsonElement crop,
        string seedItemId)
    {
        if (!crop.TryGetProperty("PlantableLocationRules", out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return Array.Empty<AcquisitionCropPlantableLocationRule>();
        }
        Require(value.ValueKind == JsonValueKind.Array,
            "Crop PlantableLocationRules is invalid: " + seedItemId);
        return value.EnumerateArray().Select((rule, index) =>
        {
            Require(rule.ValueKind == JsonValueKind.Object,
                "Crop plantable-location rule is invalid: " + seedItemId);
            return new AcquisitionCropPlantableLocationRule(
                ReadRequiredCropString(rule, "Id", seedItemId + ":" + index),
                ReadOptionalCropString(rule, "Condition"),
                ReadRequiredCropInt(rule, "PlantedIn", seedItemId + ":" + index),
                ReadRequiredCropInt(rule, "Result", seedItemId + ":" + index),
                ReadOptionalCropString(rule, "DeniedMessage"));
        }).ToArray();
    }

    private static string? ReadOptionalCropString(JsonElement row, string property)
    {
        if (!row.TryGetProperty(property, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        Require(value.ValueKind == JsonValueKind.String,
            "Optional crop rule property is not a string: " + property);
        return value.GetString();
    }

    private static AuthoritativeCalendarSourceWindow[] ExpandCropWindows(
        string seedItemId,
        IReadOnlyCollection<string> nativeSeasons,
        bool stochasticOutcome,
        int deadlineTotalDayExclusive)
    {
        var result = new List<AuthoritativeCalendarSourceWindow>();
        var deadlineYear = ((deadlineTotalDayExclusive - 1) / 112) + 1;
        for (var year = 1; year <= deadlineYear; year++)
        {
            foreach (var season in NativeCalendarConstraintNormalizer.AllSeasons)
            {
                AddCropWindow(
                    result,
                    seedItemId,
                    year,
                    season,
                    nativeSeasons.Contains(season, StringComparer.Ordinal),
                    stochasticOutcome,
                    deadlineTotalDayExclusive);
            }
        }
        return result
            .OrderBy(window => window.LastTotalDay)
            .ThenBy(window => window.FirstTotalDay)
            .ThenBy(window => window.SourceKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddCropWindow(
        ICollection<AuthoritativeCalendarSourceWindow> result,
        string seedItemId,
        int year,
        string season,
        bool nativeSeason,
        bool stochasticOutcome,
        int deadlineTotalDayExclusive)
    {
        var first = TotalDay(year, season, 1);
        var last = Math.Min(
            TotalDay(year, season, 28),
            deadlineTotalDayExclusive - 1);
        if (first >= deadlineTotalDayExclusive || last < first)
            return;
        result.Add(new AuthoritativeCalendarSourceWindow
        {
            SourceKind = nativeSeason
                ? "crop_native_season"
                : "crop_season_ignored_location",
            SourceKey = seedItemId,
            Year = year,
            Season = season,
            FirstTotalDay = first,
            LastTotalDay = last,
            TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
            WeatherModes = NativeCalendarConstraintNormalizer.AllWeatherModes,
            RequiresLocationAccessEvidence = true,
            StochasticOutcome = stochasticOutcome,
            RequiredLocationCapability = nativeSeason
                ? null
                : "seeds_ignore_seasons"
        });
    }

    private static string QualifyObjectId(string itemId) =>
        itemId.StartsWith('(') ? itemId : "(O)" + itemId;
}
