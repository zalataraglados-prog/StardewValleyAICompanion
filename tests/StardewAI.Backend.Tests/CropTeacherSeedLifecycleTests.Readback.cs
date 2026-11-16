using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullReadbackReplaysOriginalRepositoryCompletionWithoutChangingMarker(bool due)
    {
        using var f = new Fixture(due: due);
        var before = JsonSerializer.Serialize(f.Ledger, JsonOptions);
        var observed = f.Repository.Get(f.Snapshot);
        RequireReadback(f.Ledger, observed, f.Snapshot);
        Assert.Equal(before, JsonSerializer.Serialize(f.Ledger, JsonOptions));
        Assert.Equal(due ? "completed" : "active", observed.CropPlantingCommitments.Single().Status);
    }

    [Theory]
    [InlineData("foreign_money")]
    [InlineData("missing_history")]
    [InlineData("revision")]
    [InlineData("source")]
    [InlineData("premature_completion")]
    public void FullReadbackRejectsAnythingBeyondOriginalReadReconciliation(string fault)
    {
        using var f = new Fixture(due: fault != "premature_completion");
        var observed = JsonSerializer.SerializeToNode(f.Repository.Get(f.Snapshot), JsonOptions)!.AsObject();
        switch (fault)
        {
            case "foreign_money": observed["currency_reservations"]![0]!["amount"] = 999; break;
            case "missing_history": observed["history"]!.AsArray().RemoveAt(observed["history"]!.AsArray().Count - 1); break;
            case "revision": observed["revision"] = 999; break;
            case "source": observed["source_state_hash"] = "unrelated-source"; break;
            case "premature_completion": observed["crop_planting_commitments"]![0]!["status"] = "completed"; break;
        }
        Assert.Throws<InvalidDataException>(() => RequireReadback(f.Ledger,
            observed.Deserialize<StrategyCommitmentLedger>(JsonOptions)!, f.Snapshot));
    }

    private static void RequireReadback(StrategyCommitmentLedger committed, StrategyCommitmentLedger observed,
        SnapshotEnvelope snapshot)
    {
        var method = typeof(StrategyLedgerEvidenceStore).GetMethod("RequireReconciledReadback",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        try { method.Invoke(null, [committed, observed, snapshot, "declared original readback"]); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
    }

    [Theory]
    [InlineData("state_hash")]
    [InlineData("save_id")]
    [InlineData("player_id")]
    public void FullReadbackBindsTheExactSnapshotIdentity(string field)
    {
        using var f = new Fixture();
        var snapshot = JsonSerializer.SerializeToNode(f.Snapshot, JsonOptions)!.AsObject();
        if (field == "state_hash") snapshot[field] = "unrelated-source";
        else snapshot[field]!["value"] = "unrelated-identity";
        Assert.Throws<InvalidDataException>(() => RequireReadback(f.Ledger, f.Repository.Get(f.Snapshot),
            snapshot.Deserialize<SnapshotEnvelope>(JsonOptions)!));
    }
}
