using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("clock_reverse")]
    [InlineData("unknown_progress")]
    public void RecaptureDoesNotHideClockRewindOrOtherPersistentChanges(string change)
    {
        using var f = new Fixture();
        var observed = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Before, JsonOptions), JsonOptions)!;
        if (change == "clock_reverse")
        {
            var time = JsonNode.Parse(observed.State["time"].GetRawText())!;
            time["time"]!["value"] = 550;
            observed.State["time"] = JsonSerializer.SerializeToElement(time, JsonOptions);
            observed.InGameTime.Value = 550;
        }
        else
        {
            var progress = JsonNode.Parse(observed.State["world_progress"].GetRawText())!;
            progress["unrecognized_progress"] = true;
            observed.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
        }
        observed.GameTick++;
        observed.StateHash = SnapshotHash.ComputeStateHash(observed.State);
        f.Recapture(observed);
        Assert.Contains("runtime boundary", Assert.Throws<InvalidDataException>(() => f.Open()).Message);
    }

    [Fact]
    public async Task RuntimeOnlyRecaptureUsesOriginalMarkerWithoutChangingClaimSourceIdentities()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var oldHash = f.Before.StateHash;
        f.Recapture(MoneyCropTeacherFixture.CreateSnapshot("SeedAcquisitionWait", f.Before));
        Assert.NotEqual(oldHash, f.Before.StateHash);
        runtime.Store.Snapshots[f.Before.StateHash] = f.Before;
        var original = JsonSerializer.Serialize(f.Ledger, JsonOptions);
        var scope = f.Open();
        Assert.Equal(original, JsonSerializer.Serialize(scope.BaseLedger, JsonOptions));
        Assert.Equal(oldHash, scope.BaseLedger.SourceStateHash);
        Assert.Equal(4, scope.InputHashes.Count);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition);
        Assert.Equal(f.Before.StateHash, runtime.Repository.Get(f.Before).SourceStateHash);
        runtime.ObserveAfter();
        var final = await prepared.CompleteAsync(transition);
        AssertOtherRootsUnchanged(f.Ledger, final.Ledger);
    }

    [Fact]
    public void ChangedRuntimeSourceWithoutPriorEvidenceIsRejectedBeforeMarker()
    {
        using var f = new Fixture();
        f.Recapture(MoneyCropTeacherFixture.CreateSnapshot("SeedAcquisitionWait", f.Before));
        Assert.Contains("prior verified snapshot", Assert.Throws<InvalidDataException>(() =>
            CropTeacherSeedPurchaseSupport.Open(f.RequestPath, f.SourcePath, f.LedgerPath, Id, "fresh-marker")).Message);
    }

    [Fact]
    public void NativePurchaseBeforeSettlementCannotBeRelabeledAsRuntimeRecapture()
    {
        using var f = new Fixture();
        f.Transition();
        f.Recapture(f.After);
        Assert.Contains("runtime boundary", Assert.Throws<InvalidDataException>(() => f.Open()).Message);
    }
}
