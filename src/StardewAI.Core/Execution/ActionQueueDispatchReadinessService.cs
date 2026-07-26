using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Execution;

public sealed class ActionQueueDispatchReadinessService
{
    public ActionQueueDispatchReadiness Evaluate(
        ActionQueueEnvelope queue,
        ActionQueueItem item,
        StrategyCommitmentLedger currentLedger,
        string currentStateHash)
    {
        var result = new ActionQueueDispatchReadiness
        {
            QueueId = queue.QueueId,
            QueueItemId = item.QueueItemId,
            OptionId = item.OptionId,
            StateHash = currentStateHash,
            CurrentLedgerId = currentLedger.LedgerId,
            CurrentLedgerRevision = currentLedger.Revision
        };
        if (!string.Equals(
                item.OptionId,
                "executor.craft_machine_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_storage_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.place_machine",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.place_storage",
                StringComparison.Ordinal) &&
            !IsStrategicMachineRemoval(item))
        {
            result.Ready = true;
            result.Status = "not_applicable";
            return result;
        }

        var reasons = new List<string>();
        if (!string.Equals(item.Status, "pending", StringComparison.Ordinal))
        {
            reasons.Add("dispatch_queue_item_not_pending");
        }

        if (IsStrategicMachineRemoval(item))
        {
            var intentId = Parameter(item, "relocation_intent_id");
            var intent = currentLedger.MachineRelocationIntents
                .FirstOrDefault(row =>
                    string.Equals(
                        row.Status,
                        StrategyCommitmentStatuses.Active,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        row.IntentId,
                        intentId,
                        StringComparison.Ordinal));
            if (intent is null)
            {
                reasons.Add(
                    "dispatch_machine_relocation_intent_not_active");
            }
            result.BlockingReasons =
                reasons.Distinct(StringComparer.Ordinal).ToArray();
            result.Ready = result.BlockingReasons.Length == 0;
            result.Status = result.Ready ? "ready" : "blocked";
            return result;
        }

        var guardStatus = Parameter(item, "material_reservation_guard_status");
        if (!string.Equals(guardStatus, "ready", StringComparison.Ordinal) &&
            !string.Equals(
                guardStatus,
                "ready_no_active_material_reservations",
                StringComparison.Ordinal))
        {
            reasons.Add("dispatch_material_reservation_guard_not_ready");
        }

        var requiredMaterialLedgerId = Parameter(item, "material_reservation_ledger_id");
        var requiredCommitmentLedgerId = Parameter(item, "commitment_ledger_id");
        var requiredMaterialRevision = IntParameter(
            item,
            "material_reservation_ledger_revision");
        var requiredCommitmentRevision = IntParameter(item, "commitment_ledger_revision");
        result.RequiredLedgerId = requiredMaterialLedgerId;
        result.RequiredLedgerRevision = requiredMaterialRevision;
        if (string.IsNullOrWhiteSpace(requiredMaterialLedgerId) ||
            string.IsNullOrWhiteSpace(requiredCommitmentLedgerId) ||
            !requiredMaterialRevision.HasValue ||
            !requiredCommitmentRevision.HasValue)
        {
            reasons.Add("dispatch_strategy_ledger_binding_incomplete");
        }
        if (!string.Equals(
                requiredMaterialLedgerId,
                requiredCommitmentLedgerId,
                StringComparison.Ordinal) ||
            requiredMaterialRevision != requiredCommitmentRevision)
        {
            reasons.Add("dispatch_strategy_ledger_bindings_disagree");
        }
        if (!string.Equals(
                requiredMaterialLedgerId,
                currentLedger.LedgerId,
                StringComparison.Ordinal))
        {
            reasons.Add("dispatch_strategy_ledger_id_drifted");
        }
        if (requiredMaterialRevision != currentLedger.Revision)
        {
            reasons.Add("dispatch_strategy_ledger_revision_drifted");
        }

        var reservationIds = StringArrayParameter(
            item,
            "material_reservation_ids_json",
            reasons);
        if (reservationIds is not null)
        {
            var activeIds = currentLedger.MaterialReservations
                .Where(row => string.Equals(
                    row.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal))
                .Select(row => row.ReservationId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var reservationId in reservationIds)
            {
                if (!activeIds.Contains(reservationId))
                {
                    reasons.Add(
                        "dispatch_material_reservation_not_active:" + reservationId);
                }
            }
        }

        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        result.Ready = result.BlockingReasons.Length == 0;
        result.Status = result.Ready ? "ready" : "blocked";
        return result;
    }

    private static string Parameter(ActionQueueItem item, string name) =>
        item.NormalizedCommand.Parameters.FirstOrDefault(row =>
            string.Equals(row.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    private static int? IntParameter(ActionQueueItem item, string name) =>
        int.TryParse(Parameter(item, name), out var value) ? value : null;

    private static bool IsStrategicMachineRemoval(
        ActionQueueItem item) =>
        string.Equals(
            item.OptionId,
            "executor.remove_machine",
            StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(
            Parameter(item, "relocation_target_location_id"));

    private static string[]? StringArrayParameter(
        ActionQueueItem item,
        string name,
        ICollection<string> reasons)
    {
        var value = Parameter(item, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            reasons.Add("dispatch_material_reservation_ids_missing");
            return null;
        }
        try
        {
            var ids = JsonSerializer.Deserialize<string[]>(value);
            if (ids is null ||
                ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            {
                reasons.Add("dispatch_material_reservation_ids_invalid");
                return null;
            }
            return ids;
        }
        catch (JsonException)
        {
            reasons.Add("dispatch_material_reservation_ids_invalid");
            return null;
        }
    }
}
