using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private ActiveMineElevatorSelection? activeMineElevatorSelection;

    private void StartMineElevatorSelection(PendingExecution pending)
    {
        var request = pending.Request;
        if (Game1.activeClickableMenu is not MineElevatorMenu menu)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_menu_not_active"));
            return;
        }
        if (activeMineElevatorSelection is not null)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_executor_busy"));
            return;
        }
        if (!request.ExpectedMineLevelAfter.HasValue)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_target_depth_required"));
            return;
        }
        if (!string.Equals(request.TargetRuntimeType, "MineElevatorMenu", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_runtime_type_mismatch"));
            return;
        }

        var target = request.ExpectedMineLevelAfter.Value;
        var identity = MineElevatorRuntimeIdentity(menu);
        if (string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            !string.Equals(request.TargetRuntimeIdentity, identity, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_menu_identity_drift"));
            return;
        }
        if (target != 0 && (target < 5 || target > 120 || target % 5 != 0))
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_target_checkpoint_invalid"));
            return;
        }
        if (target == 0 && Game1.currentLocation is not MineShaft)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_floor_zero_requires_loaded_mineshaft"));
            return;
        }
        if (target == Game1.CurrentMineLevel)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_target_is_current_level"));
            return;
        }

        var component = menu.elevators.FirstOrDefault(entry =>
            entry.visible && int.TryParse(entry.name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor) && floor == target);
        if (component is null)
        {
            pending.Completion.SetResult(Blocked(request, "mine_elevator_target_not_offered"));
            return;
        }

        activeMineElevatorSelection = new ActiveMineElevatorSelection(
            pending,
            target,
            Game1.currentLocation.NameOrUniqueName,
            Game1.CurrentMineLevel,
            DateTimeOffset.UtcNow.ToString("O"));
        try
        {
            menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
        }
        catch (Exception ex)
        {
            activeMineElevatorSelection = null;
            pending.Completion.SetResult(Blocked(
                request,
                "mine_elevator_native_click_exception:" + ex.GetType().Name));
        }
    }

    private void TickMineElevatorSelection()
    {
        var active = activeMineElevatorSelection;
        if (active is null)
            return;

        try
        {
            active.ElapsedTicks++;
            var afterLocation = Game1.currentLocation.NameOrUniqueName;
            var afterLevel = Game1.CurrentMineLevel;
            var verified = active.Target == 0
                ? Game1.activeClickableMenu is null && string.Equals(afterLocation, "Mine", StringComparison.Ordinal) && Game1.player.TilePoint.X == 17 && Game1.player.TilePoint.Y == 4
                : Game1.activeClickableMenu is null && Game1.currentLocation is MineShaft mine && mine.mineLevel == active.Target;

            if (!verified && active.ElapsedTicks < ActiveMineElevatorSelection.MaxTicks)
                return;

            CompleteMineElevatorSelection(active, verified, afterLocation, afterLevel);
        }
        catch (Exception ex)
        {
            activeMineElevatorSelection = null;
            active.Pending.Completion.SetResult(Blocked(
                active.Pending.Request,
                "mine_elevator_verification_exception:" + ex.GetType().Name));
        }
    }

    private void CompleteMineElevatorSelection(
        ActiveMineElevatorSelection active,
        bool verified,
        string afterLocation,
        int afterLevel)
    {
        activeMineElevatorSelection = null;
        var reasons = verified
            ? new[] { "native_mine_elevator_button_clicked", "exact_destination_observed" }
            : new[] { "mine_elevator_destination_timeout_or_mismatch" };
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = $"mine_elevator_target={active.Target}",
            ObservedEffect = $"location={afterLocation};mine_level={afterLevel};menu_open={(Game1.activeClickableMenu is not null).ToString().ToLowerInvariant()}",
            BlockReasons = verified ? Array.Empty<string>() : reasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                new SimulatedFactChange { Path = "player.current_mine_level", Before = active.BeforeLevel.ToString(), After = afterLevel.ToString() },
                new SimulatedFactChange { Path = "menus.active_menu.type", Before = "MineElevatorMenu", After = Game1.activeClickableMenu?.GetType().Name ?? "none" }
            }
        });
    }

    private static string MineElevatorRuntimeIdentity(MineElevatorMenu menu)
    {
        var currentLevel = Game1.CurrentMineLevel;
        var inMineShaft = Game1.currentLocation is MineShaft;
        var entries = menu.elevators
            .Select(component =>
            {
                var parsed = int.TryParse(component.name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor);
                return new
                {
                    Floor = parsed ? floor : -1,
                    component.visible,
                    Selectable = parsed && component.visible && floor != currentLevel && (floor != 0 || inMineShaft),
                    component.bounds
                };
            })
            .OrderBy(entry => entry.Floor)
            .ToArray();
        var source = string.Join("\n", new[]
        {
            currentLevel.ToString(CultureInfo.InvariantCulture),
            MineShaft.lowestLevelReached.ToString(CultureInfo.InvariantCulture),
            inMineShaft.ToString(),
            string.Join(";", entries.Select(entry => $"{entry.Floor}:{entry.visible}:{entry.Selectable}:{entry.bounds.X},{entry.bounds.Y},{entry.bounds.Width},{entry.bounds.Height}"))
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private sealed class ActiveMineElevatorSelection
    {
        public const int MaxTicks = 300;

        public ActiveMineElevatorSelection(
            PendingExecution pending,
            int target,
            string beforeLocation,
            int beforeLevel,
            string startedAt)
        {
            Pending = pending;
            Target = target;
            BeforeLocation = beforeLocation;
            BeforeLevel = beforeLevel;
            StartedAt = startedAt;
        }

        public PendingExecution Pending { get; }
        public int Target { get; }
        public string BeforeLocation { get; }
        public int BeforeLevel { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }
}
