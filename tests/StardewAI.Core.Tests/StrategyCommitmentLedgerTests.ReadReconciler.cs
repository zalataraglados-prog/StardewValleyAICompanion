using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed partial class StrategyCommitmentLedgerTests
{
    [Fact]
    public void ReadReconcilerPreservesOriginalCropRelocationSupportOrderAndHistory()
    {
        var source = Snapshot(103, 1, "winter", 20);
        var current = new CropCommitmentLedgerService().Upsert(null, source, Request(source, 0, 10), "2026-11-08T00:00:00Z").Ledger!;
        current.MachineRelocationIntents = new[] { new MachineRelocationIntent
        {
            IntentId = "fixture.relocation", Revision = 1, SourceDecisionId = "fixture.relocation.owner",
            QualifiedItemId = "(BC)12", TargetLocationId = "Farm", TargetTileX = 7, TargetTileY = 5
        } };
        current.MachineSupportIntents = new[] { new MachineSupportIntent
        {
            IntentId = "fixture.support", Revision = 1, SourceDecisionId = "fixture.support.owner",
            Stage = MachineSupportIntentStages.PlacementBound, QualifiedItemId = "(BC)12",
            TargetLocationId = "Farm", TargetTileX = 7, TargetTileY = 5
        } };
        var after = Snapshot(140, 2, "summer", 1);
        after.State["farm"] = MachineSupportSnapshot(processing: true).State["farm"];
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var original = JsonSerializer.Serialize(current);
        const string at = "2026-11-08T01:00:00Z";
        var expected = new CropCommitmentLedgerService().ReconcileCompleted(current, after, at);
        expected = new MachineRelocationIntentLedgerService().ReconcileCompleted(expected, after, at);
        expected = new MachineSupportIntentLedgerService().ReconcileCompleted(expected, after, at);
        var actual = StrategyCommitmentReadReconciler.Reconcile(current, after, at);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(new[] { "complete", "machine_relocation_intent_complete", "machine_support_complete" },
            actual.History.Skip(current.History.Length).Select(row => row.Operation));
        Assert.Equal(original, JsonSerializer.Serialize(current));
    }
}
