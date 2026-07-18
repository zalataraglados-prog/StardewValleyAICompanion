using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter : ReadAdapterBase
{
    public override string Domain => "player";
    public override int Priority => 20;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var inventory = player?.Items
            .Select((item, index) => new
            {
                slot_index = index,
                item_id = item?.ItemId,
                qualified_item_id = item?.QualifiedItemId,
                display_name = item?.DisplayName,
                stack = item?.Stack,
                quality = item?.Quality,
                category = item?.Category,
                maximum_stack_size = item?.maximumStackSize(),
                can_be_shipped = item?.canBeShipped(),
                can_be_trashed = item?.canBeTrashed(),
                sell_to_store_price = item?.sellToStorePrice(-1L),
                sale_price = item?.salePrice(),
                runtime_type = item?.GetType().FullName,
                special_item = item?.specialItem,
                context_tags = item?.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                base_tag_not_giftable = item is null ? (bool?)null : StardewValley.ItemContextTagManager.HasBaseTag(item.QualifiedItemId, "not_giftable"),
                is_object = item is StardewValley.Object,
                object_type = item is StardewValley.Object obj ? obj.Type : null,
                object_quest_item = item is StardewValley.Object objQuest ? (bool?)objQuest.questItem.Value : null,
                object_big_craftable = item is StardewValley.Object objBig ? (bool?)objBig.bigCraftable.Value : null,
                can_be_given_as_gift = item?.canBeGivenAsGift(),
                is_furniture = item is StardewValley.Objects.Furniture,
                is_wallpaper = item is StardewValley.Objects.Wallpaper,
                protected_from_auto_sell = SellProtectionReasons(item).Length > 0,
                auto_sell_protection_reasons = SellProtectionReasons(item),
                is_empty = item is null
            })
            .ToArray();
        var seedInventory = player?.Items
            .Select((item, index) => ReadSeedInventoryItem(item, index, player.currentLocation))
            .Where(item => item is not null)
            .ToArray();

        var playerFields = new Dictionary<string, object>
        {
            ["location_id"] = Field(player?.currentLocation?.NameOrUniqueName, "Game1.player.currentLocation.NameOrUniqueName", tick),
            ["tile_x"] = Field(Context.IsWorldReady ? (int?)player?.TilePoint.X : null, "Game1.player.TilePoint.X", tick),
            ["tile_y"] = Field(Context.IsWorldReady ? (int?)player?.TilePoint.Y : null, "Game1.player.TilePoint.Y", tick),
            ["facing_direction"] = Field(Context.IsWorldReady ? (int?)player?.FacingDirection : null, "Game1.player.FacingDirection", tick),
            ["current_tool_index"] = Field(Context.IsWorldReady ? (int?)player?.CurrentToolIndex : null, "Game1.player.CurrentToolIndex", tick),
            ["money"] = Field(player?.Money, "Game1.player.Money", tick),
            ["total_money_earned"] = Field(Context.IsWorldReady ? (uint?)player?.totalMoneyEarned : null, "Game1.player.totalMoneyEarned", tick),
            ["health"] = Field(player?.health, "Game1.player.health", tick),
            ["max_health"] = Field(player?.maxHealth, "Game1.player.maxHealth", tick),
            ["energy"] = Field(player?.Stamina, "Game1.player.Stamina", tick),
            ["max_energy"] = Field(player?.MaxStamina, "Game1.player.MaxStamina", tick),
            ["level"] = Field(Context.IsWorldReady ? (int?)player?.Level : null, "Game1.player.Level", tick),
            ["skills_detail"] = Field(ReadSkillsDetail(player), "Game1.player.GetUnmodifiedSkillLevel/GetSkillLevel/experiencePoints and Farmer.getBaseExperienceForLevel", tick),
            ["book_candidates"] = Field(ReadBookCandidates(player), "Game1.player.Items and Object.performUseAction/readBook native branches", tick),
            ["luck_context"] = Field(ReadLuckContext(player), "Game1.player.team.sharedDailyLuck, Farmer.DailyLuck/LuckLevel, Farmer.hasSpecialCharm, BuffManager.AppliedBuffs", tick),
            ["has_skull_key"] = Field(Context.IsWorldReady ? (bool?)player?.hasSkullKey : null, "Game1.player.hasSkullKey", tick),
            ["has_rusty_key"] = Field(Context.IsWorldReady ? (bool?)player?.hasRustyKey : null, "Game1.player.hasRustyKey", tick),
            ["married_or_roommate"] = Field(Context.IsWorldReady ? (bool?)player?.isMarriedOrRoommates() : null, "Game1.player.isMarriedOrRoommates()", tick),
            ["engaged"] = Field(Context.IsWorldReady ? (bool?)player?.isEngaged() : null, "Game1.player.isEngaged()", tick),
            ["spouse"] = Field(Context.IsWorldReady && player is not null ? (player.spouse ?? string.Empty) : null, "Game1.player.spouse", tick),
            ["has_pending_roommate"] = Field(Context.IsWorldReady ? (bool?)player?.hasCurrentOrPendingRoommate() : null, "Game1.player.hasCurrentOrPendingRoommate()", tick),
            ["can_understand_dwarves"] = Field(Context.IsWorldReady ? (bool?)player?.canUnderstandDwarves : null, "Game1.player.canUnderstandDwarves", tick),
            ["book_friendship"] = Field(Context.IsWorldReady && player is not null ? (long?)player.stats.Get("Book_Friendship") : null, "Game1.player.stats.Get(\"Book_Friendship\")", tick),
            ["active_dialogue_events"] = Field(Context.IsWorldReady ? player?.activeDialogueEvents.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray() : null, "Game1.player.activeDialogueEvents.Keys", tick),
            ["farmhouse_upgrade_level"] = Field(ReadFarmhouseUpgradeLevel(player), "Utility.getHomeOfFarmer(Game1.player).upgradeLevel", tick),
            ["days_until_farmhouse_upgrade"] = Field(Context.IsWorldReady ? (int?)player?.daysUntilHouseUpgrade.Value : null, "Game1.player.daysUntilHouseUpgrade.Value", tick),
            ["current_tool"] = Field(player?.CurrentTool?.QualifiedItemId ?? player?.CurrentTool?.DisplayName, "Game1.player.CurrentTool", tick),
            ["current_item_qualified_id"] = Field(player?.CurrentItem?.QualifiedItemId, "Game1.player.CurrentItem.QualifiedItemId", tick),
            ["active_object_qualified_id"] = Field(player?.ActiveObject?.QualifiedItemId, "Game1.player.ActiveObject.QualifiedItemId", tick),
            ["machine_crafting"] = Field(ReadMachineCraftingContext(player), "Game1.player.craftingRecipes, CraftingRecipe.craftingRecipes/recipeList/ItemMatchesForCrafting, ItemRegistry, Object.GetMachineData", tick),
            ["machine_placement"] = Field(ReadMachinePlacementContext(player), "Utility.ForEachLocation(includeInteriors:true, includeGenerated:false); Utility.isPlacementForbiddenHere; Object.canBePlacedHere with static and current collision masks; Object.placementAction runtime recheck contract", tick),
            ["safe_item_context"] = Field(ReadSafeItemContext(player), "Game1.player.CurrentToolIndex and Game1.player.Items toolbar safe slot scan", tick),
            ["inventory_capacity"] = Field(ReadInventoryCapacity(player), "Game1.player.Items and maxItems", tick),
            ["active_menu"] = Field(Game1.activeClickableMenu?.GetType().FullName ?? "none", "Game1.activeClickableMenu", tick)
        };

        return Section("player", playerFields.Concat(new Dictionary<string, object>
        {
            ["inventory"] = Field(inventory, "Game1.player.Items", tick),
            ["seed_inventory"] = Field(seedInventory, "Game1.player.Items filtered by Object.SeedsCategory and Game1.cropData", tick)
        }).ToDictionary(item => item.Key, item => item.Value));
    }

    private static object? ReadSeedInventoryItem(Item? item, int index, GameLocation? location)
    {
        if (item is null)
        {
            return null;
        }

        var resolvedSeedId = Crop.ResolveSeedId(item.ItemId, location);
        var cropCatalogMatch = Game1.cropData.ContainsKey(resolvedSeedId);
        if (item.Category != StardewValley.Object.SeedsCategory && !cropCatalogMatch)
        {
            return null;
        }

        return new
        {
            slot_index = index,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            display_name = item.DisplayName,
            stack = item.Stack,
            quality = item.Quality,
            category = item.Category,
            seed_id = resolvedSeedId,
            crop_catalog_match = cropCatalogMatch
        };
    }

    private static object ReadSafeItemContext(Farmer? player)
    {
        if (player is null)
        {
            return new
            {
                current_tool_index = (int?)null,
                active_object_selected = false,
                safe_slot_available = false,
                safe_slot_index = (int?)null,
                safe_slot_kind = "unavailable",
                policy = "prefer_empty_slot_then_tool_slot"
            };
        }

        var safeSlot = FindSafeItemSlot(player);
        var hasEmptySlot = safeSlot.HasValue && player.Items[safeSlot.Value] is null;
        var hasToolSlot = safeSlot.HasValue && player.Items[safeSlot.Value] is Tool;

        return new
        {
            current_tool_index = player.CurrentToolIndex,
            active_object_selected = player.ActiveObject is not null,
            safe_slot_available = safeSlot.HasValue,
            safe_slot_index = safeSlot,
            safe_slot_kind = hasEmptySlot ? "empty" : hasToolSlot ? "tool" : "unavailable",
            policy = "prefer_empty_slot_then_tool_slot"
        };
    }

    internal static int? FindSafeItemSlot(Farmer player)
    {
        var toolbarCount = Math.Min(12, player.Items.Count);
        for (var index = 0; index < toolbarCount; index++)
        {
            if (player.Items[index] is null)
            {
                return index;
            }
        }
        for (var index = 0; index < toolbarCount; index++)
        {
            if (player.Items[index] is Tool)
            {
                return index;
            }
        }
        return null;
    }

    private static object ReadInventoryCapacity(Farmer? player)
    {
        if (player is null)
        {
            return new
            {
                max_items = (int?)null,
                occupied_item_stacks = (int?)null,
                empty_slots = (int?)null,
                has_empty_slot = false
            };
        }

        var maxItems = player.maxItems.Value;
        var occupied = player.Items.Take(maxItems).Count(item => item is not null);
        var empty = Math.Max(0, maxItems - occupied);
        return new
        {
            max_items = maxItems,
            occupied_item_stacks = occupied,
            empty_slots = empty,
            has_empty_slot = empty > 0
        };
    }

    private static string[] SellProtectionReasons(Item? item)
    {
        if (item is null)
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (item.specialItem)
        {
            reasons.Add("special_item");
        }

        if (!item.canBeTrashed())
        {
            reasons.Add("cannot_be_trashed");
        }

        if (item.sellToStorePrice(-1L) <= 0)
        {
            reasons.Add("non_positive_sell_price");
        }

        if (item is StardewValley.Object obj)
        {
            if (obj.questItem.Value)
            {
                reasons.Add("quest_item");
            }

            if (obj.Type == "Quest")
            {
                reasons.Add("object_type_quest");
            }

            if (obj.bigCraftable.Value)
            {
                reasons.Add("big_craftable");
            }
        }

        if (item is StardewValley.Objects.Furniture)
        {
            reasons.Add("furniture");
        }

        if (item is StardewValley.Objects.Wallpaper)
        {
            reasons.Add("wallpaper");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int? ReadFarmhouseUpgradeLevel(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        return Utility.getHomeOfFarmer(player) is FarmHouse farmhouse
            ? farmhouse.upgradeLevel
            : null;
    }
}
