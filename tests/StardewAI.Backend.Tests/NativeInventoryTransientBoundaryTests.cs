using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryTransientBoundaryTests
{
    [Fact]
    public void PreservesRawNullEmptyPrimitiveCachePrivateAndPhysicalActorStateWithoutResourceCredit()
    {
        var (request, receipt) = Fixture(true);
        var endpoint = receipt.InventoryPayloadBefore!;
        var item = endpoint.Slots![0].TransientState!;
        item.SetFlagOnPickup = Text(""); item.TempDataPresent = true;
        item.TempDataEntries = [new() { Key = "null", Kind = "null" }, new() { Key = "FromStarterGiftBox", Kind = "bool", BooleanValue = false },
            new() { Key = "__Inventory_ReduceId_Remove", Kind = "string", StringValue = "" }];
        item.Cache!.QualifiedItemId = Text(""); item.Cache.ContextTagsPresent = true; item.Cache.ContextTags = [];
        var tool = endpoint.Slots[1].TransientState!.Tool!;
        tool.LastUser = new() { IsNull = false, ReferenceId = endpoint.SelectionTransientState!.ActorReferenceId, ActorId = "123", IsActor = true };
        var result = Read(request, receipt);
        Assert.Equal("", result.Before.Slots[0]!.SetFlagOnPickup);
        Assert.Equal(3, result.Before.Slots[0]!.TempData!.Count);
        Assert.False(result.Before.Slots[0]!.TempData![1].BooleanValue);
        Assert.Equal("", result.Before.Slots[0]!.TempData![2].StringValue);
        Assert.Empty(result.Before.Slots[0]!.Cache.ContextTags!);
        Assert.Null(result.After.Slots[0]!.TempData);
        Assert.Null(result.After.Slots[0]!.Cache.ContextTags);
        Assert.True(result.Before.Slots[1]!.Tool!.LastUser!.IsActor);
        Assert.False(result.Before.Slots[1]!.Tool!.PlayUseSounds);
        Assert.Equal(-4, result.Before.Slots[1]!.Pickaxe!.BoulderTileX);
        Assert.Equal(int.MaxValue, result.Before.Slots[1]!.Pickaxe!.HitsToBoulder);
        item.TempDataEntries[1].BooleanValue = true; item.Cache.ContextTags = ["replaced"];
        Assert.False(result.Before.Slots[0]!.TempData![1].BooleanValue);
        Assert.Empty(result.Before.Slots[0]!.Cache.ContextTags!);
        Assert.False(result.FullTransientStateVerified);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void PreservesARealNullTagElementDistinctFromAnAbsentOrEmptyCache()
    {
        var (request, receipt) = Fixture();
        var cache = receipt.InventoryPayloadBefore!.Slots![0].TransientState!.Cache!;
        cache.ContextTagsPresent = true; cache.ContextTags = [null, "tag"];
        var actual = Read(request, receipt).Before.Slots[0]!.Cache.ContextTags!;
        Assert.Equal(2, actual.Count); Assert.Null(actual[0]); Assert.Equal("tag", actual[1]);
        cache.ContextTags = [null, null];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void MatchingTextualActorIdDoesNotReplacePhysicalLastUserIdentity()
    {
        var (request, receipt) = Fixture();
        var tool = receipt.InventoryPayloadBefore!.Slots![1].TransientState!.Tool!;
        tool.LastUser = new() { IsNull = false, ReferenceId = Guid.NewGuid().ToString("N"), ActorId = "123", IsActor = false };
        Assert.False(Read(request, receipt).Before.Slots[1]!.Tool!.LastUser!.IsActor);
        tool.LastUser.IsActor = true;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("stowed")]
    [InlineData("temporary")]
    [InlineData("temporary-while-stowed")]
    [InlineData("temporary-non-tool")]
    public void ResolvesActualTemporaryAndStowedPrecedenceRatherThanSelectedToolEquality(string branch)
    {
        var (request, receipt) = Fixture();
        var state = receipt.InventoryPayloadBefore!;
        var selection = state.SelectionTransientState!;
        if (branch == "stowed")
        {
            selection.ItemStowed = true; selection.CurrentItem = new() { IsNull = true }; state.CurrentToolReferenceId = null;
        }
        else
        {
            var slot = state.Slots![branch == "temporary-non-tool" ? 0 : 1];
            selection.TemporaryItem = ItemReference(slot); selection.CurrentItem = ItemReference(slot);
            selection.ItemStowed = branch == "temporary-while-stowed";
            state.SelectedSlotIndex = branch == "temporary-non-tool" ? 1 : 0;
            state.SelectedItemReferenceId = state.Slots[state.SelectedSlotIndex.Value].ItemReferenceId;
            state.CurrentToolReferenceId = slot.IsTool == true ? slot.ItemReferenceId : null;
        }
        Assert.Equal(selection.ItemStowed, Read(request, receipt).Before.Selection.ItemStowed);
    }

    [Fact]
    public void RawTransientTextAndBothEndpointXmlUseOneAggregateBudget()
    {
        var (request, receipt) = Fixture();
        receipt.Collections = []; receipt.InventoryBefore = receipt.InventoryAfter = [new() { QualifiedItemId = "(O)388", Quantity = 6 }];
        var template = receipt.InventoryPayloadBefore!.Slots![0];
        var xml = template.SerializedStateXml!.Replace("<stack>10", "<stack>1").Replace("</Object>", "<!--" + new string('x', 59000) + "--></Object>");
        template.Stack = 1; template.SerializedStateXml = xml;
        template.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant();
        var first = Enumerable.Range(0, 6).Select(index =>
        { var slot = Clone(template); slot.SlotIndex = index; slot.ItemReferenceId = Guid.NewGuid().ToString("N"); return slot; }).ToArray();
        receipt.InventoryPayloadBefore.Slots = first; receipt.InventoryPayloadAfter!.Slots = Clone(first);
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore, receipt.InventoryPayloadAfter })
        {
            endpoint.SelectedSlotIndex = 0; endpoint.SelectedItemReferenceId = endpoint.Slots![0].ItemReferenceId; endpoint.CurrentToolReferenceId = null;
            endpoint.SelectionTransientState!.CurrentItem = ItemReference(endpoint.Slots[0]);
            foreach (var slot in endpoint.Slots)
            {
                slot.TransientState!.TempDataPresent = true;
                slot.TransientState.TempDataEntries = Enumerable.Range(0, 12).Select(index => new ActionOriginTempDataEntry
                    { Key = index.ToString(), Kind = "string", StringValue = new string('x', 4096) }).ToArray();
            }
        }
        Assert.Equal(6, NativeOutputObservationBoundary.ReadInventoryPayload(request, receipt, 99, 111, "Farm", "123").Before.Slots.Count);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore, receipt.InventoryPayloadAfter })
            foreach (var slot in endpoint.Slots!) slot.TransientState!.TempDataEntries = [];
        Assert.Equal(6, Read(request, receipt).Before.Slots.Count);
    }

    [Fact]
    public void OneItemsTransientTotalCannotBorrowOtherSlotsCharacterBudget()
    {
        var (request, receipt) = Fixture();
        var state = receipt.InventoryPayloadBefore!.Slots![0].TransientState!;
        state.TempDataPresent = true;
        state.TempDataEntries = Enumerable.Range(0, 17).Select(index => new ActionOriginTempDataEntry
            { Key = index.ToString(), Kind = "string", StringValue = new string('x', 4096) }).ToArray();
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        foreach (var entry in state.TempDataEntries) entry.StringValue = "";
        Assert.Equal(17, Read(request, receipt).Before.Slots[0]!.TempData!.Count);
    }
}
