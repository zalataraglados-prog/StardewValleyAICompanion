using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcCurrentSpouseDynamicTrackingResolverTests
{
    [Fact]
    public void EmptySchedulePlayerSpouseProducesIdentityBoundReplanDirective()
    {
        var result = new NpcCurrentSpouseDynamicTrackingResolver().Resolve(
            Snapshot(),
            "Abigail");

        Assert.Equal("pass", result.Status);
        var directive = Assert.IsType<CurrentSocialDynamicTrackingDirective>(
            result.Directive);
        Assert.Equal(("FarmHouse", 22, 24),
            (directive.ObservedLocationName, directive.ObservedTileX, directive.ObservedTileY));
        Assert.Equal(new[] { "social.talk_npc", "social.gift_npc" },
            directive.CandidateFamilies);
        Assert.Equal("live_npc_identity_rebind_each_snapshot", directive.TargetBindingMode);
    }

    [Fact]
    public void OrdinaryUnscheduledNpcCannotEnterSpouseTrackingPath()
    {
        var result = new NpcCurrentSpouseDynamicTrackingResolver().Resolve(
            Snapshot(isPlayerSpouse: false),
            "Abigail");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_spouse_dynamic_native_identity_contract_not_met",
            result.BlockingReasons);
    }

    [Fact]
    public void LoadedMarriageScheduleUsesNormalScheduleProjectionInstead()
    {
        var result = new NpcCurrentSpouseDynamicTrackingResolver().Resolve(
            Snapshot(scheduleLoaded: true),
            "Abigail");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_spouse_dynamic_runtime_schedule_not_empty",
            result.BlockingReasons);
    }

    [Fact]
    public void CurrentDaySnapshotProducesLiveSpouseTrackingDirective()
    {
        var result = new NpcCurrentSpouseDynamicTrackingResolver().Resolve(
            Snapshot(gameTime: 910),
            "Abigail");

        Assert.Equal("pass", result.Status);
        var directive = Assert.IsType<CurrentSocialDynamicTrackingDirective>(
            result.Directive);
        Assert.Equal(910, directive.ObservedAtTime);
        Assert.Equal("FarmHouse", directive.ObservedLocationName);
    }

    private static JsonElement Snapshot(
        bool isPlayerSpouse = true,
        bool scheduleLoaded = false,
        int gameTime = 600) => JsonSerializer.SerializeToElement(new
    {
        state = new
        {
            time = new
            {
                time = Field(gameTime)
            },
            player = new
            {
                spouse = Field("Abigail"),
                married_or_roommate = Field(true)
            },
            npcs = new
            {
                schedule_catalog = Field(new
                {
                    villagers = new[]
                    {
                        new
                        {
                            npc_name = "Abigail",
                            is_villager = true,
                            event_actor = false,
                            is_player_spouse = isPlayerSpouse,
                            currently_married = true,
                            current_location_name = "FarmHouse",
                            can_socialize_now = true,
                            can_receive_gifts_now = true
                        }
                    }
                }),
                schedules = Field(new[]
                {
                    new
                    {
                        name = "Abigail",
                        schedule_key = scheduleLoaded ? "marriage_Mon" : string.Empty,
                        follow_schedule = scheduleLoaded,
                        schedule_loaded = scheduleLoaded,
                        entries = scheduleLoaded
                            ? new[] { new { time = 800 } }
                            : Array.Empty<object>()
                    }
                }),
                positions = Field(new[]
                {
                    new
                    {
                        name = "Abigail",
                        location_id = "FarmHouse",
                        tile_x = 22,
                        tile_y = 24
                    }
                })
            }
        }
    });

    private static object Field<T>(T value) => new
    {
        status = "available",
        value
    };
}
