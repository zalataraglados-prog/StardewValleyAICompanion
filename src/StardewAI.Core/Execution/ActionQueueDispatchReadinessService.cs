using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

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
                "executor.donate_museum_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_machine_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_storage_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_quest_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_crab_pot",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.craft_rain_totem",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.place_machine",
                StringComparison.Ordinal) &&
            !string.Equals(
                item.OptionId,
                "executor.place_storage",
                StringComparison.Ordinal) &&
            !(string.Equals(
                  item.OptionId,
                  "executor.load_machine_input",
                  StringComparison.Ordinal) &&
              !string.IsNullOrWhiteSpace(
                  Parameter(
                      item,
                      "machine_support_intent_id"))) &&
            !IsStrategicMachineRemoval(item))
        {
            result.Ready = true;
            result.Status = "not_applicable";
            return result;
        }

        var reasons = new List<string>();
        if (item.OptionId == "executor.donate_museum_item")
        {
            if (item.NormalizedCommand.Parameters.Where(row =>
                    row.Name.StartsWith("material_reservation_", StringComparison.Ordinal) ||
                    row.Name.StartsWith("commitment_ledger_", StringComparison.Ordinal) ||
                    row.Name.StartsWith("museum_output_custody_", StringComparison.Ordinal) ||
                    row.Name is "museum_donation_save_id" or "museum_donation_player_id" or
                        "inventory_slot_index" or "qualified_item_id" or "expected_stack_before")
                .GroupBy(row => row.Name, StringComparer.Ordinal).Any(group => group.Count() != 1))
                reasons.Add("dispatch_museum_parameters_not_unique");
            if (currentStateHash != item.NormalizedCommand.StateHash)
                reasons.Add("dispatch_museum_donation_state_hash_drifted");
            if (Parameter(item, "museum_donation_save_id") != currentLedger.SaveId ||
                Parameter(item, "museum_donation_player_id") != currentLedger.PlayerId)
                reasons.Add("dispatch_museum_donation_actor_identity_drifted");
            var custodyId = Parameter(item, "museum_output_custody_reservation_id");
            var authorizedQuantity = 0;
            if (!string.IsNullOrWhiteSpace(custodyId))
            {
                var custody = currentLedger.MaterialReservations.SingleOrDefault(row => row.ReservationId == custodyId);
                if (custody is null || string.IsNullOrWhiteSpace(item.NormalizedCommand.GoalId) ||
                    !MaterialOutputCustodyContract.IsAuthorized(custody, currentLedger,
                        MaterialOutputCustodyContract.MuseumConsumer, item.NormalizedCommand.GoalId) ||
                    IntParameter(item, "museum_output_custody_reservation_revision") != custody.Revision ||
                    IntParameter(item, "inventory_slot_index") != custody.SlotIndex ||
                    Parameter(item, "qualified_item_id") != custody.QualifiedItemId ||
                    custody.NodeId != "player:" + currentLedger.PlayerId)
                    reasons.Add("dispatch_museum_output_custody_binding_drifted");
                else authorizedQuantity = custody.Quantity;
            }
            var slot = IntParameter(item, "inventory_slot_index");
            var stack = IntParameter(item, "expected_stack_before");
            var qualified = Parameter(item, "qualified_item_id");
            var reserved = currentLedger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.NodeId == "player:" + currentLedger.PlayerId && row.SlotIndex == slot && row.QualifiedItemId == qualified)
                .Sum(row => (long)row.Quantity);
            if (!slot.HasValue || slot < 0 || !stack.HasValue || string.IsNullOrWhiteSpace(qualified) ||
                (long)stack.GetValueOrDefault() - reserved + authorizedQuantity < 1 ||
                string.IsNullOrWhiteSpace(custodyId) && !string.IsNullOrWhiteSpace(item.NormalizedCommand.GoalId))
                reasons.Add("dispatch_museum_reserved_quantity_or_custody_binding_invalid");
        }
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

        var supportIntentId = Parameter(
            item,
            "machine_support_intent_id");
        if (!string.IsNullOrWhiteSpace(supportIntentId))
        {
            var supportIntent =
                currentLedger.MachineSupportIntents.FirstOrDefault(row =>
                    string.Equals(
                        row.IntentId,
                        supportIntentId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        row.Status,
                        StrategyCommitmentStatuses.Active,
                        StringComparison.Ordinal));
            if (supportIntent is null ||
                IntParameter(
                    item,
                    "machine_support_intent_revision") !=
                    supportIntent.Revision ||
                !string.Equals(
                    Parameter(
                        item,
                        "machine_support_intent_stage"),
                    supportIntent.Stage,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    Parameter(
                        item,
                        "machine_support_intent_source_state_hash"),
                    supportIntent.SourceStateHash,
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "dispatch_machine_support_intent_drifted");
            }
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
