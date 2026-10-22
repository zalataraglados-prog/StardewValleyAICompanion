using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileCraftRainTotemStep(
        SmallModelAction action) => CompileExactSupportCraftingStep(
            action,
            "craft_rain_totem",
            "(O)681",
            "master_angler_weather_supply_replan_required=true");

    private static string[] ValidateCraftRainTotemPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (action.OptionId != "executor.craft_rain_totem")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var binding = ValidateExactSupportCraftingBinding(
            action,
            snapshot,
            commitmentLedger,
            "rain_totem_crafting",
            "(O)681",
            "craft_rain_totem",
            reasons);
        if (binding is null)
            return reasons.Distinct(StringComparer.Ordinal).ToArray();

        MasterAnglerRainTotemSupplyIntentValidation? supplyIntent = null;
        if (!MasterAnglerWindowIntentValidator
                .TryValidateRainTotemSupplyIntent(
                    snapshot,
                    action.Parameters,
                    out var validatedSupplyIntent,
                    out var supplyIntentRejection))
        {
            reasons.Add(string.IsNullOrWhiteSpace(supplyIntentRejection)
                ? "craft_rain_totem_weather_supply_intent_invalid"
                : supplyIntentRejection);
        }
        else
        {
            supplyIntent = validatedSupplyIntent;
        }

        if (!string.Equals(
                ReadString(binding.RecipeRow, "recipe_name"),
                "Rain Totem",
                StringComparison.Ordinal) ||
            ReadInt(binding.RecipeRow, "output_count_per_craft") != 1)
        {
            reasons.Add("craft_rain_totem_recipe_identity_drifted");
        }

        if (!string.Equals(
                ReadParameter(action, "rain_totem_crafting_reason"),
                "master_angler_future_rain_window",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "weather_supply_required_weather"),
                "Rain",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "weather_supply_scheduler_status"),
                "authoritative_future_window_bound",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(
                ReadParameter(action, "weather_supply_affected_context_id")) ||
            supplyIntent is not null && !string.Equals(
                ReadParameter(action, "weather_supply_affected_context_id"),
                supplyIntent.AffectedLocationContextId,
                StringComparison.Ordinal))
        {
            reasons.Add("craft_rain_totem_weather_supply_intent_invalid");
        }

        var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
        if (ReadIntParameter(action, "weather_supply_target_total_day") !=
                currentDay + 1 ||
            supplyIntent is not null &&
            ReadIntParameter(action, "weather_supply_target_total_day") !=
                supplyIntent.TargetTotalDay)
        {
            reasons.Add("craft_rain_totem_target_day_not_tomorrow");
        }

        var targets = ReadQualifiedItemIds(
            ReadParameter(
                action,
                "weather_supply_target_qualified_item_ids_json") ??
            string.Empty);
        if (targets.Length == 0 ||
            !MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var missing) ||
            targets.Any(target => !missing.Contains(target)) ||
            supplyIntent is not null &&
            !targets.SequenceEqual(
                supplyIntent.TargetQualifiedItemIds,
                StringComparer.Ordinal))
        {
            reasons.Add("craft_rain_totem_target_not_in_exact_missing_set");
        }

        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!inventory.HasValue ||
            inventory.Value.ValueKind != JsonValueKind.Array)
        {
            reasons.Add("craft_rain_totem_inventory_unavailable");
        }
        else if (inventory.Value.EnumerateArray().Any(row =>
                     string.Equals(
                         ReadString(row, "qualified_item_id"),
                         "(O)681",
                         StringComparison.Ordinal) &&
                     ReadInt(row, "stack") > 0))
        {
            reasons.Add("craft_rain_totem_owned_supply_already_available");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ReadQualifiedItemIds(string json)
    {
        try
        {
            return (JsonSerializer.Deserialize<string[]>(json) ??
                    Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
