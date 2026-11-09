using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeDebrisStateBoundaryTests
{
    private static string Id() => Guid.NewGuid().ToString("N");
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static NativeDebrisStateSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.ReadDebrisState(request, receipt, 99, 111, "Farm", "123");

    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture()
    {
        var result = NativeInventoryGraphBoundaryTests.Fixture();
        result.Request.ActionOriginDebrisState = true;
        result.Receipt.BeforeDebris = [Row()];
        result.Receipt.AfterDebris = [Copy(result.Receipt.BeforeDebris[0])];
        foreach (var collection in result.Receipt.Collections)
        {
            var row = collection.DebrisBefore!;
            var selected = collection.ChunkId is null ? -1 : Array.IndexOf(row.ChunkIds, collection.ChunkId);
            row.DebrisId = Id(); row.ChunkIds = row.ChunkIds.Select(_ => Id()).ToArray();
            collection.DebrisId = row.DebrisId; collection.ChunkId = selected < 0 ? null : row.ChunkIds[selected];
            var template = Row().RawState!;
            template.Identity!.DebrisType = Enumerable.Range(0, 8).Single(value => NativeDebrisObservationDomain.DebrisTypeName(value) == row.DebrisType);
            template.Identity.ChunkType = row.ChunkType; template.Identity.ItemQuality = row.ItemQuality;
            template.Identity.ItemId = new() { IsNull = false, Value = row.RawItemId };
            template.Identity.HeldItem = row.HeldItem is null ? new() { IsNull = true } : new()
                { IsNull = false, ReferenceId = Id(), RuntimeType = row.HeldItem.RuntimeType, IsTool = false };
            var chunk = template.Chunks![0];
            template.Chunks = row.ChunkIds.Select(id =>
            {
                var value = Copy(chunk); value.ChunkId = id; value.Position!.ReferenceId = Id();
                value.Position.Incoming = []; value.Position.Outgoing = [];
                return value;
            }).ToArray();
            template.EventOccurrences = 0; row.RawState = template;
        }
        return result;
    }

    // Explicit synthetic native-format state, not a game observation or resource outcome.
    private static ActionOriginObservedDebris Row()
    {
        var chunk = new ActionOriginRawChunkState
        {
            ChunkId = Id(), XVelocityBits = unchecked((int)0x80000000), YVelocityBits = 0x7fc12345,
            PassedRestingLine = false, Bounces = -3, BobBits = 0, SinkTimer = -19, RandomOffset = int.MinValue,
            HitWall = true, XSpriteSheet = -2, YSpriteSheet = 7, RotationBits = 1, RotationVelocityBits = 2,
            ScaleBits = 3, AlphaBits = 4,
            Position = new()
            {
                ReferenceId = Id(), XBits = unchecked((int)0x80000000), YBits = 0x7fc12345,
                Paused = false, Moving = true, ExtrapolationEnabled = true, PauseInterpolationWait = false,
                PauseHandlerCount = 1, ChangeHandlerCount = 0, VisibleChangeHandlerCount = 0,
                Incoming = [new() { ReferenceId = Id(), Argument = false, Timestamp = uint.MaxValue },
                    new() { ReferenceId = Id(), Argument = true, Timestamp = 0 }]
            }
        };
        chunk.Position.Outgoing = [Copy(chunk.Position.Incoming[1])];
        return new()
        {
            DebrisId = Id(), RuntimeType = "StardewValley.Debris", DebrisType = "OBJECT", ChunkType = 12,
            RawItemId = "388", ItemQuality = -2, ChunkIds = [chunk.ChunkId!],
            RawState = new()
            {
                SchemaVersion = "native_debris_state.v1", Chunks = [chunk], EventOccurrences = 3,
                Identity = new()
                {
                    DebrisType = 4, ChunkType = 12, ItemQuality = -2, ItemId = new() { IsNull = false, Value = "388" },
                    HeldItem = new() { IsNull = true }, DroppedByPlayerId = -8, TargetDefined = false,
                    TargetUid = long.MinValue, Hover = new() { IsNull = true }
                },
                Motion = new()
                {
                    RelativeXBits = int.MinValue, FinalYLevel = -1, FinalYTarget = -2, TimeSinceDoneBouncingBits = 0,
                    ScaleBits = 0, MoveTowardsPlayer = false, MovingUp = true, FloppingFish = false,
                    IsFishable = true, MovingFinalYLevel = false, IsSinking = true, ReturnToDropperTimer = -3
                },
                Visual = new()
                {
                    SourceRectSize = -8, Message = new() { IsNull = true }, NonSpriteColor = uint.MaxValue,
                    ChunksColor = 0, AnimationTimerBits = 0, SpriteSheetName = new() { IsNull = false, Value = "" }, TextureCached = false
                }
            }
        };
    }
}
