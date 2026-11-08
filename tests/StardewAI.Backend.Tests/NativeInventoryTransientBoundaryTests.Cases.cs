using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryTransientBoundaryTests
{
    [Theory]
    [InlineData("request")][InlineData("selection")][InlineData("selection-schema")][InlineData("actor-reference")]
    [InlineData("actor-collision")][InlineData("actor-changed")][InlineData("stowed-missing")][InlineData("temporary-missing")]
    [InlineData("actor-guid-case-collision")]
    [InlineData("current-missing")][InlineData("current-null")][InlineData("stowed-contradiction")][InlineData("empty-state")]
    [InlineData("item-state")][InlineData("item-schema")][InlineData("flag-missing")][InlineData("flag-availability")]
    [InlineData("flag-null-extra")][InlineData("flag-nonnull-missing")][InlineData("text-limit")][InlineData("temp-availability")]
    [InlineData("temp-absent-extra")][InlineData("temp-present-missing")][InlineData("temp-count")][InlineData("temp-key")]
    [InlineData("temp-duplicate")][InlineData("temp-kind")][InlineData("temp-bool")][InlineData("temp-string")]
    [InlineData("temp-null-extra")][InlineData("cache")][InlineData("cache-id")][InlineData("tags-dirty")]
    [InlineData("tags-availability")][InlineData("tags-absent-extra")][InlineData("tags-present-missing")][InlineData("tags-duplicate")]
    [InlineData("tags-count")][InlineData("tags-text")][InlineData("tool")][InlineData("sound")]
    [InlineData("description")][InlineData("display")][InlineData("last-user")][InlineData("last-user-extra")]
    [InlineData("last-user-identity")][InlineData("last-user-relation")][InlineData("object-tool")][InlineData("axe-pickaxe")]
    public void RejectsMissingFabricatedUnboundOrUnsupportedTransientFields(string change)
    {
        var (request, receipt) = Fixture();
        var endpoint = receipt.InventoryPayloadBefore!;
        var selection = endpoint.SelectionTransientState!;
        var item = endpoint.Slots![0].TransientState!;
        var tool = endpoint.Slots[1].TransientState!.Tool!;
        switch (change)
        {
            case "request": request.ActionOriginInventoryTransients = false; break;
            case "selection": endpoint.SelectionTransientState = null; break;
            case "selection-schema": selection.SchemaVersion = "v2"; break;
            case "actor-reference": selection.ActorReferenceId = "123"; break;
            case "actor-collision": selection.ActorReferenceId = endpoint.Slots[0].ItemReferenceId; break;
            case "actor-guid-case-collision": selection.ActorReferenceId = endpoint.Slots[0].ItemReferenceId!.ToUpperInvariant(); break;
            case "actor-changed": receipt.InventoryPayloadAfter!.SelectionTransientState!.ActorReferenceId = Guid.NewGuid().ToString("N"); break;
            case "stowed-missing": selection.ItemStowed = null; break;
            case "temporary-missing": selection.TemporaryItem = null; break;
            case "current-missing": selection.CurrentItem = null; break;
            case "current-null": selection.CurrentItem = new() { IsNull = true }; break;
            case "stowed-contradiction": selection.ItemStowed = true; break;
            case "empty-state": endpoint.Slots[2].TransientState = Clone(item); break;
            case "item-state": endpoint.Slots[0].TransientState = null; break;
            case "item-schema": item.SchemaVersion = "other"; break;
            case "flag-missing": item.SetFlagOnPickup = null; break;
            case "flag-availability": item.SetFlagOnPickup!.IsNull = null; break;
            case "flag-null-extra": item.SetFlagOnPickup!.Value = "extra"; break;
            case "flag-nonnull-missing": item.SetFlagOnPickup!.IsNull = false; break;
            case "text-limit": item.SetFlagOnPickup = Text(new string('x', 4097)); break;
            case "temp-availability": item.TempDataPresent = null; break;
            case "temp-absent-extra": item.TempDataEntries = []; break;
            case "temp-present-missing": item.TempDataPresent = true; break;
            case "temp-count": item.TempDataPresent = true; item.TempDataEntries = Enumerable.Repeat(new ActionOriginTempDataEntry(), 257).ToArray(); break;
            case "temp-key": item.TempDataPresent = true; item.TempDataEntries = [new() { Kind = "null" }]; break;
            case "temp-duplicate": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "null" }, new() { Key = "x", Kind = "null" }]; break;
            case "temp-kind": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "object" }]; break;
            case "temp-bool": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "bool" }]; break;
            case "temp-string": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "string" }]; break;
            case "temp-null-extra": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "null", BooleanValue = false }]; break;
            case "cache": item.Cache = null; break;
            case "cache-id": item.Cache!.QualifiedItemId = null; break;
            case "tags-dirty": item.Cache!.ContextTagsDirty = null; break;
            case "tags-availability": item.Cache!.ContextTagsPresent = null; break;
            case "tags-absent-extra": item.Cache!.ContextTags = []; break;
            case "tags-present-missing": item.Cache!.ContextTagsPresent = true; break;
            case "tags-duplicate": item.Cache!.ContextTagsPresent = true; item.Cache.ContextTags = ["x", "x"]; break;
            case "tags-count": item.Cache!.ContextTagsPresent = true; item.Cache.ContextTags = Enumerable.Repeat("x", 257).ToArray(); break;
            case "tags-text": item.Cache!.ContextTagsPresent = true; item.Cache.ContextTags = [new string('x', 257)]; break;
            case "tool": endpoint.Slots[1].TransientState!.Tool = null; break;
            case "sound": tool.PlayUseSounds = null; break;
            case "description": tool.Description = null; break;
            case "display": tool.DisplayName = null; break;
            case "last-user": tool.LastUser = null; break;
            case "last-user-extra": tool.LastUser!.ActorId = "123"; break;
            case "last-user-identity": tool.LastUser = new() { IsNull = false, ReferenceId = selection.ActorReferenceId, IsActor = true }; break;
            case "last-user-relation": tool.LastUser = new() { IsNull = false, ReferenceId = selection.ActorReferenceId, ActorId = "123", IsActor = false }; break;
            case "object-tool": item.Tool = Clone(tool); break;
            case "axe-pickaxe": endpoint.Slots[1].TransientState!.Pickaxe = new() { BoulderTileX = 0, BoulderTileY = 0, HitsToBoulder = 0 }; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("all")][InlineData("x")][InlineData("y")][InlineData("hits")]
    public void PrivatePickaxeFieldsRequireObservedValuesNotDefaultZeros(string field)
    {
        var (request, receipt) = Fixture(true);
        var state = receipt.InventoryPayloadBefore!.Slots![1].TransientState!;
        switch (field)
        {
            case "all": state.Pickaxe = null; break;
            case "x": state.Pickaxe!.BoulderTileX = null; break;
            case "y": state.Pickaxe!.BoulderTileY = null; break;
            case "hits": state.Pickaxe!.HitsToBoulder = null; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }
}
