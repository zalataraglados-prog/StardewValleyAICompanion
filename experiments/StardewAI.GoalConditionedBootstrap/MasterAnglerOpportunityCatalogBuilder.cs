using System.Globalization;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerOpportunityCatalogBuilder
{
    private static readonly IReadOnlyDictionary<string, MasterAnglerMineOverrideRule[]> MineOverrideRules =
        new Dictionary<string, MasterAnglerMineOverrideRule[]>(StringComparer.Ordinal)
        {
            ["158"] = new[]
            {
                MineRule(0, 0.02, 0.01, "Stonefish"),
                MineRule(10, 0.02, 0.01, "Stonefish")
            },
            ["161"] = new[] { MineRule(40, 0.015, 0.009, "Ice Pip") },
            ["162"] = new[] { MineRule(80, 0.01, 0.008, "Lava Eel") }
        };

    public static MasterAnglerOpportunityCatalogReport Build(string requirementInventoryPath)
    {
        var inventoryPath = Path.GetFullPath(requirementInventoryPath);
        var inventory = JsonSerializer.Deserialize<AuthoritativeRequirementInventoryReport>(
            File.ReadAllText(inventoryPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Requirement inventory is null.");
        Require(inventory.Status == "complete" && inventory.DenominatorComplete &&
                inventory.AcquisitionRoutesComplete,
            "Requirement inventory is not complete.");

        var requirementSet = inventory.RequirementSets.SingleOrDefault(set =>
            set.RequirementSetId == "master_angler")
            ?? throw new InvalidDataException("Master Angler requirement set is missing.");
        Require(requirementSet.RequiredGroupCount == 72 && requirementSet.Groups.Length == 72,
            "Master Angler denominator is not the exact 72-species native set.");

        var evidence = inventory.SourceEvidence.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
        var fishSource = RequiredEvidence(evidence, "runtime_data_fish");
        var locationsSource = RequiredEvidence(evidence, "runtime_data_locations");
        var mineSource = RequiredEvidence(evidence, "native_mine_fishing_override_rule");
        var crabPotSource = RequiredEvidence(evidence, "native_crab_pot_output_rule");
        var gameStateQuerySource = RequiredEvidence(evidence, "native_game_state_query_rule");
        var farmOverrideSource = RequiredEvidence(evidence, "native_farm_fishing_override_rule");
        var islandOverrideSource = RequiredEvidence(evidence, "native_island_fishing_override_rule");
        var islandSouthEastOverrideSource = RequiredEvidence(evidence, "native_island_southeast_fishing_override_rule");
        var railroadOverrideSource = RequiredEvidence(evidence, "native_railroad_fishing_override_rule");
        var nativeSources = new[]
        {
            fishSource,
            locationsSource,
            mineSource,
            crabPotSource,
            gameStateQuerySource,
            farmOverrideSource,
            islandOverrideSource,
            islandSouthEastOverrideSource,
            railroadOverrideSource
        };
        foreach (var source in nativeSources)
        {
            Require(File.Exists(source.Path), "Master Angler source is missing: " + source.SourceId);
            Require(ContentInventoryVerifier.HashFile(source.Path) == source.Sha256,
                "Master Angler source hash drifted: " + source.SourceId);
        }

        GuardNativeSources(
            mineSource.Path,
            crabPotSource.Path,
            gameStateQuerySource.Path,
            farmOverrideSource.Path,
            islandOverrideSource.Path,
            islandSouthEastOverrideSource.Path,
            railroadOverrideSource.Path);
        using var fishDocument = JsonDocument.Parse(File.ReadAllText(fishSource.Path));
        using var locationsDocument = JsonDocument.Parse(File.ReadAllText(locationsSource.Path));
        var fishData = fishDocument.RootElement.GetProperty("payload");
        var locations = locationsDocument.RootElement.GetProperty("payload");

        var rows = requirementSet.Groups
            .OrderBy(group => group.Alternatives.Single().ItemId, StringComparer.Ordinal)
            .Select(group => BuildSpecies(group.Alternatives.Single(), fishData, locations))
            .ToArray();
        var unresolved = rows
            .Where(row => row.RouteStatus != "source_complete")
            .Select(row => row.QualifiedItemId)
            .ToArray();
        var unresolvedCalendarRules = rows
            .SelectMany(row => row.LocationRules)
            .Where(rule => rule.Calendar.ParseStatus != "complete" ||
                           !rule.Calendar.StaticCalendarPossible)
            .Select(rule => rule.LocationId + ":" + rule.RuleIndex)
            .ToArray();
        var rodLocationCount = rows.Count(row => row.AcquisitionClass == "rod_location_rule");
        var mineOverrideCount = rows.Count(row => row.AcquisitionClass == "rod_mine_override");
        var mineOverrideSourceSpeciesCount = rows.Count(row => row.MineOverrides.Length > 0);
        var mineOverrideAreaCount = rows.Sum(row => row.MineOverrides.Length);
        var trapCount = rows.Count(row => row.AcquisitionClass == "trap_crab_pot");
        Require(rodLocationCount == 60, "Expected exactly 60 location-rule Master Angler species.");
        Require(mineOverrideCount == 2, "Expected exactly two MineShaft override species.");
        Require(mineOverrideSourceSpeciesCount == 3,
            "Expected exactly three species with a MineShaft override source.");
        Require(mineOverrideAreaCount == 4,
            "Expected exactly four MineShaft area-to-species override routes.");
        Require(trapCount == 10, "Expected exactly ten trap species.");
        Require(unresolved.Length == 0,
            "Master Angler opportunity sources are unresolved: " + string.Join(",", unresolved));
        Require(unresolvedCalendarRules.Length == 0,
            "Master Angler static calendar constraints are unresolved: " +
            string.Join(",", unresolvedCalendarRules));

        return new MasterAnglerOpportunityCatalogReport
        {
            Status = "complete",
            GoalId = inventory.GoalId,
            GameVersion = inventory.GameVersion,
            NativeDenominatorCount = rows.Length,
            RodLocationSpeciesCount = rodLocationCount,
            MineOverrideOnlySpeciesCount = mineOverrideCount,
            MineOverrideSourceSpeciesCount = mineOverrideSourceSpeciesCount,
            MineOverrideAreaCount = mineOverrideAreaCount,
            TrapSpeciesCount = trapCount,
            SourceInventoryComplete = true,
            StaticCalendarConstraintComplete = true,
            NativeGetFishOverrideFileCount = 5,
            RequirementInventoryPath = inventoryPath,
            RequirementInventorySha256 = ContentInventoryVerifier.HashFile(inventoryPath),
            SourceEvidence = nativeSources,
            Species = rows,
            UnresolvedSpeciesIds = unresolved,
            UnresolvedCalendarRuleIds = unresolvedCalendarRules
        };
    }

    private static MasterAnglerSpeciesOpportunity BuildSpecies(
        GoalRequirementAlternative alternative,
        JsonElement fishData,
        JsonElement locations)
    {
        var raw = fishData.TryGetProperty(alternative.ItemId, out var fishValue) &&
                  fishValue.ValueKind == JsonValueKind.String
            ? fishValue.GetString() ?? string.Empty
            : string.Empty;
        var fields = raw.Split('/');
        var trap = fields.Length > 1 && fields[1] == "trap";
        var fishConstraint = ParseFishData(raw);
        var locationRules = FindLocationRules(alternative.ItemId, locations, fishConstraint);
        var mineOverrides = MineOverrideRules.TryGetValue(alternative.ItemId, out var overrideRules)
            ? overrideRules
            : Array.Empty<MasterAnglerMineOverrideRule>();

        if (trap)
        {
            return new MasterAnglerSpeciesOpportunity
            {
                ItemId = alternative.ItemId,
                QualifiedItemId = alternative.QualifiedItemId,
                DisplayName = alternative.DisplayName,
                AcquisitionClass = "trap_crab_pot",
                RouteStatus = fields.Length >= 6 && fields[4] is "freshwater" or "ocean"
                    ? "source_complete"
                    : "invalid_trap_data",
                FishData = fishConstraint,
                TrapWaterType = Field(fields, 4),
                TrapDailyOutputChance = ParseDouble(Field(fields, 2)),
                LocationRules = locationRules,
                MineOverrides = mineOverrides
            };
        }

        if (mineOverrides.Length > 0 && locationRules.Length == 0)
        {
            return new MasterAnglerSpeciesOpportunity
            {
                ItemId = alternative.ItemId,
                QualifiedItemId = alternative.QualifiedItemId,
                DisplayName = alternative.DisplayName,
                AcquisitionClass = "rod_mine_override",
                RouteStatus = raw.Length > 0 ? "source_complete" : "invalid_fish_data",
                FishData = fishConstraint,
                MineOverrides = mineOverrides,
                LocationRules = locationRules
            };
        }

        var rawDataOptional = raw.Length == 0 && locationRules.Length > 0 &&
            locationRules.All(rule => rule.IgnoreFishDataRequirements);
        var fishDataValid = rawDataOptional || fishConstraint.ParseStatus == "parsed";
        return new MasterAnglerSpeciesOpportunity
        {
            ItemId = alternative.ItemId,
            QualifiedItemId = alternative.QualifiedItemId,
            DisplayName = alternative.DisplayName,
            AcquisitionClass = "rod_location_rule",
            RouteStatus = locationRules.Length > 0 && fishDataValid
                ? "source_complete"
                : locationRules.Length == 0
                    ? "no_location_rule"
                    : "invalid_fish_data",
            FishData = fishConstraint,
            LocationRules = locationRules,
            MineOverrides = mineOverrides
        };
    }

    private static MasterAnglerFishDataConstraint ParseFishData(string raw)
    {
        if (raw.Length == 0)
        {
            return new MasterAnglerFishDataConstraint
            {
                ParseStatus = "not_required_by_location_rule"
            };
        }

        var fields = raw.Split('/');
        if (fields.Length > 1 && fields[1] == "trap")
        {
            return new MasterAnglerFishDataConstraint
            {
                ParseStatus = "trap",
                Raw = raw
            };
        }

        var windows = ParseTimeWindows(Field(fields, 5));
        var level = ParseInt(Field(fields, 12));
        return new MasterAnglerFishDataConstraint
        {
            ParseStatus = fields.Length >= 14 && windows.Length > 0 && level.HasValue
                ? "parsed"
                : "invalid",
            Raw = raw,
            TimeWindows = windows,
            Seasons = Field(fields, 6)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Weather = Field(fields, 7),
            MinimumFishingLevel = level
        };
    }

    private static MasterAnglerLocationRule[] FindLocationRules(
        string itemId,
        JsonElement locations,
        MasterAnglerFishDataConstraint fishConstraint)
    {
        var result = new List<MasterAnglerLocationRule>();
        foreach (var location in locations.EnumerateObject())
        {
            if (!location.Value.TryGetProperty("Fish", out var rows) || rows.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var row in rows.EnumerateArray())
            {
                if (LocationFishItemIds(row).Contains(itemId, StringComparer.Ordinal))
                {
                    var spawnSeason = ReadSeason(row, "Season");
                    var condition = String(row, "Condition");
                    var ignoreFishData = Bool(row, "IgnoreFishDataRequirements");
                    result.Add(new MasterAnglerLocationRule
                    {
                        LocationId = location.Name,
                        RuleIndex = index,
                        RuleId = String(row, "Id"),
                        SpawnSeason = spawnSeason,
                        Condition = condition,
                        PerItemCondition = String(row, "PerItemCondition"),
                        FishAreaId = String(row, "FishAreaId"),
                        MinimumFishingLevel = Int(row, "MinFishingLevel"),
                        MinimumDistanceFromShore = Int(row, "MinDistanceFromShore"),
                        MaximumDistanceFromShore = Int(row, "MaxDistanceFromShore"),
                        RequireMagicBait = Bool(row, "RequireMagicBait"),
                        IgnoreFishDataRequirements = ignoreFishData,
                        CanUseTrainingRod = NullableBool(row, "CanUseTrainingRod"),
                        CatchLimit = Int(row, "CatchLimit"),
                        Precedence = Int(row, "Precedence"),
                        Calendar = NormalizeCalendar(
                            fishConstraint,
                            spawnSeason,
                            condition,
                            ignoreFishData)
                    });
                }
                index++;
            }
        }
        return result
            .OrderBy(row => row.LocationId, StringComparer.Ordinal)
            .ThenBy(row => row.RuleIndex)
            .ToArray();
    }

    private static IEnumerable<string> LocationFishItemIds(JsonElement row)
    {
        if (row.TryGetProperty("ItemId", out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            foreach (var itemId in SplitObjectIds(direct.GetString()))
                yield return itemId;
        }
        if (!row.TryGetProperty("RandomItemId", out var random) || random.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var value in random.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
                continue;
            foreach (var itemId in SplitObjectIds(value.GetString()))
                yield return itemId;
        }
    }

    private static IEnumerable<string> SplitObjectIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;
        foreach (var token in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var itemId = token.StartsWith("(O)", StringComparison.Ordinal) ? token[3..] : token;
            if (itemId.All(character => char.IsLetterOrDigit(character) || character == '_'))
                yield return itemId;
        }
    }

    private static MasterAnglerTimeWindow[] ParseTimeWindows(string raw)
    {
        var values = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0 || values.Length % 2 != 0)
            return Array.Empty<MasterAnglerTimeWindow>();
        var result = new List<MasterAnglerTimeWindow>();
        for (var index = 0; index < values.Length; index += 2)
        {
            if (!int.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
                !int.TryParse(values[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
            {
                return Array.Empty<MasterAnglerTimeWindow>();
            }
            result.Add(new MasterAnglerTimeWindow { StartTime = start, EndTime = end });
        }
        return result.ToArray();
    }

    private static string ReadSeason(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return string.Empty;
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;
        if (!value.TryGetInt32(out var index))
            throw new InvalidDataException("Invalid Data/Locations season value.");
        return index switch
        {
            0 => "spring",
            1 => "summer",
            2 => "fall",
            3 => "winter",
            _ => throw new InvalidDataException("Unsupported Data/Locations season index: " + index)
        };
    }

    private static RequirementSourceEvidence RequiredEvidence(
        IReadOnlyDictionary<string, RequirementSourceEvidence> evidence,
        string sourceId) => evidence.TryGetValue(sourceId, out var source)
            ? source
            : throw new InvalidDataException("Requirement evidence is missing: " + sourceId);

    private static void GuardNativeSources(
        string minePath,
        string crabPotPath,
        string gameStateQueryPath,
        string farmPath,
        string islandPath,
        string islandSouthEastPath,
        string railroadPath)
    {
        var mine = File.ReadAllText(minePath);
        RequireContains(mine, "public override Item getFish(", minePath);
        RequireContains(mine, "text = \"(O)158\"", minePath);
        RequireContains(mine, "text = \"(O)161\"", minePath);
        RequireContains(mine, "text = \"(O)162\"", minePath);
        RequireContains(mine, "0.02 + 0.01 * num", minePath);
        RequireContains(mine, "0.015 + 0.009 * num", minePath);
        RequireContains(mine, "0.01 + 0.008 * num", minePath);
        var crabPot = File.ReadAllText(crabPotPath);
        RequireContains(crabPot, "public override void DayUpdate()", crabPotPath);
        RequireContains(crabPot, "if (!item.Value.Contains(\"trap\"))", crabPotPath);
        RequireContains(crabPot, "location.GetCrabPotFishForTile", crabPotPath);
        var query = File.ReadAllText(gameStateQueryPath);
        RequireContains(query, "public static bool YEAR(string[] query, GameStateQueryContext context)", gameStateQueryPath);
        RequireContains(query, "int maxYear", gameStateQueryPath);
        RequireContains(query, "year >= value", gameStateQueryPath);
        RequireContains(query, "year <= value2", gameStateQueryPath);
        RequireContains(query, "public static bool TIME(string[] query, GameStateQueryContext context)", gameStateQueryPath);
        RequireContains(query, "int maxTime", gameStateQueryPath);
        RequireContains(File.ReadAllText(farmPath), "FarmFishLocationOverride", farmPath);
        RequireContains(File.ReadAllText(islandPath), "limitedNutDrops[\"IslandFishing\"]", islandPath);
        RequireContains(File.ReadAllText(islandSouthEastPath), "MarkCollectedNut(\"StardropPool\")", islandSouthEastPath);
        RequireContains(File.ReadAllText(railroadPath), "GameLocation.CAROLINES_NECKLACE_ITEM_QID", railroadPath);
    }

    private static string Field(string[] fields, int index) =>
        index >= 0 && index < fields.Length ? fields[index] : string.Empty;

    private static int? ParseInt(string raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static double? ParseDouble(string raw) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static int Int(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static string String(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool Bool(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool? NullableBool(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException("Invalid boolean field: " + property)
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private static void RequireContains(string source, string value, string path)
    {
        if (!source.Contains(value, StringComparison.Ordinal))
            throw new InvalidDataException($"Native source guard failed for {path}: {value}");
    }

    private static MasterAnglerMineOverrideRule MineRule(
        int mineArea,
        double baseChance,
        double scoreMultiplier,
        string targetedBaitName) => new()
    {
        MineArea = mineArea,
        BaseChance = baseChance,
        ScoreMultiplier = scoreMultiplier,
        TargetedBaitName = targetedBaitName,
        BaseScore = 1,
        FishingLevelScoreMultiplier = 0.4,
        WaterDepthScoreMultiplier = 0.1,
        CuriosityLureScoreBonus = 5,
        TargetedBaitScoreBonus = 10,
        TrainingRodAllowed = false
    };
}
