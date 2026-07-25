using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private static readonly FieldInfo? BreakableContainerHealthField = typeof(BreakableContainer)
        .GetField("health", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
    private HarnessConfig config = new();
    private int ticksSeen;
    private bool loadAttempted;
    private HttpListener? executorListener;
    private CancellationTokenSource? executorCancellation;
    private readonly ConcurrentQueue<PendingExecution> pendingExecutions = new();
    private ActiveTileMove? activeTileMove;
    private ActiveSleep? activeSleep;
    private ActiveWait? activeWait;
    private ActiveCatchFish? activeCatchFish;
    private bool catchFishUseToolHeld;
    private Type? smapiInputStateType;
    private MethodInfo? smapiOverrideButtonMethod;
    private int? executorMovementDirection;
    private ActiveMineFishingSetup? activeMineFishingSetup;
    private ActiveMineSetup? activeMineSetup;
    private ActiveQuarrySetup? activeQuarrySetup;
    private ActiveVolcanoSetup? activeVolcanoSetup;
    private ActiveNativeTool? activeNativeTool;
    private ActiveMineStone? activeMineStone;
    private ActiveResourceClump? activeResourceClump;
    private ActiveVolcanoCoolLava? activeVolcanoCoolLava;
    private ActiveVolcanoObstacle? activeVolcanoObstacle;
    private ActiveVolcanoCombat? activeVolcanoCombat;
    private ActiveBreakContainer? activeBreakContainer;
    private ActiveCombatMonster? activeCombatMonster;
    private ActiveShootMonster? activeShootMonster;
    private ActivePlaceBomb? activePlaceBomb;
    private ActiveConsumeFood? activeConsumeFood;
    private ActivePickupDebris? activePickupDebris;
    private ActiveSpawnedObjectPickup? activeSpawnedObjectPickup;
    private ActiveBushHarvest? activeBushHarvest;
    private ActiveCrabPotCollect? activeCrabPotCollect;
    private ActiveAnimalProductHarvest? activeAnimalProductHarvest;
    private ActivePetInteraction? activePetInteraction;
    private ActiveMuseumDonation? activeMuseumDonation;
    private ActiveQuestDropBoxDonation? activeQuestDropBoxDonation;
    private ActiveCommunityCenterDonation? activeCommunityCenterDonation;
    private ActiveJojaDevelopment? activeJojaDevelopment;
    private ActiveFarmhouseUpgrade? activeFarmhouseUpgrade;
    private ActivePanOreSpot? activePanOreSpot;
    private ActiveFishPondService? activeFishPondService;
    private ActiveDescendLadder? activeDescendLadder;
    private ActiveDescendShaft? activeDescendShaft;
    private ActiveExitMine? activeExitMine;
    private bool manualAutoCombatEnabled;
    private bool manualAutoCombatInputHeld;
    private Monster? manualAutoCombatTarget;
    private int manualAutoCombatTargetHealth;
    private int manualAutoCombatAttackCount;
    private int manualAutoCombatHitCount;
    private int? manualAutoCombatRestoreSlotIndex;
    private ActiveShipInventoryToBin? activeShipInventoryToBin;
    private ActiveDialogueAdvance? activeDialogueAdvance;
    private ActiveSkullKeyChestInteraction? activeSkullKeyChestInteraction;
    private ActiveMineRewardChest? activeMineRewardChest;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<HarnessConfig>();
        ApplyEnvironmentOverrides();
        manualAutoCombatEnabled = string.Equals(Environment.GetEnvironmentVariable("STARDEWAI_COMBAT_MANUAL_MOVEMENT"), "1", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(config.SavesPath))
        {
            Monitor.Log("Runtime harness disabled: SavesPath is empty.", LogLevel.Warn);
            return;
        }

        config.SavesPath = Path.GetFullPath(config.SavesPath);
        Directory.CreateDirectory(config.SavesPath);

        SavesFolderPatch.RedirectPath = config.SavesPath;
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.Patch(
            original: AccessTools.Method("StardewValley.Program:GetSavesFolder"),
            postfix: new HarmonyMethod(typeof(SavesFolderPatch), nameof(SavesFolderPatch.Postfix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Slingshot), "updateAimPos"),
            prefix: new HarmonyMethod(typeof(SlingshotAimPatch), nameof(SlingshotAimPatch.Prefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getOldMouseX), Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(BombPlacementCursorPatch), nameof(BombPlacementCursorPatch.GetOldMouseXPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getOldMouseY), Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(BombPlacementCursorPatch), nameof(BombPlacementCursorPatch.GetOldMouseYPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), nameof(Farmer.caughtFish), new[] { typeof(string), typeof(int), typeof(bool), typeof(int) }),
            prefix: new HarmonyMethod(typeof(CrabPotCaughtFishPatch), nameof(CrabPotCaughtFishPatch.Prefix)));
        if (IsVanillaAiHostMode() &&
            string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_SUPPRESS_LOCAL_RENDER"),
                "1",
                StringComparison.Ordinal))
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "Draw", new[] { typeof(GameTime) }),
                prefix: new HarmonyMethod(typeof(HostLocalDrawPatch), nameof(HostLocalDrawPatch.Prefix)));
            Monitor.Log(
                "Suppressing host-local rendering; game updates, original multiplayer sync, and remote farmer rendering remain active.",
                LogLevel.Info);
        }

        Monitor.Log($"Redirected Stardew save folder to {config.SavesPath}", LogLevel.Info);

        if (config.AutoLoad)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        if (config.EnableTrainingExecutor)
        {
            helper.Events.GameLoop.UpdateTicking += OnExecutorUpdateTicking;
            helper.Events.GameLoop.UpdateTicked += OnExecutorUpdateTicked;
            StartTrainingExecutorServer();
        }

        if (IsAiHostRuntimeMode())
        {
            helper.Events.GameLoop.SaveLoaded += OnDedicatedHostSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += OnDedicatedHostVisibilityTicked;
        }

        helper.Events.GameLoop.DayStarted += OnDayStartedForShippingReceipts;
        ReconcileShippingReceipts();
    }

    private void ApplyEnvironmentOverrides()
    {
        var savesPath = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SAVES");
        if (!string.IsNullOrWhiteSpace(savesPath))
        {
            config.SavesPath = savesPath;
        }

        var slotName = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SLOT");
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            config.SlotName = slotName;
        }

        var executorTimeout = Environment.GetEnvironmentVariable("STARDEWAI_EXECUTOR_REQUEST_TIMEOUT_SECONDS");
        if (int.TryParse(executorTimeout, out var timeoutSeconds))
        {
            config.ExecutorRequestTimeoutSeconds = timeoutSeconds;
        }

        var companionActorId = Environment.GetEnvironmentVariable("STARDEWAI_COMPANION_ACTOR_ID");
        if (!string.IsNullOrWhiteSpace(companionActorId))
        {
            config.CompanionActorId = companionActorId;
        }

        var companionFarmerId = Environment.GetEnvironmentVariable("STARDEWAI_COMPANION_FARMER_ID");
        if (!string.IsNullOrWhiteSpace(companionFarmerId))
        {
            config.CompanionFarmerId = companionFarmerId;
        }

        var dedicatedHostActorId = Environment.GetEnvironmentVariable("STARDEWAI_DEDICATED_HOST_ACTOR_ID");
        if (!string.IsNullOrWhiteSpace(dedicatedHostActorId))
        {
            config.DedicatedHostActorId = dedicatedHostActorId;
        }

        var dedicatedHostFarmerId = Environment.GetEnvironmentVariable("STARDEWAI_DEDICATED_HOST_FARMER_ID");
        if (!string.IsNullOrWhiteSpace(dedicatedHostFarmerId))
        {
            config.DedicatedHostFarmerId = dedicatedHostFarmerId;
        }

        var disableMovementTimeouts = Environment.GetEnvironmentVariable("STARDEWAI_DISABLE_MOVEMENT_TIMEOUTS");
        if (bool.TryParse(disableMovementTimeouts, out var disableTimeouts))
        {
            config.DisableMovementTimeouts = disableTimeouts;
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (loadAttempted || Context.IsWorldReady || Game1.gameMode != 0)
        {
            return;
        }

        ticksSeen++;
        if (ticksSeen < config.LoadAfterTicks)
        {
            return;
        }

        loadAttempted = true;
        if (string.IsNullOrWhiteSpace(config.SlotName))
        {
            Monitor.Log("AutoLoad skipped: SlotName is empty.", LogLevel.Warn);
            return;
        }

        var slotPath = Path.Combine(config.SavesPath, config.SlotName);
        if (!Directory.Exists(slotPath))
        {
            Monitor.Log($"AutoLoad skipped: save slot not found at {slotPath}", LogLevel.Error);
            return;
        }

        if (IsVanillaAiHostMode())
        {
            Game1.multiplayerMode = 2;
            if (Game1.options is not null)
            {
                Game1.options.pauseWhenOutOfFocus = false;
            }

            Monitor.Log(
                "Starting vanilla co-op host: multiplayerMode=2 before SaveGame.Load; no dedicated-server lifecycle mod is required.",
                LogLevel.Info);
        }

        Monitor.Log($"Loading isolated test save slot {config.SlotName}", LogLevel.Info);
        SaveGame.Load(config.SlotName);
        Game1.exitActiveMenu();
    }

    private void StartTrainingExecutorServer()
    {
        if (!HttpListener.IsSupported)
        {
            Monitor.Log("Training executor disabled: HttpListener is not supported.", LogLevel.Warn);
            return;
        }

        executorCancellation = new CancellationTokenSource();
        executorListener = new HttpListener();
        var httpPrefixHost = string.Equals(config.ExecutorHost, "0.0.0.0", StringComparison.Ordinal)
            ? "*"
            : config.ExecutorHost;
        executorListener.Prefixes.Add($"http://{httpPrefixHost}:{config.ExecutorPort}/");
        executorListener.Start();
        Monitor.Log($"Training executor listening on http://{config.ExecutorHost}:{config.ExecutorPort}/", LogLevel.Info);
        _ = Task.Run(() => ServeTrainingExecutorAsync(executorCancellation.Token));
    }

    private async Task ServeTrainingExecutorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && executorListener is not null)
        {
            try
            {
                var context = await executorListener.GetContextAsync();
                _ = Task.Run(() => HandleExecutorRequestAsync(context), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Training executor server loop failed: {ex}", LogLevel.Error);
            }
        }
    }

    private async Task HandleExecutorRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "/";
        if (context.Request.HttpMethod == "GET" && path == "/health")
        {
            await WriteJsonAsync(context, 200, new { status = "ok", service = "StardewAI.RuntimeTestHarness.Executor" });
            return;
        }

        if (context.Request.HttpMethod != "POST" || path != "/api/v1/training/execute")
        {
            await WriteJsonAsync(context, 404, new { error = "not_found", path });
            return;
        }

        TrainingExecutionRequest? request;
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            request = JsonSerializer.Deserialize<TrainingExecutionRequest>(await reader.ReadToEndAsync(), JsonOptions);
        }
        catch (JsonException ex)
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_json", detail = ex.Message });
            return;
        }

        if (request is null)
        {
            await WriteJsonAsync(context, 400, new { error = "empty_request" });
            return;
        }

        var pending = new PendingExecution(request);
        pendingExecutions.Enqueue(pending);
        var timeoutSeconds = Math.Clamp(config.ExecutorRequestTimeoutSeconds, 5, 3600);
        var completed = await Task.WhenAny(pending.Completion.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
        if (completed != pending.Completion.Task)
        {
            await WriteJsonAsync(context, 504, new { error = "training_execution_timeout", timeout_seconds = timeoutSeconds });
            return;
        }

        await WriteJsonAsync(context, 200, pending.Completion.Task.Result);
    }

    private void OnExecutorUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        TickTileMove(e);
        TickSleep();
        TickWait();
        TickCatchFish();
        TickMineFishingSetup();
        TickMineSetup();
        TickQuarrySetup();
        TickVolcanoSetup();
        TickNativeTool();
        TickMineStone();
        TickResourceClump();
        TickVolcanoCoolLava();
        TickVolcanoObstacle();
        TickVolcanoCombat();
        TickBreakContainer();
        TickCombatMonster();
        TickManualAutoCombat();
        TickConsumeFood();
        TickPickupDebris();
        TickSpawnedObjectPickup();
        TickBushHarvest();
        TickCrabPotCollect();
        TickShootMonster();
        TickPlaceBomb();
        TickDescendLadder();
        TickDescendShaft();
        TickExitMine();
        TickShipInventoryToBin();
        TickDialogueAdvance();
        TickSkullKeyChestInteraction();
        TickMineRewardChest();
        TickAnimalProductHarvest();
        TickPetInteraction();
        TickMuseumDonation();
        TickQuestDropBoxDonation();
        TickCommunityCenterDonation();
        TickJojaDevelopment();
        TickFarmhouseUpgrade();
        TickPanOreSpot();
        TickFishPondService();

        if (activeTileMove is not null || activeSleep is not null || activeWait is not null || activeCatchFish is not null || activeMineFishingSetup is not null || activeMineSetup is not null || activeQuarrySetup is not null || activeVolcanoSetup is not null || activeNativeTool is not null || activeMineStone is not null || activeResourceClump is not null || activeVolcanoCoolLava is not null || activeVolcanoObstacle is not null || activeVolcanoCombat is not null || activeBreakContainer is not null || activeCombatMonster is not null || activeShootMonster is not null || activePlaceBomb is not null || activeConsumeFood is not null || activePickupDebris is not null || activeSpawnedObjectPickup is not null || activeBushHarvest is not null || activeCrabPotCollect is not null || activeAnimalProductHarvest is not null || activePetInteraction is not null || activeMuseumDonation is not null || activeQuestDropBoxDonation is not null || activeCommunityCenterDonation is not null || activeJojaDevelopment is not null || activeFarmhouseUpgrade is not null || activePanOreSpot is not null || activeFishPondService is not null || activeDescendLadder is not null || activeDescendShaft is not null || activeExitMine is not null || activeShipInventoryToBin is not null || activeDialogueAdvance is not null || activeSkullKeyChestInteraction is not null || activeMineRewardChest is not null)
        {
            return;
        }

        if (!pendingExecutions.TryDequeue(out var pending))
        {
            return;
        }

        try
        {
            if (pending.Request.OptionId == "debug.visible_walk" ||
                pending.Request.OptionId == "executor.move_to_tile" ||
                pending.Request.OptionId == "executor.traverse_connector")
            {
                StartTileMove(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.face_direction")
            {
                pending.Completion.SetResult(ExecuteFaceDirection(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.wait_ticks")
            {
                StartWait(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.clear_obstacle")
            {
                pending.Completion.SetResult(ExecuteClearObstacle(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.mine_stone")
            {
                StartMineStone(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.break_resource_clump" ||
                pending.Request.OptionId == "executor.break_farm_resource_clump" ||
                pending.Request.OptionId == "executor.break_current_location_resource_clump")
            {
                StartResourceClump(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.cool_volcano_lava")
            {
                StartVolcanoCoolLava(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.break_volcano_stone" ||
                pending.Request.OptionId == "executor.break_volcano_container")
            {
                StartVolcanoObstacle(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.combat_volcano_monster")
            {
                StartVolcanoCombat(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.break_container")
            {
                StartBreakContainer(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.combat_monster")
            {
                StartCombatMonster(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.shoot_monster")
            {
                StartShootMonster(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.place_bomb")
            {
                StartPlaceBomb(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.consume_food")
            {
                StartConsumeFood(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.descend_ladder")
            {
                StartDescendLadder(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.descend_shaft")
            {
                StartDescendShaft(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.exit_mine")
            {
                StartExitMine(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.advance_time_to")
            {
                pending.Completion.SetResult(ExecuteAdvanceTimeTo(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_watering_target")
            {
                pending.Completion.SetResult(ExecuteSetupWateringTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_till_soil_target")
            {
                pending.Completion.SetResult(ExecuteSetupTillSoilTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fish_frenzy")
            {
                pending.Completion.SetResult(ExecuteSetupFishFrenzy(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fish_pond")
            {
                pending.Completion.SetResult(ExecuteSetupFishPond(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_mine_fishing_floor")
            {
                StartSetupMineFishingFloor(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fish_pond_output" ||
                pending.Request.OptionId == "debug.setup_fish_pond_request")
            {
                pending.Completion.SetResult(ExecuteSetupFishPondService(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_mining_floor")
            {
                StartSetupMiningFloor(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_skull_cavern_shaft")
            {
                StartSetupSkullCavernShaft(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_quarry_mine")
            {
                StartSetupQuarryMine(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_volcano_floor")
            {
                StartSetupVolcanoFloor(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_breakable_container")
            {
                pending.Completion.SetResult(ExecuteSetupBreakableContainer(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_mining_combat_fixture")
            {
                pending.Completion.SetResult(ExecuteSetupMiningCombatFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_clear_obstacle")
            {
                pending.Completion.SetResult(ExecuteSetupClearObstacle(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_plant_seed_target")
            {
                pending.Completion.SetResult(ExecuteSetupPlantSeedTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_harvest_crop_target")
            {
                pending.Completion.SetResult(ExecuteSetupHarvestCropTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_giant_crop_target")
            {
                pending.Completion.SetResult(ExecuteSetupGiantCropTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_debris_target")
            {
                pending.Completion.SetResult(ExecuteSetupDebrisTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_machine_output_target")
            {
                pending.Completion.SetResult(ExecuteSetupMachineOutputTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_material_inventory_graph")
            {
                pending.Completion.SetResult(ExecuteSetupMaterialInventoryGraph(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_machine_input_target")
            {
                pending.Completion.SetResult(ExecuteSetupMachineInputTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_shipping_target")
            {
                pending.Completion.SetResult(ExecuteSetupShippingTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.select_safe_item_slot")
            {
                pending.Completion.SetResult(ExecuteSelectSafeItemSlot(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.close_menu")
            {
                StartDialogueAdvance(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.interact")
            {
                if (string.Equals(pending.Request.InteractionKind, "overlay_object", StringComparison.Ordinal) &&
                    string.Equals(pending.Request.ExpectedActionType, "SkullKeyChest", StringComparison.Ordinal))
                {
                    StartSkullKeyChestInteraction(pending);
                }
                else
                {
                    pending.Completion.SetResult(ExecuteInteract(pending.Request));
                }
                return;
            }

            if (pending.Request.OptionId == "executor.buy_shop_item")
            {
                pending.Completion.SetResult(ExecuteBuyShopItem(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.sell_shop_item")
            {
                pending.Completion.SetResult(ExecuteSellShopItem(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.plant_seed")
            {
                pending.Completion.SetResult(ExecutePlantSeed(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.till_soil")
            {
                StartTillSoil(pending);
                return;
            }

            if (pending.Request.OptionId == "farm.maintain_crops")
            {
                StartMaintainCrops(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_crop")
            {
                pending.Completion.SetResult(ExecuteHarvestCrop(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_giant_crop")
            {
                pending.Completion.SetResult(ExecuteHarvestGiantCrop(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.pickup_debris")
            {
                StartPickupDebris(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.collect_spawned_object")
            {
                StartSpawnedObjectPickup(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_ginger")
            {
                StartHarvestGinger(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_bush")
            {
                StartBushHarvest(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.claim_mine_reward_chest")
            {
                StartMineRewardChest(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.collect_crab_pot")
            {
                StartCrabPotCollect(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_crab_pot_target")
            {
                pending.Completion.SetResult(ExecuteSetupCrabPotTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_animal_product_target")
            {
                pending.Completion.SetResult(ExecuteSetupAnimalProductTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.collect_animal_product")
            {
                StartAnimalProductHarvest(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.pet_interact")
            {
                StartPetInteraction(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.fill_pet_bowl")
            {
                StartFillPetBowl(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.donate_museum_item")
            {
                StartMuseumDonation(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.donate_community_center_item")
            {
                StartCommunityCenterDonation(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.purchase_joja_membership" ||
                pending.Request.OptionId == "executor.purchase_joja_project")
            {
                StartJojaDevelopment(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.purchase_farmhouse_upgrade")
            {
                StartFarmhouseUpgrade(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_pan_ore_spot")
            {
                pending.Completion.SetResult(ExecuteSetupPanOreSpot(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.pan_ore_spot")
            {
                StartPanOreSpot(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.collect_fish_pond_output" ||
                pending.Request.OptionId == "executor.complete_fish_pond_request")
            {
                StartFishPondService(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.collect_machine_output")
            {
                pending.Completion.SetResult(ExecuteCollectMachineOutput(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.load_machine_input")
            {
                pending.Completion.SetResult(ExecuteLoadMachineInput(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.craft_machine_item")
            {
                pending.Completion.SetResult(ExecuteCraftMachineItem(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.read_book")
            {
                pending.Completion.SetResult(ExecuteReadBook(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.catch_fish")
            {
                StartCatchFish(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.choose_dialogue_response")
            {
                pending.Completion.SetResult(ExecuteChooseDialogueResponse(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.social_interact")
            {
                pending.Completion.SetResult(ExecuteSocialInteract(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.quest_npc_interact")
            {
                pending.Completion.SetResult(ExecuteQuestNpcInteract(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.quest_drop_box_donate")
            {
                StartQuestDropBoxDonation(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.sleep")
            {
                StartSleep(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.ship_inventory_item_to_bin")
            {
                StartShipInventoryItemToBin(pending);
                return;
            }

            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                "unsupported_executor_option",
                "executor.option_id=" + pending.Request.OptionId,
                "executor.dispatch=not_started",
                "runtime_executor_option_not_supported:" + pending.Request.OptionId));
        }
        catch (Exception ex)
        {
            StopAllMovement();
            activeCatchFish = null;
            activeCrabPotCollect = null;
            activeBushHarvest = null;
            activeMineRewardChest = null;
            activeAnimalProductHarvest = null;
            activePetInteraction = null;
            activeMuseumDonation = null;
            activeQuestDropBoxDonation = null;
            activeCommunityCenterDonation = null;
            activeJojaDevelopment = null;
            activeFarmhouseUpgrade = null;
            activePanOreSpot = null;
            activeFishPondService = null;
            CrabPotCaughtFishPatch.Reset();
            ReleaseSmapiLeftButtonOverride();
            var activeDialogue = activeDialogueAdvance;
            if (activeDialogue is not null)
            {
                activeDialogueAdvance = null;
                activeDialogue.Pending.Completion.SetResult(BlockedWithPrimitive(
                    activeDialogue.Pending.Request, "close_menu",
                    "menus.active_menu.is_open=false",
                    CloseMenuObservedEffect(),
                    "dialogue_advance_exception:" + ex.GetType().Name));
            }
            Monitor.Log($"Training execution failed: {ex}", LogLevel.Error);
            pending.Completion.SetResult(Blocked(pending.Request, "execution_exception:" + ex.GetType().Name));
        }
    }

    private void OnExecutorUpdateTicking(object? sender, UpdateTickingEventArgs e)
    {
        try
        {
            if (!ApplyExecutorMovementInput(out var movementInputReason))
            {
                executorMovementDirection = null;
                Monitor.Log($"Movement input dispatch failed: {movementInputReason}.", LogLevel.Error);
            }
            if (activeCatchFish is not null && !ApplyCatchFishUseToolInput(activeCatchFish, out var castInputReason))
            {
                CompleteBlockedCatchFish(activeCatchFish, castInputReason);
                return;
            }

            if (activeCatchFish is not null && Game1.activeClickableMenu is BobberBar bar)
            {
                activeCatchFish.SawBobberBar = true;
                if (!SetBobberBarControl(activeCatchFish, bar, out var bobberInputReason))
                {
                    CompleteBlockedCatchFish(activeCatchFish, bobberInputReason);
                }
            }

            if (activeShipInventoryToBin is not null)
            {
                ApplyShipPhaseInput(activeShipInventoryToBin);
            }

            if (activeSleep is not null && activeSleep.Stage == SleepStage.WaitForPostSleepStable)
            {
                var menu = Game1.activeClickableMenu;
                if (menu is ShippingMenu)
                {
                    ApplyShipSummaryInput(activeSleep);
                }
            }
        }
        catch (Exception ex)
        {
            var activeFish = activeCatchFish;
            if (activeFish is not null)
            {
                Monitor.Log($"Fishing input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                CompleteBlockedCatchFish(activeFish, "catch_fish_input_dispatch_exception:" + ex.GetType().Name);
                return;
            }
            if (activeShipInventoryToBin is not null)
            {
                Monitor.Log($"Ship input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                CleanupAndBlock(activeShipInventoryToBin, "ship_input_dispatch_exception:" + ex.GetType().Name);
            }
            var sleepObj = activeSleep;
            if (sleepObj is not null && sleepObj.Stage == SleepStage.WaitForPostSleepStable)
            {
                Monitor.Log($"Ship summary input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                ReleaseSmapiLeftButtonOverride();
                CompleteBlockedSleep(sleepObj, "shipping_summary_input_dispatch_exception:" + ex.GetType().Name);
            }
        }
    }

    private static TrainingExecutionResult Blocked(TrainingExecutionRequest request, params string[] reasons)
    {
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = false,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = reasons
        };
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };


}

internal static class SavesFolderPatch
{
    public static string? RedirectPath { get; set; }

    public static void Postfix(ref string __result)
    {
        if (string.IsNullOrWhiteSpace(RedirectPath))
        {
            return;
        }

        Directory.CreateDirectory(RedirectPath);
        __result = RedirectPath;
    }

}

internal static class HostLocalDrawPatch
{
    public static bool Prefix()
    {
        return false;
    }
}

internal static class SlingshotAimPatch
{
    public static Slingshot? ActiveSlingshot { get; set; }

    public static Point AimWorldPixel { get; set; }

    public static bool Prefix(Slingshot __instance)
    {
        if (!ReferenceEquals(__instance, ActiveSlingshot))
        {
            return true;
        }
        __instance.aimPos.Set(AimWorldPixel.X, AimWorldPixel.Y);
        return false;
    }

    public static void Clear(Slingshot slingshot)
    {
        if (ReferenceEquals(slingshot, ActiveSlingshot))
        {
            ActiveSlingshot = null;
            AimWorldPixel = Point.Zero;
        }
    }
}

internal static class BombPlacementCursorPatch
{
    public static bool Active { get; set; }

    public static Point ScreenPixel { get; set; }

    public static bool GetOldMouseXPrefix(ref int __result)
    {
        if (!Active)
        {
            return true;
        }
        __result = ScreenPixel.X;
        return false;
    }

    public static bool GetOldMouseYPrefix(ref int __result)
    {
        if (!Active)
        {
            return true;
        }
        __result = ScreenPixel.Y;
        return false;
    }

    public static void Clear()
    {
        Active = false;
        ScreenPixel = Point.Zero;
    }
}
