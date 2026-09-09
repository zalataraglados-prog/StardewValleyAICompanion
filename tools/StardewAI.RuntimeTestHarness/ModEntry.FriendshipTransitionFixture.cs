using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Characters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFriendshipTransitionFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        const string npcName = "Linus";
        const int points = 1000;
        var npc = Game1.getCharacterFromName(npcName);
        if (npc is null || !npc.IsVillager || npc is Child)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_friendship_transition_fixture",
                "friendship_transition_fixture=ready",
                "npc=missing_or_ineligible",
                "friendship_transition_fixture_target_unavailable");
        }

        var friendship = Game1.player.friendshipData.TryGetValue(npcName, out var existing)
            ? existing
            : Game1.player.friendshipData[npcName] = new Friendship();
        friendship.Clear();
        friendship.Points = points;
        friendship.Status = FriendshipStatus.Friendly;
        friendship.TalkedToToday = true;
        friendship.GiftsToday = 1;
        friendship.GiftsThisWeek = 2;

        var previousWeek = new WorldDate(Game1.Date);
        previousWeek.TotalDays = Math.Max(0, Game1.Date.TotalDays - 7);
        friendship.LastGiftDate = previousWeek;

        var verified = friendship.Points == points &&
            friendship.Status == FriendshipStatus.Friendly &&
            friendship.TalkedToToday &&
            friendship.GiftsToday == 1 &&
            friendship.GiftsThisWeek == 2 &&
            friendship.LastGiftDate?.TotalSundayWeeks != Game1.Date.TotalSundayWeeks;
        var observed = "npc=" + npcName +
            ";points=" + friendship.Points +
            ";talked_to_today=" + friendship.TalkedToToday.ToString().ToLowerInvariant() +
            ";gifts_today=" + friendship.GiftsToday +
            ";gifts_this_week=" + friendship.GiftsThisWeek +
            ";last_gift_total_days=" + friendship.LastGiftDate?.TotalDays +
            ";last_gift_total_sunday_weeks=" + friendship.LastGiftDate?.TotalSundayWeeks +
            ";current_total_sunday_weeks=" + Game1.Date.TotalSundayWeeks;

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
            PrimitiveKind = "debug_setup_friendship_transition_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_native_friendship_row_ready",
                    "daily_reset_branch_ready",
                    "weekly_two_gift_bonus_branch_ready"
                }
                : new[] { "friendship_transition_fixture_postcondition_mismatch" },
            RequestedEffect = "friendship_transition_fixture=ready",
            ObservedEffect = observed,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "friendship_transition_fixture_postcondition_mismatch" },
            SocialNpcName = npcName,
            SocialFriendshipPointsBefore = friendship.Points,
            SocialTalkedToTodayBefore = friendship.TalkedToToday,
            SocialGiftsTodayBefore = friendship.GiftsToday,
            SocialGiftsThisWeekBefore = friendship.GiftsThisWeek
        };
    }
}
