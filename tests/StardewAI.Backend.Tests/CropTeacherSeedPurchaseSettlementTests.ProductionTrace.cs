using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ProductionTraceRetainsTheOriginalAcceptedCrossDayChainWithoutNativeOrLedgerWork()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var harvest = await f.PrepareHarvest(owner);
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        var reads = f.Shared.Runtime.Handler.LedgerReads;
        var trace = await owner.ReadProductionTraceAsync();
        Assert.Equal(new[] { "watering_maintenance", "supporting_route", "overnight_control", "supporting_route", "crop_harvest" },
            trace.Transitions.Select(row => row.OperationKind));
        Assert.Equal(Enumerable.Range(1, 5), trace.Transitions.Select(row => row.OwnerSequence));
        Assert.Equal("native_day_rollover", trace.Transitions[2].NativeTransition.TransitionKind);
        Assert.Equal(f.HarvestAfter, trace.AcceptedSnapshotPath);
        Assert.Equal(owner.CurrentLedgerPath, trace.AcceptedLedgerPath);
        trace.ValidateInputsUnchanged();
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ReadProductionTraceAsync());
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        var restored = await reopened.ReadProductionTraceAsync();
        Assert.Equal(trace.Transitions, restored.Transitions);
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.NativeNightCalls); Assert.Equal(1, f.NativeHarvestCalls);
        Assert.True(f.Shared.Runtime.Handler.LedgerReads >= reads);
        var output = Path.Combine(f.Shared.Files.OwnerRoot, "production-terminal");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => reopened.BuildProductionEvidenceAsync(output));
        Assert.Contains("no accepted original planting boundary", error.Message);
        Assert.False(Directory.Exists(output));
    }

    [Theory]
    [InlineData("acceptance")]
    [InlineData("receipt")]
    [InlineData("snapshot")]
    public async Task ProductionTraceRejectsHistoricalTamperingEvenWhenTheLastHeadIsUnchanged(string fault)
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var trace = await owner.ReadProductionTraceAsync();
        var first = trace.Transitions[0].NativeTransition;
        var path = fault switch
        {
            "acceptance" => Path.Combine(f.Shared.Files.OwnerRoot, "operation-000001-accepted.json"),
            "receipt" => first.ExecutionReceiptPath,
            _ => first.BeforeSnapshotPath
        };
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReadProductionTraceAsync());
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ProductionTraceDoesNotTurnAnUnknownNativeClaimIntoAnAcceptedTransition()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await f.Prepare(owner);
        await Assert.ThrowsAsync<IOException>(() => owner.ExecuteOriginalRouteAsync(operation, f.Runtime.Client,
            () => throw new IOException("unknown original native outcome")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReadProductionTraceAsync());
        Assert.NotNull(owner.Pending); Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ProductionTraceBindsTheOriginalPurchaseScalarProofInsteadOfInventingPlanting()
    {
        using var f = new Fixture(otherSeeds: 39);
        using var runtime = new RuntimeFixture(f);
        using var inputs = new OwnerInputs(f);
        using var files = new OwnerFiles(f, runtime, inputs);
        var transition = f.Transition(); runtime.LiveOptions.RunId = transition.RunId;
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        var operation = await owner.AdmitAsync(step, f.SourcePath, f.LedgerPath, files.Native);
        var prepared = await runtime.Prepare(transition, selectedScope: operation.Scopes.Purchase, runtimeDirectory: operation.RuntimeDirectory);
        await owner.PinPreparedAsync(operation, prepared);
        runtime.ObserveAfter(); await prepared.CompleteAsync(transition);
        await owner.AcceptLiveAsync(operation, prepared);
        var trace = await owner.ReadProductionTraceAsync();
        Assert.Equal(transition, Assert.Single(trace.Transitions).NativeTransition);
        Assert.NotEqual("owned_seed_planting", trace.Transitions[0].OperationKind);
        Assert.Equal(2, runtime.Handler.Commits); Assert.Equal(1, runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("owner")]
    [InlineData("reserved")]
    public async Task ProductionEvidenceRejectsUnownedOrReservedOutputsBeforeCreatingAnything(string kind)
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var root = f.Files.OwnerRoot;
        var output = kind switch { "outside" => root + "-unowned", "owner" => root, _ => Path.Combine(root, "operation-overlap") };
        await Assert.ThrowsAsync<IOException>(() => owner.BuildProductionEvidenceAsync(output));
        if (kind != "owner") Assert.False(Directory.Exists(output));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }
}
