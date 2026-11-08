using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Network;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeLiveDebrisCapture
{
    private static readonly Guid ModuleId = Guid.Parse(NativeDebrisObservationDomain.NativeModuleId);
    // All private layout checks/read casts use the ORIGINAL shared codec; no fallback names,
    // property probing or second reflection framework. Nullable values are not missing wrappers.
    private static readonly FieldInfo DebrisRelativeX = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "relativeXPosition", typeof(float));
    private static readonly FieldInfo DebrisChunks = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "chunks", typeof(NetObjectShrinkList<Chunk>));
    private static readonly FieldInfo DebrisQuality = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "netItemQuality", typeof(NetInt));
    private static readonly FieldInfo DebrisFinalY = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "netChunkFinalYLevel", typeof(NetInt));
    private static readonly FieldInfo DebrisFinalTarget = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "netChunkFinalYTarget", typeof(NetInt));
    private static readonly FieldInfo DebrisTowards = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "_chunksMoveTowardsPlayer", typeof(NetBool));
    private static readonly FieldInfo DebrisMovingUp = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "movingUp", typeof(bool));
    private static readonly FieldInfo DebrisAnimationTimer = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "animationTimer", typeof(float));
    private static readonly FieldInfo DebrisReturnTimer = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "timeBeforeReturnToDroppingPlayer", typeof(int));
    private static readonly FieldInfo DebrisTexture = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "_spriteChunkSheet", typeof(Texture2D));
    private static readonly FieldInfo DebrisItem = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "netItem", typeof(NetRef<Item>));
    private static readonly FieldInfo DebrisNetFields = NativeLiveTransientFieldCodec.RequireField(typeof(Debris), "<NetFields>k__BackingField", typeof(NetFields));
    private static readonly FieldInfo ChunkArray = NativeLiveTransientFieldCodec.RequireField(typeof(NetObjectShrinkList<Chunk>), "array", typeof(NetArray<Chunk, NetRef<Chunk>>));
    private static readonly FieldInfo ChunkElements = NativeLiveTransientFieldCodec.RequireField(typeof(NetArray<Chunk, NetRef<Chunk>>), "elements", typeof(List<NetRef<Chunk>>));
    private static readonly FieldInfo ChunkScale = NativeLiveTransientFieldCodec.RequireField(typeof(Chunk), "netScale", typeof(NetFloat));
    private static readonly FieldInfo ChunkAlpha = NativeLiveTransientFieldCodec.RequireField(typeof(Chunk), "netAlpha", typeof(NetFloat));
    private static readonly FieldInfo ChunkNetFields = NativeLiveTransientFieldCodec.RequireField(typeof(Chunk), "<NetFields>k__BackingField", typeof(NetFields));
    private static readonly FieldInfo PositionNetFields = NativeLiveTransientFieldCodec.RequireField(typeof(NetPosition), "<NetFields>k__BackingField", typeof(NetFields));
    private static readonly FieldInfo PositionPaused = NativeLiveTransientFieldCodec.RequireField(typeof(NetPausableField<Vector2, NetVector2, NetVector2>), "paused", typeof(bool));
    private static readonly FieldInfo PositionPauseEvent = NativeLiveTransientFieldCodec.RequireField(typeof(NetPausableField<Vector2, NetVector2, NetVector2>), "pauseEvent", typeof(NetEvent1Field<bool, NetBool>));
    private static readonly FieldInfo Incoming = NativeLiveTransientFieldCodec.RequireField(typeof(AbstractNetEvent1<bool>), "incomingEvents", typeof(List<AbstractNetEvent1<bool>.EventRecording>));
    private static readonly FieldInfo Outgoing = NativeLiveTransientFieldCodec.RequireField(typeof(AbstractNetEvent1<bool>), "outgoingEvents", typeof(List<AbstractNetEvent1<bool>.EventRecording>));
    private static readonly FieldInfo PauseHandlers = NativeLiveTransientFieldCodec.RequireField(typeof(AbstractNetEvent1<bool>), "onEvent", typeof(AbstractNetEvent1<bool>.Event));
    private static readonly FieldInfo ChangeHandlers = NativeLiveTransientFieldCodec.RequireField(typeof(NetPosition), "fieldChangeEvent", typeof(FieldChange<NetPosition, Vector2>));
    private static readonly FieldInfo VisibleHandlers = NativeLiveTransientFieldCodec.RequireField(typeof(NetPosition), "fieldChangeVisibleEvent", typeof(FieldChange<NetPosition, Vector2>));
}
