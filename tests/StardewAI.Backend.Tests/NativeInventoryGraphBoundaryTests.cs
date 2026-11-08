using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryGraphBoundaryTests
{
    [Fact]
    public void ReusesOriginalScopeAndDeclaresOnlyTheExplicitGraphSubset()
    {
        var (request, receipt) = Fixture();
        var value = Read(request, receipt);
        Assert.Equal(2, value.Before.NodeOccurrences); Assert.Equal(2, value.Before.EdgeOccurrences);
        Assert.True(value.InventoryGraphSubsetVerified); Assert.True(value.Transients.ObjectSubtypeStateVerified);
        Assert.False(value.FullTransientStateVerified); Assert.False(value.NativeOutcomeVerified);
        Assert.False(value.ResourceCreditAuthorized); Assert.False(value.FormalProductTrainingAuthorized);
        request.ActionOriginInventoryGraph = false;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void SharedChildRetainsOneStateButEverySerializedOccurrenceAndNativeNullEdge()
    {
        var (request, receipt) = Fixture();
        var child = AddAliasedChild(receipt.InventoryPayloadBefore!);
        var value = Read(request, receipt);
        Assert.Equal(3, value.Before.Nodes.Count); Assert.Equal(4, value.Before.NodeOccurrences);
        Assert.Equal(4, value.Before.Edges.Count); Assert.Equal(6, value.Before.EdgeOccurrences);
        var node = value.Before.Nodes.Single(node => node.ReferenceId == child.ReferenceId);
        Assert.Equal(0, node.Header!.Stack); Assert.Equal(-5, node.Header.Quality);
        Assert.Equal(-3, node.Item!.Object!.Health);
        child.ItemState!.Object!.Health = 10;
        Assert.Equal(-3, node.Item.Object.Health);
    }

    [Fact]
    public void AnotherInventoryRootMayAlsoBeAnInlineChildWithoutDuplicatingItsRawRecord()
    {
        var (request, receipt) = Fixture(); var endpoint = receipt.InventoryPayloadBefore!;
        var tool = endpoint.GraphState!.Nodes![1];
        endpoint.GraphState.Edges![1].Target = Reference(tool); endpoint.GraphState.NodeOccurrences = 3;
        var input = XElement.Parse(endpoint.Slots![1].SerializedStateXml!);
        input.Name = "lastInputItem"; input.SetAttributeValue(Xsi + "type", "Axe");
        EditXml(endpoint.Slots[0], xml => xml.Add(input));
        Assert.Equal(2, Read(request, receipt).Before.Nodes.Count);
        endpoint.GraphState.Edges[0].Target = Reference(tool); endpoint.GraphState.Edges[1].Target = Reference(null);
        EditXml(endpoint.Slots[0], xml => xml.Element("lastInputItem")!.Name = "heldObject");
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnchantmentMembershipKeepsAliasesNullsAndRawAppliedWithoutInferringOwnership(bool membershipOnly)
    {
        var (request, receipt) = Fixture();
        var enchantment = AddEnchantment(receipt.InventoryPayloadBefore!, membershipOnly);
        var value = Read(request, receipt);
        Assert.Equal(new[] { enchantment.ReferenceId, null, enchantment.ReferenceId }, value.Before.ActorEnchantments);
        Assert.False(value.Before.Nodes.Last().Enchantment!.Applied);
        Assert.Equal(-3, value.Before.Nodes.Last().Enchantment!.Level);
        Assert.Equal(membershipOnly ? 0 : 2, value.Before.XmlEnchantmentOccurrences);
    }

    [Theory]
    [InlineData("missing-graph")]
    [InlineData("actor")]
    [InlineData("missing-node")]
    [InlineData("duplicate-node")]
    [InlineData("extra-node")]
    [InlineData("node-counter")]
    [InlineData("edge-counter")]
    [InlineData("edge-ordinal")]
    [InlineData("edge-target")]
    [InlineData("root-state")]
    [InlineData("root-header")]
    [InlineData("wrong-count")]
    public void RejectsUnavailableOrFabricatedGraphIdentityAndCompleteness(string mutation)
    {
        var (request, receipt) = Fixture();
        var endpoint = receipt.InventoryPayloadBefore!; var graph = endpoint.GraphState!;
        switch (mutation)
        {
            case "missing-graph": endpoint.GraphState = null; break;
            case "actor": graph.ActorReferenceId = Guid.NewGuid().ToString("N"); break;
            case "missing-node": graph.Nodes = [graph.Nodes![0]]; break;
            case "duplicate-node": graph.Nodes![1].ReferenceId = graph.Nodes[0].ReferenceId; break;
            case "extra-node": graph.Nodes = [.. graph.Nodes!, NestedObject()]; graph.NodeOccurrences = 3; break;
            case "node-counter": graph.NodeOccurrences++; break;
            case "edge-counter": graph.EdgeOccurrences++; break;
            case "edge-ordinal": graph.Edges![0].Ordinal = 1; break;
            case "edge-target": graph.Edges![0].Target = new() { IsNull = false, ReferenceId = Guid.NewGuid().ToString("N") }; break;
            case "root-state": graph.Nodes![0].ItemState = endpoint.Slots![0].TransientState; break;
            case "root-header": graph.Nodes![0].Stack++; break;
            case "wrong-count": graph.Nodes![1].AttachmentCount = null; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("raw-state")]
    [InlineData("item-id")]
    [InlineData("missing-null-edge")]
    [InlineData("nil-inline")]
    [InlineData("wrong-type")]
    public void RejectsBrokenNestedRawStateOrNativeXmlBinding(string mutation)
    {
        var (request, receipt) = Fixture(); var endpoint = receipt.InventoryPayloadBefore!;
        var child = AddAliasedChild(endpoint);
        switch (mutation)
        {
            case "raw-state": child.ItemState = null; break;
            case "item-id": child.QualifiedItemId = "(O)391"; break;
            case "missing-null-edge": endpoint.GraphState!.Edges = endpoint.GraphState.Edges![..^1]; break;
            case "nil-inline": EditXml(endpoint.Slots![0], xml => xml.Element("heldObject")!.SetAttributeValue(Xsi + "nil", "true")); break;
            case "wrong-type": EditXml(endpoint.Slots![0], xml => xml.Element("heldObject")!.SetAttributeValue(Xsi + "type", "Axe")); break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("level")]
    [InlineData("unknown-type")]
    [InlineData("nil-in-xml")]
    [InlineData("missing-null")]
    [InlineData("member-item")]
    public void RejectsEnchantmentXmlNullCodecTypeAndMembershipCorruption(string mutation)
    {
        var (request, receipt) = Fixture(); var endpoint = receipt.InventoryPayloadBefore!;
        var enchantment = AddEnchantment(endpoint);
        switch (mutation)
        {
            case "level": enchantment.EnchantmentState!.Level = 1; break;
            case "unknown-type": enchantment.RuntimeType = "StardewValley.Enchantments.UnknownEnchantment"; break;
            case "nil-in-xml": EditXml(endpoint.Slots![1], xml => xml.Add(new XElement("enchantments", new XAttribute(Xsi + "nil", "true")))); break;
            case "missing-null": endpoint.GraphState!.Edges = endpoint.GraphState.Edges!.Where(edge => edge.Role != NativeInventoryGraphRoles.Enchantments || edge.Ordinal != 1).ToArray(); break;
            case "member-item": endpoint.GraphState!.ActorEnchantments![0] = Reference(endpoint.GraphState.Nodes![0]); break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }
}
