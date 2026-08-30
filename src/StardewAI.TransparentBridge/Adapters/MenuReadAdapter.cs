using System.Reflection;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MenuReadAdapter : ReadAdapterBase
{
    private const string AdapterId = "vanilla_1_6_menu";

    private static readonly string[] MenuFields =
    {
        "active_menu",
        "sleep_prompt_context",
        "tent_sleep_prompt_context",
        "identity",
        "screen_bounds",
        "public_state",
        "shop_stock",
        "sell_context",
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
                    full_type = (string?)null,
                    last_question_key = Game1.currentLocation?.lastQuestionKey,
                    is_sleep_prompt = false,
                    is_tent_sleep_prompt = false
                }, "Game1.activeClickableMenu", tick, AdapterId),
                ["sleep_prompt_context"] = Field(ReadSleepPromptContext(null), "Game1.activeClickableMenu as DialogueBox; Game1.currentLocation.lastQuestionKey", tick, AdapterId),
                ["tent_sleep_prompt_context"] = Field(ReadTentSleepPromptContext(null), "Game1.activeClickableMenu as DialogueBox; Game1.currentLocation.lastQuestionKey", tick, AdapterId),
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
                ["shop_stock"] = Unavailable(
                    "no_shop_menu_open",
                    "Game1.activeClickableMenu as ShopMenu",
                    tick,
                    AdapterId),
                ["sell_context"] = Unavailable(
                    "no_shop_menu_open",
                    "Game1.activeClickableMenu as ShopMenu",
                    tick,
                    AdapterId),
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
            ["sleep_prompt_context"] = Field(ReadSleepPromptContext(menu), "Game1.activeClickableMenu as DialogueBox; Game1.currentLocation.lastQuestionKey", tick, AdapterId),
            ["tent_sleep_prompt_context"] = Field(ReadTentSleepPromptContext(menu), "Game1.activeClickableMenu as DialogueBox; Game1.currentLocation.lastQuestionKey", tick, AdapterId),
            ["identity"] = Field(ReadIdentity(menu), "Game1.activeClickableMenu.GetType()", tick, AdapterId),
            ["screen_bounds"] = ReadScreenBounds(menu, tick),
            ["public_state"] = ReadPublicState(menu, tick),
            ["shop_stock"] = menu is ShopMenu shopMenu
                ? (object)Field(ReadShopStock(shopMenu), "ShopMenu.itemPriceAndStock", tick, AdapterId)
                : (object)Unavailable(
                    "active_menu_is_not_shop",
                    "Game1.activeClickableMenu as ShopMenu",
                    tick,
                    AdapterId),
            ["sell_context"] = menu is ShopMenu sellMenu
                ? (object)Field(ReadSellContext(sellMenu), "ShopMenu categoriesToSellHere/tagsToSellHere/sellPercentage", tick, AdapterId)
                : (object)Unavailable(
                    "active_menu_is_not_shop",
                    "Game1.activeClickableMenu as ShopMenu",
                    tick,
                    AdapterId),
            ["menu_specific_state"] = menuSpecificState
        }, unavailableFields.ToArray(), "partial");
    }

    private static object ReadActiveMenu(IClickableMenu menu)
    {
        var type = menu.GetType();
        if (menu is DialogueBox dialogueBox)
        {
            return new
            {
                is_open = true,
                type = type.Name,
                full_type = type.FullName,
                last_question_key = Game1.currentLocation?.lastQuestionKey,
                is_sleep_prompt = string.Equals(Game1.currentLocation?.lastQuestionKey, "Sleep", StringComparison.Ordinal),
                is_tent_sleep_prompt = string.Equals(Game1.currentLocation?.lastQuestionKey, "SleepTent", StringComparison.Ordinal),
                event_up = (bool?)Game1.eventUp,
                dialogue_is_question = (bool?)dialogueBox.isQuestion,
                dialogue_response_count = (int?)dialogueBox.responses?.Length,
                dialogue_transitioning = (bool?)dialogueBox.transitioning,
                dialogue_safety_timer = (int?)dialogueBox.safetyTimer,
                dialogue_character_present = (bool?)(dialogueBox.characterDialogue is not null),
                dialogue_speaker_name = dialogueBox.characterDialogue?.speaker?.Name,
                dialogue_typing = (bool?)dialogueBox.showTyping,
                dialogue_finished = (bool?)dialogueBox.dialogueFinished
            };
        }

        return new
        {
            is_open = true,
            type = type.Name,
            full_type = type.FullName,
            last_question_key = Game1.currentLocation?.lastQuestionKey,
            is_sleep_prompt = false,
            is_tent_sleep_prompt = false,
            event_up = (bool?)null,
            dialogue_is_question = (bool?)null,
            dialogue_response_count = (int?)null,
            dialogue_transitioning = (bool?)null,
            dialogue_safety_timer = (int?)null,
            dialogue_character_present = (bool?)null,
            dialogue_speaker_name = (string?)null,
            dialogue_typing = (bool?)null,
            dialogue_finished = (bool?)null
        };
    }

    private static object ReadSleepPromptContext(IClickableMenu? menu)
    {
        var lastQuestionKey = Game1.currentLocation?.lastQuestionKey;
        var promptOpen = menu is DialogueBox && string.Equals(lastQuestionKey, "Sleep", StringComparison.Ordinal);
        return new
        {
            prompt_open = promptOpen,
            active_menu_open = menu is not null,
            active_menu_type = menu?.GetType().Name ?? "none",
            last_question_key = lastQuestionKey,
            expected_question_key = "Sleep",
            can_confirm_sleep = promptOpen,
            confirm_executor_enabled = promptOpen,
            confirm_action_key = "Sleep_Yes",
            block_reason = promptOpen ? (string?)null : "sleep_prompt_not_open"
        };
    }

    private static object ReadTentSleepPromptContext(IClickableMenu? menu)
    {
        var lastQuestionKey = Game1.currentLocation?.lastQuestionKey;
        var promptOpen = menu is DialogueBox && string.Equals(lastQuestionKey, "SleepTent", StringComparison.Ordinal);
        return new
        {
            prompt_open = promptOpen,
            active_menu_open = menu is not null,
            active_menu_type = menu?.GetType().Name ?? "none",
            last_question_key = lastQuestionKey,
            expected_question_key = "SleepTent",
            can_confirm_sleep = promptOpen,
            confirm_executor_enabled = promptOpen,
            confirm_action_key = "SleepTent_Yes",
            block_reason = promptOpen ? (string?)null : "tent_sleep_prompt_not_open"
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
            ShippingMenu shippingMenu =>
                (Field(ReadShippingMenuState(shippingMenu), "ShippingMenu public fields", tick, AdapterId),
                    Array.Empty<string>()),
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
            LevelUpMenu levelUpMenu =>
                (Field(ReadLevelUpMenuState(levelUpMenu), "LevelUpMenu public state and exact private currentSkill/currentLevel/professionsToChoose fields", tick, AdapterId),
                    Array.Empty<string>()),
            LetterViewerMenu letterViewerMenu =>
                (Field(ReadLetterViewerMenuState(letterViewerMenu), "LetterViewerMenu public mail/page/interactable/attachment/quest fields", tick, AdapterId),
                    Array.Empty<string>()),
            MineElevatorMenu mineElevatorMenu =>
                (Field(ReadMineElevatorMenuState(mineElevatorMenu), "MineElevatorMenu public elevators and locked native destination rules", tick, AdapterId),
                    Array.Empty<string>()),
            GeodeMenu geodeMenu =>
                (Field(ReadGeodeMenuState(geodeMenu), "GeodeMenu public held item, geode spot, treasure, animation, mutex and close-readiness fields", tick, AdapterId),
                    Array.Empty<string>()),
            PurchaseAnimalsMenu purchaseAnimalsMenu =>
                (Field(
                    ReadPurchaseAnimalsMenuState(purchaseAnimalsMenu),
                    "PurchaseAnimalsMenu public stock, target location, placement, naming, and animal fields",
                    tick,
                    AdapterId),
                    Array.Empty<string>()),
            AnimalQueryMenu animalQueryMenu =>
                (Field(
                    ReadAnimalQueryMenuState(animalQueryMenu),
                    "AnimalQueryMenu public animal, textbox, management buttons, confirmation, and placement state",
                    tick,
                    AdapterId),
                    Array.Empty<string>()),
            PondQueryMenu pondQueryMenu =>
                (Field(
                    ReadPondQueryMenuState(pondQueryMenu),
                    "PondQueryMenu exact bound FishPond, confirmation state, and public management buttons",
                    tick,
                    AdapterId),
                    Array.Empty<string>()),
            NamingMenu namingMenu =>
                (Field(
                    ReadNamingMenuState(namingMenu),
                    "NamingMenu public textBox/button/callback fields",
                    tick,
                    AdapterId),
                    Array.Empty<string>()),
            _ => (null, Array.Empty<string>())
        };
    }

    private static object ReadPondQueryMenuState(PondQueryMenu menu)
    {
        var pond = typeof(PondQueryMenu)
            .GetField("_pond", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(menu) as FishPond;
        var confirmingEmpty = ReadPrivateBool(menu, "confirmingEmpty") == true;
        return new
        {
            kind = "fish_pond_query",
            bound_pond_available = pond is not null,
            building_tile_x = pond?.tileX.Value,
            building_tile_y = pond?.tileY.Value,
            fish_type_item_id = pond?.fishType.Value ?? string.Empty,
            fish_count = pond?.FishCount,
            netting_style = pond?.nettingStyle.Value,
            confirming_empty = confirmingEmpty,
            global_fade = Game1.globalFade,
            ok_button = ReadButtonBounds(menu.okButton),
            empty_button = ReadButtonBounds(menu.emptyButton),
            change_netting_button = ReadButtonBounds(menu.changeNettingButton),
            yes_button = ReadButtonBounds(menu.yesButton),
            no_button = ReadButtonBounds(menu.noButton)
        };
    }

    private static object? ReadButtonBounds(ClickableTextureComponent? button) =>
        button is null
            ? null
            : new
            {
                available = true,
                id = button.myID,
                x = button.bounds.X,
                y = button.bounds.Y,
                width = button.bounds.Width,
                height = button.bounds.Height
            };

    private static object ReadAnimalQueryMenuState(AnimalQueryMenu menu)
    {
        var animal = menu.animal;
        return new
        {
            kind = "animal_query",
            animal_id = animal.myID.Value,
            animal_name = animal.Name,
            animal_display_name = animal.displayName,
            text_box_value = menu.textBox?.Text ?? string.Empty,
            confirming_sell = menu.confirmingSell,
            moving_animal = menu.movingAnimal,
            global_fade = Game1.globalFade,
            sell_price = animal.getSellPrice(),
            allow_reproduction = animal.allowReproduction.Value,
            allow_reproduction_button_available = menu.allowReproductionButton is not null,
            ok_button_available = menu.okButton is not null,
            sell_button_available = menu.sellButton is not null,
            move_home_button_available = menu.moveHomeButton is not null,
            yes_button_available = menu.yesButton is not null,
            no_button_available = menu.noButton is not null,
            viewed_location_id = Game1.currentLocation?.NameOrUniqueName ?? string.Empty,
            player_physical_location_id = Game1.player.currentLocation?.NameOrUniqueName ?? string.Empty
        };
    }

    private static object ReadPurchaseAnimalsMenuState(PurchaseAnimalsMenu menu)
    {
        return new
        {
            kind = "purchase_animals",
            target_location_id = menu.TargetLocation?.NameOrUniqueName,
            read_only = menu.readOnly,
            on_target_location = menu.onFarm,
            naming_animal = menu.namingAnimal,
            frozen = menu.freeze,
            current_scroll = menu.currentScroll,
            scroll_rows = menu.scrollRows,
            stock = menu.animalsToPurchase.Select((button, index) => new
            {
                index,
                button_id = button.myID,
                animal_type_id = button.hoverText,
                price = button.item?.salePrice() ?? 0,
                required_building_met = button.item is StardewValley.Object item && item.Type is null,
                blocked_description = button.item is StardewValley.Object blockedItem ? blockedItem.Type : null,
                visible = button.visible,
                bounds = new
                {
                    x = button.bounds.X,
                    y = button.bounds.Y,
                    width = button.bounds.Width,
                    height = button.bounds.Height
                }
            }).ToArray(),
            animal_being_purchased = menu.animalBeingPurchased is null
                ? null
                : new
                {
                    animal_id = menu.animalBeingPurchased.myID.Value,
                    animal_type_id = menu.animalBeingPurchased.type.Value,
                    owner_id = menu.animalBeingPurchased.ownerID.Value,
                    name = menu.animalBeingPurchased.Name,
                    required_house_type = menu.animalBeingPurchased.buildingTypeILiveIn.Value
                },
            selected_home = menu.newAnimalHome is null
                ? null
                : new
                {
                    building_type = menu.newAnimalHome.buildingType.Value,
                    building_tile_x = menu.newAnimalHome.tileX.Value,
                    building_tile_y = menu.newAnimalHome.tileY.Value,
                    indoor_location_id = menu.newAnimalHome.GetIndoors()?.NameOrUniqueName
                },
            price_of_animal = menu.priceOfAnimal,
            name_text = menu.textBox?.Text ?? string.Empty,
            native_transaction_contract = "stock_button->global_fade_to_target_location->compatible_nonfull_home->unique_name->AnimalHouse.adoptAnimal->money_deduction->warp_AnimalShop"
        };
    }

    private static object ReadNamingMenuState(
        NamingMenu menu)
    {
        return new
        {
            kind = "naming",
            title = menu.title,
            text = menu.textBox?.Text ?? string.Empty,
            text_box_selected =
                menu.textBox?.Selected ?? false,
            min_length = menu.minLength,
            filter_input = menu.FilterInput,
            done_callback_present =
                menu.doneNaming is not null,
            done_button_present =
                menu.doneNamingButton is not null,
            done_button_bounds =
                menu.doneNamingButton is null
                    ? null
                    : new
                    {
                        x = menu.doneNamingButton.bounds.X,
                        y = menu.doneNamingButton.bounds.Y,
                        width =
                            menu.doneNamingButton.bounds.Width,
                        height =
                            menu.doneNamingButton.bounds.Height
                    },
            native_submit_contract =
                "NamingMenu.receiveLeftClick_doneNamingButton_then_textBoxEnter_then_doneNaming"
        };
    }

    private static object ReadShippingMenuState(ShippingMenu menu)
    {
        var canReceiveInput = menu.CanReceiveInput();
        var okButtonPresent = menu.okButton is not null;
        return new
        {
            kind = "shipping_summary",
            can_receive_input = canReceiveInput,
            current_page = menu.currentPage,
            ok_button_present = okButtonPresent,
            ready_for_native_ok = canReceiveInput && menu.currentPage == -1 && okButtonPresent
        };
    }

    private static object ReadLevelUpMenuState(LevelUpMenu menu)
    {
        var currentSkill = ReadPrivateInt(menu, "currentSkill");
        var currentLevel = ReadPrivateInt(menu, "currentLevel");
        var timerBeforeStart = ReadPrivateInt(menu, "timerBeforeStart");
        var professionIds = ReadPrivateIntList(menu, "professionsToChoose");
        return new
        {
            kind = "level_up",
            information_up = menu.informationUp,
            is_active = menu.isActive,
            is_profession_chooser = menu.isProfessionChooser,
            has_updated_professions = menu.hasUpdatedProfessions,
            can_receive_input = menu.CanReceiveInput(),
            current_skill = currentSkill,
            current_level = currentLevel,
            timer_before_start = timerBeforeStart,
            reflection_fields_complete = currentSkill.HasValue &&
                currentLevel.HasValue &&
                timerBeforeStart.HasValue &&
                professionIds is not null,
            profession_choices = (professionIds ?? Array.Empty<int>())
                .Select(id => new
                {
                    profession_id = id,
                    title = LevelUpMenu.getProfessionTitleFromNumber(id),
                    description_lines = LevelUpMenu.getProfessionDescription(id).ToArray()
                })
                .ToArray()
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

    private static object ReadShopStock(ShopMenu menu)
    {
        var entries = menu.itemPriceAndStock
            .OrderBy(entry => entry.Key.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.DisplayName, StringComparer.Ordinal)
            .Select(entry => new
            {
                item_id = entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId,
                qualified_item_id = entry.Key.QualifiedItemId,
                display_name = entry.Key.DisplayName,
                name = entry.Key.Name,
                stack = entry.Key.Stack,
                quality = entry.Key.Quality,
                is_recipe = entry.Key.IsRecipe,
                price = entry.Value.Price,
                stock = entry.Value.Stock,
                infinite_stock = entry.Value.Stock == ShopMenu.infiniteStock,
                trade_item = entry.Value.TradeItem,
                trade_item_count = entry.Value.TradeItemCount,
                effective_trade_item_count = entry.Value.TradeItem is null ? (int?)null : entry.Value.TradeItemCount ?? 5,
                limited_stock_mode = entry.Value.LimitedStockMode.ToString(),
                synced_key = entry.Value.SyncedKey,
                action_on_purchase_count = entry.Value.ActionsOnPurchase?.Count ?? 0,
                can_buy_item = entry.Key.CanBuyItem(Game1.player),
                total_price_for_one_purchase = entry.Value.Price,
                currency_balance = ReadCurrencyBalance(menu.currency),
                can_afford_one_with_currency = ReadCurrencyBalance(menu.currency) >= entry.Value.Price,
                trade_item_available_count = entry.Value.TradeItem is null ? (int?)null : CountAvailableTradeItem(entry.Value.TradeItem),
                can_afford_one_with_trade_item = entry.Value.TradeItem is null || CountAvailableTradeItem(entry.Value.TradeItem) >= (entry.Value.TradeItemCount ?? 5),
                could_inventory_accept = entry.Key.GetSalableInstance() is Item salableItem && Game1.player.couldInventoryAcceptThisItem(salableItem),
                action_when_purchased_may_discard_or_mutate = entry.Key.IsRecipe || entry.Value.ActionsOnPurchase?.Count > 0 || entry.Key.GetType() != typeof(StardewValley.Object),
                executor_purchase_enabled = PurchaseExecutorBlockReasons(menu, entry.Key, entry.Value).Length == 0,
                executor_block_reasons = PurchaseExecutorBlockReasons(menu, entry.Key, entry.Value)
            })
            .ToArray();
        var anyEnabled = entries.Any(entry => entry.executor_purchase_enabled);

        return new
        {
            kind = "shop_stock",
            shop_id = menu.ShopId,
            shop_data_present = menu.ShopData is not null,
            currency = menu.currency,
            read_only = menu.readOnly,
            safety_timer = menu.safetyTimer,
            held_item_present = menu.heldItem is not null,
            shop_on_purchase_present = menu.onPurchase is not null,
            executor_purchase_enabled = anyEnabled,
            executor_block_reason = anyEnabled ? "" : "no_safe_executor_purchase_candidate",
            entry_count = entries.Length,
            entries
        };
    }

    private static string[] PurchaseExecutorBlockReasons(ShopMenu menu, ISalable item, ItemStockInformation stock)
    {
        var reasons = new List<string>();

        if (menu.readOnly)
        {
            reasons.Add("shop_menu_read_only");
        }

        if (menu.safetyTimer > 0)
        {
            reasons.Add("shop_menu_safety_timer_active");
        }

        if (menu.heldItem is not null)
        {
            reasons.Add("shop_menu_held_item_present");
        }

        if (menu.currency != 0)
        {
            reasons.Add("non_money_currency_purchase_requires_audit");
        }

        if (menu.onPurchase is not null)
        {
            reasons.Add("shop_on_purchase_callback_present");
        }

        if (stock.TradeItem is not null)
        {
            reasons.Add("trade_item_purchase_requires_consumption_audit");
        }

        if (stock.ActionsOnPurchase?.Count > 0)
        {
            reasons.Add("actions_on_purchase_present");
        }

        if (item.IsRecipe)
        {
            reasons.Add("recipe_purchase_discards_item_and_learns_recipe");
        }

        if (item.GetType() != typeof(StardewValley.Object))
        {
            reasons.Add("non_plain_object_purchase_side_effects_unmodeled");
        }

        if (stock.Stock != ShopMenu.infiniteStock && (stock.LimitedStockMode.ToString() != "None" || stock.SyncedKey is not null))
        {
            reasons.Add("synchronized_or_limited_stock_requires_post_state_audit");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static object ReadSellContext(ShopMenu menu)
    {
        return new
        {
            kind = "shop_sell_context",
            shop_id = menu.ShopId,
            currency = menu.currency,
            read_only = menu.readOnly,
            safety_timer = menu.safetyTimer,
            held_item_present = menu.heldItem is not null,
            storage_shop = ReadPrivateBool(menu, "_isStorageShop"),
            sell_percentage = ReadPrivateFloat(menu, "sellPercentage"),
            custom_on_sell_present = menu.onSell is not null,
            can_buyback = menu.CanBuyback(),
            categories_to_sell = menu.categoriesToSellHere.OrderBy(category => category).ToArray(),
            tag_groups_to_sell = menu.tagsToSellHere
                .Select(group => group.OrderBy(tag => tag, StringComparer.Ordinal).ToArray())
                .ToArray(),
            buy_back_item_count = menu.buyBackItems?.Count ?? 0,
            buy_back_resell_tomorrow_count = menu.buyBackItemsToResellTomorrow?.Count ?? 0
        };
    }

    private static bool? ReadPrivateBool(object source, string fieldName)
    {
        return source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source) as bool?;
    }

    private static float? ReadPrivateFloat(object source, string fieldName)
    {
        return source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source) as float?;
    }

    private static int? ReadPrivateInt(object source, string fieldName)
    {
        return source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source) as int?;
    }

    private static int[]? ReadPrivateIntList(object source, string fieldName)
    {
        return source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source) is IEnumerable<int> values
            ? values.ToArray()
            : null;
    }

    private static int ReadCurrencyBalance(int currency)
    {
        return currency switch
        {
            0 => Game1.player.Money,
            1 => Game1.player.festivalScore,
            2 => Game1.player.clubCoins,
            4 => Game1.player.QiGems,
            _ => 0
        };
    }

    private static int CountAvailableTradeItem(string itemId)
    {
        var qualifiedItemId = ItemRegistry.QualifyItemId(itemId) ?? itemId;
        if (qualifiedItemId == "(O)858")
        {
            return Game1.player.QiGems;
        }

        if (qualifiedItemId == "(O)73")
        {
            return Game1.netWorldState.Value.GoldenWalnuts;
        }

        return Game1.player.Items.CountId(qualifiedItemId);
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
            responses = menu.responses?
                .Select((response, index) => new
                {
                    index,
                    response_key = response.responseKey,
                    response_text = response.responseText
                })
                .ToArray() ?? Array.Empty<object>(),
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
