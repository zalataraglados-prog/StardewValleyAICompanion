using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
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
    private bool executorIdlePauseApplied;
    private HttpListener? executorListener;
    private CancellationTokenSource? executorCancellation;
    private readonly ConcurrentQueue<PendingExecution> pendingExecutions = new();
    private ActiveTileMove? activeTileMove;
    private ActiveSleep? activeSleep;
    private ActiveWait? activeWait;
    private ActiveCatchFish? activeCatchFish;
    private ActiveJunimoKart? activeJunimoKart;
    private ActiveHorseFlute? activeHorseFlute;
    private ActiveMonsterMusk? activeMonsterMusk;
    private ActiveRainTotem? activeRainTotem;
    private ActiveReturnScepter? activeReturnScepter;
    private ActiveWarpTotem? activeWarpTotem;
    private ActiveGrangeDisplay? activeGrangeDisplay;
    private ActiveFairFishingGame? activeFairFishingGame;
    private ActiveFairSlingshotGame? activeFairSlingshotGame;
    private ActiveFairStrengthGame? activeFairStrengthGame;
    private ActiveFairWheelSpin? activeFairWheelSpin;
    private ActiveCalicoJack? activeCalicoJack;
    private ActiveCraneGame? activeCraneGame;
    private ActiveDartsGame? activeDartsGame;
    private bool catchFishUseToolHeld;
    private Type? smapiInputStateType;
    private MethodInfo? smapiOverrideButtonMethod;
    private readonly MovementLease executorMovementLease = new();
    private readonly ExecutorDiagnosticRingBuffer executorDiagnosticFrames =
        new(600);
    private long executorInputTick;
    private long lastExecutorDiagnosticDumpTick = -600;
    private ActiveMineFishingSetup? activeMineFishingSetup;
    private ActiveMineSetup? activeMineSetup;
    private ActiveQuarrySetup? activeQuarrySetup;
    private ActiveVolcanoSetup? activeVolcanoSetup;
    private ActiveNativeTool? activeNativeTool;
    private ActiveAdjacentTileAction? activeAdjacentTileAction;
    private ActiveClearObstacle? activeClearObstacle;
    private ActiveMineStone? activeMineStone;
    private ActiveResourceClump? activeResourceClump;
    private ActiveVolcanoCoolLava? activeVolcanoCoolLava;
    private ActiveVolcanoObstacle? activeVolcanoObstacle;
    private ActiveVolcanoCombat? activeVolcanoCombat;
    private ActiveBreakContainer? activeBreakContainer;
    private ActiveCombatMonster? activeCombatMonster;
    private ActiveShootMonster? activeShootMonster;
    private ActivePlaceBomb? activePlaceBomb;
    private ActivePlaceStaircase? activePlaceStaircase;
    private ActiveConsumeFood? activeConsumeFood;
    private ActivePickupDebris? activePickupDebris;
    private ActiveSpawnedObjectPickup? activeSpawnedObjectPickup;
    private ActiveBushHarvest? activeBushHarvest;
    private ActiveFruitTreeHarvest? activeFruitTreeHarvest;
    private ActiveWildTreeProductHarvest? activeWildTreeProductHarvest;
    private ActiveGarbageCanRummage? activeGarbageCanRummage;
    private ActiveCrabPotCollect? activeCrabPotCollect;
    private ActiveAnimalProductHarvest? activeAnimalProductHarvest;
    private ActiveAnimalManagement? activeAnimalManagement;
    private ActivePetInteraction? activePetInteraction;
    private ActiveMuseumDonation? activeMuseumDonation;
    private ActiveFieldOfficeDonation? activeFieldOfficeDonation;
    private ActiveFieldOfficeSurvey? activeFieldOfficeSurvey;
    private ActiveQuestDropBoxDonation? activeQuestDropBoxDonation;
    private ActiveCommunityCenterDonation? activeCommunityCenterDonation;
    private ActiveJojaDevelopment? activeJojaDevelopment;
    private ActiveFarmhouseUpgrade? activeFarmhouseUpgrade;
    private ActiveHomeRenovation? activeHomeRenovation;
    private ActiveBuildingConstruction? activeBuildingConstruction;
    private ActiveBuildingAppearanceChange? activeBuildingAppearanceChange;
    private ActiveAnimalPurchase? activeAnimalPurchase;
    private ActivePanOreSpot? activePanOreSpot;
    private ActiveFishPondService? activeFishPondService;
    private ActiveFishPondManagement? activeFishPondManagement;
    private ActiveDescendLadder? activeDescendLadder;
    private ActiveDescendShaft? activeDescendShaft;
    private ActiveExitMine? activeExitMine;
    private bool manualAutoCombatEnabled;
    private int? deferredCombatRestoreSlotIndex;
    private ActiveEmergencyCombatFood? activeEmergencyCombatFood;
    private ActiveShipInventoryToBin? activeShipInventoryToBin;
    private ActiveMaterialTransfer? activeMaterialTransfer;
    private ActiveWorkbenchCraft? activeWorkbenchCraft;
    private ActiveCooking? activeCooking;
    private ActiveForge? activeForge;
    private ActiveDialogueAdvance? activeDialogueAdvance;
    private ActiveMenuClose? activeMenuClose;
    private ActiveMailProcessing? activeMailProcessing;
    private ActiveShippingSummaryClose? activeShippingSummaryClose;
    private ActiveSkullKeyChestInteraction? activeSkullKeyChestInteraction;
    private ActiveMineRewardChest? activeMineRewardChest;
    private ActivePotOfGoldClaim? activePotOfGoldClaim;
    private ActiveDwarfKingStatueChoice? activeDwarfKingStatueChoice;
    private ActiveStatueBlessingClaim? activeStatueBlessingClaim;
    private readonly NativeObjectInteractionDomainState nativeObjectInteractions = new();
    private ActiveSpecialOrderBoardOpen? activeSpecialOrderBoardOpen;
    private ActiveQuestRewardClaim? activeQuestRewardClaim;

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
            prefix: new HarmonyMethod(typeof(PlacementCursorPatch), nameof(PlacementCursorPatch.GetOldMouseXPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getOldMouseY), Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(PlacementCursorPatch), nameof(PlacementCursorPatch.GetOldMouseYPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getOldMouseX), new[] { typeof(bool) }),
            prefix: new HarmonyMethod(typeof(PlacementCursorPatch), nameof(PlacementCursorPatch.GetOldMouseXPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.getOldMouseY), new[] { typeof(bool) }),
            prefix: new HarmonyMethod(typeof(PlacementCursorPatch), nameof(PlacementCursorPatch.GetOldMouseYPrefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.didPlayerJustRightClick), new[] { typeof(bool) }),
            prefix: new HarmonyMethod(typeof(NativeRightClickEdgePatch), nameof(NativeRightClickEdgePatch.Prefix)));
        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), nameof(Farmer.caughtFish), new[] { typeof(string), typeof(int), typeof(bool), typeof(int) }),
            prefix: new HarmonyMethod(typeof(CrabPotCaughtFishPatch), nameof(CrabPotCaughtFishPatch.Prefix)));
        harmony.Patch(
            original: AccessTools.Method(
                typeof(ResourceClump),
                nameof(ResourceClump.performToolAction),
                new[] { typeof(Tool), typeof(int), typeof(Vector2) }),
            prefix: new HarmonyMethod(
                typeof(ResourceClumpToolTracePatch),
                nameof(ResourceClumpToolTracePatch.Prefix)),
            postfix: new HarmonyMethod(
                typeof(ResourceClumpToolTracePatch),
                nameof(ResourceClumpToolTracePatch.Postfix)));
        if (IsVanillaAiHostMode() &&
            string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_SUPPRESS_LOCAL_RENDER"),
                "1",
                StringComparison.Ordinal))
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), "Draw", new[] { typeof(GameTime) }),
                prefix: new HarmonyMethod(typeof(HostLocalDrawPatch), nameof(HostLocalDrawPatch.Prefix)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SaveGameMenu), nameof(SaveGameMenu.update)),
                prefix: new HarmonyMethod(
                    typeof(HeadlessSaveGameMenuLifecyclePatch),
                    nameof(HeadlessSaveGameMenuLifecyclePatch.Prefix)));
            Monitor.Log(
                "Suppressing host-local rendering; game updates, native save lifecycle, original multiplayer sync, and remote farmer rendering remain active.",
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
        helper.Events.GameLoop.DayStarted += OnDayStartedForPetBowlReceipts;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoadedForPetBowlReceipts;
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

        var autoLoad = Environment.GetEnvironmentVariable(
            "STARDEWAI_TEST_AUTO_LOAD");
        if (bool.TryParse(autoLoad, out var autoLoadEnabled))
        {
            config.AutoLoad = autoLoadEnabled;
        }
        else if (!string.IsNullOrWhiteSpace(slotName))
        {
            config.AutoLoad = true;
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

        var freezeClockWhileIdle = Environment.GetEnvironmentVariable(
            "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE");
        if (bool.TryParse(
                freezeClockWhileIdle,
                out var freezeClockWhileIdleEnabled))
        {
            config.FreezeClockWhileExecutorIdle =
                freezeClockWhileIdleEnabled;
        }

        var junimoKartStrategy = Environment.GetEnvironmentVariable(
            "STARDEWAI_JUNIMO_KART_EXECUTION_STRATEGY");
        if (!string.IsNullOrWhiteSpace(junimoKartStrategy))
        {
            config.JunimoKartExecutionStrategy = junimoKartStrategy;
        }

        var junimoKartDuration = Environment.GetEnvironmentVariable(
            "STARDEWAI_JUNIMO_KART_EQUIVALENT_DURATION_TICKS");
        if (int.TryParse(junimoKartDuration, out var equivalentDurationTicks) && equivalentDurationTicks > 0)
        {
            config.JunimoKartEquivalentDurationTicks = equivalentDurationTicks;
        }

        var junimoKartAcceleration = Environment.GetEnvironmentVariable(
            "STARDEWAI_JUNIMO_KART_EQUIVALENT_ACCELERATION");
        if (int.TryParse(junimoKartAcceleration, out var equivalentAcceleration) && equivalentAcceleration > 0)
        {
            config.JunimoKartEquivalentAcceleration = equivalentAcceleration;
        }

        var diagnosticOutputPath = Environment.GetEnvironmentVariable(
            "STARDEWAI_EXECUTOR_DIAGNOSTIC_OUTPUT");
        if (!string.IsNullOrWhiteSpace(diagnosticOutputPath))
        {
            config.DiagnosticOutputPath = diagnosticOutputPath;
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
        if (!TryNormalizeNativeObjectPayload(request, out var payloadReason))
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_native_object_payload", detail = payloadReason });
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
        TickJunimoKart();
        TickHorseFlute();
        TickMonsterMusk();
        TickRainTotem();
        TickReturnScepter();
        TickWarpTotem();
        TickSetupGrangeDisplayFixture();
        TickGrangeDisplay();
        TickFairFishingGame();
        TickFairSlingshotGame();
        TickFairStrengthGame();
        TickFairWheelSpin();
        TickCalicoJack();
        TickCraneGame();
        TickDartsGame();
        TickMineFishingSetup();
        TickMineSetup();
        TickQuarrySetup();
        TickVolcanoSetup();
        TickDeferredCombatRestore();
        TickNativeTool();
        TickAdjacentTileAction();
        TickClearObstacle();
        TickMineStone();
        TickResourceClump();
        TickVolcanoCoolLava();
        TickVolcanoObstacle();
        TickVolcanoCombat();
        TickVolcanoAutoCombat();
        TickBreakContainer();
        TickEmergencyCombatFood();
        TickCombatMonster();
        TickManualAutoCombat();
        TickConsumeFood();
        TickPickupDebris();
        TickSpawnedObjectPickup();
        TickBushHarvest();
        TickFruitTreeHarvest();
        TickWildTreeProductHarvest();
        TickGarbageCanRummage();
        TickCrabPotCollect();
        TickShootMonster();
        TickPlaceBomb();
        TickPlaceStaircase();
        TickDescendLadder();
        TickDescendShaft();
        TickExitMine();
        TickShipInventoryToBin();
        TickMaterialTransferSafely();
        TickWorkbenchCraftSafely();
        TickCookingSafely();
        TickForgeSafely();
        TickDialogueAdvance();
        TickMenuClose();
        TickMailProcessing();
        TickMineElevatorSelection();
        TickShippingSummaryClose();
        TickSkullKeyChestInteraction();
        TickMineRewardChest();
        TickPotOfGoldClaim();
        TickDwarfKingStatuePowerChoice();
        TickStatueBlessingClaim();
        TickNativeObjectInteractionDomain();
        TickSpecialOrderBoardOpen();
        TickQuestRewardClaimSafely();
        TickAnimalPurchase();
        TickAnimalProductHarvest();
        TickAnimalManagement();
        TickPetInteraction();
        TickMuseumDonation();
        TickFieldOfficeDonation();
        TickFieldOfficeSurvey();
        TickQuestDropBoxDonation();
        TickCommunityCenterDonation();
        TickJojaDevelopment();
        TickFarmhouseUpgrade();
        TickHomeRenovation();
        TickBuildingConstruction();
        TickBuildingAppearanceChange();
        TickPanOreSpot();
        TickFishPondService();
        TickFishPondManagement();
        CaptureExecutorDiagnosticFrame("update_ticked");

        if (HasActiveExecutorOperation())
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
                StartClearObstacle(pending);
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

            if (pending.Request.OptionId == "executor.place_staircase")
            {
                StartPlaceStaircase(pending);
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

            if (pending.Request.OptionId == "debug.setup_animal_purchase")
            {
                pending.Completion.SetResult(ExecuteSetupAnimalPurchase(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_animal_management")
            {
                pending.Completion.SetResult(ExecuteSetupAnimalManagement(pending.Request));
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

            if (pending.Request.OptionId == "debug.setup_fish_pond_management")
            {
                pending.Completion.SetResult(ExecuteSetupFishPondManagement(pending.Request));
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
            if (pending.Request.OptionId == "debug.setup_quest_monster_drop_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupQuestMonsterDropFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_collection_task_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupCollectionTaskFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_quest_terminal_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupQuestTerminalFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_daily_quest_acceptance")
            {
                pending.Completion.SetResult(
                    ExecuteSetupDailyQuestAcceptanceFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_special_order_acceptance")
            {
                pending.Completion.SetResult(
                    ExecuteSetupSpecialOrderAcceptanceFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_quest_reward")
            {
                pending.Completion.SetResult(ExecuteSetupQuestRewardFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_level_up_profession")
            {
                pending.Completion.SetResult(ExecuteSetupLevelUpProfessionFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_mail")
            {
                pending.Completion.SetResult(ExecuteSetupMailFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_junimo_kart_quest")
            {
                pending.Completion.SetResult(
                    ExecuteSetupJunimoKartQuest(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_green_rain_resource_clump")
            {
                pending.Completion.SetResult(
                    ExecuteSetupGreenRainResourceClumpFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_farm_resource_clump")
            {
                pending.Completion.SetResult(
                    ExecuteSetupFarmResourceClumpFixture(pending.Request));
                return;
            }
            if (pending.Request.OptionId ==
                "debug.setup_mining_resource_clump")
            {
                pending.Completion.SetResult(
                    ExecuteSetupMiningResourceClumpFixture(
                        pending.Request));
                return;
            }
            if (pending.Request.OptionId == "debug.setup_forage_source_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupForageSourceFixture(pending.Request));
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

            if (pending.Request.OptionId == "debug.setup_fertilizer_target")
            {
                pending.Completion.SetResult(ExecuteSetupFertilizerTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_tree_treatment_target")
            {
                pending.Completion.SetResult(ExecuteSetupTreeTreatmentTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_cookout_kit_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupCookoutKitPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_tent_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupTentPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_crab_pot_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupCrabPotPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fence_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupFencePlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_flooring_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupFlooringPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_grass_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupGrassPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_firework_target")
            {
                pending.Completion.SetResult(ExecuteSetupFireworkTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_horse_flute")
            {
                pending.Completion.SetResult(ExecuteSetupHorseFluteFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_monster_musk")
            {
                pending.Completion.SetResult(ExecuteSetupMonsterMuskFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_rain_totem")
            {
                pending.Completion.SetResult(ExecuteSetupRainTotemFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_return_scepter")
            {
                pending.Completion.SetResult(ExecuteSetupReturnScepterFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_treasure_totem")
            {
                pending.Completion.SetResult(ExecuteSetupTreasureTotemFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_warp_totem")
            {
                pending.Completion.SetResult(ExecuteSetupWarpTotemFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_grange_display")
            {
                StartSetupGrangeDisplayFixture(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fair_fishing_game")
            {
                StartSetupFairFishingGameFixture(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fair_slingshot_game")
            {
                StartSetupFairSlingshotGameFixture(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fair_strength_game")
            {
                StartSetupFairStrengthGameFixture(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_fair_wheel_spin")
            {
                StartSetupFairWheelSpinFixture(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_calico_jack")
            {
                pending.Completion.SetResult(ExecuteSetupCalicoJackFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_crane_game")
            {
                pending.Completion.SetResult(ExecuteSetupCraneGameFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_darts_game")
            {
                pending.Completion.SetResult(ExecuteSetupDartsGameFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_furniture_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupFurniturePlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_sign_placement_target")
            {
                pending.Completion.SetResult(ExecuteSetupSignPlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_sign_display_item_target")
            {
                pending.Completion.SetResult(ExecuteSetupSignDisplayItemTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_text_sign_edit_target")
            {
                pending.Completion.SetResult(ExecuteSetupTextSignEditTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_crab_pot_bait_target")
            {
                pending.Completion.SetResult(ExecuteSetupCrabPotBaitTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_indoor_pot_fertilizer_target")
            {
                pending.Completion.SetResult(ExecuteSetupFertilizerTarget(pending.Request, useIndoorPot: true));
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

            if (pending.Request.OptionId == "debug.setup_book_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupBookFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_secret_note_fixture")
            {
                pending.Completion.SetResult(
                    ExecuteSetupSecretNoteFixture(pending.Request));
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

            if (pending.Request.OptionId == "debug.setup_material_transfer_target")
            {
                pending.Completion.SetResult(ExecuteSetupMaterialTransferTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_single_gift_item")
            {
                pending.Completion.SetResult(ExecuteSetupSingleGiftItem(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_partnership_fixture")
            {
                pending.Completion.SetResult(ExecuteSetupPartnershipFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.prepare_partnership_sleep")
            {
                pending.Completion.SetResult(ExecutePreparePartnershipSleep(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_joja_development")
            {
                pending.Completion.SetResult(ExecuteSetupJojaDevelopmentFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.prepare_joja_settlement_sleep")
            {
                pending.Completion.SetResult(ExecutePrepareJojaSettlementSleep(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_machine_input_target")
            {
                pending.Completion.SetResult(ExecuteSetupMachineInputTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_incubator_hatch_naming")
            {
                pending.Completion.SetResult(
                    ExecuteSetupIncubatorHatchNaming(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.prepare_incubator_sleep")
            {
                pending.Completion.SetResult(
                    ExecutePrepareIncubatorSleep(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.enter_ready_incubator_house")
            {
                pending.Completion.SetResult(
                    ExecuteEnterReadyIncubatorHouse(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_idle_machine_target")
            {
                pending.Completion.SetResult(
                    ExecuteSetupIdleMachineTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_machine_placement_target")
            {
                pending.Completion.SetResult(
                    ExecuteSetupMachinePlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_storage_placement_target")
            {
                pending.Completion.SetResult(
                    ExecuteSetupStoragePlacementTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_storage_crafting_target")
            {
                pending.Completion.SetResult(
                    ExecuteSetupStorageCraftingTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_machine_lifecycle_target")
            {
                pending.Completion.SetResult(
                    ExecuteSetupMachineLifecycleTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_shipping_target")
            {
                pending.Completion.SetResult(ExecuteSetupShippingTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_sale_target")
            {
                pending.Completion.SetResult(ExecuteSetupSaleTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.select_safe_item_slot")
            {
                pending.Completion.SetResult(ExecuteSelectSafeItemSlot(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.close_menu")
            {
                if (Game1.activeClickableMenu is LetterViewerMenu letterViewerMenu)
                {
                    StartMailProcessing(pending, letterViewerMenu);
                }
                else if (Game1.activeClickableMenu is MineElevatorMenu)
                {
                    StartMineElevatorSelection(pending);
                }
                else
                {
                    StartDialogueAdvance(pending);
                }
                return;
            }

            if (pending.Request.OptionId == "executor.interact")
            {
                if (IsSpecialOrderBoardActionType(pending.Request.ExpectedActionType))
                {
                    StartSpecialOrderBoardOpen(pending);
                }
                else if (string.Equals(pending.Request.InteractionKind, "overlay_object", StringComparison.Ordinal) &&
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
                StartAdjacentTileAction(pending, "plant_seed");
                return;
            }

            if (pending.Request.OptionId == "executor.till_soil")
            {
                StartTillSoil(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.water_crop")
            {
                if (!pending.Request.TargetTileX.HasValue || !pending.Request.TargetTileY.HasValue)
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "water_crop", "current_location.crops[target].needs_watering=false", "target_tile=missing", "target_tile_required"));
                }
                else
                {
                    StartWaterCrop(pending, new Point(pending.Request.TargetTileX.Value, pending.Request.TargetTileY.Value));
                }
                return;
            }

            if (pending.Request.OptionId == "executor.accept_daily_quest")
            {
                pending.Completion.SetResult(
                    ExecuteAcceptDailyQuest(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.accept_special_order")
            {
                pending.Completion.SetResult(ExecuteAcceptSpecialOrder(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.claim_quest_reward")
            {
                StartQuestRewardClaim(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.apply_fertilizer")
            {
                StartAdjacentTileAction(pending, "apply_fertilizer");
                return;
            }

            if (pending.Request.OptionId == "executor.apply_tree_treatment")
            {
                StartAdjacentTileAction(pending, "apply_tree_treatment");
                return;
            }

            if (pending.Request.OptionId == "executor.place_cookout_kit")
            {
                StartAdjacentTileAction(pending, "place_cookout_kit");
                return;
            }

            if (pending.Request.OptionId == "executor.place_tent")
            {
                StartAdjacentTileAction(pending, "place_tent");
                return;
            }

            if (pending.Request.OptionId == "executor.place_crab_pot")
            {
                StartAdjacentTileAction(pending, "place_crab_pot");
                return;
            }

            if (pending.Request.OptionId == "executor.place_fence")
            {
                StartAdjacentTileAction(pending, "place_fence");
                return;
            }

            if (pending.Request.OptionId == "executor.place_flooring")
            {
                StartAdjacentTileAction(pending, "place_flooring");
                return;
            }

            if (pending.Request.OptionId == "executor.plant_grass")
            {
                StartAdjacentTileAction(pending, "plant_grass");
                return;
            }

            if (pending.Request.OptionId == "executor.use_firework")
            {
                StartAdjacentTileAction(pending, "use_firework");
                return;
            }

            if (pending.Request.OptionId == "executor.use_horse_flute")
            {
                StartUseHorseFlute(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.use_monster_musk")
            {
                StartUseMonsterMusk(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.use_rain_totem")
            {
                StartUseRainTotem(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.use_return_scepter")
            {
                StartUseReturnScepter(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.use_treasure_totem")
            {
                ExecuteUseTreasureTotem(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.use_warp_totem")
            {
                StartUseWarpTotem(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.manage_grange_display")
            {
                StartGrangeDisplay(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_fair_fishing_game")
            {
                StartFairFishingGame(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_fair_slingshot_game")
            {
                StartFairSlingshotGame(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_fair_strength_game")
            {
                StartFairStrengthGame(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.spin_fair_wheel")
            {
                StartFairWheelSpin(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_calico_jack")
            {
                StartCalicoJack(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_crane_game")
            {
                StartCraneGame(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_darts")
            {
                StartDartsGame(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.place_furniture")
            {
                if (pending.Request.FurnitureCanFreePlace == true)
                {
                    pending.Completion.SetResult(ExecutePlaceFurniture(pending.Request));
                }
                else
                {
                    StartAdjacentTileAction(pending, "place_furniture");
                }
                return;
            }

            if (pending.Request.OptionId == "executor.place_sign")
            {
                StartAdjacentTileAction(pending, "place_sign");
                return;
            }

            if (pending.Request.OptionId == "executor.set_sign_display_item")
            {
                StartAdjacentTileAction(pending, "set_sign_display_item");
                return;
            }

            if (pending.Request.OptionId == "executor.edit_text_sign")
            {
                StartAdjacentTileAction(pending, "edit_text_sign");
                return;
            }

            if (pending.Request.OptionId == "executor.load_crab_pot_bait")
            {
                StartAdjacentTileAction(pending, "load_crab_pot_bait");
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_crop")
            {
                StartAdjacentTileAction(pending, "harvest_crop");
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_giant_crop")
            {
                StartHarvestGiantCrop(pending);
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

            if (pending.Request.OptionId == "debug.setup_pet_care_target")
            {
                pending.Completion.SetResult(ExecuteSetupPetCareTarget(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_museum_donation")
            {
                pending.Completion.SetResult(ExecuteSetupMuseumDonationFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_community_center_donation")
            {
                pending.Completion.SetResult(ExecuteSetupCommunityCenterDonationFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.prepare_pet_bowl_sleep")
            {
                pending.Completion.SetResult(ExecutePreparePetBowlSleep(pending.Request));
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

            if (pending.Request.OptionId == "executor.construct_building")
            {
                StartBuildingConstruction(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.change_building_skin")
            {
                StartBuildingAppearanceChange(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_farmhouse_upgrade")
            {
                pending.Completion.SetResult(ExecuteSetupFarmhouseUpgradeFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_field_office_donation")
            {
                pending.Completion.SetResult(ExecuteSetupFieldOfficeDonationFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_field_office_survey")
            {
                pending.Completion.SetResult(ExecuteSetupFieldOfficeSurveyFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.field_office_survey_day_update")
            {
                pending.Completion.SetResult(ExecuteFieldOfficeSurveyDayUpdate(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_home_renovation")
            {
                pending.Completion.SetResult(ExecuteSetupHomeRenovationFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_building_skin")
            {
                pending.Completion.SetResult(ExecuteSetupBuildingSkinFixture(pending.Request));
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

            if (pending.Request.OptionId == "executor.name_hatched_animal")
            {
                pending.Completion.SetResult(
                    ExecuteNameHatchedAnimal(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.craft_machine_item" ||
                pending.Request.OptionId == "executor.craft_storage_item" ||
                pending.Request.OptionId == "executor.craft_quest_item")
            {
                if (string.Equals(
                        pending.Request.CraftingSource,
                        "native_workbench_crafting_menu",
                        StringComparison.Ordinal))
                {
                    StartWorkbenchCraft(pending);
                    return;
                }
                pending.Completion.SetResult(ExecuteCraftMachineItem(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.cook_recipe")
            {
                StartCooking(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_pot_of_gold")
            {
                pending.Completion.SetResult(ExecuteSetupPotOfGoldFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "rewards.claim_pot_of_gold")
            {
                StartPotOfGoldClaim(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_dwarf_king_statue")
            {
                pending.Completion.SetResult(ExecuteSetupDwarfKingStatueFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "mining.choose_dwarf_statue_power")
            {
                StartDwarfKingStatuePowerChoice(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_statue_blessing")
            {
                pending.Completion.SetResult(ExecuteSetupStatueBlessingFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "rewards.claim_statue_blessing")
            {
                StartStatueBlessingClaim(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_house_plant_rotation")
            {
                pending.Completion.SetResult(ExecuteSetupHousePlantFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "world.rotate_house_plant")
            {
                StartHousePlantRotation(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_singing_stone")
            {
                pending.Completion.SetResult(ExecuteSetupSingingStoneFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "world.play_singing_stone")
            {
                StartSingingStone(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.renovate_home")
            {
                StartHomeRenovation(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.donate_field_office_piece")
            {
                StartFieldOfficeDonation(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.answer_field_office_survey")
            {
                StartFieldOfficeSurvey(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.answer_field_office_survey_wrong")
            {
                StartFieldOfficeSurvey(pending, intentionallyWrong: true);
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_fruit_tree")
            {
                StartFruitTreeHarvest(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.harvest_tree_product")
            {
                StartWildTreeProductHarvest(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.rummage_garbage")
            {
                StartGarbageCanRummage(pending);
                return;
            }

            if (pending.Request.OptionId == "fishing.manage_fish_pond")
            {
                StartFishPondManagement(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_flute_block")
            {
                pending.Completion.SetResult(ExecuteSetupFluteBlockFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "world.tune_flute_block")
            {
                StartFluteBlockTuning(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_drum_block")
            {
                pending.Completion.SetResult(ExecuteSetupDrumBlockFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "world.tune_drum_block")
            {
                StartDrumBlockTuning(pending);
                return;
            }

            if (pending.Request.OptionId == "farming.read_farm_computer_report")
            {
                StartFarmComputerReport(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_farm_computer")
            {
                pending.Completion.SetResult(ExecuteSetupFarmComputerFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_mini_obelisk")
            {
                pending.Completion.SetResult(ExecuteSetupMiniObeliskFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "movement.use_mini_obelisk")
            {
                StartMiniObeliskUse(pending);
                return;
            }

            if (pending.Request.OptionId == "farming.collect_slime_ball")
            {
                StartSlimeBallCollection(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_slime_ball")
            {
                pending.Completion.SetResult(ExecuteSetupSlimeBallFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_feed_hopper")
            {
                pending.Completion.SetResult(ExecuteSetupFeedHopperFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_auto_grabber")
            {
                pending.Completion.SetResult(ExecuteSetupAutoGrabberFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "animals.withdraw_feed_hopper_hay")
            {
                StartFeedHopperWithdrawal(pending);
                return;
            }

            if (pending.Request.OptionId == "animals.collect_auto_grabber_contents")
            {
                StartAutoGrabberCollection(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.forge_item")
            {
                StartForge(pending);
                return;
            }

            if (pending.Request.OptionId == "debug.setup_cooking_fixture")
            {
                pending.Completion.SetResult(ExecuteSetupCookingFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "debug.setup_forge_fixture")
            {
                pending.Completion.SetResult(ExecuteSetupForgeFixture(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.place_machine")
            {
                pending.Completion.SetResult(
                    ExecutePlaceMachine(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.remove_machine")
            {
                pending.Completion.SetResult(
                    ExecuteRemoveMachine(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.place_storage")
            {
                pending.Completion.SetResult(
                    ExecutePlaceStorage(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.read_book")
            {
                pending.Completion.SetResult(ExecuteReadBook(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.read_secret_note")
            {
                pending.Completion.SetResult(ExecuteReadSecretNote(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.catch_fish")
            {
                StartCatchFish(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.play_junimo_kart")
            {
                StartJunimoKart(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.choose_dialogue_response")
            {
                pending.Completion.SetResult(ExecuteChooseDialogueResponse(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.choose_animal_purchase_response")
            {
                pending.Completion.SetResult(ExecuteChooseAnimalPurchaseResponse(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.purchase_animal")
            {
                StartAnimalPurchase(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.manage_animal")
            {
                StartAnimalManagement(pending);
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

            if (pending.Request.OptionId == "recovery.sleep_in_tent")
            {
                StartTentSleep(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.ship_inventory_item_to_bin")
            {
                StartShipInventoryItemToBin(pending);
                return;
            }

            if (pending.Request.OptionId == "executor.transfer_material")
            {
                StartMaterialTransfer(pending);
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
            activeJunimoKart = null;
            activeCrabPotCollect = null;
            activeBushHarvest = null;
            activeFruitTreeHarvest = null;
            activeWildTreeProductHarvest = null;
            activeGarbageCanRummage = null;
            activeMineRewardChest = null;
            activePotOfGoldClaim = null;
            activeDwarfKingStatueChoice = null;
            activeStatueBlessingClaim = null;
            ResetNativeObjectInteractionDomain();
            activeAnimalProductHarvest = null;
            activeAnimalManagement = null;
            activePetInteraction = null;
            activeMuseumDonation = null;
            activeFieldOfficeDonation = null;
            activeFieldOfficeSurvey = null;
            activeQuestDropBoxDonation = null;
            activeCommunityCenterDonation = null;
            activeJojaDevelopment = null;
            activeFarmhouseUpgrade = null;
            activeHomeRenovation = null;
            activeBuildingConstruction = null;
            activeBuildingAppearanceChange = null;
            activeAnimalPurchase = null;
            activePanOreSpot = null;
            activeFishPondService = null;
            activeFishPondManagement = null;
            activeMaterialTransfer = null;
            activeWorkbenchCraft = null;
            activeCooking = null;
            activeForge = null;
            activeSpecialOrderBoardOpen = null;
            activeQuestRewardClaim = null;
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
            var activeMail = activeMailProcessing;
            if (activeMail is not null)
            {
                activeMailProcessing = null;
                activeMail.Pending.Completion.SetResult(MailBlocked(
                    activeMail.Pending.Request,
                    "mail_processing_outer_exception:" + ex.GetType().Name));
            }
            Monitor.Log($"Training execution failed: {ex}", LogLevel.Error);
            pending.Completion.SetResult(Blocked(pending.Request, "execution_exception:" + ex.GetType().Name));
        }
    }

    private void OnExecutorUpdateTicking(object? sender, UpdateTickingEventArgs e)
    {
        try
        {
            executorInputTick++;
            var shouldPauseForExecutorIdle =
                config.FreezeClockWhileExecutorIdle &&
                Context.IsWorldReady &&
                pendingExecutions.IsEmpty &&
                !HasActiveExecutorOperation();
            if (config.FreezeClockWhileExecutorIdle && Game1.options is not null)
            {
                Game1.options.pauseWhenOutOfFocus = false;
            }
            if (shouldPauseForExecutorIdle)
            {
                Game1.gameTimeInterval = 0;
                Game1.paused = true;
                executorIdlePauseApplied = true;
            }
            else if (executorIdlePauseApplied)
            {
                Game1.paused = false;
                executorIdlePauseApplied = false;
            }

            if (!ApplyExecutorMovementInput(out var movementInputReason))
            {
                executorMovementLease.ForceRelease(
                    "movement_input_dispatch_failed",
                    executorInputTick);
                WriteExecutorDiagnosticDump(
                    "movement_input_dispatch_failed:" + movementInputReason);
                Monitor.Log($"Movement input dispatch failed: {movementInputReason}.", LogLevel.Error);
            }
            CaptureExecutorDiagnosticFrame("update_ticking");
            ApplyFairStrengthGameInput();
            if (activeCraneGame is not null &&
                !ApplyCraneGameInput(activeCraneGame, out var craneInputReason))
            {
                BlockCraneGame(activeCraneGame,
                    "crane_game_input_failed:" + craneInputReason);
                return;
            }
            if (activeDartsGame is not null &&
                !ApplyDartsGameInput(activeDartsGame, out var dartsInputReason))
            {
                BlockDartsGame(activeDartsGame,
                    "darts_game_input_failed:" + dartsInputReason);
                return;
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

            if (activeFairFishingGame is not null && Game1.activeClickableMenu is BobberBar fairBar &&
                !ApplyFairFishingBobberInput(activeFairFishingGame, fairBar, out var fairBobberInputReason))
            {
                BlockFairFishingGame(activeFairFishingGame,
                    "fair_fishing_game_bobber_input_failed:" + fairBobberInputReason);
                return;
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

            if (activeJunimoKart is not null && !ApplyJunimoKartInput(activeJunimoKart, out var junimoKartInputReason))
            {
                CompleteBlockedJunimoKart(activeJunimoKart, junimoKartInputReason);
                return;
            }

            if (activeSleep is not null &&
                (activeSleep.Stage == SleepStage.ConfirmPromptPress ||
                 activeSleep.Stage == SleepStage.ConfirmPromptRelease) &&
                !ApplySleepConfirmInput(activeSleep))
            {
                return;
            }

            if (activeShippingSummaryClose is not null)
            {
                ApplyShippingSummaryCloseInput(activeShippingSummaryClose);
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
            if (sleepObj is not null &&
                (sleepObj.Stage == SleepStage.ConfirmPromptPress ||
                 sleepObj.Stage == SleepStage.ConfirmPromptRelease))
            {
                Monitor.Log($"Sleep confirmation input failed once and was blocked: {ex}", LogLevel.Error);
                ReleaseSleepConfirmInput(sleepObj);
                CompleteBlockedSleep(sleepObj, "sleep_confirm_input_dispatch_exception:" + ex.GetType().Name);
                return;
            }
            var activeKart = activeJunimoKart;
            if (activeKart is not null)
            {
                CompleteBlockedJunimoKart(activeKart, "junimo_kart_input_dispatch_exception:" + ex.GetType().Name);
                return;
            }
            var activeCrane = activeCraneGame;
            if (activeCrane is not null)
            {
                Monitor.Log($"Crane Game input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                BlockCraneGame(activeCrane, "crane_game_input_dispatch_exception:" + ex.GetType().Name);
                return;
            }
            var activeDarts = activeDartsGame;
            if (activeDarts is not null)
            {
                Monitor.Log($"Darts input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                BlockDartsGame(activeDarts, "darts_game_input_dispatch_exception:" + ex.GetType().Name);
                return;
            }
            if (sleepObj is not null && sleepObj.Stage == SleepStage.WaitForPostSleepStable)
            {
                Monitor.Log($"Ship summary input dispatch failed once and was blocked: {ex}", LogLevel.Error);
                ReleaseSmapiLeftButtonOverride();
                CompleteBlockedSleep(sleepObj, "shipping_summary_input_dispatch_exception:" + ex.GetType().Name);
            }
            var summaryClose = activeShippingSummaryClose;
            if (summaryClose is not null)
            {
                Monitor.Log($"Standalone ship summary recovery input failed once and was blocked: {ex}", LogLevel.Error);
                CompleteBlockedShippingSummaryClose(summaryClose, "shipping_summary_input_dispatch_exception:" + ex.GetType().Name);
            }
        }
    }

    private bool HasActiveExecutorOperation()
    {
        return activeTileMove is not null ||
            activeSleep is not null ||
            activeWait is not null ||
            activeCatchFish is not null ||
            activeJunimoKart is not null ||
            activeHorseFlute is not null ||
            activeMonsterMusk is not null ||
            activeRainTotem is not null ||
            activeReturnScepter is not null ||
            activeWarpTotem is not null ||
            activeGrangeFixture is not null ||
            activeGrangeDisplay is not null ||
            activeFairFishingGame is not null ||
            activeFairSlingshotGame is not null ||
            activeFairStrengthGame is not null ||
            activeFairWheelSpin is not null ||
            activeCalicoJack is not null ||
            activeCraneGame is not null ||
            activeDartsGame is not null ||
            activeMineFishingSetup is not null ||
            activeMineSetup is not null ||
            activeQuarrySetup is not null ||
            activeVolcanoSetup is not null ||
            activeNativeTool is not null ||
            activeAdjacentTileAction is not null ||
            activeClearObstacle is not null ||
            activeMineStone is not null ||
            activeResourceClump is not null ||
            activeVolcanoCoolLava is not null ||
            activeVolcanoObstacle is not null ||
            activeVolcanoCombat is not null ||
            activeBreakContainer is not null ||
            activeCombatMonster is not null ||
            activeShootMonster is not null ||
            activePlaceBomb is not null ||
            activePlaceStaircase is not null ||
            activeConsumeFood is not null ||
            activePickupDebris is not null ||
            activeSpawnedObjectPickup is not null ||
            activeBushHarvest is not null ||
            activeFruitTreeHarvest is not null ||
            activeWildTreeProductHarvest is not null ||
            activeGarbageCanRummage is not null ||
            activeCrabPotCollect is not null ||
            activeAnimalProductHarvest is not null ||
            activeAnimalManagement is not null ||
            activePetInteraction is not null ||
            activeMuseumDonation is not null ||
            activeFieldOfficeDonation is not null ||
            activeFieldOfficeSurvey is not null ||
            activeQuestDropBoxDonation is not null ||
            activeCommunityCenterDonation is not null ||
            activeJojaDevelopment is not null ||
            activeFarmhouseUpgrade is not null ||
            activeHomeRenovation is not null ||
            activeBuildingConstruction is not null ||
            activeBuildingAppearanceChange is not null ||
            activeAnimalPurchase is not null ||
            activePanOreSpot is not null ||
            activeFishPondService is not null ||
            activeFishPondManagement is not null ||
            activeDescendLadder is not null ||
            activeDescendShaft is not null ||
            activeExitMine is not null ||
            activeShipInventoryToBin is not null ||
            activeMaterialTransfer is not null ||
            activeWorkbenchCraft is not null ||
            activeCooking is not null ||
            activeForge is not null ||
            activeDialogueAdvance is not null ||
            activeMenuClose is not null ||
            activeMailProcessing is not null ||
            activeMineElevatorSelection is not null ||
            activeShippingSummaryClose is not null ||
            activeSkullKeyChestInteraction is not null ||
            activeMineRewardChest is not null ||
            activePotOfGoldClaim is not null ||
            activeDwarfKingStatueChoice is not null ||
            activeStatueBlessingClaim is not null ||
            nativeObjectInteractions.IsActive ||
            activeSpecialOrderBoardOpen is not null ||
            activeQuestRewardClaim is not null;
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

internal static class HeadlessSaveGameMenuLifecyclePatch
{
    public static void Prefix(SaveGameMenu __instance)
    {
        // Native SaveGameMenu gates SaveGame.Save() on a flag set only by draw().
        __instance.hasDrawn = true;
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

internal static class PlacementCursorPatch
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
