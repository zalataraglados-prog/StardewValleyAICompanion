using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FriendshipDayTransitionSnapshotAuditorTests
{
    [Fact]
    public void ExactOneDayTransitionMatchesNativeObservedState()
    {
        var before = Snapshot(15, 16, 100, giftsToday: 1, talked: false);
        var after = Snapshot(16, 17, 98, giftsToday: 0, talked: false);

        var report = new FriendshipDayTransitionSnapshotAuditor().Audit(before, after);

        Assert.Equal("pass", report.Status);
        Assert.Equal(2, report.VerifiedNpcCount);
        Assert.Equal(1, report.VerifiedFriendshipRowCount);
        Assert.Equal(0, report.MismatchCount);
        Assert.Contains(Assert.Single(report.Rows, row => row.NpcName == "Abigail").PointTransitionReasons,
            reason => reason == "ordinary_not_talked_below_2000");
    }

    [Fact]
    public void ObservedPointMismatchBlocksAudit()
    {
        var before = Snapshot(15, 16, 100, giftsToday: 1, talked: false);
        var after = Snapshot(16, 17, 99, giftsToday: 0, talked: false);

        var report = new FriendshipDayTransitionSnapshotAuditor().Audit(before, after);

        Assert.Equal("blocked", report.Status);
        Assert.Equal(1, report.MismatchCount);
        var row = Assert.Single(report.Rows, value => value.NpcName == "Abigail");
        Assert.Contains("friendship_points_mismatch", row.Issues);
    }

    private static JsonElement Snapshot(
        int currentDays,
        int nextDays,
        int points,
        int giftsToday,
        bool talked)
    {
        var rows = new object[]
        {
            new
            {
                npc_name = "Abigail", is_villager = true, event_actor = false,
                is_child = false, friendship_row_exists = true, friendship_points = (int?)points,
                is_datably_flagged = true, is_npc_married = false, is_player_spouse = false,
                is_dating = false, is_divorced = false, talked_to_today = talked,
                gifts_today = (int?)giftsToday, gifts_this_week = (int?)1,
                last_gift_date_total_days = (int?)15,
                last_gift_date_total_sunday_weeks = (int?)2,
                speaks_dwarvish = false, maximum_hearts = 8
            },
            new
            {
                npc_name = "Mister Qi", is_villager = true, event_actor = false,
                is_child = false, friendship_row_exists = false, friendship_points = (int?)null,
                is_datably_flagged = false, is_npc_married = false, is_player_spouse = false,
                is_dating = false, is_divorced = false, talked_to_today = false,
                gifts_today = (int?)null, gifts_this_week = (int?)null,
                last_gift_date_total_days = (int?)null,
                last_gift_date_total_sunday_weeks = (int?)null,
                speaks_dwarvish = false, maximum_hearts = 10
            },
            new
            {
                npc_name = "Mister Qi", is_villager = true, event_actor = false,
                is_child = false, friendship_row_exists = false, friendship_points = (int?)null,
                is_datably_flagged = false, is_npc_married = false, is_player_spouse = false,
                is_dating = false, is_divorced = false, talked_to_today = false,
                gifts_today = (int?)null, gifts_this_week = (int?)null,
                last_gift_date_total_days = (int?)null,
                last_gift_date_total_sunday_weeks = (int?)null,
                speaks_dwarvish = false, maximum_hearts = 10
            }
        };
        var progress = new
        {
            projection_status = "complete_live_native_iteration",
            day_transition_inputs_status = "complete_live_native_fields",
            current_total_days = currentDays,
            current_total_sunday_weeks = 2,
            next_total_days = nextDays,
            next_total_sunday_weeks = 2,
            player_has_friendship_book = false,
            player_can_understand_dwarves = true,
            eligible_villager_rows = rows
        };
        return JsonSerializer.SerializeToElement(new
        {
            state = new
            {
                npcs = new
                {
                    grandpa_friendship_progress = new { status = "available", value = progress }
                }
            }
        });
    }
}
