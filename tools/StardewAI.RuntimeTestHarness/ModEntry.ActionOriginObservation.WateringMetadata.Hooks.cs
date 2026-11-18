using System.Reflection;
using HarmonyLib;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool nativeWateringMetadataPatchesReady;
    private bool nativeWateringMetadataPatchesFailed;

    private void EnsureNativeWateringMetadataPatches()
    {
        if (nativeWateringMetadataPatchesFailed) throw new InvalidOperationException("Native watering metadata hooks previously failed.");
        if (nativeWateringMetadataPatchesReady) return;
        var harmony = new Harmony(ModManifest.UniqueID + ".native-watering-metadata");
        var installed = new List<MethodInfo>();
        try
        {
            var update = AccessTools.Method(typeof(Tool), nameof(Tool.Update), new[] { typeof(int), typeof(int), typeof(Farmer) })
                ?? throw new MissingMethodException("Native Tool.Update signature is unavailable.");
            var end = AccessTools.Method(typeof(Tool), nameof(Tool.endUsing), new[] { typeof(GameLocation), typeof(Farmer) })
                ?? throw new MissingMethodException("Native Tool.endUsing signature is unavailable.");
            // Do not patch Equip<T>: reference-type generic instantiations can share a body.
            var history = AccessTools.PropertySetter(typeof(Item), nameof(Item.HasBeenInInventory))
                ?? throw new MissingMethodException("Native inventory-history setter is unavailable.");
            foreach (var (method, prefix) in new[]
            {
                (update, nameof(NativeCollectionObservation.WateringUpdatePrefix)),
                (end, nameof(NativeCollectionObservation.WateringEndUsingPrefix)),
                (history, nameof(NativeCollectionObservation.WateringInventoryHistoryPrefix))
            })
            {
                if (method.IsGenericMethod || method.DeclaringType?.ContainsGenericParameters == true)
                    throw new InvalidOperationException("Watering metadata does not patch shared generic methods.");
                installed.Add(method);
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(NativeCollectionObservation), prefix),
                    postfix: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.WateringMetadataPostfix)),
                    finalizer: new HarmonyMethod(typeof(NativeCollectionObservation), nameof(NativeCollectionObservation.WateringMetadataFinalizer)));
            }
            nativeWateringMetadataPatchesReady = true;
        }
        catch
        {
            nativeWateringMetadataPatchesFailed = true;
            foreach (var method in installed) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
            throw;
        }
    }

    private sealed partial class NativeCollectionObservation
    {
        // Observation-only prefixes: never skip a native call or replace its arguments/result.
        internal static void WateringUpdatePrefix(Tool __instance, [HarmonyArgument(0)] int direction,
            [HarmonyArgument(1)] int frame, [HarmonyArgument(2)] Farmer who, out NativeWateringToolMetadataCall? __state) =>
            __state = RecordWateringMetadataCall(__instance, who, "update", direction, frame);

        internal static void WateringEndUsingPrefix(Tool __instance, [HarmonyArgument(0)] GameLocation location,
            [HarmonyArgument(1)] Farmer who, out NativeWateringToolMetadataCall? __state) =>
            __state = RecordWateringMetadataCall(__instance, who, "end_using", location: location);

        internal static void WateringInventoryHistoryPrefix(Item __instance, [HarmonyArgument(0)] bool value,
            out NativeWateringToolMetadataCall? __state)
        {
            var scope = current;
            __state = scope is not null && scope.request.ActionOriginWateringResources &&
                __instance is Tool tool && scope.farmer.Items.Contains(tool)
                ? RecordWateringMetadataCall(tool, scope.farmer, "inventory_history", history: value) : null;
        }

        internal static void WateringMetadataPostfix(NativeWateringToolMetadataCall? __state)
        {
            if (__state is not null) __state.Completed = true;
        }

        internal static Exception? WateringMetadataFinalizer(Exception? __exception, NativeWateringToolMetadataCall? __state)
        {
            if (__state is not null && __exception is not null)
            {
                __state.Completed = false;
                current?.Reject("watering_metadata_native_call_failed");
            }
            return __exception;
        }
    }
}
