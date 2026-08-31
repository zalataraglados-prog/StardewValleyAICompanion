using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPlayerEmoteFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var chat = Game1.chatBox;
        if (chat is null) reasons.Add("emote_fixture_chat_box_unavailable");
        if (reasons.Count > 0)
            return BlockedWithPrimitive(request, "debug_setup_player_emote", "emote_fixture=ready",
                "emote_fixture=blocked", reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.farmEvent = null;
        Game1.currentMinigame = null;
        if (Game1.currentLocation is not null) Game1.currentLocation.currentEvent = null;
        chat!.clickAway();
        StopAllMovement();
        Game1.player.completelyStopAnimatingOrDoingAction();
        Game1.player.isEmoting = false;
        Game1.player.isEmoteAnimating = false;
        Game1.player.usingSlingshot = false;
        Game1.player.isEating = false;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.bathingClothes.Value = false;
        Game1.player.performedEmotes.Clear();

        var projection = ReadLivePlayerEmoteProjection();
        var verified = projection is not null && projection.ServiceStatus == "ready" &&
            projection.Emotes.Length == 22 && projection.Emotes.Count(option => option.Hidden) == 4 &&
            projection.Emotes.All(option => option.NativeCommandAccepted && !option.PerformedEntryPresent) &&
            projection.RawFavorites.Length == Game1.player.emoteFavorites.Count &&
            projection.EffectiveFavorites.Length == 8;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_player_emote",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_emote_fixture_ready", "exact_22_entry_catalog_and_four_hidden_rows_observed", "no_GetEmoteFavorites_read_mutation_observed" }
                : new[] { "emote_fixture_receipt_mismatch" },
            RequestedEffect = "emote_fixture=ready",
            ObservedEffect = "catalog_count=" + projection?.Emotes.Length + ";service=" + projection?.ServiceStatus,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "emote_fixture_receipt_mismatch" }
        };
    }
}
