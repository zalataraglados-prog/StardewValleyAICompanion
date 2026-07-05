using System.Reflection;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class MenuReadAdapter : ReadAdapterBase
{
    private const string AdapterId = "vanilla_1_6_menu";

    private static readonly string[] MenuFields =
    {
        "active_menu",
        "identity",
        "screen_bounds",
        "public_state",
        "menu_specific_state"
    };

    public override string Domain => "menus";
    public override int Priority => 35;

    public override StateAdapterResult Collect(long tick)
    {
        var menu = Game1.activeClickableMenu;
        if (menu is null)
        {
            return Section("menus", new Dictionary<string, object>
            {
                ["active_menu"] = Field(new
                {
                    is_open = false,
                    type = "none",
                    full_type = (string?)null
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["identity"] = Field(new
                {
                    is_applicable = false,
                    reason = "no_active_clickable_menu"
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["screen_bounds"] = Field(new
                {
                    is_applicable = false,
                    reason = "no_active_clickable_menu"
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["public_state"] = Field(new
                {
                    is_applicable = false,
                    reason = "no_active_clickable_menu"
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["menu_specific_state"] = Field(new
                {
                    is_applicable = false,
                    reason = "no_active_clickable_menu"
                }, "Game1.activeClickableMenu", tick, AdapterId)
            }, Array.Empty<string>(), "complete");
        }

        var (menuSpecificState, unavailableMenuSpecificFields) = ReadMenuSpecificState(menu, tick);
        var unavailableFields = new List<string>(unavailableMenuSpecificFields);
        if (menuSpecificState is null)
        {
            menuSpecificState = Unavailable(
                "menu_specific_fields_not_verified_for_menu_type",
                "Game1.activeClickableMenu concrete menu fields",
                tick,
                AdapterId);
            unavailableFields.Add("menus.menu_specific_state");
        }

        return Section("menus", new Dictionary<string, object>
        {
            ["active_menu"] = Field(ReadActiveMenu(menu), "Game1.activeClickableMenu", tick, AdapterId),
            ["identity"] = Field(ReadIdentity(menu), "Game1.activeClickableMenu.GetType()", tick, AdapterId),
            ["screen_bounds"] = ReadScreenBounds(menu, tick),
            ["public_state"] = ReadPublicState(menu, tick),
            ["menu_specific_state"] = menuSpecificState
        }, unavailableFields.ToArray(), "partial");
    }

    private static object ReadActiveMenu(IClickableMenu menu)
    {
        var type = menu.GetType();
        return new
        {
            is_open = true,
            type = type.Name,
            full_type = type.FullName
        };
    }

    private static object ReadIdentity(IClickableMenu menu)
    {
        var type = menu.GetType();
        return new
        {
            type = type.Name,
            full_type = type.FullName,
            assembly = type.Assembly.GetName().Name,
            is_iclickable_menu = menu is IClickableMenu
        };
    }

    private static object ReadScreenBounds(IClickableMenu menu, long tick)
    {
        var x = TryReadPublicInt(menu, "xPositionOnScreen");
        var y = TryReadPublicInt(menu, "yPositionOnScreen");
        var width = TryReadPublicInt(menu, "width");
        var height = TryReadPublicInt(menu, "height");
        if (x is null || y is null || width is null || height is null)
        {
            return Unavailable(
                "iclickablemenu_public_bounds_fields_not_available",
                "IClickableMenu.xPositionOnScreen/yPositionOnScreen/width/height",
                tick,
                AdapterId);
        }

        return Field(new
        {
            x,
            y,
            width,
            height
        }, "IClickableMenu.xPositionOnScreen/yPositionOnScreen/width/height", tick, AdapterId);
    }

    private static object ReadPublicState(IClickableMenu menu, long tick)
    {
        var destroy = TryReadPublicBool(menu, "destroy");
        var invisible = TryReadPublicBool(menu, "invisible");
        var gameWindowSizeChanged = TryReadPublicBool(menu, "gameWindowSizeChanged");
        var closeButton = TryReadPublicField(menu, "upperRightCloseButton");
        var snappedComponent = TryReadPublicField(menu, "currentlySnappedComponent");
        var state = new Dictionary<string, object?>
        {
            ["destroy"] = destroy,
            ["invisible"] = invisible,
            ["game_window_size_changed"] = gameWindowSizeChanged,
            ["upper_right_close_button_present"] = closeButton is not null,
            ["currently_snapped_component_present"] = snappedComponent is not null
        };

        if (destroy is null &&
            invisible is null &&
            gameWindowSizeChanged is null &&
            !HasPublicField("upperRightCloseButton") &&
            !HasPublicField("currentlySnappedComponent"))
        {
            return Unavailable(
                "iclickablemenu_public_state_fields_not_available",
                "IClickableMenu public state fields",
                tick,
                AdapterId);
        }

        return Field(state, "IClickableMenu public state fields", tick, AdapterId);
    }

    private static (object? Field, IReadOnlyList<string> UnavailableFields) ReadMenuSpecificState(IClickableMenu menu, long tick)
    {
        return menu switch
        {
            ItemGrabMenu itemGrabMenu when itemGrabMenu.source == ItemGrabMenu.source_chest =>
                (Field(ReadChestMenuState(itemGrabMenu, tick), "ItemGrabMenu public fields for chest source", tick, AdapterId),
                    new[] { "menus.menu_specific_state.chest.source_item_details" }),
            InventoryMenu inventoryMenu =>
                (Field(ReadInventoryMenuState(inventoryMenu), "InventoryMenu public fields", tick, AdapterId),
                    Array.Empty<string>()),
            ShopMenu shopMenu =>
                (Field(ReadShopMenuState(shopMenu, tick), "ShopMenu public fields", tick, AdapterId),
                    new[] { "menus.menu_specific_state.shop.current_tab" }),
            DialogueBox dialogueBox =>
                (Field(ReadDialogueState(dialogueBox, tick), "DialogueBox public fields", tick, AdapterId),
                    new[] { "menus.menu_specific_state.dialogue.current_text" }),
            _ => (null, Array.Empty<string>())
        };
    }

    private static object ReadInventoryMenuState(InventoryMenu menu)
    {
        return new
        {
            kind = "inventory",
            inventory = ReadInventorySummary(menu)
        };
    }

    private static object ReadChestMenuState(ItemGrabMenu menu, long tick)
    {
        return new
        {
            kind = "chest",
            source = menu.source,
            source_name = "chest",
            reverse_grab = menu.reverseGrab,
            show_receiving_menu = menu.showReceivingMenu,
            draw_background = menu.drawBG,
            destroy_item_on_click = menu.destroyItemOnClick,
            can_exit_on_key = menu.canExitOnKey,
            play_right_click_sound = menu.playRightClickSound,
            allow_right_click = menu.allowRightClick,
            shipping_bin = menu.shippingBin,
            snapped_to_bottom = menu.snappedtoBottom,
            essential = menu.essential,
            super_essential = menu.superEssential,
            storage_space_top_border_offset = menu.storageSpaceTopBorderOffset,
            message_present = menu.message is not null,
            source_item_type = TypeName(menu.sourceItem),
            context_type = TypeName(menu.context),
            source_item_details = Unavailable(
                "chest_source_item_concrete_fields_not_verified_in_this_slice",
                "ItemGrabMenu.sourceItem concrete fields",
                tick,
                AdapterId),
            receiving_inventory = menu.ItemsToGrabMenu is null ? null : ReadInventorySummary(menu.ItemsToGrabMenu),
            player_inventory = menu.inventory is null ? null : ReadInventorySummary(menu.inventory),
            buttons = new
            {
                fill_stacks_present = menu.fillStacksButton is not null,
                organize_present = menu.organizeButton is not null,
                color_picker_toggle_present = menu.colorPickerToggleButton is not null,
                special_present = menu.specialButton is not null,
                last_shipped_holder_present = menu.lastShippedHolder is not null,
                junimo_note_present = menu.junimoNoteIcon is not null,
                discrete_color_picker_component_count = menu.discreteColorPickerCC?.Count
            },
            transient_sprite_count = menu._transferredItemSprites?.Count
        };
    }

    private static object ReadShopMenuState(ShopMenu menu, long tick)
    {
        return new
        {
            kind = "shop",
            shop_id = menu.ShopId,
            shop_data_present = menu.ShopData is not null,
            currency = menu.currency,
            read_only = menu.readOnly,
            current_item_index = menu.currentItemIndex,
            safety_timer = menu.safetyTimer,
            for_sale_count = menu.forSale?.Count,
            for_sale_button_count = menu.forSaleButtons?.Count,
            item_price_and_stock_count = menu.itemPriceAndStock?.Count,
            categories_to_sell_count = menu.categoriesToSellHere?.Count,
            tag_group_count = menu.tagsToSellHere?.Count,
            tab_button_count = menu.tabButtons?.Count,
            buy_back_item_count = menu.buyBackItems?.Count,
            buy_back_resell_tomorrow_count = menu.buyBackItemsToResellTomorrow?.Count,
            held_item_present = menu.heldItem is not null,
            hovered_item_present = menu.hoveredItem is not null,
            hover_price = menu.hoverPrice,
            portrait_texture_present = menu.portraitTexture is not null,
            portrait_dialogue_present = menu.potraitPersonDialogue is not null,
            inventory = menu.inventory is null ? null : ReadInventorySummary(menu.inventory),
            current_tab = Unavailable(
                "shop_current_tab_is_protected_field_not_read_in_this_slice",
                "ShopMenu.currentTab",
                tick,
                AdapterId),
            controls = new
            {
                up_arrow_present = menu.upArrow is not null,
                down_arrow_present = menu.downArrow is not null,
                scroll_bar_present = menu.scrollBar is not null
            }
        };
    }

    private static object ReadDialogueState(DialogueBox menu, long tick)
    {
        return new
        {
            kind = "dialogue",
            dialogue_count = menu.dialogues?.Count,
            character_dialogue_present = menu.characterDialogue is not null,
            broken_up_dialogue_count = menu.characterDialoguesBrokenUp?.Count,
            response_count = menu.responses?.Length,
            response_component_count = menu.responseCC?.Count,
            is_question = menu.isQuestion,
            selected_response = menu.selectedResponse,
            dialogue_finished = menu.dialogueFinished,
            dialogue_continued_on_next_page = menu.dialogueContinuedOnNextPage,
            show_typing = menu.showTyping,
            transitioning = menu.transitioning,
            transitioning_bigger = menu.transitioningBigger,
            transition_initialized = menu.transitionInitialized,
            character_index_in_dialogue = menu.characterIndexInDialogue,
            character_advance_timer = menu.characterAdvanceTimer,
            safety_timer = menu.safetyTimer,
            question_finish_pause_timer = menu.questionFinishPauseTimer,
            height_for_questions = menu.heightForQuestions,
            new_portrait_shake_timer = menu.newPortaitShakeTimer,
            dialogue_icon_present = menu.dialogueIcon is not null,
            above_dialogue_image_present = menu.aboveDialogueImage is not null,
            friendship_jewel = new
            {
                x = menu.friendshipJewel.X,
                y = menu.friendshipJewel.Y,
                width = menu.friendshipJewel.Width,
                height = menu.friendshipJewel.Height
            },
            dialogue_bounds = new
            {
                x = menu.x,
                y = menu.y,
                width = menu.width,
                height = menu.height
            },
            transition_bounds = new
            {
                x = menu.transitionX,
                y = menu.transitionY,
                width = menu.transitionWidth,
                height = menu.transitionHeight
            },
            current_text = Unavailable(
                "dialogue_current_text_requires_method_call_not_read_in_this_slice",
                "DialogueBox.getCurrentString()",
                tick,
                AdapterId)
        };
    }

    private static object ReadInventorySummary(InventoryMenu menu)
    {
        return new
        {
            player_inventory = menu.playerInventory,
            draw_slots = menu.drawSlots,
            show_grayed_out_slots = menu.showGrayedOutSlots,
            capacity = menu.capacity,
            rows = menu.rows,
            horizontal_gap = menu.horizontalGap,
            vertical_gap = menu.verticalGap,
            component_count = menu.inventory?.Count,
            actual_inventory_count = menu.actualInventory?.Count,
            occupied_slot_count = menu.actualInventory?.Count(item => item is not null),
            drop_item_invisible_button_present = menu.dropItemInvisibleButton is not null,
            move_item_sound = menu.moveItemSound
        };
    }

    private static string? TypeName(object? value)
    {
        return value?.GetType().FullName;
    }

    private static int? TryReadPublicInt(IClickableMenu menu, string fieldName)
    {
        return TryReadPublicField(menu, fieldName) is int value ? value : null;
    }

    private static bool? TryReadPublicBool(IClickableMenu menu, string fieldName)
    {
        return TryReadPublicField(menu, fieldName) is bool value ? value : null;
    }

    private static object? TryReadPublicField(IClickableMenu menu, string fieldName)
    {
        var field = typeof(IClickableMenu).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field?.GetValue(menu);
    }

    private static bool HasPublicField(string fieldName)
    {
        return typeof(IClickableMenu).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public) is not null;
    }
}
