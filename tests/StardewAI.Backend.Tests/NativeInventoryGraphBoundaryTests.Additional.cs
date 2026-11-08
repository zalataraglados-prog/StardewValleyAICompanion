using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryGraphBoundaryTests
{
    [Fact]
    public void HatHasAPersistedEnchantmentListEvenThoughItIsNotATool()
    {
        var (request, receipt) = Fixture();
        foreach (var inventory in new[] { receipt.InventoryBefore, receipt.InventoryAfter })
            inventory.Single(row => row.QualifiedItemId == "(T)Axe").QualifiedItemId = "(H)hat";
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            var slot = endpoint.Slots![1]; slot.RuntimeType = "StardewValley.Objects.Hat";
            slot.QualifiedItemId = "(H)hat"; slot.IsTool = false; slot.TransientState = NativeInventoryTransientBoundaryTests.ItemState(false);
            endpoint.CurrentToolReferenceId = null;
            endpoint.SelectionTransientState!.CurrentItem!.RuntimeType = slot.RuntimeType;
            endpoint.SelectionTransientState.CurrentItem.IsTool = false;
            var node = endpoint.GraphState!.Nodes![1];
            node.RuntimeType = slot.RuntimeType; node.QualifiedItemId = slot.QualifiedItemId; node.IsTool = false; node.AttachmentCount = null;
            EditXml(slot, xml => { xml.Name = "Hat"; xml.SetElementValue("itemId", "hat"); xml.Elements("attachments").Remove(); });
            AddEnchantment(endpoint);
        }
        var value = Read(request, receipt);
        Assert.Equal(2, value.Before.XmlEnchantmentOccurrences);
        Assert.False(value.Before.Nodes[1].Header!.IsTool);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullAttachmentPositionsMustRemainExplicitNativeNilElements(bool omit)
    {
        var (request, receipt) = Fixture(); var endpoint = receipt.InventoryPayloadBefore!; var graph = endpoint.GraphState!;
        graph.Nodes![1].AttachmentCount = 1;
        graph.Edges = [.. graph.Edges!, Edge(graph.Nodes[1], NativeInventoryGraphRoles.Attachments, 0, null)];
        graph.EdgeOccurrences = 3;
        EditXml(endpoint.Slots![1], xml => xml.Element("attachments")!.Add(new XElement("Object", new XAttribute(Xsi + "nil", "true"))));
        Assert.Null(Read(request, receipt).Before.Edges.Last().TargetReferenceId);
        EditXml(endpoint.Slots[1], xml =>
        {
            var child = xml.Element("attachments")!.Element("Object")!;
            if (omit) child.Remove(); else child.SetAttributeValue(Xsi + "nil", "false");
        });
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void NativeAncestorCycleCannotBeHiddenBehindFiniteXmlOrNodeDeduplication()
    {
        var (request, receipt) = Fixture(); var endpoint = receipt.InventoryPayloadBefore!; var graph = endpoint.GraphState!;
        graph.Edges![0].Target = Reference(graph.Nodes![0]);
        var child = new XElement("heldObject", new XElement("itemId", "388"), new XElement("quality", 0),
            new XElement("stack", 10), new XElement("bigCraftable", false));
        EditXml(endpoint.Slots![0], xml => xml.Add(child));
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void BothEndpointsShareTheOriginalXmlAndNewRawNodeTextBudget()
    {
        var (request, receipt) = Fixture();
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            var graph = endpoint.GraphState!;
            var enchantments = Enumerable.Range(0, 128).Select(_ => new ActionOriginInventoryGraphNode
            {
                ReferenceId = Guid.NewGuid().ToString("N"), RuntimeType = "StardewValley.Enchantments.BaseEnchantment", Kind = "enchantment",
                EnchantmentState = new() { Applied = false, DisplayName = new() { IsNull = false, Value = new string('x', 4096) }, Level = 0 }
            }).ToArray();
            graph.Nodes = [.. graph.Nodes!, .. enchantments];
            graph.ActorEnchantments = enchantments.Select(Reference).ToArray();
            graph.NodeOccurrences = 130; graph.EdgeOccurrences = 130;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        receipt.InventoryPayloadAfter!.GraphState!.Nodes!.Last().EnchantmentState!.DisplayName!.Value = "";
        Assert.Equal(128, Read(request, receipt).After.ActorEnchantments.Count);
    }
}
