using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

internal sealed record PreparedMaterialTransferTestFixture(
    SnapshotEnvelope Before, SnapshotEnvelope After, ReservationPortfolioLedgerService Service,
    StrategyCommitmentLedger BaseLedger, StrategyCommitmentLedger PreparedLedger,
    ReservationPortfolioCommitRequest PreparationRequest)
{
    internal static PreparedMaterialTransferTestFixture Create()
    {
        var before = Snapshot(false);
        var after = Snapshot(true);
        var service = new ReservationPortfolioLedgerService();
        var route = service.Commit(null, before, RouteRequest(before), "route");
        Assert.True(route.Accepted, string.Join(";", route.Errors));
        var other = service.Commit(route.Ledger, before, OtherRequest(before), "other");
        Assert.True(other.Accepted, string.Join(";", other.Errors));
        var request = Preparation(before);
        var prepared = service.Commit(other.Ledger, before, request, "prepared");
        Assert.True(prepared.Accepted, string.Join(";", prepared.Errors));
        return new(before, after, service, other.Ledger!, prepared.Ledger!, request);
    }

    internal ReservationPortfolioSupportingTransitionSettlementRequest Settlement() => new()
    {
        StateHash = After.StateHash, ExpectedLedgerRevision = PreparedLedger.Revision,
        PortfolioId = "support:atomic-transfer", GoalId = "goal.grandpa_21", RouteSourceDecisionId = "route:a",
        SupportingTransitionReceiptSha256 = new string('c', 64),
        PreparedMaterialTransferSha256 = PreparedLedger.PreparedMaterialTransfer!.BindingSha256,
        MaterialRelocations = JsonSerializer.Deserialize<ReservationPortfolioMaterialRelocation[]>(
            JsonSerializer.Serialize(PreparationRequest.MaterialTransferPreparation!.MaterialRelocations))!,
        Reason = "verified_supporting_transition_relocation"
    };

    internal static ReservationPortfolioCommitRequest RouteRequest(SnapshotEnvelope before) => new()
    {
        StateHash = before.StateHash, PortfolioId = "route:portfolio", GoalId = "goal.grandpa_21", SourceDecisionId = "route:a",
        MaterialClaims = new[] { Claim(before, 0, "moved", "goal.grandpa_21", "route:a", "chest:Farm:4,5", 5, "(O)388", 2) }
    };

    internal static ReservationPortfolioCommitRequest OtherRequest(SnapshotEnvelope before) => new()
    {
        StateHash = before.StateHash, ExpectedLedgerRevision = 1, PortfolioId = "other:portfolio",
        GoalId = "other.goal", SourceDecisionId = "other:decision",
        MaterialClaims = new[]
        {
            Claim(before, 1, "retained", "other.goal", "other:decision", "chest:Farm:4,5", 5, "(O)388", 9),
            Claim(before, 1, "stone", "other.goal", "other:decision", "chest:Farm:4,5", 9, "(O)390", 3),
            Claim(before, 1, "outside", "other.goal", "other:decision", "chest:Farm:8,5", 6, "(O)330", 4)
        }
    };

    internal static ReservationPortfolioCommitRequest Preparation(SnapshotEnvelope before) => new()
    {
        StateHash = before.StateHash, ExpectedLedgerRevision = 2, PortfolioId = "support:atomic-transfer",
        GoalId = "goal.grandpa_21", SourceDecisionId = "support:atomic-transfer",
        MaterialTransferPreparation = new MaterialTransferPreparationRequest
        {
            RouteSourceDecisionId = "route:a",
            Intent = new MaterialTransferIntent
            {
                SourceNodeId = "chest:Farm:4,5", SourceSlotIndex = 5, DestinationNodeId = "player:123",
                QualifiedItemId = "(O)388", Quantity = 2, ExpectedSourceStack = 11
            },
            MaterialRelocations = new[]
            {
                new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = "moved", SourceNodeId = "chest:Farm:4,5", SourceSlotIndex = 5,
                    DestinationNodeId = "player:123", DestinationSlotIndex = 0, QualifiedItemId = "(O)388", Quantity = 2
                }
            }
        }
    };

    internal static SnapshotEnvelope Snapshot(bool after)
    {
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 123,
            InventoryNodes = new[]
            {
                Node("player:123", "player_inventory", after ? new[] { Slot(0, "(O)388", 2) } : Array.Empty<MaterialInventorySlot>()),
                Node("chest:Farm:4,5", "chest", new[] { Slot(after ? 0 : 5, "(O)388", after ? 9 : 11), Slot(after ? 1 : 9, "(O)390", 3) }),
                Node("chest:Farm:8,5", "chest", new[] { Slot(6, "(O)330", 4) })
            },
            AccessPoints = new[] { new MaterialInventoryAccessPoint
            {
                NodeId = "chest:Farm:4,5", AccessPointId = "access:Farm:4,5", AccessKind = "placed_chest",
                SpecialChestType = "None", ActorUseAuthorized = true, LocationId = "Farm", TileX = 4, TileY = 5
            } }
        };
        return WithGraph(graph, after ? 200 : 100);
    }

    internal static SnapshotEnvelope WithGraph(MaterialInventoryGraph graph, long tick)
    {
        var state = new Dictionary<string, JsonElement>
        {
            ["player"] = JsonSerializer.SerializeToElement(new
            {
                money = new { value = 1000, status = "available" },
                shop_currency_balances = new
                {
                    status = "available",
                    value = new
                    {
                        schema_version = "shop_currency_balances.v1",
                        projection_status = "complete_locked_base_1.6.15_shop_menu_currency_domain",
                        supported_currency_ids = NativeShopCurrencies.All.Select(row => row.Id).ToArray(),
                        rows = NativeShopCurrencies.All.Select(row => new
                        {
                            currency_id = row.Id, currency_key = row.Key, balance = row.Id == NativeShopCurrencies.Money ? 1000 : 0
                        }).ToArray()
                    }
                }
            }),
            ["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { value = graph, status = "available" } })
        };
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?> { Value = "AtomicTransferFarm", Status = FieldStatus.Available },
            PlayerId = new FieldEnvelope<string?> { Value = "123", Status = FieldStatus.Available },
            GameTick = tick, State = state, StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static MaterialReservationUpsertRequest Claim(SnapshotEnvelope before, int revision,
        string id, string goal, string decision, string node, int slot, string item, int quantity) => new()
    {
        StateHash = before.StateHash, ExpectedLedgerRevision = revision, ReservationId = id, GoalId = goal,
        SourceDecisionId = decision, NodeId = node, SlotIndex = slot, QualifiedItemId = item, Quantity = quantity, Purpose = "preserve:" + id
    };

    private static MaterialInventoryNode Node(string id, string kind, MaterialInventorySlot[] slots) => new()
    {
        NodeId = id, InventoryKind = kind, SupplyState = "available", OwnerPlayerId = 123,
        ActorUseAuthorized = true, OwnershipClass = "actor_owned", LocationId = "Farm", Capacity = 36, Slots = slots
    };

    private static MaterialInventorySlot Slot(int index, string item, int quantity) => new()
    {
        SlotIndex = index, QualifiedItemId = item, Stack = quantity, MaximumStackSize = 999, RuntimeType = "StardewValley.Object"
    };
}
