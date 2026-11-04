using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed partial class MaterialTransferProjectorTests
{
    private static (MaterialInventoryGraph Graph, MaterialTransferIntent Intent, StrategyCommitmentLedger Ledger,
        ReservationPortfolioMaterialRelocation[] Moves) CustodyTransferProjectionFixture()
    {
        var graph = ReservationGraph(Node("player:1", "player_inventory", 12, "Farm", OtherSlot(2, 2, "(O)96")),
            Node("chest:Farm:4,5", "chest", 36, "Farm", OtherSlot(5, 7, "(O)388"), Slot(9, 3)),
            Node("chest:Farm:8,5", "chest", 36, "Farm", OtherSlot(6, 1, "(O)330")));
        var custodyPortfolio = MaterialOutputCustodyContract.PortfolioId("acquired", "geode-route",
            MaterialOutputCustodyContract.MuseumConsumer, "(O)96");
        var custody = ReservationClaim(MaterialOutputCustodyContract.ReservationId(custodyPortfolio), "player:1", 2, 1, "(O)96");
        custody.SourceDecisionId = custodyPortfolio;
        custody.GoalId = "goal.grandpa_21";
        custody.OutputCustody = new MaterialOutputCustody
        {
            AcquisitionPortfolioId = "acquired",
            AcquisitionRouteSourceDecisionId = "geode-route",
            ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer,
            FreshTerminalReceiptSha256 = new string('a', 64),
            CreatedLedgerRevision = 3
        };
        var ledger = ReservationLedger(custody,
            ReservationClaim("same-stack-other-goal", "player:1", 2, 1, "(O)96"),
            ReservationClaim("wood-other-goal", "chest:Farm:4,5", 5, 7, "(O)388"),
            ReservationClaim("stone-other-goal", "chest:Farm:4,5", 9, 3),
            ReservationClaim("outside-transfer", "chest:Farm:8,5", 6, 1, "(O)330"));
        ledger.History = new[]
        {
            new StrategyCommitmentHistoryEntry
            {
                CommitmentId = custody.ReservationId, SourceDecisionId = custodyPortfolio,
                Operation = MaterialOutputCustodyContract.CreatedOperation, LedgerRevision = 3,
                CommitmentRevision = custody.Revision, SourceStateHash = custody.SourceStateHash,
                Reason = custody.OutputCustody.FreshTerminalReceiptSha256
            },
            new StrategyCommitmentHistoryEntry
            {
                CommitmentId = custodyPortfolio, SourceDecisionId = custodyPortfolio,
                Operation = "reservation_portfolio_commit", LedgerRevision = 3
            }
        };
        return (graph, new MaterialTransferIntent
        {
            SourceNodeId = "player:1", SourceSlotIndex = 2, DestinationNodeId = "chest:Farm:4,5",
            QualifiedItemId = "(O)96", Quantity = 1, ExpectedSourceStack = 2
        }, ledger, new[] { ReservationMove(custody, "chest:Farm:4,5", 2) });
    }

    private static MaterialInventoryGraph ReservationGraph(params MaterialInventoryNode[] nodes)
    {
        var graph = TransferGraph(nodes);
        graph.PlayerId = 1;
        foreach (var node in nodes) node.OwnerPlayerId = 1;
        return graph;
    }

    private static MaterialReservation ReservationClaim(string id, string node, int slot, int quantity, string item = "(O)390") => new()
    {
        ReservationId = id, Revision = 2, SourceDecisionId = "decision:" + id,
        SourceStateHash = "before-state", GoalId = "goal:" + id, OwnerPlayerId = 1,
        NodeId = node, SlotIndex = slot, QualifiedItemId = item, Quantity = quantity, Purpose = "preserve:" + id
    };

    private static StrategyCommitmentLedger ReservationLedger(params MaterialReservation[] claims) => new()
    {
        LedgerId = "strategy-ledger:save:1", SaveId = "save", PlayerId = "1", Revision = 7,
        SourceStateHash = "before-state", MaterialReservations = claims
    };

    private static ReservationPortfolioMaterialRelocation ReservationMove(MaterialReservation claim, string node, int slot) => new()
    {
        MaterialReservationId = claim.ReservationId, SourceNodeId = claim.NodeId, SourceSlotIndex = claim.SlotIndex,
        DestinationNodeId = node, DestinationSlotIndex = slot, QualifiedItemId = claim.QualifiedItemId, Quantity = claim.Quantity
    };
}
