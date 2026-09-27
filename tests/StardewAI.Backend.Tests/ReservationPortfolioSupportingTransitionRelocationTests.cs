using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void SupportingTransitionRelocatesExactClaimToObservedPlayerSlot()
    {
        var before = RelocationSnapshot(afterTransfer: false);
        var after = RelocationSnapshot(afterTransfer: true);
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            RelocationCommitRequest(before),
            "2026-09-27T02:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-transfer",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-transfer"
            },
            "2026-09-27T02:01:00Z");
        var request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash,
            ExpectedLedgerRevision = 2,
            PortfolioId = "support:route-transfer",
            GoalId = "goal.grandpa_21",
            RouteSourceDecisionId = "route:a",
            SupportingTransitionReceiptSha256 = new string('c', 64),
            MaterialRelocations = new[]
            {
                new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = "route-a-material",
                    SourceNodeId = "chest:Farm:4,5",
                    SourceSlotIndex = 0,
                    DestinationNodeId = "player:123",
                    DestinationSlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 2
                }
            },
            Reason = "verified_supporting_transition_relocation"
        };

        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            request,
            "2026-09-27T02:02:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(3, result.CommittedLedgerRevision);
        Assert.Empty(result.CompletedMaterialReservationIds);
        Assert.Empty(result.MaterialSettlements);
        Assert.Equal(0, result.ConsumedQuantity);
        Assert.Equal(new[] { "route-a-material" },
            result.ActiveMaterialReservationIds);
        Assert.Equal(new[] { "route-a-material" },
            result.RelocatedMaterialReservationIds);
        var relocation = Assert.Single(result.MaterialRelocations);
        Assert.Equal("player:123", relocation.DestinationNodeId);
        var claim = Assert.Single(result.Ledger!.MaterialReservations);
        Assert.Equal(StrategyCommitmentStatuses.Active, claim.Status);
        Assert.Equal(after.StateHash, claim.SourceStateHash);
        Assert.Equal("player:123", claim.NodeId);
        Assert.Equal(0, claim.SlotIndex);
        Assert.Equal(2, claim.Quantity);
        Assert.Equal(2, claim.Revision);
        Assert.Contains(result.Ledger.History, row =>
            row.CommitmentId == "route-a-material" &&
            row.Operation == "material_reservation_relocated");
        Assert.DoesNotContain(result.Ledger.History, row =>
            row.Operation == "reservation_portfolio_route_complete");
    }

    [Fact]
    public void SupportingTransitionRejectsMixedConsumptionAndRelocation()
    {
        var before = RelocationSnapshot(afterTransfer: false);
        var after = RelocationSnapshot(afterTransfer: true);
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            RelocationCommitRequest(before),
            "2026-09-27T02:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-transfer",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-transfer"
            },
            "2026-09-27T02:01:00Z");
        var request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash,
            ExpectedLedgerRevision = 2,
            PortfolioId = "support:route-transfer",
            GoalId = "goal.grandpa_21",
            RouteSourceDecisionId = "route:a",
            SupportingTransitionReceiptSha256 = new string('c', 64),
            MaterialConsumptions = new[]
            {
                new ReservationPortfolioMaterialConsumption
                {
                    MaterialReservationId = "route-a-material",
                    NodeId = "chest:Farm:4,5",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    ConsumedQuantity = 1
                }
            },
            MaterialRelocations = new[]
            {
                new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = "route-a-material",
                    SourceNodeId = "chest:Farm:4,5",
                    SourceSlotIndex = 0,
                    DestinationNodeId = "player:123",
                    DestinationSlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 2
                }
            },
            Reason = "invalid_mixed_mutation"
        };

        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            request,
            "2026-09-27T02:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains("supporting_transition_material_mutation_mode_invalid",
            result.Errors);
        Assert.Same(support.Ledger, result.Ledger);
    }

    [Fact]
    public void SupportingTransitionRejectsPartialClaimRelocation()
    {
        var before = RelocationSnapshot(afterTransfer: false);
        var after = RelocationSnapshot(afterTransfer: true);
        var (service, support) = CommittedRelocationSupport(before);
        var request = RelocationSettlementRequest(after);
        request.MaterialRelocations[0].Quantity = 1;

        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            request,
            "2026-09-27T02:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains("supporting_transition_relocated_claim_mismatch",
            result.Errors);
        Assert.Same(support.Ledger, result.Ledger);
    }

    [Fact]
    public void SupportingTransitionRejectsUnobservedDestinationMaterial()
    {
        var before = RelocationSnapshot(afterTransfer: false);
        var after = RelocationSnapshot(afterTransfer: false);
        var (service, support) = CommittedRelocationSupport(before);
        var request = RelocationSettlementRequest(after);

        var result = service.SettleSupportingTransition(
            support.Ledger,
            after,
            request,
            "2026-09-27T02:02:00Z");

        Assert.False(result.Accepted);
        Assert.Contains(
            "supporting_transition_relocation_destination_unavailable",
            result.Errors);
        Assert.Same(support.Ledger, result.Ledger);
    }

    private static (
        ReservationPortfolioLedgerService Service,
        ReservationPortfolioCommitResult Support)
        CommittedRelocationSupport(SnapshotEnvelope before)
    {
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            before,
            RelocationCommitRequest(before),
            "2026-09-27T02:00:00Z");
        var support = service.Commit(
            committed.Ledger,
            before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = before.StateHash,
                ExpectedLedgerRevision = 1,
                PortfolioId = "support:route-transfer",
                GoalId = "goal.grandpa_21",
                SourceDecisionId = "support:route-transfer"
            },
            "2026-09-27T02:01:00Z");
        return (service, support);
    }

    private static ReservationPortfolioSupportingTransitionSettlementRequest
        RelocationSettlementRequest(SnapshotEnvelope after) => new()
        {
            StateHash = after.StateHash,
            ExpectedLedgerRevision = 2,
            PortfolioId = "support:route-transfer",
            GoalId = "goal.grandpa_21",
            RouteSourceDecisionId = "route:a",
            SupportingTransitionReceiptSha256 = new string('c', 64),
            MaterialRelocations = new[]
            {
                new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = "route-a-material",
                    SourceNodeId = "chest:Farm:4,5",
                    SourceSlotIndex = 0,
                    DestinationNodeId = "player:123",
                    DestinationSlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 2
                }
            },
            Reason = "verified_supporting_transition_relocation"
        };

    private static ReservationPortfolioCommitRequest RelocationCommitRequest(
        SnapshotEnvelope snapshot) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = 0,
            PortfolioId = "portfolio:transfer",
            GoalId = "goal.grandpa_21",
            SourceDecisionId = "route:a",
            MaterialClaims = new[]
            {
                new MaterialReservationUpsertRequest
                {
                    StateHash = snapshot.StateHash,
                    ExpectedLedgerRevision = 0,
                    ReservationId = "route-a-material",
                    SourceDecisionId = "route:a",
                    GoalId = "goal.grandpa_21",
                    NodeId = "chest:Farm:4,5",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)388",
                    Quantity = 2,
                    Purpose = "test route staged material"
                }
            }
        };

    private static SnapshotEnvelope RelocationSnapshot(bool afterTransfer)
    {
        var playerSlots = afterTransfer
            ? new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "388",
                    qualified_item_id = "(O)388",
                    stack = 2,
                    maximum_stack_size = 999,
                    quality = 0
                }
            }
            : Array.Empty<object>();
        var chestSlots = afterTransfer
            ? Array.Empty<object>()
            : new object[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "388",
                    qualified_item_id = "(O)388",
                    stack = 2,
                    maximum_stack_size = 999,
                    quality = 0
                }
            };
        var state = new Dictionary<string, JsonElement>
        {
            ["farm"] = JsonSerializer.SerializeToElement(new
            {
                material_inventory_graph = new
                {
                    value = new
                    {
                        schema_version = "material_inventory_graph.v1",
                        status = "available",
                        player_id = 123,
                        inventory_nodes = new object[]
                        {
                            new
                            {
                                node_id = "player:123",
                                inventory_kind = "player_inventory",
                                supply_state = "available",
                                owner_player_id = 123,
                                actor_use_authorized = true,
                                capacity = 12,
                                slots = playerSlots
                            },
                            new
                            {
                                node_id = "chest:Farm:4,5",
                                inventory_kind = "chest",
                                supply_state = "available",
                                owner_player_id = 123,
                                actor_use_authorized = true,
                                capacity = 36,
                                slots = chestSlots
                            }
                        }
                    },
                    status = "available"
                }
            })
        };
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "TestFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
