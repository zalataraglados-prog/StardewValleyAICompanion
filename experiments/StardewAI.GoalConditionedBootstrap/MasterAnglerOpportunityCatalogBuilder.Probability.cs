using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerOpportunityCatalogBuilder
{
    private static string[] StringArray(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return Array.Empty<string>();
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid string array field: " + property);
        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : throw new InvalidDataException("Invalid string array item: " + property))
            .ToArray();
    }

    private static string ItemSelectionMode(string directItemId, string[] randomItemIds)
    {
        if (directItemId.Length > 0 && randomItemIds.Length == 0)
            return "direct_item_id";
        if (directItemId.Length == 0 && randomItemIds.Length > 0)
            return "random_item_ids";
        throw new InvalidDataException(
            "Location fish rule must define exactly one item selection source.");
    }

    private static int ChanceModifierMode(JsonElement row)
    {
        var mode = RequiredInt(row, "ChanceModifierMode");
        Require(mode is >= 0 and <= 2, "Unsupported chance modifier mode: " + mode);
        return mode;
    }

    internal static MasterAnglerChanceModifier[] ParseChanceModifiers(JsonElement row)
    {
        if (!row.TryGetProperty("ChanceModifiers", out var value) || value.ValueKind == JsonValueKind.Null)
            return Array.Empty<MasterAnglerChanceModifier>();
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid ChanceModifiers field.");
        return value.EnumerateArray().Select(modifier =>
        {
            var modification = RequiredInt(modifier, "Modification");
            Require(modification is >= 0 and <= 4,
                "Unsupported chance modification type: " + modification);
            return new MasterAnglerChanceModifier
            {
                Id = String(modifier, "Id"),
                Condition = String(modifier, "Condition"),
                Modification = modification,
                Amount = RequiredDouble(modifier, "Amount"),
                RandomAmount = DoubleArray(modifier, "RandomAmount")
            };
        }).ToArray();
    }

    private static double[] DoubleArray(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
            return Array.Empty<double>();
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid number array field: " + property);
        return value.EnumerateArray()
            .Select(item => item.TryGetDouble(out var parsed) && double.IsFinite(parsed)
                ? parsed
                : throw new InvalidDataException("Invalid number array item: " + property))
            .ToArray();
    }

    private static int RequiredInt(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || !value.TryGetInt32(out var parsed))
            throw new InvalidDataException("Missing or invalid integer field: " + property);
        return parsed;
    }

    private static double RequiredDouble(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) ||
            !value.TryGetDouble(out var parsed) ||
            !double.IsFinite(parsed))
        {
            throw new InvalidDataException("Missing or invalid number field: " + property);
        }
        return parsed;
    }

    private static bool RequiredBool(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value))
            throw new InvalidDataException("Missing boolean field: " + property);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException("Invalid boolean field: " + property)
        };
    }

    private static void GuardFishingProbabilitySources(
        string gameLocationPath,
        string spawnFishDataPath)
    {
        var gameLocation = File.ReadAllText(gameLocationPath);
        RequireContains(gameLocation, "orderby p.Precedence, Game1.random.Next()", gameLocationPath);
        RequireContains(gameLocation, "for (int num2 = 0; num2 < 2; num2++)", gameLocationPath);
        RequireContains(gameLocation, "float chance = spawn.GetChance(", gameLocationPath);
        RequireContains(gameLocation, "spawn.ItemId == text", gameLocationPath);
        RequireContains(gameLocation, "if (spawn.UseFishCaughtSeededRandom)", gameLocationPath);
        RequireContains(gameLocation,
            "player.stats.Get(\"PreciseFishCaught\") * 859).NextBool(chance)",
            gameLocationPath);
        RequireContains(gameLocation, "ItemQueryResolver.TryResolveRandomItem(spawn", gameLocationPath);
        RequireContains(gameLocation, "CheckGenericFishRequirements(item3", gameLocationPath);

        var spawnFishData = File.ReadAllText(spawnFishDataPath);
        RequireContains(spawnFishData, "public float GetChance(", spawnFishDataPath);
        RequireContains(spawnFishData, "float num = Chance;", spawnFishDataPath);
        RequireContains(spawnFishData, "num += CuriosityLureBuff;", spawnFishDataPath);
        RequireContains(spawnFishData, "num += (float)dailyLuck;", spawnFishDataPath);
        RequireContains(spawnFishData,
            "num = applyModifiers(num, ChanceModifiers, ChanceModifierMode);",
            spawnFishDataPath);
        RequireContains(spawnFishData,
            "num = num * SpecificBaitMultiplier + SpecificBaitBuff;",
            spawnFishDataPath);
        RequireContains(spawnFishData,
            "return num + ChanceBoostPerLuckLevel * (float)luckLevel;",
            spawnFishDataPath);
    }
}
