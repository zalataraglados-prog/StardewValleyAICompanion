using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class GoldenWalnutFreshSaveEpisodeContinuityVerifierTests
{
    [Fact]
    public void AcceptsObservedPausedStoryEventHandoff()
    {
        var previousAfter = Snapshot("after-story");
        var currentBefore = Snapshot("before-watering");

        GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(
            "previous.json",
            PreviousEpisode("executor.advance_story_event"),
            previousAfter,
            "current.json",
            CurrentEpisode(),
            currentBefore);
    }

    [Fact]
    public void RejectsSameGapAfterOrdinaryAction()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(
                "previous.json",
                PreviousEpisode("executor.water_crop"),
                Snapshot("after-story"),
                "current.json",
                CurrentEpisode(),
                Snapshot("before-watering")));

        Assert.Contains(
            "exceeds the bounded observation window",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPausedStoryEventHandoffWithInventoryChange()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(
                "previous.json",
                PreviousEpisode("executor.advance_story_event"),
                Snapshot("after-story", inventoryItemId: "(O)472"),
                "current.json",
                CurrentEpisode(),
                Snapshot("before-watering", inventoryItemId: "(O)473")));

        Assert.Contains(
            "exceeds the bounded observation window",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPausedStoryEventHandoffWithPlayerMovement()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(
                "previous.json",
                PreviousEpisode("executor.advance_story_event"),
                Snapshot("after-story", tileX: 64),
                "current.json",
                CurrentEpisode(),
                Snapshot("before-watering", tileX: 65)));

        Assert.Contains(
            "exceeds the bounded observation window",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsPausedStoryEventHandoffBeyondDedicatedBound()
    {
        var current = CurrentEpisode();
        current.BeforeGameTick = 143968;

        var error = Assert.Throws<InvalidDataException>(() =>
            GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(
                "previous.json",
                PreviousEpisode("executor.advance_story_event"),
                Snapshot("after-story"),
                "current.json",
                current,
                Snapshot("before-watering")));

        Assert.Contains(
            "exceeds the bounded observation window",
            error.Message,
            StringComparison.Ordinal);
    }

    private static PlanExecutionEpisodeEnvelope PreviousEpisode(
        string optionId) => new()
        {
            AfterStateHash = "after-story",
            AfterGameTick = 143607,
            OptionId = optionId,
            Status = "applied",
            Success = true,
            PrimitiveVerificationStatus = "verified"
        };

    private static PlanExecutionEpisodeEnvelope CurrentEpisode() => new()
    {
        SourceStateHash = "before-watering",
        BeforeGameTick = 143942,
        OptionId = "executor.water_crop",
        Status = "applied",
        Success = true,
        PrimitiveVerificationStatus = "verified"
    };

    private static SnapshotEnvelope Snapshot(
        string hash,
        int tileX = 64,
        string inventoryItemId = "(O)472") => new()
    {
        SaveId = Available<string?>("ProofFarm_452924491"),
        PlayerId = Available<string?>("training-farmer"),
        InGameTime = Available<int?>(600),
        StateHash = hash,
        State = new Dictionary<string, JsonElement>
        {
            ["time"] = JsonSerializer.SerializeToElement(new
            {
                total_days = AvailableValue(19)
            }),
            ["player"] = JsonSerializer.SerializeToElement(new
            {
                location_id = AvailableValue("Farm"),
                tile_x = AvailableValue(tileX),
                tile_y = AvailableValue(15),
                money = AvailableValue(403),
                energy = AvailableValue(270),
                health = AvailableValue(100),
                current_tool = AvailableValue("(T)WateringCan"),
                inventory = AvailableValue(new[]
                {
                    new { qualified_item_id = inventoryItemId, stack = 1 }
                })
            }),
            ["locations"] = JsonSerializer.SerializeToElement(new
            {
                ginger_island_access = AvailableValue(new { })
            }),
            ["world_progress"] = JsonSerializer.SerializeToElement(new
            {
                golden_walnut_sources = AvailableValue(new
                {
                    sources = Array.Empty<object>()
                })
            })
        }
    };

    private static FieldEnvelope<T> Available<T>(T value) => new()
    {
        Value = value,
        Status = FieldStatus.Available
    };

    private static object AvailableValue<T>(T value) => new
    {
        value,
        status = "available"
    };
}
