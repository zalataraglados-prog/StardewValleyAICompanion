using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewAI.Contracts.Training;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartShippingSummaryClose(PendingExecution pending, ShippingMenu shippingMenu)
    {
        if (activeShippingSummaryClose is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                "close_menu",
                "menus.active_menu.is_open=false",
                CloseMenuObservedEffect(),
                "shipping_summary_close_executor_busy"));
            return;
        }

        activeShippingSummaryClose = new ActiveShippingSummaryClose(pending, shippingMenu);
        Monitor.Log("Started native ShippingMenu OK-button recovery.", LogLevel.Info);
    }

    private void TickShippingSummaryClose()
    {
        var active = activeShippingSummaryClose;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteBlockedShippingSummaryClose(active, "shipping_summary_close_timeout");
            return;
        }

        var currentMenu = Game1.activeClickableMenu;
        if (currentMenu is null)
        {
            CompleteShippingSummaryClose(active);
            return;
        }

        if (!ReferenceEquals(currentMenu, active.InitialMenu) || currentMenu is not ShippingMenu shippingMenu)
        {
            CompleteBlockedShippingSummaryClose(
                active,
                "shipping_summary_menu_changed:type=" + currentMenu.GetType().Name);
            return;
        }

        switch (active.Phase)
        {
            case ShipSummaryClosePhase.WaitReady:
                if (shippingMenu.CanReceiveInput() && shippingMenu.currentPage == -1)
                {
                    ResetShippingSummaryInput(active);
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.Position);
                }
                break;

            case ShipSummaryClosePhase.Position:
                if (active.PositionSet && !active.PositionVerified)
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.PositionVerify);
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (active.PositionVerified && !active.ButtonPressed)
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.Press);
                break;

            case ShipSummaryClosePhase.Press:
                if (active.ButtonPressed && !active.ButtonReleased)
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.Release);
                break;

            case ShipSummaryClosePhase.Release:
                if (active.ButtonReleased)
                {
                    ResetShippingSummaryInput(active);
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.WaitClose);
                }
                break;

            case ShipSummaryClosePhase.WaitClose:
                if (active.ElapsedTicks - active.PhaseStartTick > 120)
                {
                    active.CloseRetries++;
                    if (active.CloseRetries > 3)
                    {
                        CompleteBlockedShippingSummaryClose(active, "shipping_summary_close_not_observed_after_retries");
                        return;
                    }

                    ReleaseSmapiLeftButtonOverride();
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.WaitReady);
                }
                break;
        }
    }

    private void ApplyShippingSummaryCloseInput(ActiveShippingSummaryClose active)
    {
        if (!ReferenceEquals(Game1.activeClickableMenu, active.InitialMenu) ||
            Game1.activeClickableMenu is not ShippingMenu shippingMenu)
        {
            return;
        }

        switch (active.Phase)
        {
            case ShipSummaryClosePhase.Position:
                if (!active.PositionSet)
                {
                    var okButton = shippingMenu.okButton;
                    if (okButton is null)
                    {
                        CompleteBlockedShippingSummaryClose(active, "shipping_summary_ok_button_null");
                        return;
                    }

                    active.PositionTarget = new Point(okButton.bounds.Center.X, okButton.bounds.Center.Y);
                    Game1.setMousePosition(active.PositionTarget.X, active.PositionTarget.Y, ui_scale: true);
                    active.PositionSet = true;
                }
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (active.PositionSet && !active.PositionVerified)
                {
                    var actualX = Game1.getMouseX(ui_scale: true);
                    var actualY = Game1.getMouseY(ui_scale: true);
                    if (Math.Abs(actualX - active.PositionTarget.X) > 2 ||
                        Math.Abs(actualY - active.PositionTarget.Y) > 2)
                    {
                        CompleteBlockedShippingSummaryClose(
                            active,
                            "shipping_summary_cursor_position_mismatch:expected=" +
                            active.PositionTarget.X + "," + active.PositionTarget.Y +
                            ";actual=" + actualX + "," + actualY);
                        return;
                    }

                    active.PositionVerified = true;
                }
                break;

            case ShipSummaryClosePhase.Press:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                    {
                        CompleteBlockedShippingSummaryClose(active, "shipping_summary_press_failed:" + pressReason);
                        return;
                    }

                    active.ButtonPressed = true;
                }
                break;

            case ShipSummaryClosePhase.Release:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: false, out var releaseReason))
                    {
                        active.ReleaseRetries++;
                        if (active.ReleaseRetries > 3)
                        {
                            CompleteBlockedShippingSummaryClose(
                                active,
                                "shipping_summary_release_failed_after_retries:" + releaseReason);
                        }
                        return;
                    }

                    active.ButtonReleased = true;
                }
                break;
        }
    }

    private static void SetShippingSummaryPhase(
        ActiveShippingSummaryClose active,
        ShipSummaryClosePhase phase)
    {
        active.Phase = phase;
        active.PhaseStartTick = active.ElapsedTicks;
    }

    private static void ResetShippingSummaryInput(ActiveShippingSummaryClose active)
    {
        active.PositionSet = false;
        active.PositionVerified = false;
        active.ButtonPressed = false;
        active.ButtonReleased = false;
        active.ReleaseRetries = 0;
    }

    private void CompleteShippingSummaryClose(ActiveShippingSummaryClose active)
    {
        ReleaseSmapiLeftButtonOverride();
        activeShippingSummaryClose = null;
        active.Pending.Completion.SetResult(ShippingSummaryCloseResult(
            active,
            "applied",
            "verified",
            new[] { "shipping_summary_closed_by_native_ok_button" }));
    }

    private void CompleteBlockedShippingSummaryClose(
        ActiveShippingSummaryClose active,
        string reason)
    {
        ReleaseSmapiLeftButtonOverride();
        activeShippingSummaryClose = null;
        active.Pending.Completion.SetResult(ShippingSummaryCloseResult(
            active,
            "blocked",
            "blocked",
            new[] { reason }));
    }

    private static TrainingExecutionResult ShippingSummaryCloseResult(
        ActiveShippingSummaryClose active,
        string status,
        string verificationStatus,
        string[] reasons)
    {
        var afterMenu = Game1.activeClickableMenu;
        return new TrainingExecutionResult
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
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = CloseMenuObservedEffect() +
                ";shipping_summary_close_ticks=" + active.ElapsedTicks +
                ";shipping_summary_close_retries=" + active.CloseRetries,
            BlockReasons = status == "blocked" ? reasons : Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.is_open",
                    Before = "true",
                    After = (afterMenu is not null).ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "menus.active_menu.type",
                    Before = "ShippingMenu",
                    After = afterMenu?.GetType().Name ?? "none"
                }
            }
        };
    }
}
