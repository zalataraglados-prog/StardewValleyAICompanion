using System.Text.Json;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed class FishingSnapshotValidationTests
{
    [Fact]
    public void SnapshotValidatorAcceptsPurposeLimitedFishingProfile()
    {
        var errors = SnapshotValidator.ValidateRaw(
            PurposeLimitedFishingSnapshotJson(),
            out var snapshot,
            "fishing");

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.Equal(
            FishingProfileDomains.OrderBy(value => value),
            snapshot!.State.Keys.OrderBy(value => value));
    }

    [Fact]
    public void SnapshotValidatorKeepsPurposeLimitedFishingProfileFailClosed()
    {
        var errors = SnapshotValidator.ValidateRaw(
            PurposeLimitedFishingSnapshotJson("fishing"),
            out _,
            "fishing");

        Assert.Contains("missing state domain: fishing", errors);
    }

    private static string PurposeLimitedFishingSnapshotJson(
        string? omittedDomain = null)
    {
        var state = FishingProfileDomains
            .Where(domain => domain != omittedDomain)
            .ToDictionary(
                domain => domain,
                _ => JsonSerializer.SerializeToElement(
                    new Dictionary<string, object>
                    {
                        ["marker"] = Field("available")
                    },
                    JsonOptions),
                StringComparer.Ordinal);
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "test",
            GameTick = 812,
            RealTimestamp = "2026-08-09T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static object Field(object value)
    {
        return new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 812,
            confidence = 1.0
        };
    }

    private static readonly string[] FishingProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "farm",
        "current_location",
        "locations",
        "fishing",
        "npcs",
        "quests",
        "world_progress",
        "mods",
        "modded_state"
    };

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
