using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string GeodeCrusherQualifiedItemId =
        "(BC)182";
    private const string GeodeCrusherPredictionModelId =
        "geode_crusher_day_save_counter_rng.v1";

    private static readonly HashSet<string>
        VettedGeodeCrusherInputIds =
        new(
            new[]
            {
                "(O)535",
                "(O)536",
                "(O)537",
                "(O)749"
            },
            StringComparer.Ordinal);

    private static bool IsVettedGeodeCrusherOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId ==
                GeodeCrusherQualifiedItemId &&
            machine.GetType() ==
                typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputGeodeCrusher",
                StringComparison.Ordinal);
    }

    private static bool IsVettedGeodeCrusherInputSupported(
        Item inputItem)
    {
        return inputItem.GetType() ==
                typeof(StardewValley.Object) &&
            VettedGeodeCrusherInputIds.Contains(
                inputItem.QualifiedItemId) &&
            Utility.IsGeode(
                inputItem,
                disallow_special_geodes: true);
    }

    private static bool TryReadGeodeCrusherPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (!IsVettedGeodeCrusherOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        if (!IsVettedGeodeCrusherInputSupported(
                inputItem))
        {
            prediction = BlockedGeodeCrusherPrediction(
                "geode_crusher_input_not_vetted_vanilla_ordinary_geode");
            return true;
        }

        var outputItem =
            Utility.getTreasureFromGeode(inputItem);
        if (outputItem is null)
        {
            prediction = BlockedGeodeCrusherPrediction(
                "geode_crusher_deterministic_replay_returned_null");
            return true;
        }

        var effectiveMinutesUntilReady =
            ReadIntNullable(
                outputRule,
                "MinutesUntilReady") ?? -1;
        if (effectiveMinutesUntilReady < 0)
        {
            prediction = BlockedGeodeCrusherPrediction(
                "geode_crusher_duration_unavailable");
            return true;
        }

        prediction = new
        {
            status = "available",
            training_eligibility_status =
                ExactMachinePredictionStatus,
            source =
                "decompiled_Utility.getTreasureFromGeode_vetted_vanilla_ordinary_geode",
            special_prediction_model_id =
                GeodeCrusherPredictionModelId,
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
            input_qualified_item_id =
                inputItem.QualifiedItemId,
            item = SummarizeItem(outputItem),
            sale_price = outputItem.salePrice(),
            stack = outputItem.Stack,
            quality = outputItem.Quality,
            effective_minutes_until_ready =
                effectiveMinutesUntilReady,
            rng_seed_inputs = new
            {
                geodes_cracked_before_load =
                    Game1.stats.GeodesCracked,
                save_id_half =
                    Game1.uniqueIDForThisGame / 2,
                player_id_half =
                    (int)Game1.player
                        .UniqueMultiplayerID / 2
            },
            stats_after_successful_load = new
            {
                geodes_cracked =
                    Game1.stats.GeodesCracked + 1
            },
            rng_safety_status =
                "exact_fresh_Utility_CreateRandom_no_shared_Game1_random",
            replay_side_effect_status =
                "vetted_four_vanilla_ordinary_geodes_exclude_MysteryBox_mail_mutation_branch",
            prediction_kind =
                "exact_current_counter_seeded_output"
        };
        return true;
    }

    private static object BlockedGeodeCrusherPrediction(
        string reason)
    {
        return new
        {
            status = "blocked",
            reason,
            special_prediction_model_id =
                GeodeCrusherPredictionModelId,
            training_eligibility_status =
                "blocked_geode_crusher_model_precondition"
        };
    }
}
