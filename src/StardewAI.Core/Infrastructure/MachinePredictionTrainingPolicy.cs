using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

internal static class MachinePredictionTrainingPolicy
{
    internal const string ExactStatus =
        "exact_current_snapshot_probe_supported";
    internal const string CompleteDistributionStatus =
        "distribution_complete_shared_rng_realized_stats_blocked";
    internal const string AnvilModelId =
        "anvil_trinket_reforge_distribution.v1";

    private static readonly HashSet<string> AnvilOutcomeKinds =
        new(
            new[]
            {
                "iridium_spur",
                "parrot_egg",
                "frog_egg",
                "fairy_box",
                "ice_rod",
                "magic_quiver"
            },
            StringComparer.Ordinal);

    internal static MachinePredictionTrainingContract ReadContract(
        JsonElement predictedOutput,
        string inputQualifiedItemId)
    {
        if (predictedOutput.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(predictedOutput, "status"),
                "available",
                StringComparison.Ordinal))
        {
            return MachinePredictionTrainingContract.Blocked;
        }

        var trainingStatus = ReadString(
            predictedOutput,
            "training_eligibility_status");
        if (string.Equals(
                trainingStatus,
                ExactStatus,
                StringComparison.Ordinal))
        {
            return new MachinePredictionTrainingContract(
                true,
                "exact",
                ReadString(
                    predictedOutput,
                    "special_prediction_model_id"),
                string.Empty,
                string.Empty);
        }

        if (!string.Equals(
                trainingStatus,
                CompleteDistributionStatus,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(
                    predictedOutput,
                    "special_prediction_model_id"),
                AnvilModelId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(
                    predictedOutput,
                    "distribution_status"),
                "complete_vanilla_generative_rules",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(
                    predictedOutput,
                    "realized_generation_seed_status"),
                "blocked_shared_Game1_random_Next_9999999",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(
                    predictedOutput,
                    "realized_output_stats_status"),
                "blocked_until_native_load_records_held_trinket",
                StringComparison.Ordinal) ||
            ReadInt(
                predictedOutput,
                "effective_minutes_until_ready") != 10)
        {
            return MachinePredictionTrainingContract.Blocked;
        }

        var outcomeKind = ReadString(
            predictedOutput,
            "outcome_kind");
        if (!AnvilOutcomeKinds.Contains(outcomeKind) ||
            !predictedOutput.TryGetProperty(
                "outcome_rules",
                out var outcomeRules) ||
            outcomeRules.ValueKind != JsonValueKind.Object ||
            !predictedOutput.TryGetProperty(
                "output_identity",
                out var outputIdentity) ||
            outputIdentity.ValueKind != JsonValueKind.Object ||
            !ReadBool(
                outputIdentity,
                "same_trinket_identity") ||
            ReadInt(outputIdentity, "stack") != 1 ||
            string.IsNullOrWhiteSpace(inputQualifiedItemId) ||
            !string.Equals(
                ReadString(
                    outputIdentity,
                    "qualified_item_id"),
                inputQualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            !HasExactAnvilAdditionalConsumption(
                predictedOutput))
        {
            return MachinePredictionTrainingContract.Blocked;
        }

        return new MachinePredictionTrainingContract(
            true,
            "complete_distribution",
            AnvilModelId,
            outcomeKind,
            Fingerprint(predictedOutput));
    }

    private static bool HasExactAnvilAdditionalConsumption(
        JsonElement predictedOutput)
    {
        if (!predictedOutput.TryGetProperty(
                "consumed_additional_items",
                out var items) ||
            items.ValueKind != JsonValueKind.Array ||
            items.GetArrayLength() != 1)
        {
            return false;
        }

        var item = items[0];
        return item.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(item, "qualified_item_id"),
                "(O)337",
                StringComparison.Ordinal) &&
            ReadInt(item, "required_count") == 3;
    }

    private static string Fingerprint(
        JsonElement predictedOutput)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(
            predictedOutput.GetRawText());
        return string.Concat(
            sha.ComputeHash(bytes)
                .Select(value =>
                    value.ToString("x2")));
    }

    private static string ReadString(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
                value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
                value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }

    private static bool ReadBool(
        JsonElement element,
        string property)
    {
        return element.TryGetProperty(
                    property,
                    out var value) &&
            value.ValueKind == JsonValueKind.True;
    }
}

internal readonly record struct
    MachinePredictionTrainingContract(
        bool Supported,
        string Kind,
        string ModelId,
        string OutcomeKind,
        string Fingerprint)
{
    internal static MachinePredictionTrainingContract Blocked =>
        new(
            false,
            "blocked",
            string.Empty,
            string.Empty,
            string.Empty);
}
