using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed class MachineRelocationIntentLedgerService
{
    public StrategyCommitmentMutationResult Upsert(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        MachineRelocationIntentUpsertRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateRequest(snapshot, request, errors);
        var activeConflict = current?.MachineRelocationIntents.Any(row =>
            string.Equals(
                row.Status,
                StrategyCommitmentStatuses.Active,
                StringComparison.Ordinal) &&
            !string.Equals(
                row.IntentId,
                request.IntentId,
                StringComparison.Ordinal)) == true;
        if (activeConflict)
        {
            errors.Add("machine_relocation_active_intent_conflict");
        }
        if (errors.Count > 0)
        {
            return Rejected(current, errors);
        }

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        var previous = ledger.MachineRelocationIntents.FirstOrDefault(row =>
            string.Equals(
                row.IntentId,
                request.IntentId,
                StringComparison.Ordinal));
        var intent = new MachineRelocationIntent
        {
            IntentId = request.IntentId,
            Revision = (previous?.Revision ?? 0) + 1,
            Status = StrategyCommitmentStatuses.Active,
            SourceDecisionId = request.SourceDecisionId,
            SourceStateHash = snapshot.StateHash,
            QualifiedItemId = request.QualifiedItemId,
            ItemId = request.ItemId,
            SourceLocationId = request.SourceLocationId,
            SourceTileX = request.SourceTileX,
            SourceTileY = request.SourceTileY,
            TargetLocationId = request.TargetLocationId,
            TargetTileX = request.TargetTileX,
            TargetTileY = request.TargetTileY,
            MachinePlacementProjectionFingerprint =
                request.MachinePlacementProjectionFingerprint,
            LayoutNetBenefitTicks = request.LayoutNetBenefitTicks
        };
        ledger.MachineRelocationIntents =
            ledger.MachineRelocationIntents
                .Where(row => !string.Equals(
                    row.IntentId,
                    request.IntentId,
                    StringComparison.Ordinal))
                .Append(intent)
                .ToArray();
        StrategyCommitmentLedgerSupport.Advance(
            ledger,
            snapshot,
            updatedAt);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            intent.IntentId,
            intent.Revision,
            intent.SourceDecisionId,
            "machine_relocation_intent_upsert",
            updatedAt,
            "positive_layout_benefit_plan_accepted");
        return Accepted(ledger);
    }

    public StrategyCommitmentLedger ReconcileCompleted(
        StrategyCommitmentLedger current,
        SnapshotEnvelope snapshot,
        string updatedAt)
    {
        var completed = current.MachineRelocationIntents
            .Where(row =>
                string.Equals(
                    row.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal) &&
                MachineExists(
                    snapshot,
                    row.TargetLocationId,
                    row.TargetTileX,
                    row.TargetTileY,
                    row.QualifiedItemId))
            .Select(row => row.IntentId)
            .ToHashSet(StringComparer.Ordinal);
        if (completed.Count == 0)
        {
            return current;
        }

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        ledger.MachineRelocationIntents =
            ledger.MachineRelocationIntents.Select(row =>
            {
                if (!completed.Contains(row.IntentId))
                {
                    return row;
                }
                var updated =
                    StrategyCommitmentLedgerSupport
                        .CloneMachineRelocation(row);
                updated.Revision++;
                updated.Status = StrategyCommitmentStatuses.Completed;
                updated.CompletionReason =
                    "exact_target_machine_observed";
                return updated;
            }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(
            ledger,
            snapshot,
            updatedAt);
        foreach (var intent in ledger.MachineRelocationIntents.Where(row =>
            completed.Contains(row.IntentId)))
        {
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                intent.IntentId,
                intent.Revision,
                intent.SourceDecisionId,
                "machine_relocation_intent_complete",
                updatedAt,
                intent.CompletionReason);
        }
        return ledger;
    }

    private static void ValidateRequest(
        SnapshotEnvelope snapshot,
        MachineRelocationIntentUpsertRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.IntentId))
        {
            errors.Add("machine_relocation_intent_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
        {
            errors.Add("machine_relocation_source_decision_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            errors.Add("machine_relocation_qualified_item_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.SourceLocationId) ||
            string.IsNullOrWhiteSpace(request.TargetLocationId) ||
            !string.Equals(
                request.SourceLocationId,
                request.TargetLocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "machine_relocation_same_map_location_required");
        }
        if (request.LayoutNetBenefitTicks <= 0)
        {
            errors.Add(
                "machine_relocation_positive_layout_benefit_required");
        }

        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "machine_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(
                    placement.Value,
                    "static_projection_fingerprint"),
                request.MachinePlacementProjectionFingerprint,
                StringComparison.Ordinal))
        {
            errors.Add(
                "machine_relocation_placement_projection_drifted");
        }
        if (!TargetIsNativeLegal(
                placement,
                request.QualifiedItemId,
                request.TargetLocationId,
                request.TargetTileX,
                request.TargetTileY))
        {
            errors.Add(
                "machine_relocation_target_not_native_legal");
        }

        var source = FindMachine(
            snapshot,
            request.SourceLocationId,
            request.SourceTileX,
            request.SourceTileY);
        if (!source.HasValue ||
            !string.Equals(
                ReadString(source.Value, "qualified_item_id"),
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                "machine_relocation_source_identity_drifted");
        }
        else if (ReadBool(source.Value, "removal_safe_now") != true)
        {
            errors.Add(
                "machine_relocation_source_not_removal_safe");
        }
    }

    private static bool TargetIsNativeLegal(
        JsonElement? placement,
        string qualifiedItemId,
        string locationId,
        int targetX,
        int targetY)
    {
        if (!placement.HasValue ||
            !placement.Value.TryGetProperty(
                "relocation_rows",
                out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in rows.EnumerateArray().Where(row =>
            row.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(row, "qualified_item_id"),
                qualifiedItemId,
                StringComparison.OrdinalIgnoreCase)))
        {
            if (!row.TryGetProperty("locations", out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var location in locations.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "location_id"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                if (!location.TryGetProperty(
                        "static_legal_tile_ranges",
                        out var ranges) ||
                    ranges.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                return ranges.EnumerateArray().Any(range =>
                    range.ValueKind == JsonValueKind.Object &&
                    ReadInt(range, "y") == targetY &&
                    targetX >= ReadInt(range, "start_x") &&
                    targetX <= ReadInt(
                        range,
                        "end_x",
                        ReadInt(range, "start_x") - 1));
            }
        }
        return false;
    }

    private static bool MachineExists(
        SnapshotEnvelope snapshot,
        string locationId,
        int x,
        int y,
        string qualifiedItemId)
    {
        var machine = FindMachine(snapshot, locationId, x, y);
        return machine.HasValue &&
            string.Equals(
                ReadString(machine.Value, "qualified_item_id"),
                qualifiedItemId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement? FindMachine(
        SnapshotEnvelope snapshot,
        string locationId,
        int x,
        int y)
    {
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue ||
            machines.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in machines.Value.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == x &&
                ReadInt(row, "tile_y") == y &&
                string.Equals(
                    ReadString(row, "location_id"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }
        return null;
    }

    private static StrategyCommitmentMutationResult Accepted(
        StrategyCommitmentLedger ledger) => new()
        {
            Accepted = true,
            Ledger = ledger
        };

    private static StrategyCommitmentMutationResult Rejected(
        StrategyCommitmentLedger? ledger,
        IEnumerable<string> errors) => new()
        {
            Accepted = false,
            Ledger = ledger,
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray()
        };
}
