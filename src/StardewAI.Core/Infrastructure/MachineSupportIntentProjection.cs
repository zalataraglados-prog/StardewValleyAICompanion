using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineSupportContinuation(
    string Status,
    string Kind,
    string IntentId,
    int IntentRevision,
    string IntentStage,
    string SourceStateHash,
    string GoalId,
    string DemandClass,
    int OriginalNetBenefit,
    int CurrentInputNetBenefit,
    double Score,
    string Reason);

internal static class MachineSupportIntentProjection
{
    public static MachineSupportIntent? SelectForPlacement(
        StrategyCommitmentLedger? ledger,
        string qualifiedItemId,
        string currentLocationId)
    {
        return ledger?.MachineSupportIntents
            .Where(IsValid)
            .Where(row => string.Equals(
                row.QualifiedItemId,
                qualifiedItemId,
                StringComparison.OrdinalIgnoreCase))
            .Where(row =>
                string.Equals(
                    row.Stage,
                    MachineSupportIntentStages.CraftSelected,
                    StringComparison.Ordinal) ||
                (string.Equals(
                     row.Stage,
                     MachineSupportIntentStages.PlacementBound,
                     StringComparison.Ordinal) &&
                 string.Equals(
                     row.TargetLocationId,
                     currentLocationId,
                     StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(row => string.Equals(
                row.Stage,
                MachineSupportIntentStages.PlacementBound,
                StringComparison.Ordinal))
            .ThenBy(row => row.IntentId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static MachineSupportIntent? SelectForLoad(
        StrategyCommitmentLedger? ledger,
        string qualifiedItemId,
        string locationId,
        int tileX,
        int tileY)
    {
        return ledger?.MachineSupportIntents
            .Where(IsValid)
            .Where(row =>
                string.Equals(
                    row.Stage,
                    MachineSupportIntentStages.PlacementBound,
                    StringComparison.Ordinal) &&
                string.Equals(
                    row.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    row.TargetLocationId,
                    locationId,
                    StringComparison.OrdinalIgnoreCase) &&
                row.TargetTileX == tileX &&
                row.TargetTileY == tileY)
            .OrderBy(row => row.IntentId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static MachineSupportContinuation Placement(
        MachineSupportIntent? intent)
    {
        return intent is null
            ? None("place_machine")
            : new(
                "active",
                "place_supported_machine",
                intent.IntentId,
                intent.Revision,
                intent.Stage,
                intent.SourceStateHash,
                intent.GoalId,
                intent.DemandClass,
                intent.NetBenefit,
                0,
                intent.SupportScore,
                string.Equals(
                    intent.DemandClass,
                    "priority_task_requirement",
                    StringComparison.Ordinal)
                        ? "continue_committed_task_machine_capacity"
                        : "continue_committed_positive_machine_capacity");
    }

    public static MachineSupportContinuation Load(
        MachineSupportIntent? intent,
        int? currentInputNetBenefit)
    {
        if (intent is null)
        {
            return None("load_machine_input");
        }

        if (string.Equals(
                intent.DemandClass,
                "priority_task_requirement",
                StringComparison.Ordinal))
        {
            return new(
                "active",
                "load_task_supported_machine",
                intent.IntentId,
                intent.Revision,
                intent.Stage,
                intent.SourceStateHash,
                intent.GoalId,
                intent.DemandClass,
                0,
                currentInputNetBenefit ?? 0,
                intent.SupportScore,
                "exact_task_binding_owns_input_value_tradeoff");
        }

        if (!currentInputNetBenefit.HasValue ||
            currentInputNetBenefit <= 0)
        {
            return new(
                "blocked_current_input_not_positive",
                "load_supported_machine",
                intent.IntentId,
                intent.Revision,
                intent.Stage,
                intent.SourceStateHash,
                intent.GoalId,
                intent.DemandClass,
                intent.NetBenefit,
                currentInputNetBenefit ?? 0,
                0,
                "continuation_requires_fresh_positive_input_net_value");
        }

        return new(
            "active",
            "load_supported_machine",
            intent.IntentId,
            intent.Revision,
            intent.Stage,
            intent.SourceStateHash,
            intent.GoalId,
            intent.DemandClass,
            intent.NetBenefit,
            currentInputNetBenefit.Value,
            intent.SupportScore,
            "fresh_positive_input_continues_committed_machine_capacity");
    }

    public static int? CurrentInputNetValue(
        JsonElement machine,
        JsonElement input)
    {
        if (!TryReadInt(
                input,
                "sale_price",
                out var inputSalePrice) ||
            inputSalePrice < 0 ||
            !input.TryGetProperty(
                "predicted_output",
                out var output) ||
            output.ValueKind != JsonValueKind.Object ||
            !TryReadInt(
                output,
                "sale_price",
                out var outputSalePrice) ||
            outputSalePrice < 0 ||
            !TryReadInt(output, "stack", out var outputStack) ||
            outputStack <= 0)
        {
            return null;
        }

        var additionalCountKnown =
            TryReadInt(
                output,
                "additional_consumed_item_count",
                out var additionalCount);
        if (!additionalCountKnown &&
            machine.TryGetProperty(
                "machine_data",
                out var machineData) &&
            machineData.ValueKind == JsonValueKind.Object)
        {
            additionalCountKnown = TryReadInt(
                machineData,
                "additional_consumed_item_count",
                out additionalCount);
        }
        if (!additionalCountKnown || additionalCount != 0)
        {
            return null;
        }

        var requiredCount = RequiredInputCount(input);
        var value =
            (long)outputSalePrice * outputStack -
            (long)inputSalePrice * requiredCount;
        return value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : null;
    }

    public static int RequiredInputCount(JsonElement input)
    {
        return input.TryGetProperty(
                "predicted_output",
                out var output) &&
            output.ValueKind == JsonValueKind.Object &&
            TryReadInt(
                output,
                "required_count",
                out var projectedRequiredCount) &&
            projectedRequiredCount > 0
                ? projectedRequiredCount
                : 1;
    }

    public static string ExpectedEffectSuffix(
        MachineSupportContinuation continuation) =>
        ";machine_support_continuation_status=" +
        continuation.Status +
        ";machine_support_continuation_kind=" +
        continuation.Kind +
        ";machine_support_intent_id=" +
        continuation.IntentId +
        ";machine_support_intent_revision=" +
        continuation.IntentRevision +
        ";machine_support_intent_stage=" +
        continuation.IntentStage +
        ";machine_support_intent_source_state_hash=" +
        continuation.SourceStateHash +
        ";machine_support_goal_id=" +
        continuation.GoalId +
        ";machine_support_demand_class=" +
        continuation.DemandClass +
        ";machine_support_original_net_benefit=" +
        continuation.OriginalNetBenefit +
        ";machine_support_current_input_net_benefit=" +
        continuation.CurrentInputNetBenefit +
        ";machine_support_continuation_score=" +
        Format(continuation.Score) +
        ";machine_support_continuation_reason=" +
        continuation.Reason;

    public static SmallModelActionParameter[] Parameters(
        MachineSupportContinuation continuation) =>
        [
            Parameter(
                "machine_support_continuation_status",
                continuation.Status),
            Parameter(
                "machine_support_continuation_kind",
                continuation.Kind),
            Parameter(
                "machine_support_intent_id",
                continuation.IntentId),
            Parameter(
                "machine_support_intent_revision",
                continuation.IntentRevision.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "machine_support_intent_stage",
                continuation.IntentStage),
            Parameter(
                "machine_support_intent_source_state_hash",
                continuation.SourceStateHash),
            Parameter(
                "machine_support_goal_id",
                continuation.GoalId),
            Parameter(
                "machine_support_demand_class",
                continuation.DemandClass),
            Parameter(
                "machine_support_original_net_benefit",
                continuation.OriginalNetBenefit.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "machine_support_current_input_net_benefit",
                continuation.CurrentInputNetBenefit.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "machine_support_continuation_score",
                Format(continuation.Score)),
            Parameter(
                "machine_support_continuation_reason",
                continuation.Reason)
        ];

    public static bool IsValid(MachineSupportIntent row) =>
        string.Equals(
            row.Status,
            StrategyCommitmentStatuses.Active,
            StringComparison.Ordinal) &&
        (EconomicIntentIsValid(row) || TaskIntentIsValid(row));

    public static bool TaskDemandMatchesSnapshot(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? ledger,
        MachineSupportIntent intent)
    {
        if (!TaskIntentIsValid(intent))
        {
            return true;
        }

        var context = ReadStateFieldValue(
            snapshot,
            "player",
            "machine_crafting");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var recipes = rows.EnumerateArray().Where(row =>
            row.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(row, "output_qualified_item_id"),
                intent.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase)).ToArray();
        if (recipes.Length != 1)
        {
            return false;
        }

        var current = MachineDemandProjectionEvaluator.Evaluate(
            snapshot,
            recipes[0],
            ledger);
        return string.Equals(
                current.DemandClass,
                "priority_task_requirement",
                StringComparison.Ordinal) &&
            string.Equals(
                JsonSerializer.Serialize(current.PriorityTaskSources),
                intent.TaskSourcesJson,
                StringComparison.Ordinal);
    }

    private static bool EconomicIntentIsValid(
        MachineSupportIntent row) =>
        string.Equals(
            row.GoalId,
            "goal.economy.earn_money",
            StringComparison.Ordinal) &&
        string.Equals(
            row.DemandClass,
            "production_capacity_requirement",
            StringComparison.Ordinal) &&
        string.Equals(
            row.SupportKind,
            "machine_capacity_current_backlog",
            StringComparison.Ordinal) &&
        row.GrossBenefit > 0 &&
        row.OpportunityCost >= 0 &&
        row.NetBenefit > 0 &&
        (long)row.GrossBenefit - row.OpportunityCost ==
            row.NetBenefit &&
        row.SupportScore is >= 0.01 and <= 0.12;

    private static bool TaskIntentIsValid(MachineSupportIntent row)
    {
        if (!string.Equals(
                row.DemandClass,
                "priority_task_requirement",
                StringComparison.Ordinal) ||
            !string.Equals(
                row.SupportKind,
                "machine_capacity_active_collection_task",
                StringComparison.Ordinal) ||
            row.GrossBenefit != 0 ||
            row.OpportunityCost != 0 ||
            row.NetBenefit != 0 ||
            row.SupportScore != 0.12 ||
            row.RequiredAdditionalMachineCount != 1 ||
            string.IsNullOrWhiteSpace(row.GoalId) ||
            !string.Equals(
                row.EvidenceStatus,
                row.TaskSourcesJson,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var sources = JsonSerializer.Deserialize<string[]>(
                row.TaskSourcesJson) ?? Array.Empty<string>();
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

    private static MachineSupportContinuation None(string kind) => new(
        "not_applicable",
        kind,
        string.Empty,
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        "no_active_machine_support_intent");

    private static bool TryReadInt(
        JsonElement source,
        string property,
        out int value)
    {
        value = 0;
        return source.TryGetProperty(property, out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value);
    }

    private static string Format(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
