using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class VolcanoSnapshotValidationTests
{
    [Fact]
    public void SnapshotValidatorAcceptsPurposeLimitedVolcanoProfile()
    {
        var snapshot = VolcanoSnapshot();

        var errors = SnapshotValidator.Validate(snapshot, "volcano");

        Assert.Empty(errors);
        Assert.Equal(
            VolcanoProfileDomains.OrderBy(value => value),
            snapshot.State.Keys.OrderBy(value => value));
    }

    [Fact]
    public void SnapshotValidatorKeepsPurposeLimitedVolcanoProfileFailClosed()
    {
        var snapshot = VolcanoSnapshot(omittedDomain: "volcano");

        var errors = SnapshotValidator.Validate(snapshot, "volcano");

        Assert.Contains("missing state domain: volcano", errors);
    }

    private static SnapshotEnvelope VolcanoSnapshot(
        string? omittedDomain = null)
    {
        var state = VolcanoProfileDomains
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
            RealTimestamp = "2026-07-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static readonly string[] VolcanoProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "volcano"
    };
}
