using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mining;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter
{
    internal const string CalicoStatueNativeContract =
        "MineShaft_Buildings_284_checkAction_then_recentlyActivatedCalicoStatue_event_then_master_seeded_effect_rating_and_native_side_effect_receipt";

    private static object ReadCalicoStatue(MineShaft mine)
    {
        var player = Game1.player;
        var spot = mine.calicoStatueSpot.Value;
        var activatedSpot = mine.recentlyActivatedCalicoStatue.Value;
        var festivalDay = Utility.GetDayOfPassiveFestival("DesertFestival");
        var currentEffects = player.team.calicoStatueEffects.Pairs
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var nextActivationNumber = MineShaft.totalCalicoStatuesActivatedToday + 1;
        var averageDailyLuck = player.team.AverageDailyLuck(mine);
        var projectedEffectId = CalicoStatueEffectModel.SelectEffect(
            Utility.CreateDaySaveRandom(nextActivationNumber),
            averageDailyLuck,
            currentEffects);
        var projectedEffect = CalicoStatueEffectModel.GetRequired(projectedEffectId);
        var expectedEffects = new Dictionary<int, int>(currentEffects);
        expectedEffects[projectedEffectId] = expectedEffects.TryGetValue(projectedEffectId, out var count)
            ? count + 1
            : 1;
        var targetTileIndex = spot == Point.Zero ? -1 : mine.getTileIndexAt(spot.X, spot.Y, "Buildings", "mine");
        var stands = spot == Point.Zero ? Array.Empty<object>() : CalicoStatueStandTiles(mine, spot);
        var activeInvasion = festivalDay > 0 && mine.getMineArea() == MineShaft.desertArea;
        var gateStatus = !activeInvasion
            ? "excluded_not_desert_festival_skull_cavern"
            : !Game1.IsMasterGame
                ? "blocked_host_authoritative_seed_projection_required"
            : spot == Point.Zero
                ? "excluded_no_calico_statue_on_loaded_floor"
                : activatedSpot != Point.Zero || targetTileIndex == 285
                    ? "complete_current_floor_statue_already_activated"
                    : targetTileIndex != 284
                        ? "blocked_calico_statue_tile_identity_drifted"
                        : stands.Length == 0
                            ? "blocked_no_reachable_adjacent_stand"
                            : "ready";
        var currentEffectsCsv = CalicoEffectsCsv(currentEffects);
        var expectedEffectsCsv = CalicoEffectsCsv(expectedEffects);
        var fingerprint = CalicoSha256(JsonSerializer.Serialize(new
        {
            schema = "calico_statue.v1",
            location = mine.NameOrUniqueName,
            mine.mineLevel,
            mine_area = mine.getMineArea(),
            festivalDay,
            spot,
            activatedSpot,
            targetTileIndex,
            stands,
            total_before = MineShaft.totalCalicoStatuesActivatedToday,
            rating_before = player.team.calicoEggSkullCavernRating.Value,
            averageDailyLuck,
            days_played = Game1.stats.DaysPlayed,
            unique_game_id_half = Game1.uniqueIDForThisGame / 2,
            Game1.UseLegacyRandom,
            Game1.IsMasterGame,
            currentEffectsCsv,
            projectedEffectId,
            expectedEffectsCsv
        }));

        return new
        {
            schema_version = "calico_statue.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            gate_status = gateStatus,
            location_id = mine.NameOrUniqueName,
            mine_level = mine.mineLevel,
            mine_area = mine.getMineArea(),
            mine_kind = mine.getMineArea() == MineShaft.desertArea ? "skull_cavern" : "other_mineshaft",
            desert_festival_day = festivalDay,
            is_desert_festival_skull_cavern = activeInvasion,
            target_tile_x = spot.X,
            target_tile_y = spot.Y,
            target_tile_index_before = targetTileIndex,
            target_tile_index_after = 285,
            activated_tile_x = activatedSpot.X,
            activated_tile_y = activatedSpot.Y,
            is_activated = activatedSpot != Point.Zero || targetTileIndex == 285,
            stand_tiles = stands,
            total_activated_today_before = MineShaft.totalCalicoStatuesActivatedToday,
            next_activation_number = nextActivationNumber,
            rating_before = player.team.calicoEggSkullCavernRating.Value,
            expected_rating_after = player.team.calicoEggSkullCavernRating.Value + 1,
            average_daily_luck = averageDailyLuck,
            days_played = Game1.stats.DaysPlayed,
            unique_game_id_half = (Game1.uniqueIDForThisGame / 2).ToString(),
            use_legacy_random = Game1.UseLegacyRandom,
            host_authoritative = Game1.IsMasterGame,
            current_effects = currentEffects.OrderBy(pair => pair.Key).Select(CalicoEffectState).ToArray(),
            current_effects_csv = currentEffectsCsv,
            projected_effect = CalicoEffectProjection(projectedEffect),
            projected_effect_id = projectedEffectId,
            expected_effects_after_csv = expectedEffectsCsv,
            effect_catalog = CalicoStatueEffectModel.All.Select(CalicoEffectProjection).ToArray(),
            calico_eggs_before = CountCalicoEggs(player, mine),
            health_before = player.health,
            max_health = player.maxHealth,
            stamina_before = player.Stamina,
            max_stamina = player.MaxStamina,
            speed_buff_active_before = player.hasBuff("CalicoStatueSpeed"),
            interaction_kind = "mineshaft_buildings_tile",
            expected_action_type = "CalicoStatue",
            native_contract = CalicoStatueNativeContract,
            selection_policy = "small_model_accepts_or_rejects_exact_projected_effect;fresh_projection_must_match;one_native_activation_then_replan",
            multiplayer_policy = "host_only_because_totalCalicoStatuesActivatedToday_is_non_net_static_seed_input_and_effects_are_team_wide",
            direct_mutation_policy = "forbid_rating_effect_dictionary_reward_health_stamina_buff_tile_or_rng_writes_in_production_executor"
        };
    }

    private static object[] CalicoStatueStandTiles(MineShaft mine, Point target)
    {
        var collisionMask = CollisionMask.All & ~CollisionMask.Farmers;
        return new[]
            {
                new Point(target.X, target.Y - 1),
                new Point(target.X, target.Y + 1),
                new Point(target.X - 1, target.Y),
                new Point(target.X + 1, target.Y)
            }
            .Where(tile => mine.isTileOnMap(tile.X, tile.Y))
            .Select(tile => new
            {
                tile_x = tile.X,
                tile_y = tile.Y,
                available = mine.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport) &&
                    !mine.IsTileBlockedBy(tile.ToVector2(), collisionMask, CollisionMask.None, useFarmerTile: true) &&
                    !mine.farmers.Any(farmer => farmer != Game1.player && FarmerBlocksTile(farmer, tile.X, tile.Y))
            })
            .Cast<object>()
            .ToArray();
    }

    private static object CalicoEffectProjection(CalicoStatueEffectDefinition definition) => new
    {
        effect_id = definition.EffectId,
        effect_key = definition.EffectKey,
        strategy_polarity = definition.StrategyPolarity,
        can_stack = definition.CanStack,
        calico_egg_reward = definition.CalicoEggReward,
        exact_effect = definition.ExactEffect,
        localized_description = Game1.content.LoadString(
            "Strings\\1_6_Strings:DF_Mine_CalicoStatue_Description_" + definition.EffectId)
    };

    private static object CalicoEffectState(KeyValuePair<int, int> pair)
    {
        var definition = CalicoStatueEffectModel.GetRequired(pair.Key);
        return new
        {
            effect_id = pair.Key,
            stack_count = pair.Value,
            effect_key = definition.EffectKey,
            strategy_polarity = definition.StrategyPolarity,
            exact_effect = definition.ExactEffect
        };
    }

    private static int CountCalicoEggs(Farmer player, MineShaft mine) =>
        player.Items.CountId("(O)CalicoEgg") + mine.debris.Sum(debris =>
            debris.item?.QualifiedItemId == "(O)CalicoEgg" ? debris.item.Stack : 0);

    private static string CalicoEffectsCsv(IReadOnlyDictionary<int, int> effects) =>
        string.Join(",", effects.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value));

    private static string CalicoSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
