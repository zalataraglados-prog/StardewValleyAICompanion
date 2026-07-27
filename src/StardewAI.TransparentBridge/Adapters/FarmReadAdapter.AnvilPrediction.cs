using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Objects.Trinkets;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string AnvilQualifiedItemId =
        "(BC)Anvil";
    private const string AnvilPredictionModelId =
        "anvil_trinket_reforge_distribution.v1";
    private const string AnvilDistributionTrainingStatus =
        "distribution_complete_shared_rng_realized_stats_blocked";

    private static bool IsVettedAnvilOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId ==
                AnvilQualifiedItemId &&
            machine.GetType() ==
                typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputAnvil",
                StringComparison.Ordinal);
    }

    private static bool IsVettedAnvilInputSupported(
        Item inputItem)
    {
        if (inputItem is not Trinket trinket ||
            trinket.GetTrinketData()?.CanBeReforged !=
                true)
        {
            return false;
        }

        var outcomeKind =
            ReadAnvilOutcomeKind(trinket);
        if (outcomeKind.Length == 0)
        {
            return false;
        }

        if (outcomeKind != "parrot_egg")
        {
            return true;
        }

        var levelCount = ReadParrotEggLevelCount();
        var currentStat =
            ReadParrotEggCurrentGeneralStat(
            trinket);
        return currentStat >= 0 &&
            (levelCount > 1 || currentStat != 0);
    }

    private static bool TryReadAnvilPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (!IsVettedAnvilOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        if (inputItem is not Trinket trinket ||
            trinket.GetTrinketData()?.CanBeReforged !=
                true)
        {
            prediction = BlockedAnvilPrediction(
                "anvil_input_not_reforgeable_trinket");
            return true;
        }

        var outcomeKind =
            ReadAnvilOutcomeKind(trinket);
        if (outcomeKind.Length == 0)
        {
            prediction = BlockedAnvilPrediction(
                "anvil_trinket_effect_not_vetted");
            return true;
        }

        var outcomeRules =
            ReadAnvilOutcomeRules(
                trinket,
                outcomeKind);
        if (outcomeRules is null)
        {
            prediction = BlockedAnvilPrediction(
                "anvil_trinket_current_state_rejects_reforge");
            return true;
        }

        var data = trinket.GetTrinketData();
        prediction = new
        {
            status = "available",
            training_eligibility_status =
                AnvilDistributionTrainingStatus,
            source =
                "decompiled_Object.OutputAnvil_and_vanilla_TrinketEffect_GenerateRandomStats",
            special_prediction_model_id =
                AnvilPredictionModelId,
            vetted_output_method =
                outputData.OutputMethod ?? string.Empty,
            matched_rule_id =
                outputRule.Id ?? string.Empty,
            required_item_id =
                triggerRule?.RequiredItemId ??
                string.Empty,
            required_tags =
                triggerRule?.RequiredTags?.ToArray() ??
                Array.Empty<string>(),
            required_count =
                triggerRule?.RequiredCount ?? 0,
            input = new
            {
                item = SummarizeItem(trinket),
                generation_seed =
                    trinket.generationSeed.Value,
                effect_class =
                    data?.TrinketEffectClass ??
                    string.Empty,
                description_substitution_templates =
                    trinket
                        .descriptionSubstitutionTemplates
                        .ToArray(),
                display_name_override_template =
                    trinket
                        .displayNameOverrideTemplate.Value,
                current_first_integer_description_value =
                    ReadAnvilFirstIntegerDescriptionValue(
                        trinket),
                parrot_egg_current_general_stat =
                    outcomeKind == "parrot_egg"
                        ? (int?)ReadParrotEggCurrentGeneralStat(
                            trinket)
                        : null,
                current_outcome =
                    ReadAnvilCurrentOutcomeState(
                        trinket,
                        outcomeKind)
            },
            output_identity = new
            {
                qualified_item_id =
                    trinket.QualifiedItemId,
                same_trinket_identity = true,
                stack = 1,
                quality = trinket.Quality
            },
            consumed_additional_items = new[]
            {
                new
                {
                    qualified_item_id = "(O)337",
                    required_count = 3
                }
            },
            effective_minutes_until_ready = 10,
            outcome_kind = outcomeKind,
            outcome_rules = outcomeRules,
            distribution_status =
                "complete_vanilla_generative_rules",
            realized_generation_seed_status =
                "blocked_shared_Game1_random_Next_9999999",
            realized_output_stats_status =
                "blocked_until_native_load_records_held_trinket",
            rng_safety_status =
                "no_callback_no_RerollStats_no_Game1_random_read"
        };
        return true;
    }

    private static object BlockedAnvilPrediction(
        string reason)
    {
        return new
        {
            status = "blocked",
            reason,
            special_prediction_model_id =
                AnvilPredictionModelId,
            training_eligibility_status =
                "blocked_anvil_model_precondition"
        };
    }
}
