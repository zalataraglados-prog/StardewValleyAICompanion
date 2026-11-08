using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeInventoryPayloadBoundaryTests
{
    [Fact]
    public void RetainsFullSlotPositionsRawXmlReferenceAndSelectionWithoutClaimingTransientProof()
    {
        var (request, receipt) = Fixture();
        var result = Read(request, receipt);
        Assert.Equal(3, result.Before.Slots.Count);
        Assert.True(result.Before.Slots[2].IsEmpty);
        Assert.Equal(1, result.Before.SelectedSlotIndex);
        Assert.Equal(result.Before.Slots[1].ItemReferenceId, result.Before.CurrentToolReferenceId);
        Assert.Contains("<modData>", result.Before.Slots[0].Xml);
        Assert.NotEqual(result.Before.Slots[0].Xml, result.After.Slots[0].Xml);
        receipt.InventoryPayloadBefore!.Slots![0].SerializedStateXml = "replaced";
        Assert.Contains("<modData>", result.Before.Slots[0].Xml);
        Assert.False(result.FullTransientStateVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("stowed")]
    [InlineData("temporary-tool")]
    [InlineData("non-tool-selected")]
    public void RetainsNativeCurrentToolGetterExceptionsWithoutClaimingSelectionResolution(string mode)
    {
        var (request, receipt) = Fixture();
        var state = receipt.InventoryPayloadBefore!;
        if (mode == "stowed") state.CurrentToolReferenceId = null;
        else if (mode == "non-tool-selected")
        {
            state.SelectedSlotIndex = 0;
            state.SelectedItemReferenceId = state.Slots![0].ItemReferenceId;
        }
        else
        {
            var pick = Slot(2, "StardewValley.Tools.Pickaxe", "(T)Pickaxe", 1, true);
            state.Slots![2] = pick;
            receipt.InventoryPayloadAfter!.Slots![2] = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(pick))!;
            receipt.InventoryBefore = [.. receipt.InventoryBefore, new() { QualifiedItemId = "(T)Pickaxe", Quantity = 1 }];
            receipt.InventoryAfter = [.. receipt.InventoryAfter, new() { QualifiedItemId = "(T)Pickaxe", Quantity = 1 }];
            state.CurrentToolReferenceId = state.Slots[2].ItemReferenceId;
        }
        var result = Read(request, receipt);
        Assert.Equal(state.CurrentToolReferenceId, result.Before.CurrentToolReferenceId);
        Assert.Equal(state.SelectedItemReferenceId, result.Before.SelectedItemReferenceId);
        Assert.False(result.FullTransientStateVerified);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("tool-mode")]
    [InlineData("anchor")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("schema")]
    [InlineData("coverage")]
    [InlineData("actor")]
    [InlineData("location")]
    [InlineData("tick")]
    [InlineData("selection-missing")]
    [InlineData("selection-index")]
    [InlineData("selection-reference")]
    [InlineData("current-tool")]
    [InlineData("missing-slots")]
    [InlineData("slot-limit")]
    [InlineData("slot-index")]
    [InlineData("slot-availability")]
    [InlineData("empty-xml")]
    [InlineData("missing-ref")]
    [InlineData("alias-ref")]
    [InlineData("changed-type")]
    [InlineData("custom-type")]
    [InlineData("missing-quality")]
    [InlineData("missing-stack")]
    [InlineData("missing-tool-flag")]
    [InlineData("wrong-tool-flag")]
    [InlineData("quantity-denominator")]
    [InlineData("xml-hash")]
    [InlineData("xml-root")]
    [InlineData("xml-id")]
    [InlineData("xml-quality")]
    [InlineData("xml-stack")]
    [InlineData("xml-duplicate")]
    [InlineData("xml-dtd")]
    [InlineData("xml-depth")]
    [InlineData("xml-nodes")]
    [InlineData("xml-size")]
    public void RejectsMissingUnboundUnsafeOrTruncatedSlotWitnesses(string change)
    {
        var (request, receipt) = Fixture();
        var state = receipt.InventoryPayloadBefore!;
        var slot = state.Slots![0];
        switch (change)
        {
            case "request": request.ActionOriginInventoryPayload = false; break;
            case "tool-mode": request.ActionOriginToolInputs = false; break;
            case "anchor": request.ActionOriginSnapshotAnchor = false; break;
            case "before": receipt.InventoryPayloadBefore = null; break;
            case "after": receipt.InventoryPayloadAfter = null; break;
            case "schema": state.SchemaVersion = "v2"; break;
            case "coverage": state.PayloadKind = "complete_world"; break;
            case "actor": state.ActorId = "other"; break;
            case "location": state.LocationId = "Town"; break;
            case "tick": state.GameTick = 101; break;
            case "selection-missing": state.SelectionObserved = null; break;
            case "selection-index": state.SelectedSlotIndex = 3; break;
            case "selection-reference": state.SelectedItemReferenceId = slot.ItemReferenceId; break;
            case "current-tool": state.CurrentToolReferenceId = slot.ItemReferenceId; break;
            case "missing-slots": state.Slots = null; break;
            case "slot-limit": state.Slots = Enumerable.Repeat(slot, 513).ToArray(); break;
            case "slot-index": slot.SlotIndex = 1; break;
            case "slot-availability": slot.IsEmpty = null; break;
            case "empty-xml": state.Slots[2].SerializedStateXml = "invented"; break;
            case "missing-ref": slot.ItemReferenceId = null; break;
            case "alias-ref": state.Slots[1].ItemReferenceId = slot.ItemReferenceId; break;
            case "changed-type": receipt.InventoryPayloadAfter!.Slots![0].RuntimeType = "StardewValley.Tools.Axe"; break;
            case "custom-type": slot.RuntimeType = "Mod.Object"; break;
            case "missing-quality": slot.Quality = null; break;
            case "missing-stack": slot.Stack = null; break;
            case "missing-tool-flag": slot.IsTool = null; break;
            case "wrong-tool-flag": slot.IsTool = true; break;
            case "quantity-denominator": slot.Stack = 9; SetXml(slot, slot.SerializedStateXml!.Replace("<stack>10", "<stack>9")); break;
            case "xml-hash": slot.SerializedStateSha256 = new string('0', 64); break;
            case "xml-root": SetXml(slot, slot.SerializedStateXml!.Replace("Object>", "Axe>")); break;
            case "xml-id": SetXml(slot, slot.SerializedStateXml!.Replace("<itemId>388", "<itemId>390")); break;
            case "xml-quality": SetXml(slot, slot.SerializedStateXml!.Replace("<quality>0", "<quality>1")); break;
            case "xml-stack": SetXml(slot, slot.SerializedStateXml!.Replace("<stack>10", "<stack>9")); break;
            case "xml-duplicate": SetXml(slot, slot.SerializedStateXml!.Replace("</Object>", "<stack>10</stack></Object>")); break;
            case "xml-dtd": SetXml(slot, "<!DOCTYPE Object [<!ENTITY x '388'>]>" + slot.SerializedStateXml); break;
            case "xml-depth": SetXml(slot, slot.SerializedStateXml!.Replace("</Object>",
                string.Concat(Enumerable.Repeat("<n>", 65)) + string.Concat(Enumerable.Repeat("</n>", 65)) + "</Object>")); break;
            case "xml-nodes": SetXml(slot, slot.SerializedStateXml!.Replace("</Object>", string.Concat(Enumerable.Repeat("<n/>", 8193)) + "</Object>")); break;
            case "xml-size": SetXml(slot, new string('x', 65537)); break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void EndpointsAndOldAdmissionPayloadShareOneAggregateBudget()
    {
        var (request, receipt) = Fixture();
        receipt.Collections = [];
        receipt.InventoryBefore = receipt.InventoryAfter = [new() { QualifiedItemId = "(O)388", Quantity = 5 }];
        var large = Slot(0, "StardewValley.Object", "(O)388", 1, false);
        SetXml(large, large.SerializedStateXml!.Replace("</Object>", "<!--" + new string('x', 59000) + "--></Object>"));
        receipt.BeforeDebris = Enumerable.Range(0, 8).Select(_ => new ActionOriginObservedDebris
        {
            DebrisId = Guid.NewGuid().ToString("N"), RuntimeType = "StardewValley.Debris", DebrisType = "OBJECT",
            RawItemId = "(O)388", HeldItemStack = 1,
            HeldItem = new() { RuntimeType = large.RuntimeType!, QualifiedItemId = large.QualifiedItemId!, Quality = 0,
                SerializedStateXml = large.SerializedStateXml!, SerializedStateSha256 = large.SerializedStateSha256! }
        }).ToArray();
        receipt.AfterDebris = [];
        foreach (var state in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            state.Slots = Enumerable.Range(0, 5).Select(index =>
            {
                var slot = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(large))!;
                slot.SlotIndex = index; slot.ItemReferenceId = Guid.NewGuid().ToString("N"); return slot;
            }).ToArray();
            state.SelectedSlotIndex = -1; state.SelectedItemReferenceId = state.CurrentToolReferenceId = null;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        receipt.BeforeDebris = [];
        Assert.Equal(5, Read(request, receipt).Before.Slots.Count);
    }

    [Theory]
    [InlineData("StardewValley.Tools.FabricatedTool", "(T)Axe", true)]
    [InlineData("StardewValley.Objects.Chest", "(BC)Axe", false)]
    [InlineData("StardewValley.Objects.Trinket", "(TR)Axe", false)]
    [InlineData("StardewValley.Tools.Axe", "(O)Axe", true)]
    [InlineData("StardewValley.Tools.MeleeWeapon", "(T)Axe", true)]
    [InlineData("StardewValley.Objects.ColoredObject", "(T)Axe", false)]
    public void RejectsSelfConsistentButUnsupportedNativeTypeOrQualifier(string type, string id, bool tool)
    {
        var (request, receipt) = IdentityFixture(type, id, tool, "<bigCraftable>false</bigCraftable>");
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("StardewValley.Objects.ColoredObject", "(BC)", false, "<bigCraftable>true</bigCraftable>")]
    [InlineData("StardewValley.Objects.Boots", "(B)", false, "")]
    [InlineData("StardewValley.Objects.Hat", "(H)", false, "")]
    [InlineData("StardewValley.Objects.Ring", "(O)", false, "")]
    [InlineData("StardewValley.Objects.CombinedRing", "(O)", false, "")]
    [InlineData("StardewValley.Objects.SpecialItem", "(O)", false, "<which>99</which>")]
    [InlineData("StardewValley.Tools.MeleeWeapon", "(W)", true, "")]
    [InlineData("StardewValley.Tools.Slingshot", "(W)", true, "")]
    [InlineData("StardewValley.Objects.Clothing", "(P)", false, "<clothesType>PANTS</clothesType>")]
    [InlineData("StardewValley.Objects.Clothing", "(S)", false, "<clothesType>SHIRT</clothesType>")]
    [InlineData("StardewValley.Objects.Clothing", "(S)", false, "<clothesType>2</clothesType>")]
    public void UsesNativeIdentityRuleRatherThanUniversalObjectOrToolQualifier(string type, string prefix, bool tool, string discriminator)
    {
        var (request, receipt) = IdentityFixture(type, prefix + "sample", tool, discriminator);
        Assert.Equal(prefix + "sample", Read(request, receipt).Before.Slots[1].QualifiedItemId);
        foreach (var state in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
            state.Slots![1].QualifiedItemId = "(invented)sample";
        foreach (var inventory in new[] { receipt.InventoryBefore, receipt.InventoryAfter })
            inventory.Single(row => row.QualifiedItemId == prefix + "sample").QualifiedItemId = "(invented)sample";
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<clothesType><string>PANTS</string></clothesType>")]
    [InlineData("<clothesType><string>SKIRT</string></clothesType>")]
    [InlineData("<clothesType><string>PANTS</string><string>SHIRT</string></clothesType>")]
    [InlineData("<clothesType><string>PANTS</string></clothesType><clothesType><string>PANTS</string></clothesType>")]
    public void RejectsMissingOrFabricatedClothingEnumDiscriminator(string discriminator)
    {
        var (request, receipt) = IdentityFixture("StardewValley.Objects.Clothing", "(P)sample", false, discriminator);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) IdentityFixture(
        string type, string id, bool tool, string discriminator)
    {
        var (request, receipt) = Fixture();
        foreach (var state in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            var previous = state.Slots![1];
            var slot = Slot(1, type, id, 1, tool);
            slot.ItemReferenceId = previous.ItemReferenceId;
            SetXml(slot, slot.SerializedStateXml!.Replace("<bigCraftable>false</bigCraftable>", discriminator));
            state.Slots[1] = slot;
            state.SelectedItemReferenceId = slot.ItemReferenceId;
            state.CurrentToolReferenceId = tool ? slot.ItemReferenceId : null;
        }
        foreach (var inventory in new[] { receipt.InventoryBefore, receipt.InventoryAfter })
            inventory.Single(row => row.QualifiedItemId == "(T)Axe").QualifiedItemId = id;
        return (request, receipt);
    }

    private static NativeInventoryPayloadSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.ReadInventoryPayload(request, receipt, 99, 111, "Farm", "123");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        request.ActionOriginInventoryPayload = request.ActionOriginToolInputs = request.ActionOriginSnapshotAnchor = true;
        receipt.InventoryBefore = [.. receipt.InventoryBefore, new() { QualifiedItemId = "(T)Axe", Quantity = 1 }];
        receipt.InventoryAfter = [.. receipt.InventoryAfter, new() { QualifiedItemId = "(T)Axe", Quantity = 1 }];
        var item = Slot(0, "StardewValley.Object", "(O)388", 10, false);
        var tool = Slot(1, "StardewValley.Tools.Axe", "(T)Axe", 1, true);
        receipt.InventoryPayloadBefore = State(100, [item, tool, new() { SlotIndex = 2, IsEmpty = true }]);
        receipt.InventoryPayloadAfter = JsonSerializer.Deserialize<ActionOriginInventoryPayload>(JsonSerializer.Serialize(receipt.InventoryPayloadBefore))!;
        receipt.InventoryPayloadAfter.GameTick = 110;
        var after = receipt.InventoryPayloadAfter.Slots![0]; after.Stack = 16;
        SetXml(after, after.SerializedStateXml!.Replace("<stack>10", "<stack>16"));
        return (request, receipt);
    }

    private static ActionOriginInventoryPayload State(long tick, ActionOriginInventoryPayloadSlot[] slots) => new()
    {
        SchemaVersion = "native_inventory_payload.v1", PayloadKind = "persisted_xml", ActorId = "123", LocationId = "Farm",
        GameTick = tick, SelectionObserved = true, SelectedSlotIndex = 1, SelectedItemReferenceId = slots[1].ItemReferenceId,
        CurrentToolReferenceId = slots[1].ItemReferenceId, Slots = slots
    };

    private static ActionOriginInventoryPayloadSlot Slot(int index, string type, string id, int stack, bool tool)
    {
        var slot = new ActionOriginInventoryPayloadSlot { SlotIndex = index, IsEmpty = false, ItemReferenceId = Guid.NewGuid().ToString("N"),
            RuntimeType = type, QualifiedItemId = id, IsTool = tool, Quality = 0, Stack = stack };
        var root = type[(type.LastIndexOf('.') + 1)..];
        SetXml(slot, $"<{root}><itemId>{id[(id.IndexOf(')') + 1)..]}</itemId><quality>0</quality><stack>{stack}</stack>" +
            "<bigCraftable>false</bigCraftable><modData><item><key><string>fixture</string></key>" +
            $"<value><string>retained</string></value></item></modData></{root}>");
        return slot;
    }

    private static void SetXml(ActionOriginInventoryPayloadSlot slot, string xml)
    {
        slot.SerializedStateXml = xml;
        slot.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant();
    }
}
