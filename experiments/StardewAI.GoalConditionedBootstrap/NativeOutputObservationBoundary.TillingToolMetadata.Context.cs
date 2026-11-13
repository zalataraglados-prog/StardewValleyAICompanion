using System.Globalization;
using System.Xml.Linq;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static VerifiedTillingToolMetadata VerifyTillingToolMetadata(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, NativeInventoryGraphSummary graph, int slot, string toolId)
    {
        var first = receipt.TillingToolMetadataBefore; var next = receipt.TillingToolMetadataAfter;
        Require(first is not null && next is not null && slot >= 0 && !string.IsNullOrWhiteSpace(toolId),
            "tilling metadata raw endpoints or selected tool are unavailable");
        var payload = graph.Payload;
        var before = payload.Before.Slots.SingleOrDefault(row => row.SlotIndex == slot);
        var after = payload.After.Slots.SingleOrDefault(row => row.SlotIndex == slot);
        Require(before is { IsEmpty: false, RuntimeType: "StardewValley.Tools.Hoe", IsTool: true } &&
            after is { IsEmpty: false, RuntimeType: "StardewValley.Tools.Hoe", IsTool: true } && before.QualifiedItemId == toolId &&
            after.QualifiedItemId == toolId && before.ItemReferenceId == after.ItemReferenceId &&
            payload.Before.Slots.FirstOrDefault(row => row.RuntimeType == "StardewValley.Tools.Hoe")?.SlotIndex == slot &&
            (!request.ToolSlotIndex.HasValue || request.ToolSlotIndex == slot), "tilling metadata changed its original first Hoe slot/reference");
        ValidateTillingContext(first!, graph.Transients.Before.Selection.ActorReferenceId, before!, payload.Before);
        ValidateTillingContext(next!, graph.Transients.After.Selection.ActorReferenceId, after!, payload.After);
        Require(first!.GameTick == receipt.BeforeGameTick && next!.GameTick == receipt.AfterGameTick &&
            first.ActorReferenceId == next.ActorReferenceId && first.HoeReferenceId == next.HoeReferenceId &&
            first.InitialParentTileIndex == next.InitialParentTileIndex && first.ToolPower == 0 && next.ToolPower == 0 &&
            first.UsingTool == false && next.UsingTool == false && first.PauseForSingleAnimation == false &&
            next.PauseForSingleAnimation == false && next.CanReleaseTool == false &&
            next.CurrentToolSlotIndex == slot && payload.After.CurrentToolReferenceId == before!.ItemReferenceId &&
            next.LastEquippedToolIsNull == false && next.LastEquippedToolReferenceId == before.ItemReferenceId &&
            next.LastEquippedToolRuntimeType == "StardewValley.Tools.Hoe",
            "tilling metadata is not one completed original idle-to-idle power-zero Hoe cycle");
        // Farmer.Update detects this exact native reference change, then Equip sets history.
        var equipped = first.LastEquippedToolIsNull == true || first.LastEquippedToolReferenceId != first.HoeReferenceId;
        var sprite = NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(first.InitialParentTileIndex!.Value, next!.FacingDirection!.Value);
        var swing = NativeHoeCompletedCycleMetadata.ExpectedSwingTicker(first.SwingTicker!.Value);
        var history = NativeHoeCompletedCycleMetadata.ExpectedInventoryHistory(first.HasBeenInInventory!.Value, equipped);
        Require(next.CurrentParentTileIndex == sprite && next.SwingTicker == swing && next.HasBeenInInventory == history,
            "tilling metadata native sprite/counter/equip history differs from the completed-cycle projection");
        return new(slot, before!.ItemReferenceId!, sprite, swing, history);
    }

    private static void ValidateTillingContext(NativeTillingToolMetadataContext context, string actor,
        NativeObservedInventoryPayloadSlot tool, NativeObservedInventoryPayloadState payload)
    {
        Require(context.SchemaVersion == "native_tilling_tool_metadata_context.v1" && context.GameTick == payload.GameTick &&
            context.ActorReferenceId == actor && Guid.TryParseExact(actor, "N", out _) && context.ActorReferenceId != context.HoeReferenceId &&
            context.HoeReferenceId == tool.ItemReferenceId && context.HoeSlotIndex == tool.SlotIndex &&
            context.CurrentToolSlotIndex == payload.SelectedSlotIndex && context.FacingDirection is >= 0 and <= 3 &&
            context.ToolPower.HasValue && context.UsingTool.HasValue && context.PauseForSingleAnimation.HasValue &&
            context.CanReleaseTool.HasValue && context.InitialParentTileIndex.HasValue && context.CurrentParentTileIndex.HasValue &&
            context.SwingTicker.HasValue && context.HasBeenInInventory.HasValue && context.LastEquippedToolIsNull.HasValue,
            "tilling metadata raw frame/actor/tool/selection or typed inputs differ");
        if (context.LastEquippedToolIsNull == true)
            Require(context.LastEquippedToolReferenceId is null && context.LastEquippedToolRuntimeType is null,
                "tilling metadata null equipped tool contains a fabricated identity");
        else
        {
            Require(Guid.TryParseExact(context.LastEquippedToolReferenceId, "N", out _) && context.LastEquippedToolReferenceId != actor &&
                !string.IsNullOrWhiteSpace(context.LastEquippedToolRuntimeType) &&
                NativeInventoryPayloadTypes.TryGet(context.LastEquippedToolRuntimeType!, out var descriptor) && descriptor.IsTool,
                "tilling metadata equipped reference/type is unavailable or not native");
            var bound = payload.Slots.FirstOrDefault(row => row.ItemReferenceId == context.LastEquippedToolReferenceId);
            Require(bound is null || bound.RuntimeType == context.LastEquippedToolRuntimeType,
                "tilling metadata equipped reference changed its known inventory type");
        }
        var characters = 0; var xml = ReadItemXml(tool.Xml, tool.Sha256, ref characters);
        Require(XmlInt(xml, "initialParentTileIndex") == context.InitialParentTileIndex &&
            XmlInt(xml, "InitialParentTileIndex") == context.InitialParentTileIndex &&
            XmlInt(xml, "currentParentTileIndex") == context.CurrentParentTileIndex && XmlInt(xml, "swingTicker") == context.SwingTicker &&
            bool.TryParse(ItemXmlValue(xml, "hasBeenInInventory"), out var history) && history == context.HasBeenInInventory,
            "tilling metadata raw native values differ from the original Hoe XML");
    }

    private static int XmlInt(XElement root, string name) => int.TryParse(ItemXmlValue(root, name), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var value) ? value : throw new InvalidDataException("Tilling metadata XML integer is invalid: " + name);
}
