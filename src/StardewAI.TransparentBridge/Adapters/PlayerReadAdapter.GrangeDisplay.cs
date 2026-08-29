using System.Text.Json;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string GrangeNativeContract =
        "Event.checkAction(festival_fall16_buildings_349_350_351)->FarmerTeam.grangeMutex->StorageContainer(9x3,Event.onGrangeChange,Utility.highlightSmallObjects)->one_native_remove_or_place_click_pair->okButton->mutex_release";

    private static object ReadGrangeDisplayContext(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        var festival = location.currentEvent;
        var activeFair = festival is not null && festival.isFestival &&
            string.Equals(festival.id, "festival_fall16", StringComparison.Ordinal);
        if (!activeFair)
        {
            return new
            {
                schema_version = "grange_display.v1",
                projection_status = "contextual_inactive_festival_fall16",
                native_contract = GrangeNativeContract,
                rows = Array.Empty<object>()
            };
        }
        var fair = festival!;

        var interactionTiles = ReadGrangeInteractionTiles(location);
        var display = Enumerable.Range(0, 9)
            .Select(slot => slot < player.team.grangeDisplay.Count ? player.team.grangeDisplay[slot] : null)
            .ToArray();
        var choices = BuildGrangeChoices(player, display);
        var best = SelectBestGrangeChoices(choices);
        var currentScore = ScoreGrange(display);
        var bestScore = ScoreSelectedGrange(best);
        var operation = BuildNextGrangeOperation(player, fair, display, best, currentScore);
        var mutexLocked = player.team.grangeMutex.IsLocked();
        var mutexHeldByActor = player.team.grangeMutex.IsLockHeld();
        var lockedByOther = mutexLocked && !mutexHeldByActor;
        var gateStatus = lockedByOther
            ? "blocked_grange_mutex_locked_by_other"
            : interactionTiles.Length == 0
                ? "blocked_grange_interaction_tile_unavailable"
                : Game1.activeClickableMenu is not null
                    ? "blocked_active_menu_open"
                    : !player.CanMove || player.UsingTool || Game1.dialogueUp
                        ? "blocked_player_busy"
                        : operation is null
                            ? fair.grangeJudged
                                ? "complete_grange_items_retrieved"
                                : currentScore >= 90
                                    ? "complete_first_place_display_ready"
                                    : "complete_best_available_display_below_first_place"
                            : operation.status;
        var rows = choices.Select(choice => new
        {
            source_kind = choice.SourceKind,
            source_slot_index = choice.SourceSlotIndex,
            source_unit_ordinal = choice.UnitOrdinal,
            qualified_item_id = choice.Item.QualifiedItemId,
            item_id = choice.Item.ItemId,
            runtime_type = choice.Item.GetType().FullName,
            display_name = choice.Item.DisplayName,
            stack = choice.Item.Stack,
            quality = choice.Item.Quality,
            category = choice.Item.Category,
            actual_sell_price = choice.SellPrice,
            item_points = choice.ItemPoints,
            scoring_group = choice.ScoringGroup,
            category_bit = choice.CategoryBit,
            mayor_shorts = choice.MayorShorts,
            selected_for_best_display = best.Contains(choice)
        }).ToArray();
        var displayRows = display.Select((item, slot) => ReadGrangeDisplayRow(item, slot)).ToArray();
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "grange_display.v1",
            location = location.NameOrUniqueName,
            fair.id,
            fair.grangeJudged,
            fair.grangeScore,
            currentScore,
            bestScore,
            mutexLocked,
            mutexHeldByActor,
            interactionTiles,
            displayRows,
            rows,
            operation
        }));

        return new
        {
            schema_version = "grange_display.v1",
            projection_status = "complete_current_festival_fall16_grange_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            festival_id = fair.id,
            festival_location_id = location.NameOrUniqueName,
            grange_judged = fair.grangeJudged,
            native_grange_score = fair.grangeScore,
            current_projected_score = currentScore,
            best_available_score = bestScore,
            first_place_score = 90,
            best_available_reaches_first_place = bestScore >= 90,
            display_capacity = 9,
            scoring_contract = new
            {
                base_points = 14,
                item_count_points = "9-2*empty_slots",
                category_points = "min(30,distinct_scoring_groups*5)",
                price_thresholds = new[] { 20, 90, 200, 300, 400 },
                quality_points = "quality+1",
                first_place_minimum = 90,
                second_place_minimum = 75,
                third_place_minimum = 60,
                mayor_shorts_score = -666
            },
            mutex_locked = mutexLocked,
            mutex_held_by_actor = mutexHeldByActor,
            mutex_locked_by_other = lockedByOther,
            interaction_tiles = interactionTiles,
            display_rows = displayRows,
            rows,
            next_operation = operation,
            native_contract = GrangeNativeContract
        };
    }

    private static object[] ReadGrangeInteractionTiles(GameLocation location)
    {
        if (location.Map?.Layers.Count is not > 0)
            return Array.Empty<object>();
        var layer = location.Map.Layers[0];
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
                if (tileIndex is 349 or 350 or 351)
                    result.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex });
            }
        }
        return result.ToArray();
    }

    private static object ReadGrangeDisplayRow(Item? item, int slot)
    {
        if (item is not StardewValley.Object obj)
        {
            return new
            {
                display_slot_index = slot,
                occupied = item is not null,
                qualified_item_id = item?.QualifiedItemId ?? string.Empty,
                runtime_type = item?.GetType().FullName ?? string.Empty,
                quality = item?.Quality ?? -1,
                actual_sell_price = item?.sellToStorePrice(-1L) ?? 0,
                item_points = 0,
                scoring_group = string.Empty,
                mayor_shorts = item is not null && StardewValley.Event.IsItemMayorShorts(item)
            };
        }
        return new
        {
            display_slot_index = slot,
            occupied = true,
            qualified_item_id = obj.QualifiedItemId,
            runtime_type = obj.GetType().FullName ?? string.Empty,
            quality = obj.Quality,
            actual_sell_price = obj.sellToStorePrice(-1L),
            item_points = GrangeItemPoints(obj),
            scoring_group = GrangeScoringGroup(obj.Category),
            mayor_shorts = StardewValley.Event.IsItemMayorShorts(obj)
        };
    }

    private static List<GrangeChoice> BuildGrangeChoices(Farmer player, Item?[] display)
    {
        var result = new List<GrangeChoice>();
        for (var slot = 0; slot < display.Length; slot++)
        {
            if (display[slot] is StardewValley.Object obj && !obj.bigCraftable.Value && !StardewValley.Event.IsItemMayorShorts(obj))
                result.Add(GrangeChoice.Create("display", slot, 0, obj));
        }
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            if (player.Items[slot] is not StardewValley.Object obj || obj.bigCraftable.Value ||
                StardewValley.Event.IsItemMayorShorts(obj) || obj.Stack < 1)
                continue;
            for (var unit = 0; unit < Math.Min(9, obj.Stack); unit++)
                result.Add(GrangeChoice.Create("player_inventory", slot, unit, obj));
        }
        return result;
    }

    private static List<GrangeChoice> SelectBestGrangeChoices(List<GrangeChoice> choices)
    {
        var states = new Dictionary<(int Count, int Mask), GrangeSelection>
        {
            [(0, 0)] = new GrangeSelection(0, 0, 0, new List<GrangeChoice>())
        };
        foreach (var choice in choices)
        {
            foreach (var entry in states.ToArray())
            {
                if (entry.Key.Count >= 9)
                    continue;
                var key = (entry.Key.Count + 1, entry.Key.Mask | choice.CategoryBit);
                var selected = new List<GrangeChoice>(entry.Value.Selected) { choice };
                var candidate = new GrangeSelection(
                    entry.Value.ItemPoints + choice.ItemPoints,
                    entry.Value.PreservedDisplayCount + (choice.SourceKind == "display" ? 1 : 0),
                    entry.Value.TotalSellPrice + choice.SellPrice,
                    selected);
                if (!states.TryGetValue(key, out var existing) || BetterGrangeSelection(candidate, existing))
                    states[key] = candidate;
            }
        }
        return states
            .Select(entry => new
            {
                Score = 14 + entry.Value.ItemPoints + Math.Min(30, CountBits(entry.Key.Mask) * 5) + 2 * entry.Key.Count - 9,
                entry.Value.PreservedDisplayCount,
                entry.Value.TotalSellPrice,
                Signature = GrangeSelectionSignature(entry.Value.Selected),
                entry.Value.Selected
            })
            .OrderByDescending(row => row.Score)
            .ThenByDescending(row => row.PreservedDisplayCount)
            .ThenBy(row => row.TotalSellPrice)
            .ThenBy(row => row.Signature, StringComparer.Ordinal)
            .First().Selected;
    }

    private static bool BetterGrangeSelection(GrangeSelection candidate, GrangeSelection existing)
    {
        if (candidate.ItemPoints != existing.ItemPoints)
            return candidate.ItemPoints > existing.ItemPoints;
        if (candidate.PreservedDisplayCount != existing.PreservedDisplayCount)
            return candidate.PreservedDisplayCount > existing.PreservedDisplayCount;
        if (candidate.TotalSellPrice != existing.TotalSellPrice)
            return candidate.TotalSellPrice < existing.TotalSellPrice;
        return string.CompareOrdinal(GrangeSelectionSignature(candidate.Selected), GrangeSelectionSignature(existing.Selected)) < 0;
    }

    private static string GrangeSelectionSignature(IEnumerable<GrangeChoice> selected) =>
        string.Join("|", selected.Select(choice => choice.StableKey).OrderBy(value => value, StringComparer.Ordinal));

    private static GrangeOperation? BuildNextGrangeOperation(
        Farmer player,
        StardewValley.Event festival,
        Item?[] display,
        List<GrangeChoice> best,
        int currentScore)
    {
        if (festival.grangeJudged)
        {
            var occupied = Array.FindIndex(display, item => item is not null);
            return occupied < 0 ? null : BuildRemoveOperation(player, display, occupied, currentScore, "retrieve_after_judging");
        }

        var selectedDisplaySlots = best
            .Where(choice => choice.SourceKind == "display")
            .Select(choice => choice.SourceSlotIndex)
            .ToHashSet();
        for (var slot = 0; slot < display.Length; slot++)
        {
            if (display[slot] is not null && !selectedDisplaySlots.Contains(slot))
                return BuildRemoveOperation(player, display, slot, currentScore, "prepare_best_available_display");
        }

        var emptySlot = Array.FindIndex(display, item => item is null);
        var inventoryChoice = best
            .Where(choice => choice.SourceKind == "player_inventory")
            .OrderBy(choice => choice.SourceSlotIndex)
            .ThenBy(choice => choice.UnitOrdinal)
            .FirstOrDefault();
        if (emptySlot < 0 || inventoryChoice is null)
            return null;
        var after = display.ToArray();
        after[emptySlot] = inventoryChoice.Item.getOne();
        after[emptySlot]!.Stack = 1;
        return new GrangeOperation
        {
            status = "ready",
            objective = "prepare_best_available_display",
            operation = "place",
            display_slot_index = emptySlot,
            inventory_slot_index = inventoryChoice.SourceSlotIndex,
            inventory_stack_before = inventoryChoice.Item.Stack,
            inventory_stack_after = inventoryChoice.Item.Stack - 1,
            sink_inventory_slot_index = -1,
            qualified_item_id = inventoryChoice.Item.QualifiedItemId,
            item_id = inventoryChoice.Item.ItemId,
            runtime_type = inventoryChoice.Item.GetType().FullName ?? string.Empty,
            quality = inventoryChoice.Item.Quality,
            actual_sell_price = inventoryChoice.SellPrice,
            item_points = inventoryChoice.ItemPoints,
            scoring_group = inventoryChoice.ScoringGroup,
            score_before = currentScore,
            score_after = ScoreGrange(after),
            occupied_slots_before = display.Count(item => item is not null),
            occupied_slots_after = display.Count(item => item is not null) + 1
        };
    }

    private static GrangeOperation BuildRemoveOperation(
        Farmer player,
        Item?[] display,
        int displaySlot,
        int currentScore,
        string objective)
    {
        var item = display[displaySlot]!;
        var sink = FindGrangeInventorySink(player, item);
        var after = display.ToArray();
        after[displaySlot] = null;
        return new GrangeOperation
        {
            status = sink < 0 ? "blocked_inventory_capacity_for_grange_retrieval" : "ready",
            objective = objective,
            operation = "remove",
            display_slot_index = displaySlot,
            inventory_slot_index = -1,
            inventory_stack_before = -1,
            inventory_stack_after = -1,
            sink_inventory_slot_index = sink,
            qualified_item_id = item.QualifiedItemId,
            item_id = item.ItemId,
            runtime_type = item.GetType().FullName ?? string.Empty,
            quality = item.Quality,
            actual_sell_price = item.sellToStorePrice(-1L),
            item_points = item is StardewValley.Object obj ? GrangeItemPoints(obj) : 0,
            scoring_group = item is StardewValley.Object scored ? GrangeScoringGroup(scored.Category) : string.Empty,
            score_before = currentScore,
            score_after = ScoreGrange(after),
            occupied_slots_before = display.Count(row => row is not null),
            occupied_slots_after = display.Count(row => row is not null) - 1
        };
    }

    private static int FindGrangeInventorySink(Farmer player, Item item)
    {
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            var existing = player.Items[slot];
            if (existing is not null && existing.canStackWith(item) && existing.Stack < existing.maximumStackSize())
                return slot;
        }
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            if (player.Items[slot] is null)
                return slot;
        }
        return -1;
    }

    private static int ScoreSelectedGrange(IReadOnlyCollection<GrangeChoice> selected)
    {
        var mask = selected.Aggregate(0, (value, choice) => value | choice.CategoryBit);
        return 14 + selected.Sum(choice => choice.ItemPoints) + Math.Min(30, CountBits(mask) * 5) + 2 * selected.Count - 9;
    }

    private static int ScoreGrange(IReadOnlyList<Item?> display)
    {
        var score = 14;
        var empty = 0;
        var mask = 0;
        var shorts = false;
        for (var slot = 0; slot < 9; slot++)
        {
            var item = slot < display.Count ? display[slot] : null;
            if (item is StardewValley.Object obj)
            {
                shorts |= StardewValley.Event.IsItemMayorShorts(obj);
                score += GrangeItemPoints(obj);
                mask |= GrangeCategoryBit(obj.Category);
            }
            else if (item is null)
            {
                empty++;
            }
        }
        score += Math.Min(30, CountBits(mask) * 5);
        score += 9 - 2 * empty;
        return shorts ? -666 : score;
    }

    private static int GrangeItemPoints(StardewValley.Object item)
    {
        var points = item.Quality + 1;
        var price = item.sellToStorePrice(-1L);
        if (price >= 20) points++;
        if (price >= 90) points++;
        if (price >= 200) points++;
        if (price >= 300 && item.Quality < 2) points++;
        if (price >= 400 && item.Quality < 1) points++;
        return points;
    }

    private static int GrangeCategoryBit(int category) => category switch
    {
        -75 => 1 << 0,
        -79 => 1 << 1,
        -18 or -14 or -6 or -5 => 1 << 2,
        -12 or -2 => 1 << 3,
        -4 => 1 << 4,
        -81 or -80 or -27 => 1 << 5,
        -7 => 1 << 6,
        -26 => 1 << 7,
        _ => 0
    };

    private static string GrangeScoringGroup(int category) => category switch
    {
        -75 => "vegetable",
        -79 => "fruit",
        -18 or -14 or -6 or -5 => "animal_product",
        -12 or -2 => "mineral",
        -4 => "fish",
        -81 or -80 or -27 => "foraging_flower_syrup",
        -7 => "cooking",
        -26 => "artisan",
        _ => string.Empty
    };

    private static int CountBits(int value)
    {
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private sealed class GrangeChoice
    {
        public string SourceKind { get; init; } = string.Empty;
        public int SourceSlotIndex { get; init; }
        public int UnitOrdinal { get; init; }
        public StardewValley.Object Item { get; init; } = null!;
        public int SellPrice { get; init; }
        public int ItemPoints { get; init; }
        public int CategoryBit { get; init; }
        public string ScoringGroup { get; init; } = string.Empty;
        public bool MayorShorts { get; init; }
        public string StableKey => ItemPoints.ToString("D2") + ":" + CategoryBit.ToString("D3") + ":" +
            Item.QualifiedItemId + ":" + Item.Quality + ":" + SellPrice.ToString("D8") + ":" +
            (SourceKind == "display" ? "0" : "1") + ":" + SourceSlotIndex.ToString("D2") + ":" + UnitOrdinal;

        public static GrangeChoice Create(string sourceKind, int sourceSlotIndex, int unitOrdinal, StardewValley.Object item) => new()
        {
            SourceKind = sourceKind,
            SourceSlotIndex = sourceSlotIndex,
            UnitOrdinal = unitOrdinal,
            Item = item,
            SellPrice = item.sellToStorePrice(-1L),
            ItemPoints = GrangeItemPoints(item),
            CategoryBit = GrangeCategoryBit(item.Category),
            ScoringGroup = GrangeScoringGroup(item.Category),
            MayorShorts = StardewValley.Event.IsItemMayorShorts(item)
        };
    }

    private sealed record GrangeSelection(
        int ItemPoints,
        int PreservedDisplayCount,
        int TotalSellPrice,
        List<GrangeChoice> Selected);

    private sealed class GrangeOperation
    {
        public string status { get; init; } = string.Empty;
        public string objective { get; init; } = string.Empty;
        public string operation { get; init; } = string.Empty;
        public int display_slot_index { get; init; }
        public int inventory_slot_index { get; init; }
        public int inventory_stack_before { get; init; }
        public int inventory_stack_after { get; init; }
        public int sink_inventory_slot_index { get; init; }
        public string qualified_item_id { get; init; } = string.Empty;
        public string item_id { get; init; } = string.Empty;
        public string runtime_type { get; init; } = string.Empty;
        public int quality { get; init; }
        public int actual_sell_price { get; init; }
        public int item_points { get; init; }
        public string scoring_group { get; init; } = string.Empty;
        public int score_before { get; init; }
        public int score_after { get; init; }
        public int occupied_slots_before { get; init; }
        public int occupied_slots_after { get; init; }
    }
}
