using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string HorseFluteNativeContract =
        "Object.performUseAction((O)911)->Utility.GetHorseWarpRestrictionsForFarmer(start+delayed)->FarmerTeam.requestHorseWarpEvent->OnRequestHorseWarp->Horse.mutex->Game1.warpCharacter";

    private sealed class ActiveHorseFlute
    {
        public ActiveHorseFlute(PendingExecution pending, Horse horse, int slot, int stackBefore, bool nearby,
            int facingBefore, string startedAt)
        {
            Pending = pending;
            Horse = horse;
            Slot = slot;
            StackBefore = stackBefore;
            Nearby = nearby;
            FacingBefore = facingBefore;
            StartedAt = startedAt;
            BeforeLocationId = horse.currentLocation?.NameOrUniqueName ?? string.Empty;
            BeforeTile = horse.TilePoint;
        }

        public PendingExecution Pending { get; }
        public Horse Horse { get; }
        public int Slot { get; }
        public int StackBefore { get; }
        public bool Nearby { get; }
        public int FacingBefore { get; }
        public string StartedAt { get; }
        public string BeforeLocationId { get; }
        public Microsoft.Xna.Framework.Point BeforeTile { get; }
        public int ElapsedTicks { get; set; }
    }

    private void StartUseHorseFlute(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "owned_horse=" + request.OwnedHorseId + ";result=" + request.HorseFluteExpectedResult +
            ";player.inventory[" + request.InventorySlotIndex + "].stack_unchanged=true";
        if (!request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue ||
            request.ExpectedStackAfter != request.ExpectedStackBefore || request.HorseWarpRestrictions != 0 ||
            !string.Equals(request.HorseWarpRestrictionNames, "none", StringComparison.Ordinal) ||
            !request.OwnedHorseNearby.HasValue || !request.HorseFluteUseDelayMs.HasValue ||
            !request.TeamEventStableTileX.HasValue || !request.TeamEventStableTileY.HasValue ||
            !request.TeamEventStableMatchesOwnedHorse.HasValue ||
            !request.HorseFluteExpectedFacingDirection.HasValue ||
            !string.Equals(request.NativeContract, HorseFluteNativeContract, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_horse_flute", requested,
                "typed_contract=missing_or_invalid", "use_horse_flute_typed_fields_required"));
            return;
        }

        var location = Game1.currentLocation;
        var slot = request.InventorySlotIndex.Value;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            Game1.activeClickableMenu is not null || slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object flute || flute.isTemporarilyInvisible ||
            flute.Stack != request.ExpectedStackBefore || !string.Equals(flute.QualifiedItemId, "(O)911", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_horse_flute", requested,
                "location_or_inventory_identity_mismatch", "use_horse_flute_location_menu_or_inventory_drift"));
            return;
        }

        var restrictions = Utility.GetHorseWarpRestrictionsForFarmer(Game1.player);
        var horse = Utility.findHorseForPlayer(Game1.player.UniqueMultiplayerID);
        if (restrictions != Utility.HorseWarpRestrictions.None || horse is null ||
            !string.Equals(horse.HorseId.ToString(), request.OwnedHorseId, StringComparison.Ordinal) ||
            !string.Equals(horse.currentLocation?.NameOrUniqueName ?? string.Empty, request.OwnedHorseLocationId, StringComparison.Ordinal) ||
            horse.TilePoint.X != request.OwnedHorseTileX || horse.TilePoint.Y != request.OwnedHorseTileY)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_horse_flute", requested,
                "restrictions=" + (int)restrictions + ";horse=" + (horse?.HorseId.ToString() ?? "missing"),
                "use_horse_flute_native_restriction_or_horse_identity_drift"));
            return;
        }

        var nearby = ReferenceEquals(horse.currentLocation, location) &&
            Math.Abs(Game1.player.TilePoint.X - horse.TilePoint.X) <= 1 &&
            Math.Abs(Game1.player.TilePoint.Y - horse.TilePoint.Y) <= 1;
        var expectedResult = nearby ? "already_adjacent_no_warp" : "summon_after_1500ms";
        var expectedDelay = nearby ? 0 : 1500;
        var facingBefore = Game1.player.FacingDirection;
        var expectedFacingDirection = nearby ? facingBefore : 2;
        var teamEventBinding = FindHorseFluteTeamEventStable(Game1.player);
        var stableBindingMatchesRequest =
            string.Equals(teamEventBinding.Horse?.HorseId.ToString() ?? string.Empty, request.TeamEventStableHorseId, StringComparison.Ordinal) &&
            string.Equals(teamEventBinding.Stable?.GetParentLocation()?.NameOrUniqueName ?? string.Empty, request.TeamEventStableLocationId, StringComparison.Ordinal) &&
            (teamEventBinding.Stable?.tileX.Value ?? -1) == request.TeamEventStableTileX &&
            (teamEventBinding.Stable?.tileY.Value ?? -1) == request.TeamEventStableTileY &&
            (ReferenceEquals(teamEventBinding.Horse, horse) == request.TeamEventStableMatchesOwnedHorse);
        if (nearby != request.OwnedHorseNearby ||
            !string.Equals(request.HorseFluteExpectedResult, expectedResult, StringComparison.Ordinal) ||
            request.HorseFluteUseDelayMs != expectedDelay || request.HorseFluteFreezePauseMs != expectedDelay ||
            request.HorseFluteMusicDuckMs != (nearby ? 0 : 2000) ||
            request.HorseFluteExpectedFacingDirection != expectedFacingDirection || !stableBindingMatchesRequest ||
            (!nearby && (!ReferenceEquals(teamEventBinding.Horse, horse) || request.TeamEventStableMatchesOwnedHorse != true)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_horse_flute", requested,
                "nearby=" + nearby.ToString().ToLowerInvariant(), "use_horse_flute_result_or_timing_drift"));
            return;
        }

        var active = new ActiveHorseFlute(pending, horse, slot, flute.Stack, nearby, facingBefore,
            DateTimeOffset.UtcNow.ToString("O"));
        Game1.player.CurrentToolIndex = slot;
        var used = flute.performUseAction(location);
        if (used)
            Game1.player.reduceActiveItemByOne();
        if (!used)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_horse_flute", requested,
                "performUseAction=false", "use_horse_flute_native_use_rejected"));
            return;
        }

        activeHorseFlute = active;
        if (nearby)
            CompleteHorseFlute(active, warpExpected: false);
    }

    private static (Stable? Stable, Horse? Horse) FindHorseFluteTeamEventStable(Farmer player)
    {
        Stable? matchStable = null;
        Horse? matchHorse = null;
        Utility.ForEachBuilding((Stable stable) =>
        {
            var stableHorse = stable.getStableHorse();
            if (stableHorse is null || stableHorse.getOwner() != player)
                return true;
            matchStable = stable;
            matchHorse = stableHorse;
            return false;
        });
        return (matchStable, matchHorse);
    }

    private void TickHorseFlute()
    {
        var active = activeHorseFlute;
        if (active is null || active.Nearby)
            return;
        active.ElapsedTicks++;

        var horse = Utility.findHorseForPlayer(Game1.player.UniqueMultiplayerID);
        if (horse is not null && ReferenceEquals(horse, active.Horse) &&
            ReferenceEquals(horse.currentLocation, Game1.player.currentLocation) &&
            horse.TilePoint == Game1.player.TilePoint)
        {
            CompleteHorseFlute(active, warpExpected: true);
            return;
        }
        if (active.ElapsedTicks > 360)
        {
            activeHorseFlute = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_horse_flute",
                "owned_horse_warped_to_player=true", "elapsed_ticks=" + active.ElapsedTicks,
                "use_horse_flute_native_warp_timeout"));
        }
    }

    private void CompleteHorseFlute(ActiveHorseFlute active, bool warpExpected)
    {
        var request = active.Pending.Request;
        var horse = Utility.findHorseForPlayer(Game1.player.UniqueMultiplayerID);
        var item = active.Slot < Game1.player.Items.Count ? Game1.player.Items[active.Slot] : null;
        var inventoryVerified = item?.GetType() == typeof(StardewValley.Object) &&
            string.Equals(item.QualifiedItemId, "(O)911", StringComparison.Ordinal) && item.Stack == active.StackBefore;
        var identityVerified = ReferenceEquals(horse, active.Horse) &&
            string.Equals(horse?.HorseId.ToString(), request.OwnedHorseId, StringComparison.Ordinal);
        var warpVerified = !warpExpected
            ? identityVerified && string.Equals(horse?.currentLocation?.NameOrUniqueName, active.BeforeLocationId, StringComparison.Ordinal) &&
                horse?.TilePoint == active.BeforeTile
            : identityVerified && ReferenceEquals(horse?.currentLocation, Game1.player.currentLocation) &&
                horse?.TilePoint == Game1.player.TilePoint;
        var expectedFacingDirection = warpExpected ? 2 : active.FacingBefore;
        var facingVerified = Game1.player.FacingDirection == expectedFacingDirection &&
            request.HorseFluteExpectedFacingDirection == expectedFacingDirection;
        var verified = inventoryVerified && warpVerified && facingVerified;
        activeHorseFlute = null;

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_horse_flute",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_Object_performUseAction_succeeded", "native_start_and_delayed_restrictions_passed", warpExpected ? "team_horse_warp_exact_identity_and_player_tile_verified" : "native_adjacent_success_noop_verified", "reusable_flute_stack_unchanged" }
                : new[] { inventoryVerified ? "reusable_flute_stack_verified" : "reusable_flute_stack_mismatch", warpVerified ? "horse_result_verified" : "horse_result_mismatch", facingVerified ? "native_facing_verified" : "native_facing_mismatch" },
            RequestedEffect = "owned_horse=" + request.OwnedHorseId + ";result=" + request.HorseFluteExpectedResult + ";inventory_stack_unchanged=true",
            ObservedEffect = "horse_id=" + (horse?.HorseId.ToString() ?? "missing") +
                ";before_location=" + active.BeforeLocationId + ";before_tile=" + active.BeforeTile.X + "," + active.BeforeTile.Y +
                ";after_location=" + (horse?.currentLocation?.NameOrUniqueName ?? "missing") +
                ";after_tile=" + (horse?.TilePoint.X.ToString() ?? "missing") + "," + (horse?.TilePoint.Y.ToString() ?? "missing") +
                ";inventory_stack=" + (item?.Stack.ToString() ?? "missing") +
                ";facing_direction=" + Game1.player.FacingDirection + ";elapsed_ticks=" + active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_horse_flute_post_state_mismatch" },
            ChangedFacts = verified && warpExpected
                ? new[] { new SimulatedFactChange { Path = "player.owned_horse.location_tile", Before = active.BeforeLocationId + ":" + active.BeforeTile.X + "," + active.BeforeTile.Y, After = Game1.player.currentLocation.NameOrUniqueName + ":" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y } }
                : Array.Empty<SimulatedFactChange>()
        });
    }
}
