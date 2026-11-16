using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Fact]
    public void PlantingSourceRefreshKeepsAlreadyCurrentMarkerUnchanged()
    {
        using var f = new Fixture();
        Assert.Empty(f.Open().MarkerRequest.MaterialClaims);
    }

    [Fact]
    public void PlantingSourceRefreshRebindsOwnedSeedsAfterMarkerOnlySiteActions()
    {
        using var f = new Fixture();
        CropTeacherSeedPlantingPrefixTests.CompleteGrowthContext(f);
        f.AdvanceSourceWithMarkerOnly();
        var before = JsonSerializer.Serialize(f.Ledger, JsonOptions);
        var old = f.Ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == "player:123").ToDictionary(row => row.ReservationId);
        Assert.All(old.Values, row => Assert.NotEqual(f.Before.StateHash, row.SourceStateHash));
        var scope = f.Open();
        var requested = scope.MarkerRequest;
        Assert.Equal(old.Keys.Order(), requested.MaterialClaims.Select(row => row.ReservationId).Order());
        Assert.All(requested.MaterialClaims, claim =>
        {
            var prior = old[claim.ReservationId];
            Assert.Equal(f.Before.StateHash, claim.StateHash);
            Assert.Equal(prior.SourceDecisionId, claim.SourceDecisionId);
            Assert.Equal(prior.NodeId, claim.NodeId); Assert.Equal(prior.SlotIndex, claim.SlotIndex);
            Assert.Equal(prior.QualifiedItemId, claim.QualifiedItemId); Assert.Equal(prior.Quantity, claim.Quantity);
            Assert.Equal(prior.Purpose, claim.Purpose);
        });
        var bounded = scope.ValidateExecutionPreflight(f.Batch(3), f.OwnedSlotOne);
        Assert.Equal(3, bounded.Tiles.Length);
        Assert.Equal(before, JsonSerializer.Serialize(f.Ledger, JsonOptions));
        var marker = f.Mark(scope);
        var actions = marker.BuildActions(f.Batch(3), f.OwnedSlotOne);
        var queue = f.Compile(marker, actions);
        Assert.Equal("pending", queue.Status); Assert.Empty(queue.CompilerDiagnostics);
        Assert.All(old.Values, prior =>
        {
            var current = marker.Ledger.MaterialReservations.Single(row => row.ReservationId == prior.ReservationId);
            Assert.Equal(f.Before.StateHash, current.SourceStateHash);
            Assert.Equal(prior.Revision + 1, current.Revision); Assert.Equal(prior.Quantity, current.Quantity);
            Assert.Equal(prior.SourceDecisionId, current.SourceDecisionId);
        });
        Fixture.Save(f.QueuePath, queue); marker.ValidateQueue(f.QueuePath);
    }

    internal sealed partial class Fixture
    {
        internal void AdvanceSourceWithMarkerOnly()
        {
            var progress = JsonNode.Parse(Before.State["world_progress"].GetRawText())!;
            progress["game_state_query_unlock_state"]!["value"]!["players"]![0]!["stats"]!["stepsTaken"] = 26;
            Before.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
            Before.GameTick++; Before.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(Before.State);
            var reply = Repository.CommitReservationPortfolio(Before, new ReservationPortfolioCommitRequest
            {
                StateHash = Before.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                PortfolioId = "fixture.site.marker-only", SourceDecisionId = "fixture.site.marker-only", GoalId = "earn_money"
            });
            Assert.True(reply.Accepted, string.Join(",", reply.Errors)); Ledger = reply.Ledger!;
            Save(SourcePath, Before); Save(LedgerPath, Ledger);
        }
    }
}
