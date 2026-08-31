using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System.Globalization;

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
                special_state = item is null
                    ? null
                    : FarmReadAdapter.ReadItemSpecialState(item),
                special_item = item?.specialItem,
                context_tags = item?.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                donate_color_context = ReadDonateColorContext(item),
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
            ["club_coins"] = Field(Context.IsWorldReady ? (int?)player?.clubCoins : null, "Game1.player.clubCoins", tick),
            ["has_club_card"] = Field(Context.IsWorldReady ? (bool?)player?.hasClubCard : null, "Game1.player.hasClubCard", tick),
            ["total_money_earned"] = Field(Context.IsWorldReady ? (uint?)player?.totalMoneyEarned : null, "Game1.player.totalMoneyEarned", tick),
            ["geodes_cracked"] = Field(Context.IsWorldReady ? (uint?)Game1.stats.GeodesCracked : null, "Game1.stats.GeodesCracked", tick),
            ["health"] = Field(player?.health, "Game1.player.health", tick),
            ["max_health"] = Field(player?.maxHealth, "Game1.player.maxHealth", tick),
            ["energy"] = Field(player?.Stamina, "Game1.player.Stamina", tick),
            ["max_energy"] = Field(player?.MaxStamina, "Game1.player.MaxStamina", tick),
            ["level"] = Field(Context.IsWorldReady ? (int?)player?.Level : null, "Game1.player.Level", tick),
            ["skills_detail"] = Field(ReadSkillsDetail(player), "Game1.player.GetUnmodifiedSkillLevel/GetSkillLevel/experiencePoints/professions/newLevels; Farmer.getBaseExperienceForLevel; LevelUpMenu profession text", tick),
            ["book_candidates"] = Field(ReadBookCandidates(player), "Game1.player.Items and Object.performUseAction/readBook native branches", tick),
            ["secret_note_candidates"] = Field(ReadSecretNoteCandidates(player), "DataLoader.SecretNotes; Utility.GetUnseenSecretNotes; Object.performUseAction exact (O)79/(O)842 branches; native LetterViewerMenu and quest side effects", tick),
            ["firework_placement"] = Field(ReadFireworkPlacementContext(player), "Game1.player.Items exact base (O)893/(O)894/(O)895; current loaded map native placement legality; exact temporarySprites target collision; Object.placementAction firework branch without advancing Game1.random during reads", tick),
            ["horse_flute"] = Field(ReadHorseFluteContext(player), "Game1.player.Items exact base (O)911; Object.performUseAction horse-flute branch; Utility.GetHorseWarpRestrictionsForFarmer; Utility.findHorseForPlayer; current online mounts and exact adjacent no-op", tick),
            ["monster_musk"] = Field(ReadMonsterMuskContext(player), "Game1.player.Items exact base (O)879; DataLoader.Buffs id 24; BuffManager.AppliedBuffs; Object.performUseAction/MonsterMusk callback; MineShaft and VolcanoDungeon buff-24 spawn consumers", tick),
            ["rain_totem"] = Field(ReadRainTotemContext(player), "Game1.player.Items exact base (O)681; current LocationContextData AllowRainTotem/RainTotemAffectsContext; Utility.isFestivalDay tomorrow guard; default or current-context WeatherForTomorrow; Object.performUseAction/rainTotem native branch", tick),
            ["return_scepter"] = Field(ReadReturnScepterContext(player), "Game1.player.Items exact base (T)ReturnScepter Wand; Farmer.homeLocation; Utility.getHomeOfFarmer; FarmHouse.getFrontDoorSpot; Wand.DoFunction delayed native Farm warp", tick),
            ["treasure_totem"] = Field(ReadTreasureTotemContext(player), "Game1.player.Items exact base (O)TreasureTotem; Object.performUseAction/treasureTotem; Game1.netWorldState.TreasureTotemsUsed; current loaded location exact rounded-distance ring placement, occupancy, front-layer, bush and diggable/winter-grass gates", tick),
            ["warp_totem"] = Field(ReadWarpTotemContext(player), "Game1.player.Items exact base (O)261/(O)688/(O)689/(O)690/(O)886; Data/Objects; Object.performUseAction/totemWarp/totemWarpForReal; Farm WarpTotemEntry; active and passive festival destination routing", tick),
            ["grange_display"] = Field(ReadGrangeDisplayContext(player), "current festival_fall16 Event; FarmerTeam.grangeDisplay/grangeMutex; live small Object inventory; Object.sellToStorePrice(-1); Event.IsItemMayorShorts/judgeGrange/checkAction/onGrangeChange; current festival Buildings tile sheet indexes 349/350/351", tick),
            ["fair_fishing_game"] = Field(ReadFairFishingGameContext(player), "current festival_fall16 Event and Buildings tile sheet indexes 503/504; Data/Shops Festival_StardewValleyFair_StarTokens through ShopBuilder.GetShopStock; FishingGame public score state and reflected private timers; native Event.checkAction/answerDialogue/FishingGame scoring and reward branches", tick),
            ["fair_slingshot_game"] = Field(ReadFairSlingshotGameContext(player), "current festival_fall16 Event and Buildings tile sheet indexes 501/502; Data/Shops Festival_StardewValleyFair_StarTokens through ShopBuilder.GetShopStock; TargetGame live target list, public score state and reflected private target/timer fields; native Event.checkAction/answerDialogue/TargetGame scoring and reward branches", tick),
            ["fair_strength_game"] = Field(ReadFairStrengthGameContext(player), "current festival_fall16 Event and Buildings tile sheet index 540 with required player tile x=29; Data/Shops Festival_StardewValleyFair_StarTokens through ShopBuilder.GetShopStock; StrengthGame reflected power/speed/timer/result fields; native Event.checkAction/StrengthGame/FarmerSprite reward branches", tick),
            ["fair_wheel_spin"] = Field(ReadFairWheelSpinContext(player), "current festival_fall16 Event and Buildings tile sheet indexes 308/309; Data/Shops Festival_StardewValleyFair_StarTokens through ShopBuilder.GetShopStock; Event wheelBet/NumberSelectionMenu; WheelSpinGame live rotation/velocity/timer/wager/result fields and native stochastic score settlement", tick),
            ["calico_jack"] = Field(ReadCalicoJackContext(player), "Game1.player.clubCoins/hasClubCard; Utility.doesItemExistAnywhere (BC)126; Data/Shops Casino; current Club map ClubCards/BlackJack action tiles; Club.timesPlayedCalicoJack seed inputs; CalicoJack public cards and reflected native timers/result fields", tick),
            ["slots"] = Field(ReadSlotsContext(player), "Game1.player clubCoins/DailyLuck/LuckLevel/hasClubCard; shared Casino Rarecrow demand and Data/Shops Casino; live ClubSlots map action tiles; Club.timesPlayedSlots; Slots public spin state and reflected reels/buttons", tick),
            ["crane_game"] = Field(ReadCraneGameContext(player), "MovieTheater Buildings CraneGame action and occupancy tiles; live Data/Movies CranePrizes and clear-group rules; locked base 1.6.15 prize pools; active CraneGame GameLogic, Claw and Prize physical state; player money and inventory capacity", tick),
            ["movie_theater"] = Field(ReadMovieTheaterContext(player), "locked 1.6.15 MovieTheater movie rotation, ticket inventory, weekly player/NPC gates, native invitation list, loaded vanilla NPC invite legality, current movie and concession reactions, deterministic daily concession offers, Town entrance, lobby concession and screening-door action tiles, movie mutex and native screening event state", tick),
            ["story_event"] = Field(ReadStoryEventContext(player), "Game1.CurrentEvent/Event public command stream and state; DialogueBox live responses; Game1.currentMinigame; native Event.Update/tryEventCommand and DialogueBox input boundaries", tick),
            ["darts_game"] = Field(ReadDartsGameContext(player), "IslandSouthEastCave.isPirateNight and live Buildings DartsGame action; FarmerTeam limited Darts nut drops; native Darts 301 score, dart count, charge, aim, flight and result state", tick),
            ["prairie_king"] = Field(ReadPrairieKingContext(player), "Saloon Arcade_Prairie action; Farmer JOTPKProgress and completion stats; AbigailGame locked 1.6.15 start, save and native phase-one completion side effects", tick),
            ["luck_context"] = Field(ReadLuckContext(player), "Game1.player.team.sharedDailyLuck, Farmer.DailyLuck/LuckLevel, Farmer.hasSpecialCharm, BuffManager.AppliedBuffs", tick),
            ["trinket_loadout"] = Field(ReadTrinketLoadout(player), "Game1.player.stats.Get(\"trinketSlots\") unlock flag, Farmer.MaximumTrinkets, Game1.player.trinketItems and exact Trinket special state", tick),
            ["has_skull_key"] = Field(Context.IsWorldReady ? (bool?)player?.hasSkullKey : null, "Game1.player.hasSkullKey", tick),
            ["deepest_mine_level"] = Field(Context.IsWorldReady ? (int?)player?.deepestMineLevel : null, "Game1.player.deepestMineLevel", tick),
            ["current_mine_level"] = Field(Context.IsWorldReady ? (int?)Game1.CurrentMineLevel : null, "Game1.CurrentMineLevel", tick),
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
            ["active_object_qualified_id"] = Field(
                player is null ? null : player.ActiveObject?.QualifiedItemId ?? string.Empty,
                "Game1.player.ActiveObject?.QualifiedItemId; empty string means no active object",
                tick),
            ["temporary_sleep"] = Field(ReadTemporarySleepContext(player), "Farmer.isInBed/sleptInTemporaryBed/lastSleepLocation/lastSleepPoint/timeWentToBed; Game1.displayFarmer", tick),
            ["object_trap_recovery"] = Field(ReadObjectTrapRecovery(player), "Game1.player.TilePoint; current location four cardinal objects and Object.checkForAction performToolAction(null) anti-trap branch", tick),
            ["multiplayer_runtime"] = Field(ReadMultiplayerRuntime(player), "Context.IsMultiplayer/IsMainPlayer; Game1.IsServer/displayFarmer/getAllFarmers/getOnlineFarmers; Farmer hidden/ignoreCollisions/location/position", tick),
            ["multiplayer_wallet"] = Field(ReadMultiplayerWallet(player), "FarmerTeam.useSeparateWallets/money/individualMoney; Farmer.changeWalletTypeTonight; ManorHouse LedgerBook/readLedgerBook/answerDialogueAction/sendMoney/SeparateWallets/MergeWallets; Game1 newDay player.wallets barrier", tick),
            ["prize_ticket_reward"] = Field(ReadPrizeTicketReward(player), "Game1.player Items PrizeTicket and stats specialOrderPrizeTickets; Game1.stats ticketPrizesClaimed; PrizeTicketMenu.getPrizeItem exact local RNG; Town SpecialOrdersPrizeTickets and ManorHouse PrizeMachine native action tiles; PrizeTicketMenu inventory-else-debris settlement", tick),
            ["mastery_claim"] = Field(ReadMasteryClaim(player), "Farmer five raw skill levels; Game1.stats MasteryExp/masteryLevelsSpent; Farmer.stats mastery_0..mastery_4/trinketSlots; MasteryTrackerMenu thresholds and exact rewards; MasteryCave five native skill action tiles", tick),
            ["emote"] = Field(ReadPlayerEmote(player), "Farmer.EMOTES exact 22-entry catalog and animation frames; raw/effective favorites without GetEmoteFavorites mutation; performedEmotes; Farmer.CanEmote; EmoteMenu/ChatCommands/native doEmoteEvent route", tick),
            ["multiplayer_chat"] = Field(ReadMultiplayerChat(player), "Game1.chatBox/ChatTextBox width and messages; Game1.otherFarmers native order and active display-name tokens; ChatBox textBoxEnter/sendPrivateMessage; Multiplayer message type 10 and AllPlayers routing", tick),
            ["bobber_selection"] = Field(ReadBobberSelection(player), "FishShop Buildings Action=Bobbers; ChooseFromIconsMenu(bobbers); Farmer.bobberStyle/usingRandomizedBobber/fishCaught.Count; FishingRod.NUM_BOBBER_STYLES/getBobberStyle and Sonar Bobber tackle override", tick),
            ["jukebox_selection"] = Field(ReadJukeboxSelection(player), "Saloon Buildings Action=Jukebox; Utility.GetJukeboxTracks exact ordered unlock catalog; Data/JukeboxTracks; Farmer.songsHeard; ChooseFromListMenu native input; Game1 default/requested/current music receipt and green-rain guard", tick),
            ["customization"] = Field(ReadPlayerCustomization(player), "WizardHouseBasement Buildings Action=WizardShrine and WizardShrine_Yes 500g native CharacterCustomization(Source.Wizard); Farmer appearance and exact ColorPicker HSV sliders; DesertFestival Back TouchAction=DesertMakeover, stylist/daily/inventory gates, Data/MakeoverOutfits and deterministic day-save RNG", tick),
            ["geode_processing"] = Field(ReadGeodeProcessing(player), "locked base 1.6.15 Utility.IsGeode/getTreasureFromGeode and GeodeMenu; Blacksmith Buildings Action=Blacksmith; Clint presence and tool-upgrade interception; exact inventory, money, stats, Golden Coconut mutex state and pure local-RNG output replay", tick),
            ["machine_crafting"] = Field(ReadMachineCraftingContext(player), "Game1.player.craftingRecipes, CraftingRecipe.craftingRecipes/recipeList/ItemMatchesForCrafting, ItemRegistry, Object.GetMachineData", tick),
            ["storage_crafting"] = Field(ReadStorageCraftingContext(player), "Game1.player.craftingRecipes, CraftingRecipe.craftingRecipes/recipeList/ItemMatchesForCrafting, ItemRegistry, native Chest placement classification", tick),
            ["quest_crafting"] = Field(ReadQuestCraftingContext(player), "Game1.player.questLog CraftingQuest.ItemId, Game1.player.craftingRecipes, CraftingRecipe.craftingRecipes/recipeList/ItemMatchesForCrafting, ItemRegistry", tick),
            ["cooking"] = Field(ReadCookingContext(player), "Game1.player.cookingRecipes/recipesCooked; CraftingRecipe.cookingRecipes/recipeList/ItemMatchesForCrafting; GameLocation.ActivateKitchen/GetFridge; fridge and mini-fridge mutexes; Cookout Kit Torch.checkForAction; native CraftingPage seasoning, quest, recipe-count and achievement callbacks", tick),
            ["cookout_kit_placement"] = Field(ReadCookoutKitPlacementContext(player), "Game1.player.Items (O)926; shared persistent placement topology and legal-tile compression; Object.placementAction (O)926 Cookout Kit branch creates Torch (BC)278 with Fragility 1 and destroyOvernight true", tick),
            ["tent_placement"] = Field(ReadTentPlacementContext(player), "Game1.player.Items exact base (O)TentKit; Object.placementAction directional 3x2 isAreaClear, tomorrow festival gates and TerrainFeatures.Tent handoff", tick),
            ["crab_pot_placement"] = Field(ReadCrabPotPlacementContext(player), "Game1.player.Items (O)710; CrabPot.IsValidCrabPotLocationTile; GameLocation fish-area and crab-pot habitat data; Data/Fish trap rows; Object.placementAction (O)710 creates owned CrabPot", tick),
            ["fence_placement"] = Field(ReadFencePlacementContext(player), "Game1.player.Items Object.IsFenceItem; live Data/Fences; shared persistent placement topology; Object.placementAction creates Fence", tick),
            ["flooring_placement"] = Field(ReadFlooringPlacementContext(player), "Game1.player.Items Object.IsFloorPathItem; live Data/FloorsAndPaths; shared persistent placement topology; Object.placementAction creates TerrainFeatures.Flooring", tick),
            ["grass_placement"] = Field(ReadGrassPlacementContext(player), "Game1.player.Items exact base (O)297 or (O)BlueGrassStarter; current loaded map native placement legality; Object.placementAction creates TerrainFeatures.Grass type 1 or 7 with four initial weeds", tick),
            ["furniture_placement"] = Field(ReadFurniturePlacementContext(player), "Game1.player.Items Furniture; live Data/Furniture; Furniture rotation, wall correction, footprint and table-held endpoint; Utility.tryToPlaceItem native placement", tick),
            ["sign_placement"] = Field(ReadSignPlacementContext(player), "Game1.player.Items exact base Object signs; live Data/BigCraftables sign_item and TextSign branches; current loaded map native placement legality", tick),
            ["forge"] = Field(ReadForgeContext(player), "live inventory and equipped rings; loaded Action=Forge tiles and (BC)MiniForge objects; ForgeMenu.IsValidCraft/GetForgeCost/CanFitCraftedItem/IsValidUnforge; Tool.CanForge/Forge, Ring.CanCombine/Combine, native random result domains and unforge refunds", tick),
            ["quest_building_construction"] = Field(ReadQuestBuildingConstructionContext(player), "Game1.player.questLog HaveBuildingQuest.buildingType; Game1.buildingData; Game1.player.team.constructedBuildings; Farm.buildings; GameLocation.isBuildable; Building placement footprint and door access", tick),
            ["building_construction_catalog"] = Field(ReadBuildingConstructionCatalog(player), "Game1.buildingData; GameStateQuery.CheckConditions; Game1.locations IsBuildableLocation; native building placement predicates; Robin Carpenter and WizardBook service actions", tick),
            ["building_skin_catalog"] = Field(ReadBuildingSkinCatalog(player), "Game1.locations IsBuildableLocation; Building.GetData/CanBePainted/CanBeReskinned; GameStateQuery.CheckConditions; CarpenterMenu.HasPermissionsToPaint and native Robin Carpenter action", tick),
            ["building_paint_catalog"] = Field(ReadBuildingPaintCatalog(player), "Game1.locations IsBuildableLocation; Building.CanBePainted/GetPaintDataKey/netBuildingPaintColor; Data/PaintData; native Robin Carpenter and BuildingPaintMenu", tick),
            ["machine_placement"] = Field(ReadMachinePlacementContext(player), "Utility.ForEachLocation(includeInteriors:true, includeGenerated:false); Utility.isPlacementForbiddenHere; Object.canBePlacedHere with static and current collision masks; Object.placementAction runtime recheck contract", tick),
            ["storage_placement"] = Field(ReadStoragePlacementContext(player), "Game1.player.Items as Chest; Utility.ForEachLocation(includeInteriors:true, includeGenerated:false); Utility.isPlacementForbiddenHere; Chest.canBePlacedHere with transient actors excluded", tick),
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

    private static object ReadTemporarySleepContext(Farmer? player)
    {
        return new
        {
            is_applicable = player is not null,
            is_in_bed = player?.isInBed.Value,
            slept_in_temporary_bed = player?.sleptInTemporaryBed.Value,
            last_sleep_location = player?.lastSleepLocation.Value,
            last_sleep_point_x = player is null ? (int?)null : player.lastSleepPoint.Value.X,
            last_sleep_point_y = player is null ? (int?)null : player.lastSleepPoint.Value.Y,
            time_went_to_bed = player?.timeWentToBed.Value,
            display_farmer = Context.IsWorldReady ? Game1.displayFarmer : (bool?)null,
            current_location_can_wake_here = player?.currentLocation?.CanWakeUpHere(player)
        };
    }

    private static object ReadMultiplayerRuntime(Farmer? localPlayer)
    {
        if (!Context.IsWorldReady)
        {
            return new
            {
                context_is_multiplayer = false,
                context_is_main_player = false,
                game_is_server = false,
                original_server_active = false,
                server_enabled = false,
                pause_when_out_of_focus = (bool?)null,
                game_display_farmer = false,
                local_player_id = (string?)null,
                online_farmer_count = 0,
                farmers = Array.Empty<object>()
            };
        }

        var onlineIds = Game1.getOnlineFarmers()
            .Select(farmer => farmer.UniqueMultiplayerID)
            .ToHashSet();
        var farmers = Game1.getAllFarmers()
            .Select(farmer => new
            {
                player_id = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                name = farmer.Name,
                display_name = farmer.displayName,
                is_local_player = farmer.IsLocalPlayer,
                is_main_player = farmer.IsMainPlayer,
                is_online = onlineIds.Contains(farmer.UniqueMultiplayerID),
                is_other_farmer = Game1.otherFarmers.ContainsKey(farmer.UniqueMultiplayerID),
                location_id = farmer.currentLocation?.NameOrUniqueName,
                tile_x = farmer.TilePoint.X,
                tile_y = farmer.TilePoint.Y,
                pixel_x = farmer.Position.X,
                pixel_y = farmer.Position.Y,
                facing_direction = farmer.FacingDirection,
                hidden = farmer.hidden.Value,
                ignore_collisions = farmer.ignoreCollisions,
                can_move = farmer.CanMove,
                using_tool = farmer.UsingTool
            })
            .OrderByDescending(farmer => farmer.is_main_player)
            .ThenBy(farmer => farmer.player_id, StringComparer.Ordinal)
            .ToArray();

        return new
        {
            context_is_multiplayer = Context.IsMultiplayer,
            context_is_main_player = Context.IsMainPlayer,
            game_is_server = Game1.IsServer,
            original_server_active = Game1.server is not null,
            server_enabled = Game1.options?.enableServer == true,
            pause_when_out_of_focus = Game1.options?.pauseWhenOutOfFocus,
            game_display_farmer = Game1.displayFarmer,
            local_player_id = localPlayer?.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
            online_farmer_count = onlineIds.Count,
            farmers
        };
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
