using StardewValley;
using StardewValley.Objects.Trinkets;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    internal static object? ReadItemSpecialState(
        Item item)
    {
        return item is Trinket trinket
            ? ReadTrinketState(trinket)
            : null;
    }

    private static object ReadTrinketState(
        Trinket trinket)
    {
        var data = trinket.GetTrinketData();
        var outcomeKind =
            ReadAnvilOutcomeKind(trinket);
        return new
        {
            schema_version =
                "trinket_item_state.v1",
            generation_seed =
                trinket.generationSeed.Value,
            can_be_reforged =
                data?.CanBeReforged == true,
            effect_class =
                data?.TrinketEffectClass ??
                string.Empty,
            display_name_override_template =
                trinket.displayNameOverrideTemplate.Value,
            description_substitution_templates =
                trinket
                    .descriptionSubstitutionTemplates
                    .ToArray(),
            metadata = trinket.trinketMetadata.Pairs
                .OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal),
            vanilla_outcome_kind = outcomeKind,
            current_outcome =
                outcomeKind.Length == 0
                    ? null
                    : ReadAnvilCurrentOutcomeState(
                        trinket,
                        outcomeKind),
            current_outcome_status =
                outcomeKind.Length == 0
                    ? "blocked_unvetted_trinket_effect"
                    : "exact_from_generation_seed_or_live_displayed_level"
        };
    }
}
