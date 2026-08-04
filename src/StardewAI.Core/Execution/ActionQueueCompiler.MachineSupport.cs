using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static bool MachineSupportContinuationMatches(
        SmallModelAction action,
        MachineSupportContinuation expected)
    {
        return MachineSupportIntentProjection.Parameters(expected)
            .All(parameter => string.Equals(
                ReadParameter(action, parameter.Name),
                parameter.Value,
                StringComparison.Ordinal));
    }

    private static bool MachineInputReservationMatches(
        SmallModelAction action,
        MachineInputMaterialReservationGuardResult guard,
        int requiredCount)
    {
        return ReadIntParameter(
                action,
                "machine_input_required_count") == requiredCount &&
            string.Equals(
                ReadParameter(action, "commitment_ledger_id"),
                guard.LedgerId,
                StringComparison.Ordinal) &&
            ReadIntParameter(
                action,
                "commitment_ledger_revision") == guard.LedgerRevision &&
            string.Equals(
                ReadParameter(
                    action,
                    "material_reservation_guard_status"),
                guard.Status,
                StringComparison.Ordinal) &&
            string.Equals(
                ReadParameter(
                    action,
                    "material_reservation_ledger_id"),
                guard.LedgerId,
                StringComparison.Ordinal) &&
            ReadIntParameter(
                action,
                "material_reservation_ledger_revision") ==
                guard.LedgerRevision &&
            string.Equals(
                ReadParameter(
                    action,
                    "material_reservation_ids_json"),
                JsonSerializer.Serialize(guard.ReservationIds),
                StringComparison.Ordinal);
    }
}
