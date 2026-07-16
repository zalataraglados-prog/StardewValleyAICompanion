using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
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

public sealed class ModEntry : Mod
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
    private ActiveNativeFarmTool? activeNativeFarmTool;
    private ActiveMineStone? activeMineStone;
    private ActiveBreakContainer? activeBreakContainer;
    private ActiveCombatMonster? activeCombatMonster;
    private ActiveShootMonster? activeShootMonster;
    private ActivePlaceBomb? activePlaceBomb;
    private ActiveConsumeFood? activeConsumeFood;
    private ActivePickupDebris? activePickupDebris;
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
        executorListener.Prefixes.Add($"http://{config.ExecutorHost}:{config.ExecutorPort}/");
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
        TickNativeFarmTool();
        TickMineStone();
        TickBreakContainer();
        TickCombatMonster();
        TickManualAutoCombat();
        TickConsumeFood();
        TickPickupDebris();
        TickShootMonster();
        TickPlaceBomb();
        TickDescendLadder();
        TickDescendShaft();
        TickExitMine();
        TickShipInventoryToBin();
        TickDialogueAdvance();

        if (activeTileMove is not null || activeSleep is not null || activeWait is not null || activeCatchFish is not null || activeMineFishingSetup is not null || activeMineSetup is not null || activeNativeFarmTool is not null || activeMineStone is not null || activeBreakContainer is not null || activeCombatMonster is not null || activeShootMonster is not null || activePlaceBomb is not null || activeConsumeFood is not null || activePickupDebris is not null || activeDescendLadder is not null || activeDescendShaft is not null || activeExitMine is not null || activeShipInventoryToBin is not null || activeDialogueAdvance is not null)
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

            if (pending.Request.OptionId == "debug.setup_mining_floor")
            {
                StartSetupMiningFloor(pending);
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
                pending.Completion.SetResult(ExecuteInteract(pending.Request));
                return;
            }

            if (pending.Request.OptionId == "executor.buy_shop_item")
            {
                pending.Completion.SetResult(ExecuteBuyShopItem(pending.Request));
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

            pending.Completion.SetResult(ExecuteMaintainCropsNoOp(pending.Request));
        }
        catch (Exception ex)
        {
            StopAllMovement();
            activeCatchFish = null;
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

    private void StartTileMove(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(pending.Request, reasons.ToArray()));
            return;
        }

        var primitiveKind = MovementPrimitiveKind(pending.Request);
        if ((pending.Request.OptionId == "executor.move_to_tile" ||
             pending.Request.OptionId == "executor.traverse_connector") &&
            (!pending.Request.TargetTileX.HasValue || !pending.Request.TargetTileY.HasValue))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                "player.tile=missing",
                "player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "movement_target_tile_required"));
            return;
        }

        if (pending.Request.OptionId == "executor.traverse_connector" &&
            string.IsNullOrWhiteSpace(pending.Request.ExpectedTargetLocation))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                ConnectorRequestedEffect(pending.Request),
                ConnectorObservedEffect(),
                "connector_expected_target_location_required"));
            return;
        }

        if (activeTileMove is not null)
        {
            pending.Completion.SetResult(Blocked(pending.Request, "movement_executor_busy"));
            return;
        }

        var startTile = Game1.player.TilePoint;
        var requestedTargetTile = ResolveTargetTile(pending.Request, startTile);
        var targetTile = requestedTargetTile;
        Point? connectorActionTile = null;
        int? connectorExitDirection = null;
        if (pending.Request.OptionId == "executor.traverse_connector" && IsActionConnectorKind(pending.Request.ConnectorKind))
        {
            connectorActionTile = requestedTargetTile;

            if (string.Equals(pending.Request.ConnectorKind, "building_door", StringComparison.OrdinalIgnoreCase))
            {
                var building = Game1.currentLocation.buildings
                    .FirstOrDefault(b =>
                        b.humanDoor.X >= 0 && b.humanDoor.Y >= 0 &&
                        b.tileX.Value + b.humanDoor.X == requestedTargetTile.X &&
                        b.tileY.Value + b.humanDoor.Y == requestedTargetTile.Y);
                if (building is null)
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_building_door_building_not_found"));
                    return;
                }

                var standTile = new Point(requestedTargetTile.X, requestedTargetTile.Y + 1);
                if (!IsTileTraversableForPlan(Game1.currentLocation, standTile, avoidSoftObstacles: true))
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_building_door_stand_tile_blocked"));
                    return;
                }

                targetTile = standTile;
            }
            else
            {
            var standTile = FindConnectorActionStandTile(Game1.currentLocation, startTile, requestedTargetTile);
            if (standTile is null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(
                    pending.Request,
                    primitiveKind,
                    ConnectorRequestedEffect(pending.Request),
                    ConnectorObservedEffect(),
                    "connector_action_stand_tile_unavailable"));
                return;
            }

            targetTile = standTile.Value;
        }
        }
        else if (pending.Request.OptionId == "executor.traverse_connector" &&
            string.Equals(pending.Request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase) &&
            IsTileOnMap(Game1.currentLocation, requestedTargetTile))
        {
            connectorActionTile = requestedTargetTile;
            var standTile = FindConnectorActionStandTile(Game1.currentLocation, startTile, requestedTargetTile);
            if (standTile is null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(
                    pending.Request,
                    primitiveKind,
                    ConnectorRequestedEffect(pending.Request),
                    ConnectorObservedEffect(),
                    "connector_warp_stand_tile_unavailable"));
                return;
            }

            targetTile = standTile.Value;
        }
        else if (pending.Request.OptionId == "executor.traverse_connector" &&
            string.Equals(pending.Request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase) &&
            !IsTileOnMap(Game1.currentLocation, requestedTargetTile) &&
            TryResolveBoundaryWarpStandTile(Game1.currentLocation, requestedTargetTile, out var boundaryStandTile, out var boundaryDirection))
        {
            targetTile = boundaryStandTile;
            connectorExitDirection = boundaryDirection;
        }

        if (startTile == targetTile)
        {
            if (pending.Request.OptionId == "executor.traverse_connector")
            {
                if (!IsActionConnectorKind(pending.Request.ConnectorKind))
                {
                    pending.Completion.SetResult(BlockedWithPrimitive(
                        pending.Request,
                        primitiveKind,
                        ConnectorRequestedEffect(pending.Request),
                        ConnectorObservedEffect(),
                        "connector_already_on_target_without_location_change"));
                    return;
                }

                activeTileMove = new ActiveTileMove(pending, startTile, targetTile, new List<Point>(), connectorActionTile, connectorExitDirection);
                return;
            }

            pending.Completion.SetResult(CompletedMove(pending, startTile, startTile, startTile, "verified", new[] { "already_at_target_tile" }));
            return;
        }

        var maxTiles = Math.Clamp(pending.Request.MaxMovementTiles ?? pending.Request.MaxCrops, 1, 512);
        var path = TryBuildTilePath(Game1.currentLocation, startTile, targetTile, maxTiles, out var blockReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                primitiveKind,
                "player.tile=" + targetTile.X + "," + targetTile.Y,
                "player.tile=" + startTile.X + "," + startTile.Y,
                blockReason));
            return;
        }

        activeTileMove = new ActiveTileMove(pending, startTile, targetTile, path, connectorActionTile, connectorExitDirection);
        Monitor.Log($"Started collision-checked tile move from {startTile.X},{startTile.Y} to {targetTile.X},{targetTile.Y} with {path.Count} path tile(s).", LogLevel.Info);
    }

    private TrainingExecutionResult ExecuteDirectConnectorTraversal(TrainingExecutionRequest request, Point startTile, Point requestedTargetTile, Point? connectorActionTile)
    {
        var beforeLocation = Game1.currentLocation.NameOrUniqueName;
        var beforeTile = Game1.player.TilePoint;
        var expectedLocation = request.ExpectedTargetLocation;
        var arrivalX = request.ExpectedArrivalTileX;
        var arrivalY = request.ExpectedArrivalTileY;

        if (!arrivalX.HasValue || !arrivalY.HasValue)
        {
            var actionTile = connectorActionTile ?? requestedTargetTile;
            var rawAction = Game1.currentLocation.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings");
            if (!string.IsNullOrWhiteSpace(rawAction))
            {
                var parts = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 && (string.Equals(parts[0], "Warp", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase)))
                {
                    arrivalX = ParseIntPart(parts, 1);
                    arrivalY = ParseIntPart(parts, 2);
                    expectedLocation = parts[3];
                }
            }
        }

        if (!arrivalX.HasValue || !arrivalY.HasValue)
        {
            var warp = Game1.currentLocation.warps.FirstOrDefault(candidate => candidate.X == requestedTargetTile.X && candidate.Y == requestedTargetTile.Y);
            if (warp is not null)
            {
                arrivalX = warp.TargetX;
                arrivalY = warp.TargetY;
                expectedLocation = warp.TargetName;
            }
        }

        if (string.IsNullOrWhiteSpace(expectedLocation) || !arrivalX.HasValue || !arrivalY.HasValue)
        {
            return BlockedWithPrimitive(request, "traverse_connector", ConnectorRequestedEffect(request), ConnectorObservedEffect(), "connector_direct_target_unresolved");
        }

        DirectSetPlayerLocation(expectedLocation, arrivalX.Value, arrivalY.Value);
        var afterLocation = Game1.currentLocation.NameOrUniqueName;
        var afterTile = Game1.player.TilePoint;
        var verified = string.Equals(afterLocation, request.ExpectedTargetLocation, StringComparison.OrdinalIgnoreCase);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "traverse_connector",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "connector_location_changed_as_expected", "collision_safe_path_validated_before_direct_runtime_transition" } : new[] { "connector_target_location_mismatch" },
            RequestedEffect = ConnectorRequestedEffect(request),
            ObservedEffect = ConnectorObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "connector_target_location_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = afterLocation },
                new SimulatedFactChange { Path = "player.tile", Before = beforeTile.X + "," + beforeTile.Y, After = afterTile.X + "," + afterTile.Y }
            }
        };
    }

    private void TickTileMove(UpdateTickedEventArgs e)
    {
        if (activeTileMove is null)
        {
            return;
        }

        var move = activeTileMove;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteBlockedMove(move, "world_not_ready_during_movement");
            return;
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, move.LocationId, StringComparison.Ordinal))
        {
            if (move.AllowsLocationChange)
            {
                CompleteConnectorMoveAfterLocationChange(move);
                return;
            }

            CompleteBlockedMove(move, "location_changed_during_movement");
            return;
        }

        if (Game1.player.TilePoint == move.TargetTile)
        {
            if (move.AllowsLocationChange)
            {
                move.PathIndex = move.Path.Count;
                move.StuckTicks = 0;
                move.LastPosition = Game1.player.Position;
                if (TryTriggerWarpConnector(move))
                {
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, move.LocationId, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    return;
                }

                if (TryTriggerConnectorAction(move))
                {
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, move.LocationId, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    return;
                }

                if (move.ConnectorExitDirection.HasValue)
                {
                    var beforeLocation = Game1.currentLocation.NameOrUniqueName;
                    StartMoving(move.ConnectorExitDirection.Value);
                    MovePlayerForTick();
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, beforeLocation, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    move.Tick++;
                    if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                    {
                        CompleteBlockedMove(move, "connector_boundary_warp_timeout");
                    }

                    return;
                }

                if (string.Equals(move.Pending.Request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase) &&
                    move.ConnectorActionTile.HasValue &&
                    !move.ConnectorActionAttempted)
                {
                    var beforeLocation = Game1.currentLocation.NameOrUniqueName;
                    var warpStepDirection = DirectionTo(Game1.player.TilePoint, move.ConnectorActionTile.Value);
                    StartMoving(warpStepDirection);
                    MovePlayerForTick();
                    if (!string.Equals(Game1.currentLocation.NameOrUniqueName, beforeLocation, StringComparison.Ordinal))
                    {
                        CompleteConnectorMoveAfterLocationChange(move);
                    }

                    move.Tick++;
                    if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                    {
                        CompleteBlockedMove(move, "connector_warp_step_timeout");
                    }

                    return;
                }

                move.Tick++;
                if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
                {
                    CompleteBlockedMove(move, "connector_target_reached_without_location_change");
                }

                return;
            }

            CompleteMove(move, "verified", new[] { "target_tile_reached" });
            return;
        }

        if (move.PathIndex >= move.Path.Count)
        {
            if (move.AllowsLocationChange)
            {
                CompleteBlockedMove(move, "connector_path_exhausted_before_location_change");
                return;
            }

            CompleteMove(move, "observed_mismatch", new[] { "path_exhausted_before_target_tile" });
            return;
        }

        var currentTile = Game1.player.TilePoint;
        var nextTile = move.Path[move.PathIndex];
        if (currentTile == nextTile)
        {
            move.PathIndex++;
            move.StuckTicks = 0;
            move.LastPosition = Game1.player.Position;
            return;
        }

        if (IsTileOccupiedByCharacter(Game1.currentLocation, nextTile))
        {
            StopAllMovement();
            move.CurrentDirection = null;
            move.SoftObstacleTicks++;
            if (move.SoftObstacleTicks % 30 == 0)
            {
                ReplanTileMove(move, avoidSoftObstacles: true);
            }

            if (move.SoftObstacleTicks > 180)
            {
                CompleteBlockedMove(move, "movement_soft_obstacle_timeout");
            }

            return;
        }

        move.SoftObstacleTicks = 0;

        if (!IsTileWalkable(Game1.currentLocation, nextTile))
        {
            if (TryClearRemovableObstacle(Game1.currentLocation, nextTile, move))
            {
                StopAllMovement();
                move.CurrentDirection = null;
                ReplanTileMove(move, avoidSoftObstacles: true);
                return;
            }

            if (ReplanTileMove(move, avoidSoftObstacles: true))
            {
                StopAllMovement();
                move.CurrentDirection = null;
                return;
            }

            CompleteBlockedMove(move, "movement_hard_obstacle_not_clearable");
            return;
        }

        if (!AreAdjacent(currentTile, nextTile))
        {
            CompleteBlockedMove(move, "movement_path_desynchronized");
            return;
        }

        var direction = DirectionTo(currentTile, nextTile);
        var movedSinceLastTick = Vector2.DistanceSquared(move.LastPosition, Game1.player.Position) >= 0.01f;
        move.LastPosition = Game1.player.Position;
        StartMovingIfNeeded(move, direction);
        MovePlayerForTick();

        if (Game1.player.TilePoint == nextTile)
        {
            move.PathIndex++;
        }

        if (!movedSinceLastTick)
        {
            move.StuckTicks++;
        }
        else
        {
            move.StuckTicks = 0;
            move.LastPosition = Game1.player.Position;
        }

        if (move.StuckTicks > 45)
        {
            CompleteBlockedMove(move, "movement_stuck_or_collision_blocked");
            return;
        }

        move.Tick++;
        if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
        {
            CompleteBlockedMove(move, "movement_timeout");
        }
    }

    private bool TryTriggerWarpConnector(ActiveTileMove move)
    {
        if (!string.Equals(move.Pending.Request.ConnectorKind, "warp", StringComparison.OrdinalIgnoreCase) || move.ConnectorExitDirection.HasValue)
        {
            return false;
        }

        if (move.ConnectorActionAttempted)
        {
            return false;
        }

        var warpTile = move.ConnectorActionTile ?? move.TargetTile;
        var warp = Game1.currentLocation.warps.FirstOrDefault(candidate => candidate.X == warpTile.X && candidate.Y == warpTile.Y);
        if (warp is null)
        {
            return false;
        }

        if (!string.Equals(warp.TargetName, move.Pending.Request.ExpectedTargetLocation, StringComparison.OrdinalIgnoreCase))
        {
            CompleteBlockedMove(move, "connector_warp_target_mismatch");
            return true;
        }

        move.ConnectorActionAttempted = true;
        DirectSetPlayerLocation(warp.TargetName, warp.TargetX, warp.TargetY);
        return true;
    }

    private bool TryTriggerConnectorAction(ActiveTileMove move)
    {
        if (move.ConnectorActionAttempted)
        {
            return false;
        }

        var kind = move.Pending.Request.ConnectorKind;
        if (!IsActionConnectorKind(kind))
        {
            return false;
        }

        move.ConnectorActionAttempted = true;
        var actionTile = move.ConnectorActionTile ?? move.TargetTile;

        if (string.Equals(kind, "building_door", StringComparison.OrdinalIgnoreCase))
        {
            TriggerBuildingDoorConnector(move, actionTile);
            return true;
        }

        var rawAction = Game1.currentLocation.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings");
        if (string.IsNullOrWhiteSpace(rawAction))
        {
            CompleteBlockedMove(move, "connector_action_property_missing");
            return true;
        }

        var actionType = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!IsConnectorActionTypeWhitelisted(actionType))
        {
            CompleteBlockedMove(move, "connector_action_type_not_whitelisted");
            return true;
        }

        if (string.Equals(kind, "locked_door_warp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionType, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
        {
            CompleteBlockedMove(move, "connector_action_type_mismatch");
            return true;
        }

        if (string.Equals(kind, "action_warp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionType, "Warp", StringComparison.OrdinalIgnoreCase))
        {
            CompleteBlockedMove(move, "connector_action_type_mismatch");
            return true;
        }

        var parts = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (string.Equals(actionType, "Warp", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
        {
            DirectSetPlayerLocation(parts[3], ParseIntPart(parts, 1) ?? Game1.player.TilePoint.X, ParseIntPart(parts, 2) ?? Game1.player.TilePoint.Y);
            return true;
        }

        if (string.Equals(actionType, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
        {
            DirectSetPlayerLocation(parts[3], ParseIntPart(parts, 1) ?? Game1.player.TilePoint.X, ParseIntPart(parts, 2) ?? Game1.player.TilePoint.Y);
            return true;
        }

        var handled = Game1.currentLocation.performAction(rawAction, Game1.player, new TileLocation(actionTile.X, actionTile.Y));
        if (!handled)
        {
            CompleteBlockedMove(move, "connector_action_not_handled");
        }

        return true;
    }

    private static void DirectSetPlayerLocation(string targetLocationName, int targetX, int targetY)
    {
        var targetLocation = Game1.getLocationFromName(targetLocationName);
        if (targetLocation is null)
        {
            return;
        }

        Game1.player.previousLocationName = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        Game1.currentLocation = targetLocation;
        Game1.player.currentLocation = targetLocation;
        Game1.player.Position = new Vector2(targetX * Game1.tileSize, targetY * Game1.tileSize);
        Game1.player.Halt();
        Game1.player.forceCanMove();
    }

    private static int? ParseIntPart(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length && int.TryParse(parts[index], out var value)
            ? value
            : null;
    }

    private void StartSleep(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), reasons.ToArray()));
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), "active_menu_must_be_closed_before_sleep"));
            return;
        }

        if (activeSleep is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), "sleep_executor_busy"));
            return;
        }

        var startTile = Game1.player.TilePoint;
        var target = ResolveHomeSleepTarget(startTile, out var targetReason);
        if (target is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), targetReason));
            return;
        }

        var path = TryBuildTilePath(Game1.currentLocation, startTile, target.StandTile, 512, out var blockReason, avoidSoftObstacles: true);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), blockReason));
            return;
        }

        activeSleep = new ActiveSleep(pending, startTile, target.BedTile, target.StandTile, path, Game1.year, Game1.dayOfMonth, Game1.timeOfDay, Game1.currentSeason);
        Monitor.Log($"Started terminal sleep macro via stand tile {target.StandTile.X},{target.StandTile.Y} and bed touch tile {target.BedTile.X},{target.BedTile.Y}.", LogLevel.Info);
    }

    private void TickSleep()
    {
        if (activeSleep is null)
        {
            return;
        }

        var sleep = activeSleep;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteBlockedSleep(sleep, "world_not_ready_during_sleep");
            return;
        }

        sleep.ElapsedTicks++;
        if (sleep.ElapsedTicks > sleep.MaxTicks)
        {
            CompleteBlockedSleep(sleep, "sleep_macro_timeout");
            return;
        }

        if ((sleep.Stage == SleepStage.MoveToStand || sleep.Stage == SleepStage.StepOntoSleepTouchTile) && SleepPromptOpen())
        {
            StopAllMovement();
            sleep.StuckTicks = 0;
            sleep.Stage = SleepStage.ConfirmPrompt;
        }

        if (sleep.Stage == SleepStage.MoveToStand)
        {
            if (!TickSleepMoveToStand(sleep))
            {
                return;
            }

            sleep.Stage = SleepStage.StepOntoSleepTouchTile;
        }

        if (sleep.Stage == SleepStage.StepOntoSleepTouchTile)
        {
            if (Game1.player.TilePoint != sleep.BedTile && !AreAdjacent(Game1.player.TilePoint, sleep.BedTile))
            {
                CompleteBlockedSleep(sleep, "sleep_bed_tile_not_adjacent_after_move");
                return;
            }

            if (Game1.player.TilePoint != sleep.BedTile)
            {
                var movedSinceLastTick = Vector2.DistanceSquared(sleep.LastPosition, Game1.player.Position) >= 0.01f;
                sleep.LastPosition = Game1.player.Position;
                StartMoving(DirectionTo(Game1.player.TilePoint, sleep.BedTile));
                MovePlayerForTick();
                if (!movedSinceLastTick)
                {
                    sleep.StuckTicks++;
                    if (sleep.StuckTicks > 45)
                    {
                        CompleteBlockedSleep(sleep, "sleep_bed_step_stuck_or_collision_blocked");
                    }
                    return;
                }

                sleep.StuckTicks = 0;
                return;
            }

            StopAllMovement();
            sleep.Stage = SleepStage.TriggerPrompt;
        }

        if (sleep.Stage == SleepStage.TriggerPrompt)
        {
            var touchAction = Game1.currentLocation.doesTileHaveProperty(sleep.BedTile.X, sleep.BedTile.Y, "TouchAction", "Back");
            if (!string.Equals(touchAction, "Sleep", StringComparison.Ordinal))
            {
                CompleteBlockedSleep(sleep, "sleep_touch_action_missing");
                return;
            }

            Game1.currentLocation.performTouchAction("Sleep", new Vector2(sleep.BedTile.X, sleep.BedTile.Y));
            sleep.Stage = SleepStage.ConfirmPrompt;
            return;
        }

        if (sleep.Stage == SleepStage.ConfirmPrompt)
        {
            if (!SleepPromptOpen())
            {
                sleep.PromptWaitTicks++;
                if (sleep.PromptWaitTicks > 60)
                {
                    CompleteBlockedSleep(sleep, "sleep_prompt_not_open_after_touch_action");
                }
                return;
            }

            Game1.currentLocation.answerDialogueAction("Sleep_Yes", new[] { "Sleep" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            sleep.Stage = SleepStage.WaitForNewDay;
            return;
        }

        if (sleep.Stage == SleepStage.WaitForNewDay)
        {
            if (Game1.year != sleep.StartYear || Game1.dayOfMonth != sleep.StartDay || !string.Equals(Game1.currentSeason, sleep.StartSeason, StringComparison.Ordinal))
            {
                sleep.Stage = SleepStage.WaitForPostSleepStable;
                return;
            }
        }

        if (sleep.Stage == SleepStage.WaitForPostSleepStable)
        {
            var menu = Game1.activeClickableMenu;
            if (menu is null)
            {
                if (sleep.SummaryPhase != default)
                {
                    ReleaseSmapiLeftButtonOverride();
                }
                try
                {
                    TrySettleActiveRunPendingShippingReceipts();
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Post-sleep shipping receipt settlement threw: {ex.Message}", LogLevel.Error);
                    CompleteBlockedSleep(sleep, "post_sleep_receipt_settlement_threw:" + ex.GetType().Name);
                    return;
                }
                CompleteSleep(sleep, "verified", new[] { "sleep_yes_confirmed", "new_day_observed", "post_sleep_menu_closed" });
                return;
            }

            if (menu is ShippingMenu shippingMenu)
            {
                TickShipSummaryClosePhase(sleep, shippingMenu);
                return;
            }

            sleep.PostSleepWaitTicks++;
            if (sleep.PostSleepWaitTicks > 600)
            {
                CompleteBlockedSleep(sleep, "post_sleep_menu_not_closed");
            }
        }
    }

    private bool TickSleepMoveToStand(ActiveSleep sleep)
    {
        if (Game1.player.TilePoint == sleep.StandTile)
        {
            StopAllMovement();
            return true;
        }

        if (sleep.PathIndex >= sleep.Path.Count)
        {
            CompleteBlockedSleep(sleep, "sleep_path_exhausted_before_stand_tile");
            return false;
        }

        var currentTile = Game1.player.TilePoint;
        var nextTile = sleep.Path[sleep.PathIndex];
        if (currentTile == nextTile)
        {
            sleep.PathIndex++;
            sleep.StuckTicks = 0;
            StopAllMovement();
            return false;
        }

        if (!AreAdjacent(currentTile, nextTile))
        {
            CompleteBlockedSleep(sleep, "sleep_path_desynchronized");
            return false;
        }

        if (!IsTileWalkable(Game1.currentLocation, nextTile))
        {
            CompleteBlockedSleep(sleep, "sleep_path_blocked");
            return false;
        }

        var movedSinceLastTick = Vector2.DistanceSquared(sleep.LastPosition, Game1.player.Position) >= 0.01f;
        sleep.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(currentTile, nextTile));
        MovePlayerForTick();
        if (Game1.player.TilePoint == nextTile)
        {
            sleep.PathIndex++;
        }

        if (!movedSinceLastTick)
        {
            sleep.StuckTicks++;
        }
        else
        {
            sleep.StuckTicks = 0;
        }

        if (sleep.StuckTicks > 45)
        {
            CompleteBlockedSleep(sleep, "sleep_movement_stuck_or_collision_blocked");
        }

        return false;
    }

    private void TickShipSummaryClosePhase(ActiveSleep sleep, ShippingMenu shippingMenu)
    {
        switch (sleep.SummaryPhase)
        {
            case ShipSummaryClosePhase.WaitReady:
                {
                    if (shippingMenu.CanReceiveInput() && shippingMenu.currentPage == -1)
                    {
                        sleep.SummaryPhase = ShipSummaryClosePhase.Position;
                        sleep.SummaryPositionSet = false;
                        sleep.SummaryPositionVerified = false;
                        sleep.SummaryButtonPressed = false;
                        sleep.SummaryButtonReleased = false;
                    }
                }
                break;

            case ShipSummaryClosePhase.Position:
                if (sleep.SummaryPositionSet && !sleep.SummaryPositionVerified)
                    sleep.SummaryPhase = ShipSummaryClosePhase.PositionVerify;
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (sleep.SummaryPositionVerified && !sleep.SummaryButtonPressed)
                    sleep.SummaryPhase = ShipSummaryClosePhase.Press;
                break;

            case ShipSummaryClosePhase.Press:
                if (sleep.SummaryButtonPressed && !sleep.SummaryButtonReleased)
                    sleep.SummaryPhase = ShipSummaryClosePhase.Release;
                break;

            case ShipSummaryClosePhase.Release:
                if (sleep.SummaryButtonReleased)
                {
                    sleep.SummaryButtonPressed = false;
                    sleep.SummaryButtonReleased = false;
                    sleep.SummaryPositionSet = false;
                    sleep.SummaryPositionVerified = false;
                    sleep.SummaryPhase = ShipSummaryClosePhase.WaitClose;
                }
                break;

            case ShipSummaryClosePhase.WaitClose:
                break;
        }
    }

    private void ApplyShipSummaryInput(ActiveSleep sleep)
    {
        if (Game1.activeClickableMenu is not ShippingMenu shippingMenu) return;

        switch (sleep.SummaryPhase)
        {
            case ShipSummaryClosePhase.Position:
                if (!sleep.SummaryPositionSet)
                {
                    var okButton = shippingMenu.okButton;
                    if (okButton is null)
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep, "shipping_summary_ok_button_null");
                        return;
                    }
                    var bounds = okButton.bounds;
                    var target = new Point(bounds.Center.X, bounds.Center.Y);
                    Game1.setMousePosition(target.X, target.Y, ui_scale: true);
                    sleep.SummaryPositionTarget = target;
                    sleep.SummaryPositionSet = true;
                }
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (sleep.SummaryPositionSet && !sleep.SummaryPositionVerified)
                {
                    var ax = Game1.getMouseX(ui_scale: true);
                    var ay = Game1.getMouseY(ui_scale: true);
                    if (Math.Abs(ax - sleep.SummaryPositionTarget.X) > 2 || Math.Abs(ay - sleep.SummaryPositionTarget.Y) > 2)
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep,
                            "shipping_summary_cursor_position_mismatch:expected=" + sleep.SummaryPositionTarget.X + "," + sleep.SummaryPositionTarget.Y + ";actual=" + ax + "," + ay);
                        return;
                    }
                    sleep.SummaryPositionVerified = true;
                }
                break;

            case ShipSummaryClosePhase.Press:
                if (!sleep.SummaryButtonPressed)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep, "shipping_summary_press_failed:" + reason);
                        return;
                    }
                    sleep.SummaryButtonPressed = true;
                }
                break;

            case ShipSummaryClosePhase.Release:
                if (!sleep.SummaryButtonReleased)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: false, out var relReason))
                    {
                        sleep.SummaryReleaseRetries++;
                        if (sleep.SummaryReleaseRetries > 3)
                        {
                            ReleaseSmapiLeftButtonOverride();
                            CompleteBlockedSleep(sleep, "shipping_summary_release_failed_after_retries:" + relReason);
                            return;
                        }
                        return;
                    }
                    sleep.SummaryButtonReleased = true;
                }
                break;
        }
    }

    private void CompleteSleep(ActiveSleep sleep, string verificationStatus, string[] verificationReasons)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeSleep = null;
        sleep.Pending.Completion.SetResult(CompletedSleep(sleep, verificationStatus, verificationReasons));
    }

    private void CompleteBlockedSleep(ActiveSleep sleep, string reason)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeSleep = null;
        sleep.Pending.Completion.SetResult(BlockedWithPrimitive(sleep.Pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), reason));
    }

    private static TrainingExecutionResult CompletedSleep(ActiveSleep sleep, string verificationStatus, string[] verificationReasons)
    {
        var request = sleep.Pending.Request;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verificationStatus == "verified" ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = sleep.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "sleep",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "day_transition=new_day",
            ObservedEffect = SleepObservedEffect(),
            BlockReasons = verificationStatus == "verified" ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.tile", Before = sleep.StartTile.X + "," + sleep.StartTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                new SimulatedFactChange { Path = "time.year", Before = sleep.StartYear.ToString(), After = Game1.year.ToString() },
                new SimulatedFactChange { Path = "time.season", Before = sleep.StartSeason, After = Game1.currentSeason },
                new SimulatedFactChange { Path = "time.day", Before = sleep.StartDay.ToString(), After = Game1.dayOfMonth.ToString() },
                new SimulatedFactChange { Path = "time.time", Before = sleep.StartTime.ToString(), After = Game1.timeOfDay.ToString() },
                new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = "false", After = (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() }
            }
        };
    }

    private static SleepTarget? ResolveHomeSleepTarget(Point startTile, out string reason)
    {
        reason = string.Empty;
        if (Game1.currentLocation is not FarmHouse farmHouse)
        {
            reason = "sleep_current_location_not_home";
            return null;
        }

        var bedTile = farmHouse.GetPlayerBedSpot();
        if (!BedFurniture.IsBedHere(farmHouse, bedTile.X, bedTile.Y))
        {
            reason = "sleep_bed_tile_unverified";
            return null;
        }

        var stand = new[]
        {
            new Point(bedTile.X - 1, bedTile.Y),
            new Point(bedTile.X + 1, bedTile.Y),
            new Point(bedTile.X, bedTile.Y + 1),
            new Point(bedTile.X, bedTile.Y - 1)
        }
            .Where(tile => IsTileWalkable(farmHouse, tile))
            .OrderBy(tile => tile == startTile ? 1 : 0)
            .ThenBy(tile => Math.Abs(startTile.X - tile.X) + Math.Abs(startTile.Y - tile.Y))
            .FirstOrDefault();

        if (stand == default)
        {
            reason = "sleep_stand_tile_unavailable";
            return null;
        }

        return new SleepTarget(bedTile, stand);
    }

    private static bool SleepPromptOpen()
    {
        return Game1.activeClickableMenu is DialogueBox && string.Equals(Game1.currentLocation?.lastQuestionKey, "Sleep", StringComparison.Ordinal);
    }

    private static string SleepObservedEffect()
    {
        return "time.day=" + Game1.dayOfMonth + ";time.time=" + Game1.timeOfDay + ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }

    private TrainingExecutionResult CompletedMove(PendingExecution pending, Point startTile, Point targetTile, Point observedTile, string verificationStatus, string[] verificationReasons)
    {
        var request = pending.Request;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verificationStatus == "verified" ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "move_to_tile",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "player.tile=" + targetTile.X + "," + targetTile.Y,
            ObservedEffect = "player.tile=" + observedTile.X + "," + observedTile.Y + MovementCostSuffix(pending),
            BlockReasons = verificationStatus == "verified" ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = MovementChangedFacts(pending, startTile, observedTile)
        };
    }

    private void CompleteConnectorMoveAfterLocationChange(ActiveTileMove move)
    {
        StopAllMovement();
        activeTileMove = null;

        var request = move.Pending.Request;
        var observedLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var observedTile = Game1.player.TilePoint;
        var reasons = new List<string>();

        if (!string.Equals(observedLocation, request.ExpectedTargetLocation, StringComparison.Ordinal))
        {
            reasons.Add("connector_unexpected_target_location");
        }

        if (request.ExpectedArrivalTileX.HasValue && request.ExpectedArrivalTileY.HasValue)
        {
            var expectedArrival = new Point(request.ExpectedArrivalTileX.Value, request.ExpectedArrivalTileY.Value);
            if (observedTile != expectedArrival)
            {
                reasons.Add("connector_unexpected_arrival_tile");
            }
        }

        if (reasons.Count == 0)
        {
            reasons.Add("connector_location_changed_as_expected");
        }

        var verified = reasons.Count == 1 && reasons[0] == "connector_location_changed_as_expected";
        move.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = move.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "traverse_connector",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons.ToArray(),
            RequestedEffect = ConnectorRequestedEffect(request),
            ObservedEffect = ConnectorObservedEffect() + MovementCostSuffix(move.Pending),
            BlockReasons = verified ? Array.Empty<string>() : reasons.ToArray(),
            ChangedFacts = ConnectorChangedFacts(move.Pending, move.LocationId, move.StartTile, observedLocation, observedTile)
        });
    }

    private static string MovementCostSuffix(PendingExecution pending)
    {
        return pending.MovementExtraTicks > 0 || pending.MovementClearanceActions > 0
            ? ";movement_extra_ticks=" + pending.MovementExtraTicks + ";clearance_actions=" + pending.MovementClearanceActions
            : string.Empty;
    }

    private static SimulatedFactChange[] MovementChangedFacts(PendingExecution pending, Point startTile, Point observedTile)
    {
        return pending.ChangedFacts
            .Prepend(new SimulatedFactChange
            {
                Path = "player.tile",
                Before = startTile.X + "," + startTile.Y,
                After = observedTile.X + "," + observedTile.Y
            })
            .ToArray();
    }

    private static SimulatedFactChange[] ConnectorChangedFacts(PendingExecution pending, string startLocation, Point startTile, string observedLocation, Point observedTile)
    {
        return pending.ChangedFacts
            .Prepend(new SimulatedFactChange
            {
                Path = "player.tile",
                Before = startTile.X + "," + startTile.Y,
                After = observedTile.X + "," + observedTile.Y
            })
            .Prepend(new SimulatedFactChange
            {
                Path = "player.location_id",
                Before = startLocation,
                After = observedLocation
            })
            .ToArray();
    }

    private bool ReplanTileMove(ActiveTileMove move, bool avoidSoftObstacles)
    {
        var currentTile = Game1.player.TilePoint;
        var remainingTiles = Math.Max(1, 512 - move.PathIndex);
        var path = TryBuildTilePath(Game1.currentLocation, currentTile, move.TargetTile, remainingTiles, out _, avoidSoftObstacles);
        if (path is null)
        {
            return false;
        }

        move.Path = path;
        move.PathIndex = 0;
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.Pending.MovementExtraTicks += 30;
        return true;
    }

    private bool TryClearRemovableObstacle(GameLocation location, Point tile, ActiveTileMove move)
    {
        if (!CanClearRouteObstacles(location))
        {
            return false;
        }

        var key = new Vector2(tile.X, tile.Y);
        var before = ObstacleLabel(location, tile);
        var tool = SelectClearanceTool(location, tile);
        if (tool is null)
        {
            return false;
        }

        var staminaBefore = Game1.player.Stamina;
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, tile));
        ApplyClearanceTool(location, tile, tool);

        if (Game1.activeClickableMenu is DialogueBox)
        {
            Game1.exitActiveMenu();
            return false;
        }

        if (!IsTileWalkable(location, tile) && location.objects.ContainsKey(key))
        {
            return false;
        }

        move.Pending.MovementClearanceActions++;
        move.Pending.MovementExtraTicks += ClearanceTickCost(tool);
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "movement.clearance[" + tile.X + "," + tile.Y + "]",
            Before = before,
            After = ObstacleLabel(location, tile)
        });
        move.Pending.ChangedFacts.Add(new SimulatedFactChange
        {
            Path = "player.energy",
            Before = staminaBefore.ToString("0.###"),
            After = Game1.player.Stamina.ToString("0.###")
        });
        return true;
    }

    private TrainingExecutionResult ExecuteClearObstacle(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", "current_location.obstacle=clear", ClearObstacleObservedEffect(null), "clear_obstacle_target_tile_required");
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = "current_location.obstacle[" + target.X + "," + target.Y + "]=clear";
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_location_not_whitelisted");
        }

        if (ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_target_not_adjacent");
        }

        var tool = SelectClearanceTool(location, target);
        if (tool is null)
        {
            return BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_no_matching_tool_or_obstacle");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var before = ObstacleLabel(location, target);
        var staminaBefore = Game1.player.Stamina;
        var swings = Math.Clamp(request.MaxCrops, 1, 64);
        var observedLabels = new List<string> { before };
        for (var swing = 0; swing < swings; swing++)
        {
            if (ObstacleLabel(location, target) == "clear")
            {
                break;
            }

            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
            ApplyClearanceTool(location, target, tool);
            if (Game1.activeClickableMenu is DialogueBox)
            {
                Game1.exitActiveMenu();
                break;
            }

            observedLabels.Add(ObstacleLabel(location, target));
        }

        var after = ObstacleLabel(location, target);
        var verified = after == "clear";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_obstacle_cleared", "tool=" + tool.GetType().Name }
                : new[] { "target_obstacle_still_present", "tool=" + tool.GetType().Name },
            RequestedEffect = requested,
            ObservedEffect = "before=" + before + ";after=" + after + ";labels=" + string.Join(">", observedLabels),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "target_obstacle_still_present" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                },
                new SimulatedFactChange
                {
                    Path = "player.energy",
                    Before = staminaBefore.ToString("0.###"),
                    After = Game1.player.Stamina.ToString("0.###")
                }
            }
        };
    }

    private static void ApplyClearanceTool(GameLocation location, Point target, Tool tool)
    {
        var tile = new Vector2(target.X, target.Y);
        if (location.terrainFeatures.TryGetValue(tile, out var feature))
        {
            if (feature.performToolAction(tool, 0, tile))
            {
                location.terrainFeatures.Remove(tile);
            }

            return;
        }

        tool.DoFunction(location, target.X * Game1.tileSize, target.Y * Game1.tileSize, 0, Game1.player);
    }

    private static string ClearObstacleObservedEffect(Point? target)
    {
        return target.HasValue
            ? "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";obstacle=" + ObstacleLabel(Game1.currentLocation, target.Value)
            : "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static Tool? SelectClearanceTool(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj is BreakableContainer)
            {
                return FindHeavyTool();
            }

            if (obj.IsBreakableStone())
            {
                return FindTool<Pickaxe>();
            }

            if (obj.IsWeeds())
            {
                return FindScythe() ?? FindHeavyTool();
            }

            if (obj.IsTwig())
            {
                return FindTool<Axe>();
            }

            return null;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return feature switch
            {
                Grass => FindScythe() ?? FindHeavyTool(),
                Tree => FindTool<Axe>(),
                FruitTree => FindTool<Axe>(),
                _ => null
            };
        }

        var tileRect = TileRectangle(tile);
        foreach (var largeFeature in location.largeTerrainFeatures)
        {
            if (largeFeature.getBoundingBox().Intersects(tileRect))
            {
                return FindTool<Axe>();
            }
        }

        return null;
    }

    private static TTool? FindTool<TTool>() where TTool : Tool
    {
        return Game1.player.Items.OfType<TTool>().FirstOrDefault();
    }

    private static Tool? FindScythe()
    {
        return Game1.player.Items.OfType<MeleeWeapon>().FirstOrDefault(weapon => weapon.isScythe());
    }

    private static Tool? FindHeavyTool()
    {
        return Game1.player.Items.OfType<Tool>().FirstOrDefault(tool => tool.isHeavyHitter());
    }

    private static int ClearanceTickCost(Tool tool)
    {
        return tool switch
        {
            MeleeWeapon => 30,
            Axe => 60,
            Pickaxe => 60,
            _ => 60
        };
    }

    private static string ObstacleLabel(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            return "object:" + obj.QualifiedItemId + ":" + obj.Name;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return "terrain_feature:" + feature.GetType().Name;
        }

        var tileRect = TileRectangle(tile);
        if (location.largeTerrainFeatures.Any(feature => feature.getBoundingBox().Intersects(tileRect)))
        {
            return "large_terrain_feature";
        }

        if (location.resourceClumps.Any(clump => clump.getBoundingBox().Intersects(tileRect)))
        {
            return "resource_clump";
        }

        return "clear";
    }

    private static bool IsRemovableObstacle(GameLocation location, Point tile)
    {
        return CanClearRouteObstacles(location) && SelectClearanceTool(location, tile) is not null;
    }

    private static bool CanClearRouteObstacles(GameLocation location)
    {
        return location.IsFarm
            || location is MineShaft
            || location is VolcanoDungeon
            || string.Equals(location.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTileOccupiedByCharacter(GameLocation location, Point tile)
    {
        var tileRect = TileRectangle(tile);
        return location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect));
    }

    private static XnaRectangle TileRectangle(Point tile)
    {
        return new XnaRectangle(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
    }

    private static bool IsTileTraversableForPlan(GameLocation location, Point tile, bool avoidSoftObstacles, bool allowRemovableObstacles = true)
    {
        if (!IsTileOnMap(location, tile))
        {
            return false;
        }

        if (avoidSoftObstacles && IsTileOccupiedByCharacter(location, tile))
        {
            return false;
        }

        return IsTileWalkable(location, tile) || allowRemovableObstacles && IsRemovableObstacle(location, tile) || IsTileOccupiedByCharacter(location, tile);
    }

    private static bool IsTileHardBlocked(GameLocation location, Point tile)
    {
        return !IsTileWalkable(location, tile) && !IsRemovableObstacle(location, tile) && !IsTileOccupiedByCharacter(location, tile);
    }

    private static string MovementHardBlockReason(GameLocation location, Point tile)
    {
        if (!IsTileOnMap(location, tile))
        {
            return "movement_target_tile_out_of_map";
        }

        if (IsTileOccupiedByCharacter(location, tile))
        {
            return "movement_target_soft_obstacle";
        }

        if (IsRemovableObstacle(location, tile))
        {
            return "movement_target_requires_clearance";
        }

        return "movement_target_tile_hard_blocked";
    }

    private void CompleteMove(ActiveTileMove move, string verificationStatus, string[] verificationReasons)
    {
        StopAllMovement();
        activeTileMove = null;
        move.Pending.Completion.SetResult(CompletedMove(move.Pending, move.StartTile, move.TargetTile, Game1.player.TilePoint, verificationStatus, verificationReasons));
    }

    private void CompleteBlockedMove(ActiveTileMove move, string reason)
    {
        StopAllMovement();
        activeTileMove = null;
        move.Pending.Completion.SetResult(BlockedWithPrimitive(
            move.Pending.Request,
            MovementPrimitiveKind(move.Pending.Request),
            MovementRequestedEffect(move.Pending.Request, move.TargetTile),
            MovementObservedEffect(),
            reason));
    }

    private static string MovementPrimitiveKind(TrainingExecutionRequest request)
    {
        return request.OptionId == "executor.traverse_connector" ? "traverse_connector" : "move_to_tile";
    }

    private static string MovementRequestedEffect(TrainingExecutionRequest request, Point targetTile)
    {
        return request.OptionId == "executor.traverse_connector"
            ? ConnectorRequestedEffect(request)
            : "player.tile=" + targetTile.X + "," + targetTile.Y;
    }

    private static string MovementObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static string ConnectorRequestedEffect(TrainingExecutionRequest request)
    {
        var arrival = request.ExpectedArrivalTileX.HasValue && request.ExpectedArrivalTileY.HasValue
            ? ";arrival_tile=" + request.ExpectedArrivalTileX.Value + "," + request.ExpectedArrivalTileY.Value
            : string.Empty;
        return "connector.target_location=" + request.ExpectedTargetLocation + arrival;
    }

    private static string ConnectorObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static bool IsActionConnectorKind(string kind)
    {
        return string.Equals(kind, "action_warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "locked_door_warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "building_door", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConnectorActionTypeWhitelisted(string actionType)
    {
        return string.Equals(actionType, "Warp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionType, "LockedDoorWarp", StringComparison.OrdinalIgnoreCase);
    }

    private void TriggerBuildingDoorConnector(ActiveTileMove move, Point actionTile)
    {
        var location = Game1.currentLocation;
        var building = location.buildings
            .FirstOrDefault(b =>
                b.humanDoor.X >= 0 && b.humanDoor.Y >= 0 &&
                b.tileX.Value + b.humanDoor.X == actionTile.X &&
                b.tileY.Value + b.humanDoor.Y == actionTile.Y);

        if (building is null)
        {
            CompleteBlockedMove(move, "building_door_no_building_at_action_tile");
            return;
        }

        if (building.daysOfConstructionLeft.Value > 0)
        {
            CompleteBlockedMove(move, "building_under_construction");
            return;
        }

        var indoors = building.GetIndoors();
        if (indoors is null)
        {
            CompleteBlockedMove(move, "building_door_no_indoor_location");
            return;
        }

        if (indoors.warps.Count == 0)
        {
            CompleteBlockedMove(move, "building_door_no_indoor_warps");
            return;
        }

        var expectedLocation = move.Pending.Request.ExpectedTargetLocation;
        if (!string.Equals(indoors.NameOrUniqueName, expectedLocation, StringComparison.OrdinalIgnoreCase))
        {
            CompleteBlockedMove(move, "building_door_target_location_mismatch:expected=" + expectedLocation + ";actual=" + indoors.NameOrUniqueName);
            return;
        }

        var playerTile = Game1.player.TilePoint;
        var expectedStandTile = new Point(actionTile.X, actionTile.Y + 1);
        if (Game1.player.TilePoint != expectedStandTile)
        {
            CompleteBlockedMove(move, "building_door_player_not_on_stand_tile:expected=" + expectedStandTile.X + "," + expectedStandTile.Y + ";actual=" + playerTile.X + "," + playerTile.Y);
            return;
        }

        Game1.player.faceDirection(0);

        bool doActionResult;
        try
        {
            doActionResult = building.doAction(new Vector2(actionTile.X, actionTile.Y), Game1.player);
        }
        catch (Exception ex)
        {
            CompleteBlockedMove(move, "building_door_doAction_exception:" + ex.GetType().Name);
            return;
        }

        if (!doActionResult)
        {
            CompleteBlockedMove(move, "building_door_doAction_returned_false");
            return;
        }
    }

    private static Point? FindConnectorActionStandTile(GameLocation location, Point startTile, Point actionTile)
    {
        return Neighbors(actionTile)
            .Where(tile => IsTileTraversableForPlan(location, tile, avoidSoftObstacles: true))
            .OrderBy(tile => Math.Abs(startTile.X - tile.X) + Math.Abs(startTile.Y - tile.Y))
            .Cast<Point?>()
            .FirstOrDefault();
    }

    private static bool TryResolveBoundaryWarpStandTile(GameLocation location, Point warpTile, out Point standTile, out int direction)
    {
        var dimensions = MapDimensions(location);
        var width = dimensions.X;
        var height = dimensions.Y;
        if (width <= 0 || height <= 0)
        {
            standTile = Point.Zero;
            direction = 0;
            return false;
        }

        if (warpTile.X < 0 && warpTile.Y >= 0 && warpTile.Y < height)
        {
            standTile = new Point(0, warpTile.Y);
            direction = 3;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.X >= width && warpTile.Y >= 0 && warpTile.Y < height)
        {
            standTile = new Point(width - 1, warpTile.Y);
            direction = 1;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.Y < 0 && warpTile.X >= 0 && warpTile.X < width)
        {
            standTile = new Point(warpTile.X, 0);
            direction = 0;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        if (warpTile.Y >= height && warpTile.X >= 0 && warpTile.X < width)
        {
            standTile = new Point(warpTile.X, height - 1);
            direction = 2;
            return IsTileTraversableForPlan(location, standTile, avoidSoftObstacles: true);
        }

        standTile = Point.Zero;
        direction = 0;
        return false;
    }

    private static Point MapDimensions(GameLocation location)
    {
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        if (layers.Length == 0)
        {
            return Point.Zero;
        }

        return new Point(layers.Max(layer => layer.LayerWidth), layers.Max(layer => layer.LayerHeight));
    }

    private static TrainingExecutionResult BlockedWithPrimitive(TrainingExecutionRequest request, string primitiveKind, string requestedEffect, string observedEffect, params string[] reasons)
    {
        var result = Blocked(request, reasons);
        result.FeedbackAvailable = true;
        result.PrimitiveKind = primitiveKind;
        result.PrimitiveVerificationStatus = "blocked";
        result.PrimitiveVerificationReasons = reasons;
        result.RequestedEffect = requestedEffect;
        result.ObservedEffect = observedEffect;
        return result;
    }

    private static Point ResolveTargetTile(TrainingExecutionRequest request, Point startTile)
    {
        if (request.TargetTileX.HasValue && request.TargetTileY.HasValue)
        {
            return new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        }

        var steps = Math.Clamp(request.MaxCrops, 1, 8);
        return new Point(startTile.X + steps, startTile.Y);
    }

    private static List<Point>? TryBuildTilePath(GameLocation location, Point startTile, Point targetTile, int maxTiles, out string blockReason, bool avoidSoftObstacles = false, bool allowRemovableObstacles = true)
    {
        blockReason = string.Empty;
        if (IsTileHardBlocked(location, targetTile))
        {
            blockReason = MovementHardBlockReason(location, targetTile);
            return null;
        }

        var costs = new Dictionary<string, int>(StringComparer.Ordinal) { [TileKey(startTile)] = 0 };
        var steps = new Dictionary<string, int>(StringComparer.Ordinal) { [TileKey(startTile)] = 0 };
        var previous = new Dictionary<string, Point>(StringComparer.Ordinal);
        var queue = new PriorityQueue<Point, int>();
        queue.Enqueue(startTile, 0);

        while (queue.Count > 0)
        {
            queue.TryDequeue(out var current, out var dequeuedCost);
            var currentKey = TileKey(current);
            if (!costs.TryGetValue(currentKey, out var currentCost) || dequeuedCost != currentCost)
            {
                continue;
            }
            if (current == targetTile)
            {
                return ReconstructPath(startTile, targetTile, previous);
            }

            foreach (var next in Neighbors(current))
            {
                var key = TileKey(next);
                if (!IsTileTraversableForPlan(location, next, avoidSoftObstacles, allowRemovableObstacles))
                {
                    continue;
                }

                var nextSteps = steps[currentKey] + 1;
                if (nextSteps > maxTiles)
                {
                    continue;
                }
                var nextCost = currentCost + MovementTraversalCost(location, next);
                if (costs.TryGetValue(key, out var knownCost) && knownCost <= nextCost)
                {
                    continue;
                }

                costs[key] = nextCost;
                steps[key] = nextSteps;
                previous[key] = current;
                queue.Enqueue(next, nextCost);
            }
        }

        blockReason = "movement_no_collision_safe_path";
        return null;
    }

    private static int MovementTraversalCost(GameLocation location, Point tile)
    {
        if (IsTileWalkable(location, tile))
        {
            return 32;
        }
        if (IsTileOccupiedByCharacter(location, tile))
        {
            return 192;
        }

        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj.IsBreakableStone() && FindTool<Pickaxe>() is Pickaxe pickaxe)
            {
                var damage = Math.Max(1, pickaxe.UpgradeLevel + 1) + Math.Max(0, pickaxe.additionalPower.Value);
                var swings = Math.Max(1, (int)Math.Ceiling(obj.MinutesUntilReady / (double)damage));
                return 32 + swings * ClearanceTickCost(pickaxe);
            }
            if (obj is BreakableContainer)
            {
                return 32 + 3 * 30;
            }
            if (obj.IsWeeds())
            {
                return 32 + 30;
            }
            if (obj.IsTwig())
            {
                return 32 + 60;
            }
        }

        return 32 + 8 * 60;
    }

    private static List<Point> ReconstructPath(Point startTile, Point targetTile, Dictionary<string, Point> previous)
    {
        var path = new List<Point>();
        var current = targetTile;
        while (current != startTile)
        {
            path.Add(current);
            current = previous[TileKey(current)];
        }

        path.Reverse();
        return path;
    }

    private static bool IsTileOnMap(GameLocation location, Point tile)
    {
        return location.isTileOnMap(new Vector2(tile.X, tile.Y));
    }

    private static bool IsTileWalkable(GameLocation location, Point tile)
    {
        var rectangle = new XnaRectangle(tile.X * Game1.tileSize + 1, tile.Y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
        return !location.isCollidingPosition(rectangle, Game1.viewport, isFarmer: true, 0, glider: false, Game1.player, pathfinding: true);
    }

    private static IEnumerable<Point> Neighbors(Point tile)
    {
        yield return new Point(tile.X + 1, tile.Y);
        yield return new Point(tile.X - 1, tile.Y);
        yield return new Point(tile.X, tile.Y + 1);
        yield return new Point(tile.X, tile.Y - 1);
    }

    private static int ManhattanDistance(Point left, Point right)
    {
        return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
    }

    private static bool AreAdjacent(Point left, Point right)
    {
        return ManhattanDistance(left, right) == 1;
    }

    private static int DirectionTo(Point from, Point to)
    {
        if (to.Y < from.Y)
        {
            return 0;
        }

        if (to.X > from.X)
        {
            return 1;
        }

        if (to.Y > from.Y)
        {
            return 2;
        }

        return 3;
    }

    private static string TileKey(Point tile)
    {
        return tile.X + "," + tile.Y;
    }

    private void StartMoving(int direction)
    {
        Game1.player.forceCanMove();
        Game1.player.faceDirection(direction);
        executorMovementDirection = direction;
    }

    private void StopAllMovement()
    {
        executorMovementDirection = null;
        ApplyExecutorMovementInput(out _);
    }

    private static void MovePlayerForTick()
    {
        // Movement is consumed by the game's native update on the next tick.
    }

    private bool ApplyExecutorMovementInput(out string reason)
    {
        reason = string.Empty;
        var buttons = new[] { SButton.W, SButton.D, SButton.S, SButton.A };
        for (var direction = 0; direction < buttons.Length; direction++)
        {
            if (!TryApplySmapiButtonOverride(buttons[direction], executorMovementDirection == direction, out reason))
            {
                return false;
            }
        }

        return true;
    }

    private void StartMovingIfNeeded(ActiveTileMove move, int direction)
    {
        if (move.CurrentDirection == direction)
        {
            return;
        }

        StartMoving(direction);
        move.CurrentDirection = direction;
    }

    private void StartMaintainCrops(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var farm = Game1.getFarm();
        var hasTargetTile = request.TargetTileX.HasValue && request.TargetTileY.HasValue;
        var targetTileX = request.TargetTileX.GetValueOrDefault();
        var targetTileY = request.TargetTileY.GetValueOrDefault();

        foreach (var pair in farm.terrainFeatures.Pairs.OrderBy(item => item.Key.Y).ThenBy(item => item.Key.X))
        {
            if (hasTargetTile &&
                ((int)pair.Key.X != targetTileX ||
                 (int)pair.Key.Y != targetTileY))
            {
                continue;
            }

            if (pair.Value is not HoeDirt dirt || dirt.crop is null || !dirt.needsWatering())
            {
                continue;
            }

            StartWaterCrop(pending, new Point((int)pair.Key.X, (int)pair.Key.Y));
            return;
        }

        pending.Completion.SetResult(ExecuteMaintainCropsNoOp(request));
    }

    private TrainingExecutionResult ExecuteMaintainCropsNoOp(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var energyBefore = Game1.player.Stamina;
        var farm = Game1.getFarm();
        var hasTargetTile = request.TargetTileX.HasValue && request.TargetTileY.HasValue;
        var targetTileX = request.TargetTileX.GetValueOrDefault();
        var targetTileY = request.TargetTileY.GetValueOrDefault();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "no_op",
            FeedbackAvailable = true,
            WateredCount = 0,
            EnergyBefore = energyBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = hasTargetTile ? targetTileX : null,
            TargetTileY = hasTargetTile ? targetTileY : null,
            FailureCategory = hasTargetTile ? "invalid_tile" : "skipped_no_candidate",
            TrainingImpactScope = "executor_calibration",
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "maintain_crops",
            PrimitiveVerificationStatus = "not_applicable_no_op",
            PrimitiveVerificationReasons = new[] { hasTargetTile ? "target_crop_not_found_or_not_needing_watering" : "no_crop_needed_watering" },
            RequestedEffect = hasTargetTile
                ? "farm.crops[" + targetTileX + "," + targetTileY + "].needs_watering=false"
                : "farm.crops.needs_watering=false",
            ObservedEffect = "watered_count=0"
        };
    }

    private void StartWaterCrop(PendingExecution pending, Point target)
    {
        var request = pending.Request;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var staminaBefore = Game1.player.Stamina;
        var can = FindTool<WateringCan>();
        var waterBefore = can?.WaterLeft;
        var estimatedTicks = EstimateRuntimeToolTicks(target);
        var requested = WaterCropRequestedEffect(target);

        if (Game1.currentLocation != farm)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, "wrong_location", requested, WaterCropObservedEffect(farm, target)));
            return;
        }

        var precheck = ValidateWaterCropTarget(farm, target, can);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, precheck[0], requested, WaterCropObservedEffect(farm, target), precheck));
            return;
        }

        var path = BuildAdjacentToolPath(farm, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, moveReason, requested, WaterCropObservedEffect(farm, target)));
            return;
        }

        activeNativeFarmTool = ActiveNativeFarmTool.Water(pending, farm.NameOrUniqueName, target, path, can!, staminaBefore, waterBefore, started, estimatedTicks, requested, IsCropWatered(farm, target));
    }

    private void StartTillSoil(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "till_soil", "farm.terrain_features[target].type=HoeDirt", TillSoilObservedEffect(Game1.getFarm(), null), "target_tile_required"));
            return;
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var staminaBefore = Game1.player.Stamina;
        var hoe = FindTool<Hoe>();
        var estimatedTicks = EstimateRuntimeToolTicks(target);
        var requested = TillSoilRequestedEffect(target);

        if (Game1.currentLocation != farm)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, "wrong_location", requested, TillSoilObservedEffect(farm, target)));
            return;
        }

        var precheck = ValidateTillSoilTarget(farm, target, hoe);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, precheck[0], requested, TillSoilObservedEffect(farm, target), precheck));
            return;
        }

        var path = BuildAdjacentToolPath(farm, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, moveReason, requested, TillSoilObservedEffect(farm, target)));
            return;
        }

        var tile = new Vector2(target.X, target.Y);
        var hadHoeDirt = farm.terrainFeatures.TryGetValue(tile, out var beforeFeature) && beforeFeature is HoeDirt;
        activeNativeFarmTool = ActiveNativeFarmTool.Till(pending, farm.NameOrUniqueName, target, path, hoe!, staminaBefore, started, estimatedTicks, requested, hadHoeDirt);
    }

    private static string[] ValidateWaterCropTarget(Farm farm, Point target, WateringCan? can)
    {
        var reasons = new List<string>();
        var tile = new Vector2(target.X, target.Y);
        if (!IsTileOnMap(farm, target))
        {
            reasons.Add("invalid_tile");
        }
        if (can is null)
        {
            reasons.Add("missing_tool");
        }
        else if (can.WaterLeft <= 0 && !Game1.player.hasWateringCanEnchantment)
        {
            reasons.Add("no_water");
        }
        if (Game1.player.Stamina <= 0f)
        {
            reasons.Add("insufficient_stamina");
        }
        if (!farm.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            reasons.Add("invalid_tile");
        }
        else if (!dirt.needsWatering())
        {
            reasons.Add("already_satisfied_runtime_drift");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateTillSoilTarget(Farm farm, Point target, Hoe? hoe)
    {
        var reasons = new List<string>();
        var tile = new Vector2(target.X, target.Y);
        if (!IsTileOnMap(farm, target) || farm.doesTileHaveProperty(target.X, target.Y, "Diggable", "Back") is null)
        {
            reasons.Add("invalid_tile");
        }
        if (hoe is null)
        {
            reasons.Add("missing_tool");
        }
        if (Game1.player.Stamina <= 0f)
        {
            reasons.Add("insufficient_stamina");
        }
        if (farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt)
        {
            reasons.Add("already_satisfied_runtime_drift");
        }
        else if (farm.terrainFeatures.ContainsKey(tile) || farm.objects.ContainsKey(tile) || farm.IsTileBlockedBy(tile, ~(CollisionMask.Characters | CollisionMask.Farmers)))
        {
            reasons.Add("occupied_tile");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static List<Point>? BuildAdjacentToolPath(GameLocation location, Point target, int maxTiles, out string blockReason, bool avoidSoftObstacles = false, bool allowRemovableObstacles = true)
    {
        blockReason = string.Empty;
        if (AreAdjacent(Game1.player.TilePoint, target))
        {
            return new List<Point>();
        }

        var start = Game1.player.TilePoint;
        var standTiles = Neighbors(target)
            .Where(tile => IsTileOnMap(location, tile) && IsTileWalkable(location, tile) &&
                (!avoidSoftObstacles || !IsTileOccupiedByCharacter(location, tile)))
            .OrderBy(tile => ManhattanDistance(start, tile))
            .ToArray();
        foreach (var standTile in standTiles)
        {
            var path = TryBuildTilePath(location, start, standTile, Math.Clamp(maxTiles, 1, 512), out blockReason, avoidSoftObstacles, allowRemovableObstacles);
            if (path is null)
            {
                continue;
            }

            return path;
        }

        blockReason = "unreachable_target";
        return null;
    }

    private void TickNativeFarmTool()
    {
        if (activeNativeFarmTool is null)
        {
            return;
        }

        var tool = activeNativeFarmTool;
        try
        {
            TickNativeFarmToolCore(tool);
        }
        catch (Exception ex)
        {
            CleanupBlockedNativeToolLifecycle(tool);
            activeNativeFarmTool = null;
            Monitor.Log($"Native farm tool execution failed: {ex}", LogLevel.Error);
            tool.Pending.Completion.SetResult(NativeToolBlocked(tool.Pending.Request, tool.PrimitiveKind, tool.Target, tool.Tool, tool.WaterBefore, tool.StaminaBefore, tool.StartedAt, tool.EstimatedTicks, "execution_exception:" + ex.GetType().Name, tool.RequestedEffect, NativeToolObservedEffect(tool), actualTicks: tool.ElapsedTicks));
        }
    }

    private void TickNativeFarmToolCore(ActiveNativeFarmTool tool)
    {
        tool.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteNativeToolBlocked(tool, "world_not_ready_during_tool_use");
            return;
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, tool.LocationId, StringComparison.Ordinal))
        {
            CompleteNativeToolBlocked(tool, "location_changed_during_tool_use");
            return;
        }

        if (tool.ElapsedTicks > tool.MaxTicks)
        {
            CompleteNativeToolBlocked(tool, tool.BeginIssued ? "tool_timeout" : "movement_timeout");
            return;
        }

        if (!tool.BeginIssued && !AreAdjacent(Game1.player.TilePoint, tool.Target))
        {
            if (tool.PathIndex >= tool.Path.Count)
            {
                CompleteNativeToolBlocked(tool, "unreachable_target");
                return;
            }

            var next = tool.Path[tool.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                tool.PathIndex++;
                tool.StuckTicks = 0;
                tool.LastPosition = Game1.player.Position;
                return;
            }

            if (!IsTileWalkable(Game1.currentLocation, next) || IsTileOccupiedByCharacter(Game1.currentLocation, next))
            {
                CompleteNativeToolBlocked(tool, "unreachable_target");
                return;
            }

            var direction = DirectionTo(Game1.player.TilePoint, next);
            var movedSinceLastTick = Vector2.DistanceSquared(tool.LastPosition, Game1.player.Position) >= 0.01f;
            tool.LastPosition = Game1.player.Position;
            StartMoving(direction);
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                tool.PathIndex++;
            }

            if (!movedSinceLastTick)
            {
                tool.StuckTicks++;
                if (tool.StuckTicks > 45)
                {
                    CompleteNativeToolBlocked(tool, "movement_stuck_or_collision_blocked");
                }
            }
            else
            {
                tool.StuckTicks = 0;
                tool.LastPosition = Game1.player.Position;
            }

            return;
        }

        StopAllMovement();
        if (!tool.BeginIssued)
        {
            var farm = Game1.getFarm();
            var recheck = tool.PrimitiveKind == "water_crop"
                ? ValidateWaterCropTarget(farm, tool.Target, tool.Tool as WateringCan)
                : ValidateTillSoilTarget(farm, tool.Target, tool.Tool as Hoe);
            if (recheck.Length > 0)
            {
                CompleteNativeToolBlocked(tool, recheck[0], recheck);
                return;
            }

            SelectTool(tool.Tool);
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, tool.Target));
            Game1.player.lastClick = new Vector2(tool.Target.X * Game1.tileSize, tool.Target.Y * Game1.tileSize);
            Game1.player.BeginUsingTool();
            tool.BeginIssued = true;
            return;
        }

        if (!tool.ReleaseIssued && Game1.player.UsingTool && Game1.player.canReleaseTool)
        {
            Game1.player.EndUsingTool();
            tool.ReleaseIssued = true;
            return;
        }

        if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }

        CompleteNativeTool(tool);
    }

    private void CompleteNativeToolBlocked(ActiveNativeFarmTool tool, string reason, string[]? reasons = null)
    {
        CleanupBlockedNativeToolLifecycle(tool);
        activeNativeFarmTool = null;
        tool.Pending.Completion.SetResult(NativeToolBlocked(tool.Pending.Request, tool.PrimitiveKind, tool.Target, tool.Tool, tool.WaterBefore, tool.StaminaBefore, tool.StartedAt, tool.EstimatedTicks, reason, tool.RequestedEffect, NativeToolObservedEffect(tool), reasons, tool.ElapsedTicks));
    }

    private void CleanupBlockedNativeToolLifecycle(ActiveNativeFarmTool tool)
    {
        StopAllMovement();
        if (!tool.BeginIssued || !ReferenceEquals(Game1.player.CurrentTool, tool.Tool))
        {
            return;
        }

        Game1.player.completelyStopAnimatingOrDoingAction();
    }

    private void CompleteNativeTool(ActiveNativeFarmTool tool)
    {
        StopAllMovement();
        activeNativeFarmTool = null;

        var farm = Game1.getFarm();
        var verified = tool.PrimitiveKind == "water_crop"
            ? !tool.BeforeWatered.GetValueOrDefault() && IsCropWatered(farm, tool.Target)
            : !tool.BeforeHadHoeDirt.GetValueOrDefault() && farm.terrainFeatures.TryGetValue(new Vector2(tool.Target.X, tool.Target.Y), out var feature) && feature is HoeDirt;
        var failureCategory = verified ? string.Empty : "unchanged_postcondition";
        var waterAfter = tool.Tool is WateringCan can ? can.WaterLeft : (int?)null;
        var afterWatered = tool.PrimitiveKind == "water_crop" ? IsCropWatered(farm, tool.Target) : (bool?)null;
        var afterHoeDirt = tool.PrimitiveKind == "till_soil" ? farm.terrainFeatures.TryGetValue(new Vector2(tool.Target.X, tool.Target.Y), out var afterFeature) && afterFeature is HoeDirt : (bool?)null;

        tool.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = tool.Pending.Request.RunId,
            QueueId = tool.Pending.Request.QueueId,
            QueueItemId = tool.Pending.Request.QueueItemId,
            BeforeStateHash = tool.Pending.Request.BeforeStateHash,
            OptionId = tool.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            WateredCount = tool.PrimitiveKind == "water_crop" && verified ? 1 : 0,
            EnergyBefore = tool.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = tool.Target.X,
            TargetTileY = tool.Target.Y,
            ToolQualifiedItemId = tool.Tool.QualifiedItemId,
            ToolUpgradeLevel = tool.Tool.UpgradeLevel,
            ToolPower = Game1.player.toolPower.Value,
            WaterBefore = tool.WaterBefore,
            WaterAfter = waterAfter,
            EstimatedTicks = tool.EstimatedTicks,
            ActualTicks = tool.ElapsedTicks,
            FailureCategory = failureCategory,
            TrainingImpactScope = "executor_calibration",
            StartedAt = tool.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = tool.PrimitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? NativeToolVerifiedReasons(tool) : NativeToolMismatchReasons(tool),
            RequestedEffect = tool.RequestedEffect,
            ObservedEffect = NativeToolObservedEffect(tool) + ";move_ticks=" + Math.Min(tool.ElapsedTicks, tool.MaxMovementTicks),
            BlockReasons = verified ? Array.Empty<string>() : new[] { failureCategory },
            ChangedFacts = NativeToolChangedFacts(tool, afterWatered, afterHoeDirt, waterAfter)
        });
    }

    private static TrainingExecutionResult NativeToolBlocked(TrainingExecutionRequest request, string primitiveKind, Point target, Tool? tool, int? waterBefore, double staminaBefore, string started, int estimatedTicks, string failureCategory, string requestedEffect, string observedEffect, string[]? reasons = null, int actualTicks = 0)
    {
        var blockReasons = reasons is { Length: > 0 } ? reasons : new[] { failureCategory };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ToolQualifiedItemId = tool?.QualifiedItemId ?? string.Empty,
            ToolUpgradeLevel = tool?.UpgradeLevel,
            ToolPower = Game1.player.toolPower.Value,
            WaterBefore = waterBefore,
            WaterAfter = tool is WateringCan can ? can.WaterLeft : null,
            EstimatedTicks = estimatedTicks,
            ActualTicks = actualTicks,
            FailureCategory = failureCategory,
            TrainingImpactScope = "executor_calibration",
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = blockReasons,
            RequestedEffect = requestedEffect,
            ObservedEffect = observedEffect,
            BlockReasons = blockReasons
        };
    }

    private static string NativeToolObservedEffect(ActiveNativeFarmTool tool)
    {
        var farm = Game1.getFarm();
        return tool.PrimitiveKind == "water_crop"
            ? WaterCropObservedEffect(farm, tool.Target)
            : TillSoilObservedEffect(farm, tool.Target);
    }

    private static string[] NativeToolVerifiedReasons(ActiveNativeFarmTool tool)
    {
        return tool.PrimitiveKind == "water_crop"
            ? new[] { "native_watering_can_lifecycle_watered_target_crop" }
            : new[] { "native_hoe_lifecycle_created_hoe_dirt" };
    }

    private static string[] NativeToolMismatchReasons(ActiveNativeFarmTool tool)
    {
        return tool.PrimitiveKind == "water_crop"
            ? new[] { "target_crop_water_state_unchanged_after_native_tool_lifecycle" }
            : new[] { "target_tile_unchanged_after_native_hoe_lifecycle" };
    }

    private static SimulatedFactChange[] NativeToolChangedFacts(ActiveNativeFarmTool tool, bool? afterWatered, bool? afterHoeDirt, int? waterAfter)
    {
        var changes = new List<SimulatedFactChange>
        {
            new() { Path = "player.energy", Before = tool.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
        };

        if (tool.PrimitiveKind == "water_crop")
        {
            changes.Insert(0, new SimulatedFactChange { Path = "farm.crops[" + tool.Target.X + "," + tool.Target.Y + "].watered", Before = tool.BeforeWatered.GetValueOrDefault().ToString().ToLowerInvariant(), After = afterWatered.GetValueOrDefault().ToString().ToLowerInvariant() });
            changes.Add(new SimulatedFactChange { Path = "player.watering_can.water_left", Before = tool.WaterBefore?.ToString() ?? "missing", After = waterAfter?.ToString() ?? "missing" });
        }
        else
        {
            changes.Insert(0, new SimulatedFactChange { Path = "farm.terrain_features[" + tool.Target.X + "," + tool.Target.Y + "].type", Before = tool.BeforeHadHoeDirt.GetValueOrDefault() ? "HoeDirt" : "none", After = afterHoeDirt.GetValueOrDefault() ? "HoeDirt" : "none" });
        }

        return changes.ToArray();
    }

    private static void SelectTool(Tool tool)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (ReferenceEquals(Game1.player.Items[index], tool))
            {
                Game1.player.CurrentToolIndex = index;
                return;
            }
        }
    }

    private static bool IsCropWatered(Farm farm, Point target)
    {
        return farm.terrainFeatures.TryGetValue(new Vector2(target.X, target.Y), out var feature) &&
            feature is HoeDirt dirt &&
            dirt.isWatered();
    }

    private static int EstimateRuntimeToolTicks(Point target)
    {
        return Math.Max(0, ManhattanDistance(Game1.player.TilePoint, target) - 1) * 30 + 85;
    }

    private static string WaterCropRequestedEffect(Point target)
    {
        return "farm.crops[" + target.X + "," + target.Y + "].needs_watering=false;native_tool=WateringCan";
    }

    private static string WaterCropObservedEffect(Farm farm, Point target)
    {
        var tile = new Vector2(target.X, target.Y);
        var water = FindTool<WateringCan>();
        var cropState = farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt
            ? "has_hoe_dirt=true;has_crop=" + (dirt.crop is not null).ToString().ToLowerInvariant() + ";watered=" + dirt.isWatered().ToString().ToLowerInvariant() + ";needs_watering=" + dirt.needsWatering().ToString().ToLowerInvariant()
            : "has_hoe_dirt=false";
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";" + cropState + ";water_left=" + (water?.WaterLeft.ToString() ?? "missing");
    }

    private static string TillSoilRequestedEffect(Point target)
    {
        return "farm.terrain_features[" + target.X + "," + target.Y + "].type=HoeDirt;native_tool=Hoe";
    }

    private static string TillSoilObservedEffect(Farm farm, Point? target)
    {
        if (!target.HasValue)
        {
            return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";target=missing";
        }

        var tile = new Vector2(target.Value.X, target.Value.Y);
        var feature = farm.terrainFeatures.TryGetValue(tile, out var existing) ? existing.GetType().Name : "none";
        var obj = farm.objects.ContainsKey(tile).ToString().ToLowerInvariant();
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";terrain_feature=" + feature + ";object_present=" + obj;
    }

    private TrainingExecutionResult ExecuteFaceDirection(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "face_direction", "player.facing_direction=" + (request.Direction?.ToString() ?? "missing"), "player.facing_direction=" + Game1.player.FacingDirection, reasons.ToArray());
        }

        if (!request.Direction.HasValue || request.Direction.Value < 0 || request.Direction.Value > 3)
        {
            return BlockedWithPrimitive(request, "face_direction", "player.facing_direction=" + (request.Direction?.ToString() ?? "missing"), "player.facing_direction=" + Game1.player.FacingDirection, "direction_0_3_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var before = Game1.player.FacingDirection;
        Game1.player.faceDirection(request.Direction.Value);
        var observed = Game1.player.FacingDirection;
        var verified = observed == request.Direction.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "face_direction",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "facing_direction_matches_request" } : new[] { "facing_direction_mismatch" },
            RequestedEffect = "player.facing_direction=" + request.Direction.Value,
            ObservedEffect = "player.facing_direction=" + observed,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "facing_direction_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.facing_direction",
                    Before = before.ToString(),
                    After = observed.ToString()
                }
            }
        };
    }

    private TrainingExecutionResult ExecuteSelectSafeItemSlot(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var requested = request.SafeSlotIndex?.ToString() ?? "missing";
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "select_safe_item_slot", "player.current_tool_index=" + requested, SafeSlotObservedEffect(), reasons.ToArray());
        }

        if (!request.SafeSlotIndex.HasValue || request.SafeSlotIndex.Value < 0 || request.SafeSlotIndex.Value > 11)
        {
            return BlockedWithPrimitive(request, "select_safe_item_slot", "player.current_tool_index=" + requested, SafeSlotObservedEffect(), "safe_slot_index_0_11_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeIndex = Game1.player.CurrentToolIndex;
        var beforeActiveObject = Game1.player.ActiveObject?.QualifiedItemId ?? string.Empty;
        Game1.player.CurrentToolIndex = request.SafeSlotIndex.Value;
        var observedIndex = Game1.player.CurrentToolIndex;
        var observedActiveObject = Game1.player.ActiveObject?.QualifiedItemId ?? string.Empty;
        var verified = observedIndex == request.SafeSlotIndex.Value && string.IsNullOrEmpty(observedActiveObject);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "select_safe_item_slot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "current_tool_index_matches_safe_slot", "active_object_cleared" } : new[] { "safe_slot_selection_mismatch" },
            RequestedEffect = "player.current_tool_index=" + request.SafeSlotIndex.Value + ";player.active_object_qualified_id=null",
            ObservedEffect = SafeSlotObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "safe_slot_selection_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.current_tool_index",
                    Before = beforeIndex.ToString(),
                    After = observedIndex.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.active_object_qualified_id",
                    Before = beforeActiveObject,
                    After = observedActiveObject
                }
            }
        };
    }

    private static string SafeSlotObservedEffect()
    {
        return "player.current_tool_index=" + Game1.player.CurrentToolIndex + ";player.active_object_qualified_id=" + (Game1.player.ActiveObject?.QualifiedItemId ?? "null");
    }

    private TrainingExecutionResult ExecuteCloseMenu(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var menu = Game1.activeClickableMenu;
        var beforeOpen = menu is not null;
        var beforeType = menu?.GetType().Name ?? "none";
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), reasons.ToArray());
        }

        if (menu is null)
        {
            return CompletedCloseMenu(request, beforeOpen, beforeType, "no_op", "verified_no_active_menu", new[] { "active_menu_already_closed" });
        }

        if (beforeType == "DialogueBox" && menu is DialogueBox unsafeBox && !CanAdvanceOrdinaryDialogue(unsafeBox))
        {
            var unsafeReasons = new List<string>();
            if (unsafeBox.isQuestion) unsafeReasons.Add("dialogue_is_question_true");
            if (unsafeBox.responses is { Length: > 0 }) unsafeReasons.Add("dialogue_responses_present:" + unsafeBox.responses.Length);
            if (Game1.eventUp) unsafeReasons.Add("dialogue_event_up_true");
            if (unsafeBox.characterDialogue is null) unsafeReasons.Add("dialogue_character_missing");
            else if (string.IsNullOrWhiteSpace(unsafeBox.characterDialogue.speaker?.Name)) unsafeReasons.Add("dialogue_speaker_name_missing_or_empty");
            if (!string.IsNullOrWhiteSpace(Game1.currentLocation?.lastQuestionKey)) unsafeReasons.Add("dialogue_last_question_key_present:" + Game1.currentLocation.lastQuestionKey);
            if (unsafeBox.transitioning) unsafeReasons.Add("dialogue_transitioning_true");
            var beforeSpeakerName = unsafeBox.characterDialogue?.speaker?.Name ?? string.Empty;
            return new TrainingExecutionResult
            {
                RunId = request.RunId,
                QueueId = request.QueueId,
                QueueItemId = request.QueueItemId,
                BeforeStateHash = request.BeforeStateHash,
                OptionId = request.OptionId,
                Status = "blocked",
                FeedbackAvailable = true,
                StartedAt = DateTimeOffset.UtcNow.ToString("O"),
                CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
                PrimitiveKind = "close_menu",
                PrimitiveVerificationStatus = "blocked",
                PrimitiveVerificationReasons = unsafeReasons.ToArray(),
                RequestedEffect = "menus.active_menu.is_open=false",
                ObservedEffect = CloseMenuObservedEffect(),
                BlockReasons = unsafeReasons.ToArray(),
                DialogueNativeHandled = false,
                DialoguePressAttempts = 0,
                DialogueAdvanceTicks = 0,
                DialogueMenuTypeBefore = "DialogueBox",
                DialogueMenuTypeAfter = "DialogueBox",
                DialogueIsQuestionBefore = unsafeBox.isQuestion,
                DialogueIsQuestionAfter = unsafeBox.isQuestion,
                DialogueResponseCountBefore = unsafeBox.responses?.Length ?? 0,
                DialogueResponseCountAfter = unsafeBox.responses?.Length ?? 0,
                DialogueSpeakerNameBefore = beforeSpeakerName,
                DialogueSpeakerNameAfter = beforeSpeakerName,
                DialogueEventUpBefore = Game1.eventUp,
                DialogueEventUpAfter = Game1.eventUp,
                ChangedFacts = new[]
                {
                    new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = "true", After = "true" },
                    new SimulatedFactChange { Path = "menus.active_menu.type", Before = "DialogueBox", After = "DialogueBox" }
                }
            };
        }

        if (!IsSafeCloseMenuType(beforeType))
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), "close_menu_type_not_whitelisted");
        }

        if (!menu.readyToClose())
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), "menu_not_ready_to_close");
        }

        Game1.exitActiveMenu();
        var verified = Game1.activeClickableMenu is null;
        return CompletedCloseMenu(
            request,
            beforeOpen,
            beforeType,
            verified ? "applied" : "blocked",
            verified ? "verified" : "observed_mismatch",
            verified ? new[] { "active_menu_closed" } : new[] { "active_menu_still_open" });
    }

    private static bool CanAdvanceOrdinaryDialogue(DialogueBox dialogueBox)
    {
        return !dialogueBox.isQuestion &&
            (dialogueBox.responses is null || dialogueBox.responses.Length == 0) &&
            !string.Equals(Game1.currentLocation?.lastQuestionKey, "Sleep", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(Game1.currentLocation?.lastQuestionKey) &&
            !Game1.eventUp &&
            dialogueBox.characterDialogue is not null &&
            !string.IsNullOrWhiteSpace(dialogueBox.characterDialogue.speaker?.Name);
    }

    private void StartDialogueAdvance(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(pending.Request, reasons.ToArray()));
            return;
        }

        var menu = Game1.activeClickableMenu;
        if (menu is not DialogueBox dialogueBox || !CanAdvanceOrdinaryDialogue(dialogueBox))
        {
            pending.Completion.SetResult(ExecuteCloseMenu(pending.Request));
            return;
        }

        if (activeDialogueAdvance is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request, "close_menu",
                "menus.active_menu.is_open=false",
                CloseMenuObservedEffect(),
                "dialogue_advance_executor_busy"));
            return;
        }

        activeDialogueAdvance = new ActiveDialogueAdvance(pending, dialogueBox);
        Monitor.Log($"Started native dialogue advance: isQuestion={dialogueBox.isQuestion}, responses={dialogueBox.responses?.Length ?? 0}, transitioning={dialogueBox.transitioning}, safetyTimer={dialogueBox.safetyTimer}, eventUp={Game1.eventUp}, speaker={dialogueBox.characterDialogue?.speaker?.Name ?? "none"}", LogLevel.Info);
    }

    private void TickDialogueAdvance()
    {
        if (activeDialogueAdvance is null)
        {
            return;
        }

        var advance = activeDialogueAdvance;
        advance.ElapsedTicks++;

        try
        {
            TickDialogueAdvanceCore(advance);
        }
        catch (Exception ex)
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_advance_exception:" + ex.GetType().Name,
                new[] { "dialogue_advance_exception:" + ex.GetType().Name + ":" + ex.Message }));
        }
    }

    private void TickDialogueAdvanceCore(ActiveDialogueAdvance advance)
    {
        if (advance.ElapsedTicks > advance.MaxTicks)
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_advance_timeout",
                new[] { "dialogue_advance_timeout" }));
            return;
        }

        var currentBox = Game1.activeClickableMenu as DialogueBox;

        if (!ReferenceEquals(currentBox, advance.InitialMenu))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            var verified = Game1.activeClickableMenu is null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance,
                verified ? "applied" : "blocked",
                verified ? "verified" : "observed_mismatch",
                verified ? "dialogue_advanced_and_closed_natively" : "dialogue_menu_instance_changed_during_advance",
                verified
                    ? new[] { "dialogue_advanced_and_closed_natively", "press_attempts=" + advance.PressAttempts, "advance_ticks=" + advance.ElapsedTicks }
                    : new[] { "dialogue_menu_instance_changed_during_advance", "type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") }));
            return;
        }

        if (!string.Equals(currentBox.characterDialogue?.speaker?.Name, advance.InitialSpeakerName, StringComparison.Ordinal))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_speaker_changed_during_advance",
                new[] { "dialogue_speaker_changed_during_advance:expected=" + advance.InitialSpeakerName + ";actual=" + (currentBox.characterDialogue?.speaker?.Name ?? "null") }));
            return;
        }

        if (!CanAdvanceOrdinaryDialogue(currentBox))
        {
            ReleaseSmapiLeftButtonOverride();
            activeDialogueAdvance = null;
            advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                advance, "blocked", "blocked", "dialogue_became_unsafe_during_advance",
                new[] { "dialogue_became_unsafe_during_advance:isQuestion=" + currentBox.isQuestion + ";responses=" + (currentBox.responses?.Length ?? 0) + ";lastQuestionKey=" + (Game1.currentLocation?.lastQuestionKey ?? "null") + ";eventUp=" + Game1.eventUp }));
            return;
        }

        switch (advance.Stage)
        {
            case DialogueAdvanceStage.WaitTransition:
                if (currentBox.transitioning || currentBox.safetyTimer > 0)
                {
                    advance.TransitionWaitTicks++;
                    return;
                }

                advance.Stage = DialogueAdvanceStage.Press;
                break;

            case DialogueAdvanceStage.Press:
                if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_input_press_failed",
                        new[] { "dialogue_advance_input_press_failed:" + pressReason }));
                    return;
                }

                advance.PressAttempts++;
                advance.SawDialogueFinishedBeforePress = currentBox.dialogueFinished;
                advance.SawShowTypingBeforePress = currentBox.showTyping;
                advance.SawTransitioningBeforePress = currentBox.transitioning;
                advance.Stage = DialogueAdvanceStage.ReleaseAfterAdvance;
                break;

            case DialogueAdvanceStage.ReleaseAfterAdvance:
                if (!TryApplySmapiLeftButtonOverride(pressed: false, out var releaseReason))
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_input_release_failed",
                        new[] { "dialogue_advance_input_release_failed:" + releaseReason }));
                    return;
                }

                advance.AdvanceWaitTicks = 0;
                advance.Stage = DialogueAdvanceStage.WaitAdvanceEffect;
                break;

            case DialogueAdvanceStage.WaitAdvanceEffect:
                advance.AdvanceWaitTicks++;
                var dialogueChanged = currentBox.dialogueFinished != advance.SawDialogueFinishedBeforePress ||
                    currentBox.showTyping != advance.SawShowTypingBeforePress ||
                    currentBox.transitioning != advance.SawTransitioningBeforePress;
                if (dialogueChanged || advance.AdvanceWaitTicks > 30)
                {
                    advance.Stage = DialogueAdvanceStage.CheckClose;
                }

                break;

            case DialogueAdvanceStage.CheckClose:
                advance.CheckCloseTicks++;
                if (advance.PressAttempts >= advance.MaxPressAttempts)
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeDialogueAdvance = null;
                    advance.Pending.Completion.SetResult(DialogueAdvanceResult(
                        advance, "blocked", "blocked", "dialogue_advance_max_press_exhausted",
                        new[] { "dialogue_advance_max_press_exhausted:" + advance.PressAttempts }));
                    return;
                }

                if (currentBox.transitioning || currentBox.safetyTimer > 0)
                {
                    advance.Stage = DialogueAdvanceStage.WaitTransition;
                    return;
                }

                advance.Stage = DialogueAdvanceStage.Press;
                break;
        }
    }

    private static TrainingExecutionResult DialogueAdvanceResult(
        ActiveDialogueAdvance advance,
        string status,
        string verificationStatus,
        string primaryReason,
        string[] allReasons)
    {
        var observedMenu = Game1.activeClickableMenu;
        var observedType = observedMenu?.GetType().Name ?? "none";
        var observedBox = observedMenu as DialogueBox;
        return new TrainingExecutionResult
        {
            RunId = advance.Pending.Request.RunId,
            QueueId = advance.Pending.Request.QueueId,
            QueueItemId = advance.Pending.Request.QueueItemId,
            BeforeStateHash = advance.Pending.Request.BeforeStateHash,
            OptionId = advance.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = advance.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = allReasons,
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = CloseMenuObservedEffect() + ";dialogue_press_attempts=" + advance.PressAttempts + ";advance_ticks=" + advance.ElapsedTicks,
            BlockReasons = status == "blocked" ? allReasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = "true", After = (observedMenu is not null).ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = advance.BeforeMenuType, After = observedType }
            },
            DialogueNativeHandled = true,
            DialoguePressAttempts = advance.PressAttempts,
            DialogueAdvanceTicks = advance.ElapsedTicks,
            DialogueMenuTypeBefore = advance.BeforeMenuType,
            DialogueMenuTypeAfter = observedType,
            DialogueIsQuestionBefore = advance.BeforeIsQuestion,
            DialogueIsQuestionAfter = observedBox?.isQuestion,
            DialogueResponseCountBefore = advance.BeforeResponseCount,
            DialogueResponseCountAfter = observedBox?.responses?.Length,
            DialogueSpeakerNameBefore = advance.BeforeSpeakerName,
            DialogueSpeakerNameAfter = observedBox?.characterDialogue?.speaker?.Name ?? string.Empty,
            DialogueEventUpBefore = advance.BeforeEventUp,
            DialogueEventUpAfter = Game1.eventUp
        };
    }

    private static TrainingExecutionResult CompletedCloseMenu(TrainingExecutionRequest request, bool beforeOpen, string beforeType, string status, string verificationStatus, string[] verificationReasons)
    {
        var observedOpen = Game1.activeClickableMenu is not null;
        var observedType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = CloseMenuObservedEffect(),
            BlockReasons = status == "blocked" ? verificationReasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.is_open",
                    Before = beforeOpen.ToString().ToLowerInvariant(),
                    After = observedOpen.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.type",
                    Before = beforeType,
                    After = observedType
                }
            }
        };
    }

    private static string CloseMenuObservedEffect()
    {
        return "menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() + ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }

    private TrainingExecutionResult ExecuteInteract(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_target_tile_required");
        }

        if (!string.Equals(request.InteractionKind, "map_action", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_kind_unsupported");
        }

        if (!IsInteractActionTypeWhitelisted(request.ExpectedActionType))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_expected_action_type_not_whitelisted");
        }

        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_target_not_adjacent");
        }

        if (Game1.activeClickableMenu is not null)
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_menu_must_be_clear");
        }

        var rawAction = Game1.currentLocation.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings");
        if (string.IsNullOrWhiteSpace(rawAction))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_action_property_missing");
        }

        var actionType = rawAction.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!string.Equals(actionType, request.ExpectedActionType, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "interact", InteractRequestedEffect(request), InteractObservedEffect(), "interact_expected_action_type_mismatch");
        }

        var beforeMenuOpen = Game1.activeClickableMenu is not null;
        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeLocation = Game1.currentLocation.NameOrUniqueName;
        var beforeTile = Game1.player.TilePoint;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        var afterMenuOpen = Game1.activeClickableMenu is not null;
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterLocation = Game1.currentLocation.NameOrUniqueName;
        var afterTile = Game1.player.TilePoint;
        var verified = handled && (afterMenuOpen != beforeMenuOpen || !string.Equals(afterMenuType, beforeMenuType, StringComparison.Ordinal) || !string.Equals(afterLocation, beforeLocation, StringComparison.Ordinal) || afterTile != beforeTile);
        var verificationReasons = verified
            ? new[] { "map_action_handled", "observable_state_changed" }
            : new[] { handled ? "map_action_handled_without_observable_change" : "map_action_not_handled" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "interact",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request),
            ObservedEffect = InteractObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = beforeMenuOpen.ToString().ToLowerInvariant(), After = afterMenuOpen.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType },
                new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = afterLocation },
                new SimulatedFactChange { Path = "player.tile", Before = beforeTile.X + "," + beforeTile.Y, After = afterTile.X + "," + afterTile.Y }
            }
        };
    }

    private static bool IsInteractActionTypeWhitelisted(string actionType)
    {
        return actionType is "OpenShop" or "Buy" or "JojaShop" or "Blacksmith" or "Carpenter" or "AnimalShop" or "AdventureShop";
    }

    private TrainingExecutionResult ExecuteSocialInteract(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", reasons.ToArray());
        }

        var npcName = request.SocialNpcName;
        var actionKind = request.SocialActionKind;
        if (string.IsNullOrWhiteSpace(npcName))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_name_required");
        }
        if (actionKind != "talk" && actionKind != "gift")
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_action_kind_talk_or_gift_required");
        }

        if (string.IsNullOrWhiteSpace(request.LocationId))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_location_id_required");
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_location_id_mismatch");
        }

        if (Game1.activeClickableMenu is not null)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_interact_menu_must_be_clear");
        }

        if (!request.SocialObservedNpcTileX.HasValue || !request.SocialObservedNpcTileY.HasValue)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_coordinates_required");
        }

        var npc = Game1.currentLocation.characters
            .FirstOrDefault(character => string.Equals(character.Name, npcName, StringComparison.Ordinal));
        if (npc is null)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_not_in_current_location");
        }

        var npcTile = npc.TilePoint;
        if (npcTile.X != request.SocialObservedNpcTileX.Value ||
            npcTile.Y != request.SocialObservedNpcTileY.Value)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_moved_from_observed_tile");
        }

        if (!npc.IsVillager)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_not_ordinary_villager");
        }

        if (npc.IsMonster)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_is_monster");
        }

        if (npc.IsInvisible)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_invisible");
        }

        if (npc.isSleeping.Value)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_sleeping");
        }

        if (!npc.CanSocialize)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_cannot_socialize");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - npcTile.X) + Math.Abs(playerTile.Y - npcTile.Y) != 1)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_player_not_adjacent_to_npc");
        }

        var actionTargetRectangle = new XnaRectangle(npcTile.X * 64, npcTile.Y * 64, 64, 64);
        if (!npc.GetBoundingBox().Intersects(actionTargetRectangle))
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_not_intersecting_action_target_rectangle");
        }

        if (actionKind == "talk" && Game1.player.ActiveObject is not null)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_talk_active_object_must_be_cleared_first");
        }

        var beforeNpcLocation = Game1.currentLocation.NameOrUniqueName;
        var beforeNpcTile = npc.TilePoint;
        var beforeNpcVisible = !npc.IsInvisible;
        var beforeNpcSleeping = npc.isSleeping.Value;
        var beforeNpcOrdinary = npc.IsVillager && !npc.IsMonster;
        var beforePlayerTile = Game1.player.TilePoint;
        var beforeFacing = Game1.player.FacingDirection;
        var beforeSelectedSlot = Game1.player.CurrentToolIndex;
        var beforeMenuOpen = Game1.activeClickableMenu is not null;
        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeDialogueOpen = Game1.dialogueUp;
        var beforeDialogueSpeakerName = Game1.currentSpeaker?.Name ?? string.Empty;
        var beforeCurrentDialogue = npc.CurrentDialogue;
        var beforeDialogueCount = beforeCurrentDialogue?.Count ?? 0;
        var beforeDialogueKey = beforeCurrentDialogue is not null && beforeCurrentDialogue.Count > 0 ? beforeCurrentDialogue.Peek().TranslationKey : string.Empty;

        var beforeTalkedToToday = false;
        var beforeGiftsToday = 0;
        var beforeGiftsThisWeek = 0;
        var beforePoints = 0;
        var beforeFriendshipRowExists = false;
        if (Game1.player.friendshipData.TryGetValue(npcName, out var friendshipEntry))
        {
            beforeFriendshipRowExists = true;
            beforeTalkedToToday = friendshipEntry.TalkedToToday;
            beforeGiftsToday = friendshipEntry.GiftsToday;
            beforeGiftsThisWeek = friendshipEntry.GiftsThisWeek;
            beforePoints = friendshipEntry.Points;
        }

        int? beforeGiftStack = null;
        string beforeGiftItemId = string.Empty;
        int? beforeGiftQuality = null;
        int? beforeGiftSlot = null;

        if (actionKind == "gift")
        {
            var slotIndex = request.SocialGiftSlotIndex ?? -1;
            if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_slot_index_invalid");
            }

            var item = Game1.player.Items[slotIndex];
            if (item is null)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_slot_empty");
            }

            if (!string.IsNullOrWhiteSpace(request.SocialGiftQualifiedItemId) &&
                !string.Equals(item.QualifiedItemId, request.SocialGiftQualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_item_id_mismatch");
            }

            if (item.Stack <= 0)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_stack_empty");
            }

            if (!npc.CanReceiveGifts())
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_cannot_receive_gifts");
            }

            var isStardropTea = string.Equals(item.QualifiedItemId, "(O)StardropTea", StringComparison.Ordinal);
            var isSpouse = string.Equals(Game1.player.spouse, npcName, StringComparison.Ordinal);
            var isBirthday = npc.isBirthday();
            var dailyExhausted = beforeGiftsToday >= 1;
            var weeklyExhausted = beforeGiftsThisWeek >= 2;
            if (dailyExhausted && !isStardropTea)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_daily_limit_exhausted");
            }
            if (weeklyExhausted && !isSpouse && !isBirthday && !isStardropTea)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_weekly_limit_exhausted");
            }

            beforeGiftStack = item.Stack;
            beforeGiftItemId = item.QualifiedItemId;
            beforeGiftQuality = (item as StardewValley.Object)?.Quality;
            beforeGiftSlot = slotIndex;
        }

        var startTicks = Game1.ticks;
        var startedAt = DateTimeOffset.UtcNow.ToString("O");

        if (actionKind == "gift" && beforeGiftSlot.HasValue)
        {
            Game1.player.CurrentToolIndex = beforeGiftSlot.Value;

            var item = Game1.player.Items[beforeGiftSlot.Value];
            if (Game1.player.ActiveObject is null ||
                Game1.player.ActiveObject.QualifiedItemId != item?.QualifiedItemId ||
                Game1.player.ActiveObject.Stack != (item?.Stack ?? 0))
            {
                Game1.player.CurrentToolIndex = beforeSelectedSlot;
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_active_object_not_selected");
            }
        }

        Game1.player.faceDirection(DirectionTo(beforePlayerTile, npcTile));

        var viewportRect = new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(npcTile.X, npcTile.Y),
            viewportRect,
            Game1.player);

        var endTicks = Game1.ticks;
        var actualTicks = Math.Max(0, endTicks - startTicks);
        var completedAt = DateTimeOffset.UtcNow.ToString("O");

        var afterFacing = Game1.player.FacingDirection;
        var afterSelectedSlot = Game1.player.CurrentToolIndex;
        var afterMenuOpen = Game1.activeClickableMenu is not null;
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterDialogueOpen = Game1.dialogueUp;
        var afterDialogueSpeakerName = Game1.currentSpeaker?.Name ?? string.Empty;

        var afterNpcMember = Game1.currentLocation.characters
            .FirstOrDefault(character => string.Equals(character.Name, npcName, StringComparison.Ordinal));
        var afterNpcPresent = afterNpcMember is not null;
        var afterNpcLocation = afterNpcPresent ? Game1.currentLocation.NameOrUniqueName : string.Empty;
        var afterNpcTile = afterNpcMember is not null ? afterNpcMember.TilePoint : (Point?)null;
        var afterNpcVisible = afterNpcMember is not null ? !afterNpcMember.IsInvisible : (bool?)null;
        var afterNpcSleeping = afterNpcMember is not null ? afterNpcMember.isSleeping.Value : (bool?)null;
        var afterNpcOrdinary = afterNpcMember is not null ? afterNpcMember.IsVillager && !afterNpcMember.IsMonster : (bool?)null;
        var afterPlayerTile = Game1.player.TilePoint;
        var afterCurrentDialogue = afterNpcMember?.CurrentDialogue;
        var afterDialogueCount = afterCurrentDialogue?.Count;
        var afterDialogueKey = afterCurrentDialogue is not null && afterCurrentDialogue.Count > 0
            ? afterCurrentDialogue.Peek().TranslationKey : string.Empty;

        var afterTalkedToToday = false;
        var afterGiftsToday = 0;
        var afterGiftsThisWeek = 0;
        var afterPoints = 0;
        var afterFriendshipRowExists = false;
        if (Game1.player.friendshipData.TryGetValue(npcName, out var afterFriendshipEntry))
        {
            afterFriendshipRowExists = true;
            afterTalkedToToday = afterFriendshipEntry.TalkedToToday;
            afterGiftsToday = afterFriendshipEntry.GiftsToday;
            afterGiftsThisWeek = afterFriendshipEntry.GiftsThisWeek;
            afterPoints = afterFriendshipEntry.Points;
        }

        int? afterGiftStack = null;
        string afterGiftItemId = string.Empty;
        int? afterGiftQuality = null;
        int? afterGiftSlot = null;

        if (actionKind == "gift" && beforeGiftSlot.HasValue && beforeGiftSlot.Value < Game1.player.Items.Count)
        {
            var afterItem = Game1.player.Items[beforeGiftSlot.Value];
            afterGiftStack = afterItem?.Stack;
            afterGiftItemId = afterItem?.QualifiedItemId ?? string.Empty;
            afterGiftQuality = (afterItem as StardewValley.Object)?.Quality;
            afterGiftSlot = beforeGiftSlot.Value;
        }

        if (actionKind == "talk" && !handled)
        {
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "blocked",
                "observed_mismatch", new[] { "native_checkAction_not_handled_for_talk" },
                "native_checkAction_not_handled_for_talk", "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "talk" && handled)
        {
            var hasTalkChange = afterTalkedToToday != beforeTalkedToToday;
            var hasDialogueChange = afterDialogueCount != beforeDialogueCount ||
                !string.Equals(afterDialogueKey, beforeDialogueKey, StringComparison.Ordinal) ||
                afterDialogueOpen != beforeDialogueOpen ||
                !string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal);
            var hasFriendshipChange = afterPoints != beforePoints;
            var socialTransitionObserved = hasTalkChange || hasDialogueChange || hasFriendshipChange ||
                afterMenuOpen != beforeMenuOpen ||
                afterMenuType != beforeMenuType;

            if (!socialTransitionObserved)
            {
                return BuildSocialInteractResult(request, handled, npcName,
                    beforeNpcLocation, afterNpcLocation,
                    beforeNpcTile, afterNpcTile,
                    beforeNpcVisible, afterNpcVisible,
                    beforeNpcSleeping, afterNpcSleeping,
                    beforeNpcOrdinary, afterNpcOrdinary,
                    afterNpcPresent,
                    beforePlayerTile, afterPlayerTile,
                    beforeFacing, afterFacing,
                    beforeSelectedSlot, afterSelectedSlot,
                    beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                    beforeDialogueOpen, afterDialogueOpen,
                    beforeDialogueCount, afterDialogueCount,
                    beforeDialogueKey, afterDialogueKey,
                    beforeDialogueSpeakerName, afterDialogueSpeakerName,
                    beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                    beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                    beforeFriendshipRowExists, afterFriendshipRowExists,
                    beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                    beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                    true, "blocked",
                    "observed_mismatch", new[] { "native_handled_but_no_social_transition_observed" },
                    "native_handled_but_no_social_transition_observed", "executor_calibration",
                    startedAt, completedAt, actualTicks);
            }

            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "applied",
                "verified", new[] { "native_talk_handled", "observable_social_transition" },
                string.Empty, string.Empty,
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "gift" && !handled)
        {
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "blocked",
                "observed_mismatch", new[] { "native_checkAction_not_handled_for_gift" },
                "native_checkAction_not_handled_for_gift", "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "gift")
        {
            bool itemConsumed;
            if (!afterGiftStack.HasValue)
            {
                itemConsumed = beforeGiftStack.HasValue && beforeGiftStack.Value == 1;
            }
            else
            {
                itemConsumed = beforeGiftStack.HasValue &&
                    string.Equals(afterGiftItemId, beforeGiftItemId, StringComparison.Ordinal) &&
                    afterGiftStack.Value == beforeGiftStack.Value - 1;
            }

            if (!itemConsumed && afterGiftStack.HasValue && afterGiftStack.Value > 0 &&
                beforeGiftSlot.HasValue && beforeGiftSlot.Value < Game1.player.Items.Count)
            {
                var slotItem = Game1.player.Items[beforeGiftSlot.Value];
                if (slotItem is not null &&
                    !string.Equals(slotItem.QualifiedItemId, beforeGiftItemId, StringComparison.Ordinal))
                {
                    itemConsumed = false;
                }
            }

            var hasDialogueChange = afterDialogueCount != beforeDialogueCount ||
                !string.Equals(afterDialogueKey, beforeDialogueKey, StringComparison.Ordinal) ||
                afterDialogueOpen != beforeDialogueOpen ||
                !string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal);
            var hasFriendshipChange = afterPoints != beforePoints;
            var hasGiftCounterChange = afterGiftsToday != beforeGiftsToday || afterGiftsThisWeek != beforeGiftsThisWeek;
            var hasSocialEffect = hasDialogueChange || hasFriendshipChange || hasGiftCounterChange ||
                afterMenuOpen != beforeMenuOpen || afterMenuType != beforeMenuType;

            if (!itemConsumed && handled)
            {
                return BuildSocialInteractResult(request, handled, npcName,
                    beforeNpcLocation, afterNpcLocation,
                    beforeNpcTile, afterNpcTile,
                    beforeNpcVisible, afterNpcVisible,
                    beforeNpcSleeping, afterNpcSleeping,
                    beforeNpcOrdinary, afterNpcOrdinary,
                    afterNpcPresent,
                    beforePlayerTile, afterPlayerTile,
                    beforeFacing, afterFacing,
                    beforeSelectedSlot, afterSelectedSlot,
                    beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                    beforeDialogueOpen, afterDialogueOpen,
                    beforeDialogueCount, afterDialogueCount,
                    beforeDialogueKey, afterDialogueKey,
                    beforeDialogueSpeakerName, afterDialogueSpeakerName,
                    beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                    beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                    beforeFriendshipRowExists, afterFriendshipRowExists,
                    beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                    beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                    true, "blocked",
                    "observed_mismatch", new[] { "native_handled_but_gift_item_not_consumed" },
                    "native_handled_but_gift_item_not_consumed", "executor_calibration",
                    startedAt, completedAt, actualTicks);
            }

            var verified = handled && itemConsumed && hasSocialEffect;
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, verified ? "applied" : "blocked",
                verified ? "verified" : "observed_mismatch",
                verified ? new[] { "native_gift_handled", "exact_one_item_consumed", "observable_social_effect" }
                    : new[] { "native_gift_handled_but_incomplete_verification" },
                verified ? string.Empty : "native_gift_handled_but_incomplete_verification",
                "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        return BuildSocialBlockedResult(request, true, null, "social_interact", "social_unexpected_state_after_interact");
    }

    private static TrainingExecutionResult BuildSocialBlockedResult(
        TrainingExecutionRequest request, bool npcResolved, NPC? npc, string primitiveKind, params string[] reasons)
    {
        var safePlayer = Context.IsWorldReady ? Game1.player : null;
        var safeLocation = Context.IsWorldReady ? Game1.currentLocation : null;
        var allReasons = new List<string>(reasons);

        if (!Context.IsWorldReady)
        {
            allReasons.Insert(0, "world_not_ready");
        }

        var npcName = request.SocialNpcName ?? string.Empty;

        var beforePoints = 0;
        var beforeTalkedToToday = false;
        var beforeGiftsToday = 0;
        var beforeGiftsThisWeek = 0;
        bool? beforeFriendshipRowExists = null;
        if (npcResolved && !string.IsNullOrWhiteSpace(npcName) && safePlayer is not null &&
            safePlayer.friendshipData.TryGetValue(npcName, out var beforeFriendshipEntry))
        {
            beforeFriendshipRowExists = true;
            beforePoints = beforeFriendshipEntry.Points;
            beforeTalkedToToday = beforeFriendshipEntry.TalkedToToday;
            beforeGiftsToday = beforeFriendshipEntry.GiftsToday;
            beforeGiftsThisWeek = beforeFriendshipEntry.GiftsThisWeek;
        }
        else if (npcResolved && !string.IsNullOrWhiteSpace(npcName) && safePlayer is not null)
        {
            beforeFriendshipRowExists = false;
        }

        int? beforeGiftStack = null;
        string beforeGiftItemId = string.Empty;
        int? beforeGiftQuality = null;
        int? beforeGiftSlot = null;
        if (request.SocialActionKind == "gift" && request.SocialGiftSlotIndex.HasValue && safePlayer is not null)
        {
            var slotIndex = request.SocialGiftSlotIndex.Value;
            if (slotIndex >= 0 && slotIndex < safePlayer.Items.Count)
            {
                var item = safePlayer.Items[slotIndex];
                if (item is not null)
                {
                    beforeGiftStack = item.Stack;
                    beforeGiftItemId = item.QualifiedItemId;
                    beforeGiftQuality = (item as StardewValley.Object)?.Quality;
                    beforeGiftSlot = slotIndex;
                }
            }
        }

        var blockedCurrentDialogue = npc is not null ? npc.CurrentDialogue : null;
        var beforeDialogueCount = blockedCurrentDialogue?.Count;
        var beforeDialogueKey = beforeDialogueCount.GetValueOrDefault(0) > 0 && blockedCurrentDialogue is not null
            ? blockedCurrentDialogue.Peek().TranslationKey : string.Empty;

        var safePlayerTile = safePlayer?.TilePoint;
        var safeLocationName = safeLocation?.NameOrUniqueName ?? string.Empty;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = 0,
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = allReasons.ToArray(),
            RequestedEffect = SocialInteractRequestedEffect(request),
            ObservedEffect = SocialInteractObservedEffect(),
            BlockReasons = allReasons.ToArray(),
            FailureCategory = allReasons.Count > 0 ? allReasons[0] : string.Empty,
            TrainingImpactScope = "executor_calibration",
            SocialActionKind = request.SocialActionKind ?? string.Empty,
            SocialNpcName = npcName,
            SocialNativeHandled = false,
            SocialNpcPresentBefore = npcResolved ? true : null,
            SocialNpcPresentAfter = npcResolved ? true : null,
            SocialNpcLocationBefore = npcResolved && safeLocation is not null ? (npc?.currentLocation?.NameOrUniqueName ?? safeLocationName) : string.Empty,
            SocialNpcLocationAfter = npcResolved && safeLocation is not null ? (npc?.currentLocation?.NameOrUniqueName ?? safeLocationName) : string.Empty,
            SocialNpcTileXBefore = npcResolved && npc is not null ? npc.TilePoint.X : null,
            SocialNpcTileYBefore = npcResolved && npc is not null ? npc.TilePoint.Y : null,
            SocialNpcTileXAfter = npcResolved && npc is not null ? npc.TilePoint.X : null,
            SocialNpcTileYAfter = npcResolved && npc is not null ? npc.TilePoint.Y : null,
            SocialNpcVisibleBefore = npcResolved && npc is not null ? !npc.IsInvisible : null,
            SocialNpcVisibleAfter = npcResolved && npc is not null ? !npc.IsInvisible : null,
            SocialNpcSleepingBefore = npcResolved && npc is not null ? npc.isSleeping.Value : null,
            SocialNpcSleepingAfter = npcResolved && npc is not null ? npc.isSleeping.Value : null,
            SocialNpcOrdinaryBefore = npcResolved && npc is not null ? npc.IsVillager && !npc.IsMonster : null,
            SocialNpcOrdinaryAfter = npcResolved && npc is not null ? npc.IsVillager && !npc.IsMonster : null,
            SocialPlayerTileXBefore = safePlayerTile?.X,
            SocialPlayerTileYBefore = safePlayerTile?.Y,
            SocialPlayerFacingBefore = safePlayer?.FacingDirection,
            SocialPlayerSelectedSlotBefore = safePlayer?.CurrentToolIndex,
            SocialPlayerTileXAfter = safePlayerTile?.X,
            SocialPlayerTileYAfter = safePlayerTile?.Y,
            SocialPlayerFacingAfter = safePlayer?.FacingDirection,
            SocialPlayerSelectedSlotAfter = safePlayer?.CurrentToolIndex,
            SocialMenuOpenBefore = Game1.activeClickableMenu is not null,
            SocialMenuTypeBefore = Game1.activeClickableMenu?.GetType().Name ?? "none",
            SocialDialogueOpenBefore = Game1.dialogueUp,
            SocialCurrentDialogueSpeakerNameBefore = Game1.currentSpeaker?.Name ?? string.Empty,
            SocialCurrentDialogueCountBefore = beforeDialogueCount,
            SocialCurrentDialogueKeyBefore = beforeDialogueKey,
            SocialMenuOpenAfter = Game1.activeClickableMenu is not null,
            SocialMenuTypeAfter = Game1.activeClickableMenu?.GetType().Name ?? "none",
            SocialDialogueOpenAfter = Game1.dialogueUp,
            SocialCurrentDialogueSpeakerNameAfter = Game1.currentSpeaker?.Name ?? string.Empty,
            SocialCurrentDialogueCountAfter = beforeDialogueCount,
            SocialCurrentDialogueKeyAfter = beforeDialogueKey,
            SocialFriendshipPointsBefore = beforeFriendshipRowExists == true ? beforePoints : null,
            SocialFriendshipPointsAfter = beforeFriendshipRowExists == true ? beforePoints : null,
            SocialTalkedToTodayBefore = beforeFriendshipRowExists == true ? beforeTalkedToToday : null,
            SocialTalkedToTodayAfter = beforeFriendshipRowExists == true ? beforeTalkedToToday : null,
            SocialGiftsTodayBefore = beforeFriendshipRowExists == true ? beforeGiftsToday : null,
            SocialGiftsTodayAfter = beforeFriendshipRowExists == true ? beforeGiftsToday : null,
            SocialGiftsThisWeekBefore = beforeFriendshipRowExists == true ? beforeGiftsThisWeek : null,
            SocialGiftsThisWeekAfter = beforeFriendshipRowExists == true ? beforeGiftsThisWeek : null,
            SocialFriendshipRowExistsBefore = beforeFriendshipRowExists,
            SocialFriendshipRowExistsAfter = beforeFriendshipRowExists,
            SocialGiftItemIdBefore = beforeGiftItemId,
            SocialGiftItemIdAfter = beforeGiftItemId,
            SocialGiftStackBefore = beforeGiftStack,
            SocialGiftStackAfter = beforeGiftStack,
            SocialGiftQualityBefore = beforeGiftQuality,
            SocialGiftQualityAfter = beforeGiftQuality,
            SocialGiftSlotBefore = beforeGiftSlot,
            SocialGiftSlotAfter = beforeGiftSlot
        };
    }

    private static TrainingExecutionResult BuildSocialInteractResult(
        TrainingExecutionRequest request, bool handled,
        string npcName,
        string beforeNpcLocation, string afterNpcLocation,
        Point beforeNpcTile, Point? afterNpcTile,
        bool beforeNpcVisible, bool? afterNpcVisible,
        bool beforeNpcSleeping, bool? afterNpcSleeping,
        bool beforeNpcOrdinary, bool? afterNpcOrdinary,
        bool afterNpcPresent,
        Point beforePlayerTile, Point afterPlayerTile,
        int beforeFacing, int afterFacing,
        int beforeSelectedSlot, int afterSelectedSlot,
        bool beforeMenuOpen, bool afterMenuOpen, string beforeMenuType, string afterMenuType,
        bool beforeDialogueOpen, bool afterDialogueOpen,
        int beforeDialogueCount, int? afterDialogueCount,
        string beforeDialogueKey, string afterDialogueKey,
        string beforeDialogueSpeakerName, string afterDialogueSpeakerName,
        int beforePoints, int afterPoints, bool beforeTalkedToToday, bool afterTalkedToToday,
        int beforeGiftsToday, int afterGiftsToday, int beforeGiftsThisWeek, int afterGiftsThisWeek,
        bool beforeFriendshipRowExists, bool afterFriendshipRowExists,
        int? beforeGiftStack, int? afterGiftStack, string beforeGiftItemId, string afterGiftItemId,
        int? beforeGiftQuality, int? afterGiftQuality, int? beforeGiftSlot, int? afterGiftSlot,
        bool includeChangedFacts, string status, string verificationStatus, string[] verificationReasons,
        string failureCategory, string trainingImpactScope,
        string startedAt, string completedAt, int actualTicks)
    {
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ActualTicks = actualTicks,
            PrimitiveKind = "social_interact",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = SocialInteractRequestedEffect(request),
            ObservedEffect = SocialInteractObservedEffect(),
            BlockReasons = status == "blocked" ? verificationReasons : Array.Empty<string>(),
            FailureCategory = failureCategory,
            TrainingImpactScope = trainingImpactScope,
            SocialNpcName = npcName,
            SocialNpcPresentBefore = true,
            SocialNpcPresentAfter = afterNpcPresent,
            SocialNpcLocationBefore = beforeNpcLocation,
            SocialNpcLocationAfter = afterNpcPresent ? afterNpcLocation : string.Empty,
            SocialNpcTileXBefore = beforeNpcTile.X,
            SocialNpcTileYBefore = beforeNpcTile.Y,
            SocialNpcTileXAfter = afterNpcPresent ? afterNpcTile?.X : null,
            SocialNpcTileYAfter = afterNpcPresent ? afterNpcTile?.Y : null,
            SocialNpcVisibleBefore = beforeNpcVisible,
            SocialNpcVisibleAfter = afterNpcPresent ? afterNpcVisible : null,
            SocialNpcSleepingBefore = beforeNpcSleeping,
            SocialNpcSleepingAfter = afterNpcPresent ? afterNpcSleeping : null,
            SocialNpcOrdinaryBefore = beforeNpcOrdinary,
            SocialNpcOrdinaryAfter = afterNpcPresent ? afterNpcOrdinary : null,
            SocialPlayerTileXBefore = beforePlayerTile.X,
            SocialPlayerTileYBefore = beforePlayerTile.Y,
            SocialPlayerTileXAfter = afterPlayerTile.X,
            SocialPlayerTileYAfter = afterPlayerTile.Y,
            SocialPlayerFacingBefore = beforeFacing,
            SocialPlayerFacingAfter = afterFacing,
            SocialPlayerSelectedSlotBefore = beforeSelectedSlot,
            SocialPlayerSelectedSlotAfter = afterSelectedSlot,
            SocialActionKind = request.SocialActionKind,
            SocialNativeHandled = handled,
            SocialGiftItemIdBefore = beforeGiftItemId,
            SocialGiftItemIdAfter = afterGiftItemId,
            SocialGiftStackBefore = beforeGiftStack,
            SocialGiftStackAfter = afterGiftStack,
            SocialGiftQualityBefore = beforeGiftQuality,
            SocialGiftQualityAfter = afterGiftQuality,
            SocialGiftSlotBefore = beforeGiftSlot,
            SocialGiftSlotAfter = afterGiftSlot,
            SocialFriendshipPointsBefore = beforePoints,
            SocialFriendshipPointsAfter = afterPoints,
            SocialTalkedToTodayBefore = beforeTalkedToToday,
            SocialTalkedToTodayAfter = afterTalkedToToday,
            SocialGiftsTodayBefore = beforeGiftsToday,
            SocialGiftsTodayAfter = afterGiftsToday,
            SocialGiftsThisWeekBefore = beforeGiftsThisWeek,
            SocialGiftsThisWeekAfter = afterGiftsThisWeek,
            SocialMenuOpenBefore = beforeMenuOpen,
            SocialMenuOpenAfter = afterMenuOpen,
            SocialMenuTypeBefore = beforeMenuType,
            SocialMenuTypeAfter = afterMenuType,
            SocialDialogueOpenBefore = beforeDialogueOpen,
            SocialDialogueOpenAfter = afterDialogueOpen,
            SocialCurrentDialogueCountBefore = beforeDialogueCount,
            SocialCurrentDialogueCountAfter = afterNpcPresent ? afterDialogueCount : null,
            SocialCurrentDialogueKeyBefore = beforeDialogueKey,
            SocialCurrentDialogueKeyAfter = afterDialogueKey,
            SocialCurrentDialogueSpeakerNameBefore = beforeDialogueSpeakerName,
            SocialCurrentDialogueSpeakerNameAfter = afterDialogueSpeakerName,
            SocialFriendshipRowExistsBefore = beforeFriendshipRowExists,
            SocialFriendshipRowExistsAfter = afterFriendshipRowExists
        };

        if (includeChangedFacts)
        {
            var changedFacts = new List<SimulatedFactChange>();
            if (afterMenuOpen != beforeMenuOpen)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = beforeMenuOpen.ToString().ToLowerInvariant(), After = afterMenuOpen.ToString().ToLowerInvariant() });
            }
            if (!string.Equals(afterMenuType, beforeMenuType, StringComparison.Ordinal))
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType });
            }
            if (afterDialogueOpen != beforeDialogueOpen)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.dialogue.is_open", Before = beforeDialogueOpen.ToString().ToLowerInvariant(), After = afterDialogueOpen.ToString().ToLowerInvariant() });
            }
            if (!string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal))
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.dialogue.speaker_name", Before = beforeDialogueSpeakerName, After = afterDialogueSpeakerName });
            }
            if (afterFacing != beforeFacing)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.facing_direction", Before = beforeFacing.ToString(), After = afterFacing.ToString() });
            }
            if (afterSelectedSlot != beforeSelectedSlot)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.current_tool_index", Before = beforeSelectedSlot.ToString(), After = afterSelectedSlot.ToString() });
            }
            if (afterPoints != beforePoints)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".points", Before = beforePoints.ToString(), After = afterPoints.ToString() });
            }
            if (afterTalkedToToday != beforeTalkedToToday)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".talked_to_today", Before = beforeTalkedToToday.ToString().ToLowerInvariant(), After = afterTalkedToToday.ToString().ToLowerInvariant() });
            }
            if (afterGiftsToday != beforeGiftsToday)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".gifts_today", Before = beforeGiftsToday.ToString(), After = afterGiftsToday.ToString() });
            }
            if (afterGiftsThisWeek != beforeGiftsThisWeek)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".gifts_this_week", Before = beforeGiftsThisWeek.ToString(), After = afterGiftsThisWeek.ToString() });
            }
            if (afterFriendshipRowExists != beforeFriendshipRowExists)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".row_exists", Before = beforeFriendshipRowExists.ToString().ToLowerInvariant(), After = afterFriendshipRowExists.ToString().ToLowerInvariant() });
            }
            if (beforeGiftStack.HasValue && !afterGiftStack.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].stack", Before = beforeGiftStack.Value.ToString(), After = "null" });
                if (!string.IsNullOrWhiteSpace(beforeGiftItemId))
                {
                    changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].qualified_item_id", Before = beforeGiftItemId, After = string.Empty });
                }
            }
            else if (beforeGiftStack.HasValue && afterGiftStack.HasValue && afterGiftStack.Value != beforeGiftStack.Value)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].stack", Before = beforeGiftStack.Value.ToString(), After = afterGiftStack.Value.ToString() });
            }
            if (!string.IsNullOrWhiteSpace(beforeGiftItemId) && beforeGiftItemId != afterGiftItemId)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].qualified_item_id", Before = beforeGiftItemId, After = afterGiftItemId });
            }
            if (changedFacts.Count > 0)
            {
                result.ChangedFacts = changedFacts.ToArray();
            }
        }

        return result;
    }

    private static string SocialInteractRequestedEffect(TrainingExecutionRequest request)
    {
        var kind = string.IsNullOrWhiteSpace(request.SocialActionKind) ? "missing" : request.SocialActionKind;
        var npcName = string.IsNullOrWhiteSpace(request.SocialNpcName) ? "missing" : request.SocialNpcName;
        var effect = "social.kind=" + kind + ";npc=" + npcName;
        if (kind == "gift")
        {
            effect += ";slot=" + (request.SocialGiftSlotIndex?.ToString() ?? "missing") +
                ";item=" + (string.IsNullOrWhiteSpace(request.SocialGiftQualifiedItemId) ? "missing" : request.SocialGiftQualifiedItemId);
        }
        return effect;
    }

    private static string SocialInteractObservedEffect()
    {
        var safePlayer = Context.IsWorldReady ? Game1.player : null;
        var safeLocation = Context.IsWorldReady ? Game1.currentLocation : null;

        return "location=" + (safeLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + (safePlayer?.TilePoint.X.ToString() ?? "none") + "," + (safePlayer?.TilePoint.Y.ToString() ?? "none") +
            ";menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";dialogue_up=" + Game1.dialogueUp.ToString().ToLowerInvariant();
    }

    private TrainingExecutionResult ExecuteChooseDialogueResponse(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), reasons.ToArray());
        }

        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_box_not_open");
        }

        var expectedKey = request.ExpectedDialogueKey;
        var actualKey = Game1.currentLocation.lastQuestionKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(actualKey, expectedKey, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_key_mismatch");
        }

        var responseKey = request.DialogueResponseKey;
        if (!IsDialogueResponseWhitelisted(expectedKey, responseKey, request.ExpectedShopId))
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_response_not_whitelisted");
        }

        var response = menu.responses?.FirstOrDefault(item => string.Equals(item.responseKey, responseKey, StringComparison.Ordinal));
        if (response is null)
        {
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request), DialogueObservedEffect(), "dialogue_response_key_not_available");
        }

        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeQuestionKey = actualKey;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = Game1.currentLocation.answerDialogue(response);
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterShopId = Game1.activeClickableMenu is ShopMenu shopMenu ? shopMenu.ShopId : string.Empty;
        var verified = handled &&
            string.Equals(afterMenuType, "ShopMenu", StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(request.ExpectedShopId) || string.Equals(afterShopId, request.ExpectedShopId, StringComparison.OrdinalIgnoreCase));
        var verificationReasons = verified
            ? new[] { "dialogue_response_handled", "expected_shop_menu_opened" }
            : new[] { handled ? "dialogue_response_handled_without_expected_shop_menu" : "dialogue_response_not_handled" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "choose_dialogue_response",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = DialogueRequestedEffect(request),
            ObservedEffect = DialogueObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType },
                new SimulatedFactChange { Path = "menus.active_menu.shop_id", Before = "", After = afterShopId },
                new SimulatedFactChange { Path = "menus.active_menu.last_question_key", Before = beforeQuestionKey, After = Game1.currentLocation.lastQuestionKey ?? string.Empty }
            }
        };
    }

    private static bool IsDialogueResponseWhitelisted(string expectedDialogueKey, string responseKey, string expectedShopId)
    {
        return DialogueResponseOpensExpectedShop(expectedDialogueKey, responseKey, expectedShopId);
    }

    private static bool DialogueResponseOpensExpectedShop(string expectedDialogueKey, string responseKey, string expectedShopId)
    {
        return (string.Equals(expectedDialogueKey, "Blacksmith", StringComparison.Ordinal) &&
                string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Blacksmith", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(expectedDialogueKey, "carpenter", StringComparison.Ordinal) &&
                string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "Carpenter", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(expectedDialogueKey, "Marnie", StringComparison.Ordinal) &&
                string.Equals(responseKey, "Supplies", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AnimalShop", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(expectedDialogueKey, "adventureGuild", StringComparison.Ordinal) &&
                string.Equals(responseKey, "Shop", StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(expectedShopId) || string.Equals(expectedShopId, "AdventureShop", StringComparison.OrdinalIgnoreCase)));
    }

    private static string DialogueRequestedEffect(TrainingExecutionRequest request)
    {
        return "dialogue_key=" + (string.IsNullOrWhiteSpace(request.ExpectedDialogueKey) ? "missing" : request.ExpectedDialogueKey) +
            ";response_key=" + (string.IsNullOrWhiteSpace(request.DialogueResponseKey) ? "missing" : request.DialogueResponseKey) +
            ";expected_shop_id=" + (string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "missing" : request.ExpectedShopId);
    }

    private static string DialogueObservedEffect()
    {
        return "menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";last_question_key=" + (Game1.currentLocation?.lastQuestionKey ?? "none") +
            ";shop_id=" + (Game1.activeClickableMenu is ShopMenu menu ? menu.ShopId : "none");
    }

    private TrainingExecutionResult ExecuteBuyShopItem(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), reasons.ToArray());
        }

        if (Game1.activeClickableMenu is not ShopMenu menu)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_not_open");
        }

        var quantity = request.Quantity ?? 1;
        if (quantity != 1)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "quantity_one_required_for_safe_purchase_slice");
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedShopId) &&
            !string.Equals(menu.ShopId, request.ExpectedShopId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_id_mismatch");
        }

        if (menu.readOnly)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_read_only");
        }

        if (menu.safetyTimer > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_safety_timer_active");
        }

        if (menu.heldItem is not null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_held_item_present");
        }

        if (menu.currency != 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "non_money_currency_purchase_requires_audit");
        }

        if (menu.onPurchase is not null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_on_purchase_callback_present");
        }

        var match = menu.itemPriceAndStock
            .FirstOrDefault(entry =>
                (string.IsNullOrWhiteSpace(request.QualifiedItemId) || string.Equals(entry.Key.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(request.ShopItemId) || string.Equals(entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId, request.ShopItemId, StringComparison.OrdinalIgnoreCase)));
        if (match.Key is null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_item_not_found");
        }

        var salable = match.Key;
        var stock = match.Value;
        var blockReasons = SafePurchaseBlockReasons(menu, salable, stock, request);
        if (blockReasons.Length > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), blockReasons);
        }

        var itemToAdd = salable.GetSalableInstance() as Item;
        if (itemToAdd is null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "salable_instance_not_item");
        }

        itemToAdd.Stack = quantity;
        var qualifiedItemId = itemToAdd.QualifiedItemId;
        var beforeMoney = Game1.player.Money;
        var beforeCount = CountInventoryItems(qualifiedItemId);
        var beforeStock = stock.Stock;
        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.player.Money -= stock.Price * quantity;
        var accepted = Game1.player.addItemToInventoryBool(itemToAdd);
        if (!accepted)
        {
            Game1.player.Money = beforeMoney;
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "inventory_acceptance_failed_after_precheck");
        }

        if (stock.Stock != ShopMenu.infiniteStock)
        {
            stock.Stock = Math.Max(0, stock.Stock - quantity);
        }

        var afterMoney = Game1.player.Money;
        var afterCount = CountInventoryItems(qualifiedItemId);
        var afterStock = stock.Stock;
        var verified = afterMoney == beforeMoney - stock.Price * quantity && afterCount >= beforeCount + quantity;
        var verificationReasons = verified
            ? new[] { "money_decreased_by_price", "inventory_count_increased" }
            : new[] { "purchase_post_state_mismatch" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "buy_shop_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = BuyShopItemRequestedEffect(request),
            ObservedEffect = BuyShopItemObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = beforeMoney.ToString(), After = afterMoney.ToString() },
                new SimulatedFactChange { Path = "player.inventory." + qualifiedItemId + ".count", Before = beforeCount.ToString(), After = afterCount.ToString() },
                new SimulatedFactChange { Path = "menus.shop_stock." + qualifiedItemId + ".stock", Before = beforeStock.ToString(), After = afterStock.ToString() }
            }
        };
    }

    private static string[] SafePurchaseBlockReasons(ShopMenu menu, ISalable salable, ItemStockInformation stock, TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (salable.IsRecipe)
        {
            reasons.Add("recipe_purchase_discards_item_and_learns_recipe");
        }

        if (salable.GetType() != typeof(StardewValley.Object))
        {
            reasons.Add("non_plain_object_purchase_side_effects_unmodeled");
        }

        if (stock.TradeItem is not null)
        {
            reasons.Add("trade_item_purchase_requires_consumption_audit");
        }

        if (stock.ActionsOnPurchase?.Count > 0)
        {
            reasons.Add("actions_on_purchase_present");
        }

        if (stock.Stock != ShopMenu.infiniteStock && (stock.LimitedStockMode.ToString() != "None" || stock.SyncedKey is not null))
        {
            reasons.Add("synchronized_or_limited_stock_requires_post_state_audit");
        }

        if (stock.Stock != ShopMenu.infiniteStock && stock.Stock < 1)
        {
            reasons.Add("shop_item_out_of_stock");
        }

        if (request.MaxUnitPrice.HasValue && stock.Price > request.MaxUnitPrice.Value)
        {
            reasons.Add("purchase_price_exceeds_request_limit");
        }

        if (Game1.player.Money < stock.Price)
        {
            reasons.Add("insufficient_currency_for_purchase");
        }

        var itemToAdd = salable.GetSalableInstance() as Item;
        if (itemToAdd is null)
        {
            reasons.Add("salable_instance_not_item");
        }
        else if (!Game1.player.couldInventoryAcceptThisItem(itemToAdd))
        {
            reasons.Add("inventory_cannot_accept_purchase");
        }

        if (!salable.CanBuyItem(Game1.player))
        {
            reasons.Add("shop_item_cannot_be_bought");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountInventoryItems(string qualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item?.Stack ?? 0);
    }

    private static string BuyShopItemRequestedEffect(TrainingExecutionRequest request)
    {
        return "shop_id=" + (string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "any" : request.ExpectedShopId) +
            ";qualified_item_id=" + (string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "missing" : request.QualifiedItemId) +
            ";shop_item_id=" + (string.IsNullOrWhiteSpace(request.ShopItemId) ? "missing" : request.ShopItemId) +
            ";quantity=" + (request.Quantity?.ToString() ?? "1") +
            ";max_unit_price=" + (request.MaxUnitPrice?.ToString() ?? "unset");
    }

    private static string BuyShopItemObservedEffect()
    {
        return "menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";shop_id=" + (Game1.activeClickableMenu is ShopMenu menu ? menu.ShopId : "none") +
            ";money=" + Game1.player.Money;
    }

    private static string InteractRequestedEffect(TrainingExecutionRequest request)
    {
        return "interact.kind=" + (string.IsNullOrWhiteSpace(request.InteractionKind) ? "missing" : request.InteractionKind) +
            ";target_tile=" + (request.TargetTileX.HasValue && request.TargetTileY.HasValue ? request.TargetTileX.Value + "," + request.TargetTileY.Value : "missing") +
            ";expected_action_type=" + (string.IsNullOrWhiteSpace(request.ExpectedActionType) ? "missing" : request.ExpectedActionType);
    }

    private static string InteractObservedEffect()
    {
        return "menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + (Game1.player?.TilePoint.X.ToString() ?? "missing") + "," + (Game1.player?.TilePoint.Y.ToString() ?? "missing");
    }

    private static bool IsSafeCloseMenuType(string type)
    {
        return type is "GameMenu" or "InventoryMenu" or "QuestLog" or "MapPage" or "ProfileMenu" or "ShopMenu";
    }

    private void StartWait(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + (pending.Request.WaitTicks?.ToString() ?? "missing"), "executor.wait_ticks=0", reasons.ToArray()));
            return;
        }

        var waitTicks = pending.Request.WaitTicks ?? 0;
        if (waitTicks < 1 || waitTicks > 600)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + waitTicks, "executor.wait_ticks=0", "wait_ticks_1_600_required"));
            return;
        }

        if (activeWait is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + waitTicks, "executor.wait_ticks=0", "wait_executor_busy"));
            return;
        }

        activeWait = new ActiveWait(pending, waitTicks);
    }

    private void TickWait()
    {
        if (activeWait is null)
        {
            return;
        }

        activeWait.ElapsedTicks++;
        if (activeWait.ElapsedTicks < activeWait.TargetTicks)
        {
            return;
        }

        var wait = activeWait;
        activeWait = null;
        var request = wait.Pending.Request;
        wait.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = wait.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "wait_ticks",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "elapsed_ticks_reached_target" },
            RequestedEffect = "executor.wait_ticks=" + wait.TargetTicks,
            ObservedEffect = "executor.wait_ticks=" + wait.ElapsedTicks,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "executor.wait_ticks",
                    Before = "0",
                    After = wait.ElapsedTicks.ToString()
                }
            }
        });
    }

    private TrainingExecutionResult ExecuteAdvanceTimeTo(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + (request.TargetTime?.ToString() ?? "missing"), "time.time=" + Game1.timeOfDay, reasons.ToArray());
        }

        if (!request.TargetTime.HasValue || request.TargetTime.Value < 600 || request.TargetTime.Value > 2600 || request.TargetTime.Value % 10 != 0)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + (request.TargetTime?.ToString() ?? "missing"), "time.time=" + Game1.timeOfDay, "target_time_600_2600_step_10_required");
        }

        var before = Game1.timeOfDay;
        if (request.TargetTime.Value < before)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + request.TargetTime.Value, "time.time=" + before, "target_time_must_not_go_backward");
        }

        Game1.timeOfDay = request.TargetTime.Value;
        var verified = Game1.timeOfDay == request.TargetTime.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_advance_time_to",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "time_set_to_target_for_isolated_runtime_test" } : new[] { "time_set_mismatch" },
            RequestedEffect = "time.time=" + request.TargetTime.Value,
            ObservedEffect = "time.time=" + Game1.timeOfDay,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "time_set_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "time.time",
                    Before = before.ToString(),
                    After = Game1.timeOfDay.ToString()
                }
            }
        };
    }

    private TrainingExecutionResult ExecuteSetupClearObstacle(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_clear_obstacle", "current_location.obstacle[target]=terrain_feature:Grass", ClearObstacleObservedEffect(null), "target_tile_required");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!CanClearRouteObstacles(Game1.currentLocation) ||
            ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            MoveFixtureFarmerToFarmAdjacent(target);
        }

        var location = Game1.currentLocation;
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(request, "debug_setup_clear_obstacle", "current_location.obstacle[" + target.X + "," + target.Y + "]=terrain_feature:Grass", ClearObstacleObservedEffect(target), "setup_clear_obstacle_location_not_whitelisted");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var tile = new Vector2(target.X, target.Y);
        var before = ObstacleLabel(location, target);
        location.terrainFeatures.Remove(tile);
        location.objects.Remove(tile);
        location.terrainFeatures[tile] = new Grass(Grass.springGrass, 4);

        var after = ObstacleLabel(location, target);
        var verified = location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Grass;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_grass_obstacle" }
                : new[] { "fixture_grass_obstacle_not_visible" },
            RequestedEffect = "current_location.obstacle[" + target.X + "," + target.Y + "]=terrain_feature:Grass",
            ObservedEffect = "before=" + before + ";after=" + after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_grass_obstacle_not_visible" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                }
            }
        };
    }

    private static void MoveFixtureFarmerToFarmAdjacent(Point target)
    {
        MoveFixtureFarmerToFarmAdjacent(target, out _, out _);
    }

    private static bool MoveFixtureFarmerToFarmAdjacent(Point target, out Point standTile, out string blockReason)
    {
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        foreach (var candidate in Neighbors(target)
            .Where(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile))
            .OrderBy(tile => ManhattanDistance(Game1.player.TilePoint, tile)))
        {
            standTile = candidate;
            blockReason = string.Empty;
            Game1.player.Position = new Vector2(candidate.X * Game1.tileSize, candidate.Y * Game1.tileSize);
            Game1.player.faceDirection(DirectionTo(candidate, target));
            return true;
        }

        standTile = Point.Zero;
        blockReason = "fixture_no_collision_safe_adjacent_tile";
        return false;
    }

    private TrainingExecutionResult ExecuteSetupWateringTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_watering_target", "farm.crops[target].needs_watering=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforeTile = Game1.player.TilePoint;
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        if (farm.terrainFeatures.TryGetValue(tile, out var existing) && existing is not HoeDirt)
        {
            farm.terrainFeatures.Remove(tile);
        }

        var dirt = farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt existingDirt
            ? existingDirt
            : new HoeDirt(0, farm);
        dirt.state.Value = HoeDirt.dry;
        dirt.crop = new Crop("472", request.TargetTileX.Value, request.TargetTileY.Value, farm);
        farm.terrainFeatures[tile] = dirt;
        var fixtureMoved = MoveFixtureFarmerToFarmAdjacent(target, out var standTile, out var fixtureMoveReason);

        var verified = farm.terrainFeatures.TryGetValue(tile, out var afterFeature) &&
            afterFeature is HoeDirt afterDirt &&
            afterDirt.crop is not null &&
            afterDirt.needsWatering() &&
            !afterDirt.isWatered() &&
            fixtureMoved &&
            Game1.currentLocation == farm &&
            Game1.player.TilePoint == standTile &&
            AreAdjacent(Game1.player.TilePoint, target);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_watering_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_crop_needs_watering", "fixture_farmer_on_farm_adjacent_to_target" }
                : new[] { fixtureMoved ? "fixture_crop_not_waterable" : fixtureMoveReason },
            RequestedEffect = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].needs_watering=true;player.location_id=Farm;player.adjacent_to_target=true",
            ObservedEffect = "needs_watering=" + (afterFeature is HoeDirt observedDirt && observedDirt.needsWatering()).ToString().ToLowerInvariant() + ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty) + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { fixtureMoved ? "fixture_crop_not_waterable" : fixtureMoveReason },
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[" + target.X + "," + target.Y + "].needs_watering",
                        Before = "unknown",
                        After = "true"
                    },
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = farm.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.tile", Before = beforeTile.X + "," + beforeTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                    new SimulatedFactChange { Path = "player.facing_direction", Before = "unknown", After = Game1.player.FacingDirection.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupTillSoilTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var farm = Game1.getFarm();
        var selectedTarget = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : FindTillSoilFixtureTarget(farm);
        if (!selectedTarget.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_till_soil_target", "farm.terrain_features[target].type=none;player.location_id=Farm;player.adjacent_to_target=true", TillSoilObservedEffect(farm, null), "till_soil_fixture_no_diggable_candidate");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var target = selectedTarget.Value;
        var tile = new Vector2(target.X, target.Y);
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforePlayerTile = Game1.player.TilePoint;
        var beforeFeature = farm.terrainFeatures.TryGetValue(tile, out var existingFeature) ? existingFeature.GetType().Name : "none";
        var beforeObject = farm.objects.ContainsKey(tile).ToString().ToLowerInvariant();

        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        farm.objects.Remove(tile);
        farm.terrainFeatures.Remove(tile);

        var fixtureMoved = MoveFixtureFarmerToFarmAdjacent(target, out var standTile, out var fixtureMoveReason);
        var precheck = ValidateTillSoilTarget(farm, target, FindTool<Hoe>());
        var verified = fixtureMoved &&
            precheck.Length == 0 &&
            Game1.currentLocation == farm &&
            Game1.player.TilePoint == standTile &&
            AreAdjacent(Game1.player.TilePoint, target) &&
            !farm.terrainFeatures.TryGetValue(tile, out _) &&
            !farm.objects.ContainsKey(tile);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_till_soil_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_diggable_untilled_tile", "fixture_farmer_on_farm_adjacent_to_target" }
                : fixtureMoved ? precheck.DefaultIfEmpty("till_soil_fixture_state_mismatch").ToArray() : new[] { fixtureMoveReason },
            RequestedEffect = "farm.terrain_features[" + target.X + "," + target.Y + "].type=none;player.location_id=Farm;player.adjacent_to_target=true",
            ObservedEffect = TillSoilObservedEffect(farm, target),
            BlockReasons = verified ? Array.Empty<string>() : fixtureMoved ? precheck.DefaultIfEmpty("till_soil_fixture_state_mismatch").ToArray() : new[] { fixtureMoveReason },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "farm.terrain_features[" + target.X + "," + target.Y + "].type", Before = beforeFeature, After = "none" },
                    new SimulatedFactChange { Path = "farm.objects[" + target.X + "," + target.Y + "].present", Before = beforeObject, After = "false" },
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = farm.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.tile", Before = beforePlayerTile.X + "," + beforePlayerTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                    new SimulatedFactChange { Path = "player.facing_direction", Before = "unknown", After = Game1.player.FacingDirection.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindTillSoilFixtureTarget(Farm farm)
    {
        var dimensions = MapDimensions(farm);
        for (var y = 0; y < dimensions.Y; y++)
        {
            for (var x = 0; x < dimensions.X; x++)
            {
                var target = new Point(x, y);
                var tile = new Vector2(x, y);
                if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") is null ||
                    farm.terrainFeatures.ContainsKey(tile) ||
                    farm.objects.ContainsKey(tile) ||
                    farm.IsTileBlockedBy(tile, ~(CollisionMask.Characters | CollisionMask.Farmers)) ||
                    !Neighbors(target).Any(stand => IsTileOnMap(farm, stand) && IsTileWalkable(farm, stand)))
                {
                    continue;
                }

                return target;
            }
        }

        return null;
    }

    private TrainingExecutionResult ExecuteSetupFishFrenzy(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "target_tile=missing", "target_tile_required");
        }

        var location = Game1.currentLocation;
        var tile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.canFishHere() || !location.isTileFishable(tile.X, tile.Y))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "fishable_tile=false", "fish_frenzy_fixture_tile_not_fishable");
        }

        Item fish;
        try
        {
            fish = ItemRegistry.Create(request.QualifiedItemId);
        }
        catch
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "qualified_item=invalid", "fish_frenzy_fixture_item_invalid");
        }

        if (fish.Category != StardewValley.Object.FishCategory)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "qualified_item=" + fish.QualifiedItemId, "fish_frenzy_fixture_item_not_fish");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeFish = location.fishFrenzyFish.Value ?? string.Empty;
        var beforePoint = location.fishSplashPoint.Value;
        var frenzyTimeField = Helper.Reflection.GetField<int>(location, "fishSplashPointTime");
        var beforeFrenzyTime = frenzyTimeField.GetValue();
        location.fishFrenzyFish.Value = fish.QualifiedItemId;
        location.fishSplashPoint.Value = tile;
        frenzyTimeField.SetValue(Game1.timeOfDay);
        var verified = string.Equals(location.fishFrenzyFish.Value, fish.QualifiedItemId, StringComparison.Ordinal) &&
            location.fishSplashPoint.Value == tile &&
            frenzyTimeField.GetValue() == Game1.timeOfDay;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_fish_frenzy",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_fish_frenzy_active" }
                : new[] { "fish_frenzy_fixture_state_mismatch" },
            RequestedEffect = "current_location.fish_frenzy.active=true;qualified_item_id=" + fish.QualifiedItemId + ";center_tile=" + tile.X + "," + tile.Y + ";start_time=" + Game1.timeOfDay,
            ObservedEffect = "active=" + verified.ToString().ToLowerInvariant() + ";qualified_item_id=" + (location.fishFrenzyFish.Value ?? string.Empty) + ";center_tile=" + location.fishSplashPoint.Value.X + "," + location.fishSplashPoint.Value.Y + ";start_time=" + frenzyTimeField.GetValue(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fish_frenzy_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.qualified_item_id", Before = beforeFish, After = fish.QualifiedItemId },
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.center_tile", Before = beforePoint.X + "," + beforePoint.Y, After = tile.X + "," + tile.Y },
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.start_time", Before = beforeFrenzyTime.ToString(), After = Game1.timeOfDay.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupFishPond(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (Game1.currentLocation is not Farm farm || !ReferenceEquals(farm, Game1.getFarm()))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "location=" + Game1.currentLocation?.NameOrUniqueName, "fish_pond_fixture_requires_farm_location");
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "top_left_tile=missing", "target_tile_required");
        }

        Item fish;
        try
        {
            fish = ItemRegistry.Create(request.QualifiedItemId);
        }
        catch
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "qualified_item=invalid", "fish_pond_fixture_item_invalid");
        }

        if (fish.Category != StardewValley.Object.FishCategory || fish.HasContextTag("fish_legendary"))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "qualified_item=" + fish.QualifiedItemId, "fish_pond_fixture_item_not_legal_fish");
        }

        var requestedTopLeft = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var selectedTopLeft = FindFishPondFixturePlacement(farm, requestedTopLeft);
        if (!selectedTopLeft.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "requested_top_left_tile=" + request.TargetTileX.Value + "," + request.TargetTileY.Value, "fish_pond_fixture_no_legal_placement");
        }

        var topLeft = new Vector2(selectedTopLeft.Value.X, selectedTopLeft.Value.Y);
        var pond = new FishPond(topLeft);
        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeBuildingCount = farm.buildings.Count;
        if (!farm.buildStructure(pond, topLeft, Game1.player, skipSafetyChecks: false))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "top_left_tile=" + selectedTopLeft.Value.X + "," + selectedTopLeft.Value.Y, "fish_pond_fixture_placement_rejected");
        }

        pond.daysOfConstructionLeft.Value = 0;
        pond.fishType.Value = fish.ItemId;
        pond.currentOccupants.Value = 1;
        var fishableTile = new Vector2(pond.tileX.Value + 1, pond.tileY.Value + 1);
        var verified = farm.buildings.Contains(pond) &&
            pond.daysOfConstructionLeft.Value == 0 &&
            pond.FishCount == 1 &&
            string.Equals(pond.fishType.Value, fish.ItemId, StringComparison.Ordinal) &&
            pond.isTileFishable(fishableTile);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_fish_pond",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_fish_pond_catch_available" }
                : new[] { "fish_pond_fixture_state_mismatch" },
            RequestedEffect = "current_location.fish_pond.catch_available=true;qualified_item_id=" + fish.QualifiedItemId + ";top_left_tile=" + pond.tileX.Value + "," + pond.tileY.Value + ";fish_count=1",
            ObservedEffect = "building_present=" + farm.buildings.Contains(pond).ToString().ToLowerInvariant() + ";qualified_item_id=(O)" + (pond.fishType.Value ?? string.Empty) + ";top_left_tile=" + pond.tileX.Value + "," + pond.tileY.Value + ";fish_count=" + pond.FishCount + ";fishable_tile=" + (int)fishableTile.X + "," + (int)fishableTile.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fish_pond_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "farm.buildings.count", Before = beforeBuildingCount.ToString(), After = farm.buildings.Count.ToString() },
                    new SimulatedFactChange { Path = "current_location.fish_pond.top_left_tile", Before = string.Empty, After = pond.tileX.Value + "," + pond.tileY.Value },
                    new SimulatedFactChange { Path = "current_location.fish_pond.qualified_item_id", Before = string.Empty, After = fish.QualifiedItemId },
                    new SimulatedFactChange { Path = "current_location.fish_pond.fish_count", Before = "0", After = pond.FishCount.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindFishPondFixturePlacement(Farm farm, Point requestedTopLeft)
    {
        var probe = new FishPond(Vector2.Zero);
        var layer = farm.map?.Layers.FirstOrDefault();
        if (layer is null)
        {
            return null;
        }

        var candidates = new List<Point> { requestedTopLeft };
        for (var y = 1; y <= layer.LayerHeight - probe.tilesHigh.Value - 1; y++)
        {
            for (var x = 1; x <= layer.LayerWidth - probe.tilesWide.Value - 1; x++)
            {
                candidates.Add(new Point(x, y));
            }
        }

        foreach (var candidate in candidates
            .Distinct()
            .OrderBy(candidate => ManhattanDistance(candidate, requestedTopLeft))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X))
        {
            if (CanPlaceFishPondFixture(farm, probe, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool CanPlaceFishPondFixture(Farm farm, FishPond pond, Point topLeft)
    {
        for (var y = 0; y < pond.tilesHigh.Value; y++)
        {
            for (var x = 0; x < pond.tilesWide.Value; x++)
            {
                if (!farm.isBuildable(new Vector2(topLeft.X + x, topLeft.Y + y)))
                {
                    return false;
                }
            }
        }

        return pond.isThereAnythingtoPreventConstruction(farm, new Vector2(topLeft.X, topLeft.Y)) is null;
    }

    private void StartSetupMineFishingFloor(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue || request.MineLevel.Value < 80 || request.MineLevel.Value > 120)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_mine_fishing_floor", "current_location.mine_area=80;can_fish_here=true", "mine_level=" + request.MineLevel, "mine_fishing_fixture_level_not_in_lava_area"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var prerequisiteFacts = EnsureMineFishingFixtureEquipment();
        activeMineFishingSetup = new ActiveMineFishingSetup(pending, request.MineLevel.Value, beforeLocation, prerequisiteFacts);
        Game1.enterMine(request.MineLevel.Value);
    }

    private void StartMineStone(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", "mining.objects[target].is_breakable_stone=false", "target=missing", "mine_stone_target_tile_required"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var mine = Game1.currentLocation as MineShaft;
        var pickaxe = FindTool<Pickaxe>();
        var requested = "mining.objects[" + target.X + "," + target.Y + "].is_breakable_stone=false;native_tool=Pickaxe";
        if (mine is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_requires_loaded_mineshaft"));
            return;
        }

        var tile = new Vector2(target.X, target.Y);
        if (!mine.objects.TryGetValue(tile, out var stone) || !stone.IsBreakableStone())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_target_not_breakable_stone"));
            return;
        }

        if (pickaxe is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_pickaxe_unavailable"));
            return;
        }

        if (Game1.player.Stamina <= 0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_energy_exhausted"));
            return;
        }

        var path = BuildAdjacentToolPath(mine, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), moveReason));
            return;
        }

        activeMineStone = new ActiveMineStone(
            pending,
            mine.NameOrUniqueName,
            target,
            path,
            pickaxe,
            stone.QualifiedItemId,
            stone.MinutesUntilReady,
            Game1.player.Stamina,
            Math.Clamp(request.MaxCrops, 1, 64),
            requested);
    }

    private void TickMineStone()
    {
        if (activeMineStone is null)
        {
            return;
        }

        var active = activeMineStone;
        try
        {
            TickMineStoneCore(active);
        }
        catch (Exception ex)
        {
            CompleteMineStoneBlocked(active, "mine_stone_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickMineStoneCore(ActiveMineStone active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteMineStoneBlocked(active, "mine_stone_location_changed_or_world_unavailable");
            return;
        }

        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteMineStoneBlocked(active, "mine_stone_timeout");
            return;
        }

        var targetVector = new Vector2(active.Target.X, active.Target.Y);
        if (!mine.objects.TryGetValue(targetVector, out var current))
        {
            if (active.BeginIssued)
            {
                RecordMineStoneCompletedSwing(active, 0);
            }
            CompleteMineStone(active);
            return;
        }

        if (!current.IsBreakableStone() || !string.Equals(current.QualifiedItemId, active.QualifiedItemId, StringComparison.Ordinal))
        {
            CompleteMineStoneBlocked(active, "mine_stone_runtime_target_drift");
            return;
        }

        if (!active.BeginIssued && ImmediateMiningThreat(mine))
        {
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!active.BeginIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteMineStoneBlocked(active, "mine_stone_unreachable_target");
                return;
            }

            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                active.StuckTicks = 0;
                return;
            }

            if (!IsTileWalkable(mine, next) || IsTileOccupiedByCharacter(mine, next))
            {
                CompleteMineStoneBlocked(active, "mine_stone_path_changed");
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }

            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    CompleteMineStoneBlocked(active, "mine_stone_movement_stuck");
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.SwingCount >= active.MaxSwings)
        {
            CompleteMineStoneBlocked(active, "mine_stone_max_swings_exceeded");
            return;
        }

        if (Game1.player.Stamina <= 0f)
        {
            CompleteMineStoneBlocked(active, "mine_stone_energy_exhausted");
            return;
        }

        if (!active.BeginIssued)
        {
            SelectTool(active.Pickaxe);
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            Game1.player.lastClick = new Vector2(active.Target.X * Game1.tileSize, active.Target.Y * Game1.tileSize);
            Game1.player.BeginUsingTool();
            active.BeginIssued = true;
            return;
        }

        if (!active.ReleaseIssued && Game1.player.UsingTool && Game1.player.canReleaseTool)
        {
            Game1.player.EndUsingTool();
            active.ReleaseIssued = true;
            return;
        }

        if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }

        RecordMineStoneCompletedSwing(active, mine.objects.TryGetValue(targetVector, out var afterSwing) ? afterSwing.MinutesUntilReady : 0);
    }

    private static void RecordMineStoneCompletedSwing(ActiveMineStone active, int remainingHealth)
    {
        active.SwingCount++;
        active.ObservedHealth.Add(remainingHealth);
        active.BeginIssued = false;
        active.ReleaseIssued = false;
    }

    private void CompleteMineStone(ActiveMineStone active)
    {
        StopAllMovement();
        activeMineStone = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            EnergyBefore = active.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = active.LocationId,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.Pickaxe.QualifiedItemId,
            ToolUpgradeLevel = active.Pickaxe.UpgradeLevel,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "mine_stone",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_pickaxe_lifecycle_removed_breakable_stone", "native_swing_count=" + active.SwingCount },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = MineStoneObservedEffect(active.Target) + ";health_sequence=" + string.Join(",", active.ObservedHealth) + ";native_swings=" + active.SwingCount,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.QualifiedItemId + ":health=" + active.HealthBefore, After = "removed" },
                new SimulatedFactChange { Path = "player.energy", Before = active.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteMineStoneBlocked(ActiveMineStone active, string reason)
    {
        StopAllMovement();
        if (active.BeginIssued && ReferenceEquals(Game1.player.CurrentTool, active.Pickaxe))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        activeMineStone = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "mine_stone", active.RequestedEffect, MineStoneObservedEffect(active.Target) + ";native_swings=" + active.SwingCount, reason));
    }

    private static string MineStoneObservedEffect(Point target)
    {
        var location = Game1.currentLocation as MineShaft;
        var tile = new Vector2(target.X, target.Y);
        var state = location?.objects.TryGetValue(tile, out var obj) == true
            ? obj.QualifiedItemId + ":breakable=" + obj.IsBreakableStone().ToString().ToLowerInvariant() + ":health=" + obj.MinutesUntilReady
            : "removed_or_missing";
        return "location=" + (location?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";stone=" + state;
    }

    private void StartBreakContainer(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", "mining.objects[target].is_container=false", "target=missing", "break_container_target_tile_required"));
            return;
        }

        var mine = Game1.currentLocation as MineShaft;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        var requested = "mining.objects[" + target.X + "," + target.Y + "].is_container=false;native_input=use_tool";
        if (mine is null || !mine.objects.TryGetValue(targetVector, out var obj) || obj is not BreakableContainer container)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), "break_container_target_not_found"));
            return;
        }

        var tool = BestContainerTool();
        if (tool is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), "break_container_heavy_hitter_unavailable"));
            return;
        }
        var path = BuildAdjacentToolPath(mine, target, request.MaxMovementTiles ?? 512, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), pathReason));
            return;
        }

        activeBreakContainer = new ActiveBreakContainer(
            pending,
            mine,
            target,
            path,
            container,
            tool,
            ReadBreakableContainerHealth(container) ?? 3,
            Math.Clamp(request.MaxCrops, 1, 64),
            request.RestoreSlotIndex ?? Game1.player.CurrentToolIndex,
            requested);
    }

    private TrainingExecutionResult ExecuteSetupBreakableContainer(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(request, "debug_setup_breakable_container", "mining.objects[target].is_container=true", "location=not_mineshaft", "setup_breakable_container_requires_mineshaft");
        }

        var start = Game1.player.TilePoint;
        var candidates = Enumerable.Range(1, 8)
            .SelectMany(radius => Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offset => new[]
                {
                    new Point(start.X + offset, start.Y - radius),
                    new Point(start.X + offset, start.Y + radius),
                    new Point(start.X - radius, start.Y + offset),
                    new Point(start.X + radius, start.Y + offset)
                }))
            .Distinct()
            .Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile))
            .Where(tile => !mine.objects.ContainsKey(new Vector2(tile.X, tile.Y)) && !IsTileOccupiedByCharacter(mine, tile))
            .Where(tile => Neighbors(tile).Any(stand => IsTileOnMap(mine, stand) && IsTileWalkable(mine, stand)))
            .OrderBy(tile => ManhattanDistance(start, tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArray();
        var target = candidates.FirstOrDefault();
        if (target == default)
        {
            return BlockedWithPrimitive(request, "debug_setup_breakable_container", "mining.objects[target].is_container=true", "candidate=missing", "setup_breakable_container_no_reachable_tile");
        }

        var tileVector = new Vector2(target.X, target.Y);
        var beforeCount = mine.objects.Count();
        var container = BreakableContainer.GetBarrelForMines(tileVector, mine);
        mine.objects[tileVector] = container;
        var health = ReadBreakableContainerHealth(container);
        var verified = mine.objects.TryGetValue(tileVector, out var observed) && ReferenceEquals(observed, container) && health.HasValue;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_breakable_container",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified ? new[] { "native_breakable_container_fixture_present", "health=" + health } : new[] { "breakable_container_fixture_missing" },
            RequestedEffect = "mining.objects[" + target.X + "," + target.Y + "].is_container=true",
            ObservedEffect = BreakContainerObservedEffect(target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "setup_breakable_container_fixture_mismatch" },
            ChangedFacts = verified
                ? new[] { new SimulatedFactChange { Path = "mining.objects.count", Before = beforeCount.ToString(), After = mine.objects.Count().ToString() } }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupMiningCombatFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "location=not_mineshaft", "setup_mining_combat_fixture_requires_mineshaft");
        }

        var fixtureKind = string.Equals(request.TargetName, "explosive_ammo", StringComparison.Ordinal)
            ? "explosive_ammo"
            : "mummy_chain";
        var target = FindMiningCombatFixtureTarget(
            mine,
            requireClearProjectilePath: fixtureKind == "explosive_ammo",
            requireBombEscape: fixtureKind == "mummy_chain");
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "candidate=missing", "setup_mining_combat_fixture_no_reachable_tile");
        }

        foreach (var monster in mine.characters.OfType<Monster>().ToArray())
        {
            mine.characters.Remove(monster);
        }
        ClearMiningFixtureArea(mine, target.Value, radius: 4);
        var bombEscape = fixtureKind == "mummy_chain"
            ? FindMiningCombatFixtureBombEscape(mine, target.Value)
            : null;
        if (fixtureKind == "mummy_chain" && !bombEscape.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "bomb_escape=missing", "setup_mining_combat_fixture_no_bomb_escape");
        }
        EnsureFixtureInventoryCapacity(Game1.player);

        Monster targetMonster;
        int weaponSlot;
        int consumableSlot;
        if (fixtureKind == "explosive_ammo")
        {
            targetMonster = new GreenSlime(target.Value.ToVector2() * Game1.tileSize, mine.mineLevel);
            var slingshot = new Slingshot("34");
            var ammo = new StardewValley.Object("441", 99);
            slingshot.attach(ammo);
            weaponSlot = InstallFixtureItem(Game1.player, slingshot);
            consumableSlot = weaponSlot;
            var playerTile = Game1.player.TilePoint;
            var resourceTiles = Math.Abs(target.Value.X - playerTile.X) >= Math.Abs(target.Value.Y - playerTile.Y)
                ? new[] { new Point(target.Value.X, target.Value.Y - 1), new Point(target.Value.X, target.Value.Y + 1) }
                : new[] { new Point(target.Value.X - 1, target.Value.Y), new Point(target.Value.X + 1, target.Value.Y) };
            foreach (var tile in resourceTiles.Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile)))
            {
                var vector = new Vector2(tile.X, tile.Y);
                mine.objects[vector] = new StardewValley.Object("751", 1)
                {
                    MinutesUntilReady = 3,
                    TileLocation = vector
                };
            }
        }
        else
        {
            targetMonster = new Mummy(target.Value.ToVector2() * Game1.tileSize);
            weaponSlot = InstallFixtureItem(Game1.player, new MeleeWeapon("9"));
            consumableSlot = InstallFixtureItem(Game1.player, new StardewValley.Object("286", 20));
        }
        targetMonster.Speed = 0;
        targetMonster.moveTowardPlayerThreshold.Value = -1;
        mine.characters.Add(targetMonster);
        Game1.player.health = Game1.player.maxHealth;
        Game1.player.CurrentToolIndex = weaponSlot;

        var runtimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(targetMonster).ToString("X8");
        var verified = mine.characters.Contains(targetMonster) &&
            weaponSlot >= 0 &&
            consumableSlot >= 0;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.Value.X,
            TargetTileY = target.Value.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mining_combat_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_mining_combat_fixture_present", "fixture_kind=" + fixtureKind }
                : new[] { "isolated_mining_combat_fixture_missing" },
            RequestedEffect = "mining.combat_fixture=" + fixtureKind,
            ObservedEffect = "target_identity=" + runtimeIdentity +
                ";target_type=" + (targetMonster.GetType().FullName ?? targetMonster.GetType().Name) +
                ";target_tile=" + target.Value.X + "," + target.Value.Y +
                ";weapon_slot=" + weaponSlot +
                ";consumable_slot=" + consumableSlot,
            CombatTargetRuntimeType = targetMonster.GetType().FullName ?? targetMonster.GetType().Name,
            CombatTargetRuntimeIdentity = runtimeIdentity,
            CombatTargetName = targetMonster.Name,
            BombEscapeTileX = bombEscape?.X,
            BombEscapeTileY = bombEscape?.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "setup_mining_combat_fixture_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "mining.monsters[" + runtimeIdentity + "].present", Before = "false", After = "true" },
                    new SimulatedFactChange { Path = "player.current_tool_index", Before = string.Empty, After = weaponSlot.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindMiningCombatFixtureTarget(
        MineShaft mine,
        bool requireClearProjectilePath,
        bool requireBombEscape)
    {
        var start = Game1.player.TilePoint;
        var candidates = Enumerable.Range(5, 10)
            .SelectMany(radius => Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offset => new[]
                {
                    new Point(start.X + offset, start.Y - radius),
                    new Point(start.X + offset, start.Y + radius),
                    new Point(start.X - radius, start.Y + offset),
                    new Point(start.X + radius, start.Y + offset)
                }))
            .Distinct()
            .Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile))
            .Where(tile => !mine.objects.ContainsKey(tile.ToVector2()) && !IsTileOccupiedByCharacter(mine, tile))
            .Where(tile => !requireClearProjectilePath || HasClearProjectilePath(mine, start, tile))
            .Where(tile => !requireBombEscape || FindMiningCombatFixtureBombEscape(mine, tile).HasValue)
            .Select(tile => new
            {
                Tile = tile,
                Stand = Neighbors(tile)
                    .Where(stand => IsTileOnMap(mine, stand) && IsTileWalkable(mine, stand))
                    .Select(stand => new
                    {
                        Tile = stand,
                        Path = TryBuildTilePath(mine, start, stand, 512, out _, avoidSoftObstacles: true)
                    })
                    .FirstOrDefault(row => row.Path is not null)
            })
            .Where(row => row.Stand is not null)
            .OrderBy(row => ManhattanDistance(start, row.Tile))
            .ThenBy(row => row.Tile.Y)
            .ThenBy(row => row.Tile.X)
            .ToArray();
        return candidates.FirstOrDefault()?.Tile;
    }

    private static Point? FindMiningCombatFixtureBombEscape(MineShaft mine, Point target)
    {
        const int minimumDistance = 4;
        foreach (var direction in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
        {
            var clear = true;
            for (var distance = 1; distance <= minimumDistance; distance++)
            {
                var tile = new Point(target.X + direction.X * distance, target.Y + direction.Y * distance);
                if (!IsTileOnMap(mine, tile) || !IsTileWalkable(mine, tile) || IsTileOccupiedByCharacter(mine, tile))
                {
                    clear = false;
                    break;
                }
            }
            if (clear)
            {
                return new Point(target.X + direction.X * minimumDistance, target.Y + direction.Y * minimumDistance);
            }
        }
        return null;
    }

    private static void EnsureFixtureInventoryCapacity(Farmer player)
    {
        if (player.MaxItems < 36)
        {
            player.increaseBackpackSize(36 - player.MaxItems);
        }
        while (player.Items.Count < player.MaxItems)
        {
            player.Items.Add(null);
        }
    }

    private static int InstallFixtureItem(Farmer player, Item item)
    {
        var slot = FirstEmptyInventorySlot(player);
        if (slot < 0)
        {
            slot = Math.Max(0, Math.Min(player.Items.Count, player.MaxItems) - 1);
        }
        player.Items[slot] = item;
        return slot;
    }

    private void TickBreakContainer()
    {
        if (activeBreakContainer is null)
        {
            return;
        }

        var active = activeBreakContainer;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteBreakContainerBlocked(active, "break_container_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteBreakContainerBlocked(active, "break_container_timeout");
            return;
        }

        var targetVector = new Vector2(active.Target.X, active.Target.Y);
        if (!active.Mine.objects.TryGetValue(targetVector, out var obj))
        {
            CompleteBreakContainer(active);
            return;
        }
        if (!ReferenceEquals(obj, active.Container) || obj is not BreakableContainer container)
        {
            CompleteBreakContainerBlocked(active, "break_container_runtime_target_drift");
            return;
        }

        if (!active.ButtonHeld && ImmediateMiningThreat(active.Mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteBreakContainerBlocked(active, "break_container_unreachable_target");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.Mine, next) || IsTileOccupiedByCharacter(active.Mine, next))
            {
                CompleteBreakContainerBlocked(active, "break_container_path_changed");
                return;
            }
            if (Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) < 0.01f)
            {
                active.StuckTicks++;
            }
            else
            {
                active.StuckTicks = 0;
                active.LastPosition = Game1.player.Position;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (active.StuckTicks > 45)
            {
                CompleteBreakContainerBlocked(active, "break_container_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        if (active.ButtonHeld)
        {
            TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
            active.ButtonHeld = false;
            var health = ReadBreakableContainerHealth(container);
            if (health.HasValue && (active.ObservedHealth.Count == 0 || active.ObservedHealth[^1] != health.Value))
            {
                active.ObservedHealth.Add(health.Value);
            }
            return;
        }
        if (Game1.player.UsingTool)
        {
            return;
        }
        if (active.SwingCount >= active.MaxSwings)
        {
            CompleteBreakContainerBlocked(active, "break_container_swing_budget_exceeded");
            return;
        }

        SelectTool(active.Tool);
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        Game1.player.lastClick = new Vector2(active.Target.X * Game1.tileSize + Game1.tileSize / 2, active.Target.Y * Game1.tileSize + Game1.tileSize / 2);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteBreakContainerBlocked(active, "break_container_" + inputReason);
            return;
        }
        active.ButtonHeld = true;
        active.SwingCount++;
    }

    private void CompleteBreakContainer(ActiveBreakContainer active)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeBreakContainer = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.Tool.QualifiedItemId,
            ToolUpgradeLevel = active.Tool.UpgradeLevel,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "break_container",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_heavy_hitter_input_removed_container", "released_contents_left_as_game_debris", "native_swing_count=" + active.SwingCount },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = BreakContainerObservedEffect(active.Target) + ";health_sequence=" + string.Join(",", active.ObservedHealth) + ";native_swings=" + active.SwingCount,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.Container.QualifiedItemId + ":health=" + active.HealthBefore, After = "removed" },
                new SimulatedFactChange { Path = "mining.debris.count", Before = active.DebrisCountBefore.ToString(), After = active.Mine.debris.Count.ToString() }
            }
        });
    }

    private void CompleteBreakContainerBlocked(ActiveBreakContainer active, string reason)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeBreakContainer = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "break_container", active.RequestedEffect, BreakContainerObservedEffect(active.Target) + ";native_swings=" + active.SwingCount, reason));
    }

    private static Tool? BestContainerTool()
    {
        return Game1.player.Items.OfType<Tool>()
            .Where(tool => tool.isHeavyHitter())
            .OrderByDescending(tool => tool is MeleeWeapon weapon && weapon.type.Value == MeleeWeapon.club ? 2 : 1)
            .ThenBy(tool => tool is MeleeWeapon weapon ? Math.Max(40, 400 - weapon.speed.Value * 40) : 400)
            .FirstOrDefault();
    }

    private static void RestoreSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = slotIndex;
        }
    }

    private static int? ReadBreakableContainerHealth(BreakableContainer container)
    {
        var netInt = BreakableContainerHealthField?.GetValue(container);
        return netInt?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(netInt) as int?;
    }

    private static string BreakContainerObservedEffect(Point target)
    {
        var mine = Game1.currentLocation as MineShaft;
        var exists = mine?.objects.TryGetValue(new Vector2(target.X, target.Y), out var obj) == true && obj is BreakableContainer;
        return "location=" + (mine?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";container_present=" + exists.ToString().ToLowerInvariant();
    }

    private static bool ImmediateMiningThreat(MineShaft mine)
    {
        var playerTile = Game1.player.TilePoint;
        return mine.characters.OfType<Monster>()
            .Any(monster => monster.Health > 0 && ManhattanDistance(playerTile, monster.TilePoint) <= 3);
    }

    private void StartDescendLadder(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "mine.level=before+1;native_action=MineShaft.checkAction(ladder)";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_target_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_tool_or_menu_conflict"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 173)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_tile_not_live_ladder"));
            return;
        }
        var path = BuildAdjacentToolPath(mine, target, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_path_unavailable:" + pathReason));
            return;
        }

        activeDescendLadder = new ActiveDescendLadder(pending, mine, mine.mineLevel, target, path, requested);
    }

    private void TickDescendLadder()
    {
        if (activeDescendLadder is null)
        {
            return;
        }

        var active = activeDescendLadder;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_timeout");
            return;
        }

        if (active.ActionIssued)
        {
            if (Game1.currentLocation is MineShaft afterMine && afterMine.mineLevel == active.MineLevelBefore + 1)
            {
                CompleteDescendLadder(active, afterMine);
                return;
            }
            if (!ReferenceEquals(Game1.currentLocation, active.MineBefore) && Game1.currentLocation is not MineShaft)
            {
                CompleteDescendLadderBlocked(active, "descend_ladder_unexpected_location_after_action");
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_location_changed_before_action");
            return;
        }
        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteDescendLadderBlocked(active, "descend_ladder_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                var repaired = BuildAdjacentToolPath(active.MineBefore, active.Target, 512, out var repairReason);
                if (repaired is null)
                {
                    CompleteDescendLadderBlocked(active, "descend_ladder_replan_failed:" + repairReason);
                    return;
                }
                active.Path = repaired;
                active.PathIndex = 0;
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    active.Path.Clear();
                    active.PathIndex = 0;
                    active.StuckTicks = 0;
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 173)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_native_action_not_handled");
            return;
        }
        active.ActionIssued = true;
    }

    private void CompleteDescendLadder(ActiveDescendLadder active, MineShaft afterMine)
    {
        StopAllMovement();
        activeDescendLadder = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "descend_ladder",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_ladder", "native_mineshaft_check_action_handled", "exact_next_mine_level_loaded", "no_direct_enter_mine_call" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = DescendLadderObservedEffect(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.current_mine.mine_level", Before = active.MineLevelBefore.ToString(), After = afterMine.mineLevel.ToString() },
                new SimulatedFactChange { Path = "player.location_id", Before = active.MineBefore.NameOrUniqueName, After = afterMine.NameOrUniqueName }
            }
        });
    }

    private void CompleteDescendLadderBlocked(ActiveDescendLadder active, string reason)
    {
        StopAllMovement();
        activeDescendLadder = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "descend_ladder", active.RequestedEffect, DescendLadderObservedEffect(), reason));
    }

    private static string DescendLadderObservedEffect()
    {
        return Game1.currentLocation is MineShaft mine
            ? "location=" + mine.NameOrUniqueName + ";mine_level=" + mine.mineLevel + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y
            : "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";mine_level=none";
    }

    private void StartDescendShaft(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "mine.level=before+expected_delta;player.health=expected_after;native_dialogue=Shaft_Jump";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.ExpectedMineLevelDelta.HasValue || !request.ExpectedMineLevelAfter.HasValue ||
            !request.ExpectedHealthCost.HasValue || !request.ExpectedHealthAfter.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_exact_preview_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_tool_or_menu_conflict"));
            return;
        }

        var expectedDelta = request.ExpectedMineLevelDelta.Value;
        var expectedCost = request.ExpectedHealthCost.Value;
        if (expectedDelta <= 0 || expectedCost != expectedDelta * 3 ||
            request.ExpectedMineLevelAfter.Value != mine.mineLevel + expectedDelta ||
            request.ExpectedHealthAfter.Value != Math.Max(1, Game1.player.health - expectedCost))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_preview_mismatch_live_state"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 174)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_tile_not_live_shaft"));
            return;
        }
        var path = BuildAdjacentToolPath(mine, target, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_path_unavailable:" + pathReason));
            return;
        }

        activeDescendShaft = new ActiveDescendShaft(
            pending,
            mine,
            mine.mineLevel,
            Game1.player.health,
            target,
            path,
            expectedDelta,
            request.ExpectedMineLevelAfter.Value,
            expectedCost,
            request.ExpectedHealthAfter.Value,
            requested);
    }

    private void TickDescendShaft()
    {
        if (activeDescendShaft is null)
        {
            return;
        }

        var active = activeDescendShaft;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_timeout");
            return;
        }

        if (active.DialogueConfirmed)
        {
            if (Game1.currentLocation is MineShaft afterMine && afterMine.mineLevel == active.ExpectedMineLevelAfter)
            {
                if (Game1.player.health != active.ExpectedHealthAfter)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_health_after_mismatch");
                    return;
                }
                CompleteDescendShaft(active, afterMine);
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_location_changed_before_confirmation");
            return;
        }

        if (active.PromptOpened)
        {
            if (Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "Shaft", StringComparison.Ordinal))
            {
                CompleteDescendShaftBlocked(active, "descend_shaft_prompt_drift");
                return;
            }

            active.MineBefore.answerDialogueAction("Shaft_Jump", new[] { "Shaft", "Jump" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            active.DialogueConfirmed = true;
            return;
        }

        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteDescendShaftBlocked(active, "descend_shaft_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                var repaired = BuildAdjacentToolPath(active.MineBefore, active.Target, 512, out var repairReason);
                if (repaired is null)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_replan_failed:" + repairReason);
                    return;
                }
                active.Path = repaired;
                active.PathIndex = 0;
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    active.Path.Clear();
                    active.PathIndex = 0;
                    active.StuckTicks = 0;
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 174)
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled || Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "Shaft", StringComparison.Ordinal))
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_native_prompt_not_opened");
            return;
        }
        active.PromptOpened = true;
    }

    private void CompleteDescendShaft(ActiveDescendShaft active, MineShaft afterMine)
    {
        StopAllMovement();
        activeDescendShaft = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "descend_shaft",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_shaft", "native_shaft_prompt_observed", "native_shaft_jump_answer_handled", "exact_previewed_floor_and_health_observed" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = DescendShaftObservedEffect(),
            ShaftMineLevelBefore = active.MineLevelBefore,
            ShaftMineLevelAfter = afterMine.mineLevel,
            ShaftLevelDelta = afterMine.mineLevel - active.MineLevelBefore,
            ShaftHealthBefore = active.HealthBefore,
            ShaftHealthAfter = Game1.player.health,
            ShaftNativeDialogueHandled = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.current_mine.mine_level", Before = active.MineLevelBefore.ToString(), After = afterMine.mineLevel.ToString() },
                new SimulatedFactChange { Path = "player.health", Before = active.HealthBefore.ToString(), After = Game1.player.health.ToString() }
            }
        });
    }

    private void CompleteDescendShaftBlocked(ActiveDescendShaft active, string reason)
    {
        StopAllMovement();
        activeDescendShaft = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "descend_shaft", active.RequestedEffect, DescendShaftObservedEffect(), reason);
        result.ShaftMineLevelBefore = active.MineLevelBefore;
        result.ShaftMineLevelAfter = Game1.currentLocation is MineShaft mine ? mine.mineLevel : null;
        result.ShaftLevelDelta = result.ShaftMineLevelAfter - active.MineLevelBefore;
        result.ShaftHealthBefore = active.HealthBefore;
        result.ShaftHealthAfter = Game1.player.health;
        result.ShaftNativeDialogueHandled = active.DialogueConfirmed;
        active.Pending.Completion.SetResult(result);
    }

    private static string DescendShaftObservedEffect()
    {
        return Game1.currentLocation is MineShaft mine
            ? "location=" + mine.NameOrUniqueName + ";mine_level=" + mine.mineLevel + ";health=" + Game1.player.health + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y
            : "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";mine_level=none;health=" + Game1.player.health;
    }

    private void StartExitMine(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "leave_loaded_mine=true;native_dialogue=ExitMine_Leave;reason=" + request.RetreatReason;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.ExpectedTargetLocation) ||
            !request.ExpectedArrivalTileX.HasValue || !request.ExpectedArrivalTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.RetreatReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_exact_target_and_reason_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_tool_or_menu_conflict"));
            return;
        }

        var expectedDestination = ExpectedMineExitDestination(mine.mineLevel);
        if (!string.Equals(request.ExpectedTargetLocation, expectedDestination.LocationId, StringComparison.Ordinal) ||
            request.ExpectedArrivalTileX.Value != expectedDestination.TileX ||
            request.ExpectedArrivalTileY.Value != expectedDestination.TileY)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_destination_mismatch_live_mine_kind"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 115)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_tile_not_live_exit"));
            return;
        }
        var path = BuildAdjacentToolPath(mine, target, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_path_unavailable:" + pathReason));
            return;
        }

        activeExitMine = new ActiveExitMine(
            pending,
            mine,
            mine.mineLevel,
            Game1.timeOfDay,
            Game1.player.health,
            Game1.player.Stamina,
            Game1.player.TilePoint,
            target,
            path,
            expectedDestination.LocationId,
            expectedDestination.TileX,
            expectedDestination.TileY,
            request.RetreatReason,
            requested);
    }

    private void TickExitMine()
    {
        if (activeExitMine is null)
        {
            return;
        }

        var active = activeExitMine;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteExitMineBlocked(active, "exit_mine_timeout");
            return;
        }

        if (active.DialogueConfirmed)
        {
            if (Game1.currentLocation is not MineShaft)
            {
                if (!string.Equals(Game1.currentLocation?.NameOrUniqueName, active.ExpectedLocationId, StringComparison.Ordinal) ||
                    Game1.player.TilePoint.X != active.ExpectedTileX || Game1.player.TilePoint.Y != active.ExpectedTileY)
                {
                    CompleteExitMineBlocked(active, "exit_mine_destination_after_native_answer_mismatch");
                    return;
                }
                CompleteExitMine(active);
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteExitMineBlocked(active, "exit_mine_location_changed_before_confirmation");
            return;
        }

        if (active.PromptOpened)
        {
            if (Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "ExitMine", StringComparison.Ordinal))
            {
                CompleteExitMineBlocked(active, "exit_mine_prompt_drift");
                return;
            }

            active.MineBefore.answerDialogueAction("ExitMine_Leave", new[] { "ExitMine", "Leave" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            active.DialogueConfirmed = true;
            return;
        }

        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteExitMineBlocked(active, "exit_mine_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                var repaired = BuildAdjacentToolPath(active.MineBefore, active.Target, 512, out var repairReason);
                if (repaired is null)
                {
                    CompleteExitMineBlocked(active, "exit_mine_replan_failed:" + repairReason);
                    return;
                }
                active.Path = repaired;
                active.PathIndex = 0;
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    active.Path.Clear();
                    active.PathIndex = 0;
                    active.StuckTicks = 0;
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 115)
        {
            CompleteExitMineBlocked(active, "exit_mine_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled || Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "ExitMine", StringComparison.Ordinal))
        {
            CompleteExitMineBlocked(active, "exit_mine_native_prompt_not_opened");
            return;
        }
        active.PromptOpened = true;
    }

    private void CompleteExitMine(ActiveExitMine active)
    {
        StopAllMovement();
        activeExitMine = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.ExpectedLocationId,
            TargetTileX = active.ExpectedTileX,
            TargetTileY = active.ExpectedTileY,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "exit_mine",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_exit", "native_exit_prompt_observed", "native_exit_leave_answer_handled", "exact_decompiled_destination_observed" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = ExitMineObservedEffect(),
            RetreatReason = active.RetreatReason,
            RetreatMineLevelBefore = active.MineLevelBefore,
            RetreatTimeBefore = active.TimeBefore,
            RetreatHealthBefore = active.HealthBefore,
            RetreatEnergyBefore = active.EnergyBefore,
            RetreatDestination = active.ExpectedLocationId + ":" + active.ExpectedTileX + "," + active.ExpectedTileY,
            RetreatNativeDialogueHandled = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.location_id", Before = active.MineBefore.NameOrUniqueName, After = active.ExpectedLocationId },
                new SimulatedFactChange { Path = "player.tile", Before = active.PlayerTileBefore.X + "," + active.PlayerTileBefore.Y, After = active.ExpectedTileX + "," + active.ExpectedTileY }
            }
        });
    }

    private void CompleteExitMineBlocked(ActiveExitMine active, string reason)
    {
        StopAllMovement();
        activeExitMine = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "exit_mine", active.RequestedEffect, ExitMineObservedEffect(), reason);
        result.RetreatReason = active.RetreatReason;
        result.RetreatMineLevelBefore = active.MineLevelBefore;
        result.RetreatTimeBefore = active.TimeBefore;
        result.RetreatHealthBefore = active.HealthBefore;
        result.RetreatEnergyBefore = active.EnergyBefore;
        result.RetreatDestination = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        result.RetreatNativeDialogueHandled = active.DialogueConfirmed;
        active.Pending.Completion.SetResult(result);
    }

    private static (string LocationId, int TileX, int TileY) ExpectedMineExitDestination(int mineLevel)
    {
        return mineLevel == 77377
            ? ("Mine", 67, 10)
            : mineLevel > 120 ? ("SkullCave", 3, 4) : ("Mine", 23, 8);
    }

    private static string ExitMineObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";time=" + Game1.timeOfDay +
            ";health=" + Game1.player.health +
            ";energy=" + Game1.player.Stamina.ToString("0.###");
    }

    private void StartConsumeFood(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "inventory[" + (request.SlotIndex?.ToString() ?? "missing") + "].stack-=1;player.health>before;native_dialogue=Eat_Yes";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), reasons.ToArray()));
            return;
        }

        if (activeConsumeFood is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_executor_busy"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_active_menu_must_be_closed"));
            return;
        }
        if (Game1.player.UsingTool || Game1.player.isEating || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_tool_or_animation_conflict"));
            return;
        }
        if (!request.SlotIndex.HasValue || request.SlotIndex.Value < 0 || request.SlotIndex.Value >= Game1.player.Items.Count)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_slot_out_of_range"));
            return;
        }

        var slotIndex = request.SlotIndex.Value;
        if (Game1.player.Items[slotIndex] is not StardewValley.Object food || food.Edibility <= 0 || food.healthRecoveredOnConsumption() <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_slot_not_healing_food"));
            return;
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId) || !string.Equals(food.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_item_identity_mismatch"));
            return;
        }
        if (Game1.player.health >= Game1.player.maxHealth)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_health_already_full"));
            return;
        }
        if (Game1.player.hasBuff("25") && !food.HasContextTag("ginger_item"))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_nauseous"));
            return;
        }
        if (Game1.player.hasBuff("6"))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_food_fullness_active"));
            return;
        }
        if (Game1.player.team.SpecialOrderRuleActive("SC_NO_FOOD") && mine.getMineArea() == 121)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "consume_food", requested, ConsumeFoodObservedEffect(request.SlotIndex), "consume_food_special_order_forbids_food"));
            return;
        }

        activeConsumeFood = new ActiveConsumeFood(
            pending,
            mine.NameOrUniqueName,
            slotIndex,
            food.QualifiedItemId,
            food.Stack,
            Game1.player.CurrentToolIndex,
            Game1.player.health,
            Game1.player.Stamina,
            requested);
    }

    private void TickConsumeFood()
    {
        if (activeConsumeFood is null)
        {
            return;
        }

        var active = activeConsumeFood;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteConsumeFoodBlocked(active, "consume_food_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteConsumeFoodBlocked(active, "consume_food_native_lifecycle_timeout");
            return;
        }

        switch (active.Stage)
        {
            case ConsumeFoodStage.PressUse:
                if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || Game1.player.isEating || !Game1.player.CanMove)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_pre_input_state_drift");
                    return;
                }
                if (!ConsumeFoodSlotMatches(active))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_slot_drift_before_input");
                    return;
                }

                Game1.player.CurrentToolIndex = active.FoodSlotIndex;
                if (!TryApplySmapiRightButtonOverride(pressed: true, out var pressReason))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_right_press_failed:" + pressReason);
                    return;
                }
                active.RightButtonHeld = true;
                active.Stage = ConsumeFoodStage.ReleaseUse;
                return;

            case ConsumeFoodStage.ReleaseUse:
                ReleaseConsumeFoodRightButton(active);
                active.Stage = ConsumeFoodStage.WaitForPrompt;
                return;

            case ConsumeFoodStage.WaitForPrompt:
                if (Game1.activeClickableMenu is DialogueBox && string.Equals(Game1.currentLocation.lastQuestionKey, "Eat", StringComparison.Ordinal))
                {
                    active.Stage = ConsumeFoodStage.ConfirmPrompt;
                    return;
                }
                if (active.ElapsedTicks > 120)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_eat_prompt_not_observed");
                }
                return;

            case ConsumeFoodStage.ConfirmPrompt:
                if (Game1.activeClickableMenu is not DialogueBox || !string.Equals(Game1.currentLocation.lastQuestionKey, "Eat", StringComparison.Ordinal))
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_eat_prompt_drift");
                    return;
                }

                Game1.currentLocation.answerDialogueAction("Eat_Yes", new[] { "Eat" });
                Game1.activeClickableMenu = null;
                Game1.dialogueUp = false;
                active.NativeConfirmationIssued = true;
                active.Stage = ConsumeFoodStage.WaitForCompletion;
                return;

            case ConsumeFoodStage.WaitForCompletion:
                active.EatingObserved |= Game1.player.isEating;
                if (Game1.player.isEating || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
                {
                    return;
                }
                if (!active.EatingObserved)
                {
                    return;
                }

                var stackAfter = ConsumeFoodStackAt(active.FoodSlotIndex, active.FoodQualifiedItemId);
                if (stackAfter != active.FoodStackBefore - 1)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_stack_delta_mismatch");
                    return;
                }
                if (Game1.player.health <= active.HealthBefore)
                {
                    CompleteConsumeFoodBlocked(active, "consume_food_health_not_recovered");
                    return;
                }

                CompleteConsumeFood(active, stackAfter);
                return;
        }
    }

    private static bool ConsumeFoodSlotMatches(ActiveConsumeFood active)
    {
        return active.FoodSlotIndex >= 0 && active.FoodSlotIndex < Game1.player.Items.Count &&
            Game1.player.Items[active.FoodSlotIndex] is StardewValley.Object food &&
            string.Equals(food.QualifiedItemId, active.FoodQualifiedItemId, StringComparison.Ordinal) &&
            food.Stack == active.FoodStackBefore;
    }

    private static int ConsumeFoodStackAt(int slotIndex, string qualifiedItemId)
    {
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count || Game1.player.Items[slotIndex] is not Item item)
        {
            return 0;
        }
        return string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal) ? item.Stack : 0;
    }

    private void CompleteConsumeFood(ActiveConsumeFood active, int stackAfter)
    {
        ReleaseConsumeFoodRightButton(active);
        RestoreConsumeFoodSlot(active);
        activeConsumeFood = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            EnergyBefore = active.EnergyBefore,
            EnergyAfter = Game1.player.Stamina,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "consume_food",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_right_click_opened_eat_prompt", "native_eat_yes_completed", "exact_food_stack_decremented", "health_recovery_observed", "previous_toolbar_slot_restored" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = ConsumeFoodObservedEffect(active.FoodSlotIndex),
            RecoveryFoodSlotIndex = active.FoodSlotIndex,
            RecoveryFoodQualifiedItemId = active.FoodQualifiedItemId,
            RecoveryFoodStackBefore = active.FoodStackBefore,
            RecoveryFoodStackAfter = stackAfter,
            RecoveryHealthBefore = active.HealthBefore,
            RecoveryHealthAfter = Game1.player.health,
            RecoveryRestoreSlotIndex = active.RestoreSlotIndex,
            RecoverySafetyStatus = "native_eating_lifecycle_verified",
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory[" + active.FoodSlotIndex + "].stack", Before = active.FoodStackBefore.ToString(), After = stackAfter.ToString() },
                new SimulatedFactChange { Path = "player.health", Before = active.HealthBefore.ToString(), After = Game1.player.health.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = active.EnergyBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteConsumeFoodBlocked(ActiveConsumeFood active, string reason)
    {
        ReleaseConsumeFoodRightButton(active);
        RestoreConsumeFoodSlot(active);
        activeConsumeFood = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "consume_food", active.RequestedEffect, ConsumeFoodObservedEffect(active.FoodSlotIndex), reason);
        result.RecoveryFoodSlotIndex = active.FoodSlotIndex;
        result.RecoveryFoodQualifiedItemId = active.FoodQualifiedItemId;
        result.RecoveryFoodStackBefore = active.FoodStackBefore;
        result.RecoveryFoodStackAfter = ConsumeFoodStackAt(active.FoodSlotIndex, active.FoodQualifiedItemId);
        result.RecoveryHealthBefore = active.HealthBefore;
        result.RecoveryHealthAfter = Game1.player.health;
        result.RecoveryRestoreSlotIndex = active.RestoreSlotIndex;
        result.RecoverySafetyStatus = "blocked_or_drifted";
        active.Pending.Completion.SetResult(result);
    }

    private void ReleaseConsumeFoodRightButton(ActiveConsumeFood active)
    {
        if (!active.RightButtonHeld)
        {
            return;
        }
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        active.RightButtonHeld = false;
    }

    private static void RestoreConsumeFoodSlot(ActiveConsumeFood active)
    {
        if (active.RestoreSlotIndex >= 0 && active.RestoreSlotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
    }

    private static string ConsumeFoodObservedEffect(int? slotIndex)
    {
        var slot = "missing";
        if (slotIndex.HasValue && slotIndex.Value >= 0 && slotIndex.Value < Game1.player.Items.Count && Game1.player.Items[slotIndex.Value] is Item item)
        {
            slot = item.QualifiedItemId + ":stack=" + item.Stack;
        }
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";slot=" + (slotIndex?.ToString() ?? "missing") + ":" + slot +
            ";health=" + Game1.player.health + ";energy=" + Game1.player.Stamina.ToString("0.###") +
            ";is_eating=" + Game1.player.isEating.ToString().ToLowerInvariant() +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }

    private void StartShootMonster(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        const string requested = "target_monster.defeated=true;native_input=full_charge_slingshot";
        if (Game1.currentLocation is not MineShaft mine ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
            string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "target_or_location=missing", "slingshot_target_identity_required"));
            return;
        }
        var targets = mine.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.Name, request.TargetName, StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "matching_target_count=" + targets.Length, "slingshot_target_not_unique"));
            return;
        }
        if (!request.SlingshotSlotIndex.HasValue ||
            request.SlingshotSlotIndex.Value < 0 ||
            request.SlingshotSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.SlingshotSlotIndex.Value] is not Slingshot slingshot ||
            slingshot.attachments.Count == 0 ||
            slingshot.attachments[0] is not StardewValley.Object ammo ||
            ammo.Stack <= 0 ||
            !string.Equals(ammo.QualifiedItemId, request.SlingshotAmmoQualifiedItemId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "slingshot=missing_or_ammo_drifted", "loaded_slingshot_contract_not_met"));
            return;
        }
        if (!HasClearProjectilePath(mine, Game1.player.TilePoint, targets[0].TilePoint))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "projectile_path=blocked", "slingshot_projectile_path_blocked"));
            return;
        }
        if (ammo.QualifiedItemId == "(O)441" &&
            !ExplosiveAmmoAreaIsSafe(mine, targets[0], out var explosiveSafetyReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested,
                "explosive_area=unsafe", explosiveSafetyReason));
            return;
        }

        activeShootMonster = new ActiveShootMonster(
            pending,
            mine,
            targets[0],
            slingshot,
            ammo.QualifiedItemId,
            ammo.Stack,
            Game1.player.CurrentToolIndex,
            Math.Clamp(request.MaxAttacks, 1, 256),
            requested);
        SlingshotAimPatch.ActiveSlingshot = slingshot;
        SlingshotAimPatch.AimWorldPixel = targets[0].GetBoundingBox().Center;
    }

    private void TickShootMonster()
    {
        if (activeShootMonster is null)
        {
            return;
        }
        var active = activeShootMonster;
        SlingshotAimPatch.ActiveSlingshot = active.Slingshot;
        SlingshotAimPatch.AimWorldPixel = active.Target.GetBoundingBox().Center;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteShootMonsterBlocked(active, "slingshot_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteShootMonsterBlocked(active, "slingshot_timeout");
            return;
        }
        if (active.Target.Health <= 0 || !active.Mine.characters.Contains(active.Target))
        {
            if (active.ButtonHeld)
            {
                active.Slingshot.finish();
                active.ButtonHeld = false;
                return;
            }
            if (Game1.player.UsingTool || Game1.player.usingSlingshot)
            {
                return;
            }
            CompleteShootMonster(active);
            return;
        }
        if (!HasClearProjectilePath(active.Mine, Game1.player.TilePoint, active.Target.TilePoint))
        {
            CompleteShootMonsterBlocked(active, "slingshot_projectile_path_drifted");
            return;
        }
        if (active.Slingshot.attachments.Count == 0 ||
            active.Slingshot.attachments[0] is not StardewValley.Object ammo ||
            !string.Equals(ammo.QualifiedItemId, active.AmmoQualifiedItemId, StringComparison.Ordinal) ||
            ammo.Stack <= 0)
        {
            CompleteShootMonsterBlocked(active, "slingshot_ammo_exhausted_or_drifted");
            return;
        }
        if (active.AmmoQualifiedItemId == "(O)441" &&
            !ExplosiveAmmoAreaIsSafe(active.Mine, active.Target, out var explosiveSafetyReason))
        {
            CompleteShootMonsterBlocked(active, explosiveSafetyReason);
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        if (active.ButtonHeld)
        {
            active.HoldTicks++;
            if (active.HoldTicks < 20)
            {
                return;
            }
            active.Slingshot.onRelease(active.Mine, targetCenter.X, targetCenter.Y, Game1.player);
            active.ButtonHeld = false;
            active.CooldownTicks = 12;
            active.AttackCount++;
            active.AimPrepared = false;
            return;
        }
        if (active.CooldownTicks > 0)
        {
            active.CooldownTicks--;
            return;
        }
        if (Game1.player.UsingTool || Game1.player.usingSlingshot || Game1.activeClickableMenu is not null || Game1.eventUp)
        {
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteShootMonsterBlocked(active, "slingshot_attack_budget_exceeded");
            return;
        }
        if (active.Target.Health < active.LastTargetHealth)
        {
            active.HitCount++;
            active.LastTargetHealth = active.Target.Health;
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        Game1.player.CurrentToolIndex = active.SlingshotSlotIndex;
        var targetDirection = active.Target.GetBoundingBox().Center.ToVector2();
        Game1.player.faceGeneralDirection(targetDirection, 0);
        if (!active.AimPrepared)
        {
            active.AimPrepared = true;
            return;
        }
        Game1.player.lastClick = targetCenter.ToVector2();
        Game1.player.BeginUsingTool();
        if (!Game1.player.usingSlingshot)
        {
            CompleteShootMonsterBlocked(active, "slingshot_native_begin_using_not_observed");
            return;
        }
        active.ButtonHeld = true;
        active.HoldTicks = 0;
    }

    private void CompleteShootMonster(ActiveShootMonster active)
    {
        active.Slingshot.finish();
        SlingshotAimPatch.Clear(active.Slingshot);
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activeShootMonster = null;
        var ammoAfter = active.Slingshot.attachments.Count > 0 && active.Slingshot.attachments[0] is StardewValley.Object ammo
            ? ammo.Stack
            : 0;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            ToolQualifiedItemId = active.Slingshot.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "shoot_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_full_charge_slingshot_defeated_target", "ammo_consumption_observed" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "target_health=" + active.Target.Health + ";ammo_stack=" + ammoAfter,
            CombatMethod = "slingshot",
            CombatConsumableQualifiedItemId = active.AmmoQualifiedItemId,
            CombatConsumableCountBefore = active.AmmoCountBefore,
            CombatConsumableCountAfter = ammoAfter,
            CombatTargetRuntimeType = active.Target.GetType().FullName ?? active.Target.GetType().Name,
            CombatTargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.Target).ToString("X8"),
            CombatTargetName = active.Target.Name,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatTargetDefeated = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.monsters[target].health", Before = active.TargetHealthBefore.ToString(), After = active.Target.Health.ToString() },
                new SimulatedFactChange { Path = "player.slingshot.ammo.stack", Before = active.AmmoCountBefore.ToString(), After = ammoAfter.ToString() }
            }
        });
    }

    private void CompleteShootMonsterBlocked(ActiveShootMonster active, string reason)
    {
        active.Slingshot.finish();
        SlingshotAimPatch.Clear(active.Slingshot);
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activeShootMonster = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "shoot_monster", active.RequestedEffect,
            "target_health=" + active.Target.Health, reason));
    }

    private static bool HasClearProjectilePath(GameLocation location, Point start, Point target)
    {
        var x = start.X;
        var y = start.Y;
        var deltaX = Math.Abs(target.X - start.X);
        var stepX = start.X < target.X ? 1 : -1;
        var deltaY = -Math.Abs(target.Y - start.Y);
        var stepY = start.Y < target.Y ? 1 : -1;
        var error = deltaX + deltaY;
        while (x != target.X || y != target.Y)
        {
            var doubled = 2 * error;
            if (doubled >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }
            if (doubled <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
            if ((x != target.X || y != target.Y) &&
                ((location.objects.TryGetValue(new Vector2(x, y), out var obj) && !obj.isPassable()) || location.BlocksDamageLOS(x, y)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ExplosiveAmmoAreaIsSafe(MineShaft mine, Monster target, out string reason)
    {
        const int radius = 2;
        const int targetMotionMargin = 1;
        for (var offsetX = -targetMotionMargin; offsetX <= targetMotionMargin; offsetX++)
        {
            for (var offsetY = -targetMotionMargin; offsetY <= targetMotionMargin; offsetY++)
            {
                var possibleCenter = new Point(target.TilePoint.X + offsetX, target.TilePoint.Y + offsetY);
                var damageRectangle = new Rectangle(
                    (possibleCenter.X - radius) * Game1.tileSize,
                    (possibleCenter.Y - radius) * Game1.tileSize,
                    (radius * 2 + 1) * Game1.tileSize,
                    (radius * 2 + 1) * Game1.tileSize);
                if (damageRectangle.Intersects(Game1.player.GetBoundingBox()))
                {
                    reason = "explosive_ammo_player_inside_target_motion_envelope";
                    return false;
                }
                if (mine.farmers.Any(farmer =>
                    farmer != Game1.player && damageRectangle.Intersects(farmer.GetBoundingBox())))
                {
                    reason = "explosive_ammo_other_farmer_inside_target_motion_envelope";
                    return false;
                }
                foreach (var tile in BombAffectedTiles(possibleCenter, radius))
                {
                    if (mine.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj) &&
                        !obj.IsBreakableStone() &&
                        obj is not BreakableContainer)
                    {
                        reason = "explosive_ammo_protected_object_inside_target_motion_envelope";
                        return false;
                    }
                    if (mine.terrainFeatures.ContainsKey(new Vector2(tile.X, tile.Y)))
                    {
                        reason = "explosive_ammo_terrain_feature_inside_target_motion_envelope";
                        return false;
                    }
                }
            }
        }
        reason = string.Empty;
        return true;
    }

    private void StartPlaceBomb(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        const string requested = "bomb.placed=true;escape_damage_square=true;native_input=MouseRight+WASD";
        if (Game1.currentLocation is not MineShaft mine ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.EscapeTileX.HasValue || !request.EscapeTileY.HasValue ||
            !request.BombSlotIndex.HasValue || !request.BombRadiusTiles.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "bomb_contract=missing", "bomb_target_escape_and_slot_required"));
            return;
        }
        var slot = request.BombSlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object bomb ||
            !string.Equals(bomb.QualifiedItemId, request.BombQualifiedItemId, StringComparison.Ordinal) ||
            bomb.Stack <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "bomb=missing_or_drifted", "bomb_inventory_contract_not_met"));
            return;
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var escape = new Point(request.EscapeTileX.Value, request.EscapeTileY.Value);
        if (!AreAdjacent(stand, target))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "stand_target=not_adjacent", "bomb_placement_stand_invalid"));
            return;
        }
        if (mine.objects.ContainsKey(new Vector2(target.X, target.Y)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "target=occupied", "bomb_placement_tile_not_empty"));
            return;
        }
        Monster? targetMonster = null;
        if (!string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity))
        {
            var targetMonsters = mine.characters.OfType<Monster>()
                .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
                .Where(monster => string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
                    string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
                .ToArray();
            if (targetMonsters.Length != 1 ||
                request.CombatTerminalState == "mummy_finalized" &&
                (targetMonsters[0] is not Mummy mummy || mummy.reviveTimer.Value <= 0))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested,
                    "matching_target_count=" + targetMonsters.Length, "bomb_target_terminal_state_not_ready"));
                return;
            }
            targetMonster = targetMonsters[0];
            var damageRectangle = new Rectangle(
                (target.X - request.BombRadiusTiles.Value) * Game1.tileSize,
                (target.Y - request.BombRadiusTiles.Value) * Game1.tileSize,
                (request.BombRadiusTiles.Value * 2 + 1) * Game1.tileSize,
                (request.BombRadiusTiles.Value * 2 + 1) * Game1.tileSize);
            if (!damageRectangle.Intersects(targetMonster.GetBoundingBox()))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested,
                    "target_monster=outside_damage_square", "bomb_target_outside_damage_square"));
                return;
            }
        }
        var path = TryBuildTilePath(mine, Game1.player.TilePoint, stand, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason, avoidSoftObstacles: true);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "placement_path=blocked", pathReason));
            return;
        }
        activePlaceBomb = new ActivePlaceBomb(
            pending,
            mine,
            target,
            escape,
            path,
            slot,
            bomb,
            request.BombRadiusTiles.Value,
            Game1.player.CurrentToolIndex,
            BombAffectedObjectCount(mine, target, request.BombRadiusTiles.Value),
            targetMonster,
            request.CombatTerminalState,
            requested);
    }

    private void TickPlaceBomb()
    {
        if (activePlaceBomb is null)
        {
            return;
        }
        var active = activePlaceBomb;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompletePlaceBombBlocked(active, "bomb_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompletePlaceBombBlocked(active, "bomb_timeout");
            return;
        }

        if (active.Stage == PlaceBombStage.MoveToPlacement)
        {
            if (!TickBombPathMovement(active, active.Path, out var movementReason))
            {
                if (!string.IsNullOrEmpty(movementReason))
                {
                    CompletePlaceBombBlocked(active, movementReason);
                }
                return;
            }
            active.Stage = PlaceBombStage.AimPlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.AimPlacement)
        {
            StopAllMovement();
            Game1.player.CurrentToolIndex = active.BombSlotIndex;
            var pixel = new Point(active.Target.X * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.X,
                active.Target.Y * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.Y);
            Game1.setMousePosition(pixel.X, pixel.Y, ui_scale: false);
            active.Stage = PlaceBombStage.PressPlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.PressPlacement)
        {
            StopAllMovement();
            Game1.player.CurrentToolIndex = active.BombSlotIndex;
            var pixel = new Point(active.Target.X * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.X,
                active.Target.Y * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.Y);
            Game1.setMousePosition(pixel.X, pixel.Y, ui_scale: false);
            if (!TryApplySmapiRightButtonOverride(pressed: true, out var reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            active.Stage = PlaceBombStage.ReleasePlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.ReleasePlacement)
        {
            if (!TryApplySmapiRightButtonOverride(pressed: false, out var reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            var stackAfter = BombStackAt(active.BombSlotIndex, active.BombQualifiedItemId);
            if (stackAfter >= active.BombStackBefore)
            {
                CompletePlaceBombBlocked(active, "bomb_native_placement_not_observed");
                return;
            }
            active.PlacedAtTick = active.ElapsedTicks;
            active.EscapePath = TryBuildTilePath(active.Mine, Game1.player.TilePoint, active.Escape, 64, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false) ?? new List<Point>();
            if (active.EscapePath.Count == 0)
            {
                if (!TryRebuildBombEscape(active))
                {
                    CompletePlaceBombBlocked(active, "bomb_escape_path_drifted:" + pathReason);
                }
                return;
            }
            active.PathIndex = 0;
            active.Stage = PlaceBombStage.Escape;
        }

        if (active.Stage == PlaceBombStage.Escape)
        {
            if (!TickBombPathMovement(active, active.EscapePath, out var movementReason))
            {
                if (!string.IsNullOrEmpty(movementReason))
                {
                    if (!TryRebuildBombEscape(active))
                    {
                        CompletePlaceBombBlocked(active, movementReason);
                    }
                }
                return;
            }
            if (Math.Abs(Game1.player.TilePoint.X - active.Target.X) <= active.Radius &&
                Math.Abs(Game1.player.TilePoint.Y - active.Target.Y) <= active.Radius)
            {
                CompletePlaceBombBlocked(active, "bomb_escape_finished_inside_damage_square");
                return;
            }
            active.Stage = PlaceBombStage.WaitForExplosion;
        }

        if (active.Stage == PlaceBombStage.WaitForExplosion &&
            active.ElapsedTicks - active.PlacedAtTick >= 180 &&
            !active.Mine.temporarySprites.Any(sprite => sprite.bombRadius == active.Radius &&
                sprite.position.Equals(new Vector2(active.Target.X * Game1.tileSize, active.Target.Y * Game1.tileSize))))
        {
            CompletePlaceBomb(active);
        }
    }

    private bool TryRebuildBombEscape(ActivePlaceBomb active)
    {
        if (active.Mine.map?.Layers.Count is not > 0)
        {
            return false;
        }
        var layer = active.Mine.map.Layers[0];
        List<Point>? best = null;
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (Math.Abs(x - active.Target.X) <= active.Radius && Math.Abs(y - active.Target.Y) <= active.Radius)
                {
                    continue;
                }
                var path = TryBuildTilePath(active.Mine, Game1.player.TilePoint, new Point(x, y), 64, out _,
                    avoidSoftObstacles: true, allowRemovableObstacles: false);
                if (path is not null && (best is null || path.Count < best.Count))
                {
                    best = path;
                }
            }
        }
        if (best is null)
        {
            return false;
        }
        active.EscapePath = best;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private bool TickBombPathMovement(ActivePlaceBomb active, List<Point> path, out string reason)
    {
        reason = string.Empty;
        while (active.PathIndex < path.Count && Game1.player.TilePoint == path[active.PathIndex])
        {
            active.PathIndex++;
        }
        if (active.PathIndex >= path.Count)
        {
            StopAllMovement();
            return true;
        }
        var next = path[active.PathIndex];
        if (!IsTileWalkable(active.Mine, next) || IsTileOccupiedByCharacter(active.Mine, next))
        {
            reason = "bomb_path_drifted";
            StopAllMovement();
            return false;
        }
        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
        if (Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) < 0.01f)
        {
            active.StuckTicks++;
        }
        else
        {
            active.StuckTicks = 0;
        }
        active.LastPosition = Game1.player.Position;
        if (active.StuckTicks > 60)
        {
            reason = "bomb_path_stuck";
        }
        return false;
    }

    private static int BombAffectedObjectCount(MineShaft mine, Point center, int radius)
    {
        return BombAffectedTiles(center, radius).Count(tile =>
            mine.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj) &&
            (obj.IsBreakableStone() || obj is BreakableContainer));
    }

    private static IEnumerable<Point> BombAffectedTiles(Point center, int radius)
    {
        var outline = Game1.getCircleOutlineGrid(radius);
        var fill = 0;
        for (var x = 0; x < radius * 2 + 1; x++)
        {
            for (var y = 0; y < radius * 2 + 1; y++)
            {
                var include = false;
                if (x == 0 || y == 0 || x == radius * 2 || y == radius * 2)
                {
                    fill = outline[x, y] ? 1 : 0;
                }
                else if (outline[x, y])
                {
                    fill += y <= radius ? 1 : -1;
                    include = fill <= 0;
                }
                if (fill >= 1)
                {
                    include = true;
                }
                if (include)
                {
                    yield return new Point(center.X + x - radius, center.Y + y - radius);
                }
            }
        }
    }

    private static int BombStackAt(int slotIndex, string qualifiedItemId)
    {
        return slotIndex >= 0 && slotIndex < Game1.player.Items.Count &&
            Game1.player.Items[slotIndex] is StardewValley.Object obj &&
            string.Equals(obj.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)
                ? obj.Stack
                : 0;
    }

    private void CompletePlaceBomb(ActivePlaceBomb active)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        StopAllMovement();
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activePlaceBomb = null;
        var objectCountAfter = BombAffectedObjectCount(active.Mine, active.Target, active.Radius);
        var stackAfter = BombStackAt(active.BombSlotIndex, active.BombQualifiedItemId);
        var targetFinalized = active.TargetMonster is not null &&
            (active.TargetMonster.Health <= 0 || !active.Mine.characters.Contains(active.TargetMonster));
        var requiresTargetFinalization = active.TerminalState == "mummy_finalized";
        var verified = requiresTargetFinalization
            ? targetFinalized
            : objectCountAfter < active.ObjectCountBefore;
        var result = new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_bomb",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? targetFinalized
                    ? new[] { "native_bomb_consumption_observed", "escape_tile_outside_damage_square", "natural_explosion_finalized_target_monster" }
                    : new[] { "native_bomb_consumption_observed", "escape_tile_outside_damage_square", "natural_explosion_removed_breakable_objects" }
                : new[] { requiresTargetFinalization ? "bomb_target_mummy_not_finalized" : "bomb_explosion_did_not_reduce_predicted_breakable_cluster" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "bomb_stack=" + stackAfter + ";breakable_objects=" + objectCountAfter +
                ";target_finalized=" + targetFinalized.ToString().ToLowerInvariant() +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
            CombatMethod = "bomb",
            CombatTerminalState = active.TerminalState,
            CombatConsumableQualifiedItemId = active.BombQualifiedItemId,
            CombatConsumableCountBefore = active.BombStackBefore,
            CombatConsumableCountAfter = stackAfter,
            BombRadiusTiles = active.Radius,
            BombEscapeTileX = active.Escape.X,
            BombEscapeTileY = active.Escape.Y,
            BombObjectCountBefore = active.ObjectCountBefore,
            BombObjectCountAfter = objectCountAfter,
            CombatTargetRuntimeType = active.TargetMonster?.GetType().FullName ?? string.Empty,
            CombatTargetRuntimeIdentity = active.TargetMonster is null ? string.Empty : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.TargetMonster).ToString("X8"),
            CombatTargetName = active.TargetMonster?.Name ?? string.Empty,
            CombatTargetDefeated = active.TargetMonster is null ? null : targetFinalized,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { requiresTargetFinalization ? "bomb_target_mummy_not_finalized" : "bomb_effect_verification_failed" },
            ChangedFacts = new[]
                {
                    new SimulatedFactChange { Path = "player.inventory.bomb.stack", Before = active.BombStackBefore.ToString(), After = stackAfter.ToString() },
                    new SimulatedFactChange { Path = "mining.blast.breakable_object_count", Before = active.ObjectCountBefore.ToString(), After = objectCountAfter.ToString() }
                }
                .Concat(active.TargetMonster is null
                    ? Array.Empty<SimulatedFactChange>()
                    : new[]
                    {
                        new SimulatedFactChange
                        {
                            Path = "mining.monsters[" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.TargetMonster).ToString("X8") + "].present",
                            Before = "true",
                            After = (!targetFinalized).ToString().ToLowerInvariant()
                        }
                    })
                .ToArray()
        };
        active.Pending.Completion.SetResult(result);
    }

    private void CompletePlaceBombBlocked(ActivePlaceBomb active, string reason)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        StopAllMovement();
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activePlaceBomb = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "place_bomb", active.RequestedEffect,
            "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y, reason));
    }

    private void StartCombatMonster(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var terminalState = string.IsNullOrWhiteSpace(request.CombatTerminalState) ? "defeat" : request.CombatTerminalState;
        var requested = "target_monster.terminal_state=" + terminalState + ";native_input=Farmer.FireTool";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) || string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "target=missing_or_incomplete", "combat_target_identity_required"));
            return;
        }

        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "location=not_loaded_mineshaft", "combat_requires_loaded_mineshaft"));
            return;
        }

        var targets = mine.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.Name, request.TargetName, StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "matching_target_count=" + targets.Length, targets.Length == 0 ? "combat_target_not_found_or_moved" : "combat_target_ambiguous"));
            return;
        }

        var target = targets[0];
        if (terminalState == "knockdown_requires_bomb_finish" && target is not Mummy)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "target=not_mummy", "combat_terminal_state_target_mismatch"));
            return;
        }
        var weapon = ResolveCombatWeapon(target, request.CombatWeaponSlotIndex, request.RequiredWeaponEnchantmentRuntimeType);
        if (weapon is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "weapon=missing", "combat_melee_weapon_unavailable"));
            return;
        }

        activeCombatMonster = new ActiveCombatMonster(
            pending,
            mine.NameOrUniqueName,
            target,
            weapon,
            Math.Clamp(request.MaxAttacks, 1, 256),
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
            string.Equals(Environment.GetEnvironmentVariable("STARDEWAI_COMBAT_MANUAL_MOVEMENT"), "1", StringComparison.Ordinal),
            terminalState,
            requested);
        Monitor.Log($"Combat lock: {target.Name} [{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target):X8}], health={target.Health}, manual_movement={activeCombatMonster.ManualMovement}.", LogLevel.Info);
    }

    private void TickCombatMonster()
    {
        if (activeCombatMonster is null)
        {
            return;
        }

        var active = activeCombatMonster;
        try
        {
            TickCombatMonsterCore(active);
        }
        catch (Exception ex)
        {
            CompleteCombatMonsterBlocked(active, "combat_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickManualAutoCombat()
    {
        var executorCombatInterrupt = activeMineStone?.CombatInterrupted == true ||
            activeBreakContainer?.CombatInterrupted == true ||
            activePickupDebris?.CombatInterrupted == true ||
            activeDescendLadder?.CombatInterrupted == true ||
            activeDescendShaft?.CombatInterrupted == true ||
            activeExitMine?.CombatInterrupted == true;
        var enabled = manualAutoCombatEnabled || executorCombatInterrupt;
        if (!enabled || activeCombatMonster is not null || activeShootMonster is not null || activePlaceBomb is not null ||
            !Context.IsWorldReady || Game1.currentLocation is not MineShaft mine)
        {
            ReleaseManualAutoCombatInput();
            RestoreManualAutoCombatTool();
            manualAutoCombatTarget = null;
            return;
        }

        if (manualAutoCombatInputHeld)
        {
            ReleaseManualAutoCombatInput();
            return;
        }

        var target = mine.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .OrderBy(monster => Vector2.DistanceSquared(
                Game1.player.GetBoundingBox().Center.ToVector2(),
                monster.GetBoundingBox().Center.ToVector2()))
            .FirstOrDefault();
        if (target is null)
        {
            manualAutoCombatTarget = null;
            return;
        }

        if (!ReferenceEquals(target, manualAutoCombatTarget))
        {
            manualAutoCombatTarget = target;
            manualAutoCombatTargetHealth = target.Health;
            Monitor.Log($"Manual auto-combat target: {target.Name} [{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target):X8}], health={target.Health}.", LogLevel.Info);
        }
        else if (target.Health < manualAutoCombatTargetHealth)
        {
            manualAutoCombatHitCount++;
            Monitor.Log($"Manual auto-combat hit {manualAutoCombatHitCount}: {target.Name} health {manualAutoCombatTargetHealth}->{target.Health}.", LogLevel.Info);
            manualAutoCombatTargetHealth = target.Health;
        }

        var weapon = BestCombatWeapon(target);
        if (weapon is null)
        {
            RestoreManualAutoCombatTool();
            return;
        }
        if (!IsMonsterWithinCombatReach(target, weapon))
        {
            RestoreManualAutoCombatTool();
            if (executorCombatInterrupt && !manualAutoCombatEnabled)
            {
                MoveTowardCombatTarget(mine, target);
            }
            return;
        }
        if (target.isInvincible() || Game1.player.UsingTool)
        {
            return;
        }

        var targetCenter = target.GetBoundingBox().Center;
        manualAutoCombatRestoreSlotIndex ??= Game1.player.CurrentToolIndex;
        SelectTool(weapon);
        Game1.player.faceDirection(DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection));
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var reason))
        {
            Monitor.Log($"Manual auto-combat input failed: {reason}.", LogLevel.Error);
            manualAutoCombatEnabled = false;
            return;
        }

        manualAutoCombatInputHeld = true;
        manualAutoCombatAttackCount++;
        Monitor.Log($"Manual auto-combat attack {manualAutoCombatAttackCount}: {target.Name} health={target.Health}.", LogLevel.Info);
    }

    private void MoveTowardCombatTarget(MineShaft mine, Monster target)
    {
        var path = BuildAdjacentToolPath(mine, target.TilePoint, 512, out _);
        if (path is null)
        {
            return;
        }

        var nextIndex = path.FindIndex(tile => tile != Game1.player.TilePoint);
        if (nextIndex < 0)
        {
            return;
        }

        var next = path[nextIndex];
        if (!IsTileWalkable(mine, next) || IsTileOccupiedByCharacter(mine, next))
        {
            return;
        }

        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
    }

    private void ReleaseManualAutoCombatInput()
    {
        if (!manualAutoCombatInputHeld)
        {
            return;
        }

        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        manualAutoCombatInputHeld = false;
    }

    private void RestoreManualAutoCombatTool()
    {
        if (!manualAutoCombatRestoreSlotIndex.HasValue || Game1.player.UsingTool)
        {
            return;
        }

        Game1.player.CurrentToolIndex = manualAutoCombatRestoreSlotIndex.Value;
        manualAutoCombatRestoreSlotIndex = null;
    }

    private void TickCombatMonsterCore(ActiveCombatMonster active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteCombatMonsterBlocked(active, "combat_location_changed_or_world_unavailable");
            return;
        }

        RecordCombatHealth(active);
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteCombatMonsterBlocked(active, "combat_timeout");
            return;
        }

        if (Game1.player.health <= 0)
        {
            CompleteCombatMonsterBlocked(active, "combat_player_defeated");
            return;
        }

        if (active.ManualMovement && active.Target.Health > 0)
        {
            var nearestTarget = mine.characters.OfType<Monster>()
                .Where(monster => monster.Health > 0)
                .OrderBy(monster => Vector2.DistanceSquared(
                    Game1.player.GetBoundingBox().Center.ToVector2(),
                    monster.GetBoundingBox().Center.ToVector2()))
                .FirstOrDefault();
            if (nearestTarget is not null && !ReferenceEquals(nearestTarget, active.Target))
            {
                active.Retarget(nearestTarget);
                Monitor.Log($"Combat retarget: {nearestTarget.Name} [{active.TargetRuntimeIdentity}], health={nearestTarget.Health}.", LogLevel.Info);
            }
        }

        var targetPresent = mine.characters.Contains(active.Target);
        if (active.TerminalState == "knockdown_requires_bomb_finish" &&
            active.Target is Mummy mummy &&
            mummy.reviveTimer.Value > 0)
        {
            CompleteCombatMonster(active, targetDefeated: false, terminalVerificationReason: "native_melee_knocked_down_mummy_for_bomb_finish");
            return;
        }
        if (active.Target.Health <= 0 || !targetPresent)
        {
            if (active.Target.Health <= 0)
            {
                CompleteCombatMonster(active);
            }
            else
            {
                CompleteCombatMonsterBlocked(active, "combat_target_disappeared_without_defeat");
            }
            return;
        }

        if (TrackCombatProgress(active) > 600)
        {
            var detail = string.IsNullOrWhiteSpace(active.LastNoProgressReason) ? "unknown" : active.LastNoProgressReason;
            CompleteCombatMonsterBlocked(active, "combat_no_movement_or_damage_progress:" + detail);
            return;
        }

        var releasedAttackThisTick = false;
        if (active.AttackButtonHeld)
        {
            if (!TryApplySmapiButtonOverride(SButton.C, pressed: false, out var releaseReason))
            {
                CompleteCombatMonsterBlocked(active, releaseReason);
                return;
            }
            active.AttackButtonHeld = false;
            releasedAttackThisTick = true;
        }

        if (TickCombatClearance(active, mine))
        {
            return;
        }

        var targetTile = active.Target.TilePoint;
        if (!IsMonsterWithinCombatReach(active.Target, active.Weapon))
        {
            if (active.ManualMovement)
            {
                return;
            }

            if (AreAdjacent(Game1.player.TilePoint, targetTile))
            {
                ObserveCombatMovement(active);
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteCombatMonsterBlocked(active, "combat_movement_budget_exceeded");
                    return;
                }
                var adjacentTargetCenter = active.Target.GetBoundingBox().Center;
                StartMoving(DirectionToPixel(Game1.player.GetBoundingBox().Center, adjacentTargetCenter, Game1.player.FacingDirection));
                MovePlayerForTick();
                return;
            }

            if (active.PathIndex >= active.Path.Count || ManhattanDistance(active.PathTarget, targetTile) > 4)
            {
                var path = BuildAdjacentToolPath(mine, targetTile, Math.Max(1, active.MaxMovementTiles - active.MovementTiles), out var pathReason, avoidSoftObstacles: true);
                if (path is null)
                {
                    active.PathFailures++;
                    if (active.PathFailures > 120)
                    {
                        CompleteCombatMonsterBlocked(active, "combat_dynamic_path_unavailable:" + pathReason);
                    }
                    return;
                }

                active.Path = path;
                active.PathIndex = 0;
                active.PathTarget = targetTile;
                active.PathFailures = 0;
            }

            if (active.PathIndex >= active.Path.Count)
            {
                return;
            }

            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }

            if (IsTileOccupiedByCharacter(mine, next))
            {
                active.LastNoProgressReason = "combat_next_tile_soft_occupied";
                active.Path.Clear();
                active.PathIndex = 0;
                return;
            }
            if (!IsTileWalkable(mine, next))
            {
                if (BeginCombatClearance(active, mine, next))
                {
                    return;
                }

                active.LastNoProgressReason = "combat_next_tile_hard_blocked";
                active.Path.Clear();
                active.PathIndex = 0;
                return;
            }

            ObserveCombatMovement(active);
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteCombatMonsterBlocked(active, "combat_movement_budget_exceeded");
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (active.StuckTicks > 45)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.StuckTicks = 0;
            }
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        var attackDirection = DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection);
        if (active.Target.isInvincible())
        {
            return;
        }
        if (Game1.player.UsingTool)
        {
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteCombatMonsterBlocked(active, "combat_attack_budget_exceeded");
            return;
        }
        if (releasedAttackThisTick || Game1.activeClickableMenu is not null || Game1.eventUp)
        {
            return;
        }

        SelectTool(active.Weapon);
        Game1.player.faceDirection(attackDirection);
        Game1.player.lastClick = new Vector2(targetCenter.X, targetCenter.Y);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteCombatMonsterBlocked(active, inputReason);
            return;
        }
        active.AttackButtonHeld = true;
        active.AttackCount++;
    }

    private bool BeginCombatClearance(ActiveCombatMonster active, MineShaft mine, Point tile)
    {
        var tool = SelectClearanceTool(mine, tile);
        if (tool is null || !AreAdjacent(Game1.player.TilePoint, tile))
        {
            return false;
        }

        StopAllMovement();
        active.ClearanceTarget = tile;
        active.ClearanceTool = tool;
        active.ClearanceBefore = ObstacleLabel(mine, tile);
        active.ClearanceSwings = 0;
        active.LastNoProgressReason = "combat_clearing_route_obstacle";
        active.Path.Clear();
        active.PathIndex = 0;
        Monitor.Log($"Combat clearance: {active.ClearanceBefore} at {tile.X},{tile.Y} with {tool.GetType().Name}.", LogLevel.Info);
        return true;
    }

    private bool TickCombatClearance(ActiveCombatMonster active, MineShaft mine)
    {
        if (!active.ClearanceTarget.HasValue)
        {
            return false;
        }

        var target = active.ClearanceTarget.Value;
        if (active.ClearanceButtonHeld)
        {
            TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
            active.ClearanceButtonHeld = false;
            return true;
        }

        if (string.Equals(ObstacleLabel(mine, target), "clear", StringComparison.Ordinal))
        {
            active.Pending.MovementClearanceActions++;
            active.Pending.ChangedFacts.Add(new SimulatedFactChange
            {
                Path = "combat.route_clearance[" + target.X + "," + target.Y + "]",
                Before = active.ClearanceBefore,
                After = ObstacleLabel(mine, target)
            });
            active.ClearanceTarget = null;
            active.ClearanceTool = null;
            active.ClearanceBefore = string.Empty;
            active.ClearanceSwings = 0;
            active.LastNoProgressReason = string.Empty;
            active.NoProgressTicks = 0;
            return true;
        }

        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_target_no_longer_adjacent");
            return true;
        }

        var tool = SelectClearanceTool(mine, target);
        if (tool is null)
        {
            CompleteCombatMonsterBlocked(active, "combat_route_obstacle_not_clearable");
            return true;
        }
        if (Game1.player.UsingTool)
        {
            return true;
        }
        if (active.ClearanceSwings >= 64)
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_swing_budget_exceeded");
            return true;
        }

        active.ClearanceTool = tool;
        SelectTool(tool);
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        Game1.player.lastClick = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_" + inputReason);
            return true;
        }

        active.ClearanceButtonHeld = true;
        active.ClearanceSwings++;
        active.NoProgressTicks = 0;
        return true;
    }

    private static bool IsMonsterWithinCombatReach(Monster target, MeleeWeapon weapon)
    {
        if (AreAdjacent(Game1.player.TilePoint, target.TilePoint))
        {
            return true;
        }

        var playerCenter = Game1.player.GetBoundingBox().Center;
        var targetBox = target.GetBoundingBox();
        var targetCenter = targetBox.Center;
        var reach = weapon.type.Value == MeleeWeapon.dagger ? 64 : 96 + Math.Max(0, weapon.addedAreaOfEffect.Value);
        var deltaX = targetCenter.X - playerCenter.X;
        var deltaY = targetCenter.Y - playerCenter.Y;
        return targetBox.Intersects(Game1.player.GetBoundingBox()) ||
            deltaX * deltaX + deltaY * deltaY <= reach * reach;
    }

    private static int TrackCombatProgress(ActiveCombatMonster active)
    {
        var playerPosition = Game1.player.Position;
        if (Vector2.DistanceSquared(active.LastProgressPosition, playerPosition) >= 0.01f ||
            active.Target.Health < active.LastProgressTargetHealth)
        {
            active.LastProgressPosition = playerPosition;
            active.LastProgressTargetHealth = active.Target.Health;
            active.NoProgressTicks = 0;
        }
        else
        {
            active.NoProgressTicks++;
        }

        return active.NoProgressTicks;
    }

    private static void ObserveCombatMovement(ActiveCombatMonster active)
    {
        var currentPosition = Game1.player.Position;
        if (Vector2.DistanceSquared(active.LastMovementPosition, currentPosition) < 0.01f)
        {
            active.StuckTicks++;
        }
        else
        {
            active.StuckTicks = 0;
        }

        var currentTile = Game1.player.TilePoint;
        if (currentTile != active.LastMovementTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastMovementTile, currentTile);
        }

        active.LastMovementPosition = currentPosition;
        active.LastMovementTile = currentTile;
    }

    private static int DirectionToPixel(Point from, Point to, int fallback)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        if (deltaX == 0 && deltaY == 0)
        {
            return fallback;
        }
        if (Math.Abs(deltaY) >= Math.Abs(deltaX))
        {
            return deltaY < 0 ? 0 : 2;
        }
        return deltaX > 0 ? 1 : 3;
    }

    private void RecordCombatHealth(ActiveCombatMonster active)
    {
        if (active.TargetHealthSequence.Count == 0 || active.TargetHealthSequence[^1] != active.Target.Health)
        {
            var previousHealth = active.TargetHealthSequence.Count > 0 ? active.TargetHealthSequence[^1] : active.Target.Health;
            if (active.TargetHealthSequence.Count > 0 && active.Target.Health < active.TargetHealthSequence[^1])
            {
                active.HitCount++;
                Monitor.Log($"Combat hit {active.HitCount}: {active.TargetName} health {previousHealth}->{active.Target.Health}; attacks={active.AttackCount}.", LogLevel.Info);
            }
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        if (active.PlayerHealthSequence.Count == 0 || active.PlayerHealthSequence[^1] != Game1.player.health)
        {
            Monitor.Log($"Combat player health: {active.PlayerHealthSequence[^1]}->{Game1.player.health}.", LogLevel.Info);
            active.PlayerHealthSequence.Add(Game1.player.health);
        }
    }

    private static MeleeWeapon? BestCombatWeapon(Monster target, string requiredEnchantmentRuntimeType = "")
    {
        return Game1.player.Items.OfType<MeleeWeapon>()
            .Where(weapon => !weapon.isScythe())
            .Where(weapon => string.IsNullOrWhiteSpace(requiredEnchantmentRuntimeType) ||
                weapon.enchantments.Any(enchantment => string.Equals(enchantment.GetType().Name, requiredEnchantmentRuntimeType, StringComparison.Ordinal)))
            .OrderByDescending(weapon => CombatWeaponScore(weapon, target))
            .ThenByDescending(weapon => weapon.maxDamage.Value)
            .ThenBy(weapon => weapon.QualifiedItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static MeleeWeapon? ResolveCombatWeapon(Monster target, int? requestedSlotIndex, string requiredEnchantmentRuntimeType)
    {
        if (!requestedSlotIndex.HasValue)
        {
            return BestCombatWeapon(target, requiredEnchantmentRuntimeType);
        }
        var slot = requestedSlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count || Game1.player.Items[slot] is not MeleeWeapon weapon || weapon.isScythe())
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(requiredEnchantmentRuntimeType) ||
            weapon.enchantments.Any(enchantment => string.Equals(enchantment.GetType().Name, requiredEnchantmentRuntimeType, StringComparison.Ordinal))
                ? weapon
                : null;
    }

    private static double CombatWeaponScore(MeleeWeapon weapon, Monster target)
    {
        var attackMultiplier = 1d + Game1.player.buffs.AttackMultiplier;
        var averageDamage = ((weapon.minDamage.Value + weapon.maxDamage.Value) / 2d) * attackMultiplier;
        var postResilience = Math.Max(1d, averageDamage - target.resilience.Value);
        var precision = weapon.addedPrecision.Value * (1d + Game1.player.buffs.WeaponPrecisionMultiplier);
        var hitChance = 1d - Math.Max(0d, target.missChance.Value - target.missChance.Value * precision);
        double criticalChance = weapon.critChance.Value;
        if (weapon.type.Value == MeleeWeapon.dagger)
        {
            criticalChance = (criticalChance + 0.005f) * 1.12f;
        }
        criticalChance = Math.Clamp(criticalChance * (1d + Game1.player.buffs.CriticalChanceMultiplier), 0d, 1d);
        var criticalMultiplier = weapon.critMultiplier.Value * (1d + Game1.player.buffs.CriticalPowerMultiplier);
        var expectedDamage = postResilience * hitChance * (1d + criticalChance * Math.Max(0d, criticalMultiplier - 1d));
        var swipeSpeed = Math.Max(40d, (400d - weapon.speed.Value * 40d) * (1d - Game1.player.buffs.WeaponSpeedMultiplier));
        var animationFactor = weapon.type.Value == MeleeWeapon.dagger ? 0.5d : weapon.type.Value == MeleeWeapon.club ? 1.6d : 0.75d;
        return expectedDamage / Math.Max(40d, swipeSpeed * animationFactor);
    }

    private void CompleteCombatMonster(ActiveCombatMonster active, bool targetDefeated = true, string terminalVerificationReason = "native_fire_tool_defeated_target")
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        activeCombatMonster = null;
        RecordCombatHealth(active);
        var request = active.Pending.Request;
        var damageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        var inventoryAfter = InventoryStackSignature();
        var changedFacts = active.Pending.ChangedFacts.Concat(new[]
        {
            new SimulatedFactChange { Path = "mining.monsters[target].health", Before = active.TargetHealthBefore.ToString(), After = active.Target.Health.ToString() },
            new SimulatedFactChange { Path = "player.health", Before = active.PlayerHealthBefore.ToString(), After = Game1.player.health.ToString() }
        }).ToList();
        if (!string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal))
        {
            changedFacts.Add(new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter });
        }
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.LocationId,
            TargetTileX = request.TargetTileX,
            TargetTileY = request.TargetTileY,
            ToolQualifiedItemId = active.Weapon.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "combat_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = (damageTaken == 0
                ? new[] { terminalVerificationReason, "player_health_unchanged" }
                : new[] { terminalVerificationReason, "player_damage_observed=" + damageTaken })
                .Concat(string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal)
                    ? Array.Empty<string>()
                    : new[] { "natural_incidental_pickup_observed" })
                .ToArray(),
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = CombatObservedEffect(active),
            CombatTargetRuntimeType = active.TargetRuntimeType,
            CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity,
            CombatTargetName = active.TargetName,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray(),
            CombatDamageTaken = damageTaken,
            CombatTargetDefeated = targetDefeated,
            CombatMethod = "melee",
            CombatTerminalState = active.TerminalState,
            ChangedFacts = changedFacts.ToArray()
        });
    }

    private void CompleteCombatMonsterBlocked(ActiveCombatMonster active, string reason)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        if (ReferenceEquals(Game1.player.CurrentTool, active.Weapon))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        activeCombatMonster = null;
        RecordCombatHealth(active);
        var result = BlockedWithPrimitive(active.Pending.Request, "combat_monster", active.RequestedEffect, CombatObservedEffect(active), reason);
        result.ToolQualifiedItemId = active.Weapon.QualifiedItemId;
        result.ActualTicks = active.ElapsedTicks;
        result.TrainingImpactScope = "executor_calibration";
        result.CombatTargetRuntimeType = active.TargetRuntimeType;
        result.CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity;
        result.CombatTargetName = active.TargetName;
        result.CombatAttackCount = active.AttackCount;
        result.CombatHitCount = active.HitCount;
        result.CombatTargetHealthSequence = active.TargetHealthSequence.ToArray();
        result.CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray();
        result.CombatDamageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        result.CombatTargetDefeated = active.Target.Health <= 0;
        result.CombatMethod = "melee";
        result.CombatTerminalState = active.TerminalState;
        var inventoryAfter = InventoryStackSignature();
        result.ChangedFacts = active.Pending.ChangedFacts
            .Concat(string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal)
                ? Array.Empty<SimulatedFactChange>()
                : new[] { new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter } })
            .ToArray();
        active.Pending.Completion.SetResult(result);
    }

    private static string CombatObservedEffect(ActiveCombatMonster active)
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";target_type=" + active.TargetRuntimeType +
            ";target_name=" + active.TargetName +
            ";target_health=" + active.Target.Health +
            ";player_health=" + Game1.player.health +
            ";attacks=" + active.AttackCount +
            ";hits=" + active.HitCount;
    }

    private void StartSetupMiningFloor(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue || request.MineLevel.Value < 1 || request.MineLevel.Value > 120)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_mining_floor", "current_location.mine_level=requested", "mine_level=" + request.MineLevel, "mining_fixture_level_out_of_range"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        activeMineSetup = new ActiveMineSetup(pending, request.MineLevel.Value, beforeLocation);
        Game1.enterMine(request.MineLevel.Value);
    }

    private static MineFishingFixtureFacts EnsureMineFishingFixtureEquipment()
    {
        var player = Game1.player;
        var before = ReadMineFishingFixtureSnapshot(player);
        if (player.MaxItems < 36)
        {
            player.increaseBackpackSize(36 - player.MaxItems);
        }
        while (player.Items.Count < player.MaxItems)
        {
            player.Items.Add(null);
        }

        var rod = player.Items.OfType<FishingRod>().FirstOrDefault(item => item.UpgradeLevel == 4 && item.AttachmentSlotsCount >= 3);
        if (rod is null)
        {
            rod = new FishingRod(4);
            var slot = FirstEmptyInventorySlot(player);
            if (slot < 0)
            {
                for (var index = 0; index < player.Items.Count; index++)
                {
                    if (player.Items[index] is FishingRod)
                    {
                        slot = index;
                        break;
                    }
                }
            }
            if (slot >= 0)
            {
                player.Items[slot] = rod;
            }
        }

        if (rod is null)
        {
            return new MineFishingFixtureFacts(before, ReadMineFishingFixtureSnapshot(player));
        }

        rod.AttachmentSlotsCount = Math.Max(rod.AttachmentSlotsCount, 3);
        while (rod.attach(null) is not null)
        {
        }

        var bait = ItemRegistry.GetObjectTypeDefinition().CreateFlavoredBait(ItemRegistry.Create<StardewValley.Object>("(O)162"));
        bait.Stack = 999;
        rod.attach(bait);
        rod.attach(ItemRegistry.Create<StardewValley.Object>("(O)856"));
        rod.attach(ItemRegistry.Create<StardewValley.Object>("(O)695"));
        player.CurrentToolIndex = player.Items.IndexOf(rod);
        player.Stamina = Math.Max(player.Stamina, 200f);
        return new MineFishingFixtureFacts(before, ReadMineFishingFixtureSnapshot(player));
    }

    private static MineFishingFixtureSnapshot ReadMineFishingFixtureSnapshot(Farmer player)
    {
        var selectedRod = player.CurrentTool as FishingRod;
        var selectedBait = selectedRod?.GetBait();
        var lavaEelInternalName = ItemRegistry.GetData("(O)162")?.InternalName ?? string.Empty;
        var baitInternalName = selectedBait?.Name ?? string.Empty;
        var emptySlots = player.Items.Take(player.MaxItems).Count(item => item is null);
        return new MineFishingFixtureSnapshot(
            player.MaxItems,
            emptySlots,
            player.CurrentToolIndex,
            selectedRod?.QualifiedItemId ?? string.Empty,
            selectedRod?.UpgradeLevel ?? -1,
            selectedRod?.AttachmentSlotsCount ?? 0,
            selectedBait?.preservedParentSheetIndex.Value ?? string.Empty,
            baitInternalName,
            !string.IsNullOrWhiteSpace(lavaEelInternalName) && baitInternalName.Contains(lavaEelInternalName, StringComparison.Ordinal),
            selectedRod?.HasCuriosityLure() == true,
            selectedRod?.GetTackle().Any(item => item?.QualifiedItemId == "(O)695") == true,
            player.Stamina);
    }

    private static int FirstEmptyInventorySlot(Farmer player)
    {
        for (var i = 0; i < player.MaxItems && i < player.Items.Count; i++)
        {
            if (player.Items[i] is null)
            {
                return i;
            }
        }

        return -1;
    }

    private void TickMineFishingSetup()
    {
        if (activeMineFishingSetup is null)
        {
            return;
        }

        var active = activeMineFishingSetup;
        active.ElapsedTicks++;
        var mine = Game1.currentLocation as MineShaft;
        var fishableTileCount = CountFishableTiles(mine);
        var verified = mine is not null &&
            mine.mineLevel == active.MineLevel &&
            mine.getMineArea() == MineShaft.lavaArea &&
            mine.canFishHere() &&
            fishableTileCount > 0;
        if (verified)
        {
            CompleteMineFishingSetup(active, mine!, fishableTileCount, verified: true);
            return;
        }

        if (active.ElapsedTicks >= active.MaxTicks)
        {
            CompleteMineFishingSetup(active, mine, fishableTileCount, verified: false);
        }
    }

    private void TickMineSetup()
    {
        if (activeMineSetup is null)
        {
            return;
        }

        var active = activeMineSetup;
        active.ElapsedTicks++;
        var mine = Game1.currentLocation as MineShaft;
        var verified = mine is not null && mine.mineLevel == active.MineLevel && mine.map is not null;
        if (verified || active.ElapsedTicks >= active.MaxTicks)
        {
            CompleteMineSetup(active, mine, verified);
        }
    }

    private void CompleteMineSetup(ActiveMineSetup active, MineShaft? mine, bool verified)
    {
        activeMineSetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mining_floor",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_enter_mine_completed", "mine_level_verified", "loaded_mine_map_present" }
                : new[] { "mining_fixture_state_mismatch" },
            RequestedEffect = "current_location.mine_level=" + active.MineLevel,
            ObservedEffect = "location=" + afterLocation + ";mine_level=" + (mine?.mineLevel.ToString() ?? "unavailable") + ";loaded_map=" + (mine?.map is not null),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mining_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                    new SimulatedFactChange { Path = "current_location.mine_level", Before = string.Empty, After = mine!.mineLevel.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static void ClearMiningFixtureArea(MineShaft mine, Point center, int radius)
    {
        foreach (var pair in mine.objects.Pairs.Where(pair =>
            Math.Abs((int)pair.Key.X - center.X) <= radius &&
            Math.Abs((int)pair.Key.Y - center.Y) <= radius).ToArray())
        {
            mine.objects.Remove(pair.Key);
        }
        foreach (var pair in mine.terrainFeatures.Pairs.Where(pair =>
            Math.Abs((int)pair.Key.X - center.X) <= radius &&
            Math.Abs((int)pair.Key.Y - center.Y) <= radius).ToArray())
        {
            mine.terrainFeatures.Remove(pair.Key);
        }
    }

    private static int CountFishableTiles(MineShaft? mine)
    {
        if (mine?.map?.Layers.FirstOrDefault() is not { } layer)
        {
            return 0;
        }

        var count = 0;
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (mine.isTileFishable(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void CompleteMineFishingSetup(ActiveMineFishingSetup active, MineShaft? mine, int fishableTileCount, bool verified)
    {
        activeMineFishingSetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mine_fishing_floor",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_enter_mine_completed", "mine_area_80_verified", "mine_fishable_tiles_present" }
                : new[] { "mine_fishing_fixture_state_mismatch" },
            RequestedEffect = "current_location.mine_level=" + active.MineLevel + ";mine_area=80;can_fish_here=true",
            ObservedEffect = "location=" + afterLocation + ";mine_level=" + (mine?.mineLevel.ToString() ?? "unavailable") + ";mine_area=" + (mine?.getMineArea().ToString() ?? "unavailable") + ";fishable_tile_count=" + fishableTileCount,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mine_fishing_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                    new SimulatedFactChange { Path = "current_location.mine_level", Before = string.Empty, After = mine!.mineLevel.ToString() },
                    new SimulatedFactChange { Path = "current_location.mine_area", Before = string.Empty, After = mine.getMineArea().ToString() },
                    new SimulatedFactChange { Path = "current_location.fishable_tile_count", Before = string.Empty, After = fishableTileCount.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.backpack_max_items", Before = active.PrerequisiteFacts.Before.BackpackMaxItems.ToString(), After = active.PrerequisiteFacts.After.BackpackMaxItems.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.backpack_empty_slots", Before = active.PrerequisiteFacts.Before.BackpackEmptySlots.ToString(), After = active.PrerequisiteFacts.After.BackpackEmptySlots.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_qualified_item_id", Before = active.PrerequisiteFacts.Before.SelectedRodQualifiedItemId, After = active.PrerequisiteFacts.After.SelectedRodQualifiedItemId },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_slot", Before = active.PrerequisiteFacts.Before.SelectedRodSlot.ToString(), After = active.PrerequisiteFacts.After.SelectedRodSlot.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_upgrade_level", Before = active.PrerequisiteFacts.Before.SelectedRodUpgradeLevel.ToString(), After = active.PrerequisiteFacts.After.SelectedRodUpgradeLevel.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_attachment_slots", Before = active.PrerequisiteFacts.Before.SelectedRodAttachmentSlots.ToString(), After = active.PrerequisiteFacts.After.SelectedRodAttachmentSlots.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.specific_bait_target_item_id", Before = active.PrerequisiteFacts.Before.SpecificBaitTargetItemId, After = active.PrerequisiteFacts.After.SpecificBaitTargetItemId },
                    new SimulatedFactChange { Path = "fishing.fixture.bait_internal_name", Before = active.PrerequisiteFacts.Before.BaitInternalName, After = active.PrerequisiteFacts.After.BaitInternalName },
                    new SimulatedFactChange { Path = "fishing.fixture.lava_eel_native_name_condition", Before = active.PrerequisiteFacts.Before.LavaEelNativeNameCondition.ToString(), After = active.PrerequisiteFacts.After.LavaEelNativeNameCondition.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.curiosity_lure_equipped", Before = active.PrerequisiteFacts.Before.CuriosityLureEquipped.ToString(), After = active.PrerequisiteFacts.After.CuriosityLureEquipped.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.cork_bobber_equipped", Before = active.PrerequisiteFacts.Before.CorkBobberEquipped.ToString(), After = active.PrerequisiteFacts.After.CorkBobberEquipped.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.stamina", Before = active.PrerequisiteFacts.Before.Stamina.ToString("R"), After = active.PrerequisiteFacts.After.Stamina.ToString("R") }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private TrainingExecutionResult ExecuteSetupPlantSeedTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_plant_seed_target", "current_location.planting_context[target].hard_rule_allows_planting=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var seedId = PlantSeedId(request);
        var farm = Game1.getFarm();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 1;
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        farm.terrainFeatures[tile] = new HoeDirt(0, farm);
        EnsureSeedInInventory(seedId);
        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));

        var verified = farm.terrainFeatures.TryGetValue(tile, out var feature) &&
            feature is HoeDirt dirt &&
            dirt.crop is null &&
            FindSeedInventoryIndex(seedId, farm) >= 0 &&
            farm.CanPlantSeedsHere(seedId, request.TargetTileX.Value, request.TargetTileY.Value, false, out _) &&
            Game1.cropData.TryGetValue(seedId, out var cropData) &&
            cropData.Seasons.Contains(farm.GetSeason());

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_plant_seed_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_plantable_seed_tile" }
                : new[] { "fixture_plant_seed_target_not_verified" },
            RequestedEffect = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].hard_rule_allows_planting=true",
            ObservedEffect = PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_plant_seed_target_not_verified" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].hard_rule_allows_planting",
                        Before = "unknown",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecutePlantSeed(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "plant_seed", "current_location.planting_context[target].has_crop=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var seedId = PlantSeedId(request);
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_not_hoe_dirt");
        }

        if (dirt.crop is not null)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_target_already_has_crop");
        }

        if (!Game1.cropData.TryGetValue(seedId, out var cropData) || !cropData.Seasons.Contains(location.GetSeason()))
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_crop_catalog_or_season_blocked");
        }

        if (!location.CanPlantSeedsHere(seedId, request.TargetTileX.Value, request.TargetTileY.Value, false, out _))
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_location_rule_blocked");
        }

        var seedIndex = FindSeedInventoryIndex(seedId, location);
        if (seedIndex < 0)
        {
            return BlockedWithPrimitive(request, "plant_seed", PlantSeedRequestedEffect(request, seedId), PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId), "plant_seed_inventory_seed_missing");
        }

        var beforeStack = Game1.player.Items[seedIndex]?.Stack ?? 0;
        var planted = dirt.plant(seedId, Game1.player, isFertilizer: false);
        if (planted)
        {
            ConsumeOneInventoryItem(seedIndex);
        }

        var afterStack = Game1.player.Items.ElementAtOrDefault(seedIndex)?.Stack ?? 0;
        var verified = planted && dirt.crop is not null && afterStack == beforeStack - 1;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "plant_seed",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_tile_crop_created", "seed_stack_decreased" }
                : new[] { "plant_seed_post_state_mismatch" },
            RequestedEffect = PlantSeedRequestedEffect(request, seedId),
            ObservedEffect = PlantSeedObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value, seedId),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "plant_seed_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.planting_context[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].has_crop",
                        Before = "false",
                        After = "true"
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.seed_inventory[" + seedId + "].stack",
                        Before = beforeStack.ToString(),
                        After = afterStack.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupHarvestCropTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_harvest_crop_target", "farm.crops[target].ready_for_harvest=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var seedId = string.IsNullOrWhiteSpace(request.SeedId) ? "472" : PlantSeedId(request);
        var farm = Game1.getFarm();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 1;
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        var dirt = new HoeDirt(0, farm)
        {
            crop = new Crop(seedId, request.TargetTileX.Value, request.TargetTileY.Value, farm)
        };
        dirt.crop.growCompletely();
        farm.terrainFeatures[tile] = dirt;
        if (request.DebugFillInventory)
        {
            FillInventoryWithBlockingItems(dirt.crop.indexOfHarvest.Value);
        }

        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));

        var verified = farm.terrainFeatures.TryGetValue(tile, out var afterFeature) &&
            afterFeature is HoeDirt afterDirt &&
            afterDirt.crop is not null &&
            afterDirt.readyForHarvest() &&
            (!request.DebugFillInventory || !CanInventoryAcceptHarvest(afterDirt.crop));

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_harvest_crop_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { request.DebugFillInventory ? "isolated_runtime_fixture_crop_ready_for_harvest_inventory_full" : "isolated_runtime_fixture_crop_ready_for_harvest" }
                : new[] { "fixture_crop_not_ready_for_harvest" },
            RequestedEffect = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest=true",
            ObservedEffect = HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_crop_not_ready_for_harvest" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest",
                        Before = "unknown",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string PlantSeedId(TrainingExecutionRequest request)
    {
        var raw = !string.IsNullOrWhiteSpace(request.SeedId)
            ? request.SeedId
            : !string.IsNullOrWhiteSpace(request.ShopItemId)
                ? request.ShopItemId
                : request.QualifiedItemId;
        return raw.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? raw[3..] : raw;
    }

    private static int FindSeedInventoryIndex(string seedId, GameLocation location)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var item = Game1.player.Items[index];
            if (item is null)
            {
                continue;
            }

            if (string.Equals(Crop.ResolveSeedId(item.ItemId, location), seedId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ItemId, seedId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.QualifiedItemId, "(O)" + seedId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void EnsureSeedInInventory(string seedId)
    {
        if (FindSeedInventoryIndex(seedId, Game1.getFarm()) >= 0)
        {
            return;
        }

        var item = ItemRegistry.Create("(O)" + seedId, 2);
        Game1.player.addItemToInventoryBool(item);
    }

    private static void ConsumeOneInventoryItem(int index)
    {
        var item = Game1.player.Items[index];
        if (item is null)
        {
            return;
        }

        item.Stack -= 1;
        if (item.Stack <= 0)
        {
            Game1.player.Items[index] = null;
        }
    }

    private static string PlantSeedRequestedEffect(TrainingExecutionRequest request, string seedId)
    {
        return "current_location.planting_context[" + request.TargetTileX + "," + request.TargetTileY + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases";
    }

    private static string PlantSeedObservedEffect(int x, int y, string seedId)
    {
        var location = Game1.currentLocation;
        var tile = new Vector2(x, y);
        var hasCrop = location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt { crop: not null };
        var seedIndex = FindSeedInventoryIndex(seedId, location);
        var stack = seedIndex >= 0 ? Game1.player.Items[seedIndex]?.Stack ?? 0 : 0;
        return "has_crop=" + hasCrop.ToString().ToLowerInvariant() + ";seed_id=" + seedId + ";seed_stack=" + stack;
    }

    private TrainingExecutionResult ExecuteHarvestCrop(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "harvest_crop", "farm.crops[target].ready_for_harvest=false", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = HarvestCropRequestedEffect(request);
        if (!location.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_target_not_crop");
        }

        if (!dirt.readyForHarvest())
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_not_ready");
        }

        var crop = dirt.crop;
        var method = crop.GetHarvestMethod();
        if (!string.IsNullOrWhiteSpace(request.HarvestMethod) &&
            !string.Equals(method.ToString(), request.HarvestMethod, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_method_mismatch");
        }

        if (method == HarvestMethod.Grab && !CanInventoryAcceptHarvest(crop))
        {
            return BlockedWithPrimitive(request, "harvest_crop", requested, HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value), "harvest_crop_inventory_cannot_accept_grab_yield");
        }

        var beforeReady = dirt.readyForHarvest();
        var beforeHadCrop = dirt.crop is not null;
        var beforeInventory = InventoryStackSignature();
        var harvestItemId = crop.indexOfHarvest.Value;
        var beforeHarvestDebrisCount = CountDebrisForItem(location, harvestItemId);
        var expectedRegrow = crop.RegrowsAfterHarvest();
        var harvestCallApplied = crop.harvest(request.TargetTileX.Value, request.TargetTileY.Value, dirt, null, isForcedScytheHarvest: method == HarvestMethod.Scythe);
        if (!expectedRegrow && dirt.crop is not null && (harvestCallApplied || !dirt.readyForHarvest()))
        {
            dirt.destroyCrop(showAnimation: false);
        }

        var afterReady = dirt.crop is not null && dirt.readyForHarvest();
        var afterHadCrop = dirt.crop is not null;
        var afterInventory = InventoryStackSignature();
        var afterHarvestDebrisCount = CountDebrisForItem(location, harvestItemId);
        var verifiedRegrowState = beforeReady && afterHadCrop && !afterReady;
        var verifiedRemovedState = beforeReady && !afterHadCrop && !afterReady;
        var cropStateChanged = verifiedRegrowState || verifiedRemovedState;
        var inventoryChanged = !string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal);
        var harvestDebrisCreated = method != HarvestMethod.Scythe ||
            string.IsNullOrWhiteSpace(harvestItemId) ||
            afterHarvestDebrisCount > beforeHarvestDebrisCount;
        var verified = cropStateChanged &&
            (method != HarvestMethod.Grab || inventoryChanged) &&
            harvestDebrisCreated;
        var changed = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].ready_for_harvest",
                Before = beforeReady.ToString().ToLowerInvariant(),
                After = afterReady.ToString().ToLowerInvariant()
            },
            new()
            {
                Path = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].has_crop",
                Before = beforeHadCrop.ToString().ToLowerInvariant(),
                After = afterHadCrop.ToString().ToLowerInvariant()
            }
        };
        if (inventoryChanged)
        {
            changed.Add(new SimulatedFactChange
            {
                Path = "player.inventory.stack_signature",
                Before = beforeInventory,
                After = afterInventory
            });
        }

        if (method == HarvestMethod.Scythe && !string.IsNullOrWhiteSpace(harvestItemId))
        {
            changed.Add(new SimulatedFactChange
            {
                Path = "farm.debris[" + QualifyObjectId(harvestItemId) + "].count",
                Before = beforeHarvestDebrisCount.ToString(),
                After = afterHarvestDebrisCount.ToString()
            });
        }

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "harvest_crop",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? (method == HarvestMethod.Scythe
                    ? new[] { verifiedRegrowState ? "target_crop_regrow_state_updated" : "target_crop_removed_or_no_longer_ready", "target_harvest_debris_created" }
                    : new[] { verifiedRegrowState ? "target_crop_regrow_state_updated" : "target_crop_removed_or_no_longer_ready" })
                : new[] { method == HarvestMethod.Grab && !inventoryChanged ? "harvest_crop_inventory_did_not_change" : !harvestDebrisCreated ? "harvest_crop_debris_not_created" : "harvest_crop_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = HarvestCropObservedEffect(request.TargetTileX.Value, request.TargetTileY.Value),
            BlockReasons = verified ? Array.Empty<string>() : new[] { method == HarvestMethod.Grab && !inventoryChanged ? "harvest_crop_inventory_did_not_change" : !harvestDebrisCreated ? "harvest_crop_debris_not_created" : "harvest_crop_post_state_mismatch" },
            ChangedFacts = verified ? changed.ToArray() : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupGiantCropTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_giant_crop_target", "farm.resource_clumps[target].is_giant_crop=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var requestedGiantCropId = string.IsNullOrWhiteSpace(request.GiantCropId) ? "276" : request.GiantCropId;
        var giantCropId = ResolveGiantCropId(requestedGiantCropId);
        if (string.IsNullOrWhiteSpace(giantCropId) || !GiantCrop.TryGetData(giantCropId, out _))
        {
            return BlockedWithPrimitive(request, "debug_setup_giant_crop_target", "farm.resource_clumps[target].is_giant_crop=true", "requested_giant_crop_id=" + requestedGiantCropId + ";valid=false", "giant_crop_id_unknown");
        }

        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var area = new XnaRectangle(target.X * Game1.tileSize, target.Y * Game1.tileSize, 3 * Game1.tileSize, 3 * Game1.tileSize);
        for (var x = target.X; x < target.X + 3; x++)
        {
            for (var y = target.Y; y < target.Y + 3; y++)
            {
                var key = new Vector2(x, y);
                farm.objects.Remove(key);
                farm.terrainFeatures.Remove(key);
            }
        }

        foreach (var existing in farm.resourceClumps.Where(clump => clump.getBoundingBox().Intersects(area)).ToList())
        {
            farm.resourceClumps.Remove(existing);
        }

        var before = GiantCropObservedEffect(farm, target);
        farm.resourceClumps.Add(new GiantCrop(giantCropId, tile));
        MoveFixtureFarmerToFarmAdjacent(target);
        var after = GiantCropObservedEffect(farm, target);
        var verified = GiantCropAt(farm, target) is not null;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_giant_crop_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_giant_crop_present", "giant_crop_id=" + giantCropId }
                : new[] { "fixture_giant_crop_not_present", "giant_crop_id=" + giantCropId },
            RequestedEffect = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop=true",
            ObservedEffect = after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_giant_crop_not_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop",
                        Before = before,
                        After = after
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static int CountDebrisForItem(GameLocation location, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        var qualified = QualifyObjectId(itemId);
        return location.debris.Count(debris =>
            string.Equals(debris.item?.QualifiedItemId, qualified, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(debris.itemId.Value, qualified, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(debris.itemId.Value, itemId, StringComparison.OrdinalIgnoreCase));
    }

    private static string QualifyObjectId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
    }

    private static bool CanInventoryAcceptHarvest(Crop crop)
    {
        if (string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value))
        {
            return true;
        }

        return Game1.player.couldInventoryAcceptThisItem(crop.indexOfHarvest.Value, 1);
    }

    private static void FillInventoryWithBlockingItems(string harvestItemId)
    {
        var fillerIds = new[] { "390", "388", "770", "382" };
        var maxItems = Game1.player.maxItems.Value;
        for (var index = 0; index < maxItems; index++)
        {
            var fillerId = fillerIds[index % fillerIds.Length];
            if (string.Equals(fillerId, harvestItemId, StringComparison.OrdinalIgnoreCase))
            {
                fillerId = fillerIds.First(id => !string.Equals(id, harvestItemId, StringComparison.OrdinalIgnoreCase));
            }

            var item = ItemRegistry.Create("(O)" + fillerId, 999);
            Game1.player.Items[index] = item;
        }
    }

    private static string HarvestCropRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.crops[" + request.TargetTileX + "," + request.TargetTileY + "].ready_for_harvest=false";
    }

    private static string HarvestCropObservedEffect(int x, int y)
    {
        var tile = new Vector2(x, y);
        if (!Game1.currentLocation.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            return "has_crop=false;ready_for_harvest=false";
        }

        return "has_crop=true;ready_for_harvest=" + dirt.readyForHarvest().ToString().ToLowerInvariant() + ";harvest_method=" + dirt.crop.GetHarvestMethod();
    }

    private TrainingExecutionResult ExecuteHarvestGiantCrop(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", "farm.resource_clumps[target].is_giant_crop=false", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = GiantCropRequestedEffect(request);
        var before = GiantCropObservedEffect(location, target);
        var clump = GiantCropAt(location, target);
        if (clump is null)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_target_not_giant_crop");
        }

        var axe = FindTool<Axe>();
        if (axe is null)
        {
            return BlockedWithPrimitive(request, "harvest_giant_crop", requested, before, "harvest_giant_crop_axe_missing");
        }

        var beforeDebrisCount = location.debris.Count;
        var beforeHealth = clump.health.Value;
        var swings = 0;
        const int maxSwings = 64;
        axe.lastUser = Game1.player;
        while (GiantCropAt(location, target) is GiantCrop current && swings < maxSwings)
        {
            swings++;
            if (current.performToolAction(axe, 0, current.Tile))
            {
                location.resourceClumps.Remove(current);
            }
        }

        var after = GiantCropObservedEffect(location, target);
        var afterDebrisCount = location.debris.Count;
        var removed = GiantCropAt(location, target) is null;
        var debrisCreated = afterDebrisCount > beforeDebrisCount;
        var verified = removed && debrisCreated;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "harvest_giant_crop",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_giant_crop_removed", "target_giant_crop_debris_created", "tool=Axe", "swings=" + swings }
                : new[] { removed ? "target_giant_crop_removed" : "target_giant_crop_still_present", debrisCreated ? "target_giant_crop_debris_created" : "target_giant_crop_debris_not_created", "tool=Axe", "swings=" + swings },
            RequestedEffect = requested,
            ObservedEffect = after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { removed ? "harvest_giant_crop_debris_not_created" : "harvest_giant_crop_still_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].is_giant_crop",
                        Before = "true",
                        After = "false"
                    },
                    new SimulatedFactChange
                    {
                        Path = "farm.resource_clumps[" + target.X + "," + target.Y + "].health",
                        Before = beforeHealth.ToString(),
                        After = "0"
                    },
                    new SimulatedFactChange
                    {
                        Path = "farm.debris.count",
                        Before = beforeDebrisCount.ToString(),
                        After = afterDebrisCount.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static GiantCrop? GiantCropAt(GameLocation location, Point target)
    {
        var tileRect = TileRectangle(target);
        return location.resourceClumps
            .OfType<GiantCrop>()
            .FirstOrDefault(clump => clump.getBoundingBox().Intersects(tileRect));
    }

    private static string ResolveGiantCropId(string requested)
    {
        if (GiantCrop.TryGetData(requested, out _))
        {
            return requested;
        }

        var qualifiedCropId = QualifyObjectId(requested);
        var matches = GiantCrop.GetGiantCropsFor(qualifiedCropId);
        return matches.Count > 0 ? matches[0].Key : string.Empty;
    }

    private static string GiantCropRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.resource_clumps[" + request.TargetTileX + "," + request.TargetTileY + "].is_giant_crop=false";
    }

    private static string GiantCropObservedEffect(GameLocation location, Point target)
    {
        var clump = GiantCropAt(location, target);
        return clump is null
            ? "is_giant_crop=false"
            : "is_giant_crop=true;id=" + clump.Id + ";health=" + clump.health.Value + ";tile=" + (int)clump.Tile.X + "," + (int)clump.Tile.Y;
    }

    private TrainingExecutionResult ExecuteSetupDebrisTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_debris_target", "farm.debris[target].chunk_count>0", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var itemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "388" : request.ShopItemId)
            : request.QualifiedItemId;
        var origin = new Vector2(target.X * Game1.tileSize + 32, target.Y * Game1.tileSize + 32);
        var beforeCount = farm.debris.Count;
        var debris = new Debris(ItemRegistry.Create(itemId, Math.Max(1, request.Quantity ?? 1)), origin, Utility.PointToVector2(Game1.player.StandingPixel))
        {
            timeSinceDoneBouncing = -60000f,
            chunksMoveTowardPlayer = false
        };
        foreach (var chunk in debris.Chunks)
        {
            chunk.position.Value = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
            chunk.xVelocity.Value = 0f;
            chunk.yVelocity.Value = 0f;
            chunk.hasPassedRestingLineOnce.Value = true;
        }

        farm.debris.Add(debris);
        MoveFixtureFarmerToFarmAdjacent(target);
        var afterCount = farm.debris.Count;
        var verified = DebrisAt(farm, target, afterCount - 1) is not null;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_debris_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_debris_present", "qualified_item_id=" + itemId, "debris_index=" + (afterCount - 1) }
                : new[] { "fixture_debris_not_present", "qualified_item_id=" + itemId },
            RequestedEffect = "farm.debris[" + (afterCount - 1) + "].chunk_count>0",
            ObservedEffect = DebrisObservedEffect(farm, target, afterCount - 1),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_debris_not_present" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.debris.count",
                        Before = beforeCount.ToString(),
                        After = afterCount.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private void StartPickupDebris(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", "location.debris[target].chunk_count_decreases_or_removed=true", "target_tile=missing", "target_tile_required"));
            return;
        }
        if (activePickupDebris is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), "executor=busy", "pickup_debris_executor_busy"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), "player=busy_or_menu_open", "pickup_debris_tool_or_menu_conflict"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var beforeObserved = DebrisObservedEffect(location, target, request.DebrisIndex);
        var debris = DebrisAt(location, target, request.DebrisIndex);
        var chunk = debris is null ? null : DebrisChunkAt(debris, target);
        if (debris is null || chunk is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_target_not_found"));
            return;
        }

        var itemId = debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value;
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId) || !string.Equals(itemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_item_mismatch"));
            return;
        }
        if (!Game1.player.couldInventoryAcceptThisItem(debris.item ?? ItemRegistry.Create(itemId, 1, debris.itemQuality)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_inventory_cannot_accept_item"));
            return;
        }
        activePickupDebris = new ActivePickupDebris(
            pending,
            location,
            debris,
            chunk,
            target,
            itemId,
            location.debris.Count,
            debris.Chunks.Count,
            CountInventoryItem(itemId),
            InventoryStackSignature(),
            DebrisRequestedEffect(request));
    }

    private void TickPickupDebris()
    {
        if (activePickupDebris is null)
        {
            return;
        }

        var active = activePickupDebris;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_natural_collection_timeout");
            return;
        }

        var debrisStillPresent = active.Location.debris.Contains(active.Debris);
        var chunkStillPresent = debrisStillPresent && active.Debris.Chunks.Contains(active.Chunk);
        var itemCountAfter = CountInventoryItem(active.QualifiedItemId);
        if ((!debrisStillPresent || !chunkStillPresent) && itemCountAfter > active.ItemCountBefore)
        {
            CompletePickupDebris(active, itemCountAfter);
            return;
        }

        if (active.Location is MineShaft mine && ImmediateMiningThreat(mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!chunkStillPresent)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_removed_without_inventory_gain");
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_tool_or_menu_conflict_during_move");
            return;
        }

        var target = DebrisChunkTile(active.Chunk);
        if (Game1.player.TilePoint == target)
        {
            StopAllMovement();
            active.WaitAtTargetTicks++;
            if (active.WaitAtTargetTicks > 120)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.WaitAtTargetTicks = 0;
            }
            return;
        }

        if (active.PathIndex >= active.Path.Count || active.PathTarget != target)
        {
            var path = TryBuildTilePath(active.Location, Game1.player.TilePoint, target, 512, out var pathReason, avoidSoftObstacles: true);
            if (path is null)
            {
                active.PathFailures++;
                if (active.PathFailures > 90)
                {
                    CompletePickupDebrisBlocked(active, "pickup_debris_dynamic_path_unavailable:" + pathReason);
                }
                return;
            }
            active.Path = path;
            active.PathIndex = 0;
            active.PathTarget = target;
            active.PathFailures = 0;
        }

        if (active.PathIndex >= active.Path.Count)
        {
            return;
        }
        var next = active.Path[active.PathIndex];
        if (Game1.player.TilePoint == next)
        {
            active.PathIndex++;
            return;
        }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
        {
            active.Path.Clear();
            active.PathIndex = 0;
            return;
        }

        var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
        if (Game1.player.TilePoint == next)
        {
            active.PathIndex++;
        }
        if (!movedSinceLastTick)
        {
            active.StuckTicks++;
            if (active.StuckTicks > 45)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.StuckTicks = 0;
            }
        }
        else
        {
            active.StuckTicks = 0;
        }
    }

    private void CompletePickupDebris(ActivePickupDebris active, int itemCountAfter)
    {
        StopAllMovement();
        activePickupDebris = null;
        var request = active.Pending.Request;
        var inventoryAfter = InventoryStackSignature();
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "pickup_debris",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_debris", "game_update_naturally_collected_chunk", "inventory_item_count_increased", "no_direct_debris_collect_call" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "debris_present=" + active.Location.debris.Contains(active.Debris).ToString().ToLowerInvariant() + ";item_count=" + itemCountAfter + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "locations[" + active.LocationId + "].debris.count", Before = active.DebrisCountBefore.ToString(), After = active.Location.debris.Count.ToString() },
                new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter },
                new SimulatedFactChange { Path = "player.inventory.count[" + active.QualifiedItemId + "]", Before = active.ItemCountBefore.ToString(), After = itemCountAfter.ToString() }
            }
        });
    }

    private void CompletePickupDebrisBlocked(ActivePickupDebris active, string reason)
    {
        StopAllMovement();
        activePickupDebris = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "pickup_debris",
            active.RequestedEffect,
            "debris_present=" + active.Location.debris.Contains(active.Debris).ToString().ToLowerInvariant() + ";item_count=" + CountInventoryItem(active.QualifiedItemId),
            reason));
    }

    private static Point DebrisChunkTile(Chunk chunk)
    {
        return new Point(
            (int)((chunk.position.X + 32f) / Game1.tileSize),
            (int)((chunk.position.Y + 32f) / Game1.tileSize));
    }

    private static Debris? DebrisAt(GameLocation location, Point target, int? debrisIndex)
    {
        for (var index = 0; index < location.debris.Count; index++)
        {
            if (debrisIndex.HasValue && index != debrisIndex.Value)
            {
                continue;
            }

            var debris = location.debris[index];
            if (DebrisChunkAt(debris, target) is not null)
            {
                return debris;
            }
        }

        return null;
    }

    private static Chunk? DebrisChunkAt(Debris debris, Point target)
    {
        return debris.Chunks.FirstOrDefault(chunk =>
            (int)((chunk.position.X + 32f) / Game1.tileSize) == target.X &&
            (int)((chunk.position.Y + 32f) / Game1.tileSize) == target.Y);
    }

    private static string DebrisRequestedEffect(TrainingExecutionRequest request)
    {
        return "location.debris[" + (request.DebrisIndex.HasValue ? request.DebrisIndex.Value.ToString() : request.TargetTileX + "," + request.TargetTileY) + "].chunk_count_decreases_or_removed=true;player.inventory.updated;collection=native_proximity";
    }

    private static string DebrisObservedEffect(GameLocation location, Point target, int? debrisIndex)
    {
        var debris = DebrisAt(location, target, debrisIndex);
        if (debris is null)
        {
            return "debris_present=false";
        }

        var index = location.debris.IndexOf(debris);
        return "debris_present=true;debris_index=" + index + ";chunk_count=" + debris.Chunks.Count + ";qualified_item_id=" + (debris.item?.QualifiedItemId ?? debris.itemId.Value);
    }

    private static int CountInventoryItem(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return 0;
        }

        return Game1.player.Items
            .Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item!.Stack);
    }

    private TrainingExecutionResult ExecuteSetupMachineOutputTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_machine_output_target", "farm.machines[target].ready_for_harvest=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var outputItemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "388" : request.ShopItemId)
            : request.QualifiedItemId;
        var beforeMachine = MachineObservedEffect(farm, target);
        ClearReadyMachineOutputsForFixture(farm, tile);
        farm.objects.Remove(tile);

        var machine = new StardewValley.Object(tile, string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "12" : request.ExpectedShopId)
        {
            heldObject =
            {
                Value = ItemRegistry.Create<StardewValley.Object>(outputItemId, Math.Max(1, request.Quantity ?? 1))
            },
            readyForHarvest =
            {
                Value = true
            }
        };
        machine.MinutesUntilReady = 0;
        farm.objects[tile] = machine;
        MoveFixtureFarmerToFarmAdjacent(target);

        var verified = MachineAt(farm, target) is { readyForHarvest.Value: true, heldObject.Value: not null };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_machine_output_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_machine_output_ready", "qualified_item_id=" + outputItemId }
                : new[] { "fixture_machine_output_not_ready", "qualified_item_id=" + outputItemId },
            RequestedEffect = "farm.machines[" + target.X + "," + target.Y + "].ready_for_harvest=true;held_item=" + outputItemId,
            ObservedEffect = MachineObservedEffect(farm, target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_machine_output_not_ready" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + target.X + "," + target.Y + "]",
                        Before = beforeMachine,
                        After = MachineObservedEffect(farm, target)
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteCollectMachineOutput(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", "farm.machines[target].held_item=null;player.inventory.updated", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = MachineRequestedEffect(request);
        var beforeObserved = MachineObservedEffect(location, target);
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_target_not_found");
        }

        if (!machine.readyForHarvest.Value || machine.heldObject.Value is null)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_not_ready");
        }

        var output = machine.heldObject.Value;
        var outputId = output.QualifiedItemId;
        if (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
            !string.Equals(outputId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_item_mismatch");
        }

        if (!Game1.player.couldInventoryAcceptThisItem(output))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_inventory_cannot_accept_item");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) + Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_player_not_adjacent");
        }

        var beforeInventory = InventoryStackSignature();
        var beforeItemCount = CountInventoryItem(outputId);
        var acted = machine.checkForAction(Game1.player);
        var afterInventory = InventoryStackSignature();
        var afterItemCount = CountInventoryItem(outputId);
        var afterObserved = MachineObservedEffect(location, target);
        var verified = acted &&
            machine.heldObject.Value is null &&
            !machine.readyForHarvest.Value &&
            (!string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal) || afterItemCount > beforeItemCount);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "collect_machine_output",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "machine_output_collected", "inventory_updated", "qualified_item_id=" + outputId }
                : new[] { acted ? "checkForAction_returned_true" : "checkForAction_returned_false", machine.heldObject.Value is null ? "held_item_cleared" : "held_item_still_present" },
            RequestedEffect = requested,
            ObservedEffect = afterObserved,
            BlockReasons = verified ? Array.Empty<string>() : new[] { acted ? "collect_machine_output_post_state_mismatch" : "collect_machine_output_action_failed" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + target.X + "," + target.Y + "].held_item",
                        Before = beforeObserved,
                        After = afterObserved
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.stack_signature",
                        Before = beforeInventory,
                        After = afterInventory
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupMachineInputTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_machine_input_target", "farm.machines[target].loadable_inputs.length>0", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var inputItemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "262" : request.ShopItemId)
            : request.QualifiedItemId;
        var beforeMachine = MachineObservedEffect(farm, target);
        farm.objects.Remove(tile);

        var machine = new StardewValley.Object(tile, string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "12" : request.ExpectedShopId);
        machine.MinutesUntilReady = -1;
        machine.readyForHarvest.Value = false;
        machine.heldObject.Value = null;
        farm.objects[tile] = machine;
        var inputSlot = EnsureInventoryItem(inputItemId, Math.Max(1, request.Quantity ?? 1));
        MoveFixtureFarmerToFarmAdjacent(target);

        var input = inputSlot >= 0 ? Game1.player.Items[inputSlot] : null;
        var accepts = input is not null && machine.performObjectDropInAction(input, probe: true, Game1.player);
        var verified = MachineAt(farm, target) is not null && inputSlot >= 0 && accepts;
        RefreshTransparentMachineProbeCache();
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_machine_input_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_machine_accepts_input_probe", "qualified_item_id=" + inputItemId, "input_slot_index=" + inputSlot }
                : new[] { "fixture_machine_input_probe_rejected", "qualified_item_id=" + inputItemId, "input_slot_index=" + inputSlot },
            RequestedEffect = "farm.machines[" + target.X + "," + target.Y + "].loadable_inputs.length>0;qualified_item_id=" + inputItemId,
            ObservedEffect = MachineObservedEffect(farm, target) + ";input_slot_index=" + inputSlot + ";input_probe_accepts=" + accepts.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_machine_input_probe_rejected" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + target.X + "," + target.Y + "]",
                        Before = beforeMachine,
                        After = MachineObservedEffect(farm, target)
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.input_slot_index",
                        Before = "unknown",
                        After = inputSlot.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteLoadMachineInput(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "load_machine_input", "farm.machines[target].minutes_until_ready>0_or_ready=true;player.inventory.updated", "target_tile=missing", "target_tile_required");
        }

        if (!request.InputSlotIndex.HasValue)
        {
            return BlockedWithPrimitive(request, "load_machine_input", "farm.machines[target].minutes_until_ready>0_or_ready=true;player.inventory.updated", "input_slot=missing", "input_slot_index_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = MachineInputRequestedEffect(request);
        var beforeObserved = MachineObservedEffect(location, target);
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_target_not_found");
        }

        if (machine.MinutesUntilReady > 0 || machine.readyForHarvest.Value)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_target_busy");
        }

        var inputSlot = request.InputSlotIndex.Value;
        if (inputSlot < 0 || inputSlot >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_slot_out_of_range");
        }

        var input = Game1.player.Items[inputSlot];
        if (input is null)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_slot_empty");
        }

        if (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
            !string.Equals(input.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_item_mismatch");
        }

        if (!machine.performObjectDropInAction(input, probe: true, Game1.player))
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_probe_rejected");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) + Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_player_not_adjacent");
        }

        var beforeInventory = InventoryStackSignature();
        var beforeStack = input.Stack;
        var inputId = input.QualifiedItemId;
        var acted = machine.performObjectDropInAction(input, probe: false, Game1.player);
        var afterInventory = InventoryStackSignature();
        var afterObserved = MachineObservedEffect(location, target);
        var afterSlotItem = inputSlot < Game1.player.Items.Count ? Game1.player.Items[inputSlot] : null;
        var afterStack = afterSlotItem?.Stack ?? 0;
        var machineStarted = machine.MinutesUntilReady > 0 || machine.readyForHarvest.Value || machine.heldObject.Value is not null;
        var inventoryChanged = !string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal) || afterStack < beforeStack;
        var verified = acted && machineStarted && inventoryChanged;
        RefreshTransparentMachineProbeCache();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "load_machine_input",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "machine_input_loaded", "machine_processing_started_or_output_ready", "inventory_updated", "qualified_item_id=" + inputId }
                : new[] { acted ? "performObjectDropInAction_returned_true" : "performObjectDropInAction_returned_false", machineStarted ? "machine_started" : "machine_not_started", inventoryChanged ? "inventory_changed" : "inventory_not_changed" },
            RequestedEffect = requested,
            ObservedEffect = afterObserved + ";input_slot_index=" + inputSlot + ";input_stack_before=" + beforeStack + ";input_stack_after=" + afterStack,
            BlockReasons = verified ? Array.Empty<string>() : new[] { acted ? "load_machine_input_post_state_mismatch" : "load_machine_input_action_failed" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + target.X + "," + target.Y + "]",
                        Before = beforeObserved,
                        After = afterObserved
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.stack_signature",
                        Before = beforeInventory,
                        After = afterInventory
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static StardewValley.Object? MachineAt(GameLocation location, Point target)
    {
        return location.objects.TryGetValue(new Vector2(target.X, target.Y), out var obj) &&
            obj.bigCraftable.Value
            ? obj
            : null;
    }

    private void StartCatchFish(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = CatchFishRequestedEffect(request);
        var reasons = ValidateCatchFishStart(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "catch_fish", requested, CatchFishObservedEffect(), reasons.ToArray()));
            return;
        }

        if (activeCatchFish is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "catch_fish", requested, CatchFishObservedEffect(), "catch_fish_executor_busy"));
            return;
        }

        var rod = (FishingRod)Game1.player.Items[request.RodSlotIndex!.Value]!;
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var bobber = new Point(request.BobberTileX!.Value, request.BobberTileY!.Value);
        if (!TryResolveFishingCast(stand, bobber, rod, out var direction, out var castingPower, out var maxCastRequested, out var reason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "catch_fish", requested, CatchFishObservedEffect(), reason));
            return;
        }

        var beforeInventory = InventoryStackSignature();
        var beforeStamina = Game1.player.Stamina;
        var beforeCaughtCount = ExpectedFishCaughtCount(request.ExpectedQualifiedItemId);
        Game1.player.CurrentToolIndex = request.RodSlotIndex.Value;
        Game1.player.faceDirection(direction);
        catchFishUseToolHeld = true;
        if (!TryApplySmapiLeftButtonOverride(pressed: true, out var inputReason))
        {
            catchFishUseToolHeld = false;
            pending.Completion.SetResult(BlockedWithPrimitive(request, "catch_fish", requested, CatchFishObservedEffect(), inputReason));
            return;
        }

        if (!rod.beginUsing(Game1.currentLocation, bobber.X * Game1.tileSize, bobber.Y * Game1.tileSize, Game1.player))
        {
            ReleaseSmapiLeftButtonOverride();
            pending.Completion.SetResult(BlockedWithPrimitive(request, "catch_fish", requested, CatchFishObservedEffect(), "catch_fish_begin_using_rejected"));
            return;
        }

        activeCatchFish = new ActiveCatchFish(pending, stand, bobber, rod, castingPower, maxCastRequested, beforeInventory, beforeStamina, beforeCaughtCount);
    }

    private bool ApplyCatchFishUseToolInput(ActiveCatchFish active, out string reason)
    {
        active.ObservedPeakCastingPower = Math.Max(active.ObservedPeakCastingPower, active.Rod.castingPower);
        var projectedCastingPower = Math.Clamp(active.Rod.castingPower + Math.Max(0f, active.Rod.castingTimerSpeed) * 17f, 0f, 1f);
        if (catchFishUseToolHeld && active.Rod.isTimingCast && projectedCastingPower >= active.DesiredCastingPower)
        {
            catchFishUseToolHeld = false;
        }

        return TryApplySmapiLeftButtonOverride(catchFishUseToolHeld, out reason);
    }

    private bool ApplyBobberBarInput(bool pressed, out string reason)
    {
        return TryApplySmapiLeftButtonOverride(pressed, out reason);
    }

    private bool TryApplySmapiLeftButtonOverride(bool pressed, out string reason)
    {
        return TryApplySmapiButtonOverride(SButton.MouseLeft, pressed, out reason);
    }

    private bool TryApplySmapiButtonOverride(SButton button, bool pressed, out string reason)
    {
        reason = string.Empty;
        var input = Game1.input;
        if (input is null)
        {
            reason = "catch_fish_smapi_input_state_unavailable";
            return false;
        }

        var inputType = input.GetType();
        if (smapiInputStateType != inputType)
        {
            smapiInputStateType = inputType;
            smapiOverrideButtonMethod = inputType.GetMethod(
                "OverrideButton",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(SButton), typeof(bool) },
                modifiers: null);
        }

        if (smapiOverrideButtonMethod is null)
        {
            reason = "catch_fish_smapi_input_override_unavailable:" + (inputType.FullName ?? inputType.Name);
            return false;
        }

        try
        {
            smapiOverrideButtonMethod.Invoke(input, new object[] { button, pressed });
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
            reason = "catch_fish_smapi_input_override_failed:" + cause.GetType().Name;
            return false;
        }
    }

    private void ReleaseSmapiLeftButtonOverride()
    {
        catchFishUseToolHeld = false;
        TryApplySmapiLeftButtonOverride(pressed: false, out _);
    }

    private void TickCatchFish()
    {
        if (activeCatchFish is null)
        {
            return;
        }

        var active = activeCatchFish;
        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteBlockedCatchFish(active, "catch_fish_timeout");
            return;
        }

        var request = active.Pending.Request;
        active.ObservedPeakCastingPower = Math.Max(active.ObservedPeakCastingPower, active.Rod.castingPower);
        active.SawTimingCast |= active.Rod.isTimingCast;
        active.SawCasting |= active.Rod.isCasting;
        if (active.WasTimingCastLastTick && !active.Rod.isTimingCast && active.Rod.isCasting && active.ObservedReleaseCastingPower < 0f)
        {
            active.ObservedReleaseCastingPower = active.Rod.castingPower;
            active.ObservedMaxCast = active.MaxCastRequested && active.Rod.castingPower >= 0.99f;
        }

        active.WasTimingCastLastTick = active.Rod.isTimingCast;
        active.SawCastingAir |= active.Rod.castedButBobberStillInAir;
        active.SawFishing |= active.Rod.isFishing;
        active.SawPullingOutOfWater |= active.Rod.pullingOutOfWater;
        if (active.Rod.pullingOutOfWater && !active.SawBobberBar)
        {
            active.SawJunkOrSpecialPullWithoutBobberBar = true;
        }

        if (active.Rod.bobber.Value != Vector2.Zero)
        {
            active.LastBobberTile = new Point(
                (int)(active.Rod.bobber.X / Game1.tileSize),
                (int)(active.Rod.bobber.Y / Game1.tileSize));
        }
        var reasons = ValidateCatchFishContinuity(active);
        if (reasons.Count > 0)
        {
            CompleteBlockedCatchFish(active, reasons.ToArray());
            return;
        }

        if (Game1.activeClickableMenu is BobberBar)
        {
            active.SawBobberBar = true;
            if (active.SawJunkOrSpecialPullWithoutBobberBar)
            {
                CompleteBlockedCatchFish(active, "catch_fish_junk_or_special_pull_then_bobber_bar", CatchFishCastDiagnostic(active));
                return;
            }
        }
        else if (active.Rod.isNibbling)
        {
            active.SawNibble = true;
            if (!active.HookIssuedForNibble)
            {
                active.HookIssuedForNibble = true;
                active.HookAttemptCount++;
                active.Rod.DoFunction(Game1.currentLocation, active.BobberTile.X * Game1.tileSize, active.BobberTile.Y * Game1.tileSize, 1, Game1.player);
            }
        }

        if (active.Rod.isFishing || active.Rod.isNibbling)
        {
            var observedBobberTile = new Point((int)(active.Rod.bobber.X / Game1.tileSize), (int)(active.Rod.bobber.Y / Game1.tileSize));
            if (observedBobberTile != active.BobberTile)
            {
                CompleteBlockedCatchFish(active, "catch_fish_bobber_tile_mismatch_after_cast");
                return;
            }
        }

        if (Game1.activeClickableMenu is BobberBar afterBar)
        {
            if (afterBar.handledFishResult && afterBar.distanceFromCatching >= 1f)
            {
                active.SawBobberBarSuccess = true;
                active.TerminalBobberBarProgress = afterBar.distanceFromCatching;
                active.TerminalCatchResult = "normal_fish_bobber_bar_success";
            }
            else if (afterBar.handledFishResult)
            {
                active.TerminalBobberBarProgress = afterBar.distanceFromCatching;
                active.TerminalCatchResult = "bobber_bar_failure";
                CompleteBlockedCatchFish(active, "catch_fish_minigame_lost", CatchFishMinigameDiagnostic(active));
                return;
            }
        }

        if (active.Rod.fishCaught)
        {
            active.SawFishCaughtHold = true;
            active.ObservedQualifiedItemId = active.Rod.whichFish?.QualifiedItemId ?? string.Empty;
            active.Rod.doneHoldingFish(Game1.player);
        }

        if (CatchFishPostStateVerified(active, out var verificationReasons, out var blockReasons))
        {
            CompleteCatchFish(active, verificationReasons);
            return;
        }

        if (blockReasons.Length > 0)
        {
            if (blockReasons.Contains("catch_fish_observed_outcome_not_in_compiled_distribution", StringComparer.Ordinal))
            {
                CompleteObservedBlockedCatchFish(active, blockReasons);
                return;
            }

            CompleteBlockedCatchFish(active, blockReasons);
            return;
        }

        if (active.ElapsedTicks > 180 && CatchFishIsIdle(active.Rod) && Game1.activeClickableMenu is not BobberBar)
        {
            var reason = active.SawFishing
                ? "catch_fish_ended_without_verified_catch"
                : "catch_fish_cast_did_not_enter_fishing_state";
            CompleteBlockedCatchFish(active, reason, CatchFishCastDiagnostic(active));
        }
    }

    private static bool CatchFishIsIdle(FishingRod rod)
    {
        return !rod.isTimingCast &&
            !rod.isCasting &&
            !rod.castedButBobberStillInAir &&
            !rod.isFishing &&
            !rod.isNibbling &&
            !rod.isReeling &&
            !rod.pullingOutOfWater &&
            !rod.fishCaught;
    }

    private static bool CatchFishFullyIdle(FishingRod rod)
    {
        return CatchFishIsIdle(rod) &&
            Game1.activeClickableMenu is null &&
            !Game1.player.UsingTool &&
            Game1.player.canMove;
    }

    private bool SetBobberBarControl(ActiveCatchFish active, BobberBar bar, out string reason)
    {
        RecordBobberBarState(active, bar);
        var trackBottom = 568f - bar.bobberBarHeight;
        var fishSpeed = bar.bobberSpeed + bar.floaterSinkerAcceleration;
        var predictedFishCenter = Math.Clamp(bar.bobberPosition + fishSpeed, 0f, 532f);
        var targetBarPosition = Math.Clamp(predictedFishCenter + 32f - bar.bobberBarHeight / 2f, 0f, trackBottom);
        var positionError = targetBarPosition - bar.bobberBarPos;
        var acceleration = bar.bobberInBar ? 0.15f : 0.25f;
        var reachableRelativeSpeed = MathF.Sqrt(2f * acceleration * MathF.Abs(positionError));
        var desiredRelativeSpeed = MathF.Sign(positionError) * MathF.Min(5f, reachableRelativeSpeed);
        var desiredBarSpeed = fishSpeed + desiredRelativeSpeed;
        var shouldPress = bar.bobberBarSpeed > desiredBarSpeed;
        active.BobberControlTicks++;
        if (shouldPress)
        {
            active.BobberControlPressedTicks++;
        }

        return ApplyBobberBarInput(shouldPress, out reason);
    }

    private static void RecordBobberBarState(ActiveCatchFish active, BobberBar bar)
    {
        active.BobberBarTicks++;
        if (bar.bobberInBar)
        {
            active.BobberInBarTicks++;
        }

        active.MinDistanceFromCatching = Math.Min(active.MinDistanceFromCatching, bar.distanceFromCatching);
        active.LastDistanceFromCatching = bar.distanceFromCatching;
        active.LastFishPosition = bar.bobberPosition;
        active.LastFishSpeed = bar.bobberSpeed + bar.floaterSinkerAcceleration;
        active.LastBarPosition = bar.bobberBarPos;
        active.LastBarSpeed = bar.bobberBarSpeed;
        active.LastBarHeight = bar.bobberBarHeight;
    }

    private static string CatchFishMinigameDiagnostic(ActiveCatchFish active)
    {
        var inBarRatio = active.BobberBarTicks == 0
            ? 0f
            : active.BobberInBarTicks / (float)active.BobberBarTicks;
        return "catch_fish_minigame_diagnostic:" +
            "ticks=" + active.BobberBarTicks +
            ",in_bar_ratio=" + inBarRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",min_progress=" + active.MinDistanceFromCatching.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",last_progress=" + active.LastDistanceFromCatching.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",fish_position=" + active.LastFishPosition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",fish_speed=" + active.LastFishSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",bar_position=" + active.LastBarPosition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",bar_speed=" + active.LastBarSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",bar_height=" + active.LastBarHeight;
    }

    private static string CatchFishCastDiagnostic(ActiveCatchFish active)
    {
        var lastBobber = active.LastBobberTile.HasValue
            ? active.LastBobberTile.Value.X + "," + active.LastBobberTile.Value.Y
            : "none";
        return "catch_fish_cast_stages:" +
            "timing=" + active.SawTimingCast +
            ",casting=" + active.SawCasting +
            ",air=" + active.SawCastingAir +
            ",fishing=" + active.SawFishing +
            ",nibble=" + active.SawNibble +
            ",bobber_bar=" + active.SawBobberBar +
            ",pulling_out=" + active.SawPullingOutOfWater +
            ",fish_hold=" + active.SawFishCaughtHold +
            ",desired_power=" + active.DesiredCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",observed_peak_power=" + active.ObservedPeakCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",observed_release_power=" + active.ObservedReleaseCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",max_cast_requested=" + active.MaxCastRequested +
            ",max_cast_observed=" + active.ObservedMaxCast +
            ",hook_attempt_count=" + active.HookAttemptCount +
            ",junk_or_special_pull_without_bobber_bar=" + active.SawJunkOrSpecialPullWithoutBobberBar +
            ",last_bobber_tile=" + lastBobber;
    }

    private List<string> ValidateCatchFishStart(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return reasons;
        }

        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("catch_fish_active_menu_blocked");
        }

        if (Game1.player.Stamina <= 1f)
        {
            reasons.Add("catch_fish_energy_too_low");
        }

        if (string.IsNullOrWhiteSpace(request.LocationId) || !string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            reasons.Add("catch_fish_location_mismatch");
        }

        if (!request.StandTileX.HasValue || !request.StandTileY.HasValue || Game1.player.TilePoint != new Point(request.StandTileX.GetValueOrDefault(), request.StandTileY.GetValueOrDefault()))
        {
            reasons.Add("catch_fish_stand_tile_mismatch");
        }

        if (!request.BobberTileX.HasValue || !request.BobberTileY.HasValue)
        {
            reasons.Add("catch_fish_bobber_tile_required");
        }
        else if (!Game1.currentLocation.canFishHere() || !Game1.currentLocation.isTileFishable(request.BobberTileX.Value, request.BobberTileY.Value))
        {
            reasons.Add("catch_fish_bobber_tile_not_fishable");
        }

        if (!request.RodSlotIndex.HasValue || request.RodSlotIndex.Value < 0 || request.RodSlotIndex.Value >= Game1.player.Items.Count)
        {
            reasons.Add("catch_fish_rod_slot_index_invalid");
        }
        else if (Game1.player.Items[request.RodSlotIndex.Value] is not FishingRod rod)
        {
            reasons.Add("catch_fish_rod_slot_not_fishing_rod");
        }
        else if (rod.inUse())
        {
            reasons.Add("catch_fish_rod_already_in_use");
        }

        if (string.IsNullOrWhiteSpace(request.RuleKey))
        {
            reasons.Add("catch_fish_rule_key_required");
        }
        else if (!request.RuleKey.StartsWith("distribution:", StringComparison.Ordinal))
        {
            reasons.Add("catch_fish_distribution_key_required");
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedQualifiedItemId))
        {
            reasons.Add("catch_fish_expected_item_must_be_unconstrained");
        }

        if (!request.OutcomeDistributionComplete)
        {
            reasons.Add("catch_fish_outcome_distribution_incomplete");
        }

        if (!TryValidateFishingOutcomeDistribution(request.OutcomeDistributionJson, request.PossibleQualifiedItemIdsJson))
        {
            reasons.Add("catch_fish_outcome_distribution_invalid");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool TryValidateFishingOutcomeDistribution(string distributionJson, string possibleItemIdsJson)
    {
        try
        {
            using var distribution = JsonDocument.Parse(distributionJson);
            using var possibleItemIds = JsonDocument.Parse(possibleItemIdsJson);
            if (distribution.RootElement.ValueKind != JsonValueKind.Array || distribution.RootElement.GetArrayLength() == 0 ||
                possibleItemIds.RootElement.ValueKind != JsonValueKind.Array || possibleItemIds.RootElement.GetArrayLength() == 0)
            {
                return false;
            }

            var possible = possibleItemIds.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.Ordinal);
            var distributed = distribution.RootElement.EnumerateArray()
                .Select(outcome => outcome.ValueKind == JsonValueKind.Object && outcome.TryGetProperty("qualified_item_id", out var itemId) && itemId.ValueKind == JsonValueKind.String
                    ? itemId.GetString() ?? string.Empty
                    : string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.Ordinal);
            return distributed.Count > 0 && distributed.SetEquals(possible);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<string> ValidateCatchFishContinuity(ActiveCatchFish active)
    {
        var request = active.Pending.Request;
        var reasons = new List<string>();
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            reasons.Add("world_not_ready_during_catch_fish");
        }
        else if (!string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            reasons.Add("catch_fish_location_changed");
        }

        if (Game1.player.TilePoint != active.StandTile)
        {
            reasons.Add("catch_fish_stand_tile_changed");
        }

        if (!ReferenceEquals(Game1.player.CurrentTool, active.Rod))
        {
            reasons.Add("catch_fish_current_tool_changed");
        }

        if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not BobberBar && Game1.activeClickableMenu is not ItemGrabMenu)
        {
            reasons.Add("catch_fish_unexpected_menu_opened");
        }

        return reasons;
    }

    private static bool TryResolveFishingCast(Point stand, Point bobber, FishingRod rod, out int direction, out float castingPower, out bool maxCastRequested, out string reason)
    {
        direction = 2;
        castingPower = 1f;
        maxCastRequested = false;
        reason = string.Empty;
        var deltaX = bobber.X - stand.X;
        var deltaY = bobber.Y - stand.Y;
        if (deltaX != 0 && deltaY != 0)
        {
            reason = "catch_fish_bobber_not_cardinal_from_stand";
            return false;
        }

        var distance = Math.Abs(deltaX) + Math.Abs(deltaY);
        if (distance < 2)
        {
            reason = "catch_fish_bobber_too_close_for_cast";
            return false;
        }

        var addedDistance = Game1.player.FishingLevel >= 15 ? 4 : Game1.player.FishingLevel >= 8 ? 3 : Game1.player.FishingLevel >= 4 ? 2 : Game1.player.FishingLevel >= 1 ? 1 : 0;
        var maxDistance = addedDistance + (deltaX == 0 ? 3 : 4);
        if (distance > maxDistance)
        {
            reason = "catch_fish_bobber_beyond_cast_reach";
            return false;
        }

        direction = deltaY < 0 ? 0 : deltaX > 0 ? 1 : deltaY > 0 ? 2 : 3;
        castingPower = Math.Clamp(distance / (float)maxDistance, 0f, 1f);
        maxCastRequested = Math.Abs(castingPower - 1f) < 0.0001f;
        return true;
    }

    private static bool CatchFishPostStateVerified(ActiveCatchFish active, out string[] verificationReasons, out string[] blockReasons)
    {
        blockReasons = Array.Empty<string>();
        var request = active.Pending.Request;
        if (Game1.activeClickableMenu is ItemGrabMenu)
        {
            verificationReasons = Array.Empty<string>();
            blockReasons = new[] { "catch_fish_inventory_full_item_grab_menu" };
            return false;
        }

        var afterInventory = InventoryStackSignature();
        var caughtCount = ExpectedFishCaughtCount(request.ExpectedQualifiedItemId);
        var inventoryChanged = !string.Equals(active.BeforeInventory, afterInventory, StringComparison.Ordinal);
        var collectionChanged = caughtCount > active.BeforeExpectedCaughtCount;
        active.IdleCleanupComplete = CatchFishFullyIdle(active.Rod);
        if (active.SawFishCaughtHold && (inventoryChanged || collectionChanged) && !active.Rod.isFishing && !active.Rod.isReeling && !active.Rod.fishCaught)
        {
            if (!active.IdleCleanupComplete)
            {
                verificationReasons = Array.Empty<string>();
                return false;
            }

            if (string.IsNullOrWhiteSpace(active.ObservedQualifiedItemId) || !FishingOutcomeDistributionContains(request.PossibleQualifiedItemIdsJson, active.ObservedQualifiedItemId))
            {
                verificationReasons = Array.Empty<string>();
                blockReasons = new[] { "catch_fish_observed_outcome_not_in_compiled_distribution" };
                return false;
            }

            if (active.SawBobberBar && !active.SawBobberBarSuccess)
            {
                verificationReasons = Array.Empty<string>();
                blockReasons = new[] { "catch_fish_bobber_bar_success_not_observed", CatchFishMinigameDiagnostic(active) };
                return false;
            }

            if (active.SawBobberBar && active.SawJunkOrSpecialPullWithoutBobberBar)
            {
                verificationReasons = Array.Empty<string>();
                blockReasons = new[] { "catch_fish_junk_or_special_pull_then_bobber_bar", CatchFishCastDiagnostic(active) };
                return false;
            }

            if (!active.SawBobberBar)
            {
                active.TerminalCatchResult = "vanilla_junk_or_special_without_bobber_bar";
            }

            var observedQualifiedItemId = string.IsNullOrWhiteSpace(active.ObservedQualifiedItemId)
                ? "unavailable"
                : active.ObservedQualifiedItemId;
            verificationReasons = new[]
            {
                active.SawBobberBar ? "bobber_bar_success_observed" : "special_catch_without_bobber_bar_observed",
                "fish_caught_hold_observed",
                "inventory_or_collection_updated",
                "action_idle_cleanup_complete",
                "target_casting_power=" + active.DesiredCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "observed_peak_casting_power=" + active.ObservedPeakCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "observed_release_casting_power=" + active.ObservedReleaseCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "max_cast_requested=" + active.MaxCastRequested.ToString().ToLowerInvariant(),
                "max_cast_observed=" + active.ObservedMaxCast.ToString().ToLowerInvariant(),
                "hook_attempt_count=" + active.HookAttemptCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CatchFishMinigameDiagnostic(active),
                "terminal_progress=" + active.TerminalBobberBarProgress.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                "terminal_result=" + active.TerminalCatchResult,
                "observed_qualified_item_id=" + observedQualifiedItemId,
                "observed_outcome_in_compiled_distribution",
                string.IsNullOrWhiteSpace(request.ExpectedQualifiedItemId)
                    ? "candidate_item_match=unconstrained"
                    : "candidate_item_match=" + string.Equals(request.ExpectedQualifiedItemId, active.ObservedQualifiedItemId, StringComparison.Ordinal).ToString().ToLowerInvariant()
            };
            return true;
        }

        verificationReasons = Array.Empty<string>();
        return false;
    }

    private static bool FishingOutcomeDistributionContains(string possibleItemIdsJson, string observedQualifiedItemId)
    {
        try
        {
            using var possibleItemIds = JsonDocument.Parse(possibleItemIdsJson);
            return possibleItemIds.RootElement.ValueKind == JsonValueKind.Array &&
                possibleItemIds.RootElement.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), observedQualifiedItemId, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void CompleteCatchFish(ActiveCatchFish active, string[] verificationReasons)
    {
        activeCatchFish = null;
        ReleaseSmapiLeftButtonOverride();
        var request = active.Pending.Request;
        var afterInventory = InventoryStackSignature();
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            EnergyBefore = active.BeforeStamina,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "catch_fish",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = CatchFishRequestedEffect(request),
            ObservedEffect = CatchFishObservedEffect(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.BeforeInventory, After = afterInventory },
                new SimulatedFactChange { Path = "player.stamina", Before = active.BeforeStamina.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") },
                new SimulatedFactChange { Path = "fishing.rule_key", Before = request.RuleKey, After = request.RuleKey },
                new SimulatedFactChange { Path = "fishing.planned_outcome_distribution_json", Before = request.OutcomeDistributionJson, After = request.OutcomeDistributionJson },
                new SimulatedFactChange { Path = "fishing.outcome_probability_status", Before = request.OutcomeProbabilityStatus, After = request.OutcomeProbabilityStatus },
                new SimulatedFactChange { Path = "fishing.caught_qualified_item_id", Before = string.Empty, After = active.ObservedQualifiedItemId },
                new SimulatedFactChange { Path = "fishing.target_casting_power", Before = string.Empty, After = active.DesiredCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.observed_peak_casting_power", Before = string.Empty, After = active.ObservedPeakCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.observed_release_casting_power", Before = string.Empty, After = active.ObservedReleaseCastingPower.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.max_cast_requested", Before = string.Empty, After = active.MaxCastRequested.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "fishing.max_cast_observed", Before = string.Empty, After = active.ObservedMaxCast.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "fishing.hook_attempt_count", Before = string.Empty, After = active.HookAttemptCount.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.bobber_bar_tick_count", Before = string.Empty, After = active.BobberBarTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.bobber_bar_in_bar_ratio", Before = string.Empty, After = active.BobberInBarRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.terminal_progress", Before = string.Empty, After = active.TerminalBobberBarProgress.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "fishing.terminal_result", Before = string.Empty, After = active.TerminalCatchResult },
                new SimulatedFactChange { Path = "fishing.action_idle_cleanup_complete", Before = string.Empty, After = active.IdleCleanupComplete.ToString().ToLowerInvariant() }
            }
        });
    }

    private void CompleteBlockedCatchFish(ActiveCatchFish active, params string[] reasons)
    {
        activeCatchFish = null;
        ReleaseSmapiLeftButtonOverride();
        CancelCatchFish(active);
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "catch_fish", CatchFishRequestedEffect(active.Pending.Request), CatchFishObservedEffect(), reasons));
    }

    private void CompleteObservedBlockedCatchFish(ActiveCatchFish active, string[] reasons)
    {
        activeCatchFish = null;
        ReleaseSmapiLeftButtonOverride();
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            BlockReasons = reasons,
            EnergyBefore = active.BeforeStamina,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "catch_fish",
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = CatchFishRequestedEffect(request),
            ObservedEffect = CatchFishObservedEffect(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.BeforeInventory, After = InventoryStackSignature() },
                new SimulatedFactChange { Path = "player.stamina", Before = active.BeforeStamina.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") },
                new SimulatedFactChange { Path = "fishing.planned_outcome_distribution_json", Before = request.OutcomeDistributionJson, After = request.OutcomeDistributionJson },
                new SimulatedFactChange { Path = "fishing.caught_qualified_item_id", Before = string.Empty, After = active.ObservedQualifiedItemId }
            }
        });
    }

    private static void CancelCatchFish(ActiveCatchFish active)
    {
        if (Game1.activeClickableMenu is BobberBar)
        {
            Game1.exitActiveMenu();
        }

        active.Rod.doneFishing(Game1.player);
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.canReleaseTool = true;
    }

    private static int ExpectedFishCaughtCount(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId) || !qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal))
        {
            return 0;
        }

        if (Game1.player.fishCaught?.TryGetValue(qualifiedItemId, out var qualifiedCount) == true)
        {
            return qualifiedCount[0];
        }

        var itemId = qualifiedItemId.Substring(3);
        return Game1.player.fishCaught?.TryGetValue(itemId, out var legacyCount) == true ? legacyCount[0] : 0;
    }

    private static string CatchFishRequestedEffect(TrainingExecutionRequest request)
    {
        return "fishing.catch;location=" + request.LocationId + ";stand_tile=" + request.StandTileX + "," + request.StandTileY + ";bobber_tile=" + request.BobberTileX + "," + request.BobberTileY + ";rod_slot_index=" + request.RodSlotIndex + ";rule_key=" + request.RuleKey + ";expected_qualified_item_id=" + (string.IsNullOrWhiteSpace(request.ExpectedQualifiedItemId) ? "unconstrained" : request.ExpectedQualifiedItemId) + ";outcome_distribution_complete=" + request.OutcomeDistributionComplete.ToString().ToLowerInvariant() + ";outcome_probability_status=" + request.OutcomeProbabilityStatus;
    }

    private static string CatchFishObservedEffect()
    {
        var rod = Game1.player.CurrentTool as FishingRod;
        var menu = Game1.activeClickableMenu?.GetType().Name ?? "none";
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";stand_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";current_tool=" + (rod?.QualifiedItemId ?? "none") +
            ";active_menu=" + menu +
            ";rod_state=" + (rod is null ? "none" : "isFishing=" + rod.isFishing + ",isNibbling=" + rod.isNibbling + ",isReeling=" + rod.isReeling + ",fishCaught=" + rod.fishCaught);
    }

    private static void ClearReadyMachineOutputsForFixture(GameLocation location, Vector2 preservedTile)
    {
        foreach (var pair in location.objects.Pairs.ToArray())
        {
            if (pair.Key == preservedTile || !pair.Value.bigCraftable.Value)
            {
                continue;
            }

            if (pair.Value.readyForHarvest.Value || pair.Value.heldObject.Value is not null)
            {
                pair.Value.readyForHarvest.Value = false;
                pair.Value.heldObject.Value = null;
                if (pair.Value.MinutesUntilReady < 0)
                {
                    pair.Value.MinutesUntilReady = 0;
                }
            }
        }
    }

    private static void RefreshTransparentMachineProbeCache()
    {
        try
        {
            var bridgeType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("StardewAI.TransparentBridge.Adapters.FarmReadAdapter", throwOnError: false))
                .FirstOrDefault(type => type is not null);
            var method = bridgeType?.GetMethod("RefreshMachineProbeCache", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
        catch
        {
            // The executor must not fail because the read-side cache refresh failed.
        }
    }

    private static string MachineRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.machines[" + request.TargetTileX + "," + request.TargetTileY + "].held_item=null;player.inventory.updated";
    }

    private static string MachineInputRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.machines[" + request.TargetTileX + "," + request.TargetTileY + "].minutes_until_ready>0_or_ready=true;player.inventory[" + request.InputSlotIndex + "].stack_decreases";
    }

    private static string MachineObservedEffect(GameLocation location, Point target)
    {
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return "machine_present=false";
        }

        return "machine_present=true;qualified_item_id=" + machine.QualifiedItemId +
            ";ready_for_harvest=" + machine.readyForHarvest.Value.ToString().ToLowerInvariant() +
            ";minutes_until_ready=" + machine.MinutesUntilReady +
            ";held_item=" + (machine.heldObject.Value?.QualifiedItemId ?? "null");
    }

    private static int EnsureInventoryItem(string qualifiedItemId, int stack)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var existing = Game1.player.Items[index];
            if (existing is not null &&
                string.Equals(existing.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                if (existing.Stack < stack)
                {
                    existing.Stack = stack;
                }
                return index;
            }
        }

        var item = ItemRegistry.Create(qualifiedItemId, stack);
        if (!Game1.player.addItemToInventoryBool(item))
        {
            return -1;
        }

        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var existing = Game1.player.Items[index];
            if (existing is not null &&
                string.Equals(existing.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string InventoryStackSignature()
    {
        return string.Join("|", Game1.player.Items
            .Select((item, index) => item is null ? index + ":null" : index + ":" + item.QualifiedItemId + ":" + item.Stack)
            .Where(value => !value.EndsWith(":null", StringComparison.Ordinal)));
    }

    private TrainingExecutionResult ExecuteSetupShippingTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (Game1.currentLocation is not Farm farm ||
            !string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none"),
                "fixture_requires_farm");
        }

        var qualifiedItemId = !string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? request.QualifiedItemId
            : "(O)388";
        var quantity = Math.Max(1, request.Quantity ?? 5);

        var slotIndex = EnsureInventoryItem(qualifiedItemId, quantity);
        if (slotIndex < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "inventory_full_or_item_invalid");
        }

        ShippingBin? bin;
        if (request.TargetTileX.HasValue && request.TargetTileY.HasValue)
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b =>
                    b.daysOfConstructionLeft.Value <= 0 &&
                    request.TargetTileX.Value >= b.tileX.Value &&
                    request.TargetTileX.Value <= b.tileX.Value + b.tilesWide.Value - 1 &&
                    request.TargetTileY.Value == b.tileY.Value);
        }
        else
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b => b.daysOfConstructionLeft.Value <= 0);
        }

        if (bin is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "no_completed_shipping_bin");
        }

        var binCenterX = (float)(bin.tileX.Value + bin.tilesWide.Value * 0.5);
        var binCenterY = (float)bin.tileY.Value;
        Point? standTile = null;
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                var tx = bin.tileX.Value + dx;
                var ty = bin.tileY.Value + dy;
                if (tx >= bin.tileX.Value && tx < bin.tileX.Value + bin.tilesWide.Value &&
                    ty == bin.tileY.Value) continue;
                if (tx < 0 || ty < 0 || tx >= farm.map.Layers[0].LayerWidth ||
                    ty >= farm.map.Layers[0].LayerHeight) continue;
                var dist = Math.Sqrt((tx - binCenterX) * (tx - binCenterX) +
                                     (ty - binCenterY) * (ty - binCenterY));
                if (dist > 2.0) continue;
                var tileLoc = new xTile.Dimensions.Location(tx, ty);
                if (farm.isTilePassable(tileLoc, Game1.viewport) &&
                    !farm.isCollidingPosition(
                        new XnaRectangle(tx * 64 + 1, ty * 64 + 1, 62, 62),
                        Game1.viewport, isFarmer: true, damagesFarmer: 0, glider: false,
                        Game1.player, pathfinding: true))
                {
                    standTile = new Point(tx, ty);
                    break;
                }
            }
            if (standTile.HasValue) break;
        }

        if (!standTile.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "no_passable_stand_tile_near_bin");
        }

        var item = Game1.player.Items[slotIndex];
        var unqualifiedId = item?.ItemId ?? string.Empty;

        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.player.Position = new Vector2(
            standTile.Value.X * 64 + 32 - Game1.player.GetBoundingBox().Width / 2,
            standTile.Value.Y * 64 + 32 - Game1.player.GetBoundingBox().Height / 2 + 16);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_shipping_target",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "fixture_item_ensured", "slot_index=" + slotIndex,
                "qualified_item_id=" + qualifiedItemId,
                "unqualified_item_id=" + unqualifiedId,
                "bin_tile=" + bin.tileX.Value + "," + bin.tileY.Value,
                "stand_tile=" + standTile.Value.X + "," + standTile.Value.Y
            },
            RequestedEffect = "player.inventory[" + slotIndex + "].stack>=" + quantity,
            ObservedEffect = "fixture_item_ensured;slot_index=" + slotIndex +
                ";qualified_item_id=" + qualifiedItemId,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.inventory.slot_index",
                    Before = "",
                    After = slotIndex.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "shipping_bin.tile",
                    Before = "",
                    After = bin.tileX.Value + "," + bin.tileY.Value
                },
                new SimulatedFactChange
                {
                    Path = "shipping_bin.stand_tile",
                    Before = "",
                    After = standTile.Value.X + "," + standTile.Value.Y
                }
            }
        };
    }

    private void OnDayStartedForShippingReceipts(object? sender, StardewModdingAPI.Events.DayStartedEventArgs e)
    {
        try
        {
            TrySettleActiveRunPendingShippingReceipts();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shipping receipt reconciliation error: {ex.Message}", LogLevel.Warn);
        }
    }

    private void ReconcileShippingReceipts()
    {
        try
        {
            var receiptsDir = ResolveReceiptDirectory();
            if (!Directory.Exists(receiptsDir)) return;

            var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
            var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
            foreach (var receiptPath in receiptFiles)
            {
                try
                {
                    var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                    var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                    if (receipt is null) continue;

                    var isTerminal = receipt.Status == "completed" || receipt.Status == "failed" || receipt.Status == "ambiguous" || receipt.Status == "timed_out";

                    if (receipt.Status == "pending")
                    {
                        if (string.IsNullOrWhiteSpace(activeRunId) ||
                            !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                            continue;

                        if (receipt.ExpiresAt != null && DateTimeOffset.TryParse(receipt.ExpiresAt, out var expires) && DateTimeOffset.UtcNow > expires)
                        {
                            receipt.Status = "timed_out";
                            receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                            receipt.SettlementReason = "receipt_expired";
                            if (!receipt.FeedbackAppended)
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                                if (AppendDelayedFeedback(receipt))
                                {
                                    receipt.FeedbackAppended = true;
                                }
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                            else
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                        }
                    }
                    else if (isTerminal && !receipt.FeedbackAppended)
                    {
                        if (AppendDelayedFeedback(receipt))
                        {
                            receipt.FeedbackAppended = true;
                        }
                        AtomicWriteReceipt(receiptPath, receipt);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void AtomicWriteReceipt(string receiptPath, ShippingReceipt receipt)
    {
        var tempPath = receiptPath + ".tmp";
        var json = JsonSerializer.Serialize(receipt, JsonOptions);
        File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fs.Length == 0) throw new InvalidOperationException("temp file empty after flush");
        }
        File.Move(tempPath, receiptPath, overwrite: true);
    }

    private static string ResolveReceiptDirectory()
    {
        var trainingDir = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(trainingDir))
        {
            return Path.Combine(trainingDir, "pending_receipts");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") == "1")
        {
            throw new InvalidOperationException("STARDEWAI_TRAINING_OUTPUT_DIR is required when STARDEWAI_TRAINING_MODE=1");
        }

        var dir = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "training_output");
        return Path.Combine(dir, "pending_receipts");
    }

    private bool AppendDelayedFeedback(ShippingReceipt receipt)
    {
        try
        {
            if (receipt.FeedbackAppended) return true;

            var dir = ResolveReceiptDirectory();
            var feedbackPath = Path.Combine(dir, "delayed_shipping_feedback.jsonl");

            if (File.Exists(feedbackPath))
            {
                var existingLines = File.ReadAllLines(feedbackPath, System.Text.Encoding.UTF8);
                foreach (var existingLine in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(existingLine)) continue;
                    try
                    {
                        var existingRow = JsonSerializer.Deserialize<JsonElement>(existingLine, JsonOptions);
                        if (existingRow.TryGetProperty("receipt_id", out var existingId) &&
                            string.Equals(existingId.GetString(), receipt.ReceiptId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }

            var row = new
            {
                receipt_id = receipt.ReceiptId,
                run_id = receipt.RunId,
                queue_id = receipt.QueueId,
                queue_item_id = receipt.QueueItemId,
                request_nonce = receipt.RequestNonce,
                unqualified_item_id = receipt.UnqualifiedItemId,
                qualified_item_id = receipt.QualifiedItemId,
                quantity = receipt.Quantity,
                source_date = receipt.SourceDate,
                pre_basic_shipped_count = receipt.PreBasicShippedCount,
                settled_basic_shipped_count = receipt.SettledBasicShippedCount,
                settlement_status = receipt.Status,
                settlement_reason = receipt.SettlementReason,
                settled_at = receipt.SettledAt,
                settled_game_date = receipt.SettledGameDate
            };
            var line = JsonSerializer.Serialize(row, JsonOptions) + "\n";
            File.AppendAllText(feedbackPath, line, System.Text.Encoding.UTF8);

            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write delayed shipping feedback: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private void TrySettleActiveRunPendingShippingReceipts()
    {
        var receiptsDir = ResolveReceiptDirectory();
        if (!Directory.Exists(receiptsDir)) return;

        var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
        var errors = new List<Exception>();
        foreach (var receiptPath in receiptFiles)
        {
            try
            {
                var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                if (receipt is null || receipt.Status != "pending") continue;

                if (string.IsNullOrWhiteSpace(activeRunId) ||
                    !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                    continue;

                if (!int.TryParse(receipt.SourceDate, out var sourceDate)) continue;
                var currentGameDate = Game1.Date.TotalDays;
                if (currentGameDate <= sourceDate) continue;

                var currentCount = GetBasicShippedCount(Game1.player, receipt.UnqualifiedItemId);
                var expected = receipt.PreBasicShippedCount + receipt.Quantity;
                var newStatus = "ambiguous";
                var reason = string.Empty;

                if (currentCount == expected)
                {
                    newStatus = "completed";
                    reason = "basicShipped_incremented_by_expected_quantity";
                }
                else if (currentCount == receipt.PreBasicShippedCount)
                {
                    newStatus = "failed";
                    reason = "basicShipped_did_not_increment";
                }
                else
                {
                    newStatus = "ambiguous";
                    reason = "basicShipped_unexpected_delta:" + (currentCount - receipt.PreBasicShippedCount);
                }

                receipt.Status = newStatus;
                receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                receipt.SettlementReason = reason;
                receipt.SettledBasicShippedCount = currentCount;
                receipt.SettledGameDate = currentGameDate.ToString();
                receipt.SettledSeason = Game1.currentSeason;
                receipt.SettledDayOfMonth = Game1.dayOfMonth.ToString();
                receipt.SettledYear = Game1.year.ToString();

                AtomicWriteReceipt(receiptPath, receipt);

                if (AppendDelayedFeedback(receipt))
                {
                    receipt.FeedbackAppended = true;
                    AtomicWriteReceipt(receiptPath, receipt);
                }
                Monitor.Log($"Shipping receipt {receipt.ReceiptId} settled: {newStatus} ({reason}). basicShipped: {receipt.PreBasicShippedCount}->{currentCount}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to process shipping receipt {receiptPath}: {ex.Message}", LogLevel.Warn);
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("One or more shipping receipt settlements failed.", errors);
    }

    private void StartShipInventoryItemToBin(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), reasons.ToArray()));
            return;
        }

        var slotIndex = pending.Request.SlotIndex ?? -1;
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_index_invalid"));
            return;
        }

        var slotItem = Game1.player.Items[slotIndex];
        if (slotItem is null || slotItem.Stack <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_empty"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(pending.Request.QualifiedItemId) &&
            !string.Equals(slotItem.QualifiedItemId, pending.Request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_item_id_mismatch"));
            return;
        }

        var quantity = pending.Request.Quantity ?? 1;
        if (quantity != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "quantity_one_required"));
            return;
        }

        if (slotItem.Stack < quantity)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "insufficient_stack"));
            return;
        }

        if (!slotItem.canBeShipped())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "item_not_shippable"));
            return;
        }

        if (string.IsNullOrWhiteSpace(pending.Request.RequestNonce))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "request_nonce_required"));
            return;
        }

        if (Game1.currentLocation is not Farm farm ||
            !string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "not_on_farm"));
            return;
        }

        ShippingBin? bin = null;
        if (pending.Request.TargetTileX.HasValue && pending.Request.TargetTileY.HasValue)
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b =>
                    b.daysOfConstructionLeft.Value <= 0 &&
                    pending.Request.TargetTileX.Value == b.tileX.Value &&
                    pending.Request.TargetTileY.Value == b.tileY.Value);
            if (bin is null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                    ShipRequestedEffect(pending.Request), ShipObservedEffect(), "no_completed_bin_at_target_tile"));
                return;
            }
        }
        else
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "target_tile_required"));
            return;
        }

        if (!pending.Request.StandTileX.HasValue || !pending.Request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "stand_tile_required"));
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile.X != pending.Request.StandTileX.Value || playerTile.Y != pending.Request.StandTileY.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(),
                "player_not_on_exact_stand_tile:expected=" + pending.Request.StandTileX.Value + "," + pending.Request.StandTileY.Value +
                ";actual=" + playerTile.X + "," + playerTile.Y));
            return;
        }

        var distance = Vector2.Distance(
            new Vector2(Game1.player.TilePoint.X + 0.5f, Game1.player.TilePoint.Y + 0.5f),
            new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value + 0.5f));
        if (distance > 2.0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "player_out_of_shipping_range"));
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "menu_already_open"));
            return;
        }

        var binInventory = farm.getShippingBin(Game1.player);
        var beforeInventoryCount = CountInventoryItems(slotItem.QualifiedItemId);
        var beforeBinCount = CountBinItems(binInventory, slotItem.QualifiedItemId);
        var binAggregate = ReadBinAggregate(binInventory);
        var beforeBinTotal = binAggregate.Sum(c => c.count);
        var beforeBinDistinct = binAggregate.Length;
        var beforeBinSignature = ComputeShippingBinSignature(binAggregate);
        var unqualifiedItemId = slotItem.ItemId ?? string.Empty;
        var beforeBasicShipped = GetBasicShippedCount(Game1.player, unqualifiedItemId);
        var beforeSlotQualifiedId = slotItem.QualifiedItemId ?? string.Empty;
        var beforeSlotStack = slotItem.Stack;
        var beforeSlotItemId = unqualifiedItemId;

        activeShipInventoryToBin = new ActiveShipInventoryToBin(
            pending, bin, slotIndex, slotItem.QualifiedItemId ?? string.Empty, unqualifiedItemId, quantity,
            beforeInventoryCount, beforeBinCount, beforeBinTotal, beforeBinDistinct,
            beforeBinSignature, beforeBasicShipped, beforeSlotQualifiedId, beforeSlotStack,
            beforeSlotItemId);
    }

    private void TickShipInventoryToBin()
    {
        if (activeShipInventoryToBin is null) return;

        var active = activeShipInventoryToBin;
        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            ReleaseShipRightButton();
            CleanupAndBlock(active, "ship_timeout");
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.BinPosition:
            case ShipPhase.BinPositionVerify:
            case ShipPhase.BinPress:
            case ShipPhase.BinRelease:
            case ShipPhase.WaitForShippingMenu:
                TickShipBinOpenPhase(active);
                break;
            case ShipPhase.SlotPosition:
            case ShipPhase.SlotPositionVerify:
            case ShipPhase.SlotPress:
            case ShipPhase.SlotRelease:
            case ShipPhase.WaitForSlotDispatch:
                TickShipSlotClickPhase(active);
                break;
            case ShipPhase.VerifyAndClose:
                TickShipVerifyAndClose(active);
                break;
        }
    }

    private void TickShipBinOpenPhase(ActiveShipInventoryToBin active)
    {
        switch (active.Phase)
        {
            case ShipPhase.BinPosition:
                if (active.PositionSet && !active.PositionVerified)
                    active.Phase = ShipPhase.BinPositionVerify;
                break;

            case ShipPhase.BinPositionVerify:
                if (active.PositionVerified && !active.ButtonPressed)
                    active.Phase = ShipPhase.BinPress;
                break;

            case ShipPhase.BinPress:
                if (active.ButtonPressed && !active.ButtonReleased)
                    active.Phase = ShipPhase.BinRelease;
                break;

            case ShipPhase.BinRelease:
                if (active.ButtonReleased && Game1.activeClickableMenu is ItemGrabMenu menu && menu.shippingBin)
                {
                    active.SawShippingMenu = true;
                    active.ButtonPressed = false;
                    active.ButtonReleased = false;
                    active.PositionSet = false;
                    active.PositionVerified = false;
                    active.Phase = ShipPhase.SlotPosition;
                }
                break;

            case ShipPhase.WaitForShippingMenu:
                if (Game1.activeClickableMenu is ItemGrabMenu binMenu && binMenu.shippingBin)
                {
                    active.SawShippingMenu = true;
                    active.Phase = ShipPhase.SlotPosition;
                }
                break;
        }
    }

    private void TickShipSlotClickPhase(ActiveShipInventoryToBin active)
    {
        var menu = Game1.activeClickableMenu as ItemGrabMenu;
        if (menu is null || !menu.shippingBin)
        {
            ReleaseShipRightButton();
            CleanupAndBlock(active, "shipping_menu_lost");
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.SlotPosition:
                if (active.PositionSet && !active.PositionVerified)
                    active.Phase = ShipPhase.SlotPositionVerify;
                break;

            case ShipPhase.SlotPositionVerify:
                if (active.PositionVerified && !active.ButtonPressed)
                    active.Phase = ShipPhase.SlotPress;
                break;

            case ShipPhase.SlotPress:
                if (active.ButtonPressed && !active.ButtonReleased)
                    active.Phase = ShipPhase.SlotRelease;
                break;

            case ShipPhase.SlotRelease:
                if (active.ButtonReleased)
                {
                    active.SlotClickDispatched = true;
                    active.Phase = ShipPhase.WaitForSlotDispatch;
                }
                break;

            case ShipPhase.WaitForSlotDispatch:
                if (active.SlotClickDispatched)
                {
                    var slotItem = Game1.player.Items[active.SlotIndex];
                    var slotStackNow = slotItem?.Stack ?? 0;
                    var slotQualifiedIdNow = slotItem?.QualifiedItemId ?? string.Empty;
                    var afterInventoryCount = CountInventoryItems(active.QualifiedItemId);
                    var binInventory = (Game1.currentLocation as Farm)?.getShippingBin(Game1.player);
                    var afterBinCount = CountBinItems(binInventory, active.QualifiedItemId);

                    var slotIdentityOk = string.Equals(slotQualifiedIdNow, active.BeforeSlotQualifiedId, StringComparison.OrdinalIgnoreCase);
                    var slotStackOk = false;
                    if (active.BeforeSlotStack > 1)
                        slotStackOk = slotIdentityOk && slotStackNow == active.BeforeSlotStack - active.Quantity;
                    else
                        slotStackOk = slotStackNow == 0 && (slotItem is null || string.IsNullOrEmpty(slotQualifiedIdNow));

                    var inventoryDecreased = afterInventoryCount == active.InventoryCountBefore - active.Quantity;
                    var binIncreased = afterBinCount == active.BinCountBefore + active.Quantity;
                    var verified = slotStackOk && inventoryDecreased && binIncreased;

                    if (!slotStackOk)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_stack_delta_mismatch",
                            "expected_stack=" + (active.BeforeSlotStack - active.Quantity) + ";actual=" + slotStackNow,
                            "before_stack=" + active.BeforeSlotStack + ";slot_null=" + (slotItem is null).ToString().ToLowerInvariant());
                        return;
                    }
                    if (afterInventoryCount != active.InventoryCountBefore && !inventoryDecreased)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "ambiguous_quantity_delta",
                            "inventory_delta=" + (afterInventoryCount - active.InventoryCountBefore),
                            "bin_delta=" + (afterBinCount - active.BinCountBefore));
                        return;
                    }
                    if (verified)
                    {
                        active.Phase = ShipPhase.VerifyAndClose;
                        active.AfterSlotStack = slotStackNow;
                        active.AfterSlotQualifiedId = slotQualifiedIdNow;
                        active.SawShipDispatch = true;
                    }
                }
                break;
        }
    }

    private void TickShipVerifyAndClose(ActiveShipInventoryToBin active)
    {
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
            return;
        }

        var fItem = Game1.player.Items[active.SlotIndex];
        var fSlotQualifiedId = fItem?.QualifiedItemId ?? string.Empty;
        var fSlotStack = fItem?.Stack ?? 0;
        var fInventoryCount = CountInventoryItems(active.QualifiedItemId);
        var fBinInventory = (Game1.currentLocation as Farm)?.getShippingBin(Game1.player);
        var fBinCount = CountBinItems(fBinInventory, active.QualifiedItemId);
        var fBinAggregate = ReadBinAggregate(fBinInventory);
        var fBinTotal = fBinAggregate.Sum(c => c.count);
        var fBinDistinct = fBinAggregate.Length;
        var fBinSignature = ComputeShippingBinSignature(fBinAggregate);

        var fSlotStackOk = false;
        if (active.BeforeSlotStack > 1)
            fSlotStackOk = string.Equals(fSlotQualifiedId, active.BeforeSlotQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                fSlotStack == active.BeforeSlotStack - active.Quantity;
        else
            fSlotStackOk = fSlotStack == 0 && (fItem is null || string.IsNullOrEmpty(fSlotQualifiedId));

        var fInventoryDecreased = fInventoryCount == active.InventoryCountBefore - active.Quantity;
        var fBinIncreased = fBinCount == active.BinCountBefore + active.Quantity;
        var fVerified = fSlotStackOk && fInventoryDecreased && fBinIncreased;

        var receiptPath = string.Empty;
        if (fVerified)
        {
            receiptPath = WriteShipPendingReceipt(active, fInventoryCount, fBinCount,
                fBinTotal, fBinDistinct, fBinSignature, fSlotStack, fSlotQualifiedId);
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                ReleaseShipRightButton();
                CleanupAndBlock(active, "receipt_write_failed");
                return;
            }
        }

        CompleteShip(active, fVerified, fInventoryCount, fBinCount,
            fBinTotal, fBinDistinct, fBinSignature, fSlotStack,
            fSlotQualifiedId, receiptPath);
    }

    private void ApplyShipPhaseInput(ActiveShipInventoryToBin active)
    {
        switch (active.Phase)
        {
            case ShipPhase.BinPosition:
                if (!active.PositionSet)
                {
                    var pos = BinScreenPosition(active.Bin);
                    Game1.setMousePosition(pos.X, pos.Y, ui_scale: false);
                    active.PositionTarget = pos;
                    active.PositionSet = true;
                }
                break;

            case ShipPhase.BinPositionVerify:
                if (active.PositionSet && !active.PositionVerified)
                {
                    var actualX = Game1.getMouseX(ui_scale: false);
                    var actualY = Game1.getMouseY(ui_scale: false);
                    if (Math.Abs(actualX - active.PositionTarget.X) > 2 || Math.Abs(actualY - active.PositionTarget.Y) > 2)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active,
                            "cursor_position_mismatch:expected=" + active.PositionTarget.X + "," + active.PositionTarget.Y + ";actual=" + actualX + "," + actualY);
                        return;
                    }
                    active.PositionVerified = true;
                }
                break;

            case ShipPhase.BinPress:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "bin_press_failed:" + reason);
                        return;
                    }
                    active.ButtonPressed = true;
                }
                break;

            case ShipPhase.BinRelease:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: false, out var releaseReason))
                    {
                        active.ReleaseRetries++;
                        if (active.ReleaseRetries > 3)
                        {
                            CleanupAndBlock(active, "bin_release_failed_after_retries:" + releaseReason);
                            return;
                        }
                        return;
                    }
                    active.ButtonReleased = true;
                }
                break;

            case ShipPhase.SlotPosition:
                if (!active.PositionSet && Game1.activeClickableMenu is ItemGrabMenu menu && menu.shippingBin)
                {
                    var slotPos = InventorySlotScreenPosition(menu, active.SlotIndex);
                    if (!slotPos.HasValue)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_screen_position_unavailable");
                        return;
                    }
                    Game1.setMousePosition(slotPos.Value.X, slotPos.Value.Y, ui_scale: true);
                    active.PositionTarget = slotPos.Value;
                    active.PositionSet = true;
                }
                break;

            case ShipPhase.SlotPositionVerify:
                if (active.PositionSet && !active.PositionVerified && Game1.activeClickableMenu is ItemGrabMenu vMenu && vMenu.shippingBin)
                {
                    var ax = Game1.getMouseX(ui_scale: true);
                    var ay = Game1.getMouseY(ui_scale: true);
                    if (Math.Abs(ax - active.PositionTarget.X) > 2 || Math.Abs(ay - active.PositionTarget.Y) > 2)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active,
                            "slot_cursor_position_mismatch:expected=" + active.PositionTarget.X + "," + active.PositionTarget.Y + ";actual=" + ax + "," + ay);
                        return;
                    }
                    active.PositionVerified = true;
                }
                break;

            case ShipPhase.SlotPress:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_press_failed:" + reason);
                        return;
                    }
                    active.ButtonPressed = true;
                }
                break;

            case ShipPhase.SlotRelease:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: false, out var relReason))
                    {
                        active.ReleaseRetries++;
                        if (active.ReleaseRetries > 3)
                        {
                            CleanupAndBlock(active, "slot_release_failed_after_retries:" + relReason);
                            return;
                        }
                        return;
                    }
                    active.ButtonReleased = true;
                }
                break;
        }
    }

    private void CleanupAndBlock(ActiveShipInventoryToBin active, params string[] reasons)
    {
        ReleaseShipRightButton();
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
        }
        activeShipInventoryToBin = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "ship_inventory_item_to_bin",
            ShipRequestedEffect(active.Pending.Request), ShipObservedEffect(), reasons));
    }

    private static Point BinScreenPosition(ShippingBin bin)
    {
        var worldX = (bin.tileX.Value + 1) * 64;
        var worldY = bin.tileY.Value * 64 + 32;
        return new Point(worldX - Game1.viewport.X, worldY - Game1.viewport.Y);
    }

    private static Point? InventorySlotScreenPosition(ItemGrabMenu menu, int slotIndex)
    {
        if (menu.inventory is null || menu.inventory.inventory is null) return null;
        if (slotIndex < 0 || slotIndex >= menu.inventory.inventory.Count) return null;
        var component = menu.inventory.inventory[slotIndex];
        var bounds = component.bounds;
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static int CountBinItems(object? binInventory, string qualifiedItemId)
    {
        if (binInventory is null) return 0;
        System.Collections.IEnumerable enumerable;
        if (binInventory is System.Collections.IEnumerable binEnum)
            enumerable = binEnum;
        else
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (itemsProp is null) return 0;
            var items = itemsProp.GetValue(binInventory);
            if (items is not System.Collections.IEnumerable itemsEnum) return 0;
            enumerable = itemsEnum;
        }
        var count = 0;
        foreach (var obj in enumerable)
        {
            if (obj is Item item && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
                count += item.Stack;
        }
        return count;
    }

    private static (string itemId, string qualifiedItemId, int count)[] ReadBinAggregate(object? binInventory)
    {
        if (binInventory is null) return Array.Empty<(string, string, int)>();
        System.Collections.IEnumerable enumerable;
        if (binInventory is System.Collections.IEnumerable binEnum)
            enumerable = binEnum;
        else
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (itemsProp is null) return Array.Empty<(string, string, int)>();
            var items = itemsProp.GetValue(binInventory);
            if (items is not System.Collections.IEnumerable itemsEnum) return Array.Empty<(string, string, int)>();
            enumerable = itemsEnum;
        }
        var dict = new Dictionary<string, (string itemId, int count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in enumerable)
        {
            if (obj is Item item && item.Stack > 0)
            {
                var qid = item.QualifiedItemId ?? string.Empty;
                if (dict.TryGetValue(qid, out var existing))
                    dict[qid] = (existing.itemId, existing.count + item.Stack);
                else
                    dict[qid] = (item.ItemId ?? string.Empty, item.Stack);
            }
        }
        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ThenBy(kvp => kvp.Value.itemId, StringComparer.Ordinal)
            .Select(kvp => (kvp.Value.itemId, kvp.Key, kvp.Value.count))
            .ToArray();
    }

    private static string ComputeShippingBinSignature((string itemId, string qualifiedItemId, int count)[] aggregate)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var entry in aggregate)
        {
            sb.Append(entry.qualifiedItemId);
            sb.Append('|');
            sb.Append(entry.count);
            sb.Append('\n');
        }
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int GetBasicShippedCount(Farmer farmer, string unqualifiedItemId)
    {
        if (farmer.basicShipped.TryGetValue(unqualifiedItemId, out var count))
            return count;
        return 0;
    }

    private void ReleaseShipRightButton()
    {
        for (var i = 0; i < 3; i++)
        {
            if (TryApplySmapiRightButtonOverride(pressed: false, out _))
                return;
        }
    }

    private string WriteShipPendingReceipt(ActiveShipInventoryToBin active,
        int afterInventoryCount, int afterBinCount, int afterBinTotal, int afterBinDistinct,
        string afterBinSignature, int afterSlotStack, string afterSlotQualifiedId)
    {
        try
        {
            var receiptsDir = ResolveReceiptDirectory();
            Directory.CreateDirectory(receiptsDir);

            var safeRunId = SanitizeFileName(active.Pending.Request.RunId);
            var safeQueueItemId = SanitizeFileName(active.Pending.Request.QueueItemId);
            var safeNonce = SanitizeFileName(active.Pending.Request.RequestNonce);
            if (string.IsNullOrWhiteSpace(safeNonce) || safeNonce == "unknown")
            {
                Monitor.Log("Shipping receipt write blocked: request nonce is empty after sanitization", LogLevel.Error);
                return string.Empty;
            }
            var receiptFileName = "ship_" + safeRunId + "_" + safeQueueItemId + "_" + safeNonce + ".json";
            var receiptId = "ship_" + safeRunId + "_" + safeQueueItemId + "_" + safeNonce;
            var receiptPath = Path.Combine(receiptsDir, receiptFileName);
            var tempPath = receiptPath + ".tmp";

            var receipt = new ShippingReceipt
            {
                ReceiptId = receiptId,
                Status = "pending",
                RunId = active.Pending.Request.RunId,
                QueueId = active.Pending.Request.QueueId,
                QueueItemId = active.Pending.Request.QueueItemId,
                RequestNonce = active.Pending.Request.RequestNonce,
                UnqualifiedItemId = active.UnqualifiedItemId,
                QualifiedItemId = active.QualifiedItemId,
                Quantity = active.Quantity,
                SourceDate = Game1.Date.TotalDays.ToString(),
                SourceSeason = Game1.currentSeason,
                SourceDayOfMonth = Game1.dayOfMonth.ToString(),
                SourceYear = Game1.year.ToString(),
                PreBasicShippedCount = active.BasicShippedCountBefore,
                PreInventoryCount = active.InventoryCountBefore,
                PreBinCount = active.BinCountBefore,
                PreBinTotal = active.BinTotalCountBefore,
                PreBinDistinct = active.BinDistinctCountBefore,
                PreBinSignature = active.BinSignatureBefore,
                PreSlotStack = active.BeforeSlotStack,
                PreSlotQualifiedId = active.BeforeSlotQualifiedId,
                SlotIndex = active.SlotIndex,
                AfterInventoryCount = afterInventoryCount,
                AfterBinCount = afterBinCount,
                AfterBinTotal = afterBinTotal,
                AfterBinDistinct = afterBinDistinct,
                AfterBinSignature = afterBinSignature,
                AfterSlotStack = afterSlotStack,
                AfterSlotQualifiedId = afterSlotQualifiedId,
                CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToString("O")
            };

            var json = JsonSerializer.Serialize(receipt, JsonOptions);
            File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bytes = new byte[fs.Length];
                fs.Read(bytes, 0, bytes.Length);
                if (bytes.Length == 0) throw new InvalidOperationException("temp file empty after flush");
            }

            File.Move(tempPath, receiptPath, overwrite: true);
            return receiptPath;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write shipping pending receipt: {ex.Message}", LogLevel.Error);
            return string.Empty;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalid.Contains(c)).ToArray();
        return new string(chars).Replace(" ", "_");
    }

    private void CompleteShip(ActiveShipInventoryToBin active, bool verified,
        int afterInventoryCount, int afterBinCount,
        int afterBinTotal, int afterBinDistinct, string afterBinSignature,
        int afterSlotStack, string afterSlotQualifiedId,
        string pendingReceiptPath)
    {
        ReleaseShipRightButton();
        activeShipInventoryToBin = null;

        var startedAt = active.StartedAt;
        var completedAt = DateTimeOffset.UtcNow.ToString("O");
        var status = verified ? "applied" : "blocked";
        var verificationStatus = verified ? "verified" : "observed_mismatch";
        var verificationReasons = verified
            ? new[] { "inventory_count_decreased_by_one", "shipping_bin_count_increased_by_one", "slot_stack_decreased_by_one" }
            : new[] { "shipping_post_state_mismatch" };

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            PrimitiveKind = "ship_inventory_item_to_bin",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = ShipRequestedEffect(active.Pending.Request),
            ObservedEffect = ShipObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ShipSlotIndex = active.SlotIndex,
            ShipQualifiedItemId = active.QualifiedItemId,
            ShipItemId = active.UnqualifiedItemId,
            ShipInventoryCountBefore = active.InventoryCountBefore,
            ShipInventoryCountAfter = afterInventoryCount,
            ShipBinCountBefore = active.BinCountBefore,
            ShipBinCountAfter = afterBinCount,
            ShipBinTotalCountBefore = active.BinTotalCountBefore,
            ShipBinTotalCountAfter = afterBinTotal,
            ShipBinDistinctCountBefore = active.BinDistinctCountBefore,
            ShipBinDistinctCountAfter = afterBinDistinct,
            ShipBinSignatureBefore = active.BinSignatureBefore,
            ShipBinSignatureAfter = afterBinSignature,
            ShipBasicShippedCountBefore = active.BasicShippedCountBefore,
            ShipPendingReceiptPath = pendingReceiptPath,
            ShipBeforeSlotStack = active.BeforeSlotStack,
            ShipAfterSlotStack = afterSlotStack,
            ShipBeforeSlotQualifiedId = active.BeforeSlotQualifiedId,
            ShipAfterSlotQualifiedId = afterSlotQualifiedId,
            ShipSourceDate = Game1.Date.TotalDays.ToString(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory." + active.QualifiedItemId + ".count", Before = active.InventoryCountBefore.ToString(), After = afterInventoryCount.ToString() },
                new SimulatedFactChange { Path = "player.inventory.slot." + active.SlotIndex + ".stack", Before = active.BeforeSlotStack.ToString(), After = afterSlotStack.ToString() },
                new SimulatedFactChange { Path = "player.inventory.slot." + active.SlotIndex + ".qualified_item_id", Before = active.BeforeSlotQualifiedId, After = afterSlotQualifiedId },
                new SimulatedFactChange { Path = "farm.shipping_bin." + active.QualifiedItemId + ".count", Before = active.BinCountBefore.ToString(), After = afterBinCount.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.total_count", Before = active.BinTotalCountBefore.ToString(), After = afterBinTotal.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.distinct_count", Before = active.BinDistinctCountBefore.ToString(), After = afterBinDistinct.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.contents_signature", Before = active.BinSignatureBefore, After = afterBinSignature },
                new SimulatedFactChange { Path = "ship.pending_receipt_path", Before = "", After = pendingReceiptPath }
            }
        });
    }

    private static string ShipRequestedEffect(TrainingExecutionRequest request)
    {
        return "executor_kind=ship_inventory_item_to_bin" +
            ";qualified_item_id=" + (string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "missing" : request.QualifiedItemId) +
            ";slot_index=" + (request.SlotIndex?.ToString() ?? "missing") +
            ";quantity=" + (request.Quantity?.ToString() ?? "1") +
            ";target_tile=" + (request.TargetTileX?.ToString() ?? "missing") + "," + (request.TargetTileY?.ToString() ?? "missing");
    }

    private static string ShipObservedEffect()
    {
        var menuOpen = Game1.activeClickableMenu is not null;
        var menuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var isShippingMenu = (Game1.activeClickableMenu as ItemGrabMenu)?.shippingBin ?? false;
        var playerTile = Game1.player?.TilePoint;
        return "menus.active_menu.is_open=" + menuOpen.ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + menuType +
            ";menus.active_menu.is_shipping=" + isShippingMenu.ToString().ToLowerInvariant() +
            ";player.tile=" + (playerTile?.X.ToString() ?? "none") + "," + (playerTile?.Y.ToString() ?? "none");
    }

    private bool TryApplySmapiRightButtonOverride(bool pressed, out string reason)
    {
        reason = string.Empty;
        var input = Game1.input;
        if (input is null)
        {
            reason = "smapi_input_null";
            return false;
        }

        var inputType = input.GetType();
        if (smapiInputStateType != inputType)
        {
            smapiInputStateType = inputType;
            smapiOverrideButtonMethod = inputType.GetMethod(
                "OverrideButton",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(SButton), typeof(bool) },
                modifiers: null);
        }

        if (smapiOverrideButtonMethod is null)
        {
            reason = "override_button_not_found";
            return false;
        }

        try
        {
            smapiOverrideButtonMethod.Invoke(input, new object[] { SButton.MouseRight, pressed });
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
            reason = "override_button_invoke_failed:" + cause.GetType().Name;
            return false;
        }
    }

    private List<string> ValidateExecutionRequest(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (request.SchemaVersion != "training_execution_request.v1")
        {
            reasons.Add("unsupported_schema_version");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") != "1")
        {
            reasons.Add("training_mode_env_required");
        }

        var expectedRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.RunId) || request.RunId != expectedRunId)
        {
            reasons.Add("run_id_mismatch");
        }

        var expectedSavePath = Path.GetFullPath(Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH") ?? config.SavesPath);
        var requestedSavePath = string.IsNullOrWhiteSpace(request.SaveIsolationPath)
            ? string.Empty
            : Path.GetFullPath(request.SaveIsolationPath);
        if (string.IsNullOrWhiteSpace(requestedSavePath) ||
            !string.Equals(requestedSavePath, expectedSavePath, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("save_isolation_path_mismatch");
        }

        if (!Context.IsWorldReady)
        {
            reasons.Add("world_not_ready");
        }

        if (request.OptionId != "farm.maintain_crops" &&
            request.OptionId != "debug.visible_walk" &&
            request.OptionId != "executor.move_to_tile" &&
            request.OptionId != "executor.traverse_connector" &&
            request.OptionId != "executor.face_direction" &&
            request.OptionId != "executor.wait_ticks" &&
            request.OptionId != "executor.clear_obstacle" &&
            request.OptionId != "executor.mine_stone" &&
            request.OptionId != "executor.break_container" &&
            request.OptionId != "executor.combat_monster" &&
            request.OptionId != "executor.shoot_monster" &&
            request.OptionId != "executor.place_bomb" &&
            request.OptionId != "executor.consume_food" &&
            request.OptionId != "executor.descend_ladder" &&
            request.OptionId != "executor.descend_shaft" &&
            request.OptionId != "executor.exit_mine" &&
            request.OptionId != "executor.till_soil" &&
            request.OptionId != "debug.advance_time_to" &&
            request.OptionId != "debug.setup_watering_target" &&
            request.OptionId != "debug.setup_till_soil_target" &&
            request.OptionId != "debug.setup_fish_frenzy" &&
            request.OptionId != "debug.setup_fish_pond" &&
            request.OptionId != "debug.setup_mine_fishing_floor" &&
            request.OptionId != "debug.setup_mining_floor" &&
            request.OptionId != "debug.setup_breakable_container" &&
            request.OptionId != "debug.setup_mining_combat_fixture" &&
            request.OptionId != "debug.setup_clear_obstacle" &&
            request.OptionId != "debug.setup_plant_seed_target" &&
            request.OptionId != "debug.setup_harvest_crop_target" &&
            request.OptionId != "debug.setup_giant_crop_target" &&
            request.OptionId != "debug.setup_debris_target" &&
            request.OptionId != "debug.setup_machine_output_target" &&
            request.OptionId != "debug.setup_machine_input_target" &&
            request.OptionId != "debug.setup_shipping_target" &&
            request.OptionId != "executor.select_safe_item_slot" &&
            request.OptionId != "executor.close_menu" &&
            request.OptionId != "executor.interact" &&
            request.OptionId != "executor.buy_shop_item" &&
            request.OptionId != "executor.plant_seed" &&
            request.OptionId != "executor.harvest_crop" &&
            request.OptionId != "executor.harvest_giant_crop" &&
            request.OptionId != "executor.pickup_debris" &&
            request.OptionId != "executor.collect_machine_output" &&
            request.OptionId != "executor.load_machine_input" &&
            request.OptionId != "executor.catch_fish" &&
            request.OptionId != "executor.choose_dialogue_response" &&
            request.OptionId != "executor.social_interact" &&
            request.OptionId != "executor.sleep" &&
            request.OptionId != "executor.ship_inventory_item_to_bin")
        {
            reasons.Add("unsupported_option_id");
        }

        if (request.ExecutionMode != "training_singleplayer")
        {
            reasons.Add("unsupported_execution_mode");
        }

        return reasons;
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

    private sealed class PendingExecution
    {
        public PendingExecution(TrainingExecutionRequest request)
        {
            Request = request;
        }

        public TrainingExecutionRequest Request { get; }
        public TaskCompletionSource<TrainingExecutionResult> Completion { get; } = new();
        public List<SimulatedFactChange> ChangedFacts { get; } = new();
        public int MovementClearanceActions { get; set; }
        public int MovementExtraTicks { get; set; }
    }

    private sealed class ActiveTileMove
    {
        public ActiveTileMove(PendingExecution pending, Point startTile, Point targetTile, List<Point> path, Point? connectorActionTile = null, int? connectorExitDirection = null)
        {
            Pending = pending;
            StartTile = startTile;
            TargetTile = targetTile;
            Path = path;
            ConnectorActionTile = connectorActionTile;
            ConnectorExitDirection = connectorExitDirection;
            LastPosition = Game1.player.Position;
            LocationId = Game1.currentLocation.NameOrUniqueName;
            MaxTicks = Math.Max(120, path.Count * 90);
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public Point StartTile { get; }
        public Point TargetTile { get; }
        public Point? ConnectorActionTile { get; }
        public int? ConnectorExitDirection { get; }
        public List<Point> Path { get; set; }
        public string LocationId { get; }
        public bool AllowsLocationChange => Pending.Request.OptionId == "executor.traverse_connector";
        public bool ConnectorActionAttempted { get; set; }
        public int Tick { get; set; }
        public int PathIndex { get; set; }
        public int? CurrentDirection { get; set; }
        public int StuckTicks { get; set; }
        public int SoftObstacleTicks { get; set; }
        public int MaxTicks { get; }
        public Vector2 LastPosition { get; set; }
        public string StartedAt { get; }
    }

    private sealed class ActiveNativeFarmTool
    {
        private ActiveNativeFarmTool(PendingExecution pending, string primitiveKind, string locationId, Point target, List<Point> path, Tool tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool? beforeWatered, bool? beforeHadHoeDirt)
        {
            Pending = pending;
            PrimitiveKind = primitiveKind;
            LocationId = locationId;
            Target = target;
            Path = path;
            Tool = tool;
            StaminaBefore = staminaBefore;
            WaterBefore = waterBefore;
            StartedAt = startedAt;
            EstimatedTicks = estimatedTicks;
            RequestedEffect = requestedEffect;
            BeforeWatered = beforeWatered;
            BeforeHadHoeDirt = beforeHadHoeDirt;
            LastPosition = Game1.player.Position;
            MaxMovementTicks = Math.Max(120, path.Count * 90);
            MaxTicks = MaxMovementTicks + 240;
        }

        public static ActiveNativeFarmTool Water(PendingExecution pending, string locationId, Point target, List<Point> path, WateringCan tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeWatered)
        {
            return new ActiveNativeFarmTool(pending, "water_crop", locationId, target, path, tool, staminaBefore, waterBefore, startedAt, estimatedTicks, requestedEffect, beforeWatered, null);
        }

        public static ActiveNativeFarmTool Till(PendingExecution pending, string locationId, Point target, List<Point> path, Hoe tool, double staminaBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeHadHoeDirt)
        {
            return new ActiveNativeFarmTool(pending, "till_soil", locationId, target, path, tool, staminaBefore, null, startedAt, estimatedTicks, requestedEffect, null, beforeHadHoeDirt);
        }

        public PendingExecution Pending { get; }
        public string PrimitiveKind { get; }
        public string LocationId { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public Tool Tool { get; }
        public double StaminaBefore { get; }
        public int? WaterBefore { get; }
        public string StartedAt { get; }
        public int EstimatedTicks { get; }
        public string RequestedEffect { get; }
        public bool? BeforeWatered { get; }
        public bool? BeforeHadHoeDirt { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MaxMovementTicks { get; }
        public int MaxTicks { get; }
        public Vector2 LastPosition { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
    }

    private sealed class ActiveCatchFish
    {
        public ActiveCatchFish(PendingExecution pending, Point standTile, Point bobberTile, FishingRod rod, float desiredCastingPower, bool maxCastRequested, string beforeInventory, float beforeStamina, int beforeExpectedCaughtCount)
        {
            Pending = pending;
            StandTile = standTile;
            BobberTile = bobberTile;
            Rod = rod;
            DesiredCastingPower = desiredCastingPower;
            MaxCastRequested = maxCastRequested;
            BeforeInventory = beforeInventory;
            BeforeStamina = beforeStamina;
            BeforeExpectedCaughtCount = beforeExpectedCaughtCount;
        }

        public PendingExecution Pending { get; }
        public Point StandTile { get; }
        public Point BobberTile { get; }
        public FishingRod Rod { get; }
        public float DesiredCastingPower { get; }
        public bool MaxCastRequested { get; }
        public string BeforeInventory { get; }
        public float BeforeStamina { get; }
        public int BeforeExpectedCaughtCount { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 9000;
        public bool SawNibble { get; set; }
        public bool HookIssuedForNibble { get; set; }
        public int HookAttemptCount { get; set; }
        public bool SawTimingCast { get; set; }
        public bool WasTimingCastLastTick { get; set; }
        public bool SawCasting { get; set; }
        public bool SawCastingAir { get; set; }
        public bool SawFishing { get; set; }
        public bool SawPullingOutOfWater { get; set; }
        public bool SawJunkOrSpecialPullWithoutBobberBar { get; set; }
        public Point? LastBobberTile { get; set; }
        public bool SawBobberBar { get; set; }
        public bool SawBobberBarSuccess { get; set; }
        public int BobberBarTicks { get; set; }
        public int BobberInBarTicks { get; set; }
        public int BobberControlTicks { get; set; }
        public int BobberControlPressedTicks { get; set; }
        public float BobberInBarRatio => BobberBarTicks == 0 ? 0f : BobberInBarTicks / (float)BobberBarTicks;
        public float MinDistanceFromCatching { get; set; } = 1f;
        public float LastDistanceFromCatching { get; set; }
        public float TerminalBobberBarProgress { get; set; } = -1f;
        public string TerminalCatchResult { get; set; } = "none";
        public float LastFishPosition { get; set; }
        public float LastFishSpeed { get; set; }
        public float LastBarPosition { get; set; }
        public float LastBarSpeed { get; set; }
        public int LastBarHeight { get; set; }
        public float ObservedPeakCastingPower { get; set; }
        public float ObservedReleaseCastingPower { get; set; } = -1f;
        public bool ObservedMaxCast { get; set; }
        public bool IdleCleanupComplete { get; set; }
        public bool SawFishCaughtHold { get; set; }
        public string ObservedQualifiedItemId { get; set; } = string.Empty;
    }

    private sealed class ActiveMineFishingSetup
    {
        public ActiveMineFishingSetup(PendingExecution pending, int mineLevel, string beforeLocation, MineFishingFixtureFacts prerequisiteFacts)
        {
            Pending = pending;
            MineLevel = mineLevel;
            BeforeLocation = beforeLocation;
            PrerequisiteFacts = prerequisiteFacts;
        }

        public PendingExecution Pending { get; }
        public int MineLevel { get; }
        public string BeforeLocation { get; }
        public MineFishingFixtureFacts PrerequisiteFacts { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 600;
    }

    private sealed class ActiveMineStone
    {
        public ActiveMineStone(PendingExecution pending, string locationId, Point target, List<Point> path, Pickaxe pickaxe, string qualifiedItemId, int healthBefore, double staminaBefore, int maxSwings, string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            Target = target;
            Path = path;
            Pickaxe = pickaxe;
            QualifiedItemId = qualifiedItemId;
            HealthBefore = healthBefore;
            StaminaBefore = staminaBefore;
            MaxSwings = maxSwings;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Max(180, path.Count * 90) + maxSwings * 240;
            LastPosition = Game1.player.Position;
            ObservedHealth.Add(healthBefore);
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public Pickaxe Pickaxe { get; }
        public string QualifiedItemId { get; }
        public int HealthBefore { get; }
        public double StaminaBefore { get; }
        public int MaxSwings { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int SwingCount { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public bool CombatInterrupted { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public List<int> ObservedHealth { get; } = new();
    }

    private sealed class ActiveBreakContainer
    {
        public ActiveBreakContainer(PendingExecution pending, MineShaft mine, Point target, List<Point> path, BreakableContainer container, Tool tool, int healthBefore, int maxSwings, int restoreSlotIndex, string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Path = path;
            Container = container;
            Tool = tool;
            HealthBefore = healthBefore;
            MaxSwings = maxSwings;
            RestoreSlotIndex = restoreSlotIndex;
            RequestedEffect = requestedEffect;
            DebrisCountBefore = mine.debris.Count;
            MaxTicks = Math.Max(300, path.Count * 90 + maxSwings * 180);
            LastPosition = Game1.player.Position;
            ObservedHealth.Add(healthBefore);
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public BreakableContainer Container { get; }
        public Tool Tool { get; }
        public int HealthBefore { get; }
        public int MaxSwings { get; }
        public int RestoreSlotIndex { get; }
        public string RequestedEffect { get; }
        public int DebrisCountBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int SwingCount { get; set; }
        public bool ButtonHeld { get; set; }
        public bool CombatInterrupted { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public List<int> ObservedHealth { get; } = new();
    }

    private sealed class ActiveShootMonster
    {
        public ActiveShootMonster(
            PendingExecution pending,
            MineShaft mine,
            Monster target,
            Slingshot slingshot,
            string ammoQualifiedItemId,
            int ammoCountBefore,
            int restoreSlotIndex,
            int maxAttacks,
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Slingshot = slingshot;
            SlingshotSlotIndex = Game1.player.Items.IndexOf(slingshot);
            AmmoQualifiedItemId = ammoQualifiedItemId;
            AmmoCountBefore = ammoCountBefore;
            RestoreSlotIndex = restoreSlotIndex;
            MaxAttacks = maxAttacks;
            RequestedEffect = requestedEffect;
            TargetHealthBefore = target.Health;
            LastTargetHealth = target.Health;
            TargetHealthSequence.Add(target.Health);
            MaxTicks = Math.Clamp(1200 + maxAttacks * 180, 1800, 7200);
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Monster Target { get; }
        public Slingshot Slingshot { get; }
        public int SlingshotSlotIndex { get; }
        public string AmmoQualifiedItemId { get; }
        public int AmmoCountBefore { get; }
        public int RestoreSlotIndex { get; }
        public int MaxAttacks { get; }
        public string RequestedEffect { get; }
        public int TargetHealthBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public bool ButtonHeld { get; set; }
        public int HoldTicks { get; set; }
        public int CooldownTicks { get; set; }
        public bool AimPrepared { get; set; }
        public int AttackCount { get; set; }
        public int HitCount { get; set; }
        public int LastTargetHealth { get; set; }
        public List<int> TargetHealthSequence { get; } = new();
    }

    private sealed class ActivePlaceBomb
    {
        public ActivePlaceBomb(
            PendingExecution pending,
            MineShaft mine,
            Point target,
            Point escape,
            List<Point> path,
            int bombSlotIndex,
            StardewValley.Object bomb,
            int radius,
            int restoreSlotIndex,
            int objectCountBefore,
            Monster? targetMonster,
            string terminalState,
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Escape = escape;
            Path = path;
            BombSlotIndex = bombSlotIndex;
            BombQualifiedItemId = bomb.QualifiedItemId;
            BombStackBefore = bomb.Stack;
            Radius = radius;
            RestoreSlotIndex = restoreSlotIndex;
            ObjectCountBefore = objectCountBefore;
            TargetMonster = targetMonster;
            TerminalState = terminalState;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public Point Escape { get; }
        public List<Point> Path { get; }
        public List<Point> EscapePath { get; set; } = new();
        public int BombSlotIndex { get; }
        public string BombQualifiedItemId { get; }
        public int BombStackBefore { get; }
        public int Radius { get; }
        public int RestoreSlotIndex { get; }
        public int ObjectCountBefore { get; }
        public Monster? TargetMonster { get; }
        public string TerminalState { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public int PlacedAtTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public PlaceBombStage Stage { get; set; }
    }

    private enum PlaceBombStage
    {
        MoveToPlacement,
        AimPlacement,
        PressPlacement,
        ReleasePlacement,
        Escape,
        WaitForExplosion
    }

    private sealed class ActiveCombatMonster
    {
        public ActiveCombatMonster(PendingExecution pending, string locationId, Monster target, MeleeWeapon weapon, int maxAttacks, int maxMovementTiles, bool manualMovement, string terminalState, string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            Target = target;
            Weapon = weapon;
            TargetRuntimeType = target.GetType().FullName ?? target.GetType().Name;
            TargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target).ToString("X8");
            TargetName = target.Name;
            TargetHealthBefore = target.Health;
            PlayerHealthBefore = Game1.player.health;
            MaxAttacks = maxAttacks;
            MaxMovementTiles = maxMovementTiles;
            ManualMovement = manualMovement;
            TerminalState = terminalState;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Clamp(1200 + maxAttacks * 120, 1800, 7200);
            LastProgressPosition = Game1.player.Position;
            LastMovementPosition = Game1.player.Position;
            LastMovementTile = Game1.player.TilePoint;
            LastProgressTargetHealth = target.Health;
            InventoryBefore = InventoryStackSignature();
            TargetHealthSequence.Add(target.Health);
            PlayerHealthSequence.Add(Game1.player.health);
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public Monster Target { get; private set; }
        public MeleeWeapon Weapon { get; }
        public string TargetRuntimeType { get; private set; }
        public string TargetRuntimeIdentity { get; private set; }
        public string TargetName { get; private set; }
        public int TargetHealthBefore { get; private set; }
        public int PlayerHealthBefore { get; }
        public int MaxAttacks { get; }
        public int MaxMovementTiles { get; }
        public bool ManualMovement { get; }
        public string TerminalState { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int MovementTiles { get; set; }
        public List<Point> Path { get; set; } = new();
        public int PathIndex { get; set; }
        public Point PathTarget { get; set; } = new(-1, -1);
        public int PathFailures { get; set; }
        public int StuckTicks { get; set; }
        public bool AttackButtonHeld { get; set; }
        public int AttackCount { get; set; }
        public int HitCount { get; set; }
        public Vector2 LastProgressPosition { get; set; }
        public Vector2 LastMovementPosition { get; set; }
        public Point LastMovementTile { get; set; }
        public int LastProgressTargetHealth { get; set; }
        public int NoProgressTicks { get; set; }
        public string InventoryBefore { get; }
        public Point? ClearanceTarget { get; set; }
        public Tool? ClearanceTool { get; set; }
        public string ClearanceBefore { get; set; } = string.Empty;
        public bool ClearanceButtonHeld { get; set; }
        public int ClearanceSwings { get; set; }
        public string LastNoProgressReason { get; set; } = string.Empty;
        public List<int> TargetHealthSequence { get; } = new();
        public List<int> PlayerHealthSequence { get; } = new();

        public void Retarget(Monster target)
        {
            Target = target;
            TargetRuntimeType = target.GetType().FullName ?? target.GetType().Name;
            TargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target).ToString("X8");
            TargetName = target.Name;
            TargetHealthBefore = target.Health;
            TargetHealthSequence.Clear();
            TargetHealthSequence.Add(target.Health);
        }
    }

    private sealed class ActiveConsumeFood
    {
        public ActiveConsumeFood(PendingExecution pending, string locationId, int foodSlotIndex, string foodQualifiedItemId, int foodStackBefore, int restoreSlotIndex, int healthBefore, double energyBefore, string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            FoodSlotIndex = foodSlotIndex;
            FoodQualifiedItemId = foodQualifiedItemId;
            FoodStackBefore = foodStackBefore;
            RestoreSlotIndex = restoreSlotIndex;
            HealthBefore = healthBefore;
            EnergyBefore = energyBefore;
            RequestedEffect = requestedEffect;
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public int FoodSlotIndex { get; }
        public string FoodQualifiedItemId { get; }
        public int FoodStackBefore { get; }
        public int RestoreSlotIndex { get; }
        public int HealthBefore { get; }
        public double EnergyBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public ConsumeFoodStage Stage { get; set; }
        public bool RightButtonHeld { get; set; }
        public bool NativeConfirmationIssued { get; set; }
        public bool EatingObserved { get; set; }
    }

    private sealed class ActivePickupDebris
    {
        public ActivePickupDebris(PendingExecution pending, GameLocation location, Debris debris, Chunk chunk, Point initialTarget, string qualifiedItemId, int debrisCountBefore, int chunkCountBefore, int itemCountBefore, string inventoryBefore, string requestedEffect)
        {
            Pending = pending;
            Location = location;
            LocationId = location.NameOrUniqueName;
            Debris = debris;
            Chunk = chunk;
            PathTarget = initialTarget;
            QualifiedItemId = qualifiedItemId;
            DebrisCountBefore = debrisCountBefore;
            ChunkCountBefore = chunkCountBefore;
            ItemCountBefore = itemCountBefore;
            InventoryBefore = inventoryBefore;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public string LocationId { get; }
        public Debris Debris { get; }
        public Chunk Chunk { get; }
        public string QualifiedItemId { get; }
        public int DebrisCountBefore { get; }
        public int ChunkCountBefore { get; }
        public int ItemCountBefore { get; }
        public string InventoryBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public List<Point> Path { get; set; } = new();
        public int PathIndex { get; set; }
        public Point PathTarget { get; set; }
        public int PathFailures { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int WaitAtTargetTicks { get; set; }
    }

    private sealed class ActiveDescendLadder
    {
        public ActiveDescendLadder(PendingExecution pending, MineShaft mineBefore, int mineLevelBefore, Point target, List<Point> path, string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            Target = target;
            Path = path;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool ActionIssued { get; set; }
    }

    private sealed class ActiveDescendShaft
    {
        public ActiveDescendShaft(
            PendingExecution pending,
            MineShaft mineBefore,
            int mineLevelBefore,
            int healthBefore,
            Point target,
            List<Point> path,
            int expectedMineLevelDelta,
            int expectedMineLevelAfter,
            int expectedHealthCost,
            int expectedHealthAfter,
            string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            HealthBefore = healthBefore;
            Target = target;
            Path = path;
            ExpectedMineLevelDelta = expectedMineLevelDelta;
            ExpectedMineLevelAfter = expectedMineLevelAfter;
            ExpectedHealthCost = expectedHealthCost;
            ExpectedHealthAfter = expectedHealthAfter;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public int HealthBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public int ExpectedMineLevelDelta { get; }
        public int ExpectedMineLevelAfter { get; }
        public int ExpectedHealthCost { get; }
        public int ExpectedHealthAfter { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool PromptOpened { get; set; }
        public bool DialogueConfirmed { get; set; }
    }

    private sealed class ActiveExitMine
    {
        public ActiveExitMine(
            PendingExecution pending,
            MineShaft mineBefore,
            int mineLevelBefore,
            int timeBefore,
            int healthBefore,
            float energyBefore,
            Point playerTileBefore,
            Point target,
            List<Point> path,
            string expectedLocationId,
            int expectedTileX,
            int expectedTileY,
            string retreatReason,
            string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            TimeBefore = timeBefore;
            HealthBefore = healthBefore;
            EnergyBefore = energyBefore;
            PlayerTileBefore = playerTileBefore;
            Target = target;
            Path = path;
            ExpectedLocationId = expectedLocationId;
            ExpectedTileX = expectedTileX;
            ExpectedTileY = expectedTileY;
            RetreatReason = retreatReason;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public int TimeBefore { get; }
        public int HealthBefore { get; }
        public float EnergyBefore { get; }
        public Point PlayerTileBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public string ExpectedLocationId { get; }
        public int ExpectedTileX { get; }
        public int ExpectedTileY { get; }
        public string RetreatReason { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool PromptOpened { get; set; }
        public bool DialogueConfirmed { get; set; }
    }

    private enum ConsumeFoodStage
    {
        PressUse,
        ReleaseUse,
        WaitForPrompt,
        ConfirmPrompt,
        WaitForCompletion
    }

    private sealed class ActiveMineSetup
    {
        public ActiveMineSetup(PendingExecution pending, int mineLevel, string beforeLocation)
        {
            Pending = pending;
            MineLevel = mineLevel;
            BeforeLocation = beforeLocation;
        }

        public PendingExecution Pending { get; }
        public int MineLevel { get; }
        public string BeforeLocation { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 600;
    }

    private sealed record MineFishingFixtureFacts(MineFishingFixtureSnapshot Before, MineFishingFixtureSnapshot After);

    private sealed record MineFishingFixtureSnapshot(
        int BackpackMaxItems,
        int BackpackEmptySlots,
        int SelectedRodSlot,
        string SelectedRodQualifiedItemId,
        int SelectedRodUpgradeLevel,
        int SelectedRodAttachmentSlots,
        string SpecificBaitTargetItemId,
        string BaitInternalName,
        bool LavaEelNativeNameCondition,
        bool CuriosityLureEquipped,
        bool CorkBobberEquipped,
        float Stamina);

    private sealed class ActiveSleep
    {
        public ActiveSleep(PendingExecution pending, Point startTile, Point bedTile, Point standTile, List<Point> path, int startYear, int startDay, int startTime, string startSeason)
        {
            Pending = pending;
            StartTile = startTile;
            BedTile = bedTile;
            StandTile = standTile;
            Path = path;
            StartYear = startYear;
            StartDay = startDay;
            StartTime = startTime;
            StartSeason = startSeason;
            MaxTicks = Math.Max(600, path.Count * 90 + 600);
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public Point StartTile { get; }
        public Point BedTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int StartYear { get; }
        public int StartDay { get; }
        public int StartTime { get; }
        public string StartSeason { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public SleepStage Stage { get; set; } = SleepStage.MoveToStand;
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int PromptWaitTicks { get; set; }
        public int PostSleepWaitTicks { get; set; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public ShipSummaryClosePhase SummaryPhase { get; set; }
        public int SummaryPhaseStartTick { get; set; }
        public bool SummaryPositionSet { get; set; }
        public bool SummaryPositionVerified { get; set; }
        public Point SummaryPositionTarget { get; set; }
        public bool SummaryButtonPressed { get; set; }
        public bool SummaryButtonReleased { get; set; }
        public int SummaryReleaseRetries { get; set; }
    }

    private enum ShipSummaryClosePhase
    {
        WaitReady,
        Position,
        PositionVerify,
        Press,
        Release,
        WaitClose
    }

    private enum SleepStage
    {
        MoveToStand,
        StepOntoSleepTouchTile,
        TriggerPrompt,
        ConfirmPrompt,
        WaitForNewDay,
        WaitForPostSleepStable
    }

    private sealed class SleepTarget
    {
        public SleepTarget(Point bedTile, Point standTile)
        {
            BedTile = bedTile;
            StandTile = standTile;
        }

        public Point BedTile { get; }
        public Point StandTile { get; }
    }

    private sealed class ActiveWait
    {
        public ActiveWait(PendingExecution pending, int targetTicks)
        {
            Pending = pending;
            TargetTicks = targetTicks;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public int TargetTicks { get; }
        public int ElapsedTicks { get; set; }
        public string StartedAt { get; }
    }

    private enum ShipPhase
    {
        BinPosition,
        BinPositionVerify,
        BinPress,
        BinRelease,
        WaitForShippingMenu,
        SlotPosition,
        SlotPositionVerify,
        SlotPress,
        SlotRelease,
        WaitForSlotDispatch,
        VerifyAndClose
    }

    private enum DialogueAdvanceStage
    {
        WaitTransition,
        Press,
        ReleaseAfterAdvance,
        WaitAdvanceEffect,
        CheckClose
    }

    private sealed class ActiveDialogueAdvance
    {
        public ActiveDialogueAdvance(PendingExecution pending, DialogueBox initialMenu)
        {
            Pending = pending;
            InitialMenu = initialMenu;
            InitialSpeakerName = initialMenu.characterDialogue?.speaker?.Name ?? string.Empty;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            MaxTicks = 600;
            MaxPressAttempts = 60;
            BeforeMenuType = "DialogueBox";
            BeforeIsQuestion = initialMenu.isQuestion;
            BeforeResponseCount = initialMenu.responses?.Length ?? 0;
            BeforeSpeakerName = InitialSpeakerName;
            BeforeEventUp = Game1.eventUp;
        }

        public PendingExecution Pending { get; }
        public DialogueBox InitialMenu { get; }
        public string InitialSpeakerName { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public int MaxPressAttempts { get; }
        public int PressAttempts { get; set; }
        public int AdvanceWaitTicks { get; set; }
        public int TransitionWaitTicks { get; set; }
        public int CheckCloseTicks { get; set; }
        public DialogueAdvanceStage Stage { get; set; } = DialogueAdvanceStage.WaitTransition;
        public bool SawDialogueFinishedBeforePress { get; set; }
        public bool SawShowTypingBeforePress { get; set; }
        public bool SawTransitioningBeforePress { get; set; }
        public string BeforeMenuType { get; }
        public bool BeforeIsQuestion { get; }
        public int BeforeResponseCount { get; }
        public string BeforeSpeakerName { get; }
        public bool BeforeEventUp { get; }
    }

    private sealed class ActiveShipInventoryToBin
    {
        public ActiveShipInventoryToBin(PendingExecution pending, ShippingBin bin, int slotIndex,
            string qualifiedItemId, string unqualifiedItemId, int quantity,
            int inventoryCountBefore, int binCountBefore, int binTotalCountBefore,
            int binDistinctCountBefore, string binSignatureBefore, int basicShippedCountBefore,
            string beforeSlotQualifiedId, int beforeSlotStack, string beforeSlotItemId)
        {
            Pending = pending;
            Bin = bin;
            SlotIndex = slotIndex;
            QualifiedItemId = qualifiedItemId;
            UnqualifiedItemId = unqualifiedItemId;
            Quantity = quantity;
            InventoryCountBefore = inventoryCountBefore;
            BinCountBefore = binCountBefore;
            BinTotalCountBefore = binTotalCountBefore;
            BinDistinctCountBefore = binDistinctCountBefore;
            BinSignatureBefore = binSignatureBefore;
            BasicShippedCountBefore = basicShippedCountBefore;
            BeforeSlotQualifiedId = beforeSlotQualifiedId;
            BeforeSlotStack = beforeSlotStack;
            BeforeSlotItemId = beforeSlotItemId;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            Phase = ShipPhase.BinPosition;
            PhaseStartTick = 0;
        }

        public PendingExecution Pending { get; }
        public ShippingBin Bin { get; }
        public int SlotIndex { get; }
        public string QualifiedItemId { get; }
        public string UnqualifiedItemId { get; }
        public int Quantity { get; }
        public int InventoryCountBefore { get; }
        public int BinCountBefore { get; }
        public int BinTotalCountBefore { get; }
        public int BinDistinctCountBefore { get; }
        public string BinSignatureBefore { get; }
        public int BasicShippedCountBefore { get; }
        public string BeforeSlotQualifiedId { get; }
        public int BeforeSlotStack { get; }
        public string BeforeSlotItemId { get; }
        public string StartedAt { get; }
        public ShipPhase Phase { get; set; }
        public int PhaseStartTick { get; set; }
        public bool PositionSet { get; set; }
        public bool PositionVerified { get; set; }
        public Point PositionTarget { get; set; }
        public bool ButtonPressed { get; set; }
        public bool ButtonReleased { get; set; }
        public int ReleaseRetries { get; set; }
        public bool SawShippingMenu { get; set; }
        public bool SlotClickDispatched { get; set; }
        public bool SawShipDispatch { get; set; }
        public int AfterSlotStack { get; set; }
        public string AfterSlotQualifiedId { get; set; } = string.Empty;
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 300;
    }

    private sealed class ShippingReceipt
    {
        [System.Text.Json.Serialization.JsonPropertyName("receipt_id")]
        public string ReceiptId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [System.Text.Json.Serialization.JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("feedback_appended")]
        public bool FeedbackAppended { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("unqualified_item_id")]
        public string UnqualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source_date")]
        public string SourceDate { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_season")]
        public string SourceSeason { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_day_of_month")]
        public string SourceDayOfMonth { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_year")]
        public string SourceYear { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_basic_shipped_count")]
        public int PreBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_inventory_count")]
        public int PreInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_count")]
        public int PreBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_total")]
        public int PreBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_distinct")]
        public int PreBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_signature")]
        public string PreBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_stack")]
        public int PreSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_qualified_id")]
        public string PreSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_inventory_count")]
        public int AfterInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_count")]
        public int AfterBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_total")]
        public int AfterBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_distinct")]
        public int AfterBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_signature")]
        public string AfterBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_stack")]
        public int AfterSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_qualified_id")]
        public string AfterSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("slot_index")]
        public int SlotIndex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_at")]
        public string? SettledAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_status")]
        public string? SettlementStatus { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_reason")]
        public string? SettlementReason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_basic_shipped_count")]
        public int? SettledBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_game_date")]
        public string? SettledGameDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_season")]
        public string? SettledSeason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_day_of_month")]
        public string? SettledDayOfMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_year")]
        public string? SettledYear { get; set; }
    }
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
