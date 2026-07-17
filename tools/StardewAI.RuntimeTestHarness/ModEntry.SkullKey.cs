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
    private void StartSkullKeyChestInteraction(PendingExecution pending)
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
            !string.Equals(request.ExpectedActionType, "SkullKeyChest", StringComparison.Ordinal))
        {
            reasons.Add("skull_key_chest_contract_mismatch");
        }
        if (Game1.currentLocation is not MineShaft mine ||
            !string.Equals(RuntimeMineKind(mine), "ordinary_mines", StringComparison.Ordinal) ||
            mine.mineLevel != MineShaft.bottomOfMineLevel)
        {
            reasons.Add("skull_key_chest_requires_ordinary_mine_floor_120");
        }
        else if (!TryGetSkullKeyRewardChest(mine, target, out _))
        {
            reasons.Add("skull_key_reward_chest_not_observed_at_target");
        }
        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            reasons.Add("interact_target_not_adjacent");
        }
        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("interact_menu_must_be_clear");
        }
        if (Game1.player.hasSkullKey)
        {
            reasons.Add("skull_key_already_acquired");
        }
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "claim_skull_key",
                InteractRequestedEffect(request) + ";required_postcondition=player.has_skull_key=true",
                InteractObservedEffect() + ";player.has_skull_key=" + Game1.player.hasSkullKey.ToString().ToLowerInvariant(),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        activeSkullKeyChestInteraction = new ActiveSkullKeyChestInteraction(pending, (MineShaft)Game1.currentLocation, target);
    }

    private void TickSkullKeyChestInteraction()
    {
        var active = activeSkullKeyChestInteraction;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (Game1.player.hasSkullKey)
        {
            if (active.KeyObservedAtTick == 0)
            {
                active.KeyObservedAtTick = active.ElapsedTicks;
            }
            if (Game1.player.CanMove && !Game1.player.UsingTool && Game1.activeClickableMenu is null && !Game1.dialogueUp)
            {
                TryApplySmapiRightButtonOverride(pressed: false, out _);
                activeSkullKeyChestInteraction = null;
                active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: true));
                return;
            }
            if (active.DismissActionHeld)
            {
                if (!TryApplySmapiRightButtonOverride(pressed: false, out var releaseReason))
                {
                    activeSkullKeyChestInteraction = null;
                    active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_hold_up_" + releaseReason));
                    return;
                }
                active.DismissActionHeld = false;
                active.DismissAttempts++;
                active.LastDismissAttemptTick = active.ElapsedTicks;
                return;
            }
            if (active.ElapsedTicks - active.KeyObservedAtTick >= 15 &&
                active.ElapsedTicks - active.LastDismissAttemptTick >= 20)
            {
                if (!TryApplySmapiRightButtonOverride(pressed: true, out var pressReason))
                {
                    activeSkullKeyChestInteraction = null;
                    active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_hold_up_" + pressReason));
                    return;
                }
                active.DismissActionHeld = true;
            }
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            TryApplySmapiRightButtonOverride(pressed: false, out _);
            activeSkullKeyChestInteraction = null;
            active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_postcondition_timeout"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine || !ReferenceEquals(mine, active.Mine) || mine.mineLevel != MineShaft.bottomOfMineLevel)
        {
            activeSkullKeyChestInteraction = null;
            active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_chest_location_changed"));
            return;
        }
        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            activeSkullKeyChestInteraction = null;
            active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_chest_adjacency_lost"));
            return;
        }

        switch (active.Stage)
        {
            case SkullKeyChestStage.OpenChest:
                active.OpenHandled = mine.checkAction(
                    new TileLocation(active.Target.X, active.Target.Y),
                    new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                if (!active.OpenHandled)
                {
                    activeSkullKeyChestInteraction = null;
                    active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_chest_open_not_handled"));
                    return;
                }
                active.Stage = SkullKeyChestStage.WaitForOpenAnimation;
                active.StageStartedAtTick = active.ElapsedTicks;
                return;

            case SkullKeyChestStage.WaitForOpenAnimation:
                if (active.ElapsedTicks - active.StageStartedAtTick < 60)
                {
                    return;
                }
                active.Stage = SkullKeyChestStage.ClaimItem;
                return;

            case SkullKeyChestStage.ClaimItem:
                active.ClaimHandled = mine.checkAction(
                    new TileLocation(active.Target.X, active.Target.Y),
                    new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                active.ClaimAttempts++;
                active.Stage = SkullKeyChestStage.WaitForPostcondition;
                active.StageStartedAtTick = active.ElapsedTicks;
                return;

            case SkullKeyChestStage.WaitForPostcondition:
                if (Game1.player.hasSkullKey)
                {
                    return;
                }
                if (active.ElapsedTicks - active.StageStartedAtTick >= 45 && active.ClaimAttempts < 3)
                {
                    active.Stage = SkullKeyChestStage.ClaimItem;
                    return;
                }
                if (active.ElapsedTicks - active.StageStartedAtTick >= 90)
                {
                    activeSkullKeyChestInteraction = null;
                    active.Pending.Completion.SetResult(BuildSkullKeyChestResult(active, verified: false, "skull_key_native_claim_not_observed"));
                }
                return;
        }
    }

    private static bool TryGetSkullKeyRewardChest(MineShaft mine, Point target, out Chest? chest)
    {
        chest = null;
        if (!mine.overlayObjects.TryGetValue(new Vector2(target.X, target.Y), out var overlay) || overlay is not Chest candidate)
        {
            return false;
        }

        chest = candidate;
        return candidate.Items.OfType<SpecialItem>().Any(item => item.which.Value == 4);
    }

    private static TrainingExecutionResult BuildSkullKeyChestResult(ActiveSkullKeyChestInteraction active, bool verified, params string[] reasons)
    {
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? new[] { "native_reward_chest_open_handled", "native_reward_item_claimed", "player_has_skull_key_transition_observed" }
            : reasons.Length > 0 ? reasons : new[] { "skull_key_native_claim_not_observed" };
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
            PrimitiveKind = "claim_skull_key",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = InteractRequestedEffect(request) + ";required_postcondition=player.has_skull_key=true",
            ObservedEffect = InteractObservedEffect() +
                ";open_handled=" + active.OpenHandled.ToString().ToLowerInvariant() +
                ";claim_handled=" + active.ClaimHandled.ToString().ToLowerInvariant() +
                ";claim_attempts=" + active.ClaimAttempts +
                ";dismiss_attempts=" + active.DismissAttempts +
                ";player.has_skull_key=" + Game1.player.hasSkullKey.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.has_skull_key",
                    Before = active.HasSkullKeyBefore.ToString().ToLowerInvariant(),
                    After = Game1.player.hasSkullKey.ToString().ToLowerInvariant()
                }
            }
        };
    }
}
