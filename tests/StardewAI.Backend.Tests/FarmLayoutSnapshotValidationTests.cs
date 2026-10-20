using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class FarmLayoutSnapshotValidationTests
{
    [Fact]
    public void SnapshotValidatorAcceptsPurposeLimitedFarmLayoutProfile()
    {
        var snapshot = FarmLayoutSnapshot();

        var errors = SnapshotValidator.Validate(snapshot, "farm_layout");

        Assert.Empty(errors);
        Assert.Equal(
            FarmLayoutProfileDomains.OrderBy(value => value),
            snapshot.State.Keys.OrderBy(value => value));
    }

    [Theory]
    [InlineData("farm")]
    [InlineData("current_location")]
    [InlineData("locations")]
    public void SnapshotValidatorKeepsFarmLayoutProfileFailClosed(
        string omittedDomain)
    {
        var snapshot = FarmLayoutSnapshot(omittedDomain);

        var errors = SnapshotValidator.Validate(snapshot, "farm_layout");

        Assert.Contains("missing state domain: " + omittedDomain, errors);
    }

    private static SnapshotEnvelope FarmLayoutSnapshot(
        string? omittedDomain = null)
    {
        var state = FarmLayoutProfileDomains
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
            RealTimestamp = "2026-10-19T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static readonly string[] FarmLayoutProfileDomains =
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
        "locations"
    };
}
