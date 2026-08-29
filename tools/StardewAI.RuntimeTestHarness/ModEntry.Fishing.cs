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
        var beforeFishingExperience = Game1.player.experiencePoints[Farmer.fishingSkill];
        var beforeLuckExperience = Game1.player.experiencePoints[Farmer.luckSkill];
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

        activeCatchFish = new ActiveCatchFish(
            pending,
            stand,
            bobber,
            rod,
            castingPower,
            maxCastRequested,
            beforeInventory,
            beforeStamina,
            beforeCaughtCount,
            beforeFishingExperience,
            beforeLuckExperience);
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
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
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
        var shouldPress = PerfectBobberBarShouldPress(bar);
        active.BobberControlTicks++;
        if (shouldPress)
        {
            active.BobberControlPressedTicks++;
        }

        return ApplyBobberBarInput(shouldPress, out reason);
    }

    private static bool PerfectBobberBarShouldPress(BobberBar bar)
    {
        var trackBottom = 568f - bar.bobberBarHeight;
        var fishSpeed = bar.bobberSpeed + bar.floaterSinkerAcceleration;
        var predictedFishCenter = PredictBobberPosition(bar);
        var predictedTarget = Math.Clamp(predictedFishCenter + 32f - bar.bobberBarHeight / 2f, 0f, trackBottom);
        var currentContainmentTop = Math.Clamp(bar.bobberPosition - bar.bobberBarHeight + 50f, 0f, trackBottom);
        var currentContainmentBottom = Math.Clamp(bar.bobberPosition + 10f, 0f, trackBottom);
        var targetBarPosition = Math.Clamp(predictedTarget, currentContainmentTop, currentContainmentBottom);
        var positionError = targetBarPosition - bar.bobberBarPos;
        var acceleration = bar.bobberInBar ? 0.15f : 0.25f;
        var reachableSpeed = MathF.Sqrt(2f * acceleration * MathF.Abs(positionError));
        var desiredRelativeSpeed = MathF.Sign(positionError) * MathF.Min(7f, reachableSpeed);
        var desiredBarSpeed = fishSpeed + desiredRelativeSpeed;
        return bar.bobberBarSpeed > desiredBarSpeed;
    }

    private static float PredictBobberPosition(BobberBar bar)
    {
        var position = bar.bobberPosition;
        var speed = bar.bobberSpeed;
        var hasActiveTarget = bar.bobberTargetPosition >= 0f &&
            Math.Abs(bar.bobberPosition - bar.bobberTargetPosition) > 3f;
        var ticks = hasActiveTarget
            ? Math.Clamp((int)Math.Ceiling(4f + Math.Abs(bar.bobberAcceleration) * 2f), 4, 18)
            : 3;
        for (var tick = 0; tick < ticks; tick++)
        {
            if (hasActiveTarget)
                speed += (bar.bobberAcceleration - speed) / 5f;
            position = Math.Clamp(position + speed + bar.floaterSinkerAcceleration, 0f, 532f);
        }
        return position;
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
        var pressedRatio = active.BobberControlTicks == 0
            ? 0f
            : active.BobberControlPressedTicks / (float)active.BobberControlTicks;
        return "catch_fish_minigame_diagnostic:" +
            "ticks=" + active.BobberBarTicks +
            ",in_bar_ratio=" + inBarRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
            ",pressed_ratio=" + pressedRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
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

        if (Game1.player.Items.Take(Game1.player.maxItems.Value).Count(item => item is not null) >= Game1.player.maxItems.Value)
        {
            reasons.Add("catch_fish_inventory_full_requires_storage_transfer");
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
        var possibleItemIds = ReadFishingPossibleItemIds(
            request.PossibleQualifiedItemIdsJson);
        var resourceQuestReason = ValidateQuestResourceSourceTarget(
            request,
            possibleItemIds);
        if (!string.IsNullOrWhiteSpace(resourceQuestReason))
        {
            reasons.Add(resourceQuestReason);
        }
        if (!ValidateSpecialOrderCollectSourceTarget(
                request,
                possibleItemIds,
                out var specialOrderReason))
        {
            reasons.Add(specialOrderReason);
        }
        var fishingQuestReason = ValidateQuestFishingAttempt(
            request,
            possibleItemIds);
        if (!string.IsNullOrWhiteSpace(fishingQuestReason))
        {
            reasons.Add(fishingQuestReason);
        }

        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string[] ReadFishingPossibleItemIds(string possibleItemIdsJson)
    {
        try
        {
            using var possibleItemIds = JsonDocument.Parse(possibleItemIdsJson);
            return possibleItemIds.RootElement.ValueKind == JsonValueKind.Array
                ? possibleItemIds.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
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
        var result = new TrainingExecutionResult
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
                new SimulatedFactChange { Path = "player.skills.fishing.experience", Before = active.BeforeFishingExperience.ToString(System.Globalization.CultureInfo.InvariantCulture), After = Game1.player.experiencePoints[Farmer.fishingSkill].ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.luck.experience", Before = active.BeforeLuckExperience.ToString(System.Globalization.CultureInfo.InvariantCulture), After = Game1.player.experiencePoints[Farmer.luckSkill].ToString(System.Globalization.CultureInfo.InvariantCulture) },
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
        };
        ApplyQuestResourceSourceFeedback(result, request);
        ApplySpecialOrderCollectSourceFeedback(result, request);
        ApplyQuestFishingFeedback(result, request);
        active.Pending.Completion.SetResult(result);
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
}
