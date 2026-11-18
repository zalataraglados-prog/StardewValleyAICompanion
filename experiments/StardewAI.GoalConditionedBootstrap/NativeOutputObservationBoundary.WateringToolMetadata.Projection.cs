using System.Globalization;
using System.Xml.Linq;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static (int Sprite, int Swing, bool History) ReplayWateringToolCalls(
        WateringToolMetadataContext context, string itemReferenceId, string runtimeType, string qualifiedItemId,
        int initialSprite, int currentSprite, int swingTicker, bool hasBeenInInventory)
    {
        if (!context.CallsByRef.TryGetValue(itemReferenceId, out var calls))
            return (currentSprite, swingTicker, hasBeenInInventory);
        var spriteKind = ResolveWateringToolSpriteKind(runtimeType);
        foreach (var call in calls)
        {
            switch (call.Kind)
            {
                case "update":
                    currentSprite = NativeToolUpdateMetadata.ApplySpriteUpdate(
                        initialSprite, currentSprite, call.Direction!.Value, call.FarmerMotionFrame!.Value,
                        call.FacingDirection, spriteKind, qualifiedItemId);
                    break;
                case "end_using":
                    swingTicker = unchecked(swingTicker + 1);
                    break;
                case "inventory_history":
                    hasBeenInInventory = call.InventoryHistoryValue!.Value;
                    break;
            }
        }
        return (currentSprite, swingTicker, hasBeenInInventory);
    }

    private static NativeToolSpriteKind ResolveWateringToolSpriteKind(string runtimeType) => runtimeType switch
    {
        WateringCanRuntimeType => NativeToolSpriteKind.WateringCan,
        FishingRodRuntimeType => NativeToolSpriteKind.FishingRod,
        _ => NativeToolSpriteKind.Other
    };

    private static int ParseToolXmlInt(XElement root, string name)
    {
        var raw = ItemXmlValue(root, name);
        Require(int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value),
            "invalid integer scalar in tool XML field: " + name);
        return value;
    }

    private static bool ParseToolXmlBool(XElement root, string name) => ItemXmlValue(root, name) switch
    {
        "true" => true,
        "false" => false,
        _ => throw new InvalidDataException("Invalid boolean scalar in tool XML field: " + name)
    };

    private sealed class VerifiedWateringToolMetadata(WateringToolMetadataContext context, int expectedWaterLeft)
        : IVerifiedPickupToolMetadata
    {
        public XElement ProjectExpected(NativeObservedInventoryPayloadSlot slot, XElement original)
        {
            Require(slot is not null && original is not null && !slot.IsEmpty && slot.IsTool.HasValue,
                "watering tool metadata projection requires a non-empty classified slot and original XML");
            if (!slot!.IsTool!.Value)
            {
                Require(!context.AfterBySlot.ContainsKey(slot.SlotIndex) &&
                        (slot.ItemReferenceId is null || !context.AfterByRef.ContainsKey(slot.ItemReferenceId)),
                    "non-tool slot collides with a verified watering metadata tool");
                return original!;
            }
            Require(context.AfterBySlot.TryGetValue(slot.SlotIndex, out var afterTool) &&
                    slot.ItemReferenceId == afterTool.ItemReferenceId &&
                    slot.RuntimeType == afterTool.RuntimeType &&
                    slot.QualifiedItemId == afterTool.QualifiedItemId,
                "watering tool metadata projection left its verified tool slot or identity");
            var initLower = ParseToolXmlInt(original!, "initialParentTileIndex");
            var initUpper = ParseToolXmlInt(original!, "InitialParentTileIndex");
            var current = ParseToolXmlInt(original!, "currentParentTileIndex");
            var swing = ParseToolXmlInt(original!, "swingTicker");
            var history = ParseToolXmlBool(original!, "hasBeenInInventory");
            Require(initLower == initUpper && initLower == afterTool!.InitialParentTileIndex,
                "watering tool original XML initial tile index disagrees with verified endpoint");
            var replayed = ReplayWateringToolCalls(
                context, afterTool!.ItemReferenceId, afterTool.RuntimeType, afterTool.QualifiedItemId,
                initLower, current, swing, history);
            Require(replayed.Sprite == afterTool.CurrentParentTileIndex &&
                    replayed.Swing == afterTool.SwingTicker &&
                    replayed.History == afterTool.HasBeenInInventory,
                "replayed watering tool metadata from original XML disagrees with observed after frame");
            var expected = new XElement(original!);
            expected.Element("currentParentTileIndex")!.Value = replayed.Sprite.ToString(CultureInfo.InvariantCulture);
            expected.Element("swingTicker")!.Value = replayed.Swing.ToString(CultureInfo.InvariantCulture);
            expected.Element("hasBeenInInventory")!.Value = replayed.History ? "true" : "false";
            if (afterTool.RuntimeType == WateringCanRuntimeType)
            {
                var originalWater = ParseToolXmlInt(expected, "WaterLeft");
                if (slot.SlotIndex == context.PrimarySlotIndex && slot.ItemReferenceId == context.PrimaryItemReferenceId)
                {
                    Require(afterTool.WaterLeft == expectedWaterLeft, "selected watering can after-frame water mismatch");
                    expected.Element("WaterLeft")!.Value = expectedWaterLeft.ToString(CultureInfo.InvariantCulture);
                }
                else
                    Require(originalWater == afterTool.WaterLeft, "unrelated watering can water level changed");
            }
            else
                Require(!expected.Elements("WaterLeft").Any(), "non-watering-can XML contains WaterLeft element");
            return expected;
        }
    }
}
