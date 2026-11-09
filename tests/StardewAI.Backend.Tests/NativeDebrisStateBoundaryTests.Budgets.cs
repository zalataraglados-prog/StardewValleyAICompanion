using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeDebrisStateBoundaryTests
{
    private static void FillQueues(ActionOriginObservedDebris row, int chunks, int queueLength)
    {
        var prototype = row.RawState!.Chunks![0];
        var sharedEvent = new ActionOriginPauseEventState { ReferenceId = Id(), Argument = false, Timestamp = uint.MaxValue };
        row.RawState.Chunks = Enumerable.Range(0, chunks).Select(_ =>
        {
            var value = Copy(prototype); value.ChunkId = Id(); value.Position!.ReferenceId = Id();
            value.Position.Incoming = Enumerable.Range(0, queueLength).Select(_ => Copy(sharedEvent)).ToArray();
            value.Position.Outgoing = Enumerable.Range(0, queueLength).Select(_ => Copy(sharedEvent)).ToArray();
            return value;
        }).ToArray();
        row.ChunkIds = row.RawState.Chunks.Select(chunk => chunk.ChunkId!).ToArray();
        row.RawState.EventOccurrences = chunks * queueLength * 2;
    }

    [Fact]
    public void CountsAliasedQueuePositionsAcrossBothEndpointsWithoutUniqueDiscount()
    {
        var (request, receipt) = Fixture();
        FillQueues(receipt.BeforeDebris[0], 5, 256);
        FillQueues(receipt.AfterDebris[0], 3, 256);
        var value = Read(request, receipt);
        Assert.Equal(4096, value.Before[0].EventOccurrences + value.After[0].EventOccurrences);
        FillQueues(receipt.AfterDebris[0], 4, 256);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void QueueLocalLimitIsIndependentOfTheScopeRemainingBudget()
    {
        var (request, receipt) = Fixture(); FillQueues(receipt.BeforeDebris[0], 1, 257);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void SharedPositionMustAgreeWithinOneCaptureButMayChangeAcrossEndpoints()
    {
        var (request, receipt) = Fixture(); var row = receipt.BeforeDebris[0];
        FillQueues(row, 2, 2);
        row.RawState!.Chunks![1].Position = Copy(row.RawState.Chunks[0].Position);
        var value = Read(request, receipt).Before[0];
        Assert.Same(value.Chunks[0].Position, value.Chunks[1].Position);
        row.RawState.Chunks[1].Position!.XBits = 17;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Fact]
    public void DuplicateChunkIdentityRemainsRejectedByTheOriginalReceiptBoundary()
    {
        var (request, receipt) = Fixture(); var row = receipt.BeforeDebris[0];
        FillQueues(row, 2, 2);
        var value = Read(request, receipt).Before[0];
        Assert.Equal(8, value.EventOccurrences);
        row.RawState!.Chunks![1] = Copy(row.RawState.Chunks[0]);
        row.ChunkIds[1] = row.ChunkIds[0];
        row.RawState.Chunks[1].XVelocityBits = 17;
        var error = Assert.Throws<InvalidDataException>(() => Read(request, receipt));
        Assert.Contains("chunk identity repeats", error.Message);
    }

    [Fact]
    public void WorldTextSharesOriginalInventoryXmlAndBothEndpointBudget()
    {
        var (request, receipt) = Fixture();
        ActionOriginObservedDebris TextRow()
        {
            var row = Row(); row.ChunkIds = []; row.RawState!.Chunks = []; row.RawState.EventOccurrences = 0;
            row.RawState.Visual!.Message = new() { IsNull = false, Value = new string('m', 4096) };
            row.RawState.Visual.SpriteSheetName = new() { IsNull = false, Value = new string('s', 4096) };
            return row;
        }
        receipt.BeforeDebris = Enumerable.Range(0, 64).Select(_ => TextRow()).ToArray();
        receipt.AfterDebris = Enumerable.Range(0, 63).Select(_ => TextRow()).ToArray();
        Assert.Equal(64, Read(request, receipt).Before.Count);
        receipt.AfterDebris = [.. receipt.AfterDebris, TextRow()];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }
}
