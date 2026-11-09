using StardewValley;
using StardewValley.Network;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLivePauseEventState(object Reference, bool Argument, uint Timestamp);
internal sealed record NativeLiveChunkPositionState(NetPosition Reference, int XBits, int YBits,
    bool Paused, bool Moving, bool ExtrapolationEnabled, bool PauseInterpolationWait,
    IReadOnlyList<NativeLivePauseEventState> Incoming, IReadOnlyList<NativeLivePauseEventState> Outgoing,
    int PauseHandlerCount, int ChangeHandlerCount, int VisibleChangeHandlerCount);
internal sealed record NativeLiveChunkState(Chunk Reference, NativeLiveChunkPositionState Position,
    int XVelocityBits, int YVelocityBits, bool PassedRestingLine, int Bounces, int BobBits, int SinkTimer,
    int RandomOffset, bool HitWall, int XSpriteSheet, int YSpriteSheet, int RotationBits,
    int RotationVelocityBits, int ScaleBits, int AlphaBits);
internal sealed record NativeLiveDebrisIdentityState(int DebrisType, int ChunkType, string? ItemId,
    int ItemQuality, Item? HeldItem, long DroppedByPlayerId, bool TargetDefined, long TargetUid, Character? Hover);
internal sealed record NativeLiveDebrisMotionState(int RelativeXBits, int FinalYLevel, int FinalYTarget,
    int TimeSinceDoneBouncingBits, int ScaleBits, bool MoveTowardsPlayer, bool MovingUp, bool FloppingFish,
    bool IsFishable, bool MovingFinalYLevel, bool IsSinking, int ReturnToDropperTimer);
internal sealed record NativeLiveDebrisVisualState(int SourceRectSize, string? Message, uint NonSpriteColor,
    uint ChunksColor, int AnimationTimerBits, string? SpriteSheetName, bool TextureCached);
internal sealed record NativeLiveDebrisState(Debris Reference, NativeLiveDebrisIdentityState Identity,
    NativeLiveDebrisMotionState Motion, NativeLiveDebrisVisualState Visual,
    IReadOnlyList<NativeLiveChunkState> Chunks, int Characters, int EventOccurrences);
