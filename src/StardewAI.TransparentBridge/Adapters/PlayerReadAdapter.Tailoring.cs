using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.GameData.Crafting;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string TailoringNativeContract =
        "live_Tailoring_action_or_BC247_checkAction_then_native_TailoringMenu_inventory_slot_clicks_start_1500ms_update_collect_leftovers_and_verify_without_direct_inventory_tailoredItems_boot_or_clothing_mutation";

    private static object ReadTailoringContext(Farmer? player)
    {
        if (player is null)
            return new { projection_status = "unavailable_world", source_count = 0, row_count = 0, rows = Array.Empty<object>() };
        if (SnapshotProfileContext.Current is not "full")
            return new { projection_status = "blocked_requires_full_profile", source_count = 0, row_count = 0, rows = Array.Empty<object>() };

        var endpoints = ReadTailoringEndpoints(player);
        var inputs = player.Items.Select((item, slot) => item is null
                ? null
                : new TailoringInput("inventory:" + slot, slot, item))
            .Where(value => value is not null)
            .Cast<TailoringInput>()
            .OrderBy(value => value.SourceId, StringComparer.Ordinal)
            .ToArray();
        var recipes = DataLoader.TailoringRecipes(Game1.temporaryContent);
        var rows = new List<object>();
        foreach (var left in inputs.Where(value => TailoringIngredientAllowed(value.Item)))
        foreach (var right in inputs.Where(value => value.SourceId != left.SourceId && TailoringIngredientAllowed(value.Item)))
        {
            if (left.Item is Clothing clothing && clothing.dyeable.Value &&
                (right.Item.HasContextTag("color_prismatic") || TailoringMenu.GetDyeColor(right.Item).HasValue))
                continue;

            var recipe = left.Item is Boots && right.Item is Boots
                ? null
                : recipes.FirstOrDefault(value => TailoringTagsMatch(left.Item, value.FirstItemTags) &&
                    TailoringTagsMatch(right.Item, value.SecondItemTags));
            if (recipe is null && (left.Item is not Boots || right.Item is not Boots))
                continue;
            var output = ProjectTailoringOutput(player, left.Item, right.Item, recipe);
            if (output.Outcomes.Length == 0)
                continue;
            foreach (var endpoint in endpoints)
                rows.Add(TailoringRow(player, endpoint, left, right, recipe, output));
        }

        var orderedRows = rows.OrderBy(value => JsonSerializer.Serialize(value), StringComparer.Ordinal).ToArray();
        return new
        {
            projection_status = "complete_live_native_tailoring_recipe_input_endpoint_and_output_domain_projection",
            projection_fingerprint = Sha256(JsonSerializer.Serialize(orderedRows)),
            recipe_count = recipes.Count,
            source_count = endpoints.Length,
            input_source_count = inputs.Length,
            row_count = orderedRows.Length,
            excluded_branch = "dyeable_clothing_plus_color_or_prismatic_input_belongs_to_tailoring.dye_item",
            native_contract = TailoringNativeContract,
            rows = orderedRows
        };
    }

    private static object TailoringRow(
        Farmer player,
        TailoringEndpoint endpoint,
        TailoringInput left,
        TailoringInput right,
        TailorItemRecipe? recipe,
        TailoringOutputProjection output)
    {
        var fits = output.Outcomes.All(item => TailoringPostCraftInventoryFits(player, left, right, recipe, item));
        var ready = endpoint.Ready && fits;
        var tailoredCounts = output.Outcomes
            .Select(TailoringHistoryKey)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(key => key, key => player.tailoredItems.TryGetValue(key, out var count) ? count : 0, StringComparer.Ordinal);
        var purpose = output.Operation == "boots_stat_transfer"
            ? "combat_stat_transfer"
            : IsTailoringQuestObject(left.Item) || IsTailoringQuestObject(right.Item)
                ? "quest_or_special_item_progress"
                : tailoredCounts.Values.Any(value => value == 0)
                    ? "first_tailor_discovery"
                    : "wardrobe_collection";
        return new
        {
            tailoring_candidate_id = endpoint.SourceId + ":" + output.Operation + ":" + left.SourceId + ":" + right.SourceId,
            tailoring_operation = output.Operation,
            tailoring_purpose = purpose,
            recipe_id = recipe?.Id ?? string.Empty,
            source_id = endpoint.SourceId,
            source_kind = endpoint.Kind,
            source_ready = endpoint.Ready,
            source_block_reason = endpoint.BlockReason,
            location_id = endpoint.Location.NameOrUniqueName,
            interaction_tile_x = endpoint.Tile.X,
            interaction_tile_y = endpoint.Tile.Y,
            left_source_id = left.SourceId,
            left_slot_index = left.Slot,
            left_qualified_item_id = left.Item.QualifiedItemId,
            left_display_name = left.Item.DisplayName,
            left_state_json = TailoringItemStateJson(left.Item),
            right_source_id = right.SourceId,
            right_slot_index = right.Slot,
            right_qualified_item_id = right.Item.QualifiedItemId,
            right_display_name = right.Item.DisplayName,
            right_state_json = TailoringItemStateJson(right.Item),
            spend_left_count = output.Operation == "recipe" ? 1 : 0,
            spend_right_count = 1,
            output_contract_kind = output.ContractKind,
            expected_output_state_json = output.ContractKind == "exact_item_state"
                ? TailoringItemStateJson(output.Outcomes[0])
                : string.Empty,
            random_outcome_contract_json = output.ContractKind == "native_random_result_domain"
                ? JsonSerializer.Serialize(new
                {
                    random_source = "Game1.random.ChooseFrom(TailorItemRecipe.CraftedItemIds)",
                    allowed_output_states = output.Outcomes.Select(TailoringItemStateObject).ToArray()
                })
                : string.Empty,
            tailored_counts_before_json = JsonSerializer.Serialize(tailoredCounts),
            marks_tailored_item = recipe is not null,
            output_inventory_acceptance_after_native_input_removal = fits,
            tailoring_candidate_status = ready
                ? "ready_for_native_tailoring_menu"
                : !endpoint.Ready
                    ? endpoint.BlockReason
                    : "blocked_output_or_leftover_inventory_capacity",
            native_contract = TailoringNativeContract
        };
    }

    private static TailoringOutputProjection ProjectTailoringOutput(
        Farmer player,
        Item left,
        Item right,
        TailorItemRecipe? recipe)
    {
        if (left is Boots leftBoots && right is Boots rightBoots)
        {
            var result = (Boots)leftBoots.getOne();
            result.applyStats(rightBoots);
            return new TailoringOutputProjection("boots_stat_transfer", "exact_item_state", new Item[] { result });
        }

        var ids = recipe?.CraftedItemIds is { Count: > 0 } randomIds
            ? randomIds
            : recipe is null
                ? new List<string>()
                : new List<string>
                {
                    !player.IsMale && !string.IsNullOrWhiteSpace(recipe.CraftedItemIdFeminine)
                        ? recipe.CraftedItemIdFeminine
                        : recipe.CraftedItemId
                };
        var outcomes = ids.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TailoringMenu.ConvertLegacyItemId)
            .Distinct(StringComparer.Ordinal)
            .Select(id => ItemRegistry.Create(id))
            .Select(item => ApplyTailoringRecipeOutputEffects(item, left, right))
            .ToArray();
        return new TailoringOutputProjection(
            "recipe",
            outcomes.Length > 1 ? "native_random_result_domain" : "exact_item_state",
            outcomes);
    }

    private static Item ApplyTailoringRecipeOutputEffects(Item output, Item left, Item right)
    {
        if (output is Clothing clothing)
        {
            if (right.QualifiedItemId == "(O)74")
            {
                clothing.Dye(Color.White, 1f);
                clothing.isPrismatic.Value = true;
            }
            else if (TailoringMenu.GetDyeColor(right) is { } color)
            {
                clothing.Dye(color, 1f);
            }
        }
        if (output is StardewValley.Object result &&
            (IsTailoringQuestObject(left) || IsTailoringQuestObject(right)))
            result.questItem.Value = true;
        return output;
    }

    private static bool TailoringPostCraftInventoryFits(
        Farmer player,
        TailoringInput left,
        TailoringInput right,
        TailorItemRecipe? recipe,
        Item output)
    {
        var inventory = player.Items.Select(item => item is null ? null : TailoringClone(item, item.Stack)).ToList();
        inventory[left.Slot] = null;
        inventory[right.Slot] = null;
        var returns = new List<Item> { TailoringClone(output, output.Stack) };
        if (recipe is not null && left.Item.Stack > 1)
            returns.Add(TailoringClone(left.Item, left.Item.Stack - 1));
        if (right.Item.Stack > 1)
            returns.Add(TailoringClone(right.Item, right.Item.Stack - 1));
        return returns.All(item => Utility.addItemToThisInventoryList(item, inventory, player.MaxItems) is null);
    }

    private static Item TailoringClone(Item item, int stack)
    {
        var clone = item.getOne();
        clone.Stack = stack;
        return clone;
    }

    private static bool TailoringIngredientAllowed(Item item) =>
        item.HasContextTag("item_lucky_purple_shorts") || item.canBeTrashed();

    private static bool IsTailoringQuestObject(Item item) =>
        item is StardewValley.Object obj && obj.questItem.Value;

    private static bool TailoringTagsMatch(Item item, List<string>? tags) =>
        tags is { Count: > 0 } && tags.All(item.HasContextTag);

    private static string TailoringHistoryKey(Item item)
    {
#pragma warning disable CS0618 // Native Farmer.MarkItemAsTailored uses this legacy key.
        return Utility.getStandardDescriptionFromItem(item, 1);
#pragma warning restore CS0618
    }

    private static TailoringEndpoint[] ReadTailoringEndpoints(Farmer player)
    {
        var locations = new List<GameLocation>();
        Utility.ForEachLocation(location =>
        {
            if (location is not null)
                locations.Add(location);
            return true;
        }, includeInteriors: true, includeGenerated: true);
        if (Game1.currentLocation is not null && locations.All(value => !ReferenceEquals(value, Game1.currentLocation)))
            locations.Add(Game1.currentLocation);

        var rows = new List<TailoringEndpoint>();
        foreach (var location in locations.Distinct())
        {
            var layer = location.map?.GetLayer("Buildings");
            if (layer is not null)
            {
                for (var y = 0; y < layer.LayerHeight; y++)
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    if (!string.Equals(location.doesTileHaveProperty(x, y, "Action", "Buildings"), "Tailoring", StringComparison.Ordinal))
                        continue;
                    var unlocked = player.eventsSeen.Contains("992559");
                    rows.Add(new TailoringEndpoint(
                        "tailoring-action:" + location.NameOrUniqueName + ":" + x + "," + y,
                        "tailoring_action",
                        location,
                        new Point(x, y),
                        unlocked,
                        unlocked ? string.Empty : "blocked_tailoring_action_requires_event_992559"));
                }
            }
            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value.QualifiedItemId != "(BC)247")
                    continue;
                rows.Add(new TailoringEndpoint(
                    "sewing-machine:" + location.NameOrUniqueName + ":" + (int)pair.Key.X + "," + (int)pair.Key.Y,
                    "placed_sewing_machine",
                    location,
                    new Point((int)pair.Key.X, (int)pair.Key.Y),
                    true,
                    string.Empty));
            }
        }
        return rows.GroupBy(value => value.SourceId, StringComparer.Ordinal).Select(value => value.First())
            .OrderBy(value => value.SourceId, StringComparer.Ordinal).ToArray();
    }

    internal static string TailoringItemStateJson(Item item) => JsonSerializer.Serialize(TailoringItemStateObject(item));

    private static object TailoringItemStateObject(Item item) => new
    {
        qualified_item_id = item.QualifiedItemId,
        runtime_type = item.GetType().FullName,
        stack = item.Stack,
        quality = item.Quality,
        quest_item = item is StardewValley.Object obj && obj.questItem.Value,
        boots_defense = item is Boots boots ? boots.defenseBonus.Value : 0,
        boots_immunity = item is Boots immunity ? immunity.immunityBonus.Value : 0,
        boots_sprite_index = item is Boots sprite ? sprite.indexInTileSheet.Value : -1,
        clothing_type = item is Clothing clothing ? clothing.clothesType.Value.ToString() : string.Empty,
        clothing_dyeable = item is Clothing dyeable && dyeable.dyeable.Value,
        clothing_prismatic = item is Clothing prismatic && prismatic.isPrismatic.Value,
        clothing_color = item is Clothing colored
            ? new { colored.clothesColor.Value.R, colored.clothesColor.Value.G, colored.clothesColor.Value.B, colored.clothesColor.Value.A }
            : null
    };

    private sealed record TailoringEndpoint(
        string SourceId,
        string Kind,
        GameLocation Location,
        Point Tile,
        bool Ready,
        string BlockReason);

    private sealed record TailoringInput(string SourceId, int Slot, Item Item);

    private sealed record TailoringOutputProjection(string Operation, string ContractKind, Item[] Outcomes);
}
