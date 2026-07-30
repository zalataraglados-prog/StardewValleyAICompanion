using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewAI.Contracts.State;
using System.Reflection;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter : ReadAdapterBase
{
    private object CollisionContext(MineShaft mine, xTile.Map? loadedMap)
    {
        if (loadedMap is null || loadedMap.Layers.Count == 0)
        {
            return new { status = "unavailable", reason = "loaded_map_field_null" };
        }

        var width = loadedMap.Layers[0].LayerWidth;
        var height = loadedMap.Layers[0].LayerHeight;
        var signature = CollisionSignature(mine, width, height);
        if (cachedCollisionContext is not null && string.Equals(signature, cachedCollisionSignature, StringComparison.Ordinal))
        {
            return cachedCollisionContext;
        }

        var rows = new string[height];
        var staticRows = new string[height];
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        for (var y = 0; y < height; y++)
        {
            var row = new char[width];
            var staticRow = new char[width];
            for (var x = 0; x < width; x++)
            {
                var blocked = mine.IsTileBlockedBy(new Vector2(x, y), collisionMask, CollisionMask.None, useFarmerTile: true) ||
                    mine.farmers.Any(farmer => farmer != Game1.player && FarmerBlocksTile(farmer, x, y));
                row[x] = blocked ? '1' : '0';
                staticRow[x] = mine.isTilePassable(
                    new xTile.Dimensions.Location(x, y),
                    Game1.viewport)
                    ? '0'
                    : '1';
            }
            rows[y] = new string(row);
            staticRows[y] = new string(staticRow);
        }

        cachedCollisionSignature = signature;
        cachedCollisionContext = new
        {
            status = "available",
            width,
            height,
            encoding = "row_major_strings_1_blocked_0_passable",
            blocked_rows = rows,
            static_blocked_rows = staticRows,
            excludes_current_player = true,
            includes_map_objects_characters_terrain_and_other_farmers = true,
            static_rows_exclude_objects_characters_terrain_farmers_and_resource_clumps = true,
            source = "GameLocation.IsTileBlockedBy plus GameLocation.isTilePassable; decompiled methods are read-only"
        };
        return cachedCollisionContext;
    }

    private static string CollisionSignature(MineShaft mine, int width, int height)
    {
        var hash = new HashCode();
        hash.Add(mine.mineLevel);
        hash.Add(mine.loadedMapNumber);
        hash.Add(width);
        hash.Add(height);
        hash.Add(mine.ladderHasSpawned);
        hash.Add(Game1.ticks / 30);
        foreach (var pair in mine.objects.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.QualifiedItemId);
            hash.Add(pair.Value.MinutesUntilReady);
        }
        foreach (var character in mine.characters.OrderBy(character => character.Name, StringComparer.Ordinal).ThenBy(character => character.Position.Y).ThenBy(character => character.Position.X))
        {
            hash.Add(character.GetType().FullName);
            hash.Add(character.Position);
            hash.Add(character.GetBoundingBox());
        }
        foreach (var pair in mine.terrainFeatures.Pairs.OrderBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.X))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.GetType().FullName);
            hash.Add(pair.Value.getBoundingBox());
            hash.Add(pair.Value.isPassable());
            hash.Add(pair.Value.isTemporarilyInvisible);
        }
        foreach (var clump in mine.resourceClumps.OrderBy(clump => clump.Tile.Y).ThenBy(clump => clump.Tile.X))
        {
            hash.Add(clump.GetType().FullName);
            hash.Add(clump.Tile);
            hash.Add(clump.getBoundingBox());
            hash.Add(clump.health.Value);
        }
        foreach (var feature in mine.largeTerrainFeatures.OrderBy(feature => feature.Tile.Y).ThenBy(feature => feature.Tile.X))
        {
            hash.Add(feature.GetType().FullName);
            hash.Add(feature.Tile);
            hash.Add(feature.getBoundingBox());
            hash.Add(feature.isPassable());
            hash.Add(feature.isTemporarilyInvisible);
        }
        foreach (var furniture in mine.furniture.OrderBy(furniture => furniture.GetBoundingBox().Y).ThenBy(furniture => furniture.GetBoundingBox().X))
        {
            hash.Add(furniture.GetType().FullName);
            hash.Add(furniture.GetBoundingBox());
            hash.Add(furniture.isPassable());
        }
        foreach (var pair in mine.animals.Pairs.OrderBy(pair => pair.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value.GetType().FullName);
            hash.Add(pair.Value.GetBoundingBox());
            hash.Add(pair.Value.farmerPassesThrough);
        }
        foreach (var farmer in mine.farmers.Where(farmer => farmer != Game1.player).OrderBy(farmer => farmer.UniqueMultiplayerID))
        {
            hash.Add(farmer.UniqueMultiplayerID);
            hash.Add(farmer.GetBoundingBox());
        }
        return hash.ToHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool FarmerBlocksTile(Farmer farmer, int tileX, int tileY)
    {
        return farmer.GetBoundingBox().Intersects(new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize));
    }

    private static object ReadLadderPreview(MineShaft mine, Vector2 tile, Farmer player)
    {
        var stonesAfterBreak = Math.Max(0, mine.stonesLeftOnThisLevel - 1);
        var chance = LadderChanceAfterBreak(mine.stonesLeftOnThisLevel, player.LuckLevel, player.DailyLuck, mine.EnemyCount, player.hasBuff("dwarfStatue_1"));
        var random = Utility.CreateDaySaveRandom((int)tile.X * 1000, (int)tile.Y, mine.mineLevel);
        _ = random.NextDouble();
        var roll = random.NextDouble();
        var eligible = !mine.ladderHasSpawned && !mine.mustKillAllMonstersToAdvance() && mine.shouldCreateLadderOnThisLevel();
        return new
        {
            eligible,
            stones_after_break = stonesAfterBreak,
            chance,
            seeded_roll = roll,
            guaranteed_by_last_stone = stonesAfterBreak == 0,
            creates_ladder = eligible && (stonesAfterBreak == 0 || roll < chance),
            source = "MineShaft.checkStoneForItems exact seed and comparison"
        };
    }

    private static int? ReadBreakableContainerHealth(BreakableContainer container)
    {
        return BreakableContainerHealthField?.GetValue(container) is NetInt health ? health.Value : null;
    }

    private static bool? ReadPrivateNetBool(object target, FieldInfo? field)
    {
        return field?.GetValue(target) is NetBool value ? value.Value : null;
    }

    private sealed class ExplosiveAmmoAreaProjection
    {
        public ExplosiveAmmoAreaProjection(
            bool safe,
            string safetyStatus,
            int targetMotionMarginTiles,
            int usefulObjectHits,
            int monsterHits,
            int additionalMonsterHits,
            int protectedObjectHits,
            int protectedTerrainFeatureHits,
            int otherFarmerHits)
        {
            Safe = safe;
            SafetyStatus = safetyStatus;
            TargetMotionMarginTiles = targetMotionMarginTiles;
            UsefulObjectHits = usefulObjectHits;
            MonsterHits = monsterHits;
            AdditionalMonsterHits = additionalMonsterHits;
            ProtectedObjectHits = protectedObjectHits;
            ProtectedTerrainFeatureHits = protectedTerrainFeatureHits;
            OtherFarmerHits = otherFarmerHits;
        }

        public bool Safe { get; }
        public string SafetyStatus { get; }
        public int TargetMotionMarginTiles { get; }
        public int UsefulObjectHits { get; }
        public int MonsterHits { get; }
        public int AdditionalMonsterHits { get; }
        public int ProtectedObjectHits { get; }
        public int ProtectedTerrainFeatureHits { get; }
        public int OtherFarmerHits { get; }
        public bool HasAdditionalValue => UsefulObjectHits > 0 || AdditionalMonsterHits > 0;
    }}
