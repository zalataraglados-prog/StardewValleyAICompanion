using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed class MachineSupportIntentLedgerService
{
    private const string EarnMoneyGoal =
        "goal.economy.earn_money";
    private const string CapacityDemand =
        "production_capacity_requirement";
    private const string SupportKind =
        "machine_capacity_current_backlog";
    private const string TaskCapacityDemand =
        "priority_task_requirement";
    private const string TaskSupportKind =
        "machine_capacity_active_collection_task";

    public StrategyCommitmentMutationResult Upsert(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        MachineSupportIntentUpsertRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        if (string.IsNullOrWhiteSpace(request.IntentId))
        {
            errors.Add("machine_support_intent_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
        {
            errors.Add("machine_support_source_decision_id_required");
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            errors.Add("machine_support_machine_identity_required");
        }

        var existing = current?.MachineSupportIntents
            .FirstOrDefault(row => string.Equals(
                row.IntentId,
                request.IntentId,
                StringComparison.Ordinal));
        if (string.Equals(
                request.Stage,
                MachineSupportIntentStages.CraftSelected,
                StringComparison.Ordinal))
        {
            ValidateCraftSelection(request, existing, errors);
        }
        else if (string.Equals(
                     request.Stage,
                     MachineSupportIntentStages.PlacementBound,
                     StringComparison.Ordinal))
        {
            ValidatePlacementBinding(
                snapshot,
                request,
                existing,
                errors);
        }
        else
        {
            errors.Add("machine_support_stage_unsupported");
        }

        if (errors.Count > 0)
        {
            return Rejected(current, errors);
        }

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        existing = ledger.MachineSupportIntents.FirstOrDefault(row =>
            string.Equals(
                row.IntentId,
                request.IntentId,
                StringComparison.Ordinal));
        var next = string.Equals(
                request.Stage,
                MachineSupportIntentStages.CraftSelected,
                StringComparison.Ordinal)
            ? FromCraftRequest(request, snapshot, existing)
            : FromPlacementRequest(
                request,
                snapshot,
                existing);
        ledger.MachineSupportIntents = ledger.MachineSupportIntents
            .Where(row => !string.Equals(
                row.IntentId,
                request.IntentId,
                StringComparison.Ordinal))
            .Append(next)
            .OrderBy(row => row.IntentId, StringComparer.Ordinal)
            .ToArray();
        StrategyCommitmentLedgerSupport.Advance(
            ledger,
            snapshot,
            updatedAt);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            next.IntentId,
            next.Revision,
            next.SourceDecisionId,
            request.Stage == MachineSupportIntentStages.CraftSelected
                ? "machine_support_select"
                : "machine_support_bind_placement",
            updatedAt,
            request.Stage);
        return Accepted(ledger);
    }

    public StrategyCommitmentLedger ReconcileCompleted(
        StrategyCommitmentLedger current,
        SnapshotEnvelope snapshot,
        string updatedAt)
    {
        var completed = current.MachineSupportIntents
            .Where(row =>
                string.Equals(
                    row.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal) &&
                string.Equals(
                    row.Stage,
                    MachineSupportIntentStages.PlacementBound,
                    StringComparison.Ordinal) &&
                TargetMachineProcessing(snapshot, row))
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
        ledger.MachineSupportIntents = ledger.MachineSupportIntents
            .Select(row => completed.Contains(row.IntentId)
                ? Completed(row)
                : row)
            .ToArray();
        StrategyCommitmentLedgerSupport.Advance(
            ledger,
            snapshot,
            updatedAt);
        foreach (var row in ledger.MachineSupportIntents.Where(row =>
                     completed.Contains(row.IntentId)))
        {
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                row.IntentId,
                row.Revision,
                row.SourceDecisionId,
                "machine_support_complete",
                updatedAt,
                row.CompletionReason);
        }
        return ledger;
    }

    private static void ValidateCraftSelection(
        MachineSupportIntentUpsertRequest request,
        MachineSupportIntent? existing,
        ICollection<string> errors)
    {
        var taskSupport = IsTaskSupport(request);
        var economicSupport = string.Equals(
                request.GoalId,
                EarnMoneyGoal,
                StringComparison.Ordinal) &&
            string.Equals(
                request.DemandClass,
                CapacityDemand,
                StringComparison.Ordinal) &&
            string.Equals(
                request.SupportKind,
                SupportKind,
                StringComparison.Ordinal);
        if (!economicSupport && !taskSupport)
        {
            errors.Add("machine_support_rule_not_vetted");
        }
        if (economicSupport &&
            (request.GrossBenefit <= 0 ||
             request.OpportunityCost < 0 ||
             request.NetBenefit <= 0 ||
             (long)request.GrossBenefit -
             request.OpportunityCost != request.NetBenefit ||
             request.SupportScore is < 0.01 or > 0.12 ||
             request.RequiredAdditionalMachineCount <= 0 ||
             string.IsNullOrWhiteSpace(request.EvidenceStatus)))
        {
            errors.Add("machine_support_value_contract_invalid");
        }
        if (taskSupport &&
            (request.GrossBenefit != 0 ||
             request.OpportunityCost != 0 ||
             request.NetBenefit != 0 ||
             request.SupportScore != 0.12 ||
             request.RequiredAdditionalMachineCount != 1 ||
             string.IsNullOrWhiteSpace(request.GoalId) ||
             !string.Equals(
                 request.EvidenceStatus,
                 request.TaskSourcesJson,
                 StringComparison.Ordinal) ||
             !ExactCollectionTaskSources(request.TaskSourcesJson)))
        {
            errors.Add("machine_support_task_contract_invalid");
        }
        if (!string.IsNullOrWhiteSpace(request.TargetLocationId) ||
            request.TargetTileX.HasValue ||
            request.TargetTileY.HasValue)
        {
            errors.Add("machine_support_craft_must_not_precommit_tile");
        }
        if (existing is not null &&
            (!string.Equals(
                 existing.Status,
                 StrategyCommitmentStatuses.Active,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 existing.Stage,
                 MachineSupportIntentStages.CraftSelected,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 existing.GoalId,
                 request.GoalId,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 existing.QualifiedItemId,
                 request.QualifiedItemId,
                 StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("machine_support_existing_intent_conflict");
        }
    }

    private static void ValidatePlacementBinding(
        SnapshotEnvelope snapshot,
        MachineSupportIntentUpsertRequest request,
        MachineSupportIntent? existing,
        ICollection<string> errors)
    {
        var initialTaskPlacement = existing is null &&
            IsTaskSupport(request) &&
            request.GrossBenefit == 0 &&
            request.OpportunityCost == 0 &&
            request.NetBenefit == 0 &&
            request.SupportScore == 0.12 &&
            request.RequiredAdditionalMachineCount == 1 &&
            !string.IsNullOrWhiteSpace(request.GoalId) &&
            string.Equals(
                request.EvidenceStatus,
                request.TaskSourcesJson,
                StringComparison.Ordinal) &&
            ExactCollectionTaskSources(request.TaskSourcesJson);
        if (!initialTaskPlacement &&
            (existing is null ||
             !string.Equals(
                 existing.Status,
                 StrategyCommitmentStatuses.Active,
                 StringComparison.Ordinal)))
        {
            errors.Add("machine_support_active_intent_required");
            return;
        }
        if (existing is not null &&
            (!string.Equals(
                existing.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                existing.ItemId,
                request.ItemId,
                StringComparison.Ordinal)))
        {
            errors.Add("machine_support_machine_identity_drifted");
        }
        if (string.IsNullOrWhiteSpace(request.TargetLocationId) ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            request.TargetTileX < 0 ||
            request.TargetTileY < 0 ||
            !string.Equals(
                request.TargetLocationId,
                ReadStateFieldString(
                    snapshot,
                    "player",
                    "location_id"),
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("machine_support_loaded_target_required");
        }
    }

    private static MachineSupportIntent FromCraftRequest(
        MachineSupportIntentUpsertRequest request,
        SnapshotEnvelope snapshot,
        MachineSupportIntent? existing) => new()
        {
            IntentId = request.IntentId,
            Revision = (existing?.Revision ?? 0) + 1,
            Status = StrategyCommitmentStatuses.Active,
            Stage = MachineSupportIntentStages.CraftSelected,
            SourceDecisionId = request.SourceDecisionId,
            SourceStateHash = snapshot.StateHash,
            GoalId = request.GoalId,
            QualifiedItemId = request.QualifiedItemId,
            ItemId = request.ItemId,
            DemandClass = request.DemandClass,
            SupportKind = request.SupportKind,
            EvidenceStatus = request.EvidenceStatus,
            TaskSourcesJson = request.TaskSourcesJson,
            GrossBenefit = request.GrossBenefit,
            OpportunityCost = request.OpportunityCost,
            NetBenefit = request.NetBenefit,
            SupportScore = request.SupportScore,
            RequiredAdditionalMachineCount =
                request.RequiredAdditionalMachineCount
        };

    private static MachineSupportIntent FromPlacementRequest(
        MachineSupportIntentUpsertRequest request,
        SnapshotEnvelope snapshot,
        MachineSupportIntent? existing)
    {
        var next = existing is null
            ? FromCraftRequest(request, snapshot, null)
            : StrategyCommitmentLedgerSupport.CloneMachineSupport(existing);
        if (existing is not null)
        {
            next.Revision++;
        }
        next.Stage = MachineSupportIntentStages.PlacementBound;
        next.SourceDecisionId = request.SourceDecisionId;
        next.SourceStateHash = snapshot.StateHash;
        next.TargetLocationId = request.TargetLocationId;
        next.TargetTileX = request.TargetTileX;
        next.TargetTileY = request.TargetTileY;
        return next;
    }

    private static bool IsTaskSupport(
        MachineSupportIntentUpsertRequest request) =>
        string.Equals(
            request.DemandClass,
            TaskCapacityDemand,
            StringComparison.Ordinal) &&
        string.Equals(
            request.SupportKind,
            TaskSupportKind,
            StringComparison.Ordinal);

    private static bool ExactCollectionTaskSources(string json)
    {
        try
        {
            var sources = JsonSerializer.Deserialize<string[]>(json) ??
                Array.Empty<string>();
            var canonical = sources
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(source => source, StringComparer.Ordinal)
                .ToArray();
            return sources.Length > 0 &&
                sources.Length == canonical.Length &&
                sources.SequenceEqual(canonical, StringComparer.Ordinal) &&
                sources.All(source =>
                    source.StartsWith(
                        "ordinary_quest:ResourceCollectionQuest:",
                        StringComparison.Ordinal) ||
                    source.StartsWith(
                        "special_order:",
                        StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static MachineSupportIntent Completed(
        MachineSupportIntent row)
    {
        var next = StrategyCommitmentLedgerSupport.CloneMachineSupport(
            row);
        next.Revision++;
        next.Status = StrategyCommitmentStatuses.Completed;
        next.CompletionReason =
            "exact_target_machine_processing_observed";
        return next;
    }

    private static bool TargetMachineProcessing(
        SnapshotEnvelope snapshot,
        MachineSupportIntent intent)
    {
        if (string.IsNullOrWhiteSpace(intent.TargetLocationId) ||
            !intent.TargetTileX.HasValue ||
            !intent.TargetTileY.HasValue)
        {
            return false;
        }

        var machines = ReadStateFieldValue(
            snapshot,
            "farm",
            "machines");
        if (!machines.HasValue ||
            machines.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return machines.Value.EnumerateArray().Any(machine =>
            machine.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(machine, "location_id", "Farm"),
                intent.TargetLocationId,
                StringComparison.OrdinalIgnoreCase) &&
            ReadInt(machine, "tile_x", int.MinValue) ==
                intent.TargetTileX &&
            ReadInt(machine, "tile_y", int.MinValue) ==
                intent.TargetTileY &&
            string.Equals(
                ReadString(machine, "qualified_item_id"),
                intent.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) &&
            (ReadInt(machine, "minutes_until_ready") > 0 ||
             ReadBool(machine, "ready_for_harvest") == true));
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
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
            Ledger = ledger
        };
}
