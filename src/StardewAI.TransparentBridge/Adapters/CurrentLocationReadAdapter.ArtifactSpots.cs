using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Enchantments;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static ObjectClearanceProjection ProjectArtifactSpotClearance(
        GameLocation location,
        Vector2 tile,
        Farmer player,
        Hoe hoe,
        int toolSlotIndex,
        string requiredToolKind)
    {
        var declaringType = location.GetType()
            .GetMethod(nameof(GameLocation.digUpArtifactSpot))?
            .DeclaringType;
        if (declaringType != typeof(GameLocation) &&
            declaringType != typeof(IslandLocation) &&
            declaringType != typeof(DesertFestival))
        {
            return ObjectClearanceProjection.Blocked(
                "artifact_spot",
                "blocked_custom_artifact_spot_location_override",
                requiredToolKind);
        }

        var artifactSpotsDugBefore = player.stats.Get("ArtifactSpotsDug");
        if (artifactSpotsDugBefore >= int.MaxValue)
        {
            return ObjectClearanceProjection.Blocked(
                "artifact_spot",
                "blocked_artifact_spot_stat_projection_overflow",
                requiredToolKind);
        }
        if (!ReferenceEquals(player.CurrentTool, hoe))
        {
            return ObjectClearanceProjection.Blocked(
                "artifact_spot",
                "blocked_artifact_spot_active_hoe_projection_pending",
                requiredToolKind);
        }

        try
        {
            var outputs = new ArtifactSpotOutputAccumulator();
            var artifactSpotsDugAfter = artifactSpotsDugBefore + 1;
            var defenseBookMailBefore = player.mailReceived.Contains("DefenseBookDropped");
            var defenseRandom = Utility.CreateDaySaveRandom(
                (0f - tile.X) * 7f,
                tile.Y * 777f,
                Game1.netWorldState.Value.TreasureTotemsUsed * 777);
            var defenseBookDropped = artifactSpotsDugAfter > 2 &&
                defenseRandom.NextDouble() < 0.008 + (!defenseBookMailBefore ? artifactSpotsDugAfter * 0.002 : 0.005);
            if (defenseBookDropped)
            {
                outputs.Add(ItemRegistry.Create("(O)Book_Defense"));
            }

            AddArtifactSpotLocationOverrideOutputs(outputs, location, tile);

            var random = Utility.CreateDaySaveRandom(
                tile.X * 2000f,
                tile.Y,
                Game1.netWorldState.Value.TreasureTotemsUsed * 777);
            if (player.mailReceived.Contains("sawQiPlane") &&
                random.NextDouble() < 0.05 + player.team.AverageDailyLuck() / 2.0)
            {
                outputs.Add(ItemRegistry.Create("(O)MysteryBox", random.Next(1, 3)));
            }
            AddArtifactSpotRareOutputs(outputs, player, random);

            if (!Game1.locationData.TryGetValue("Default", out var defaultData))
            {
                return ObjectClearanceProjection.Blocked(
                    "artifact_spot",
                    "blocked_default_location_artifact_spot_data_missing",
                    requiredToolKind);
            }

            var rules = defaultData.ArtifactSpots.AsEnumerable();
            var locationData = location.GetData();
            if (locationData?.ArtifactSpots is { Count: > 0 })
            {
                rules = rules.Concat(locationData.ArtifactSpots);
            }

            var resolverErrors = new List<string>();
            var context = new ItemQueryContext(
                location,
                player,
                random,
                "location '" + location.NameOrUniqueName + "' > artifact spots > transparent preview");
            foreach (var drop in rules.OrderBy(rule => rule.Precedence))
            {
                if (!random.NextBool(drop.Chance) ||
                    (drop.Condition is not null &&
                     !GameStateQuery.CheckConditions(drop.Condition, location, player, null, null, random)))
                {
                    continue;
                }

                IEnumerable<string?> candidateQueries = drop.RandomItemId is { Count: > 0 }
                    ? drop.RandomItemId
                    : new[] { drop.ItemId };
                var queryBlockReason = candidateQueries
                    .Select(query => ArtifactSpotQueryBlockReason(query, location, player))
                    .FirstOrDefault(reason => reason is not null);
                if (queryBlockReason is not null)
                {
                    return ObjectClearanceProjection.Blocked("artifact_spot", queryBlockReason, requiredToolKind);
                }

                var item = ItemQueryResolver.TryResolveRandomItem(
                    drop,
                    context,
                    avoidRepeat: false,
                    null,
                    null,
                    null,
                    (query, error) => resolverErrors.Add(query + ":" + error));
                if (item is null)
                {
                    continue;
                }
                if (item.Stack <= 0 || string.IsNullOrWhiteSpace(item.QualifiedItemId))
                {
                    return ObjectClearanceProjection.Blocked(
                        "artifact_spot",
                        "blocked_artifact_spot_invalid_resolved_item",
                        requiredToolKind);
                }

                outputs.Add(item);
                if (hoe.hasEnchantmentOfType<GenerousEnchantment>() &&
                    drop.ApplyGenerousEnchantment &&
                    random.NextBool())
                {
                    var duplicate = (Item)ItemQueryResolver.ApplyItemFields(item.getOne(), drop, context);
                    if (duplicate.Stack <= 0 || string.IsNullOrWhiteSpace(duplicate.QualifiedItemId))
                    {
                        return ObjectClearanceProjection.Blocked(
                            "artifact_spot",
                            "blocked_artifact_spot_invalid_generous_item",
                            requiredToolKind);
                    }
                    outputs.Add(duplicate);
                }
                if (!drop.ContinueOnDrop)
                {
                    break;
                }
            }

            if (resolverErrors.Count > 0)
            {
                return ObjectClearanceProjection.Blocked(
                    "artifact_spot",
                    "blocked_artifact_spot_item_query_resolution_error",
                    requiredToolKind);
            }

            var outputRows = outputs.ToArray();
            var terrainFeatureExpectedAfter = ProjectArtifactSpotTerrainAfter(location, tile);
            return new ObjectClearanceProjection
            {
                ClearKind = "artifact_spot",
                Status = "ready",
                RequiredToolKind = requiredToolKind,
                ToolSlotIndex = toolSlotIndex,
                ExpectedToolHits = 1,
                SkillId = "foraging",
                SkillIndex = Farmer.foragingSkill,
                Experience = 15,
                ExperienceCondition = "native_hoe_digs_artifact_spot",
                ExperienceStatus = "exact",
                OutputStatus = "exact",
                OutputQualifiedItemId = outputRows.Length == 1 ? outputRows[0].QualifiedItemId : string.Empty,
                OutputQuantity = outputRows.Length == 1 ? outputRows[0].Quantity : null,
                BonusOutputQualifiedItemId = "(O)Book_Defense",
                BonusOutputQuantity = defenseBookDropped ? 1 : 0,
                ArtifactSpotsDugBefore = (int)artifactSpotsDugBefore,
                ArtifactSpotsDugDelta = 1,
                ArtifactSpotsDugExpectedAfter = (int)artifactSpotsDugAfter,
                TerrainFeatureExpectedAfter = terrainFeatureExpectedAfter,
                DefenseBookMailBefore = defenseBookMailBefore,
                DefenseBookMailExpectedAfter = defenseBookMailBefore || defenseBookDropped,
                OutputItems = outputRows
            };
        }
        catch
        {
            return ObjectClearanceProjection.Blocked(
                "artifact_spot",
                "blocked_artifact_spot_projection_exception",
                requiredToolKind);
        }
    }

    private static void AddArtifactSpotLocationOverrideOutputs(
        ArtifactSpotOutputAccumulator outputs,
        GameLocation location,
        Vector2 tile)
    {
        var random = Utility.CreateDaySaveRandom(tile.X * 2000f, tile.Y);
        if (location is DesertFestival)
        {
            outputs.Add(ItemRegistry.Create("CalicoEgg", random.Next(3, 7)));
            return;
        }
        if (location is not IslandLocation)
        {
            return;
        }

        if (Game1.netWorldState.Value.GoldenCoconutCracked && random.NextDouble() < 0.1)
        {
            outputs.Add(ItemRegistry.Create("(O)791"));
        }
        else if (random.NextDouble() < 0.33)
        {
            outputs.Add(ItemRegistry.Create("(O)831", random.Next(2, 5)));
        }
        else if (random.NextDouble() < 0.15)
        {
            outputs.Add(ItemRegistry.Create("(O)275", random.Next(1, 3)));
        }
    }

    private static void AddArtifactSpotRareOutputs(
        ArtifactSpotOutputAccumulator outputs,
        Farmer player,
        Random random)
    {
        var luckMultiplier = 1.0 + player.team.AverageDailyLuck();
        if (player.stats.Get(StatKeys.Mastery(0)) != 0 &&
            random.NextDouble() < 0.009 * luckMultiplier)
        {
            outputs.Add(ItemRegistry.Create("(O)GoldenAnimalCracker"));
        }
        if (Game1.stats.DaysPlayed > 2 && random.NextDouble() < 0.018)
        {
            outputs.Add(Utility.getRandomCosmeticItem(random));
        }
        if (Game1.stats.DaysPlayed > 2 && random.NextDouble() < 0.0054)
        {
            outputs.Add(ItemRegistry.Create("(O)SkillBook_" + random.Next(5)));
        }
    }

    private static string ProjectArtifactSpotTerrainAfter(GameLocation location, Vector2 tile)
    {
        if (location.terrainFeatures.TryGetValue(tile, out var existingFeature))
        {
            return existingFeature.GetType().Name;
        }
        return location is MineShaft mine && mine.getMineArea() == 77377
            ? "none"
            : "HoeDirt";
    }

    private static string? ArtifactSpotQueryBlockReason(string? query, GameLocation location, Farmer player)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "blocked_artifact_spot_item_query_missing";
        }
        if (ItemRegistry.GetData(query) is not null)
        {
            return null;
        }

        var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var key = parts[0];
        var arguments = parts.Length > 1 ? parts[1] : string.Empty;
        switch (key)
        {
            case "ALL_ITEMS":
            case "RANDOM_ITEMS":
            case "RANDOM_BASE_SEASON_ITEM":
            case "RANDOM_ARTIFACT_FOR_DIG_SPOT":
                return null;
            case "LOST_BOOK_OR_ITEM":
                return Game1.netWorldState.Value.LostBooksFound < 21 || string.IsNullOrWhiteSpace(arguments)
                    ? null
                    : ArtifactSpotQueryBlockReason(arguments, location, player);
            case "SECRET_NOTE_OR_ITEM":
                if (ArtifactSpotSecretNoteUsesGlobalRandom(location, player))
                {
                    return "blocked_artifact_spot_secret_note_global_rng_not_exposed";
                }
                return string.IsNullOrWhiteSpace(arguments)
                    ? null
                    : ArtifactSpotQueryBlockReason(arguments, location, player);
            default:
                return "blocked_artifact_spot_unreviewed_item_query=" + key;
        }
    }

    private static bool ArtifactSpotSecretNoteUsesGlobalRandom(GameLocation location, Farmer player)
    {
        if (location.currentEvent?.isFestival == true)
        {
            return false;
        }
        var islandNotes = location.InIslandContext();
        if (!islandNotes && !player.hasMagnifyingGlass)
        {
            return false;
        }
        var itemId = islandNotes ? "(O)842" : "(O)79";
        var unseen = Utility.GetUnseenSecretNotes(player, islandNotes, out _).Length;
        return unseen - player.Items.CountId(itemId) > 0;
    }

}

internal sealed class ArtifactSpotOutputAccumulator
{
    private readonly Dictionary<(string RuntimeType, string QualifiedItemId, int Quality, string UnitStateSha256), int> quantities = new();

    public void Add(Item item)
    {
        var projection = ClearanceOutputItemProjection.From(item);
        var key = (projection.RuntimeType, projection.QualifiedItemId, projection.Quality, projection.UnitStateSha256);
        quantities[key] = quantities.TryGetValue(key, out var quantity)
            ? quantity + item.Stack
            : item.Stack;
    }

    public ClearanceOutputItemProjection[] ToArray()
    {
        return quantities
            .OrderBy(pair => pair.Key.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.RuntimeType, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Quality)
            .ThenBy(pair => pair.Key.UnitStateSha256, StringComparer.Ordinal)
            .Select(pair => new ClearanceOutputItemProjection(
                pair.Key.RuntimeType,
                pair.Key.QualifiedItemId,
                pair.Key.Quality,
                pair.Key.UnitStateSha256,
                pair.Value))
            .ToArray();
    }
}
