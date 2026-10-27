using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartSkullKeyChestInteraction(PendingExecution pending)
    {
        StartSpecialItemChestInteraction(
            pending,
            expectedActionType: "SkullKeyChest",
            specialItemWhich: 4,
            primitiveKind: "claim_skull_key",
            progressFactPath: "player.has_skull_key");
    }

    private void StartDarkTalismanChestInteraction(PendingExecution pending)
    {
        StartSpecialItemChestInteraction(
            pending,
            expectedActionType: "DarkTalismanChest",
            specialItemWhich: 6,
            primitiveKind: "claim_dark_talisman",
            progressFactPath: "player.has_dark_talisman");
    }

    private void StartSpecialItemChestInteraction(
        PendingExecution pending,
        string expectedActionType,
        int specialItemWhich,
        string primitiveKind,
        string progressFactPath)
    {
        var request = pending.Request;
        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        var reasons = ValidateExecutionRequest(request);
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            reasons.Add("interact_target_tile_required");
        }
        if (!string.Equals(request.InteractionKind, "overlay_object", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, expectedActionType, StringComparison.Ordinal))
        {
            reasons.Add("special_item_chest_contract_mismatch");
        }
        if (!SpecialItemChestLocationMatches(expectedActionType, Game1.currentLocation, out var locationReason))
        {
            reasons.Add(locationReason);
        }
        else if (!TryGetSpecialItemRewardChest(Game1.currentLocation, target, specialItemWhich, out _))
        {
            reasons.Add("special_item_reward_chest_not_observed_at_target");
        }
        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            reasons.Add("interact_target_not_adjacent");
        }
        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("interact_menu_must_be_clear");
        }
        var progressBefore = SpecialItemProgressObserved(expectedActionType);
        if (progressBefore)
        {
            reasons.Add("special_item_progress_already_acquired");
        }
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                primitiveKind,
                InteractRequestedEffect(request) + ";required_postcondition=" + progressFactPath + "=true",
                InteractObservedEffect() + ";" + progressFactPath + "=" + progressBefore.ToString().ToLowerInvariant(),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        activeSpecialItemChestInteraction = new ActiveSpecialItemChestInteraction(
            pending,
            Game1.currentLocation,
            target,
            specialItemWhich,
            expectedActionType,
            primitiveKind,
            progressFactPath,
            progressBefore);
    }

    private void TickSpecialItemChestInteraction()
    {
        var active = activeSpecialItemChestInteraction;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (SpecialItemProgressObserved(active.ExpectedActionType))
        {
            if (active.ProgressObservedAtTick == 0)
            {
                active.ProgressObservedAtTick = active.ElapsedTicks;
            }
            if (Game1.player.CanMove && !Game1.player.UsingTool && Game1.activeClickableMenu is null && !Game1.dialogueUp)
            {
                TryApplySmapiRightButtonOverride(pressed: false, out _);
                activeSpecialItemChestInteraction = null;
                active.Pending.Completion.SetResult(BuildSpecialItemChestResult(active, verified: true));
                return;
            }
            if (active.DismissActionHeld)
            {
                if (!TryApplySmapiRightButtonOverride(pressed: false, out var releaseReason))
                {
                    CompleteSpecialItemChest(active, false, "special_item_hold_up_" + releaseReason);
                    return;
                }
                active.DismissActionHeld = false;
                active.DismissAttempts++;
                active.LastDismissAttemptTick = active.ElapsedTicks;
                return;
            }
            if (active.ElapsedTicks - active.ProgressObservedAtTick >= 15 &&
                active.ElapsedTicks - active.LastDismissAttemptTick >= 20)
            {
                if (!TryApplySmapiRightButtonOverride(pressed: true, out var pressReason))
                {
                    CompleteSpecialItemChest(active, false, "special_item_hold_up_" + pressReason);
                    return;
                }
                active.DismissActionHeld = true;
            }
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            TryApplySmapiRightButtonOverride(pressed: false, out _);
            CompleteSpecialItemChest(active, false, "special_item_postcondition_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteSpecialItemChest(active, false, "special_item_chest_location_changed");
            return;
        }
        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            CompleteSpecialItemChest(active, false, "special_item_chest_adjacency_lost");
            return;
        }

        switch (active.Stage)
        {
            case SpecialItemChestStage.OpenChest:
                active.OpenHandled = active.Location.checkAction(
                    new TileLocation(active.Target.X, active.Target.Y),
                    new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                if (!active.OpenHandled)
                {
                    CompleteSpecialItemChest(active, false, "special_item_chest_open_not_handled");
                    return;
                }
                active.Stage = SpecialItemChestStage.WaitForOpenAnimation;
                active.StageStartedAtTick = active.ElapsedTicks;
                return;

            case SpecialItemChestStage.WaitForOpenAnimation:
                if (active.ElapsedTicks - active.StageStartedAtTick < 60)
                {
                    return;
                }
                active.Stage = SpecialItemChestStage.ClaimItem;
                return;

            case SpecialItemChestStage.ClaimItem:
                active.ClaimHandled = active.Location.checkAction(
                    new TileLocation(active.Target.X, active.Target.Y),
                    new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                active.ClaimAttempts++;
                active.Stage = SpecialItemChestStage.WaitForPostcondition;
                active.StageStartedAtTick = active.ElapsedTicks;
                return;

            case SpecialItemChestStage.WaitForPostcondition:
                if (SpecialItemProgressObserved(active.ExpectedActionType))
                {
                    return;
                }
                if (active.ElapsedTicks - active.StageStartedAtTick >= 45 && active.ClaimAttempts < 3)
                {
                    active.Stage = SpecialItemChestStage.ClaimItem;
                    return;
                }
                if (active.ElapsedTicks - active.StageStartedAtTick >= 90)
                {
                    CompleteSpecialItemChest(active, false, "special_item_native_claim_not_observed");
                }
                return;
        }
    }

    private void CompleteSpecialItemChest(
        ActiveSpecialItemChestInteraction active,
        bool verified,
        params string[] reasons)
    {
        activeSpecialItemChestInteraction = null;
        active.Pending.Completion.SetResult(BuildSpecialItemChestResult(active, verified, reasons));
    }

    private static bool SpecialItemChestLocationMatches(
        string expectedActionType,
        GameLocation location,
        out string reason)
    {
        if (string.Equals(expectedActionType, "SkullKeyChest", StringComparison.Ordinal))
        {
            var matches = location is MineShaft mine &&
                string.Equals(RuntimeMineKind(mine), "ordinary_mines", StringComparison.Ordinal) &&
                mine.mineLevel == MineShaft.bottomOfMineLevel;
            reason = matches ? string.Empty : "skull_key_chest_requires_ordinary_mine_floor_120";
            return matches;
        }
        if (string.Equals(expectedActionType, "DarkTalismanChest", StringComparison.Ordinal))
        {
            var matches = string.Equals(location.NameOrUniqueName, "BugLand", StringComparison.Ordinal);
            reason = matches ? string.Empty : "dark_talisman_chest_requires_bug_land";
            return matches;
        }
        reason = "special_item_chest_type_unsupported";
        return false;
    }

    private static bool SpecialItemProgressObserved(string expectedActionType) => expectedActionType switch
    {
        "SkullKeyChest" => Game1.player.hasSkullKey,
        "DarkTalismanChest" => Game1.player.hasDarkTalisman,
        _ => false
    };

    private static bool TryGetSpecialItemRewardChest(
        GameLocation location,
        Point target,
        int specialItemWhich,
        out Chest? chest)
    {
        chest = null;
        if (!location.overlayObjects.TryGetValue(target.ToVector2(), out var overlay) || overlay is not Chest candidate)
        {
            return false;
        }

        chest = candidate;
        return candidate.Items.Count == 1 &&
            candidate.Items.OfType<SpecialItem>().Count() == 1 &&
            candidate.Items.OfType<SpecialItem>().Single().which.Value == specialItemWhich;
    }

    private static TrainingExecutionResult BuildSpecialItemChestResult(
        ActiveSpecialItemChestInteraction active,
        bool verified,
        params string[] reasons)
    {
        var request = active.Pending.Request;
        var progressAfter = SpecialItemProgressObserved(active.ExpectedActionType);
        var verificationReasons = verified
            ? new[] { "native_reward_chest_open_handled", "native_reward_item_claimed", active.ProgressFactPath + "_transition_observed" }
            : reasons.Length > 0 ? reasons : new[] { "special_item_native_claim_not_observed" };
        return new TrainingExecutionResult
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
            ActualTicks = active.ElapsedTicks,
            PrimitiveKind = active.PrimitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request) + ";required_postcondition=" + active.ProgressFactPath + "=true",
            ObservedEffect = InteractObservedEffect() +
                ";open_handled=" + active.OpenHandled.ToString().ToLowerInvariant() +
                ";claim_handled=" + active.ClaimHandled.ToString().ToLowerInvariant() +
                ";claim_attempts=" + active.ClaimAttempts +
                ";dismiss_attempts=" + active.DismissAttempts +
                ";" + active.ProgressFactPath + "=" + progressAfter.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = active.ProgressFactPath,
                    Before = active.ProgressBefore.ToString().ToLowerInvariant(),
                    After = progressAfter.ToString().ToLowerInvariant()
                }
            }
        };
    }
}
