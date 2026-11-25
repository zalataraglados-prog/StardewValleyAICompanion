using StardewValley;
using StardewValley.Menus;
using StardewAI.Contracts.Training;
using StardewAI.SharedNative;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteNativeNpcQuestionResponse(TrainingExecutionRequest request, DialogueBox menu)
    {
        var evidence = NativeNpcQuestionReader.Read(menu);
        var answer = evidence.BestAnswer();
        if (answer is null || evidence.Identity() != request.ExpectedDialogueKey ||
            answer.Key != request.DialogueResponseKey ||
            !string.IsNullOrEmpty(request.ExpectedShopId) || !string.IsNullOrEmpty(request.MinigameId))
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request),
                DialogueObservedEffect(), "native_npc_question_identity_or_effect_mismatch");
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null)
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request),
                DialogueObservedEffect(), "native_npc_question_input_not_ready");

        var dialogue = menu.characterDialogue;
        var beforePoints = Game1.player.friendshipData.TryGetValue(evidence.Speaker, out var beforeFriendship)
            ? beforeFriendship.Points : 0;
        var beforeSeen = Game1.player.DialogueQuestionsAnswered.Contains(answer.SeenId);
        var started = DateTimeOffset.UtcNow.ToString("O");
        // A native click first finishes typing, without selecting a response.
        // Both calls below are game-menu methods, never desktop mouse input.
        if (menu.characterIndexInDialogue < menu.getCurrentString().Length - 1)
        {
            var finishTyping = menu.responseCC[0].bounds.Center;
            menu.receiveLeftClick(finishTyping.X, finishTyping.Y);
        }
        if (!TryClickNativeDialogueResponse(menu, answer.Key, out var reason))
            return BlockedWithPrimitive(request, "choose_dialogue_response", DialogueRequestedEffect(request),
                DialogueObservedEffect(), reason);

        var afterPoints = Game1.player.friendshipData.TryGetValue(evidence.Speaker, out var afterFriendship)
            ? afterFriendship.Points : 0;
        var afterSeen = Game1.player.DialogueQuestionsAnswered.Contains(answer.SeenId);
        var observedChange = afterPoints - beforePoints;
        var effectWithinNativeClamping = observedChange >= Math.Min(0, answer.FriendshipChange) &&
            observedChange <= Math.Max(0, answer.FriendshipChange);
        var verified = !dialogue.isCurrentDialogueAQuestion() && afterSeen && effectWithinNativeClamping;
        var verification = verified
            ? new[] { "native_npc_response_key_selected", "native_question_consumed", "native_seen_response_observed", "native_friendship_effect_observed" }
            : new[] { "native_npc_question_response_not_verified" };
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "choose_dialogue_response",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = DialogueRequestedEffect(request) + ";nominal_friendship_change=" + answer.FriendshipChange,
            ObservedEffect = DialogueObservedEffect() + ";native_friendship_change=" + observedChange,
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "social." + evidence.Speaker + ".friendship_points", Before = beforePoints.ToString(), After = afterPoints.ToString() },
                new SimulatedFactChange { Path = "player.DialogueQuestionsAnswered:" + answer.SeenId, Before = beforeSeen.ToString(), After = afterSeen.ToString() }
            }
        };
    }
}
