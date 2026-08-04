using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineSupportContinuation(
    string Status,
    string Kind,
    string IntentId,
    int IntentRevision,
    string IntentStage,
    string SourceStateHash,
    string GoalId,
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
                intent.NetBenefit,
                0,
                intent.SupportScore,
                "continue_committed_positive_machine_capacity");
    }

    public static MachineSupportContinuation Load(
        MachineSupportIntent? intent,
        int? currentInputNetBenefit)
    {
        if (intent is null)
        {
            return None("load_machine_input");
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

    private static MachineSupportContinuation None(string kind) => new(
        "not_applicable",
        kind,
        string.Empty,
        0,
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
