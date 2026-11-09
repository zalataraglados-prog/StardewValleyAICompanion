using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeDebrisStateBoundaryTests
{
    [Fact]
    public void PreservesRawBitsNullsLatentUidAndOrderedAliasedEventsWithoutCredit()
    {
        var (request, receipt) = Fixture();
        var summary = Read(request, receipt); var value = summary.Before.Single();
        Assert.Equal(long.MinValue, value.Identity.TargetUid); Assert.False(value.Identity.TargetDefined);
        Assert.Null(value.Visual.Message); Assert.Equal("", value.Visual.SpriteSheetName);
        var chunk = value.Chunks.Single();
        Assert.Equal(int.MinValue, chunk.XVelocityBits); Assert.Equal(0x7fc12345, chunk.YVelocityBits);
        Assert.Equal(-19, chunk.SinkTimer); Assert.False(chunk.Position.Paused);
        Assert.False(chunk.Position.Incoming[0].Argument); Assert.Equal(uint.MaxValue, chunk.Position.Incoming[0].Timestamp);
        Assert.True(chunk.Position.Incoming[1].Argument); Assert.Equal(0u, chunk.Position.Incoming[1].Timestamp);
        Assert.Equal(chunk.Position.Incoming[1], chunk.Position.Outgoing.Single());
        receipt.BeforeDebris[0].RawState!.Chunks![0].Position!.Incoming![0].Argument = true;
        Assert.False(chunk.Position.Incoming[0].Argument);
        Assert.True(summary.DebrisStateSubsetVerified); Assert.False(summary.FullTransientStateVerified);
        Assert.False(summary.NativeOutcomeVerified); Assert.False(summary.ResourceCreditAuthorized);
        Assert.False(summary.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void CallbackStateSharesTheOriginalFrameChainAndAggregateDomain()
    {
        var (request, receipt) = Fixture();
        var raw = receipt.Collections[0].DebrisBefore!.RawState!;
        raw.Chunks![0].Position!.Incoming = [new() { ReferenceId = Id(), Argument = false, Timestamp = 19 }];
        raw.EventOccurrences = 1;
        Assert.Equal(1, Read(request, receipt).Collections[0].State.EventOccurrences);
        receipt.Collections[0].DebrisBefore!.RawState = null;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void EmptyEndpointsAreOnlyStructuralAndDefaultLegacyRequestsRemainIndependent()
    {
        var (request, receipt) = Fixture();
        receipt.BeforeDebris = receipt.AfterDebris = [];
        Assert.Empty(Read(request, receipt).Before);
        request.ActionOriginDebrisState = false;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        NativeOutputObservationBoundary.ReadInventoryGraph(request, receipt, 99, 111, "Farm", "123");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(18)] [InlineData(19)] [InlineData(20)] [InlineData(21)] [InlineData(22)] [InlineData(23)]
    public void MissingOrConflictingRawEvidenceIsRejectedWithoutRepair(int mutation)
    {
        var (request, receipt) = Fixture(); var row = receipt.BeforeDebris[0]; var raw = row.RawState!;
        _ = Read(request, receipt); // Invalid baselines must never make every negative case spuriously green.
        var chunk = raw.Chunks![0]; var position = chunk.Position!;
        switch (mutation)
        {
            case 0: row.RawState = null; break;
            case 1: raw.SchemaVersion = "unknown"; break;
            case 2: raw.Identity = null; break;
            case 3: raw.Motion = null; break;
            case 4: raw.Visual = null; break;
            case 5: raw.Chunks = null; break;
            case 6: raw.EventOccurrences = null; break;
            case 7: raw.EventOccurrences = 2; break;
            case 8: chunk.ChunkId = Id(); break;
            case 9: raw.Chunks = []; break;
            case 10: raw.Chunks[0] = null!; break;
            case 11: chunk.Position = null; break;
            case 12: chunk.SinkTimer = null; break;
            case 13: raw.Identity!.TargetUid = null; break;
            case 14: raw.Identity!.TargetDefined = null; break;
            case 15: raw.Visual!.Message = null; break;
            case 16: raw.Identity!.ItemId!.Value = "other"; break;
            case 17: position.Paused = null; break;
            case 18: position.Incoming = null; break;
            case 19: position.PauseHandlerCount = 257; break;
            case 20: position.Outgoing![0].ReferenceId = position.Incoming![0].ReferenceId; break;
            case 21: position.ReferenceId = receipt.InventoryPayloadBefore!.SelectionTransientState!.ActorReferenceId; break;
            case 22: raw.Identity!.Hover!.RuntimeType = "StardewValley.Farmer"; break;
            case 23: position.Outgoing![0].Timestamp = null; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }
}
