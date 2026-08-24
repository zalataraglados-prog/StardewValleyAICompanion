using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadFenceState(StardewValley.Object item)
    {
        if (item is not Fence fence)
        {
            return new
            {
                status = "not_fence",
                runtime_type_supported = false
            };
        }

        var exactBase = fence.GetType() == typeof(Fence);
        var data = fence.GetData();
        return new
        {
            status = exactBase && data is not null ? "available" : "custom_or_missing_data_blocked",
            runtime_type_supported = exactBase,
            is_gate = fence.isGate.Value,
            gate_position = fence.gatePosition.Value,
            draw_sum = fence.getDrawSum(),
            health = fence.health.Value,
            max_health = fence.maxHealth.Value,
            repair_queued = fence.repairQueued.Value,
            is_passable = fence.isPassable(),
            fence_data_key = fence.ItemId,
            fence_data_health = data?.Health,
            native_runtime_type = fence.GetType().FullName
        };
    }
}
