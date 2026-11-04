using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreparationCannotMoveOrPassivelyRebindLegacyCustodyWithoutRetainedOrigin(bool moveCustody)
    {
        var fixture = MintMuseumCustody(protectSpare: true);
        var custody = fixture.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        custody.OutputCustody!.InitialPosition = null;
        fixture.Ledger.History.Single(row => row.Operation == MaterialOutputCustodyContract.CreatedOperation).OutputCustodyInitialPositionSha256 = null;
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(fixture.Snapshot))!;
        Assert.True(MaterialInventoryGraphReader.TryRead(before, out var graph));
        var player = graph!.InventoryNodes.Single(row => row.NodeId == "player:123");
        player.LocationId = "Farm";
        foreach (var slot in player.Slots)
        {
            slot.RuntimeType = "StardewValley.Object";
            slot.MaximumStackSize = 999;
        }
        const string chestId = "chest:Farm:60,61";
        graph.InventoryNodes = graph.InventoryNodes.Append(new MaterialInventoryNode
        {
            NodeId = chestId, InventoryKind = "chest", SupplyState = "available", OwnerPlayerId = 123,
            ActorUseAuthorized = true, Capacity = 36, LocationId = "Farm"
        }).ToArray();
        graph.AccessPoints = graph.AccessPoints.Append(new MaterialInventoryAccessPoint
        {
            NodeId = chestId, AccessPointId = "custody-test-chest", AccessKind = "placed_chest",
            SpecialChestType = "None", ActorUseAuthorized = true, LocationId = "Farm", TileX = 60, TileY = 61
        }).ToArray();
        before.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { value = graph, status = "available" } });
        before.StateHash = SnapshotHash.ComputeStateHash(before.State);
        var ledger = fixture.Ledger;
        var selected = ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
        if (!moveCustody)
        {
            var wood = player.Slots.Single(row => row.QualifiedItemId == "(O)388");
            var ordinary = new MaterialReservationLedgerService().Upsert(ledger, before, new()
            {
                StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision, ReservationId = "ordinary-transfer",
                SourceDecisionId = "ordinary-route", GoalId = "goal.grandpa_21", NodeId = player.NodeId,
                SlotIndex = wood.SlotIndex, QualifiedItemId = wood.QualifiedItemId, Quantity = 1, Purpose = "ordinary"
            }, "ordinary");
            Assert.True(ordinary.Accepted, string.Join(";", ordinary.Errors));
            ledger = ordinary.Ledger!;
            selected = ledger.MaterialReservations.Single(row => row.ReservationId == "ordinary-transfer");
        }
        var original = JsonSerializer.Serialize(ledger);
        var request = new ReservationPortfolioCommitRequest
        {
            StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "support:custody-guard",
            SourceDecisionId = "support:custody-guard", GoalId = selected.GoalId,
            MaterialTransferPreparation = new MaterialTransferPreparationRequest
            {
                RouteSourceDecisionId = selected.SourceDecisionId,
                Intent = new MaterialTransferIntent
                {
                    SourceNodeId = player.NodeId, SourceSlotIndex = selected.SlotIndex, DestinationNodeId = chestId,
                    QualifiedItemId = selected.QualifiedItemId, Quantity = 1,
                    ExpectedSourceStack = player.Slots.Single(row => row.SlotIndex == selected.SlotIndex).Stack
                },
                MaterialRelocations = new[] { new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = selected.ReservationId, SourceNodeId = selected.NodeId,
                    SourceSlotIndex = selected.SlotIndex, DestinationNodeId = chestId, DestinationSlotIndex = 0,
                    QualifiedItemId = selected.QualifiedItemId, Quantity = 1
                } }
            }
        };

        var result = fixture.Service.Commit(ledger, before, request, "blocked");

        Assert.False(result.Accepted);
        Assert.Contains("output_custody_relocation_requires_retained_terminal_lifecycle", result.Errors);
        Assert.Same(ledger, result.Ledger);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
        Assert.Null(ledger.PreparedMaterialTransfer);
    }

    [Fact]
    public void OtherCommitClonesPendingPreparationAndMakesItsOldRevisionUnusable()
    {
        var fixture = PreparedMaterialTransferTestFixture.Create();
        var another = fixture.Service.Commit(fixture.PreparedLedger, fixture.Before, new ReservationPortfolioCommitRequest
        {
            StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = 3, PortfolioId = "other:marker",
            SourceDecisionId = "other:marker", GoalId = "other.goal"
        }, "other");
        Assert.True(another.Accepted);
        Assert.NotSame(fixture.PreparedLedger.PreparedMaterialTransfer, another.Ledger!.PreparedMaterialTransfer);
        Assert.Equal(fixture.PreparedLedger.PreparedMaterialTransfer!.BindingSha256, another.Ledger.PreparedMaterialTransfer!.BindingSha256);
        var request = fixture.Settlement();
        request.ExpectedLedgerRevision = another.Ledger.Revision;
        var result = fixture.Service.SettleSupportingTransition(another.Ledger, fixture.After, request, "stale");
        Assert.False(result.Accepted);
        Assert.Contains("prepared_material_transfer_binding_drifted", result.Errors);
        Assert.NotNull(result.Ledger!.PreparedMaterialTransfer);
    }
}
