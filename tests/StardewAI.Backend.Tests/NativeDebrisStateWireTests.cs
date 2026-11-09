using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class NativeDebrisStateWireTests
{
    [Fact]
    public void NewStateIsOmittedFromDefaultLegacyWire()
    {
        Assert.DoesNotContain("action_origin_debris_state", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        Assert.DoesNotContain("raw_state", JsonSerializer.Serialize(new ActionOriginObservedDebris()));
        Assert.Contains("\"action_origin_debris_state\":true", JsonSerializer.Serialize(new TrainingExecutionRequest { ActionOriginDebrisState = true }));
    }

    [Fact]
    public void RawBitsFalseAndLatentTargetUidAreNotNormalizedThroughJson()
    {
        var raw = new ActionOriginDebrisRawState
        {
            Identity = new() { TargetDefined = false, TargetUid = long.MinValue },
            Chunks = [new() { Position = new() { XBits = unchecked((int)0x80000000), YBits = 0x7fc12345, Paused = false } }]
        };
        var copy = JsonSerializer.Deserialize<ActionOriginDebrisRawState>(JsonSerializer.Serialize(raw))!;
        Assert.Equal(false, copy.Identity!.TargetDefined);
        Assert.Equal(long.MinValue, copy.Identity.TargetUid);
        Assert.Equal(unchecked((int)0x80000000), copy.Chunks![0].Position!.XBits);
        Assert.Equal(0x7fc12345, copy.Chunks[0].Position!.YBits);
        Assert.Equal(false, copy.Chunks[0].Position!.Paused);
        Assert.Null(copy.Chunks[0].SinkTimer);
        Assert.Null(copy.Motion);
    }

    [Fact]
    public void NativeCharacterAndDebrisDomainsAreExactRatherThanNamespaceAdmission()
    {
        Assert.True(NativeObservedCharacterTypes.IsSupported("StardewValley.Farmer"));
        Assert.True(NativeObservedCharacterTypes.IsSupported("StardewValley.NPC"));
        Assert.False(NativeObservedCharacterTypes.IsSupported("StardewValley.InventedCharacter"));
        Assert.False(NativeObservedCharacterTypes.IsSupported("Foreign.Farmer"));
        Assert.Equal("ARCHAEOLOGY", NativeDebrisObservationDomain.DebrisTypeName(3));
        Assert.Null(NativeDebrisObservationDomain.DebrisTypeName(2));
    }
}
