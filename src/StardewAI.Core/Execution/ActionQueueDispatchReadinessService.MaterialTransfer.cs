using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueDispatchReadinessService
{
    private static ActionQueueDispatchReadiness EvaluateMaterialTransfer(ActionQueueItem item,
        StrategyCommitmentLedger ledger, string stateHash, ActionQueueDispatchReadiness result)
    {
        var reasons = new List<string>();
        var parameters = item.NormalizedCommand.Parameters;
        var binding = Parameter(item, PreparedMaterialTransferContract.BindingParameter);
        if (parameters.Where(row => MaterialTransferIntentBinder.RequiredParameterNames.Contains(row.Name) ||
                row.Name == PreparedMaterialTransferContract.BindingParameter ||
                row.Name.StartsWith("acquisition_", StringComparison.Ordinal))
            .GroupBy(row => row.Name, StringComparer.Ordinal).Any(group => group.Count() != 1))
            reasons.Add("dispatch_material_transfer_parameters_not_unique");
        if (item.Status != "pending") reasons.Add("dispatch_queue_item_not_pending");
        if (stateHash != item.NormalizedCommand.StateHash)
            reasons.Add("dispatch_material_transfer_state_hash_drifted");
        if (!MaterialTransferIntentBinder.TryBuildIntent(parameters, out var intent))
            reasons.Add("dispatch_material_transfer_intent_invalid");

        var bound = ledger.PreparedMaterialTransfer is not null ||
            parameters.Any(row => row.Name == PreparedMaterialTransferContract.BindingParameter) ||
            !string.IsNullOrWhiteSpace(item.NormalizedCommand.GoalId) ||
            parameters.Any(row => row.Name.StartsWith("acquisition_", StringComparison.Ordinal)) ||
            intent is not null && ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
                (row.NodeId == intent.SourceNodeId || row.NodeId == intent.DestinationNodeId));
        if (bound)
        {
            var prepared = ledger.PreparedMaterialTransfer;
            result.RequiredLedgerId = ledger.LedgerId;
            result.RequiredLedgerRevision = prepared?.ExpectedLedgerRevision;
            if (prepared is null ||
                !PreparedMaterialTransferContract.IsBound(ledger, binding,
                    Parameter(item, "acquisition_reservation_portfolio_id"), item.NormalizedCommand.GoalId,
                    prepared.RouteSourceDecisionId) ||
                IntParameter(item, "acquisition_reservation_ledger_revision") != ledger.Revision ||
                Parameter(item, "acquisition_support_transition_kind") != "machine_input_material_transfer" ||
                JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared.Intent))
                reasons.Add("dispatch_material_transfer_preparation_binding_drifted");
            else if (PreparedMaterialTransferContract.ContainsCustody(ledger, prepared.ReservationProjection))
                reasons.Add("output_custody_relocation_requires_retained_terminal_lifecycle");
        }
        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        result.Ready = result.BlockingReasons.Length == 0;
        result.Status = result.Ready ? bound ? "ready" : "not_applicable" : "blocked";
        return result;
    }
}
