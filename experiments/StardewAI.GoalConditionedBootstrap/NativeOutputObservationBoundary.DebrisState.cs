using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    public static NativeDebrisStateSummary ReadDebrisState(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, string actorId)
    {
        Require(request.ActionOriginDebrisState && request.ActionOriginInventoryGraph,
            "native debris state was not explicitly requested with the original graph scope");
        var characters = 0;
        var original = ReadInventoryGraphWithBudget(request, receipt, earliestTick, latestTick, locationId, actorId,
            ref characters, out var references);
        var eventOccurrences = 0;
        var before = ReadDebrisRawRows(receipt.BeforeDebris, references, ref characters, ref eventOccurrences);
        var after = ReadDebrisRawRows(receipt.AfterDebris, references, ref characters, ref eventOccurrences);
        var collections = new List<NativeObservedCollectedDebrisState>();
        foreach (var collection in receipt.Collections)
            collections.Add(new(collection.Ordinal, ReadDebrisRawState(collection.DebrisBefore!, references,
                ref characters, ref eventOccurrences)));
        return new(original.ScopeId, original.CaptureSessionId, original, before, after, collections.AsReadOnly());
    }

    private static IReadOnlyList<NativeObservedDebrisRawState> ReadDebrisRawRows(ActionOriginObservedDebris[] rows,
        Dictionary<Guid, string> references, ref int characters, ref int events)
    {
        var result = new List<NativeObservedDebrisRawState>(rows.Length);
        foreach (var row in rows) result.Add(ReadDebrisRawState(row, references, ref characters, ref events));
        return result.AsReadOnly();
    }

    private static NativeObservedDebrisRawState ReadDebrisRawState(ActionOriginObservedDebris row,
        Dictionary<Guid, string> references, ref int characters, ref int events)
    {
        var raw = row.RawState;
        Require(raw is not null && raw.SchemaVersion == "native_debris_state.v1" && raw.Identity is not null &&
            raw.Motion is not null && raw.Visual is not null && raw.Chunks is not null && raw.EventOccurrences is >= 0,
            "native debris state schema or scoped fields are unavailable");
        BindTransientReference(row.DebrisId, "StardewValley.Debris", references);
        var identity = ReadDebrisIdentity(raw!.Identity!, row, references, ref characters);
        var motion = raw.Motion!; var visual = raw.Visual!;
        var parsedMotion = new NativeObservedDebrisMotionState(
            DebrisScalar(motion.RelativeXBits), DebrisScalar(motion.FinalYLevel), DebrisScalar(motion.FinalYTarget),
            DebrisScalar(motion.TimeSinceDoneBouncingBits), DebrisScalar(motion.ScaleBits), DebrisScalar(motion.MoveTowardsPlayer),
            DebrisScalar(motion.MovingUp), DebrisScalar(motion.FloppingFish), DebrisScalar(motion.IsFishable),
            DebrisScalar(motion.MovingFinalYLevel), DebrisScalar(motion.IsSinking), DebrisScalar(motion.ReturnToDropperTimer));
        var parsedVisual = new NativeObservedDebrisVisualState(DebrisScalar(visual.SourceRectSize),
            ReadTransientText(visual.Message, ref characters), DebrisScalar(visual.NonSpriteColor), DebrisScalar(visual.ChunksColor),
            DebrisScalar(visual.AnimationTimerBits), ReadTransientText(visual.SpriteSheetName, ref characters), DebrisScalar(visual.TextureCached));
        Require(raw.Chunks!.Length == row.ChunkIds.Length, "raw chunks omit or fabricate original logical chunk positions");
        var startEvents = events;
        var chunks = new List<NativeObservedRawChunkState>(raw.Chunks.Length);
        var aliases = new DebrisRawAliases();
        for (var i = 0; i < raw.Chunks.Length; i++)
            chunks.Add(ReadRawChunk(raw.Chunks[i], row.ChunkIds[i], references, aliases, ref events));
        Require(raw.EventOccurrences == events - startEvents, "native pause-event occurrence count differs");
        return new(row.DebrisId, identity, parsedMotion, parsedVisual, chunks.AsReadOnly(), events - startEvents);
    }

    private static NativeObservedDebrisIdentityState ReadDebrisIdentity(ActionOriginDebrisIdentityState raw,
        ActionOriginObservedDebris row, Dictionary<Guid, string> references, ref int characters)
    {
        var kind = DebrisScalar(raw.DebrisType); var chunk = DebrisScalar(raw.ChunkType);
        var quality = DebrisScalar(raw.ItemQuality); var itemId = ReadTransientText(raw.ItemId, ref characters);
        Require(NativeDebrisObservationDomain.DebrisTypeName(kind) == row.DebrisType && chunk == row.ChunkType &&
            quality == row.ItemQuality && (itemId ?? "") == row.RawItemId, "raw debris identity differs from original fields");
        var held = ReadTransientItemReference(raw.HeldItem, references);
        Require((held is null) == (row.HeldItem is null) && (held is null || held.RuntimeType == row.HeldItem!.RuntimeType),
            "raw held-item reference differs from original held payload availability/type");
        Require(raw.Hover?.IsNull is not null, "hover reference availability is missing");
        NativeObservedCharacterReference? hover = null;
        if (raw.Hover!.IsNull!.Value)
            Require(raw.Hover.ReferenceId is null && raw.Hover.RuntimeType is null, "null hover contains fabricated identity");
        else
        {
            Require(NativeObservedCharacterTypes.IsSupported(raw.Hover.RuntimeType), "hover type is outside native Character domain");
            BindTransientReference(raw.Hover.ReferenceId, raw.Hover.RuntimeType!, references);
            hover = new(raw.Hover.ReferenceId!, raw.Hover.RuntimeType!);
        }
        return new(kind, chunk, itemId, quality, held, DebrisScalar(raw.DroppedByPlayerId),
            DebrisScalar(raw.TargetDefined), DebrisScalar(raw.TargetUid), hover);
    }

    private static T DebrisScalar<T>(T? value) where T : struct
    {
        Require(value.HasValue, "raw debris/chunk scalar availability is missing");
        return value!.Value;
    }
}
