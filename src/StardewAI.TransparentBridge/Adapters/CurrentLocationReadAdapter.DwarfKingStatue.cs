using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Constants;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string DwarfKingStatueQualifiedItemId = "(BC)StatueOfTheDwarfKing";
    internal const string DwarfKingStatueNativeContract = "Object.checkForAction_StatueOfTheDwarfKing->ChooseFromIconsMenu(dwarfStatue)->receiveLeftClick_exact_offered_icon->Farmer.applyBuff(dwarfStatue_N)";

    private static object ReadDwarfKingStatuePower(GameLocation location)
    {
        var player = Game1.player;
        var masteryValue = player.stats.Get(StatKeys.Mastery(3));
        var activeBuff = player.buffs.AppliedBuffs.Values
            .Where(buff => buff.id.StartsWith("dwarfStatue_", StringComparison.Ordinal))
            .OrderBy(buff => buff.id, StringComparer.Ordinal)
            .Select(buff => new
            {
                buff_id = buff.id,
                display_name = buff.displayName,
                description = buff.description,
                milliseconds_duration = buff.millisecondsDuration
            })
            .FirstOrDefault();

        var random = Utility.CreateRandom(Game1.stats.DaysPlayed * 77, Game1.uniqueIDForThisGame);
        var firstPowerId = random.Next(5);
        int secondPowerId;
        do
        {
            secondPowerId = random.Next(5);
        }
        while (secondPowerId == firstPowerId);

        var offeredPowerIds = new[] { firstPowerId, secondPowerId };
        var offers = offeredPowerIds
            .Select((powerId, menuIndex) => DwarfKingPowerProjection(powerId, menuIndex))
            .ToArray();
        var statues = location.objects.Pairs
            .Where(pair =>
                pair.Value.GetType() == typeof(StardewObject) &&
                string.Equals(pair.Value.QualifiedItemId, DwarfKingStatueQualifiedItemId, StringComparison.Ordinal) &&
                pair.Value.bigCraftable.Value)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair =>
            {
                var target = pair.Key.ToPoint();
                var stands = ReadDwarfKingAdjacentStands(location, target);
                return new
                {
                    tile_x = target.X,
                    tile_y = target.Y,
                    qualified_item_id = pair.Value.QualifiedItemId,
                    target_runtime_type = pair.Value.GetType().FullName,
                    stack = pair.Value.Stack,
                    big_craftable = pair.Value.bigCraftable.Value,
                    stand_tiles = stands,
                    has_available_adjacent_stand = stands.Any(stand => stand.available)
                };
            })
            .ToArray();

        var status = masteryValue < 1
            ? "blocked_mining_mastery_required"
            : activeBuff is not null
                ? "already_chosen_today"
                : statues.Length == 0
                    ? "no_statue_in_current_location"
                    : !statues.Any(statue => statue.has_available_adjacent_stand)
                        ? "blocked_no_adjacent_stand"
                        : "ready";

        return new
        {
            status,
            location_id = location.NameOrUniqueName,
            mining_mastery_stat_key = StatKeys.Mastery(3),
            mining_mastery_value = masteryValue,
            mining_mastery_unlocked = masteryValue >= 1,
            days_played = Game1.stats.DaysPlayed,
            offer_random_contract = "Utility.CreateRandom(Game1.stats.DaysPlayed*77,Game1.uniqueIDForThisGame);Next(5);repeat_Next(5)_until_distinct",
            offered_power_ids = offeredPowerIds,
            offered_power_ids_csv = string.Join(",", offeredPowerIds),
            offers,
            active_dwarf_statue_buff = activeBuff,
            has_active_dwarf_statue_buff = activeBuff is not null,
            statues,
            qualified_item_id = DwarfKingStatueQualifiedItemId,
            expected_menu_type = "ChooseFromIconsMenu",
            expected_menu_kind = "dwarfStatue",
            buff_duration_contract = "Data/Buffs.Duration=-2_until_day_end",
            selection_lock_contract = "Farmer.hasBuffWithNameContainingString(dwarfStatue)",
            native_contract = DwarfKingStatueNativeContract
        };
    }

    private static object DwarfKingPowerProjection(int powerId, int menuIndex)
    {
        var effect = powerId switch
        {
            0 => new { kind = "extra_ore_per_ore_node", exact_effect = "+1 to breakStone ore quantity accumulator", source = "GameLocation.breakStone" },
            1 => new { kind = "ladder_and_shaft_chance", exact_effect = "+0.07 monster-kill ladder branch; x1.25 stone-break ladder chance", source = "MineShaft.monsterDrop+checkStoneForItems" },
            2 => new { kind = "coal_find_chance", exact_effect = "+0.03 outdoor stone coal branch; +0.025 generic coal branch; +0.1 mine coal sub-roll", source = "GameLocation.checkForBuriedItem+breakStone;MineShaft.checkStoneForItems" },
            3 => new { kind = "bomb_damage_immunity", exact_effect = "bomb-origin player damage branch skipped", source = "GameLocation.performDamagePlayers" },
            4 => new { kind = "geode_find_chance", exact_effect = "x1.25 geode probability on supported stone branches", source = "GameLocation.checkForBuriedItem;MineShaft.checkStoneForItems" },
            _ => throw new ArgumentOutOfRangeException(nameof(powerId))
        };
        return new
        {
            menu_index = menuIndex,
            power_id = powerId,
            buff_id = "dwarfStatue_" + powerId,
            display_text = Game1.content.LoadString("Strings\\1_6_Strings:DwarfStatue_" + powerId),
            icon_front_source_x = 148 + powerId * 17,
            icon_front_source_y = 123,
            icon_front_width = 17,
            icon_front_height = 17,
            effect
        };
    }

    private static DwarfKingStandProjection[] ReadDwarfKingAdjacentStands(GameLocation location, Point target)
    {
        return new[]
        {
            new Point(target.X, target.Y - 1),
            new Point(target.X, target.Y + 1),
            new Point(target.X - 1, target.Y),
            new Point(target.X + 1, target.Y)
        }.Select(stand =>
        {
            var onMap = location.isTileOnMap(stand.ToVector2());
            var collisionBlocked = !onMap || location.isCollidingPosition(
                new Rectangle(
                    stand.X * Game1.tileSize + 1,
                    stand.Y * Game1.tileSize + 1,
                    Game1.tileSize - 2,
                    Game1.tileSize - 2),
                Game1.viewport,
                Game1.player);
            return new DwarfKingStandProjection(stand.X, stand.Y, onMap, collisionBlocked);
        }).ToArray();
    }

    private sealed record DwarfKingStandProjection(
        int tile_x,
        int tile_y,
        bool on_map,
        bool collision_blocked)
    {
        public bool available => on_map && !collision_blocked;
    }
}
