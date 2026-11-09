using System.Reflection;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Network;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeLiveDebrisCapture
{
    private static IReadOnlyList<NativeLiveChunkState> CaptureChunks(Debris debris, int maximumChunks, ref int remainingEvents)
    {
        var source = Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetObjectShrinkList<Chunk>>(DebrisChunks, debris));
        var array = Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetArray<Chunk, NetRef<Chunk>>>(ChunkArray, source));
        var fields = List(NativeLiveTransientFieldCodec.ReadNullable<List<NetRef<Chunk>>>(ChunkElements, array));
        // Bound storage separately from the caller's remaining LOGICAL chunks. Native shrink
        // iteration skips holes, so holes are not fabricated chunks or aggregate charges.
        if (fields.Count > NativeDebrisObservationDomain.MaximumChunks)
            throw new InvalidDataException("Native debris chunk backing storage exceeds its bounded domain.");
        var slots = new List<(NetRef<Chunk> Field, Chunk? Chunk)>(fields.Count);
        foreach (var field in fields) slots.Add((Wrapper(field), Wrapper(field).Value));
        var result = new List<NativeLiveChunkState>();
        foreach (var slot in slots)
        {
            if (slot.Chunk is null) continue;
            if (result.Count >= maximumChunks) throw new InvalidDataException("Native debris logical chunk budget exceeded.");
            result.Add(CaptureChunk(slot.Chunk, ref remainingEvents));
        }
        if (fields.Count != slots.Count || !ReferenceEquals(NativeLiveTransientFieldCodec.ReadNullable<NetObjectShrinkList<Chunk>>(DebrisChunks, debris), source) ||
            !ReferenceEquals(NativeLiveTransientFieldCodec.ReadNullable<NetArray<Chunk, NetRef<Chunk>>>(ChunkArray, source), array) ||
            !ReferenceEquals(NativeLiveTransientFieldCodec.ReadNullable<List<NetRef<Chunk>>>(ChunkElements, array), fields))
            throw new InvalidDataException("Native debris chunk containers changed during capture.");
        for (int index = 0; index < slots.Count; index++)
            if (!ReferenceEquals(fields[index], slots[index].Field) || !ReferenceEquals(slots[index].Field.Value, slots[index].Chunk))
                throw new InvalidDataException("Native debris logical chunk references changed during capture.");
        return result.AsReadOnly();
    }

    private static NativeLiveChunkState CaptureChunk(Chunk chunk, ref int remainingEvents)
    {
        Native(chunk, typeof(Chunk));
        Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetFields>(ChunkNetFields, chunk));
        var position = CapturePosition(Wrapper(chunk.position), ref remainingEvents);
        return new(chunk, position, Bits(Wrapper(chunk.xVelocity).Value), Bits(Wrapper(chunk.yVelocity).Value),
            Wrapper(chunk.hasPassedRestingLineOnce).Value, chunk.bounces, Bits(chunk.bob), Wrapper(chunk.sinkTimer).Value,
            Wrapper(chunk.netDebrisType).Value, chunk.hitWall, Wrapper(chunk.xSpriteSheet).Value, Wrapper(chunk.ySpriteSheet).Value,
            Bits(chunk.rotation), Bits(chunk.rotationVelocity), Bits(Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetFloat>(ChunkScale, chunk)).Value),
            Bits(Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetFloat>(ChunkAlpha, chunk)).Value));
    }

    private static NativeLiveChunkPositionState CapturePosition(NetPosition position, ref int remainingEvents)
    {
        Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetFields>(PositionNetFields, position));
        var vector = Wrapper(position.Field).Value; // NEVER NetPosition.Value/Get/Paused/X/Y.
        var pause = Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetEvent1Field<bool, NetBool>>(PositionPauseEvent, position));
        var incoming = CaptureQueue(pause, Incoming, ref remainingEvents);
        var outgoing = CaptureQueue(pause, Outgoing, ref remainingEvents);
        return new(position, Bits(vector.X), Bits(vector.Y), NativeLiveTransientFieldCodec.ReadValue<bool>(PositionPaused, position),
            Wrapper(position.moving).Value, position.ExtrapolationEnabled, pause.InterpolationWait, incoming, outgoing,
            CountHandlers(pause, PauseHandlers), CountHandlers(position, ChangeHandlers), CountHandlers(position, VisibleHandlers));
    }

    private static IReadOnlyList<NativeLivePauseEventState> CaptureQueue(NetEvent1Field<bool, NetBool> pause, FieldInfo field, ref int remainingEvents)
    {
        var source = List(NativeLiveTransientFieldCodec.ReadNullable<List<AbstractNetEvent1<bool>.EventRecording>>(field, pause));
        if (source.Count > NativeDebrisObservationDomain.MaximumQueueEntries || source.Count > remainingEvents)
            throw new InvalidDataException("Native pause event queue or aggregate position budget exceeded.");
        var result = new List<NativeLivePauseEventState>(source.Count);
        foreach (var recording in source)
        {
            if (result.Count >= NativeDebrisObservationDomain.MaximumQueueEntries || remainingEvents <= 0)
                throw new InvalidDataException("Native pause event queue or aggregate position budget exceeded.");
            Wrapper(recording);
            result.Add(new(recording, recording.arg, recording.timestamp));
            remainingEvents--;
        }
        if (source.Count != result.Count || !ReferenceEquals(NativeLiveTransientFieldCodec.ReadNullable<List<AbstractNetEvent1<bool>.EventRecording>>(field, pause), source))
            throw new InvalidDataException("Native pause event queue changed during capture.");
        for (int index = 0; index < result.Count; index++)
            if (!ReferenceEquals(source[index], result[index].Reference) || source[index].arg != result[index].Argument || source[index].timestamp != result[index].Timestamp)
                throw new InvalidDataException("Native pause event positions changed during capture.");
        return result.AsReadOnly();
    }

    private static int CountHandlers(object source, FieldInfo field)
    {
        var handler = NativeLiveTransientFieldCodec.ReadNullable<Delegate>(field, source);
        int count = handler?.GetInvocationList().Length ?? 0; // BCL metadata only; never invoke a delegate.
        if (count > NativeDebrisObservationDomain.MaximumHandlers)
            throw new InvalidDataException("Native debris delegate invocation-list count exceeds its bound.");
        return count;
    }
}
