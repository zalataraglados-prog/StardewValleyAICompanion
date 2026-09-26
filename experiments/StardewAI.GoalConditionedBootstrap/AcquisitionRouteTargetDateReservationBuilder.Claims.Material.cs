using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private static readonly HashSet<string> SlotMaterialInputKinds = new(
        new[]
        {
            "crop_seed",
            "shop_trade_item",
            "loose_magic_bait",
            "machine_primary_input",
            "machine_additional_input"
        },
        StringComparer.Ordinal);

    private static ReservationClaimBuildResult BuildMaterialClaims(
        AcquisitionRouteTargetDateCurrency route,
        string goalId,
        string stateHash,
        string decisionId,
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources,
        AcquisitionResourceInputEvaluation[] inputs)
    {
        if (inputs.Length == 0)
            return ReservationClaimBuildResult.Empty;
        var unsupported = inputs.Where(row =>
                !SlotMaterialInputKinds.Contains(row.InputKind))
            .Select(row => row.InputKind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unsupported.Length > 0)
        {
            return ReservationClaimBuildResult.Blocked(
                unsupported.Select(kind =>
                        kind == "attached_magic_bait"
                            ? "attached_tool_consumable_reservation_address_not_bound"
                            : "resource_input_reservation_kind_not_bound:" + kind)
                    .ToArray());
        }
        if (!resources.MaterialEvidenceAvailable ||
            resources.MaterialGraph is null)
        {
            return ReservationClaimBuildResult.Blocked(
                resources.MaterialBlockingReasons.Length > 0
                    ? resources.MaterialBlockingReasons
                    : new[] { "material_inventory_graph_missing_or_unavailable" });
        }
        if (resources.MaterialGraph.PlayerId != ledgerState.ActorPlayerId)
        {
            return ReservationClaimBuildResult.Blocked(
                "material_inventory_graph_player_mismatch");
        }

        var otherReservations = ledgerState.Ledger.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId != decisionId)
            .ToArray();
        var supply = new MaterialSupplyProjection().Project(
            resources.MaterialGraph,
            otherReservations);
        if (supply.Status != "available")
        {
            return ReservationClaimBuildResult.Blocked(
                supply.BlockingReasons.Length > 0
                    ? supply.BlockingReasons
                    : new[] { "material_supply_projection_blocked" });
        }

        var availableBySlot = supply.Slots.ToDictionary(
            row => SlotKey(row.NodeId, row.SlotIndex),
            row => row.AvailableQuantity,
            StringComparer.Ordinal);
        var claims = new List<MaterialReservationUpsertRequest>();
        for (var inputIndex = 0; inputIndex < inputs.Length; inputIndex++)
        {
            var input = inputs[inputIndex];
            HashSet<string>? eligibleMachineSlots = null;
            if (input.MachineBinding is not null)
            {
                if (input.MachineBinding.EligibleSlots.Any(slot =>
                        slot.QualifiedItemId != input.QualifiedItemId ||
                        slot.SlotIndex < 0 ||
                        string.IsNullOrWhiteSpace(slot.NodeId)) ||
                    input.MachineBinding.EligibleSlots
                        .Select(slot => SlotKey(slot.NodeId, slot.SlotIndex))
                        .Distinct(StringComparer.Ordinal).Count() !=
                    input.MachineBinding.EligibleSlots.Length)
                {
                    return ReservationClaimBuildResult.Blocked(
                        "machine_resource_binding_invalid:" +
                        input.QualifiedItemId);
                }
                eligibleMachineSlots = input.MachineBinding.EligibleSlots
                    .Select(slot => SlotKey(slot.NodeId, slot.SlotIndex))
                    .ToHashSet(StringComparer.Ordinal);
            }
            var remaining = input.RequiredQuantity;
            var claimIndex = 0;
            foreach (var slot in supply.Slots.Where(row =>
                         row.QualifiedItemId == input.QualifiedItemId &&
                         (eligibleMachineSlots is null ||
                          eligibleMachineSlots.Contains(SlotKey(
                              row.NodeId,
                              row.SlotIndex))))
                     .OrderBy(row => row.NodeId, StringComparer.Ordinal)
                     .ThenBy(row => row.SlotIndex))
            {
                var key = SlotKey(slot.NodeId, slot.SlotIndex);
                var available = availableBySlot[key];
                if (available <= 0)
                    continue;
                var quantity = Math.Min(available, remaining);
                claims.Add(new MaterialReservationUpsertRequest
                {
                    StateHash = stateHash,
                    ExpectedLedgerRevision = ledgerState.Ledger.Revision,
                    ReservationId = ReservationId(
                        route.RouteOccurrenceId,
                        "material",
                        inputIndex,
                        claimIndex++),
                    SourceDecisionId = decisionId,
                    GoalId = goalId,
                    NodeId = slot.NodeId,
                    SlotIndex = slot.SlotIndex,
                    QualifiedItemId = input.QualifiedItemId,
                    Quantity = quantity,
                    Purpose = "reserve target-date acquisition material input"
                });
                availableBySlot[key] = available - quantity;
                remaining -= quantity;
                if (remaining == 0)
                    break;
            }
            if (remaining > 0)
            {
                return ReservationClaimBuildResult.Conflict(
                    "unreserved_material_quantity_unavailable:" +
                    input.QualifiedItemId);
            }
        }
        return new ReservationClaimBuildResult(
            claims.ToArray(),
            Array.Empty<CurrencyReservationUpsertRequest>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
