using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Network;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadPanning(GameLocation location, Farmer player)
    {
        return ReadPanningProjection(location, player).ToState();
    }

    private static PanningProjection ReadPanningProjection(GameLocation location, Farmer player)
    {
        var point = location.orePanPoint.Value;
        var panEntry = player.Items
            .Select((item, index) => new { item, index })
            .Where(entry => entry.item is Pan)
            .OrderByDescending(entry => ((Pan)entry.item!).UpgradeLevel)
            .ThenBy(entry => entry.index)
            .FirstOrDefault();
        if (point == Point.Zero)
        {
            return PanningProjection.Inactive(location, panEntry?.index);
        }
        if (panEntry?.item is not Pan pan)
        {
            return PanningProjection.Blocked(location, point, "pan_tool_unavailable");
        }
        if (pan.GetType() != typeof(Pan))
        {
            return PanningProjection.Blocked(location, point, "unsupported_pan_runtime_type", panEntry.index, pan);
        }

        try
        {
            var clone = new NetFarmerRoot(player).Clone().Value
                ?? throw new InvalidOperationException("farmer_clone_unavailable");
            clone.currentLocation = location;
            clone.Position = player.Position;

            // Pan rewards don't read Mining/Foraging levels. Keeping these below
            // mastery and level-up thresholds prevents preview-only global UI/stat side effects.
            clone.miningLevel.Value = 0;
            clone.foragingLevel.Value = 0;
            clone.experiencePoints[Farmer.miningSkill] = 15000;
            clone.experiencePoints[Farmer.foragingSkill] = 15000;
            var miningBefore = clone.experiencePoints[Farmer.miningSkill];
            var foragingBefore = clone.experiencePoints[Farmer.foragingSkill];
            var timesPannedBefore = clone.stats.Get("TimesPanned");
            var pointBefore = location.orePanPoint.Value;

            List<Item> items;
            var liveRandom = Game1.random;
            try
            {
                Game1.random = new Random(0);
                items = pan.getPanItems(location, clone);
            }
            finally
            {
                Game1.random = liveRandom;
            }
            foreach (var item in items)
            {
                item.HasBeenInInventory = true;
            }
            if (location.orePanPoint.Value != pointBefore)
            {
                return PanningProjection.Blocked(location, point, "native_preview_mutated_ore_pan_point", panEntry.index, pan);
            }

            var projectedItems = items
                .Select(ClearanceOutputItemProjection.From)
                .GroupBy(item => new { item.RuntimeType, item.QualifiedItemId, item.Quality, item.UnitStateSha256 })
                .Select(group => new ClearanceOutputItemProjection(
                    group.Key.RuntimeType,
                    group.Key.QualifiedItemId,
                    group.Key.Quality,
                    group.Key.UnitStateSha256,
                    group.Sum(item => item.Quantity)))
                .OrderBy(item => item.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(item => item.UnitStateSha256, StringComparer.Ordinal)
                .ToArray();

            var inventoryAcceptsAll = true;
            foreach (var source in items)
            {
                var item = source.getOne();
                item.Stack = source.Stack;
                if (Utility.addItemToThisInventoryList(item, clone.Items, clone.MaxItems) is not null)
                {
                    inventoryAcceptsAll = false;
                    break;
                }
            }

            var rangeSize = 4 + (pan.hasEnchantmentOfType<StardewValley.Enchantments.ReachingToolEnchantment>() ? 1 : 0);
            var rectangle = new Rectangle(
                point.X * Game1.tileSize - (int)(Game1.tileSize * (rangeSize / 2f)),
                point.Y * Game1.tileSize - (int)(Game1.tileSize * (rangeSize / 2f)),
                Game1.tileSize * rangeSize,
                Game1.tileSize * rangeSize);
            var enchantments = pan.enchantments
                .Select(enchantment => enchantment.GetType().FullName ?? enchantment.GetType().Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var receiptStatIncrements = PanningProjection.PanningReceiptStatIncrements(items, player);

            return new PanningProjection
            {
                Status = inventoryAcceptsAll ? "exact" : "inventory_capacity_blocked",
                LocationId = location.NameOrUniqueName,
                OrePanPointActive = true,
                OrePanPointX = point.X,
                OrePanPointY = point.Y,
                PanToolSlotIndex = panEntry.index,
                PanRuntimeType = pan.GetType().FullName ?? pan.GetType().Name,
                PanUpgradeLevel = pan.UpgradeLevel,
                PanEnchantments = enchantments,
                PanEnchantmentsJson = JsonSerializer.Serialize(enchantments),
                InteractionRangeSizeTiles = rangeSize,
                InteractionRectangleX = rectangle.X,
                InteractionRectangleY = rectangle.Y,
                InteractionRectangleWidth = rectangle.Width,
                InteractionRectangleHeight = rectangle.Height,
                ClickPixelX = point.X * Game1.tileSize + Game1.tileSize / 2,
                ClickPixelY = point.Y * Game1.tileSize + Game1.tileSize / 2,
                LuckLevelBase = player.luckLevel.Value,
                LuckLevelEffective = player.LuckLevel,
                DailyLuck = player.DailyLuck,
                DaysPlayed = Game1.stats.DaysPlayed,
                TimesPannedBefore = timesPannedBefore,
                TimesPannedAfter = clone.stats.Get("TimesPanned"),
                MiningExperienceBefore = player.experiencePoints[Farmer.miningSkill],
                MiningExperienceDelta = clone.experiencePoints[Farmer.miningSkill] - miningBefore,
                MiningExperienceAfter = player.experiencePoints[Farmer.miningSkill] + clone.experiencePoints[Farmer.miningSkill] - miningBefore,
                ForagingExperienceBefore = player.experiencePoints[Farmer.foragingSkill],
                ForagingExperienceDelta = clone.experiencePoints[Farmer.foragingSkill] - foragingBefore,
                ForagingExperienceAfter = player.experiencePoints[Farmer.foragingSkill] + clone.experiencePoints[Farmer.foragingSkill] - foragingBefore,
                InventoryAcceptsAllOutputs = inventoryAcceptsAll,
                ExpectedOutputItems = projectedItems,
                ExpectedOutputItemsJson = JsonSerializer.Serialize(projectedItems),
                ExpectedReceiptStatIncrementsJson = JsonSerializer.Serialize(receiptStatIncrements),
                NativeReceiptCallbacksStatus = "runtime_observed:NotifyQuests,specialOrders.onItemCollected,SetFlagOnPickup,SpecialItem.actionWhenReceived,checkForHeldItemAchievements,foundMineral,foundArtifact",
                PostUseOrePanPointStatus = pan.UpgradeLevel <= 1 ? "cleared" : "runtime_rng_observed",
                PostUseRespawnAttempts = Math.Max(0, pan.UpgradeLevel - 1),
                ProjectionBasis = "Pan.getPanItems exact native call on detached farmer clone; Pan.DoFunction decompile for post-use orePanPoint"
            };
        }
        catch (Exception ex)
        {
            return PanningProjection.Blocked(location, point, "native_pan_preview_failed:" + ex.GetType().Name, panEntry.index, pan);
        }
    }
}

internal sealed record PanningProjection
{
    public string Status { get; init; } = "inactive";
    public string LocationId { get; init; } = string.Empty;
    public bool OrePanPointActive { get; init; }
    public int? OrePanPointX { get; init; }
    public int? OrePanPointY { get; init; }
    public int? PanToolSlotIndex { get; init; }
    public string PanRuntimeType { get; init; } = string.Empty;
    public int? PanUpgradeLevel { get; init; }
    public string[] PanEnchantments { get; init; } = Array.Empty<string>();
    public string PanEnchantmentsJson { get; init; } = "[]";
    public int? InteractionRangeSizeTiles { get; init; }
    public int? InteractionRectangleX { get; init; }
    public int? InteractionRectangleY { get; init; }
    public int? InteractionRectangleWidth { get; init; }
    public int? InteractionRectangleHeight { get; init; }
    public int? ClickPixelX { get; init; }
    public int? ClickPixelY { get; init; }
    public int? LuckLevelBase { get; init; }
    public int? LuckLevelEffective { get; init; }
    public double? DailyLuck { get; init; }
    public uint? DaysPlayed { get; init; }
    public uint? TimesPannedBefore { get; init; }
    public uint? TimesPannedAfter { get; init; }
    public int? MiningExperienceBefore { get; init; }
    public int? MiningExperienceDelta { get; init; }
    public int? MiningExperienceAfter { get; init; }
    public int? ForagingExperienceBefore { get; init; }
    public int? ForagingExperienceDelta { get; init; }
    public int? ForagingExperienceAfter { get; init; }
    public bool? InventoryAcceptsAllOutputs { get; init; }
    public ClearanceOutputItemProjection[] ExpectedOutputItems { get; init; } = Array.Empty<ClearanceOutputItemProjection>();
    public string ExpectedOutputItemsJson { get; init; } = "[]";
    public string ExpectedReceiptStatIncrementsJson { get; init; } = "[]";
    public string NativeReceiptCallbacksStatus { get; init; } = string.Empty;
    public string PostUseOrePanPointStatus { get; init; } = string.Empty;
    public int? PostUseRespawnAttempts { get; init; }
    public string ProjectionBasis { get; init; } = string.Empty;

    public object ToState() => new
    {
        status = Status,
        location_id = LocationId,
        ore_pan_point_active = OrePanPointActive,
        ore_pan_point_x = OrePanPointX,
        ore_pan_point_y = OrePanPointY,
        pan_tool_slot_index = PanToolSlotIndex,
        pan_runtime_type = PanRuntimeType,
        pan_upgrade_level = PanUpgradeLevel,
        pan_enchantments = PanEnchantments,
        pan_enchantments_json = PanEnchantmentsJson,
        interaction_range_size_tiles = InteractionRangeSizeTiles,
        interaction_rectangle_x = InteractionRectangleX,
        interaction_rectangle_y = InteractionRectangleY,
        interaction_rectangle_width = InteractionRectangleWidth,
        interaction_rectangle_height = InteractionRectangleHeight,
        click_pixel_x = ClickPixelX,
        click_pixel_y = ClickPixelY,
        luck_level_base = LuckLevelBase,
        luck_level_effective = LuckLevelEffective,
        daily_luck = DailyLuck,
        days_played = DaysPlayed,
        times_panned_before = TimesPannedBefore,
        times_panned_after = TimesPannedAfter,
        mining_experience_before = MiningExperienceBefore,
        mining_experience_delta = MiningExperienceDelta,
        mining_experience_after = MiningExperienceAfter,
        foraging_experience_before = ForagingExperienceBefore,
        foraging_experience_delta = ForagingExperienceDelta,
        foraging_experience_after = ForagingExperienceAfter,
        inventory_accepts_all_outputs = InventoryAcceptsAllOutputs,
        expected_output_items = ExpectedOutputItems,
        expected_output_items_json = ExpectedOutputItemsJson,
        expected_receipt_stat_increments_json = ExpectedReceiptStatIncrementsJson,
        native_receipt_callbacks_status = NativeReceiptCallbacksStatus,
        post_use_ore_pan_point_status = PostUseOrePanPointStatus,
        post_use_respawn_attempts = PostUseRespawnAttempts,
        projection_basis = ProjectionBasis
    };

    public static PanningProjection Inactive(GameLocation location, int? panSlot) => new()
    {
        LocationId = location.NameOrUniqueName,
        PanToolSlotIndex = panSlot,
        ProjectionBasis = "GameLocation.orePanPoint"
    };


    public static PanningProjection Blocked(GameLocation location, Point point, string status, int? panSlot = null, Pan? pan = null) => new()
    {
        Status = status,
        LocationId = location.NameOrUniqueName,
        OrePanPointActive = true,
        OrePanPointX = point.X,
        OrePanPointY = point.Y,
        PanToolSlotIndex = panSlot,
        PanRuntimeType = pan?.GetType().FullName ?? string.Empty,
        PanUpgradeLevel = pan?.UpgradeLevel,
        ProjectionBasis = "GameLocation.orePanPoint; exact native Pan required"
    };

    internal static object[] PanningReceiptStatIncrements(IEnumerable<Item> items, Farmer player)
    {
        var amounts = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var amount = item.QualifiedItemId switch
            {
                "(O)378" => ("CopperFound", checked((uint)item.Stack)),
                "(O)380" => ("IronFound", checked((uint)item.Stack)),
                "(O)384" => ("GoldFound", checked((uint)item.Stack)),
                "(O)386" => ("IridiumFound", checked((uint)item.Stack)),
                "(O)72" => ("DiamondsFound", 1u),
                "(O)74" => ("PrismaticShardsFound", 1u),
                _ => (string.Empty, 0u)
            };
            if (!string.IsNullOrWhiteSpace(amount.Item1))
            {
                amounts[amount.Item1] = amounts.TryGetValue(amount.Item1, out var current)
                    ? checked(current + amount.Item2)
                    : amount.Item2;
            }
        }
        return amounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                var before = player.stats.Get(pair.Key);
                return (object)new { stat_name = pair.Key, amount = pair.Value, before, after = checked(before + pair.Value) };
            })
            .ToArray();
    }
}
