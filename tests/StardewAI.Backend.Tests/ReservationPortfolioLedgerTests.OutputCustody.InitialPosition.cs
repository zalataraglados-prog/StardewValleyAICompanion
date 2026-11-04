using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void CustodyCreationBindsInitialPositionAtTheOriginalAtomicRevision()
    {
        var fixture = MintMuseumCustody();
        var row = fixture.Ledger.MaterialReservations.Single(value => value.OutputCustody is not null);
        var initial = Assert.IsType<MaterialOutputCustodyInitialPosition>(row.OutputCustody!.InitialPosition);
        var creation = fixture.Ledger.History.Single(value => value.Operation == MaterialOutputCustodyContract.CreatedOperation);
        Assert.Equal(2, fixture.Ledger.Revision);
        Assert.Equal(1, initial.ReservationRevision);
        Assert.Equal(row.NodeId, initial.NodeId);
        Assert.Equal(row.SlotIndex, initial.SlotIndex);
        Assert.Equal(fixture.Snapshot.StateHash, initial.SourceStateHash);
        Assert.Equal(MaterialOutputCustodyContract.ComputeInitialPositionSha256(row, fixture.Ledger, initial), initial.BindingSha256);
        Assert.Equal(initial.BindingSha256, creation.OutputCustodyInitialPositionSha256);
        Assert.Equal(creation.SourceStateHash, initial.SourceStateHash);
        Assert.Equal(creation.CommitmentRevision, initial.ReservationRevision);
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(row, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
    }

    [Theory]
    [InlineData("node")]
    [InlineData("slot")]
    [InlineData("state")]
    [InlineData("revision")]
    [InlineData("schema")]
    [InlineData("hash")]
    [InlineData("missing-origin")]
    [InlineData("missing-anchor")]
    [InlineData("history-state")]
    [InlineData("history-revision")]
    [InlineData("goal")]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("current-node")]
    [InlineData("current-slot")]
    [InlineData("current-state")]
    [InlineData("current-revision")]
    [InlineData("rehashed-position")]
    public void CustodyCannotInventItsInitialOrCurrentPosition(string mutation)
    {
        var fixture = MintMuseumCustody();
        var row = fixture.Ledger.MaterialReservations.Single(value => value.OutputCustody is not null);
        var initial = row.OutputCustody!.InitialPosition!;
        var creation = fixture.Ledger.History.Single(value => value.Operation == MaterialOutputCustodyContract.CreatedOperation);
        switch (mutation)
        {
            case "node": initial.NodeId = "player:999"; break;
            case "slot": initial.SlotIndex++; break;
            case "state": initial.SourceStateHash = "different-source"; break;
            case "revision": initial.ReservationRevision++; break;
            case "schema": initial.SchemaVersion = "unknown"; break;
            case "hash": initial.BindingSha256 = new string('0', 64); break;
            case "missing-origin": row.OutputCustody.InitialPosition = null; break;
            case "missing-anchor": creation.OutputCustodyInitialPositionSha256 = null; break;
            case "history-state": creation.SourceStateHash = "different-source"; break;
            case "history-revision": creation.CommitmentRevision++; break;
            case "goal": row.GoalId = "other.goal"; break;
            case "owner": row.OwnerPlayerId++; break;
            case "purpose": row.Purpose = "different-purpose"; break;
            case "current-node": row.NodeId = "chest:Farm:4,5"; break;
            case "current-slot": row.SlotIndex++; break;
            case "current-state": row.SourceStateHash = "unverified-new-state"; break;
            case "current-revision": row.Revision++; break;
            case "rehashed-position":
                initial.SlotIndex++;
                initial.BindingSha256 = MaterialOutputCustodyContract.ComputeInitialPositionSha256(row, fixture.Ledger, initial);
                creation.OutputCustodyInitialPositionSha256 = initial.BindingSha256;
                break;
        }
        var before = JsonSerializer.Serialize(fixture.Ledger);
        Assert.False(MaterialOutputCustodyContract.IsAuthorized(row, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Ledger));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryLedgerClonePreservesAndDeepCopiesCustodyOriginAndAnchor(bool freshState)
    {
        var fixture = MintMuseumCustody();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(fixture.Snapshot))!;
        if (freshState)
        {
            snapshot.State["time"] = JsonSerializer.SerializeToElement(new { time_of_day = new { value = 1200, status = "available" } });
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            Assert.NotEqual(fixture.Snapshot.StateHash, snapshot.StateHash);
        }
        var clone = fixture.Service.Commit(fixture.Ledger, snapshot, new()
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
            PortfolioId = "unrelated-marker", SourceDecisionId = "unrelated-marker", GoalId = "other.goal"
        }, "clone");
        Assert.True(clone.Accepted, string.Join(",", clone.Errors));
        var original = fixture.Ledger.MaterialReservations.Single(value => value.OutputCustody is not null);
        var copied = clone.Ledger!.MaterialReservations.Single(value => value.OutputCustody is not null);
        Assert.NotSame(original.OutputCustody!.InitialPosition, copied.OutputCustody!.InitialPosition);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(copied));
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(copied, clone.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        copied.OutputCustody.InitialPosition!.SlotIndex++;
        clone.Ledger.History.Single(value => value.Operation == MaterialOutputCustodyContract.CreatedOperation).OutputCustodyInitialPositionSha256 = "changed";
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(original, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.False(MaterialOutputCustodyContract.IsAuthorized(copied, clone.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
    }

    [Fact]
    public void FileRepositoryRestartPreservesCustodyOriginWithoutMutation()
    {
        var root = NewPortfolioReplyRoot();
        var fixture = MintMuseumCustody(protectSpare: true, repository: new FileStrategyCommitmentRepository(root));
        var path = Assert.Single(Directory.GetFiles(root));
        var before = File.ReadAllBytes(path);
        var loaded = new FileStrategyCommitmentRepository(root).Get(fixture.Snapshot);
        Assert.Equal(JsonSerializer.Serialize(fixture.Ledger), JsonSerializer.Serialize(loaded));
        var row = loaded.MaterialReservations.Single(value => value.OutputCustody is not null);
        Assert.NotNull(row.OutputCustody!.InitialPosition);
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(row, loaded, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void LegacyCustodyKeepsOriginalConsumerAuthorityWithoutInventingInitialFacts()
    {
        var fixture = MintMuseumCustody();
        var row = fixture.Ledger.MaterialReservations.Single(value => value.OutputCustody is not null);
        row.OutputCustody!.InitialPosition = null;
        fixture.Ledger.History.Single(value => value.Operation == MaterialOutputCustodyContract.CreatedOperation).OutputCustodyInitialPositionSha256 = null;
        var before = JsonSerializer.Serialize(fixture.Ledger);
        Assert.DoesNotContain("initial_position", before, StringComparison.Ordinal);
        Assert.True(MaterialOutputCustodyContract.IsAuthorized(row, fixture.Ledger, MaterialOutputCustodyContract.MuseumConsumer));
        Assert.Null(row.OutputCustody.InitialPosition);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Ledger));
    }
}
