using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryGraphBoundaryTests
{
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static NativeInventoryGraphSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.ReadInventoryGraph(request, receipt, 99, 111, "Farm", "123");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture()
    {
        var fixture = NativeInventoryTransientBoundaryTests.Fixture();
        fixture.Request.ActionOriginInventoryGraph = true;
        foreach (var endpoint in new[] { fixture.Receipt.InventoryPayloadBefore!, fixture.Receipt.InventoryPayloadAfter! })
        {
            endpoint.Slots![0].TransientState!.Object = ObjectState();
            EditXml(endpoint.Slots[1], xml => xml.Add(new XElement("attachments")));
            var nodes = endpoint.Slots.Where(slot => slot.IsEmpty == false).Select(slot => new ActionOriginInventoryGraphNode
            {
                ReferenceId = slot.ItemReferenceId, RuntimeType = slot.RuntimeType, Kind = "item", QualifiedItemId = slot.QualifiedItemId,
                IsTool = slot.IsTool, Quality = slot.Quality, Stack = slot.Stack,
                AttachmentCount = slot.IsTool == true ? 0 : null, EnchantmentCount = slot.IsTool == true ? 0 : null
            }).ToArray();
            endpoint.GraphState = new()
            {
                SchemaVersion = "native_inventory_graph.v1", ActorReferenceId = endpoint.SelectionTransientState!.ActorReferenceId,
                Nodes = nodes, Edges = [Edge(nodes[0], NativeInventoryGraphRoles.HeldObject, 0, null),
                    Edge(nodes[0], NativeInventoryGraphRoles.LastInputItem, 0, null)],
                ActorEnchantments = [], NodeOccurrences = 2, EdgeOccurrences = 2
            };
        }
        return fixture;
    }

    private static ActionOriginObjectTransientState ObjectState() =>
        new() { HasHeldObject = false, HasLightSource = true, ShakeTimer = -2, Health = -3 };

    private static ActionOriginInventoryGraphNode NestedObject()
    {
        var state = NativeInventoryTransientBoundaryTests.ItemState(false); state.Object = ObjectState();
        return new()
        {
            ReferenceId = Guid.NewGuid().ToString("N"), RuntimeType = "StardewValley.Object", Kind = "item",
            QualifiedItemId = "(O)390", IsTool = false, Quality = -5, Stack = 0, ItemState = state
        };
    }

    private static ActionOriginGraphReference Reference(ActionOriginInventoryGraphNode? node) =>
        new() { IsNull = node is null, ReferenceId = node?.ReferenceId };
    private static ActionOriginInventoryGraphEdge Edge(ActionOriginInventoryGraphNode parent, string role, int ordinal,
        ActionOriginInventoryGraphNode? target) => new() { ParentReferenceId = parent.ReferenceId, Role = role, Ordinal = ordinal, Target = Reference(target) };

    private static XElement ChildXml(string name) => new(name, new XElement("itemId", "390"), new XElement("quality", -5),
        new XElement("stack", 0), new XElement("bigCraftable", false));
    private static void EditXml(ActionOriginInventoryPayloadSlot slot, Action<XElement> edit)
    {
        var xml = XElement.Parse(slot.SerializedStateXml!); edit(xml);
        slot.SerializedStateXml = xml.ToString(SaveOptions.DisableFormatting);
        slot.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slot.SerializedStateXml))).ToLowerInvariant();
    }

    private static ActionOriginInventoryGraphNode AddAliasedChild(ActionOriginInventoryPayload endpoint)
    {
        var graph = endpoint.GraphState!;
        var child = NestedObject();
        graph.Nodes = [.. graph.Nodes!, child];
        graph.Edges![0].Target = Reference(child); graph.Edges[1].Target = Reference(child);
        graph.Edges = [.. graph.Edges, Edge(child, NativeInventoryGraphRoles.HeldObject, 0, null),
            Edge(child, NativeInventoryGraphRoles.LastInputItem, 0, null)];
        graph.NodeOccurrences = 4; graph.EdgeOccurrences = 6;
        EditXml(endpoint.Slots![0], xml =>
        {
            var input = ChildXml("lastInputItem"); input.SetAttributeValue(Xsi + "type", "Object");
            xml.Add(ChildXml("heldObject"), input);
        });
        return child;
    }

    private static ActionOriginInventoryGraphNode AddEnchantment(ActionOriginInventoryPayload endpoint, bool membershipOnly = false)
    {
        var graph = endpoint.GraphState!;
        var enchantment = new ActionOriginInventoryGraphNode
        {
            ReferenceId = Guid.NewGuid().ToString("N"), RuntimeType = "StardewValley.Enchantments.BaseEnchantment", Kind = "enchantment",
            EnchantmentState = new() { Applied = false, DisplayName = new() { IsNull = false, Value = "" }, Level = -3 }
        };
        graph.Nodes = [.. graph.Nodes!, enchantment];
        graph.ActorEnchantments = [Reference(enchantment), Reference(null), Reference(enchantment)];
        graph.NodeOccurrences = membershipOnly ? 4 : 6;
        graph.EdgeOccurrences = membershipOnly ? 5 : 8;
        if (!membershipOnly)
        {
            var tool = graph.Nodes[1]; tool.EnchantmentCount = 3;
            graph.Edges = [.. graph.Edges!, Edge(tool, NativeInventoryGraphRoles.Enchantments, 0, enchantment),
                Edge(tool, NativeInventoryGraphRoles.Enchantments, 1, null), Edge(tool, NativeInventoryGraphRoles.Enchantments, 2, enchantment)];
            EditXml(endpoint.Slots![1], xml => xml.Add(new XElement("enchantments", new XElement("level", -3)),
                new XElement("enchantments", new XElement("level", -3))));
        }
        return enchantment;
    }
}
