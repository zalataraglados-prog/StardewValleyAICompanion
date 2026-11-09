using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private sealed class DebrisRawAliases
    {
        internal readonly Dictionary<Guid, NativeObservedChunkPositionState> Positions = new();
        internal readonly Dictionary<Guid, NativeObservedPauseEventState> Events = new();
    }

    private static NativeObservedRawChunkState ReadRawChunk(ActionOriginRawChunkState? wire, string expectedId,
        Dictionary<Guid, string> references, DebrisRawAliases aliases, ref int events)
    {
        Require(wire is not null && wire.ChunkId == expectedId, "raw chunk identity/order differs from original logical list");
        BindTransientReference(wire!.ChunkId, "StardewValley.Chunk", references);
        return new(wire.ChunkId!, ReadRawPosition(wire.Position, references, aliases, ref events),
            DebrisScalar(wire.XVelocityBits), DebrisScalar(wire.YVelocityBits), DebrisScalar(wire.PassedRestingLine),
            DebrisScalar(wire.Bounces), DebrisScalar(wire.BobBits), DebrisScalar(wire.SinkTimer),
            DebrisScalar(wire.RandomOffset), DebrisScalar(wire.HitWall), DebrisScalar(wire.XSpriteSheet),
            DebrisScalar(wire.YSpriteSheet), DebrisScalar(wire.RotationBits), DebrisScalar(wire.RotationVelocityBits),
            DebrisScalar(wire.ScaleBits), DebrisScalar(wire.AlphaBits));
    }

    private static NativeObservedChunkPositionState ReadRawPosition(ActionOriginChunkPositionState? raw,
        Dictionary<Guid, string> references, DebrisRawAliases aliases, ref int events)
    {
        Require(raw is not null, "raw native position is unavailable");
        BindTransientReference(raw!.ReferenceId, "StardewValley.Network.NetPosition", references);
        var incoming = ReadRawPauseEvents(raw.Incoming, references, aliases, ref events);
        var outgoing = ReadRawPauseEvents(raw.Outgoing, references, aliases, ref events);
        var id = GraphId(raw.ReferenceId);
        var result = new NativeObservedChunkPositionState(id.ToString("N"), DebrisScalar(raw.XBits), DebrisScalar(raw.YBits),
            DebrisScalar(raw.Paused), DebrisScalar(raw.Moving), DebrisScalar(raw.ExtrapolationEnabled),
            DebrisScalar(raw.PauseInterpolationWait), incoming, outgoing, ReadHandlerCount(raw.PauseHandlerCount),
            ReadHandlerCount(raw.ChangeHandlerCount), ReadHandlerCount(raw.VisibleChangeHandlerCount));
        if (aliases.Positions.TryGetValue(id, out var previous))
        {
            var previousScalars = previous with { Incoming = incoming, Outgoing = outgoing };
            Require(previousScalars == result && previous.Incoming.SequenceEqual(incoming) && previous.Outgoing.SequenceEqual(outgoing),
                "same native position reference has conflicting raw state within one debris capture");
            return previous;
        }
        aliases.Positions.Add(id, result);
        return result;
    }

    private static IReadOnlyList<NativeObservedPauseEventState> ReadRawPauseEvents(ActionOriginPauseEventState[]? raw,
        Dictionary<Guid, string> references, DebrisRawAliases aliases, ref int events)
    {
        Require(raw is { Length: <= NativeDebrisObservationDomain.MaximumQueueEntries } &&
            raw.Length <= NativeDebrisObservationDomain.MaximumEventOccurrences - events,
            "pause-event queue availability or aggregate occurrence limit differs");
        events += raw!.Length;
        var result = new List<NativeObservedPauseEventState>(raw.Length);
        foreach (var entry in raw)
        {
            Require(entry is not null, "pause-event position is unavailable");
            BindTransientReference(entry!.ReferenceId, NativeDebrisObservationDomain.PauseEventKind, references);
            var id = GraphId(entry.ReferenceId);
            var state = new NativeObservedPauseEventState(id.ToString("N"), DebrisScalar(entry.Argument), DebrisScalar(entry.Timestamp));
            if (aliases.Events.TryGetValue(id, out var previous))
                Require(previous == state, "same pause-event reference has conflicting argument/timestamp within a capture");
            else aliases.Events.Add(id, state);
            result.Add(state);
        }
        return result.AsReadOnly();
    }

    private static int ReadHandlerCount(int? raw)
    {
        Require(raw is >= 0 and <= NativeDebrisObservationDomain.MaximumHandlers, "native delegate count is unavailable or unbounded");
        return raw!.Value;
    }
}
