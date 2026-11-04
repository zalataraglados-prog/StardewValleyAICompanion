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
    private static readonly JsonSerializerOptions MaterialTransferExecutionJsonOptions = new(JsonSerializerDefaults.Web);

    private static ActionQueueDispatchReadiness EvaluateMaterialTransfer(ActionQueueItem item,
        StrategyCommitmentLedger ledger, string stateHash, ActionQueueDispatchReadiness result)
    {
        var reasons = new List<string>();
        var parameters = item.NormalizedCommand.Parameters;
        var binding = Parameter(item, PreparedMaterialTransferContract.BindingParameter);
        if (parameters.Where(row => MaterialTransferIntentBinder.RequiredParameterNames.Contains(row.Name) ||
                row.Name == PreparedMaterialTransferContract.BindingParameter ||
                row.Name is "material_transfer_intent_json" or "material_transfer_projection_json" ||
                row.Name.StartsWith("acquisition_", StringComparison.Ordinal) ||
                MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name))
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
            parameters.Any(row => MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name)) ||
            parameters.Any(row => row.Name.StartsWith("acquisition_", StringComparison.Ordinal)) ||
            intent is not null && ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
                (row.NodeId == intent.SourceNodeId || row.NodeId == intent.DestinationNodeId));
        PreparedMaterialTransfer? validatedPreparation = null;
        if (bound)
        {
            var prepared = ledger.PreparedMaterialTransfer;
            result.RequiredLedgerId = ledger.LedgerId;
            result.RequiredLedgerRevision = prepared?.ExpectedLedgerRevision;
            if (parameters.Any(row => MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name)))
            {
                if (!MuseumOutputCustodyTransferBinding.IsBound(item, ledger) ||
                    JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared?.Intent))
                    reasons.Add("dispatch_output_custody_transfer_binding_drifted");
                else validatedPreparation = prepared;
            }
            else if (prepared is null ||
                !PreparedMaterialTransferContract.IsBound(ledger, binding,
                    Parameter(item, "acquisition_reservation_portfolio_id"), item.NormalizedCommand.GoalId,
                    prepared.RouteSourceDecisionId) ||
                IntParameter(item, "acquisition_reservation_ledger_revision") != ledger.Revision ||
                Parameter(item, "acquisition_support_transition_kind") != "machine_input_material_transfer" ||
                JsonSerializer.Serialize(intent) != JsonSerializer.Serialize(prepared.Intent))
                reasons.Add("dispatch_material_transfer_preparation_binding_drifted");
            else if (PreparedMaterialTransferContract.ContainsCustody(ledger, prepared.ReservationProjection) &&
                (!PreparedMaterialTransferContract.CustodyPositionsBound(ledger, prepared.ReservationProjection) ||
                 ledger.MaterialReservations.Any(row => row.OutputCustody is not null &&
                     prepared.MaterialRelocations.Any(move => move.MaterialReservationId == row.ReservationId))))
                reasons.Add("output_custody_relocation_requires_retained_terminal_lifecycle");
            else validatedPreparation = prepared;
        }
        ValidateMaterialTransferExecutionPayload(item, intent, validatedPreparation, reasons);
        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        result.Ready = result.BlockingReasons.Length == 0;
        result.Status = result.Ready ? bound ? "ready" : "not_applicable" : "blocked";
        return result;
    }

    private static void ValidateMaterialTransferExecutionPayload(ActionQueueItem item, MaterialTransferIntent? intent,
        PreparedMaterialTransfer? prepared, ICollection<string> reasons)
    {
        try
        {
            // Match the Live request reader, including its case-insensitive Web JSON semantics.
            var executionIntent = JsonSerializer.Deserialize<MaterialTransferIntent>(
                Parameter(item, "material_transfer_intent_json"), MaterialTransferExecutionJsonOptions);
            var executionProjection = JsonSerializer.Deserialize<MaterialTransferProjection>(
                Parameter(item, "material_transfer_projection_json"), MaterialTransferExecutionJsonOptions);
            if (executionIntent is null || JsonSerializer.Serialize(executionIntent) != JsonSerializer.Serialize(intent))
                reasons.Add("dispatch_material_transfer_execution_intent_drifted");
            if (executionProjection is null || executionProjection.SchemaVersion != "material_transfer_projection.v1" ||
                executionProjection.Status != "projected" || prepared is not null &&
                JsonSerializer.Serialize(executionProjection) != JsonSerializer.Serialize(
                    new MaterialTransferProjector().Project(prepared.BeforeInventoryGraph, prepared.Intent)))
                reasons.Add("dispatch_material_transfer_execution_projection_drifted");
        }
        catch (JsonException)
        {
            reasons.Add("dispatch_material_transfer_execution_json_invalid");
        }
    }
}
