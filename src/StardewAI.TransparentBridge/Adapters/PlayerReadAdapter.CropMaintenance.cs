using StardewValley;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadWateringCanState(Farmer? player)
    {
        var entry = player?.Items
            .Select((item, index) => new { item, index })
            .FirstOrDefault(value => value.item is WateringCan);
        var can = entry?.item as WateringCan;
        var exactVanilla = can?.GetType() == typeof(WateringCan);
        var energy = can is null || player is null
            ? (double?)null
            : can.IsEfficient
                ? 0d
                : Math.Max(0d, 2d - player.FarmingLevel * 0.1d);
        return new
        {
            projection_status = exactVanilla
                ? "exact_vanilla_power_zero"
                : can is null
                    ? "watering_can_missing"
                    : "custom_watering_can_runtime",
            slot_index = entry?.index,
            runtime_type = can?.GetType().FullName ?? string.Empty,
            upgrade_level = can?.UpgradeLevel,
            water_left = can?.WaterLeft,
            capacity = can?.waterCanMax,
            is_bottomless = can?.IsBottomless,
            farmer_has_watering_can_enchantment = player?.hasWateringCanEnchantment,
            is_efficient = can?.IsEfficient,
            farming_level = player?.FarmingLevel,
            energy_cost_per_power_zero_action = energy
        };
    }
}
