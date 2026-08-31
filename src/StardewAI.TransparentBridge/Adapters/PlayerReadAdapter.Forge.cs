using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly string[] DiamondForgeTypes =
    {
        nameof(EmeraldEnchantment), nameof(AquamarineEnchantment), nameof(RubyEnchantment),
        nameof(AmethystEnchantment), nameof(TopazEnchantment), nameof(JadeEnchantment)
    };

    private static object ReadForgeContext(Farmer? player)
    {
        if (player is null)
        {
            return new { projection_status = "unavailable_world", source_count = 0, row_count = 0, rows = Array.Empty<object>() };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new { projection_status = "blocked_requires_full_profile", source_count = 0, row_count = 0, rows = Array.Empty<object>() };
        }

        var sources = ReadForgeSources();
        var items = ReadForgeItemSources(player);
        var rows = new List<object>();
        var shards = player.Items.CountId("(O)848");
        foreach (var source in sources)
        {
            foreach (var left in items.Where(value => value.Item is Tool or Ring))
            {
                if (left.Item is MeleeWeapon weapon &&
                    (weapon.GetTotalForgeLevels() > 0 || weapon.appearance.Value is not null))
                {
                    rows.Add(ForgeRow(source, left, null, "unforge_weapon", player, shards));
                }
                else if (left.Item is CombinedRing)
                {
                    rows.Add(ForgeRow(source, left, null, "unforge_combined_ring", player, shards));
                }

                foreach (var right in items.Where(value => value.SourceId != left.SourceId && value.Item.QualifiedItemId != "(O)848"))
                {
                    if (left.Item is Tool tool && tool.CanForge(right.Item))
                    {
                        rows.Add(ForgeRow(source, left, right, ForgeFamily(left.Item, right.Item), player, shards));
                    }
                    else if (left.Item is Ring ring && right.Item is Ring other && ring.CanCombine(other))
                    {
                        rows.Add(ForgeRow(source, left, right, "combine_rings", player, shards));
                    }
                }
            }
        }

        return new
        {
            projection_status = "complete_loaded_native_forge_source_and_live_input_projection",
            source_count = sources.Length,
            input_source_count = items.Length,
            row_count = rows.Count,
            shard_count = shards,
            times_enchanted = Game1.stats.Get("timesEnchanted"),
            random_contract = "diamond_and_dragon_tooth_consume_Game1.random_and_publish_complete_result_domain;prismatic_uses_seeded_timesEnchanted_uniqueGame_player_choice",
            rows = rows.ToArray()
        };
    }

    private static object ForgeRow(
        ForgeSource source,
        ForgeItemSource left,
        ForgeItemSource? right,
        string family,
        Farmer player,
        int shards)
    {
        var unforge = family.StartsWith("unforge_", StringComparison.Ordinal);
        var cost = unforge ? 0 : ForgeCost(left.Item, right!.Item);
        var refund = unforge ? UnforgeShardRefund(left.Item) : 0;
        var outputContract = ForgeOutputContract(left.Item, right?.Item, family);
        var outputCanFit = ForgeOutputCanFit(player, left, right, family, outputContract.Output);
        var ready = unforge
            ? outputCanFit
            : shards >= cost && outputCanFit;
        return new
        {
            forge_candidate_id = source.SourceId + ":" + family + ":" + left.SourceId + ":" + (right?.SourceId ?? "none"),
            forge_operation = family,
            forge_source_id = source.SourceId,
            forge_source_kind = source.Kind,
            location_id = source.Location.NameOrUniqueName,
            interaction_tile_x = source.Tile.X,
            interaction_tile_y = source.Tile.Y,
            left_source_id = left.SourceId,
            left_source_kind = left.Kind,
            left_slot_index = left.SlotIndex,
            left_qualified_item_id = left.Item.QualifiedItemId,
            left_display_name = left.Item.DisplayName,
            left_state_json = ForgeItemStateJson(left.Item),
            right_source_id = right?.SourceId ?? string.Empty,
            right_source_kind = right?.Kind ?? string.Empty,
            right_slot_index = right?.SlotIndex,
            right_qualified_item_id = right?.Item.QualifiedItemId ?? string.Empty,
            right_display_name = right?.Item.DisplayName ?? string.Empty,
            right_state_json = right is null ? string.Empty : ForgeItemStateJson(right.Item),
            shard_cost = cost,
            shard_refund = refund,
            shard_count_before = shards,
            shard_count_after = shards - cost + refund,
            times_enchanted_before = Game1.stats.Get("timesEnchanted"),
            times_enchanted_after = Game1.stats.Get("timesEnchanted") + (family is "prismatic_enchant" or "galaxy_soul" ? 1u : 0u),
            output_contract_kind = outputContract.Kind,
            expected_output_state_json = outputContract.Output is null ? string.Empty : ForgeItemStateJson(outputContract.Output),
            random_outcome_contract_json = outputContract.RandomContractJson,
            output_inventory_acceptance_after_input_removal = outputCanFit,
            forge_candidate_status = ready ? "ready_for_native_forge_menu" : !outputCanFit
                ? "blocked_output_cannot_fit_after_input_removal"
                : "blocked_insufficient_cinder_shards",
            native_contract = "ForgeMenu inventory/equipment clicks -> left/right slots -> start or unforge -> 1600ms native update -> native collect/return"
        };
    }

    private static ForgeSource[] ReadForgeSources()
    {
        var locations = new List<GameLocation>();
        Utility.ForEachLocation(location =>
        {
            if (location is not null)
            {
                locations.Add(location);
            }
            return true;
        }, includeInteriors: true, includeGenerated: true);
        if (Game1.currentLocation is not null && locations.All(value => !ReferenceEquals(value, Game1.currentLocation)))
        {
            locations.Add(Game1.currentLocation);
        }

        var rows = new List<ForgeSource>();
        foreach (var location in locations.Distinct())
        {
            var layer = location.map?.GetLayer("Buildings");
            if (layer is not null)
            {
                for (var y = 0; y < layer.LayerHeight; y++)
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    if (string.Equals(location.doesTileHaveProperty(x, y, "Action", "Buildings"), "Forge", StringComparison.Ordinal))
                    {
                        rows.Add(new ForgeSource("forge-action:" + location.NameOrUniqueName + ":" + x + "," + y,
                            "forge_action", location, new Point(x, y)));
                    }
                }
            }
            foreach (var pair in location.objects.Pairs)
            {
                if (pair.Value.QualifiedItemId == "(BC)MiniForge")
                {
                    rows.Add(new ForgeSource("mini-forge:" + location.NameOrUniqueName + ":" + (int)pair.Key.X + "," + (int)pair.Key.Y,
                        "mini_forge", location, new Point((int)pair.Key.X, (int)pair.Key.Y)));
                }
            }
        }
        return rows.GroupBy(value => value.SourceId, StringComparer.Ordinal).Select(value => value.First())
            .OrderBy(value => value.SourceId, StringComparer.Ordinal).ToArray();
    }

    private static ForgeItemSource[] ReadForgeItemSources(Farmer player)
    {
        var rows = player.Items.Select((item, slot) => item is null
                ? null
                : new ForgeItemSource("inventory:" + slot, "inventory", slot, item))
            .Where(value => value is not null).Cast<ForgeItemSource>().ToList();
        if (player.leftRing.Value is { } left)
        {
            rows.Add(new ForgeItemSource("equipped:left_ring", "equipped_left_ring", null, left));
        }
        if (player.rightRing.Value is { } right)
        {
            rows.Add(new ForgeItemSource("equipped:right_ring", "equipped_right_ring", null, right));
        }
        return rows.OrderBy(value => value.SourceId, StringComparer.Ordinal).ToArray();
    }

    private static string ForgeFamily(Item left, Item right)
    {
        if (left is Ring && right is Ring) return "combine_rings";
        if (left is MeleeWeapon && right is MeleeWeapon) return "weapon_appearance";
        return right.QualifiedItemId switch
        {
            "(O)896" => "galaxy_soul",
            "(O)72" => "diamond_forge",
            "(O)852" => "dragon_tooth_innate_reroll",
            "(O)74" => "prismatic_enchant",
            _ => "gem_forge"
        };
    }

    private static int ForgeCost(Item left, Item right) => right.QualifiedItemId switch
    {
        "(O)896" or "(O)74" => 20,
        "(O)72" or "(O)852" => 10,
        _ when left is Ring && right is Ring => 20,
        _ when left is Ring => 1,
        _ when left is MeleeWeapon && right is MeleeWeapon => 10,
        _ when left is Tool tool => 10 + tool.GetTotalForgeLevels() * 5,
        _ => 1
    };

    private static int UnforgeShardRefund(Item item)
    {
        if (item is CombinedRing) return 10;
        if (item is not MeleeWeapon weapon) return 0;
        var total = 0;
        var levels = weapon.GetTotalForgeLevels(for_unforge: true);
        for (var level = 0; level < levels; level++) total += 10 + level * 5;
        if (weapon.hasEnchantmentOfType<DiamondEnchantment>()) total += 10;
        if (weapon.appearance.Value is not null) total += 10;
        return total / 2;
    }

    private static ForgeOutputProjection ForgeOutputContract(Item left, Item? right, string family)
    {
        if (family == "diamond_forge" && left is Tool diamondTool)
        {
            var present = diamondTool.enchantments.Where(value => value.IsForge())
                .Select(value => value.GetType().Name).ToHashSet(StringComparer.Ordinal);
            var addCount = Math.Min(diamondTool.GetMaxForges() - diamondTool.GetTotalForgeLevels(),
                DiamondForgeTypes.Count(value => !present.Contains(value)));
            return new ForgeOutputProjection("native_random_result_domain", null, JsonSerializer.Serialize(new
            {
                branch = family,
                add_distinct_count = addCount,
                allowed_added_runtime_types = DiamondForgeTypes.Where(value => !present.Contains(value)).ToArray(),
                diamond_marker_required = true
            }));
        }
        if (family == "dragon_tooth_innate_reroll" && left is MeleeWeapon weapon)
        {
            var oldTypes = weapon.enchantments.Where(value => value.IsSecondaryEnchantment() && value is not GalaxySoulEnchantment)
                .Select(value => value.GetType().Name).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return new ForgeOutputProjection("native_random_result_domain", null, JsonSerializer.Serialize(new
            {
                branch = family,
                excluded_previous_runtime_types = oldTypes,
                required_non_galaxy_secondary_count_min = 1,
                required_non_galaxy_secondary_count_max = 2,
                allowed = new object[]
                {
                    new { runtime_type = nameof(DefenseEnchantment), min_level = 1, max_level = 2 },
                    new { runtime_type = nameof(LightweightEnchantment), min_level = 1, max_level = 5 },
                    new { runtime_type = nameof(SlimeGathererEnchantment), min_level = 1, max_level = 1 },
                    new { runtime_type = nameof(AttackEnchantment), min_level = 1, max_level = 5 },
                    new { runtime_type = nameof(CritEnchantment), min_level = 1, max_level = 3 },
                    new { runtime_type = nameof(WeaponSpeedEnchantment), min_level = 1, max_level = 4 },
                    new { runtime_type = nameof(SlimeSlayerEnchantment), min_level = 1, max_level = 1 },
                    new { runtime_type = nameof(CritPowerEnchantment), min_level = 1, max_level = 3 }
                }
            }));
        }
        if (family == "unforge_combined_ring" && left is CombinedRing combined)
        {
            return new ForgeOutputProjection("exact_multi_output", null, JsonSerializer.Serialize(new
            {
                branch = family,
                output_states = combined.combinedRings.Select(ForgeItemStateObject).ToArray(),
                cinder_shards = 10
            }));
        }
        if (family == "unforge_weapon" && left is MeleeWeapon unforgeWeapon)
        {
            var copy = (MeleeWeapon)unforgeWeapon.getOne();
            foreach (var enchantment in copy.enchantments.Where(value => value.IsForge()).ToArray()) copy.RemoveEnchantment(enchantment);
            var appearance = copy.appearance.Value;
            copy.appearance.Value = null;
            copy.ResetIndexOfMenuItemView();
            return new ForgeOutputProjection("exact_unforge_output", copy, JsonSerializer.Serialize(new
            {
                branch = family,
                returned_appearance_qualified_item_id = appearance ?? string.Empty,
                cinder_shards = UnforgeShardRefund(unforgeWeapon)
            }));
        }

        Item? output = null;
        if (left is Tool tool && right is not null)
        {
            var copy = (Tool)tool.getOne();
            var countedEnchantment = family is "prismatic_enchant" or "galaxy_soul"
                ? BaseEnchantment.GetEnchantmentFromItem(copy, right)
                : null;
            output = copy.Forge(right.getOne(), count_towards_stats: false) ? copy : null;
            if (output is Tool counted && countedEnchantment is not null)
            {
                counted.previousEnchantments.Insert(0, countedEnchantment.GetName());
                while (counted.previousEnchantments.Count > 2)
                {
                    counted.previousEnchantments.RemoveAt(counted.previousEnchantments.Count - 1);
                }
            }
        }
        else if (left is Ring ring && right is Ring other)
        {
            output = ring.Combine(other);
        }
        return new ForgeOutputProjection("exact_item_state", output, string.Empty);
    }

    private static bool ForgeOutputCanFit(Farmer player, ForgeItemSource left, ForgeItemSource? right, string family, Item? output)
    {
        var projected = player.Items.Select(item => item).ToList();
        if (left.SlotIndex.HasValue) projected[left.SlotIndex.Value] = null!;
        if (right?.SlotIndex is { } rightSlot) projected[rightSlot] = null!;
        if (family == "unforge_combined_ring")
        {
            return projected.Count(item => item is null) >= 2;
        }
        if (family == "unforge_weapon")
        {
            var weapon = (MeleeWeapon)left.Item;
            var refund = UnforgeShardRefund(weapon);
            var shardCapacity = projected
                .Where(item => item?.QualifiedItemId == "(O)848")
                .Sum(item => item!.maximumStackSize() - item.Stack);
            var emptySlots = projected.Count(item => item is null);
            var shardMaximumStack = ItemRegistry.Create("(O)848").maximumStackSize();
            var shardSlots = (Math.Max(0, refund - shardCapacity) + shardMaximumStack - 1) / shardMaximumStack;
            var appearanceSlots = weapon.appearance.Value is null ? 0 : 1;
            return emptySlots >= shardSlots + appearanceSlots;
        }
        return output is null || Utility.canItemBeAddedToThisInventoryList(output, projected);
    }

    internal static string ForgeItemStateJson(Item item) => JsonSerializer.Serialize(ForgeItemStateObject(item));

    private static object ForgeItemStateObject(Item item) => new
    {
        qualified_item_id = item.QualifiedItemId,
        runtime_type = item.GetType().FullName,
        stack = item.Stack,
        quality = item.Quality,
        enchantments = item is Tool tool
            ? tool.enchantments.Select(value => new { runtime_type = value.GetType().Name, level = value.Level }).ToArray()
            : Array.Empty<object>(),
        previous_enchantments = item is Tool previous ? previous.previousEnchantments.ToArray() : Array.Empty<string>(),
        total_forge_levels = item is Tool forge ? forge.GetTotalForgeLevels() : 0,
        total_unforge_levels = item is Tool unforge ? unforge.GetTotalForgeLevels(for_unforge: true) : 0,
        max_forge_levels = item is Tool maximum ? maximum.GetMaxForges() : 0,
        weapon_appearance = item is MeleeWeapon weapon ? weapon.appearance.Value ?? string.Empty : string.Empty,
        weapon_type = item is MeleeWeapon typed ? typed.type.Value : -1,
        weapon_item_level = item is MeleeWeapon leveled ? leveled.getItemLevel() : -1,
        combined_ring_ids = item is CombinedRing combined
            ? combined.combinedRings.Select(value => value.QualifiedItemId).ToArray()
            : Array.Empty<string>()
    };

    private sealed record ForgeSource(string SourceId, string Kind, GameLocation Location, Point Tile);
    private sealed record ForgeItemSource(string SourceId, string Kind, int? SlotIndex, Item Item);
    private sealed record ForgeOutputProjection(string Kind, Item? Output, string RandomContractJson);
}
