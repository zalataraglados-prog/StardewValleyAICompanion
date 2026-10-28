using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class RecoverySnapshotValidationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly string[] RecoveryProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "current_location",
        "locations"
    };

    private static readonly string[] FishingRewardRecoveryProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport"
    };

    [Fact]
    public void ValidatorAcceptsExactPurposeLimitedRecoveryProfile()
    {
        var errors = SnapshotValidator.ValidateRaw(
            SnapshotJson(),
            out var snapshot,
            "recovery");

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.Equal(
            RecoveryProfileDomains.OrderBy(value => value),
            snapshot!.State.Keys.OrderBy(value => value));
    }

    [Theory]
    [InlineData("menus")]
    [InlineData("locations")]
    public void ValidatorKeepsRecoveryProfileFailClosed(
        string omittedDomain)
    {
        var errors = SnapshotValidator.ValidateRaw(
            SnapshotJson(omittedDomain),
            out _,
            "recovery");

        Assert.Contains("missing state domain: " + omittedDomain, errors);
    }

    [Fact]
    public void ValidatorAcceptsExactFishingRewardRecoveryProfile()
    {
        var errors = SnapshotValidator.ValidateRaw(
            SnapshotJson(
                FishingRewardRecoveryProfileDomains,
                omittedDomain: null),
            out var snapshot,
            "fishing_reward_recovery");

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.Equal(
            FishingRewardRecoveryProfileDomains.OrderBy(value => value),
            snapshot!.State.Keys.OrderBy(value => value));
    }

    [Theory]
    [InlineData("player")]
    [InlineData("menus")]
    public void ValidatorKeepsFishingRewardRecoveryProfileFailClosed(
        string omittedDomain)
    {
        var errors = SnapshotValidator.ValidateRaw(
            SnapshotJson(
                FishingRewardRecoveryProfileDomains,
                omittedDomain),
            out _,
            "fishing_reward_recovery");

        Assert.Contains("missing state domain: " + omittedDomain, errors);
    }

    private static string SnapshotJson(string? omittedDomain = null) =>
        SnapshotJson(RecoveryProfileDomains, omittedDomain);

    private static string SnapshotJson(
        IEnumerable<string> domains,
        string? omittedDomain)
    {
        var state = domains
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
            GameTick = 1,
            RealTimestamp = "2026-10-29T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new
        {
            kind = "test",
            path = "test"
        },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1.0
    };
}
