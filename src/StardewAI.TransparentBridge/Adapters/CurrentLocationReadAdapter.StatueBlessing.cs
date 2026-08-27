using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Constants;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string StatueOfBlessingsQualifiedItemId = "(BC)StatueOfBlessings";
    internal const string StatueOfBlessingsNativeContract = "Object.checkForAction_StatueOfBlessings->CheckForActionOnBlessedStatue->Farmer.applyBuff(statue_of_blessings_N)";

    private static object ReadStatueBlessing(GameLocation location)
    {
        var player = Game1.player;
        var masteryValue = player.stats.Get(StatKeys.Mastery(0));
        var activeBuffs = player.buffs.AppliedBuffs.Values
            .Where(buff => buff.id.StartsWith("statue_of_blessings_", StringComparison.Ordinal))
            .OrderBy(buff => buff.id, StringComparer.Ordinal)
            .Select(buff => new
            {
                buff_id = buff.id,
                display_name = buff.displayName,
                description = buff.description,
                milliseconds_duration = buff.millisecondsDuration
            })
            .ToArray();

        var random = Utility.CreateDaySaveRandom(Game1.stats.DaysPlayed * 777);
        for (var i = 0; i < 8; i++)
        {
            random.Next();
        }
        var festivalDay = Utility.isFestivalDay();
        var randomUpperBoundExclusive = Game1.isRaining || festivalDay ? 6 : 7;
        var blessingId = random.Next(randomUpperBoundExclusive);
        var blessing = StatueBlessingProjection(blessingId);

        var statues = location.objects.Pairs
            .Where(pair =>
                pair.Value.GetType() == typeof(StardewObject) &&
                string.Equals(pair.Value.QualifiedItemId, StatueOfBlessingsQualifiedItemId, StringComparison.Ordinal) &&
                pair.Value.bigCraftable.Value)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair =>
            {
                var target = pair.Key.ToPoint();
                var stands = ReadStatueBlessingAdjacentStands(location, target);
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
            ? "blocked_farming_mastery_required"
            : player.hasBeenBlessedByStatueToday || activeBuffs.Length > 0
                ? "already_claimed_today"
                : statues.Length == 0
                    ? "no_statue_in_current_location"
                    : !statues.Any(statue => statue.has_available_adjacent_stand)
                        ? "blocked_no_adjacent_stand"
                        : "ready";

        return new
        {
            status,
            location_id = location.NameOrUniqueName,
            farming_mastery_stat_key = StatKeys.Mastery(0),
            farming_mastery_value = masteryValue,
            farming_mastery_unlocked = masteryValue >= 1,
            days_played = Game1.stats.DaysPlayed,
            is_raining = Game1.isRaining,
            is_festival_day = festivalDay,
            random_upper_bound_exclusive = randomUpperBoundExclusive,
            random_contract = "Utility.CreateDaySaveRandom(Game1.stats.DaysPlayed*777);discard_Next_8;Next(isRaining_or_festival?6:7)",
            blessing,
            blessing_id = blessingId,
            buff_id = "statue_of_blessings_" + blessingId,
            has_been_blessed_today = player.hasBeenBlessedByStatueToday,
            active_blessing_buffs = activeBuffs,
            has_active_blessing_buff = activeBuffs.Length > 0,
            statues,
            qualified_item_id = StatueOfBlessingsQualifiedItemId,
            buff_duration_contract = "Data/Buffs.Duration=-2_until_day_end",
            selection_lock_contract = "Farmer.hasBeenBlessedByStatueToday_or_hasBuffWithNameContainingString(statue_of_blessings_)",
            interaction_kind = "location_object",
            expected_action_type = "StatueOfBlessings",
            native_contract = StatueOfBlessingsNativeContract
        };
    }

    private static object StatueBlessingProjection(int blessingId) => blessingId switch
    {
        0 => new { blessing_id = 0, buff_id = "statue_of_blessings_0", kind = "speed", exact_effect = "Data/Buffs Speed=0.5 until day end", source = "Data/Buffs" },
        1 => new { blessing_id = 1, buff_id = "statue_of_blessings_1", kind = "luck", exact_effect = "Data/Buffs LuckLevel=1 until day end", source = "Data/Buffs" },
        2 => new { blessing_id = 2, buff_id = "statue_of_blessings_2", kind = "energy", exact_effect = "Farmer.Stamina rejects decreases while buff is active", source = "Farmer.Stamina" },
        3 => new { blessing_id = 3, buff_id = "statue_of_blessings_3", kind = "waters", exact_effect = "IncrementStat blessingOfWaters 3; each BobberBar consumes one charge and lowers fish difficulty", source = "Data/Buffs+BobberBar" },
        4 => new { blessing_id = 4, buff_id = "statue_of_blessings_4", kind = "friendship", exact_effect = "NPC.grantConversationFriendship uses 60 instead of default 20", source = "NPC.grantConversationFriendship" },
        5 => new { blessing_id = 5, buff_id = "statue_of_blessings_5", kind = "fangs", exact_effect = "GameLocation.damageMonster adds 0.1 critical chance", source = "GameLocation.damageMonster" },
        6 => new { blessing_id = 6, buff_id = "statue_of_blessings_6", kind = "butterfly", exact_effect = "one prismatic butterfly can spawn before 17:00; capture consumes buff and grants money plus 5%+DailyLuck prismatic shard roll", source = "GameLocation.tryAddPrismaticButterfly+Butterfly.update" },
        _ => throw new ArgumentOutOfRangeException(nameof(blessingId))
    };

    private static StatueBlessingStandProjection[] ReadStatueBlessingAdjacentStands(GameLocation location, Point target) =>
        new[]
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
            return new StatueBlessingStandProjection(stand.X, stand.Y, onMap, collisionBlocked);
        }).ToArray();

    private sealed record StatueBlessingStandProjection(int tile_x, int tile_y, bool on_map, bool collision_blocked)
    {
        public bool available => on_map && !collision_blocked;
    }
}
