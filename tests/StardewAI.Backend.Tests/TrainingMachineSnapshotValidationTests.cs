using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class TrainingMachineSnapshotValidationTests
{
    [Fact]
    public void SnapshotValidatorAcceptsPurposeLimitedTrainingMachineProfile()
    {
        var snapshot = TrainingMachineSnapshot();

        var errors = SnapshotValidator.Validate(
            snapshot,
            "training_machine");

        Assert.Empty(errors);
        Assert.Equal(
            TrainingMachineProfileDomains.OrderBy(value => value),
            snapshot.State.Keys.OrderBy(value => value));
    }

    [Fact]
    public void SnapshotValidatorKeepsTrainingMachineProfileFailClosed()
    {
        var snapshot = TrainingMachineSnapshot(omittedDomain: "farm");

        var errors = SnapshotValidator.Validate(
            snapshot,
            "training_machine");

        Assert.Contains("missing state domain: farm", errors);
    }

    private static SnapshotEnvelope TrainingMachineSnapshot(
        string? omittedDomain = null)
    {
        var state = TrainingMachineProfileDomains
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
            RealTimestamp = "2026-08-04T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static readonly string[] TrainingMachineProfileDomains =
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
        "npcs",
        "quests",
        "world_progress",
        "mods",
        "modded_state"
    };
}
