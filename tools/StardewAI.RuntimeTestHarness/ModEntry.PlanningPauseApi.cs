namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    public override object GetApi() =>
        new StardewAI.RuntimePrimitives.ExecutorPlanningPauseReadApi(ReadPlanningPauseEvidence);

    private string ReadPlanningPauseEvidence()
    {
        var available = StardewModdingAPI.Context.IsWorldReady &&
            config.FreezeClockWhileExecutorIdle && executorIdlePauseApplied &&
            StardewValley.Game1.paused && pendingExecutions.IsEmpty &&
            StardewValley.Game1.activeClickableMenu is null &&
            !HasActiveExecutorOperation() && executorMovementLease.Direction is null &&
            !HasPendingNativeEndOfNightTransition(out _);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            schema_version = "executor_owned_idle_pause.v1",
            owner_mod_id = "StardewAI.RuntimeTestHarness",
            applied = available,
            release_policy = "release_before_pending_execution_update",
            player_id = available
                ? StardewValley.Game1.player.UniqueMultiplayerID.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null,
            game_unique_id = available
                ? StardewValley.Game1.uniqueIDForThisGame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null,
            pause_epoch = executorIdlePauseEpoch
        });
    }
}
