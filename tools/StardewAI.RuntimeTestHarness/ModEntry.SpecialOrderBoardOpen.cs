using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool IsSpecialOrderBoardActionType(string actionType) =>
        actionType is "SpecialOrders" or "QiChallengeBoard" or "DesertMarlon";

    private void StartSpecialOrderBoardOpen(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
            reasons.Add("interact_target_tile_required");
        if (!string.Equals(request.InteractionKind, "map_action", StringComparison.Ordinal))
            reasons.Add("interact_kind_unsupported");
        if (!IsSpecialOrderBoardActionType(request.ExpectedActionType))
            reasons.Add("special_order_board_action_type_required");
        if (Game1.activeClickableMenu is not null)
            reasons.Add("interact_menu_must_be_clear");

        var target = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : Point.Zero;
        if (request.TargetTileX.HasValue && request.TargetTileY.HasValue && !AreAdjacent(Game1.player.TilePoint, target))
            reasons.Add("interact_target_not_adjacent");
        var rawAction = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? Game1.currentLocation.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings")
            : null;
        var actionType = rawAction?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (!string.Equals(actionType, request.ExpectedActionType, StringComparison.OrdinalIgnoreCase))
            reasons.Add("interact_expected_action_type_mismatch");
        if (activeSpecialOrderBoardOpen is not null)
            reasons.Add("special_order_board_open_executor_busy");
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "interact",
                "native_special_order_board_interaction_started=true",
                "active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"),
                reasons.Distinct(StringComparer.Ordinal).ToArray()));
            return;
        }

        var handled = Game1.currentLocation.checkAction(
            new TileLocation(target.X, target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "interact",
                "native_special_order_board_interaction_started=true",
                "active_menu=none",
                "map_action_not_handled"));
            return;
        }

        activeSpecialOrderBoardOpen = new ActiveSpecialOrderBoardOpen(pending, request.ExpectedActionType);
    }

    private void TickSpecialOrderBoardOpen()
    {
        if (activeSpecialOrderBoardOpen is null) return;
        var active = activeSpecialOrderBoardOpen;
        active.ElapsedTicks++;
        var menu = Game1.activeClickableMenu;
        var verified = active.ActionType switch
        {
            "SpecialOrders" => menu is SpecialOrdersBoard board && string.IsNullOrEmpty(board.boardType),
            "QiChallengeBoard" => menu is SpecialOrdersBoard board && board.boardType == "Qi",
            "DesertMarlon" => menu is DialogueBox dialogue &&
                string.Equals(dialogue.characterDialogue?.speaker?.Name, "Marlon", StringComparison.Ordinal),
            _ => false
        };
        if (verified)
        {
            activeSpecialOrderBoardOpen = null;
            active.Pending.Completion.SetResult(SpecialOrderBoardOpenResult(
                active,
                "applied",
                "verified",
                new[]
                {
                    "native_map_action_handled",
                    "native_mutex_or_dialogue_callback_observed",
                    "active_menu=" + menu!.GetType().Name,
                    "wait_ticks=" + active.ElapsedTicks
                }));
            return;
        }
        if (menu is not null && menu is not DialogueBox && menu is not SpecialOrdersBoard)
        {
            activeSpecialOrderBoardOpen = null;
            active.Pending.Completion.SetResult(SpecialOrderBoardOpenResult(
                active,
                "blocked",
                "observed_mismatch",
                new[] { "unexpected_menu_after_special_order_board_action:" + menu.GetType().Name }));
            return;
        }
        if (active.ElapsedTicks <= 180) return;
        activeSpecialOrderBoardOpen = null;
        active.Pending.Completion.SetResult(SpecialOrderBoardOpenResult(
            active,
            "blocked",
            "blocked",
            new[] { "special_order_board_native_callback_timeout" }));
    }

    private static TrainingExecutionResult SpecialOrderBoardOpenResult(
        ActiveSpecialOrderBoardOpen active,
        string status,
        string verification,
        string[] reasons) =>
        new()
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "interact",
            PrimitiveVerificationStatus = verification,
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "native_special_order_board_interaction_started=true",
            ObservedEffect = "action_type=" + active.ActionType +
                ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";wait_ticks=" + active.ElapsedTicks,
            BlockReasons = status == "blocked" ? reasons : Array.Empty<string>()
        };

    private sealed class ActiveSpecialOrderBoardOpen
    {
        public ActiveSpecialOrderBoardOpen(PendingExecution pending, string actionType)
        {
            Pending = pending;
            ActionType = actionType;
        }

        public PendingExecution Pending { get; }
        public string ActionType { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
    }
}
