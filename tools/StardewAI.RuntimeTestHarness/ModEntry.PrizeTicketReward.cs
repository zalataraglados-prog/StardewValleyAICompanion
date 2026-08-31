using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string PrizeTicketRewardRuntimeNativeContract =
        "Town.SpecialOrdersPrizeTickets->inventory_PrizeTicket_and_pending_stat_minus_one;ManorHouse.PrizeMachine->PrizeTicketMenu.currentPrizeTrack[0]->inventory_else_debris->PrizeTicket_minus_one->ticketPrizesClaimed_plus_one";

    private void StartPrizeTicketReward(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = new List<string>();
        if (request.PrizeTicketStage is not ("collect_pending_ticket" or "redeem_prize") ||
            request.PrizeTicketProjectionFingerprint.Length != 64 ||
            request.PrizeTicketCurrentRewardFingerprint.Length != 64 ||
            !request.PrizeTicketInventoryCountBefore.HasValue || !request.PrizeTicketPendingCountBefore.HasValue ||
            !request.PrizeTicketClaimedCountBefore.HasValue || !request.PrizeTicketPrizeLevel.HasValue ||
            !request.PrizeTicketRewardStack.HasValue || !request.PrizeTicketRewardQuality.HasValue ||
            !request.PrizeTicketInventoryMaxItems.HasValue || !request.PrizeTicketInventoryOccupiedSlots.HasValue ||
            !request.PrizeTicketPendingCapacitySufficient.HasValue ||
            string.IsNullOrWhiteSpace(request.PrizeTicketRewardQualifiedItemId) ||
            string.IsNullOrWhiteSpace(request.PrizeTicketRewardRuntimeType) ||
            request.NativeContract != PrizeTicketRewardRuntimeNativeContract)
            reasons.Add("prize_ticket_reward_complete_typed_request_required");
        if (!TryParsePrizeTicketPreview(request.PrizeTicketPreviewJson, out var expectedPreview) || expectedPreview.Length != 4)
            reasons.Add("prize_ticket_reward_exact_four_item_preview_required");
        var live = ReadLivePrizeTicketRewardProjection();
        if (live is null || live.ProjectionFingerprint != request.PrizeTicketProjectionFingerprint)
            reasons.Add("prize_ticket_reward_projection_drifted");
        if (live is not null && !PrizeTicketRewardRequestMatches(request, live, expectedPreview))
            reasons.Add("prize_ticket_reward_typed_state_drifted");

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX ?? -1, request.TargetTileY ?? -1);
        var stand = new Point(request.StandTileX ?? -1, request.StandTileY ?? -1);
        if (location is null || !PrizeTicketRewardEndpointMatches(location, target, stand, request.PrizeTicketActionRaw, request.PrizeTicketStage))
            reasons.Add("prize_ticket_reward_native_endpoint_drifted");
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
            reasons.Add("prize_ticket_reward_menu_conflict");
        if (reasons.Count > 0 || live is null || location is null)
        {
            pending.Completion.SetResult(PrizeTicketRewardBlocked(request, reasons.ToArray()));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(PrizeTicketRewardBlocked(request, "prize_ticket_reward_path_unavailable:" + pathReason));
            return;
        }
        activePrizeTicketReward = new ActivePrizeTicketReward(
            pending, location, target, stand, path, maxMovementTiles,
            CountPrizeTicketRewardTotal(location, request.PrizeTicketRewardQualifiedItemId));
    }

    private void TickPrizeTicketRewardSafely()
    {
        var active = activePrizeTicketReward;
        if (active is null) return;
        try
        {
            TickPrizeTicketReward(active);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Prize Ticket reward execution failed and was blocked: {ex}", StardewModdingAPI.LogLevel.Error);
            CompletePrizeTicketReward(active, false, "prize_ticket_reward_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickPrizeTicketReward(ActivePrizeTicketReward active)
    {
        if (active.Stage != PrizeTicketRuntimeStage.Move) active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompletePrizeTicketReward(active, false, "prize_ticket_reward_timeout");
            return;
        }
        if (active.Stage == PrizeTicketRuntimeStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "prize_ticket_reward", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompletePrizeTicketReward(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving) return;
            var request = active.Pending.Request;
            var live = ReadLivePrizeTicketRewardProjection();
            if (live is null || live.ProjectionFingerprint != request.PrizeTicketProjectionFingerprint ||
                !PrizeTicketRewardEndpointMatches(active.Location, active.Target, active.Stand, request.PrizeTicketActionRaw, request.PrizeTicketStage))
            {
                CompletePrizeTicketReward(active, false, "prize_ticket_reward_state_drifted_while_moving");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            if (!active.NativeHandled)
            {
                CompletePrizeTicketReward(active, false, "prize_ticket_reward_native_check_action_rejected");
                return;
            }
            if (request.PrizeTicketStage == "collect_pending_ticket")
            {
                var verified = PrizeTicketPendingCollectionReceipt(request);
                CompletePrizeTicketReward(active, verified,
                    verified ? Array.Empty<string>() : new[] { "prize_ticket_pending_collection_receipt_mismatch" });
                return;
            }
            if (Game1.activeClickableMenu is not PrizeTicketMenu menu ||
                !PrizeTicketTrackMatches(menu.currentPrizeTrack, request.PrizeTicketPreviewJson))
            {
                CompletePrizeTicketReward(active, false, "prize_ticket_reward_native_menu_or_preview_mismatch");
                return;
            }
            active.Menu = menu;
            menu.receiveLeftClick(menu.mainButton.bounds.Center.X, menu.mainButton.bounds.Center.Y, playSound: true);
            if (!menu.gettingReward)
            {
                CompletePrizeTicketReward(active, false, "prize_ticket_reward_native_button_did_not_start_redemption");
                return;
            }
            active.Stage = PrizeTicketRuntimeStage.WaitForSettlement;
            return;
        }

        var executionRequest = active.Pending.Request;
        if (checked((int)Game1.stats.Get("ticketPrizesClaimed")) == executionRequest.PrizeTicketClaimedCountBefore)
            return;
        var verifiedReceipt = PrizeTicketRedemptionReceipt(active);
        if (active.Menu is not null && ReferenceEquals(Game1.activeClickableMenu, active.Menu) && active.Menu.readyToClose())
            Game1.exitActiveMenu();
        CompletePrizeTicketReward(active, verifiedReceipt,
            verifiedReceipt ? Array.Empty<string>() : new[] { "prize_ticket_redemption_receipt_mismatch" });
    }

    private void CompletePrizeTicketReward(ActivePrizeTicketReward active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        if (active.Menu is not null && ReferenceEquals(Game1.activeClickableMenu, active.Menu) && active.Menu.readyToClose())
            Game1.exitActiveMenu();
        activePrizeTicketReward = null;
        var request = active.Pending.Request;
        var inventoryAfter = Game1.player.Items.CountId("PrizeTicket");
        var pendingAfter = checked((int)Game1.player.stats.Get("specialOrderPrizeTickets"));
        var claimedAfter = checked((int)Game1.stats.Get("ticketPrizesClaimed"));
        var rewardAfter = CountPrizeTicketRewardTotal(active.Location, request.PrizeTicketRewardQualifiedItemId);
        var verification = verified
            ? request.PrizeTicketStage == "collect_pending_ticket"
                ? new[] { "shared_BFS_reached_Town_ticket_endpoint", "native_SpecialOrdersPrizeTickets_branch_handled", "one_physical_PrizeTicket_collected", "pending_ticket_stat_decremented", "rolling_reward_objective_preserved" }
                : new[] { "shared_BFS_reached_ManorHouse_PrizeMachine", "native_PrizeTicketMenu_exact_preview_opened", "native_main_button_timer_settled", "one_PrizeTicket_consumed", "ticketPrizesClaimed_incremented", "exact_reward_conserved_in_inventory_or_debris" }
            : reasons.Length == 0 ? new[] { "prize_ticket_reward_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "policy_training",
            PrimitiveKind = "claim_prize_ticket",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = PrizeTicketRewardRequestedEffect(request),
            ObservedEffect = "stage=" + request.PrizeTicketStage + ";inventory_tickets=" + inventoryAfter +
                ";pending_tickets=" + pendingAfter + ";claimed=" + claimedAfter +
                ";reward_total_delta=" + (rewardAfter - active.RewardTotalBefore) +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verification,
            PrizeTicketStage = request.PrizeTicketStage,
            PrizeTicketRewardFingerprint = request.PrizeTicketCurrentRewardFingerprint,
            PrizeTicketInventoryCountAfter = inventoryAfter,
            PrizeTicketPendingCountAfter = pendingAfter,
            PrizeTicketClaimedCountAfter = claimedAfter,
            PrizeTicketRewardTotalDelta = rewardAfter - active.RewardTotalBefore,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory.PrizeTicket.count", Before = request.PrizeTicketInventoryCountBefore?.ToString() ?? "unknown", After = inventoryAfter.ToString() },
                new SimulatedFactChange { Path = "player.stats.specialOrderPrizeTickets", Before = request.PrizeTicketPendingCountBefore?.ToString() ?? "unknown", After = pendingAfter.ToString() },
                new SimulatedFactChange { Path = "player.stats.ticketPrizesClaimed", Before = request.PrizeTicketClaimedCountBefore?.ToString() ?? "unknown", After = claimedAfter.ToString() },
                new SimulatedFactChange { Path = "reward_total[" + request.PrizeTicketRewardQualifiedItemId + "]", Before = active.RewardTotalBefore.ToString(), After = rewardAfter.ToString() }
            }
        });
    }

    private static TrainingExecutionResult PrizeTicketRewardBlocked(TrainingExecutionRequest request, params string[] reasons)
    {
        var result = BlockedWithPrimitive(request, "claim_prize_ticket", PrizeTicketRewardRequestedEffect(request),
            "stage=" + request.PrizeTicketStage + ";status=not_started_or_incomplete", reasons.Distinct(StringComparer.Ordinal).ToArray());
        result.PrizeTicketStage = request.PrizeTicketStage;
        result.PrizeTicketRewardFingerprint = request.PrizeTicketCurrentRewardFingerprint;
        return result;
    }

    private static string PrizeTicketRewardRequestedEffect(TrainingExecutionRequest request) =>
        request.PrizeTicketStage == "collect_pending_ticket"
            ? "specialOrderPrizeTickets-=1;inventory_PrizeTicket+=1;continue_expected_prize_level=" + request.PrizeTicketPrizeLevel
            : "inventory_PrizeTicket-=1;ticketPrizesClaimed+=1;reward_total[" + request.PrizeTicketRewardQualifiedItemId + "]+=" + request.PrizeTicketRewardStack;

    private static bool PrizeTicketRewardRequestMatches(
        TrainingExecutionRequest request,
        PrizeTicketRewardProjectionRef live,
        PrizeTicketRewardItemRef[] expectedPreview)
    {
        var reward = live.CurrentReward;
        return reward is not null && live.Stage == request.PrizeTicketStage &&
            live.TargetLocationId == request.LocationId && live.MenuClear && live.ServiceStatus == "ready" &&
            live.InventoryTicketCount == request.PrizeTicketInventoryCountBefore &&
            live.PendingSpecialOrderTicketCount == request.PrizeTicketPendingCountBefore &&
            live.TicketPrizesClaimed == request.PrizeTicketClaimedCountBefore &&
            live.CurrentPrizeLevel == request.PrizeTicketPrizeLevel &&
            live.CurrentRewardFingerprint == request.PrizeTicketCurrentRewardFingerprint &&
            PrizeTicketRewardIdentity.ComputeRewardFingerprint(reward) == request.PrizeTicketCurrentRewardFingerprint &&
            reward.QualifiedItemId == request.PrizeTicketRewardQualifiedItemId && reward.ItemId == request.PrizeTicketRewardItemId &&
            reward.Stack == request.PrizeTicketRewardStack && reward.Quality == request.PrizeTicketRewardQuality &&
            reward.RuntimeType == request.PrizeTicketRewardRuntimeType &&
            live.InventoryMaxItems == request.PrizeTicketInventoryMaxItems &&
            live.InventoryOccupiedSlots == request.PrizeTicketInventoryOccupiedSlots &&
            live.PendingTicketCapacitySufficient == request.PrizeTicketPendingCapacitySufficient &&
            JsonSerializer.Serialize(live.PreviewTrack) == JsonSerializer.Serialize(expectedPreview);
    }

    private static bool PrizeTicketPendingCollectionReceipt(TrainingExecutionRequest request) =>
        Game1.player.Items.CountId("PrizeTicket") == request.PrizeTicketInventoryCountBefore + 1 &&
        checked((int)Game1.player.stats.Get("specialOrderPrizeTickets")) == request.PrizeTicketPendingCountBefore - 1 &&
        checked((int)Game1.stats.Get("ticketPrizesClaimed")) == request.PrizeTicketClaimedCountBefore;

    private static bool PrizeTicketRedemptionReceipt(ActivePrizeTicketReward active)
    {
        var request = active.Pending.Request;
        var rewardAfter = CountPrizeTicketRewardTotal(active.Location, request.PrizeTicketRewardQualifiedItemId);
        return Game1.player.Items.CountId("PrizeTicket") == request.PrizeTicketInventoryCountBefore - 1 &&
            checked((int)Game1.player.stats.Get("specialOrderPrizeTickets")) == request.PrizeTicketPendingCountBefore &&
            checked((int)Game1.stats.Get("ticketPrizesClaimed")) == request.PrizeTicketClaimedCountBefore + 1 &&
            rewardAfter == active.RewardTotalBefore + request.PrizeTicketRewardStack;
    }

    private static bool PrizeTicketRewardEndpointMatches(
        GameLocation location,
        Point action,
        Point stand,
        string actionRaw,
        string stage)
    {
        var expectedLocation = stage == "redeem_prize" ? "ManorHouse" : "Town";
        var expectedAction = stage == "redeem_prize" ? "PrizeMachine" : "SpecialOrdersPrizeTickets";
        var exactType = stage == "redeem_prize" ? location.GetType() == typeof(ManorHouse) : location.GetType() == typeof(Town);
        return exactType && string.Equals(location.NameOrUniqueName, expectedLocation, StringComparison.OrdinalIgnoreCase) &&
            AreAdjacent(action, stand) && IsTileOnMap(location, stand) && IsTileWalkable(location, stand) &&
            !IsTileOccupiedByCharacter(location, stand) && actionRaw == expectedAction &&
            location.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") == expectedAction;
    }

    private static bool TryParsePrizeTicketPreview(string json, out PrizeTicketRewardItemRef[] preview)
    {
        try
        {
            preview = JsonSerializer.Deserialize<PrizeTicketRewardItemRef[]>(json) ?? Array.Empty<PrizeTicketRewardItemRef>();
            return preview.Length == 4 && preview.All(row => row.Stack > 0 && !string.IsNullOrWhiteSpace(row.QualifiedItemId));
        }
        catch (JsonException)
        {
            preview = Array.Empty<PrizeTicketRewardItemRef>();
            return false;
        }
    }

    private static bool PrizeTicketTrackMatches(List<Item> track, string expectedJson)
    {
        if (!TryParsePrizeTicketPreview(expectedJson, out var expected) || track.Count != 4) return false;
        var actual = track.Select((item, index) => PrizeTicketRewardItem(item, expected[index].PrizeLevel)).ToArray();
        return JsonSerializer.Serialize(actual) == JsonSerializer.Serialize(expected);
    }

    private static PrizeTicketRewardProjectionRef? ReadLivePrizeTicketRewardProjection()
    {
        var player = Game1.player;
        if (player is null) return null;
        var inventoryTickets = player.Items.CountId("PrizeTicket");
        var pendingTickets = checked((int)player.stats.Get("specialOrderPrizeTickets"));
        var claimed = checked((int)Game1.stats.Get("ticketPrizesClaimed"));
        var preview = Enumerable.Range(0, 4).Select(offset =>
            PrizeTicketRewardItem(PrizeTicketMenu.getPrizeItem(claimed + offset), claimed + offset)).ToArray();
        var machineTiles = RuntimePrizeTicketActionTiles("ManorHouse", "PrizeMachine");
        var pendingTiles = RuntimePrizeTicketActionTiles("Town", "SpecialOrdersPrizeTickets");
        var stage = inventoryTickets > 0 ? "redeem_prize" : pendingTickets > 0 ? "collect_pending_ticket" : "none";
        var targetLocation = stage == "redeem_prize" ? "ManorHouse" : stage == "collect_pending_ticket" ? "Town" : string.Empty;
        var targetTiles = stage == "redeem_prize" ? machineTiles : stage == "collect_pending_ticket" ? pendingTiles : Array.Empty<PrizeTicketActionTileRef>();
        var currentMatches = string.Equals(Game1.currentLocation?.NameOrUniqueName, targetLocation, StringComparison.OrdinalIgnoreCase);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var canAccept = player.couldInventoryAcceptThisItem(ItemRegistry.Create("(O)PrizeTicket"));
        var blocked = stage == "none" || targetTiles.Length == 0 || !menuClear || stage == "collect_pending_ticket" && !canAccept;
        var projection = new PrizeTicketRewardProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            NativeContract = PrizeTicketRewardRuntimeNativeContract,
            Stage = stage,
            TargetLocationId = targetLocation,
            CurrentLocationMatches = currentMatches,
            MenuClear = menuClear,
            InventoryTicketCount = inventoryTickets,
            PendingSpecialOrderTicketCount = pendingTickets,
            AvailableTicketCount = inventoryTickets + pendingTickets,
            TicketPrizesClaimed = claimed,
            CurrentPrizeLevel = claimed,
            CurrentReward = preview[0],
            CurrentRewardFingerprint = PrizeTicketRewardIdentity.ComputeRewardFingerprint(preview[0]),
            PreviewTrack = preview,
            PrizeMachineActionTiles = machineTiles,
            SpecialOrderTicketActionTiles = pendingTiles,
            InventoryMaxItems = player.MaxItems,
            InventoryOccupiedSlots = player.Items.Take(player.MaxItems).Count(item => item is not null),
            PendingTicketCapacitySufficient = canAccept,
            GameId = Game1.uniqueIDForThisGame,
            PlayerId = player.UniqueMultiplayerID,
            HouseUpgradeLevel = player.HouseUpgradeLevel,
            Season = Game1.currentSeason,
            DayOfMonth = Game1.dayOfMonth,
            ServiceStatus = blocked ? "blocked" : currentMatches ? "ready" : "route_required"
        };
        projection.ProjectionFingerprint = PrizeTicketRewardIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static PrizeTicketRewardItemRef PrizeTicketRewardItem(Item item, int prizeLevel) => new()
    {
        PrizeLevel = prizeLevel,
        QualifiedItemId = item.QualifiedItemId,
        ItemId = item.ItemId,
        DisplayName = item.DisplayName,
        Stack = item.Stack,
        Quality = item.Quality,
        RuntimeType = item.GetType().FullName ?? string.Empty
    };

    private static PrizeTicketActionTileRef[] RuntimePrizeTicketActionTiles(string locationName, string token)
    {
        var location = Game1.getLocationFromName(locationName);
        var buildings = location?.Map?.GetLayer("Buildings");
        if (location is null || buildings is null) return Array.Empty<PrizeTicketActionTileRef>();
        var rows = new List<PrizeTicketActionTileRef>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (action != token) continue;
            rows.Add(new PrizeTicketActionTileRef { LocationId = location.NameOrUniqueName, TileX = x, TileY = y, ActionRaw = action });
        }
        return rows.OrderBy(row => row.TileY).ThenBy(row => row.TileX).ToArray();
    }

    private static int CountPrizeTicketRewardTotal(GameLocation location, string qualifiedItemId)
    {
        var inventory = CountInventoryItem(qualifiedItemId);
        var debris = location.debris.Where(row => string.Equals(DebrisQualifiedItemId(row), qualifiedItemId, StringComparison.Ordinal))
            .Sum(row => Math.Max(1, row.item?.Stack ?? row.Chunks.Count));
        return inventory + debris;
    }

    private sealed class ActivePrizeTicketReward : INativeObjectInteractionMovement
    {
        public ActivePrizeTicketReward(PendingExecution pending, GameLocation location, Point target, Point stand,
            List<Point> path, int maxMovementTiles, int rewardTotalBefore)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            RewardTotalBefore = rewardTotalBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int RewardTotalBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeHandled { get; set; }
        public PrizeTicketMenu? Menu { get; set; }
        public PrizeTicketRuntimeStage Stage { get; set; }
    }

    private enum PrizeTicketRuntimeStage { Move, WaitForSettlement }
}
