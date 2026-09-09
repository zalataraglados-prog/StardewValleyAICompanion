using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class SocialSnapshotValidationTests
{
    [Theory]
    [InlineData("social")]
    [InlineData("social_future")]
    public void SnapshotValidatorAcceptsPurposeLimitedSocialProfiles(
        string profile)
    {
        var snapshot = SocialSnapshot();

        var errors = SnapshotValidator.Validate(snapshot, profile);

        Assert.Empty(errors);
        Assert.Equal(
            SocialProfileDomains.OrderBy(value => value),
            snapshot.State.Keys.OrderBy(value => value));
    }

    [Theory]
    [InlineData("social", "npcs")]
    [InlineData("social_future", "locations")]
    [InlineData("social_future", "world_progress")]
    public void SnapshotValidatorKeepsSocialProfilesFailClosed(
        string profile,
        string omittedDomain)
    {
        var snapshot = SocialSnapshot(omittedDomain);

        var errors = SnapshotValidator.Validate(snapshot, profile);

        Assert.Contains("missing state domain: " + omittedDomain, errors);
    }

    private static SnapshotEnvelope SocialSnapshot(string? omittedDomain = null)
    {
        var state = SocialProfileDomains
            .Where(domain => domain != omittedDomain)
            .ToDictionary(
                domain => domain,
                _ => JsonSerializer.SerializeToElement(
                    new Dictionary<string, object>
                    {
                        ["marker"] = new
                        {
                            value = "available",
                            status = "available",
                            source = new
                            {
                                kind = "runtime",
                                path = "test"
                            },
                            adapter = "test",
                            read_at_tick = 812,
                            confidence = 1.0
                        }
                    }),
                StringComparer.Ordinal);
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "test",
            GameTick = 812,
            RealTimestamp = "2026-09-06T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static readonly string[] SocialProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "current_location",
        "locations",
        "npcs",
        "quests",
        "world_progress"
    };
}
