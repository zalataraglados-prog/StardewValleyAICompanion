using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupStoryEventFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        var location = Game1.currentLocation;
        if (!Context.IsWorldReady || location is null)
            return StoryEventFixtureBlocked(request, "story_event_fixture_world_unavailable");
        if (Game1.eventUp || Game1.CurrentEvent is not null || Game1.currentMinigame is not null)
            return StoryEventFixtureBlocked(request, "story_event_fixture_requires_clear_event_boundary");

        var profile = request.StoryEventBoundaryKind;
        var eventId = profile switch
        {
            "automatic_fixture" => "EVD322Automatic",
            "choice_fixture" => "EVD322Choice",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(eventId))
            return StoryEventFixtureBlocked(request, "story_event_fixture_profile_invalid");

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        StopAllMovement();
        var body = profile == "choice_fixture"
            ? "question EVD322Question \"Choose a branch#First#Second\"/message \"EVD-322 choice complete\"/end"
            : "message \"EVD-322 automatic text\"/message \"EVD-322 automatic complete\"/end";
        var script = "none/follow/farmer 0 0 2/" + body;
        var nativeEvent = new Event(
            script,
            "StardewAI.RuntimeTestHarness:story_event_fixture",
            eventId,
            Game1.player);
        location.startEvent(nativeEvent);

        var verified = Game1.eventUp && ReferenceEquals(Game1.CurrentEvent, nativeEvent) &&
            string.Equals(nativeEvent.id, eventId, StringComparison.Ordinal) &&
            nativeEvent.eventCommands.Length == 6;
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
            PrimitiveKind = "debug_setup_story_event",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_only_synthetic_event_started_through_GameLocation_startEvent",
                    "native_Event_parser_owns_initialization_and_command_progress"
                }
                : new[] { "story_event_fixture_post_state_mismatch" },
            RequestedEffect = "story_event_fixture=" + profile,
            ObservedEffect = "event_id=" + (Game1.CurrentEvent?.id ?? "none") +
                ";event_up=" + Game1.eventUp.ToString().ToLowerInvariant() +
                ";command_count=" + nativeEvent.eventCommands.Length,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "story_event_fixture_post_state_mismatch" }
        };
    }

    private static TrainingExecutionResult StoryEventFixtureBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "debug_setup_story_event",
            "story_event_fixture=ready",
            "story_event_fixture=blocked",
            reasons);
}
