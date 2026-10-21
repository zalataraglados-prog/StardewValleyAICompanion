using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TriggerNativeBusTransport(
        ActiveTileMove move,
        Point actionTile)
    {
        StopAllMovement();
        move.CurrentDirection = null;
        Game1.player.faceDirection(DirectionTo(
            Game1.player.TilePoint,
            actionTile));

        if (Game1.currentLocation is BusStop)
        {
            var handled = Game1.currentLocation.checkAction(
                new TileLocation(actionTile.X, actionTile.Y),
                new TileRectangle(
                    Game1.viewport.X,
                    Game1.viewport.Y,
                    Game1.viewport.Width,
                    Game1.viewport.Height),
                Game1.player);
            if (!handled)
                CompleteBlockedMove(move, "native_bus_ticket_action_not_handled");
            return;
        }

        if (Game1.currentLocation is Desert desert)
        {
            var rawAction = desert.doesTileHaveProperty(
                actionTile.X,
                actionTile.Y,
                "TouchAction",
                "Back");
            var parts = rawAction?.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (parts.Length == 0 ||
                !string.Equals(
                    parts[0],
                    "DesertBus",
                    StringComparison.OrdinalIgnoreCase))
            {
                CompleteBlockedMove(
                    move,
                    "native_bus_return_touch_source_mismatch");
                return;
            }

            desert.performTouchAction(parts, Game1.player.Position);
            if (Game1.activeClickableMenu is not DialogueBox)
            {
                CompleteBlockedMove(
                    move,
                    "native_bus_return_question_not_opened");
            }
            return;
        }

        CompleteBlockedMove(move, "native_bus_source_location_mismatch");
    }

    private bool TickNativeBusTransport(ActiveTileMove move)
    {
        if (!string.Equals(
                move.Pending.Request.ConnectorKind,
                "native_bus_transport",
                StringComparison.OrdinalIgnoreCase) ||
            !move.ConnectorActionAttempted)
        {
            return false;
        }

        StopAllMovement();
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.LastPosition = Game1.player.Position;

        if (!move.NativeBusResponseSelected)
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue ||
                !dialogue.isQuestion)
            {
                CompleteBlockedMove(
                    move,
                    "native_bus_question_dialogue_missing");
                return true;
            }

            var expectedKey = string.Equals(
                    move.LocationId,
                    "BusStop",
                    StringComparison.OrdinalIgnoreCase)
                ? "Bus"
                : "DesertBus";
            if (!string.Equals(
                    Game1.currentLocation.lastQuestionKey,
                    expectedKey,
                    StringComparison.Ordinal))
            {
                CompleteBlockedMove(move, "native_bus_question_key_mismatch");
                return true;
            }

            var responseIndex = Array.FindIndex(
                dialogue.responses ?? Array.Empty<Response>(),
                response => string.Equals(
                    response.responseKey,
                    "Yes",
                    StringComparison.Ordinal));
            if (responseIndex < 0)
            {
                CompleteBlockedMove(move, "native_bus_yes_response_missing");
                return true;
            }
            if (dialogue.transitioning ||
                dialogue.safetyTimer > 0 ||
                dialogue.responseCC is null ||
                responseIndex >= dialogue.responseCC.Count)
            {
                return TickNativeBusWait(move, "native_bus_dialogue_readiness_timeout");
            }

            var bounds = dialogue.responseCC[responseIndex].bounds;
            dialogue.performHoverAction(bounds.Center.X, bounds.Center.Y);
            if (dialogue.selectedResponse != responseIndex)
            {
                CompleteBlockedMove(move, "native_bus_yes_response_hover_failed");
                return true;
            }

            dialogue.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
            move.NativeBusResponseSelected = true;
            return TickNativeBusWait(move, "native_bus_native_transition_timeout");
        }

        if (Game1.activeClickableMenu is DialogueBox)
        {
            CompleteBlockedMove(move, "native_bus_dialogue_remained_open_after_yes");
            return true;
        }

        return TickNativeBusWait(move, "native_bus_native_transition_timeout");
    }

    private bool TickNativeBusWait(
        ActiveTileMove move,
        string timeoutReason)
    {
        move.Tick++;
        if (!config.DisableMovementTimeouts && move.Tick > move.MaxTicks)
            CompleteBlockedMove(move, timeoutReason);
        return true;
    }
}
