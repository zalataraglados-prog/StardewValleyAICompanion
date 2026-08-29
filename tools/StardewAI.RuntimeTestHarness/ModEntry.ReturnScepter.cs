using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeReturnScepterNativeContract =
        "Farmer.BeginUsingTool->Tool.beginUsing(InstantUse)->Game1.toolAnimationDone->Wand.DoFunction->1000ms_wandWarpForReal->Utility.getHomeOfFarmer(player).getFrontDoorSpot->Game1.warpFarmer(Farm)";

    private sealed class ActiveReturnScepter
    {
        public ActiveReturnScepter(PendingExecution pending, Wand wand, int slot, int stackBefore,
            string beforeLocationId, Point beforeTile, Point destinationTile, int facingBefore,
            int immediateSpriteDelta, bool immediateTransitionVerified, string immediateTransitionObserved, string startedAt)
        {
            Pending = pending;
            Wand = wand;
            Slot = slot;
            StackBefore = stackBefore;
            BeforeLocationId = beforeLocationId;
            BeforeTile = beforeTile;
            DestinationTile = destinationTile;
            FacingBefore = facingBefore;
            ImmediateSpriteDelta = immediateSpriteDelta;
            ImmediateTransitionVerified = immediateTransitionVerified;
            ImmediateTransitionObserved = immediateTransitionObserved;
            StartedAt = startedAt;
        }

        public PendingExecution Pending { get; }
        public Wand Wand { get; }
        public int Slot { get; }
        public int StackBefore { get; }
        public string BeforeLocationId { get; }
        public Point BeforeTile { get; }
        public Point DestinationTile { get; }
        public int FacingBefore { get; }
        public int ImmediateSpriteDelta { get; }
        public bool ImmediateTransitionVerified { get; }
        public string ImmediateTransitionObserved { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }

    private void StartUseReturnScepter(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "destination=Farm:" + request.ReturnScepterFrontDoorTileX + "," +
            request.ReturnScepterFrontDoorTileY + ";inventory_stack_unchanged=true";
        if (!request.InventorySlotIndex.HasValue || request.ExpectedStackBefore != 1 || request.ExpectedStackAfter != 1 ||
            string.IsNullOrWhiteSpace(request.ReturnScepterProjectionFingerprint) ||
            !ReturnScepterRequestContractIsExact(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_return_scepter", requested,
                "typed_contract=missing_or_invalid", "use_return_scepter_typed_fields_required"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var wand = item?.GetType() == typeof(Wand) &&
            string.Equals(item.QualifiedItemId, "(T)ReturnScepter", StringComparison.Ordinal)
                ? (Wand)item
                : null;
        FarmHouse? home = null;
        try
        {
            home = Utility.getHomeOfFarmer(Game1.player);
        }
        catch
        {
            // Fail closed below; never enter Wand's callback with an unresolved home.
        }
        var door = home?.getFrontDoorSpot();
        var isCabin = home is Cabin;
        var stablePlayerGate = Game1.player.canMove && !Game1.player.UsingTool &&
            Game1.activeClickableMenu is null && !Game1.dialogueUp && Game1.currentMinigame is null &&
            !Game1.eventUp && !Game1.fadeToBlack && !Game1.player.swimming.Value &&
            !Game1.player.bathingClothes.Value && !Game1.player.onBridge.Value &&
            !Game1.player.IsSitting() && !Game1.player.isRidingHorse() && !Game1.player.canOnlyWalk;
        var destinationMatches = home is not null && door.HasValue &&
            string.Equals(Game1.player.homeLocation.Value, request.ReturnScepterHomeLocationId, StringComparison.Ordinal) &&
            string.Equals(home.GetType().Name, request.ReturnScepterHomeRuntimeType, StringComparison.Ordinal) &&
            string.Equals(request.ReturnScepterDestinationLocationId, "Farm", StringComparison.Ordinal) &&
            door.Value.X == request.ReturnScepterFrontDoorTileX && door.Value.Y == request.ReturnScepterFrontDoorTileY &&
            isCabin == request.ReturnScepterHomeIsCabin;
        var alreadyAtDestination = door.HasValue &&
            string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.Ordinal) &&
            Game1.player.TilePoint == door.Value;
        if (wand is null || wand.Stack != 1 || !wand.InstantUse || !stablePlayerGate ||
            !destinationMatches || alreadyAtDestination || request.ReturnScepterAlreadyAtDestination != false ||
            !string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_return_scepter", requested,
                ReturnScepterObservedEffect(slot), home is null ? "use_return_scepter_home_unavailable" :
                alreadyAtDestination ? "use_return_scepter_already_at_destination" :
                "use_return_scepter_location_gate_inventory_or_destination_drift"));
            return;
        }

        var sourceLocation = Game1.currentLocation;
        var spritesBefore = sourceLocation.temporarySprites.Count;
        var facingBefore = Game1.player.FacingDirection;
        var beforeTile = Game1.player.TilePoint;
        var beforeLocation = sourceLocation.NameOrUniqueName;
        SelectTool(wand);
        Game1.player.lastClick = Game1.player.GetToolLocation();
        Game1.player.BeginUsingTool();
        if (!Game1.player.UsingTool)
            Game1.player.faceDirection(facingBefore);
        var spriteDelta = sourceLocation.temporarySprites.Count - spritesBefore;
        var immediateTransitionVerified = spriteDelta == 29 && !Game1.displayFarmer &&
            Game1.player.temporarilyInvincible && Game1.player.temporaryInvincibilityTimer == -2000 &&
            Game1.player.CanMove && Game1.player.freezePause == 2000;
        var immediateTransitionObserved = "sprites=" + spriteDelta +
            ",display_farmer=" + Game1.displayFarmer.ToString().ToLowerInvariant() +
            ",temporarily_invincible=" + Game1.player.temporarilyInvincible.ToString().ToLowerInvariant() +
            ",invincibility_timer=" + Game1.player.temporaryInvincibilityTimer +
            ",can_move=" + Game1.player.CanMove.ToString().ToLowerInvariant() +
            ",freeze_pause=" + Game1.player.freezePause;
        activeReturnScepter = new ActiveReturnScepter(pending, wand, slot, wand.Stack, beforeLocation,
            beforeTile, door!.Value, facingBefore, spriteDelta, immediateTransitionVerified, immediateTransitionObserved,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    private static bool ReturnScepterRequestContractIsExact(TrainingExecutionRequest request)
    {
        return string.Equals(request.QualifiedItemId, "(T)ReturnScepter", StringComparison.Ordinal) &&
            request.ReturnScepterInstantUse == true && request.ReturnScepterFacingDirection == 2 &&
            request.ReturnScepterCallbackDelayMs == 1000 && request.ReturnScepterFreezePauseMs == 2000 &&
            request.ReturnScepterPoofSpriteCount == 12 && request.ReturnScepterTrailSpriteCount == 17 &&
            request.ReturnScepterTrailDelayStepMs == 25 && request.ReturnScepterTrailMaxDelayMs == 400 &&
            string.Equals(request.ReturnScepterSound, "wand", StringComparison.Ordinal) &&
            string.Equals(request.NativeContract, RuntimeReturnScepterNativeContract, StringComparison.Ordinal);
    }

    private void TickReturnScepter()
    {
        var active = activeReturnScepter;
        if (active is null)
            return;
        active.ElapsedTicks++;
        var arrived = string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.Ordinal) &&
            Game1.player.TilePoint == active.DestinationTile;
        var nativeTransitionSettled = Game1.displayFarmer && !Game1.player.temporarilyInvincible &&
            Game1.player.temporaryInvincibilityTimer == 0 && Game1.player.CanMove;
        if (arrived && nativeTransitionSettled)
        {
            CompleteReturnScepter(active);
            return;
        }
        if (active.ElapsedTicks > 360)
        {
            activeReturnScepter = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_return_scepter",
                "destination=Farm:" + active.DestinationTile.X + "," + active.DestinationTile.Y,
                ReturnScepterObservedEffect(active.Slot) + ";elapsed_ticks=" + active.ElapsedTicks,
                "use_return_scepter_native_warp_timeout"));
        }
    }

    private void CompleteReturnScepter(ActiveReturnScepter active)
    {
        var request = active.Pending.Request;
        var item = active.Slot >= 0 && active.Slot < Game1.player.Items.Count
            ? Game1.player.Items[active.Slot]
            : null;
        var inventoryVerified = ReferenceEquals(item, active.Wand) && item.Stack == active.StackBefore &&
            string.Equals(item.QualifiedItemId, "(T)ReturnScepter", StringComparison.Ordinal);
        var destinationVerified = string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.Ordinal) &&
            Game1.player.TilePoint == active.DestinationTile;
        var settled = Game1.displayFarmer && !Game1.player.temporarilyInvincible &&
            Game1.player.temporaryInvincibilityTimer == 0 && Game1.player.CanMove;
        var verified = active.ImmediateTransitionVerified && inventoryVerified && destinationVerified && settled;
        activeReturnScepter = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_return_scepter",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_Farmer_BeginUsingTool_instant_chain_succeeded", "native_29_sprite_and_transition_start_verified", "own_home_front_door_native_callback_verified", "reusable_wand_identity_and_stack_unchanged" }
                : new[] { active.ImmediateTransitionVerified ? "native_transition_start_verified" : "native_transition_start_mismatch", inventoryVerified ? "reusable_wand_verified" : "reusable_wand_mismatch", destinationVerified ? "home_destination_verified" : "home_destination_mismatch", settled ? "native_transition_settled" : "native_transition_not_settled" },
            RequestedEffect = "destination=Farm:" + active.DestinationTile.X + "," + active.DestinationTile.Y + ";inventory_stack_unchanged=true",
            ObservedEffect = ReturnScepterObservedEffect(active.Slot) +
                ";before_location=" + active.BeforeLocationId + ";before_tile=" + active.BeforeTile.X + "," + active.BeforeTile.Y +
                ";sprite_delta=" + active.ImmediateSpriteDelta + ";immediate=" + active.ImmediateTransitionObserved +
                ";elapsed_ticks=" + active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_return_scepter_post_state_mismatch" },
            ChangedFacts = verified
                ? new[] { new SimulatedFactChange { Path = "player.location_tile", Before = active.BeforeLocationId + ":" + active.BeforeTile.X + "," + active.BeforeTile.Y, After = "Farm:" + active.DestinationTile.X + "," + active.DestinationTile.Y } }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static string ReturnScepterObservedEffect(int slot)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        return "location=" + (Game1.player.currentLocation?.NameOrUniqueName ?? "unavailable") +
            ";tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";slot=" + slot + ";qualified_item_id=" + (item?.QualifiedItemId ?? "missing") +
            ";stack=" + (item?.Stack.ToString() ?? "missing") +
            ";display_farmer=" + Game1.displayFarmer.ToString().ToLowerInvariant() +
            ";temporarily_invincible=" + Game1.player.temporarilyInvincible.ToString().ToLowerInvariant() +
            ";can_move=" + Game1.player.CanMove.ToString().ToLowerInvariant();
    }
}
