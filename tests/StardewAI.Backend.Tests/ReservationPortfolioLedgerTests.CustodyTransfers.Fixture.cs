using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private sealed class CustodyTransferFixture
    {
        internal const string Chest = "chest:Farm:4,5";
        internal ReservationPortfolioLedgerService Service { get; } = new();
        internal IStrategyCommitmentRepository? Repository { get; set; }
        internal StrategyCommitmentLedger Ledger { get; set; }
        internal SnapshotEnvelope Snapshot { get; set; }
        internal string CustodyId { get; }

        internal CustodyTransferFixture(IStrategyCommitmentRepository? repository = null)
        {
            Repository = repository;
            var minted = MintMuseumCustody(protectSpare: true, repository);
            Ledger = minted.Ledger;
            CustodyId = Ledger.MaterialReservations.Single(row => row.OutputCustody is not null).ReservationId;
            Assert.True(MaterialInventoryGraphReader.TryRead(minted.Snapshot, out var graph));
            var player = graph!.InventoryNodes.Single(row => row.NodeId == "player:123");
            player.LocationId = "Farm";
            player.Capacity = 12;
            foreach (var slot in player.Slots) { slot.RuntimeType = "StardewValley.Object"; slot.MaximumStackSize = 999; }
            graph.InventoryNodes = graph.InventoryNodes.Append(new MaterialInventoryNode
            {
                NodeId = Chest, InventoryKind = "chest", LocationId = "Farm", SupplyState = "available",
                ActorUseAuthorized = true, OwnerPlayerId = 123, Capacity = 36,
                Slots = new[] { Slot(9, "(O)390", 3) }
            }).ToArray();
            graph.AccessPoints = graph.AccessPoints.Append(new MaterialInventoryAccessPoint
            {
                NodeId = Chest, AccessPointId = "custody-transfer-access", AccessKind = "placed_chest", SpecialChestType = "None",
                LocationId = "Farm", TileX = 4, TileY = 5, ActorUseAuthorized = true
            }).ToArray();
            Snapshot = WithGraph(minted.Snapshot, graph, 100);
            var request = new MaterialReservationUpsertRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision, ReservationId = "other-goal-stone",
                SourceDecisionId = "stone-route", GoalId = "other-stone-goal", NodeId = Chest, SlotIndex = 9,
                QualifiedItemId = "(O)390", Quantity = 3, Purpose = "keep stone"
            };
            var result = Repository?.UpsertMaterial(Snapshot, request) ?? new MaterialReservationLedgerService().Upsert(Ledger, Snapshot, request, "stone");
            Assert.True(result.Accepted, string.Join(",", result.Errors));
            Ledger = result.Ledger!;
        }

        internal (StrategyCommitmentLedger Before, ReservationPortfolioSupportingTransitionSettlementRequest Request,
            ReservationPortfolioSupportingTransitionSettlementResult Result) Transfer(bool withdraw)
        {
            var prepared = Prepare(withdraw);
            var result = Repository?.SettleReservationPortfolioSupportingTransition(prepared.After, prepared.Request) ??
                Service.SettleSupportingTransition(prepared.Before, prepared.After, prepared.Request, "settle");
            Assert.True(result.Accepted, string.Join(",", result.Errors));
            Ledger = result.Ledger!;
            Snapshot = prepared.After;
            return (prepared.Before, prepared.Request, result);
        }

        internal (StrategyCommitmentLedger Before, SnapshotEnvelope After,
            ReservationPortfolioSupportingTransitionSettlementRequest Request) Prepare(bool withdraw)
        {
            var row = Ledger.MaterialReservations.Single(value => value.ReservationId == CustodyId);
            var preparation = new ReservationPortfolioCommitRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                PortfolioId = withdraw ? "withdraw-custody" : "store-custody", SourceDecisionId = withdraw ? "withdraw-custody" : "store-custody",
                GoalId = row.GoalId,
                MaterialTransferPreparation = new MaterialTransferPreparationRequest
                {
                    RouteSourceDecisionId = row.SourceDecisionId,
                    Intent = new MaterialTransferIntent
                    {
                        SourceNodeId = row.NodeId, SourceSlotIndex = row.SlotIndex, DestinationNodeId = withdraw ? "player:123" : Chest,
                        QualifiedItemId = row.QualifiedItemId, Quantity = 1, ExpectedSourceStack = withdraw ? 1 : 2
                    },
                    MaterialRelocations = new[] { new ReservationPortfolioMaterialRelocation
                    {
                        MaterialReservationId = row.ReservationId, SourceNodeId = row.NodeId, SourceSlotIndex = row.SlotIndex,
                        DestinationNodeId = withdraw ? "player:123" : Chest, DestinationSlotIndex = 1,
                        QualifiedItemId = row.QualifiedItemId, Quantity = 1
                    } }
                }
            };
            var commit = Repository?.CommitReservationPortfolio(Snapshot, preparation) ?? Service.Commit(Ledger, Snapshot, preparation, "prepare");
            Assert.True(commit.Accepted, string.Join(",", commit.Errors));
            var before = commit.Ledger!;
            Ledger = before;
            Assert.True(MaterialInventoryGraphReader.TryRead(Snapshot, out var graph));
            graph!.InventoryNodes.Single(value => value.NodeId == "player:123").Slots.Single(value => value.QualifiedItemId == "(O)96").Stack = withdraw ? 2 : 1;
            graph.InventoryNodes.Single(value => value.NodeId == Chest).Slots = withdraw
                ? new[] { Slot(0, "(O)390", 3) } : new[] { Slot(0, "(O)390", 3), Slot(1, "(O)96", 1) };
            var after = WithGraph(Snapshot, graph, Snapshot.GameTick + 100);
            var request = new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = after.StateHash, ExpectedLedgerRevision = before.Revision, PortfolioId = preparation.PortfolioId,
                GoalId = row.GoalId, RouteSourceDecisionId = row.SourceDecisionId,
                PreparedMaterialTransferSha256 = before.PreparedMaterialTransfer!.BindingSha256,
                SupportingTransitionReceiptSha256 = new string(withdraw ? 'd' : 'c', 64),
                MaterialRelocations = preparation.MaterialTransferPreparation.MaterialRelocations,
                Reason = "verified_supporting_transition_relocation"
            };
            return (before, after, request);
        }

        internal SnapshotEnvelope AtMuseum()
        {
            Assert.True(MaterialInventoryGraphReader.TryRead(Snapshot, out var graph));
            graph!.InventoryNodes.Single(row => row.NodeId == "player:123").LocationId = "ArchaeologyHouse";
            return WithGraph(Snapshot, graph, Snapshot.GameTick + 1);
        }

        internal static SnapshotEnvelope WithGraph(SnapshotEnvelope source, MaterialInventoryGraph graph, long tick)
        {
            var result = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(source))!;
            var farm = JsonSerializer.SerializeToNode(result.State["farm"])!;
            farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph);
            result.State["farm"] = JsonSerializer.SerializeToElement(farm);
            var player = JsonSerializer.SerializeToNode(result.State["player"])!;
            player["location_id"]!["value"] = graph.InventoryNodes.Single(row => row.NodeId == "player:123").LocationId;
            player["inventory"]!["value"]![0]!["stack"] = graph.InventoryNodes.Single(row => row.NodeId == "player:123").Slots.Single(row => row.QualifiedItemId == "(O)96").Stack;
            result.State["player"] = JsonSerializer.SerializeToElement(player);
            result.GameTick = tick;
            result.StateHash = SnapshotHash.ComputeStateHash(result.State);
            return result;
        }

        internal static MaterialInventorySlot Slot(int index, string item, int stack) => new()
        {
            SlotIndex = index, QualifiedItemId = item, Stack = stack, Quality = 0, RuntimeType = "StardewValley.Object", MaximumStackSize = 999
        };
    }
}
