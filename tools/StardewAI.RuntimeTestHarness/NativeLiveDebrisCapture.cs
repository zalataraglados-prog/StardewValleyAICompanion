using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

// Pure scoped raw subset. Held Item/Character/texture/transport/closures remain opaque;
// the original observer owns IDs, aggregate budgets and any later Item graph attribution.
internal static partial class NativeLiveDebrisCapture
{
    internal static NativeLiveDebrisState Capture(Debris debris, int maximumCharacters, int maximumChunks, int maximumEvents)
    {
        if (maximumCharacters is < 0 or > 1024 * 1024 ||
            maximumChunks is < 0 or > NativeDebrisObservationDomain.MaximumChunks ||
            maximumEvents is < 0 or > NativeDebrisObservationDomain.MaximumEventOccurrences)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters), "Native debris remaining budget is invalid.");
        ArgumentNullException.ThrowIfNull(debris);
        Native(debris, typeof(Debris));
        Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetFields>(DebrisNetFields, debris));
        var budget = new NativeLiveTransientFieldCodec.CharacterBudget(maximumCharacters);
        var target = Wrapper(debris.player);
        var hover = debris.toHover;
        if (hover is not null && (hover.GetType().Assembly != typeof(Character).Assembly ||
            hover.GetType().Module.ModuleVersionId != ModuleId || !NativeObservedCharacterTypes.IsSupported(hover.GetType().FullName)))
            throw new InvalidDataException("Native debris hover runtime type is unsupported.");
        var identity = new NativeLiveDebrisIdentityState((int)Wrapper(debris.debrisType).Value, Wrapper(debris.chunkType).Value,
            budget.String(Wrapper(debris.itemId).Value), Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetInt>(DebrisQuality, debris)).Value,
            Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetRef<Item>>(DebrisItem, debris)).Value,
            Wrapper(debris.DroppedByPlayerID).Value, Wrapper(target.defined).Value, Wrapper(target.uid).Value, hover);
        var motion = new NativeLiveDebrisMotionState(Bits(NativeLiveTransientFieldCodec.ReadValue<float>(DebrisRelativeX, debris)),
            Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetInt>(DebrisFinalY, debris)).Value,
            Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetInt>(DebrisFinalTarget, debris)).Value,
            Bits(debris.timeSinceDoneBouncing), Bits(Wrapper(debris.scale).Value),
            Wrapper(NativeLiveTransientFieldCodec.ReadNullable<NetBool>(DebrisTowards, debris)).Value,
            NativeLiveTransientFieldCodec.ReadValue<bool>(DebrisMovingUp, debris), Wrapper(debris.floppingFish).Value,
            debris.isFishable, debris.movingFinalYLevel, Wrapper(debris.isSinking).Value,
            NativeLiveTransientFieldCodec.ReadValue<int>(DebrisReturnTimer, debris));
        var visual = new NativeLiveDebrisVisualState(Wrapper(debris.sizeOfSourceRectSquares).Value,
            budget.String(Wrapper(debris.debrisMessage).Value), Wrapper(debris.nonSpriteChunkColor).Value.PackedValue,
            Wrapper(debris.chunksColor).Value.PackedValue, Bits(NativeLiveTransientFieldCodec.ReadValue<float>(DebrisAnimationTimer, debris)),
            budget.String(Wrapper(debris.spriteChunkSheetName).Value),
            NativeLiveTransientFieldCodec.ReadNullable<Texture2D>(DebrisTexture, debris) is not null);
        var remainingEvents = maximumEvents;
        var chunks = CaptureChunks(debris, maximumChunks, ref remainingEvents);
        return new(debris, identity, motion, visual, chunks, budget.Characters, maximumEvents - remainingEvents);
    }

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    private static void Native(object value, Type expected)
    {
        if (value.GetType() != expected || expected.Module.ModuleVersionId != ModuleId)
            throw new InvalidDataException("Native debris/field runtime type or module is unsupported: " + expected.FullName);
    }
    private static T Wrapper<T>(T? value) where T : class
    {
        if (value is null) throw new InvalidDataException("Native debris field wrapper is unavailable: " + typeof(T).FullName);
        Native(value, typeof(T));
        return value;
    }
    private static List<T> List<T>(List<T>? value)
    {
        if (value is null || value.GetType() != typeof(List<T>))
            throw new InvalidDataException("Native debris list storage is unavailable or unsupported.");
        return value;
    }
}
