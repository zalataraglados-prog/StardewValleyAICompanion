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
        Monitor.Log("Started native ShippingMenu menu-button recovery.", LogLevel.Info);
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
                    SetShippingSummaryPhase(active, ShipSummaryClosePhase.Press);
                }
                break;

            case ShipSummaryClosePhase.Press:
                break;

            case ShipSummaryClosePhase.WaitClose:
                if (shippingMenu.CanReceiveInput() &&
                    shippingMenu.currentPage == -1 &&
                    active.ElapsedTicks - active.PhaseStartTick > 120)
                {
                    active.CloseRetries++;
                    if (active.CloseRetries > 3)
                    {
                        CompleteBlockedShippingSummaryClose(active, "shipping_summary_close_not_observed_after_retries");
                        return;
                    }

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

        if (active.Phase == ShipSummaryClosePhase.Press)
        {
            DispatchNativeShippingSummaryClose(shippingMenu);
            SetShippingSummaryPhase(active, ShipSummaryClosePhase.WaitClose);
        }
    }

    private static void DispatchNativeShippingSummaryClose(ShippingMenu shippingMenu)
    {
        shippingMenu.receiveKeyPress(Microsoft.Xna.Framework.Input.Keys.Escape);
    }

    private static void SetShippingSummaryPhase(
        ActiveShippingSummaryClose active,
        ShipSummaryClosePhase phase)
    {
        active.Phase = phase;
        active.PhaseStartTick = active.ElapsedTicks;
    }

    private void CompleteShippingSummaryClose(ActiveShippingSummaryClose active)
    {
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
