using System.Text.Json;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static NativeToolActionObservation ObserveNativeToolAction()
    {
        return new NativeToolActionObservation(
            Game1.player.UsingTool,
            Game1.player.CanMove,
            Game1.player.canReleaseTool,
            Game1.player.FarmerSprite.PauseForSingleAnimation);
    }

    private void CaptureExecutorDiagnosticFrame(string phase)
    {
        if (!Context.IsWorldReady || Game1.player is null)
        {
            return;
        }

        var player = Game1.player;
        executorDiagnosticFrames.Add(new ExecutorDiagnosticFrame
        {
            Tick = executorInputTick,
            Phase = phase,
            Operation = CurrentExecutorDiagnosticOperation(),
            MovementOwner = executorMovementLease.Owner ?? string.Empty,
            MovementDirection = executorMovementLease.Direction,
            PixelX = player.Position.X,
            PixelY = player.Position.Y,
            TileX = player.TilePoint.X,
            TileY = player.TilePoint.Y,
            FacingDirection = player.FacingDirection,
            UsingTool = player.UsingTool,
            CanMove = player.CanMove,
            CanReleaseTool = player.canReleaseTool,
            PauseForSingleAnimation =
                player.FarmerSprite.PauseForSingleAnimation,
            MovementTransitionReason =
                executorMovementLease.LastTransitionReason
        });
    }

    private void WriteExecutorDiagnosticDump(string reason)
    {
        if (executorInputTick - lastExecutorDiagnosticDumpTick < 300 ||
            string.IsNullOrWhiteSpace(config.DiagnosticOutputPath))
        {
            return;
        }

        lastExecutorDiagnosticDumpTick = executorInputTick;
        try
        {
            var root = Path.GetFullPath(config.DiagnosticOutputPath);
            Directory.CreateDirectory(root);
            var safeReason = string.Concat(reason.Select(character =>
                char.IsLetterOrDigit(character) ? character : '_'));
            if (safeReason.Length > 80)
            {
                safeReason = safeReason[..80];
            }

            var path = Path.Combine(
                root,
                "executor-input-" +
                DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ") +
                "-" + safeReason + ".json");
            var payload = new
            {
                schema_version = "executor_input_diagnostic.v1",
                captured_at = DateTimeOffset.UtcNow.ToString("O"),
                trigger = reason,
                frame_capacity = executorDiagnosticFrames.Capacity,
                frames = executorDiagnosticFrames.Snapshot()
            };
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    payload,
                    new JsonSerializerOptions(JsonOptions)
                    {
                        WriteIndented = true
                    }));
            Monitor.Log(
                "Executor input diagnostic written to " + path,
                StardewModdingAPI.LogLevel.Warn);
        }
        catch (Exception ex)
        {
            Monitor.Log(
                "Executor input diagnostic write failed: " +
                ex.GetType().Name,
                StardewModdingAPI.LogLevel.Error);
        }
    }

    private string CurrentExecutorDiagnosticOperation()
    {
        if (activeTileMove is not null)
        {
            return activeTileMove.Pending.Request.OptionId;
        }

        if (activeNativeTool is not null)
        {
            return activeNativeTool.Pending.Request.OptionId;
        }

        if (activeClearObstacle is not null)
        {
            return activeClearObstacle.Pending.Request.OptionId;
        }

        if (activeVolcanoObstacle is not null)
        {
            return activeVolcanoObstacle.Pending.Request.OptionId;
        }

        if (activeVolcanoCombat is not null)
        {
            return activeVolcanoCombat.Pending.Request.OptionId;
        }

        if (activeCombatMonster is not null)
        {
            return activeCombatMonster.Pending.Request.OptionId;
        }

        return HasActiveExecutorOperation()
            ? "other_active_executor_operation"
            : "idle";
    }
}
