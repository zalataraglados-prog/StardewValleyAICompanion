using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string DeconstructorPredictionModelId =
        "deconstructor_recipe_recovery.v1";
    private const string DeconstructorQualifiedItemId = "(BC)265";

    private static bool IsVettedDeconstructorOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId == DeconstructorQualifiedItemId &&
            machine.GetType() == typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputDeconstructor",
                StringComparison.Ordinal);
    }

    private static bool TryReadDeconstructorPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (!IsVettedDeconstructorOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        var outputItem = StardewValley.Object.OutputDeconstructor(
            machine,
            inputItem,
            probe: true,
            outputData,
            Game1.player,
            out var overrideMinutesUntilReady);
        if (outputItem is null)
        {
            prediction = new
            {
                status = "blocked",
                reason = "deconstructor_input_has_no_single_output_recipe",
                special_prediction_model_id =
                    DeconstructorPredictionModelId,
                training_eligibility_status =
                    "blocked_deconstructor_model_precondition",
                rng_safety_status =
                    "vetted_native_probe_no_rng_or_state_mutation"
            };
            return true;
        }

        var ruleMinutesUntilReady =
            ReadIntNullable(outputRule, "MinutesUntilReady") ?? -1;
        var ruleDaysUntilReady =
            ReadIntNullable(outputRule, "DaysUntilReady") ?? -1;
        var effectiveMinutesUntilReady =
            overrideMinutesUntilReady ??
            (ruleDaysUntilReady >= 0
                ? Utility.CalculateMinutesUntilMorning(
                    Game1.timeOfDay,
                    ruleDaysUntilReady)
                : ruleMinutesUntilReady);

        prediction = new
        {
            status = "available",
            training_eligibility_status =
                ExactMachinePredictionStatus,
            source =
                "decompiled_Object.OutputDeconstructor_vetted_native_probe",
            special_prediction_model_id =
                DeconstructorPredictionModelId,
            vetted_output_method =
                outputData.OutputMethod ?? string.Empty,
            matched_rule_id = outputRule.Id ?? string.Empty,
            required_item_id =
                triggerRule?.RequiredItemId ?? string.Empty,
            required_tags =
                triggerRule?.RequiredTags?.ToArray() ??
                Array.Empty<string>(),
            required_count = triggerRule?.RequiredCount ?? 0,
            item = SummarizeItem(outputItem),
            sale_price = outputItem.salePrice(),
            stack = outputItem.Stack,
            quality = outputItem.Quality,
            effective_minutes_until_ready =
                effectiveMinutesUntilReady,
            rng_safety_status =
                "vetted_native_probe_no_rng_or_state_mutation",
            recovery_rule =
                inputItem.QualifiedItemId == "(O)710"
                    ? "hardwood_fence_special_case_two_iron_bars"
                    : "highest_total_sale_value_recipe_ingredient",
            recipe_source =
                "live_CraftingRecipe.craftingRecipes"
        };
        return true;
    }
}
