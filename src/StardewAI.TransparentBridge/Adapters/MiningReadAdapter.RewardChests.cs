using System.Text.Json;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter
{
    private static readonly HashSet<int> OrdinaryRewardLevels = new() { 10, 20, 40, 50, 60, 70, 80, 90, 100, 110 };

    private static object[] ReadRewardChests(MineShaft mine, Farmer player)
    {
        var area = mine.getMineArea();
        var mineKind = area == 121 ? "skull_cavern" : area == 77377 ? "quarry_mine" : "ordinary_mines";
        var treasureRoom = ReadPrivateNetBool(mine, TreasureRoomField) == true;
        return mine.overlayObjects
            .Where(pair => pair.Value is Chest)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => ReadRewardChest(mine, player, (Chest)pair.Value, (int)pair.Key.X, (int)pair.Key.Y, mineKind, treasureRoom))
            .ToArray();
    }

    private static object ReadRewardChest(
        MineShaft mine,
        Farmer player,
        Chest chest,
        int tileX,
        int tileY,
        string mineKind,
        bool treasureRoom)
    {
        var item = chest.Items.Count == 1 ? chest.Items[0] : null;
        var isSkullKey = item is SpecialItem special && special.which.Value == 4;
        var isStardrop = item?.QualifiedItemId == "(O)434";
        var needsInventorySpace = false;
        var showNotification = false;
        if (item is not null)
        {
            player.GetItemReceiveBehavior(item, out needsInventorySpace, out showNotification);
        }
        var inventoryAccepts = item is not null && (!needsInventorySpace || player.couldInventoryAcceptThisItem(item));
        var branch = MineRewardChestBranch(mine, mineKind, treasureRoom, isSkullKey);
        var exactVanillaShape = chest.GetType() == typeof(Chest) &&
            !chest.playerChest.Value && !chest.giftbox.Value && !chest.dropContents.Value &&
            !chest.synchronized.Value && chest.SpecialChestType == Chest.SpecialChestTypes.None &&
            chest.Items.Count == 1 && item is not null;
        var status = isSkullKey
            ? "excluded_skull_key_specialized_chain"
            : branch == "unsupported_mine_chest"
                ? "blocked_unknown_mineshaft_chest_family"
                : !exactVanillaShape
                    ? "blocked_non_vanilla_reward_chest_shape"
                    : !inventoryAccepts
                        ? "blocked_inventory_cannot_accept_exact_reward"
                        : !needsInventorySpace && !isStardrop
                            ? "blocked_non_inventory_reward_receipt_not_modeled"
                        : isStardrop && player.mailReceived.Contains("CF_Mines")
                            ? "blocked_stardrop_already_consumed"
                        : "ready";
        object[] projectedItems;
        if (item is not null && !isStardrop && needsInventorySpace)
        {
            projectedItems = new object[] { ClearanceOutputItemProjection.FromInventoryReceipt(item) };
        }
        else
        {
            projectedItems = Array.Empty<object>();
        }

        return new
        {
            tile_x = tileX,
            tile_y = tileY,
            runtime_type = chest.GetType().FullName,
            mine_level = mine.mineLevel,
            mine_kind = mineKind,
            reward_branch = branch,
            status,
            item_count = chest.Items.Count,
            player_chest = chest.playerChest.Value,
            giftbox = chest.giftbox.Value,
            drop_contents = chest.dropContents.Value,
            synchronized = chest.synchronized.Value,
            special_chest_type = chest.SpecialChestType.ToString(),
            starting_lid_frame = chest.startingLidFrame.Value,
            lid_frame_count = chest.lidFrameCount.Value,
            frame_counter = chest.frameCounter.Value,
            contains_skull_key = isSkullKey,
            is_stardrop = isStardrop,
            item = item is null ? null : new
            {
                runtime_type = item.GetType().FullName,
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                quantity = item.Stack,
                quality = item.Quality,
                is_recipe = item.IsRecipe,
                needs_inventory_space = needsInventorySpace,
                show_notification = showNotification,
                inventory_accepts = inventoryAccepts
            },
            expected_output_items = projectedItems,
            expected_output_items_json = JsonSerializer.Serialize(projectedItems),
            native_gain_experience_skill_index = Farmer.luckSkill,
            native_gain_experience_call_amount = 25 + mine.mineLevel,
            native_gain_experience_call_ignored = true,
            expected_luck_experience_delta = 0,
            expected_skill_id = "luck",
            chest_consumed_level_before = player.chestConsumedMineLevels.ContainsKey(mine.mineLevel),
            expected_chest_consumed_level_after = true,
            stardrop_mail_flag = isStardrop ? "CF_Mines" : string.Empty,
            stardrop_mail_before = isStardrop && player.mailReceived.Contains("CF_Mines"),
            expected_stardrop_max_stamina_delta = isStardrop ? 34 : 0,
            native_contract = "one_reward_open_then_wait_dumpContents_then_empty_chest_cleanup_checkAction",
            source = "MineShaft.overlayObjects live Chest plus Chest.dumpContents/Farmer.gainExperience/Farmer.GetItemReceiveBehavior"
        };
    }

    private static string MineRewardChestBranch(MineShaft mine, string mineKind, bool treasureRoom, bool isSkullKey)
    {
        if (isSkullKey)
        {
            return "ordinary_floor_120_skull_key";
        }
        if (mineKind == "ordinary_mines" && OrdinaryRewardLevels.Contains(mine.mineLevel))
        {
            return mine.mineLevel == 100 ? "ordinary_fixed_stardrop" : "ordinary_fixed_reward";
        }
        if (mineKind == "skull_cavern" && (treasureRoom || mine.mineLevel is 220 or 320 or 420))
        {
            return mine.mineLevel is 220 or 320 or 420 ? "skull_cavern_forced_treasure" : "skull_cavern_treasure_room";
        }
        return "unsupported_mine_chest";
    }
}
