using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class RustyKeyPortfolioFixture
    {
        internal string EnableStoredCustody(bool remote = false)
        {
            EnableHeldDonation();
            MaterialInventoryGraphReader.TryRead(Snapshot, out var graph);
            var player = graph!.InventoryNodes.Single();
            player.LocationId = "ArchaeologyHouse";
            player.Capacity = 12;
            player.Slots[0].Stack = 1;
            player.Slots[0].RuntimeType = "StardewValley.Object";
            player.Slots[0].MaximumStackSize = 999;
            graph.InventoryNodes = graph.InventoryNodes.Append(new MaterialInventoryNode
            {
                NodeId = "chest:rolling", InventoryKind = "chest", LocationId = "ArchaeologyHouse",
                OwnerPlayerId = 1, SupplyState = "available", ActorUseAuthorized = true, Capacity = 36
            }).ToArray();
            graph.AccessPoints = new[] { new MaterialInventoryAccessPoint
            {
                NodeId = "chest:rolling", AccessPointId = "rolling-access", LocationId = "ArchaeologyHouse",
                TileX = 4, TileY = 5, AccessKind = "placed_chest", SpecialChestType = "None", ActorUseAuthorized = true
            } };
            SetRollingGraph(graph);
            var map = JsonSerializer.SerializeToNode(Snapshot.State["locations"])!;
            Snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new JsonObject { ["map"] = map["collision_grid"]!.DeepClone() });
            Rehash();
            var service = new ReservationPortfolioLedgerService();
            var committed = service.Commit(Ledger, Snapshot, new ReservationPortfolioCommitRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                PortfolioId = "rolling-acquisition", SourceDecisionId = "rolling-acquisition", GoalId = Inventory.GoalId
            }, "synthetic-commit");
            Require(committed.Accepted, "Synthetic acquisition marker failed: " + string.Join(",", committed.Errors));
            var acquired = service.SettleCompletedRoute(committed.Ledger, Snapshot, new ReservationPortfolioRouteSettlementRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = committed.Ledger!.Revision,
                PortfolioId = "rolling-acquisition", RouteSourceDecisionId = "rolling-acquisition", GoalId = Inventory.GoalId,
                FreshTerminalReceiptSha256 = new string('a', 64), Reason = "synthetic_native_receipt_not_game_evidence",
                OutputCustodyClaims = new[] { new ReservationPortfolioOutputCustodyClaim
                {
                    NodeId = player.NodeId, SlotIndex = 0, QualifiedItemId = player.Slots[0].QualifiedItemId,
                    Quantity = 1, ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer
                } }
            }, "synthetic-acquisition");
            Require(acquired.Accepted, "Synthetic acquisition custody failed: " + string.Join(",", acquired.Errors));
            Ledger = acquired.Ledger!;
            var custody = Ledger.MaterialReservations.Single(row => row.OutputCustody is not null);
            var preparation = MuseumOutputCustodyTransferRequestBuilder.Build(Snapshot, Ledger, custody.ReservationId, "chest:rolling");
            Require(preparation.Request is not null, "Synthetic storage preparation failed: " + string.Join(",", preparation.Errors));
            var store = service.Commit(Ledger, Snapshot, preparation.Request!, "synthetic-store-prepare");
            Require(store.Accepted, "Synthetic storage commit failed: " + string.Join(",", store.Errors));
            graph.InventoryNodes[1].Slots = player.Slots;
            player.Slots = Array.Empty<MaterialInventorySlot>();
            SetRollingGraph(graph);
            var settlement = service.SettleSupportingTransition(store.Ledger, Snapshot, new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = store.Ledger!.Revision,
                PortfolioId = preparation.Request!.PortfolioId, GoalId = Inventory.GoalId,
                RouteSourceDecisionId = store.Ledger.PreparedMaterialTransfer!.RouteSourceDecisionId,
                PreparedMaterialTransferSha256 = store.Ledger.PreparedMaterialTransfer.BindingSha256,
                SupportingTransitionReceiptSha256 = new string('b', 64),
                MaterialRelocations = preparation.Request.MaterialTransferPreparation!.MaterialRelocations,
                Reason = "synthetic_storage_not_native_evidence"
            }, "synthetic-store-settled");
            Require(settlement.Accepted, "Synthetic storage settlement failed: " + string.Join(",", settlement.Errors));
            Ledger = settlement.Ledger!;
            ChangeMuseum("donation_candidates", new JsonArray());
            if (remote)
            {
                player.LocationId = "Town";
                SetRollingGraph(graph);
                ChangeMuseum("museum_is_current_location", JsonValue.Create(false));
                var locations = JsonSerializer.SerializeToNode(Snapshot.State["locations"])!;
                locations["collision_grid"]!["value"]!["location_id"] = "Town";
                object Field(object value) => new { value, status = "available", confidence = 1, adapter = "test", read_at_tick = 1,
                    source = new { kind = "game_object", path = "synthetic_rolling_custody_route" } };
                locations["route_action_branch_coverage"] = JsonSerializer.SerializeToNode(Field(new { rows = Array.Empty<object>() }));
                var state = JsonSerializer.SerializeToNode(Snapshot.State["player"])!;
                state["energy"] = JsonSerializer.SerializeToNode(Field(270));
                Snapshot.State["player"] = JsonSerializer.SerializeToElement(state);
                locations["route_graph"] = JsonSerializer.SerializeToNode(Field(new { edges = new[] {
                    new { kind = "warp", from_location = "Town", from_x = 9, from_y = 9,
                        target_location = "ArchaeologyHouse", target_x = 3, target_y = 4, resolved = true } } }));
                locations["route_connectors"] = JsonSerializer.SerializeToNode(Field(new { location_id = "Town", connectors = new[] {
                    new { kind = "warp", tile_x = 9, tile_y = 9, target_location = "ArchaeologyHouse", target_x = 3, target_y = 4, resolved = true } } }));
                Snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
                Snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new JsonObject { ["map"] = locations["collision_grid"]!.DeepClone() });
                Rehash();
            }
            return custody.ReservationId;
        }

        private void SetRollingGraph(MaterialInventoryGraph graph)
        {
            var farm = JsonSerializer.SerializeToNode(Snapshot.State["farm"])!;
            farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph);
            Snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
            var player = graph.InventoryNodes.Single(row => row.InventoryKind == "player_inventory");
            var state = JsonSerializer.SerializeToNode(Snapshot.State["player"])!;
            state["location_id"]!["value"] = player.LocationId;
            state["inventory"]!["value"] = JsonSerializer.SerializeToNode(player.Slots);
            Snapshot.State["player"] = JsonSerializer.SerializeToElement(state);
            Rehash();
        }
    }
}
