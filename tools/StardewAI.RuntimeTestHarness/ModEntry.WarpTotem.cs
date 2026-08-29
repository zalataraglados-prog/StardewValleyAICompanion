using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeWarpTotemNativeContract =
        "Object.performUseAction((O)261|688|689|690|886)->2000ms_totem_animation->Object.totemWarp->1000ms_fadeAfterDelay->Object.totemWarpForReal->Farm_WarpTotemEntry_or_variant_destination->Game1.warpFarmer->active_or_passive_festival_routing";

    private static readonly IReadOnlyDictionary<string, RuntimeWarpTotemVariant> RuntimeWarpTotemVariants =
        new Dictionary<string, RuntimeWarpTotemVariant>(StringComparer.Ordinal)
        {
            ["688"] = new("Farm", 48, 7, "LimeGreen"),
            ["689"] = new("Mountain", 31, 20, "OrangeRed"),
            ["690"] = new("Beach", 20, 4, "LightBlue"),
            ["261"] = new("Desert", 35, 43, "255,200,0,255"),
            ["886"] = new("IslandSouth", 11, 11, "LightBlue")
        };

    private sealed class ActiveWarpTotem
    {
        public ActiveWarpTotem(PendingExecution pending, int slot, NativeInventoryObjectUseResult nativeUse,
            string sourceLocationId, Point sourceTile, RuntimeWarpTotemRoute route,
            int immediateSpriteDelta, bool immediateTransitionVerified, string startedAt)
        {
            Pending = pending;
            Slot = slot;
            NativeUse = nativeUse;
            SourceLocationId = sourceLocationId;
            SourceTile = sourceTile;
            Route = route;
            ImmediateSpriteDelta = immediateSpriteDelta;
            ImmediateTransitionVerified = immediateTransitionVerified;
            StartedAt = startedAt;
        }

        public PendingExecution Pending { get; }
        public int Slot { get; }
        public NativeInventoryObjectUseResult NativeUse { get; }
        public string SourceLocationId { get; }
        public Point SourceTile { get; }
        public RuntimeWarpTotemRoute Route { get; }
        public int ImmediateSpriteDelta { get; }
        public bool ImmediateTransitionVerified { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }

    private void StartUseWarpTotem(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "destination=" + request.WarpTotemEffectiveDestinationLocationId + ":" +
            request.WarpTotemEffectiveDestinationTileX + "," + request.WarpTotemEffectiveDestinationTileY +
            ";inventory_stack=" + request.ExpectedStackAfter;
        if (!request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue ||
            request.ExpectedStackAfter != request.ExpectedStackBefore - 1 ||
            string.IsNullOrWhiteSpace(request.WarpTotemProjectionFingerprint) ||
            !WarpTotemRequestContractIsExact(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_warp_totem", requested,
                "typed_contract=missing_or_invalid", "use_warp_totem_typed_fields_required"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var location = Game1.currentLocation;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            Game1.activeClickableMenu is not null || !Game1.player.canMove || Game1.eventUp || Game1.isFestival() ||
            Game1.fadeToBlack || Game1.player.swimming.Value || Game1.player.bathingClothes.Value ||
            Game1.player.onBridge.Value || slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object totem || totem.GetType() != typeof(StardewValley.Object) ||
            totem.isTemporarilyInvisible || totem.Stack != request.ExpectedStackBefore ||
            !string.Equals(totem.ItemId, request.ItemId, StringComparison.Ordinal) ||
            !string.Equals(totem.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !RuntimeWarpTotemVariants.ContainsKey(totem.ItemId) ||
            !Game1.objectData.TryGetValue(totem.ItemId, out var objectData) ||
            !objectData.Name.Contains("Totem", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_warp_totem", requested,
                WarpTotemObservedEffect(slot), "use_warp_totem_location_gate_or_inventory_drift"));
            return;
        }

        var route = ResolveRuntimeWarpTotemRoute(Game1.player, totem.ItemId);
        if (!route.RouteComplete || route.FestivalPrestartWarpCancelled || route.FestivalReadyCheckRequired ||
            route.AlreadyAtExactDestination || !WarpTotemRouteMatchesRequest(route, request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_warp_totem", requested,
                WarpTotemObservedEffect(slot) + ";route=" + route.ToObservedString(),
                route.FestivalPrestartWarpCancelled ? "use_warp_totem_festival_not_started_consumption_without_warp" :
                route.FestivalReadyCheckRequired ? "use_warp_totem_multiplayer_festival_ready_check_required" :
                route.AlreadyAtExactDestination ? "use_warp_totem_already_at_exact_destination" :
                "use_warp_totem_destination_route_drifted"));
            return;
        }

        var sourceLocation = location.NameOrUniqueName;
        var sourceTile = Game1.player.TilePoint;
        var spritesBefore = location.temporarySprites.Count;
        var nativeUse = UseInventoryObjectNative(totem, slot);
        var spriteDelta = location.temporarySprites.Count - spritesBefore;
        var immediateVerified = nativeUse.Used && nativeUse.StackBefore == request.ExpectedStackBefore &&
            nativeUse.StackAfter == request.ExpectedStackAfter && Game1.player.FacingDirection == 2 &&
            !Game1.player.CanMove && Game1.player.temporarilyInvincible &&
            Game1.player.temporaryInvincibilityTimer == -4000 && spriteDelta >= 68;
        if (!nativeUse.Used || nativeUse.StackAfter != request.ExpectedStackAfter)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_warp_totem", requested,
                WarpTotemObservedEffect(slot), "use_warp_totem_native_use_or_consumption_rejected"));
            return;
        }
        activeWarpTotem = new ActiveWarpTotem(pending, slot, nativeUse, sourceLocation, sourceTile, route,
            spriteDelta, immediateVerified, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static bool WarpTotemRequestContractIsExact(TrainingExecutionRequest request)
    {
        if (!RuntimeWarpTotemVariants.TryGetValue(request.ItemId, out var variant))
            return false;
        return string.Equals(request.QualifiedItemId, "(O)" + request.ItemId, StringComparison.Ordinal) &&
            string.Equals(request.WarpTotemBaseDestinationLocationId, variant.DestinationLocation, StringComparison.Ordinal) &&
            request.WarpTotemRequestedDestinationTileX.HasValue && request.WarpTotemRequestedDestinationTileY.HasValue &&
            !string.IsNullOrWhiteSpace(request.WarpTotemEffectiveDestinationLocationId) &&
            request.WarpTotemEffectiveDestinationTileX.HasValue && request.WarpTotemEffectiveDestinationTileY.HasValue &&
            new[] { "ordinary", "passive_festival_replacement", "active_festival_entry" }
                .Contains(request.WarpTotemDestinationRouteMode, StringComparer.Ordinal) &&
            request.WarpTotemFestivalPrestartWarpCancelled == false &&
            request.WarpTotemFestivalReadyCheckRequired == false &&
            request.WarpTotemFacingDirection == 2 && request.WarpTotemAnimationDurationMs == 2000 &&
            request.WarpTotemCallbackDelayMs == 1000 && request.WarpTotemInitialItemSpriteCount == 3 &&
            request.WarpTotemSprinkleSpriteCount == 65 && request.WarpTotemPoofSpriteCount == 12 &&
            request.WarpTotemTrailSpriteCount == 17 &&
            string.Equals(request.WarpTotemInitialSound, "warrior", StringComparison.Ordinal) &&
            string.Equals(request.WarpTotemWarpSound, "wand", StringComparison.Ordinal) &&
            string.Equals(request.WarpTotemGlowColorRgba, variant.GlowColor, StringComparison.Ordinal) &&
            string.Equals(request.NativeContract, RuntimeWarpTotemNativeContract, StringComparison.Ordinal);
    }

    private static bool WarpTotemRouteMatchesRequest(RuntimeWarpTotemRoute route, TrainingExecutionRequest request)
    {
        return string.Equals(route.BaseDestinationLocationId, request.WarpTotemBaseDestinationLocationId, StringComparison.Ordinal) &&
            route.RequestedDestinationTileX == request.WarpTotemRequestedDestinationTileX &&
            route.RequestedDestinationTileY == request.WarpTotemRequestedDestinationTileY &&
            string.Equals(route.EffectiveDestinationLocationId, request.WarpTotemEffectiveDestinationLocationId, StringComparison.Ordinal) &&
            route.EffectiveDestinationTileX == request.WarpTotemEffectiveDestinationTileX &&
            route.EffectiveDestinationTileY == request.WarpTotemEffectiveDestinationTileY &&
            string.Equals(route.DestinationRouteMode, request.WarpTotemDestinationRouteMode, StringComparison.Ordinal) &&
            string.Equals(route.FarmDestinationSource, request.WarpTotemFarmDestinationSource, StringComparison.Ordinal) &&
            string.Equals(route.PassiveFestivalRouteJson, request.WarpTotemPassiveFestivalRouteJson, StringComparison.Ordinal) &&
            string.Equals(route.ActiveFestivalId, request.WarpTotemActiveFestivalId, StringComparison.Ordinal) &&
            route.ActiveFestivalStartTime == request.WarpTotemActiveFestivalStartTime &&
            route.ActiveFestivalEndTime == request.WarpTotemActiveFestivalEndTime &&
            route.ActiveFestivalEntryTileX == request.WarpTotemActiveFestivalEntryTileX &&
            route.ActiveFestivalEntryTileY == request.WarpTotemActiveFestivalEntryTileY &&
            route.ActiveFestivalEntryFacing == request.WarpTotemActiveFestivalEntryFacing &&
            route.FestivalPrestartWarpCancelled == request.WarpTotemFestivalPrestartWarpCancelled &&
            route.FestivalReadyCheckRequired == request.WarpTotemFestivalReadyCheckRequired;
    }

    private void TickWarpTotem()
    {
        var active = activeWarpTotem;
        if (active is null)
            return;
        active.ElapsedTicks++;
        if (Game1.activeClickableMenu is not null)
        {
            activeWarpTotem = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_warp_totem",
                "destination=" + active.Route.EffectiveDestinationLocationId,
                WarpTotemObservedEffect(active.Slot), "use_warp_totem_unexpected_menu_during_native_warp"));
            return;
        }

        var location = Game1.currentLocation;
        var expectedTile = new Point(active.Route.EffectiveDestinationTileX, active.Route.EffectiveDestinationTileY);
        var destinationReached = location is not null &&
            string.Equals(location.NameOrUniqueName, active.Route.EffectiveDestinationLocationId, StringComparison.Ordinal) &&
            Game1.player.TilePoint == expectedTile &&
            (!string.Equals(active.Route.DestinationRouteMode, "active_festival_entry", StringComparison.Ordinal) || Game1.isFestival());
        var settled = Game1.displayFarmer && !Game1.player.temporarilyInvincible &&
            Game1.player.temporaryInvincibilityTimer == 0 && Game1.player.CanMove && !Game1.fadeToBlack;
        if (destinationReached && settled)
        {
            CompleteWarpTotem(active);
            return;
        }
        if (active.ElapsedTicks <= 600)
            return;
        activeWarpTotem = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_warp_totem",
            "destination=" + active.Route.EffectiveDestinationLocationId + ":" + expectedTile.X + "," + expectedTile.Y,
            WarpTotemObservedEffect(active.Slot) + ";elapsed_ticks=" + active.ElapsedTicks,
            "use_warp_totem_native_delayed_warp_timeout"));
    }

    private void CompleteWarpTotem(ActiveWarpTotem active)
    {
        var request = active.Pending.Request;
        var location = Game1.currentLocation;
        var item = active.Slot >= 0 && active.Slot < Game1.player.Items.Count ? Game1.player.Items[active.Slot] : null;
        var inventoryVerified = active.NativeUse.StackBefore == request.ExpectedStackBefore &&
            active.NativeUse.StackAfter == request.ExpectedStackAfter &&
            (request.ExpectedStackAfter == 0
                ? item?.QualifiedItemId != request.QualifiedItemId
                : item?.QualifiedItemId == request.QualifiedItemId && item.Stack == request.ExpectedStackAfter);
        var destinationVerified = location is not null &&
            string.Equals(location.NameOrUniqueName, active.Route.EffectiveDestinationLocationId, StringComparison.Ordinal) &&
            Game1.player.TilePoint.X == active.Route.EffectiveDestinationTileX &&
            Game1.player.TilePoint.Y == active.Route.EffectiveDestinationTileY;
        var expectedFacing = string.Equals(active.Route.DestinationRouteMode, "active_festival_entry", StringComparison.Ordinal)
            ? active.Route.ActiveFestivalEntryFacing
            : 2;
        var stateVerified = Game1.displayFarmer && !Game1.player.temporarilyInvincible &&
            Game1.player.temporaryInvincibilityTimer == 0 && Game1.player.CanMove &&
            Game1.player.FacingDirection == expectedFacing;
        var festivalVerified = !string.Equals(active.Route.DestinationRouteMode, "active_festival_entry", StringComparison.Ordinal) ||
            Game1.isFestival();
        var verified = active.ImmediateTransitionVerified && inventoryVerified && destinationVerified &&
            stateVerified && festivalVerified;
        activeWarpTotem = null;

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_warp_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Object_performUseAction_succeeded", "exactly_one_selected_warp_totem_consumed",
                    "native_2000ms_animation_and_1000ms_callback_observed",
                    "native_destination_and_festival_route_verified", "native_player_state_restored"
                }
                : new[]
                {
                    active.ImmediateTransitionVerified ? "immediate_transition_verified" : "immediate_transition_mismatch",
                    inventoryVerified ? "inventory_stack_verified" : "inventory_stack_mismatch",
                    destinationVerified ? "destination_verified" : "destination_mismatch",
                    stateVerified ? "player_state_verified" : "player_state_mismatch",
                    festivalVerified ? "festival_route_verified" : "festival_route_mismatch"
                },
            RequestedEffect = "destination=" + active.Route.EffectiveDestinationLocationId + ":" +
                active.Route.EffectiveDestinationTileX + "," + active.Route.EffectiveDestinationTileY +
                ";route_mode=" + active.Route.DestinationRouteMode + ";inventory_stack=" + request.ExpectedStackAfter,
            ObservedEffect = WarpTotemObservedEffect(active.Slot) + ";source_location=" + active.SourceLocationId +
                ";source_tile=" + active.SourceTile.X + "," + active.SourceTile.Y +
                ";immediate_sprite_delta=" + active.ImmediateSpriteDelta + ";elapsed_ticks=" + active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_warp_totem_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.inventory[" + active.Slot + "]", Before = request.QualifiedItemId + "x" + request.ExpectedStackBefore, After = request.QualifiedItemId + "x" + request.ExpectedStackAfter },
                    new SimulatedFactChange { Path = "player.location_id", Before = active.SourceLocationId, After = active.Route.EffectiveDestinationLocationId },
                    new SimulatedFactChange { Path = "player.tile", Before = active.SourceTile.X + "," + active.SourceTile.Y, After = active.Route.EffectiveDestinationTileX + "," + active.Route.EffectiveDestinationTileY }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static RuntimeWarpTotemRoute ResolveRuntimeWarpTotemRoute(Farmer player, string itemId)
    {
        if (!RuntimeWarpTotemVariants.TryGetValue(itemId, out var variant))
            return RuntimeWarpTotemRoute.Unavailable;
        var baseDestination = variant.DestinationLocation;
        var requestedX = variant.TileX;
        var requestedY = variant.TileY;
        var farmSource = "fixed_variant";
        if (itemId == "688")
        {
            if (Game1.getFarm().TryGetMapPropertyAs("WarpTotemEntry", out Point parsed, required: false))
            {
                requestedX = parsed.X;
                requestedY = parsed.Y;
                farmSource = "map_property_WarpTotemEntry";
            }
            else
            {
                (requestedX, requestedY, farmSource) = Game1.whichFarm switch
                {
                    6 => (82, 29, "fallback_beach_farm"),
                    5 => (48, 39, "fallback_four_corners_farm"),
                    _ => (48, 7, "fallback_default")
                };
            }
        }

        var effectiveDestination = baseDestination;
        var passiveRows = new List<RuntimeWarpTotemPassiveRoute>();
        foreach (var festivalId in Game1.netWorldState.Value.ActivePassiveFestivals)
        {
            if (!Utility.TryGetPassiveFestivalData(festivalId, out var data) || data is null ||
                Game1.dayOfMonth < data.StartDay || Game1.dayOfMonth > data.EndDay || data.Season != Game1.season ||
                data.MapReplacements is null || !data.MapReplacements.TryGetValue(effectiveDestination, out var replacement))
                continue;
            passiveRows.Add(new RuntimeWarpTotemPassiveRoute(festivalId, effectiveDestination, replacement));
            effectiveDestination = replacement;
        }
        var passiveJson = JsonSerializer.Serialize(passiveRows);
        var routeMode = passiveRows.Count > 0 ? "passive_festival_replacement" : "ordinary";
        var festival = ReadRuntimeWarpTotemFestival(baseDestination);
        if (!festival.RouteComplete)
            return RuntimeWarpTotemRoute.Unavailable;
        var prestartCancelled = festival.TargetsDestination && Game1.timeOfDay < festival.StartTime;
        var festivalWindow = festival.TargetsDestination && Game1.timeOfDay >= festival.StartTime &&
            Game1.timeOfDay <= festival.EndTime;
        var readyCheck = festivalWindow && Game1.IsMultiplayer;
        var effectiveX = requestedX;
        var effectiveY = requestedY;
        if (festivalWindow && !readyCheck)
        {
            routeMode = "active_festival_entry";
            effectiveDestination = baseDestination;
            effectiveX = festival.EntryTileX;
            effectiveY = festival.EntryTileY;
        }
        else
        {
            var target = Game1.getLocationFromName(effectiveDestination);
            if (target?.Map?.Layers.Count > 0 && effectiveX >= target.Map.Layers[0].LayerWidth - 1)
                effectiveX--;
        }
        var destinationAvailable = festivalWindow || Game1.getLocationFromName(effectiveDestination) is not null;
        var alreadyThere = !festivalWindow && destinationAvailable &&
            string.Equals(player.currentLocation?.NameOrUniqueName, effectiveDestination, StringComparison.Ordinal) &&
            player.TilePoint.X == effectiveX && player.TilePoint.Y == effectiveY;
        return new RuntimeWarpTotemRoute(true, baseDestination, requestedX, requestedY, effectiveDestination,
            effectiveX, effectiveY, routeMode, farmSource, passiveJson, festival.FestivalId,
            festival.StartTime, festival.EndTime, festival.EntryTileX, festival.EntryTileY,
            festival.EntryFacing, prestartCancelled, readyCheck, alreadyThere);
    }

    private static RuntimeWarpTotemFestival ReadRuntimeWarpTotemFestival(string destinationLocation)
    {
        if (!Utility.isFestivalDay())
            return RuntimeWarpTotemFestival.None;
        try
        {
            var id = Game1.season.ToString().ToLowerInvariant() + Game1.dayOfMonth;
            var data = Game1.temporaryContent.Load<Dictionary<string, string>>("Data\\Festivals\\" + id);
            if (!data.TryGetValue("conditions", out var conditions))
                return RuntimeWarpTotemFestival.Unavailable;
            var parts = conditions.Split('/');
            if (parts.Length < 2)
                return RuntimeWarpTotemFestival.Unavailable;
            var times = ArgUtility.SplitBySpace(parts[1]);
            if (times.Length < 2 || !int.TryParse(times[0], out var start) || !int.TryParse(times[1], out var end))
                return RuntimeWarpTotemFestival.Unavailable;
            var entryX = -1;
            var entryY = -1;
            var entryFacing = -1;
            if (data.TryGetValue("set-up", out var setup))
            {
                foreach (var command in setup.Split('/').Select(value => ArgUtility.SplitBySpace(value)))
                {
                    if (command.Length >= 4 && string.Equals(command[0], "farmer", StringComparison.Ordinal) &&
                        int.TryParse(command[1], out entryX) && int.TryParse(command[2], out entryY) &&
                        int.TryParse(command[3], out entryFacing))
                        break;
                }
            }
            var targets = string.Equals(parts[0], destinationLocation, StringComparison.Ordinal);
            if (targets && (entryX < 0 || entryY < 0 || entryFacing < 0))
                return RuntimeWarpTotemFestival.Unavailable;
            return new RuntimeWarpTotemFestival(true, id, start, end, entryX, entryY, entryFacing, targets);
        }
        catch
        {
            return RuntimeWarpTotemFestival.Unavailable;
        }
    }

    private static string WarpTotemObservedEffect(int slot)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        return "slot=" + slot + ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) + ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "unavailable") +
            ";tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";facing=" + Game1.player.FacingDirection + ";display_farmer=" + Game1.displayFarmer.ToString().ToLowerInvariant() +
            ";invincible=" + Game1.player.temporarilyInvincible.ToString().ToLowerInvariant() +
            ";can_move=" + Game1.player.CanMove.ToString().ToLowerInvariant() +
            ";festival_event_active=" + Game1.isFestival().ToString().ToLowerInvariant();
    }

    private sealed record RuntimeWarpTotemVariant(string DestinationLocation, int TileX, int TileY, string GlowColor);
    private sealed record RuntimeWarpTotemPassiveRoute(
        string festival_id,
        string source_location_id,
        string replacement_location_id);
    private sealed record RuntimeWarpTotemFestival(
        bool RouteComplete,
        string FestivalId,
        int StartTime,
        int EndTime,
        int EntryTileX,
        int EntryTileY,
        int EntryFacing,
        bool TargetsDestination)
    {
        public static RuntimeWarpTotemFestival None { get; } = new(true, string.Empty, -1, -1, -1, -1, -1, false);
        public static RuntimeWarpTotemFestival Unavailable { get; } = new(false, string.Empty, -1, -1, -1, -1, -1, false);
    }
    private sealed record RuntimeWarpTotemRoute(
        bool RouteComplete,
        string BaseDestinationLocationId,
        int RequestedDestinationTileX,
        int RequestedDestinationTileY,
        string EffectiveDestinationLocationId,
        int EffectiveDestinationTileX,
        int EffectiveDestinationTileY,
        string DestinationRouteMode,
        string FarmDestinationSource,
        string PassiveFestivalRouteJson,
        string ActiveFestivalId,
        int ActiveFestivalStartTime,
        int ActiveFestivalEndTime,
        int ActiveFestivalEntryTileX,
        int ActiveFestivalEntryTileY,
        int ActiveFestivalEntryFacing,
        bool FestivalPrestartWarpCancelled,
        bool FestivalReadyCheckRequired,
        bool AlreadyAtExactDestination)
    {
        public static RuntimeWarpTotemRoute Unavailable { get; } = new(false, string.Empty, -1, -1,
            string.Empty, -1, -1, string.Empty, string.Empty, "[]", string.Empty, -1, -1, -1, -1, -1,
            false, false, false);

        public string ToObservedString() => BaseDestinationLocationId + "->" + EffectiveDestinationLocationId + ":" +
            EffectiveDestinationTileX + "," + EffectiveDestinationTileY + ";mode=" + DestinationRouteMode;
    }
}
