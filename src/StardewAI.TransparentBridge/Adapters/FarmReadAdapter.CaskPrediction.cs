using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using System.Globalization;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string CaskPredictionModelId = "cask_quality_aging.v1";

    private static bool TryReadCaskPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (machine is not Cask cask ||
            !IsVettedCaskOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        if (inputItem is not StardewValley.Object inputObject)
        {
            prediction = BlockedCaskPrediction(
                "cask_input_must_be_object");
            return true;
        }
        if (!cask.IsValidCaskLocation())
        {
            prediction = BlockedCaskPrediction(
                "cask_location_not_operational");
            return true;
        }
        if (cask.Quality >= 4 || inputObject.Quality >= 4)
        {
            prediction = BlockedCaskPrediction(
                "cask_or_input_already_iridium_quality");
            return true;
        }

        var agingMultiplierText = "1";
        if (outputData.CustomData is not null &&
            outputData.CustomData.TryGetValue(
                "AgingMultiplier",
                out var configuredMultiplier))
        {
            agingMultiplierText = configuredMultiplier;
        }
        if (!float.TryParse(
                agingMultiplierText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var agingRate) ||
            agingRate <= 0f)
        {
            prediction = BlockedCaskPrediction(
                "cask_aging_multiplier_invalid");
            return true;
        }

        var initialOutput = inputObject.getOne() as StardewValley.Object;
        var projectedOutput = inputObject.getOne() as StardewValley.Object;
        if (initialOutput is null || projectedOutput is null)
        {
            prediction = BlockedCaskPrediction(
                "cask_output_clone_unavailable");
            return true;
        }

        initialOutput.Stack = 1;
        projectedOutput.Stack = 1;
        projectedOutput.Quality = 4;
        var initialQuality = inputObject.Quality;
        var nextQuality = cask.GetNextQuality(initialQuality);
        var initialDaysToMature = cask.GetDaysForQuality(initialQuality);
        var nextQualityThreshold = cask.GetDaysForQuality(nextQuality);
        var effectiveDaysToNextQuality = Math.Max(
            1,
            (int)Math.Ceiling(
                (initialDaysToMature - nextQualityThreshold) /
                agingRate));
        var effectiveDaysToIridium = Math.Max(
            1,
            (int)Math.Ceiling(initialDaysToMature / agingRate));

        prediction = new
        {
            status = "available",
            training_eligibility_status =
                ExactMachinePredictionStatus,
            source =
                "decompiled_Cask.OutputCask_static_model",
            special_prediction_model_id = CaskPredictionModelId,
            vetted_output_method =
                outputData.OutputMethod ?? string.Empty,
            matched_rule_id = outputRule.Id ?? string.Empty,
            required_item_id =
                triggerRule?.RequiredItemId ?? string.Empty,
            required_tags =
                triggerRule?.RequiredTags?.ToArray() ??
                Array.Empty<string>(),
            required_count = triggerRule?.RequiredCount ?? 0,
            item = SummarizeItem(projectedOutput),
            initial_item = SummarizeItem(initialOutput),
            sale_price = projectedOutput.salePrice(),
            stack = 1,
            quality = projectedOutput.Quality,
            initial_quality = initialQuality,
            next_quality = nextQuality,
            projected_final_quality = 4,
            aging_rate_per_day = agingRate,
            initial_days_to_mature = initialDaysToMature,
            effective_days_to_next_quality =
                effectiveDaysToNextQuality,
            effective_days_until_ready =
                effectiveDaysToIridium,
            preserve_type =
                projectedOutput.preserve.Value.HasValue
                    ? projectedOutput.preserve.Value.Value.ToString()
                    : string.Empty,
            preserved_item_id =
                projectedOutput.GetPreservedItemId() ??
                string.Empty,
            completion_clock = "Cask.DayUpdate_quality_thresholds",
            minutes_until_ready_semantics =
                "999999_is_processing_sentinel_not_duration",
            rng_safety_status =
                "vetted_static_callback_model_no_rng_sampling"
        };
        return true;
    }

    private static bool IsVettedCaskOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine is Cask &&
            outputMethod.StartsWith(
                "StardewValley.Objects.Cask,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputCask",
                StringComparison.Ordinal);
    }

    private static object BlockedCaskPrediction(string reason)
    {
        return new
        {
            status = "blocked",
            reason,
            special_prediction_model_id = CaskPredictionModelId,
            training_eligibility_status =
                "blocked_cask_model_precondition"
        };
    }

    private static object? ReadCaskSpecialState(
        StardewValley.Object machine)
    {
        if (machine is not Cask cask)
        {
            return null;
        }

        var heldItem = cask.heldObject.Value;
        if (heldItem is null)
        {
            return new
            {
                schema_version = "machine_special_state.v1",
                status = "idle",
                special_prediction_model_id =
                    CaskPredictionModelId,
                operational_location =
                    cask.IsValidCaskLocation(),
                aging_rate_per_day = cask.agingRate.Value,
                days_to_mature_remaining_raw =
                    cask.daysToMature.Value,
                minutes_until_ready_semantics =
                    "idle_no_cask_input"
            };
        }

        var agingRate = cask.agingRate.Value;
        var daysRemaining = cask.daysToMature.Value;
        var currentQuality = heldItem.Quality;
        var nextQuality = cask.GetNextQuality(currentQuality);
        var nextThreshold = cask.GetDaysForQuality(nextQuality);
        var daysToNextQuality = agingRate > 0f &&
            currentQuality < 4
                ? Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (daysRemaining - nextThreshold) /
                        agingRate))
                : 0;
        var daysToIridium = agingRate > 0f &&
            currentQuality < 4
                ? Math.Max(
                    1,
                    (int)Math.Ceiling(
                        daysRemaining / agingRate))
                : 0;

        return new
        {
            schema_version = "machine_special_state.v1",
            status = currentQuality >= 4
                ? "ready_iridium"
                : "aging",
            special_prediction_model_id =
                CaskPredictionModelId,
            operational_location =
                cask.IsValidCaskLocation(),
            aging_rate_per_day = agingRate,
            days_to_mature_remaining_raw =
                daysRemaining,
            current_quality = currentQuality,
            next_quality = nextQuality,
            projected_final_quality = 4,
            effective_days_to_next_quality =
                daysToNextQuality,
            effective_days_until_ready =
                daysToIridium,
            minutes_until_ready_semantics =
                currentQuality >= 4
                    ? "1_means_ready_on_next_machine_tick"
                    : "999999_is_processing_sentinel_not_duration"
        };
    }
}
