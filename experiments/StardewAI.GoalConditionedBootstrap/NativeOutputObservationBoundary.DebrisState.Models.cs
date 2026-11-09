namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedPauseEventState(string ReferenceId, bool Argument, uint Timestamp);
public sealed record NativeObservedChunkPositionState(string ReferenceId, int XBits, int YBits, bool Paused,
    bool Moving, bool ExtrapolationEnabled, bool PauseInterpolationWait,
    IReadOnlyList<NativeObservedPauseEventState> Incoming, IReadOnlyList<NativeObservedPauseEventState> Outgoing,
    int PauseHandlerCount, int ChangeHandlerCount, int VisibleChangeHandlerCount);
public sealed record NativeObservedRawChunkState(string ChunkId, NativeObservedChunkPositionState Position,
    int XVelocityBits, int YVelocityBits, bool PassedRestingLine, int Bounces, int BobBits, int SinkTimer,
    int RandomOffset, bool HitWall, int XSpriteSheet, int YSpriteSheet, int RotationBits,
    int RotationVelocityBits, int ScaleBits, int AlphaBits);
public sealed record NativeObservedCharacterReference(string ReferenceId, string RuntimeType);
public sealed record NativeObservedDebrisIdentityState(int DebrisType, int ChunkType, string? ItemId,
    int ItemQuality, NativeObservedTransientItem? HeldItem, long DroppedByPlayerId,
    bool TargetDefined, long TargetUid, NativeObservedCharacterReference? Hover);
public sealed record NativeObservedDebrisMotionState(int RelativeXBits, int FinalYLevel, int FinalYTarget,
    int TimeSinceDoneBouncingBits, int ScaleBits, bool MoveTowardsPlayer, bool MovingUp, bool FloppingFish,
    bool IsFishable, bool MovingFinalYLevel, bool IsSinking, int ReturnToDropperTimer);
public sealed record NativeObservedDebrisVisualState(int SourceRectSize, string? Message, uint NonSpriteColor,
    uint ChunksColor, int AnimationTimerBits, string? SpriteSheetName, bool TextureCached);
public sealed record NativeObservedDebrisRawState(string DebrisId, NativeObservedDebrisIdentityState Identity,
    NativeObservedDebrisMotionState Motion, NativeObservedDebrisVisualState Visual,
    IReadOnlyList<NativeObservedRawChunkState> Chunks, int EventOccurrences);
public sealed record NativeObservedCollectedDebrisState(int Ordinal, NativeObservedDebrisRawState State);
public sealed record NativeDebrisStateSummary(string ScopeId, string CaptureSessionId,
    NativeInventoryGraphSummary InventoryGraph, IReadOnlyList<NativeObservedDebrisRawState> Before,
    IReadOnlyList<NativeObservedDebrisRawState> After, IReadOnlyList<NativeObservedCollectedDebrisState> Collections)
{
    public bool DebrisStateSubsetVerified => true;
    public bool FullTransientStateVerified => false;
    public bool NativeOutcomeVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool FormalProductTrainingAuthorized => false;
}
