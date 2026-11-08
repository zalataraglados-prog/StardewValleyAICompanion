using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Reflection;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryTransientBoundaryTests
{
    [Theory]
    [InlineData(false, false, 0, 0)]
    [InlineData(true, false, -1, -42)]
    [InlineData(false, true, int.MinValue, int.MaxValue)]
    public void ObjectProofPreservesActualFlagsSignedCountersAndDetachedValues(bool held, bool light, int timer, int health)
    {
        var (request, receipt) = ObjectFixture();
        var state = receipt.InventoryPayloadBefore!.Slots![0].TransientState!.Object!;
        state.HasHeldObject = held; state.HasLightSource = light; state.ShakeTimer = timer; state.Health = health;
        var result = ReadObjects(request, receipt);
        Assert.Equal(new NativeObservedObjectTransientState(held, light, timer, health), result.Before.Slots[0]!.Object);
        Assert.True(result.ObjectSubtypeStateVerified);
        Assert.Equal(2, result.ObservedObjectOccurrences);
        state.Health = 10;
        Assert.Equal(health, result.Before.Slots[0]!.Object!.Health);
        Assert.False(result.FullTransientStateVerified);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("before", "container")]
    [InlineData("after", "container")]
    [InlineData("before", "held")]
    [InlineData("after", "light")]
    [InlineData("before", "timer")]
    [InlineData("after", "health")]
    public void RequiredObjectProofRejectsUnavailableContainerAndEachField(string end, string missing)
    {
        var (request, receipt) = ObjectFixture();
        var slot = (end == "before" ? receipt.InventoryPayloadBefore : receipt.InventoryPayloadAfter)!.Slots![0];
        switch (missing)
        {
            case "container": slot.TransientState!.Object = null; break;
            case "held": slot.TransientState!.Object!.HasHeldObject = null; break;
            case "light": slot.TransientState!.Object!.HasLightSource = null; break;
            case "timer": slot.TransientState!.Object!.ShakeTimer = null; break;
            case "health": slot.TransientState!.Object!.Health = null; break;
        }
        Assert.Throws<InvalidDataException>(() => ReadObjects(request, receipt));
        if (missing != "container") Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void OldCommonRootEvidenceDoesNotAcquireObjectSubtypeCredit()
    {
        var (request, receipt) = Fixture();
        var legacy = Read(request, receipt);
        Assert.False(legacy.ObjectSubtypeStateVerified);
        Assert.Equal(0, legacy.ObservedObjectOccurrences);
        Assert.Throws<InvalidDataException>(() => ReadObjects(request, receipt));
    }

    [Fact]
    public void RetainedClientsCanStillResolveTheOriginalSixArgumentReader()
    {
        var signature = new[] { typeof(TrainingExecutionRequest), typeof(ActionOriginObservationReceipt),
            typeof(long), typeof(long), typeof(string), typeof(string) };
        var method = typeof(NativeOutputObservationBoundary).GetMethod("ReadInventoryTransients",
            BindingFlags.Public | BindingFlags.Static, null, signature, null);
        Assert.NotNull(method);
        var (request, receipt) = Fixture();
        var result = (NativeInventoryTransientSummary)method.Invoke(null, new object[] { request, receipt, 99L, 111L, "Farm", "123" })!;
        Assert.False(result.ObjectSubtypeStateVerified);
        Assert.Equal(0, result.ObservedObjectOccurrences);
    }

    [Fact]
    public void RejectsObjectFieldsOnToolOrEmptySlotWithoutChangingLegacyAdmission()
    {
        var (request, receipt) = ObjectFixture();
        var slots = receipt.InventoryPayloadBefore!.Slots!;
        slots[1].TransientState!.Object = new() { HasHeldObject = false, HasLightSource = false, ShakeTimer = 0, Health = 0 };
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        slots[1].TransientState!.Object = null;
        var empty = slots.Single(slot => slot.IsEmpty == true);
        empty.TransientState = Clone(slots[0].TransientState);
        Assert.Throws<InvalidDataException>(() => ReadObjects(request, receipt));
    }

    [Fact]
    public void ObjectWireRoundTripRetainsObservedFalseZeroAndOmittedLegacyField()
    {
        var raw = new ActionOriginObjectTransientState { HasHeldObject = false, HasLightSource = false, ShakeTimer = 0, Health = 0 };
        var decoded = JsonSerializer.Deserialize<ActionOriginObjectTransientState>(JsonSerializer.Serialize(raw))!;
        Assert.False(decoded.HasHeldObject); Assert.False(decoded.HasLightSource);
        Assert.Equal(0, decoded.ShakeTimer); Assert.Equal(0, decoded.Health);
        Assert.DoesNotContain("\"object\"", JsonSerializer.Serialize(ItemState(false)));
    }

    [Fact]
    public void ColoredObjectUsesTheSameCatalogSubtypeAndRawCacheFields()
    {
        var (request, receipt) = ObjectFixture();
        foreach (var endpoint in new[] { receipt.InventoryPayloadBefore!, receipt.InventoryPayloadAfter! })
        {
            var slot = endpoint.Slots![0];
            slot.RuntimeType = "StardewValley.Objects.ColoredObject";
            var xml = XDocument.Parse(slot.SerializedStateXml!);
            xml.Root!.Name = "ColoredObject";
            slot.SerializedStateXml = xml.ToString(SaveOptions.DisableFormatting);
            slot.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slot.SerializedStateXml))).ToLowerInvariant();
            slot.TransientState!.Object!.HasHeldObject = true;
            slot.TransientState.Object.HasLightSource = true;
        }
        Assert.True(ReadObjects(request, receipt).Before.Slots[0]!.Object!.HasHeldObject);
        Assert.True(ReadObjects(request, receipt).After.Slots[0]!.Object!.HasLightSource);
    }

    private static NativeInventoryTransientSummary ReadObjects(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.ReadInventoryTransients(request, receipt, 99, 111, "Farm", "123", requireObjectState: true);

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) ObjectFixture()
    {
        var fixture = Fixture();
        foreach (var endpoint in new[] { fixture.Receipt.InventoryPayloadBefore!, fixture.Receipt.InventoryPayloadAfter! })
            endpoint.Slots![0].TransientState!.Object = new() { HasHeldObject = false, HasLightSource = false, ShakeTimer = 0, Health = 0 };
        return fixture;
    }
}
