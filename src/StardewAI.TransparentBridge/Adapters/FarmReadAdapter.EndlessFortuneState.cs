using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string EndlessFortuneQualifiedItemId =
        "(BC)127";
    private const string EndlessFortuneStateModelId =
        "statue_endless_fortune_daily_output.v1";

    private static readonly string[]
        EndlessFortuneOrdinaryOutputIds =
        {
            "(O)72",
            "(O)337",
            "(O)749",
            "(O)336"
        };

    private static bool IsVettedEndlessFortuneOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId ==
                EndlessFortuneQualifiedItemId &&
            machine.GetType() ==
                typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputStatueOfEndlessFortune",
                StringComparison.Ordinal);
    }

    private static object? ReadEndlessFortuneSpecialState(
        StardewValley.Object machine)
    {
        if (machine.QualifiedItemId !=
                EndlessFortuneQualifiedItemId ||
            machine.GetType() != typeof(StardewValley.Object))
        {
            return null;
        }

        var outputMethod = machine.GetMachineData()
            ?.OutputRules?
            .Where(rule =>
                rule.Triggers?.Any(trigger =>
                    trigger.Trigger.HasFlag(
                        MachineOutputTrigger.DayUpdate)) ==
                true)
            .SelectMany(
                rule =>
                    rule.OutputItem ??
                    new List<MachineItemOutput>())
            .Select(output =>
                output.OutputMethod ?? string.Empty)
            .FirstOrDefault(method =>
                !string.IsNullOrWhiteSpace(method)) ??
            string.Empty;
        if (!IsVettedEndlessFortuneOutputMethod(
                machine,
                outputMethod))
        {
            return BlockedEndlessFortuneState(
                machine,
                outputMethod,
                "endless_fortune_output_method_not_vetted");
        }

        var birthdayNpc =
            Utility.getTodaysBirthdayNPC();
        var birthdayFavoriteItem =
            birthdayNpc?.getFavoriteItem();
        var ordinaryDistribution =
            ReadEndlessFortuneOrdinaryDistribution();
        var heldItem = machine.heldObject.Value;
        var heldMatchesCurrentDateBranch =
            birthdayFavoriteItem is not null
                ? heldItem?.QualifiedItemId ==
                    birthdayFavoriteItem.QualifiedItemId
                : heldItem is null ||
                    EndlessFortuneOrdinaryOutputIds.Contains(
                        heldItem.QualifiedItemId,
                        StringComparer.Ordinal);

        return new
        {
            schema_version =
                "endless_fortune_special_state.v1",
            status = "available",
            special_state_model_id =
                EndlessFortuneStateModelId,
            source =
                "decompiled_Object.DayUpdate_OutputStatueOfEndlessFortune_NPC.getFavoriteItem",
            vetted_output_method = outputMethod,
            lifecycle_state = heldItem is null
                ? "waiting_for_day_update"
                : "ready_for_collection",
            held_item = SummarizeItem(heldItem),
            ready_for_harvest =
                machine.readyForHarvest.Value,
            minutes_until_ready =
                machine.MinutesUntilReady,
            clears_previous_contents_overnight = true,
            current_date_branch =
                birthdayFavoriteItem is not null
                    ? "birthday_first_loved_item"
                    : "ordinary_shared_rng_uniform",
            current_birthday_npc_name =
                birthdayNpc?.Name ?? string.Empty,
            current_birthday_output =
                birthdayFavoriteItem is null
                    ? null
                    : ReadEndlessFortuneItem(
                        birthdayFavoriteItem),
            current_birthday_output_status =
                birthdayFavoriteItem is not null
                    ? "exact_native_first_resolvable_loved_item"
                    : birthdayNpc is null
                        ? "not_applicable_no_current_birthday_villager"
                        : "unavailable_birthday_npc_has_no_resolvable_loved_item",
            ordinary_output_distribution =
                ordinaryDistribution,
            ordinary_output_distribution_status =
                "complete_uniform_four_way_distribution",
            ordinary_actual_output_identity_status =
                birthdayFavoriteItem is null
                    ? "unavailable_shared_Game1_random_state_not_read"
                    : "not_applicable_birthday_branch_precedes_rng",
            held_item_matches_current_date_branch =
                heldMatchesCurrentDateBranch,
            exact_identity_training_eligibility_status =
                birthdayFavoriteItem is not null
                    ? "exact_current_date_birthday_branch"
                    : "blocked_shared_rng_actual_identity",
            planning_distribution_status =
                "complete_for_vanilla_current_date_branch"
        };
    }

    private static object BlockedEndlessFortuneState(
        StardewValley.Object machine,
        string outputMethod,
        string reason)
    {
        return new
        {
            schema_version =
                "endless_fortune_special_state.v1",
            status = "blocked",
            reason,
            special_state_model_id =
                EndlessFortuneStateModelId,
            qualified_item_id =
                machine.QualifiedItemId,
            runtime_type =
                machine.GetType().FullName ??
                machine.GetType().Name,
            observed_output_method = outputMethod
        };
    }

    private static object[] ReadEndlessFortuneOrdinaryDistribution()
    {
        return EndlessFortuneOrdinaryOutputIds
            .Select(qualifiedItemId =>
            {
                var item = ItemRegistry.Create(
                    qualifiedItemId);
                return new
                {
                    qualified_item_id =
                        qualifiedItemId,
                    item = SummarizeItem(item),
                    sale_price = item.salePrice(),
                    stack = item.Stack,
                    conditional_probability = 0.25
                };
            })
            .ToArray<object>();
    }

    private static object ReadEndlessFortuneItem(
        Item item)
    {
        return new
        {
            item = SummarizeItem(item),
            sale_price = item.salePrice(),
            stack = item.Stack,
            quality = item.Quality
        };
    }
}
