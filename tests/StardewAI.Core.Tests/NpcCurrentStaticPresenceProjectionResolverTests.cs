using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NpcCurrentStaticPresenceProjectionResolverTests
{
    [Fact]
    public void DayStartNpcWithoutMasterScheduleProducesExactStaticWindow()
    {
        var result = new NpcCurrentStaticPresenceProjectionResolver().Resolve(
            Snapshot(),
            "Wizard");

        Assert.Equal("pass", result.Status);
        Assert.Equal("ExactNativeStaticNoMasterSchedule", result.ProjectionStatus);
        var presence = Assert.IsType<NpcFuturePresenceWindowResolution>(
            result.Presence);
        var window = Assert.Single(presence.Windows);
        Assert.Equal(("WizardHouse", 3, 17),
            (window.LocationName, window.TileX, window.TileY));
        Assert.Equal((600, 2600),
            (window.WindowStartTime, window.WindowEndTimeExclusive));
    }

    [Fact]
    public void SpouseCannotBeMisclassifiedAsStatic()
    {
        var result = new NpcCurrentStaticPresenceProjectionResolver().Resolve(
            Snapshot(isSpouse: true),
            "Wizard");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_static_presence_native_no_schedule_contract_not_met",
            result.BlockingReasons);
    }

    [Fact]
    public void LoadedScheduleCannotBeMisclassifiedAsStatic()
    {
        var result = new NpcCurrentStaticPresenceProjectionResolver().Resolve(
            Snapshot(scheduleLoaded: true),
            "Wizard");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_static_presence_runtime_schedule_not_empty",
            result.BlockingReasons);
    }

    [Fact]
    public void PositionMustMatchTheNativeDefaultPosition()
    {
        var result = new NpcCurrentStaticPresenceProjectionResolver().Resolve(
            Snapshot(positionX: 4),
            "Wizard");

        Assert.Equal("blocked", result.Status);
        Assert.Contains(
            "current_static_presence_default_position_mismatch",
            result.BlockingReasons);
    }

    private static JsonElement Snapshot(
        bool isSpouse = false,
        bool scheduleLoaded = false,
        int positionX = 3) =>
        JsonSerializer.SerializeToElement(new
        {
            state = new
            {
                time = new
                {
                    time = Field(600)
                },
                npcs = new
                {
                    schedule_catalog = Field(new
                    {
                        villagers = new[]
                        {
                            new
                            {
                                npc_name = "Wizard",
                                is_villager = true,
                                event_actor = false,
                                is_player_spouse = isSpouse,
                                currently_married = isSpouse,
                                master_schedule_present = false,
                                master_schedule_entry_count = 0,
                                current_location_name = "WizardHouse",
                                default_map = "WizardHouse",
                                default_tile_x = 3,
                                default_tile_y = 17
                            }
                        }
                    }),
                    schedules = Field(new[]
                    {
                        new
                        {
                            name = "Wizard",
                            follow_schedule = scheduleLoaded,
                            schedule_loaded = scheduleLoaded,
                            entries = scheduleLoaded
                                ? new[] { new { time = 900 } }
                                : Array.Empty<object>()
                        }
                    }),
                    positions = Field(new[]
                    {
                        new
                        {
                            name = "Wizard",
                            location_id = "WizardHouse",
                            tile_x = positionX,
                            tile_y = 17
                        }
                    })
                }
            }
        });

    private static object Field(object value) => new
    {
        status = "available",
        value
    };
}
