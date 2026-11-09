using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private int remainingDebrisEvents = NativeDebrisObservationDomain.MaximumEventOccurrences;

        private NativeLiveDebrisState? CaptureDebrisRawState(Debris debris)
        {
            if (!request.ActionOriginDebrisState) return null;
            if (Environment.CurrentManagedThreadId != threadId)
                throw new InvalidOperationException("Raw debris capture left its original native thread.");
            var state = NativeLiveDebrisCapture.Capture(debris, remainingPayloadCharacters, remainingChunkIds, remainingDebrisEvents);
            remainingPayloadCharacters -= state.Characters;
            remainingDebrisEvents -= state.EventOccurrences;
            return state;
        }

        private ActionOriginDebrisRawState ReadDebrisRawPayload(NativeLiveDebrisState state)
        {
            var identity = state.Identity; var motion = state.Motion; var visual = state.Visual;
            return new()
            {
                SchemaVersion = "native_debris_state.v1", EventOccurrences = state.EventOccurrences,
                Identity = new()
                {
                    DebrisType = identity.DebrisType, ChunkType = identity.ChunkType,
                    ItemId = ObservedText(identity.ItemId), ItemQuality = identity.ItemQuality,
                    HeldItem = ObservedItemReference(identity.HeldItem), DroppedByPlayerId = identity.DroppedByPlayerId,
                    TargetDefined = identity.TargetDefined, TargetUid = identity.TargetUid,
                    Hover = ObservedCharacterReference(identity.Hover)
                },
                Motion = new()
                {
                    RelativeXBits = motion.RelativeXBits, FinalYLevel = motion.FinalYLevel, FinalYTarget = motion.FinalYTarget,
                    TimeSinceDoneBouncingBits = motion.TimeSinceDoneBouncingBits, ScaleBits = motion.ScaleBits,
                    MoveTowardsPlayer = motion.MoveTowardsPlayer, MovingUp = motion.MovingUp, FloppingFish = motion.FloppingFish,
                    IsFishable = motion.IsFishable, MovingFinalYLevel = motion.MovingFinalYLevel,
                    IsSinking = motion.IsSinking, ReturnToDropperTimer = motion.ReturnToDropperTimer
                },
                Visual = new()
                {
                    SourceRectSize = visual.SourceRectSize, Message = ObservedText(visual.Message),
                    NonSpriteColor = visual.NonSpriteColor, ChunksColor = visual.ChunksColor,
                    AnimationTimerBits = visual.AnimationTimerBits, SpriteSheetName = ObservedText(visual.SpriteSheetName),
                    TextureCached = visual.TextureCached
                },
                Chunks = state.Chunks.Select(chunk => new ActionOriginRawChunkState
                {
                    ChunkId = ReferenceIds.For(chunk.Reference), Position = ReadChunkPositionPayload(chunk.Position),
                    XVelocityBits = chunk.XVelocityBits, YVelocityBits = chunk.YVelocityBits,
                    PassedRestingLine = chunk.PassedRestingLine, Bounces = chunk.Bounces, BobBits = chunk.BobBits,
                    SinkTimer = chunk.SinkTimer, RandomOffset = chunk.RandomOffset, HitWall = chunk.HitWall,
                    XSpriteSheet = chunk.XSpriteSheet, YSpriteSheet = chunk.YSpriteSheet,
                    RotationBits = chunk.RotationBits, RotationVelocityBits = chunk.RotationVelocityBits,
                    ScaleBits = chunk.ScaleBits, AlphaBits = chunk.AlphaBits
                }).ToArray()
            };
        }

        private static ActionOriginChunkPositionState ReadChunkPositionPayload(NativeLiveChunkPositionState state) => new()
        {
            ReferenceId = ReferenceIds.For(state.Reference), XBits = state.XBits, YBits = state.YBits,
            Paused = state.Paused, Moving = state.Moving, ExtrapolationEnabled = state.ExtrapolationEnabled,
            PauseInterpolationWait = state.PauseInterpolationWait, PauseHandlerCount = state.PauseHandlerCount,
            ChangeHandlerCount = state.ChangeHandlerCount, VisibleChangeHandlerCount = state.VisibleChangeHandlerCount,
            Incoming = state.Incoming.Select(ReadPauseEventPayload).ToArray(),
            Outgoing = state.Outgoing.Select(ReadPauseEventPayload).ToArray()
        };

        private static ActionOriginPauseEventState ReadPauseEventPayload(NativeLivePauseEventState state) => new()
        {
            ReferenceId = ReferenceIds.For(state.Reference), Argument = state.Argument, Timestamp = state.Timestamp
        };

        private static ActionOriginCharacterReference ObservedCharacterReference(Character? value)
        {
            if (value is null) return new() { IsNull = true };
            var type = value.GetType();
            if (type.Assembly != typeof(Character).Assembly || !NativeObservedCharacterTypes.IsSupported(type.FullName))
                throw new InvalidDataException("Native hover reference type is unsupported.");
            return new() { IsNull = false, ReferenceId = ReferenceIds.For(value), RuntimeType = type.FullName };
        }
    }
}
