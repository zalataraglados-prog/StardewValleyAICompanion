using System.Reflection;
using StardewAI.RuntimePrimitives;
using StardewValley;
using StardewValley.Util;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo? GameInputSimulatorField = typeof(Game1)
        .GetField("inputSimulator", BindingFlags.Static | BindingFlags.NonPublic);

    private IInputSimulator? headlessInputPump;

    private static bool IsHeadlessTrainingRuntime()
    {
        return string.Equals(
                   Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE"),
                   "1",
                   StringComparison.Ordinal) &&
               string.Equals(
                   Environment.GetEnvironmentVariable(
                       "STARDEWAI_SUPPRESS_LOCAL_RENDER"),
                   "1",
                   StringComparison.Ordinal);
    }

    private void OnHeadlessTrainingSaveLoaded(
        object? sender,
        StardewModdingAPI.Events.SaveLoadedEventArgs e)
    {
        EnsureHeadlessInputPump();
    }

    private void EnsureHeadlessInputPump()
    {
        if ((!IsAiHostRuntimeMode() && !IsHeadlessTrainingRuntime()) ||
            !string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_SUPPRESS_LOCAL_RENDER"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        if (GameInputSimulatorField is null)
        {
            throw new MissingFieldException(typeof(Game1).FullName, "inputSimulator");
        }

        headlessInputPump ??= new HeadlessMovementInputSimulator(
            () => executorMovementLease.Direction,
            () => executorMovementLease.LastTransitionTick);
        if (!ReferenceEquals(GameInputSimulatorField.GetValue(null), headlessInputPump))
        {
            GameInputSimulatorField.SetValue(null, headlessInputPump);
        }
    }

    private sealed class HeadlessMovementInputSimulator : IInputSimulator
    {
        private readonly Func<int?> readDirection;
        private readonly Func<long> readTransitionTick;
        private readonly MovementInputEdgeTracker edgeTracker = new();

        public int? LastPressedDirection { get; private set; }
        public int? LastReleasedDirection { get; private set; }
        public int? LastHeldDirection { get; private set; }

        public HeadlessMovementInputSimulator(
            Func<int?> readDirection,
            Func<long> readTransitionTick)
        {
            this.readDirection = readDirection;
            this.readTransitionTick = readTransitionTick;
        }

        public void SimulateInput(
            ref bool actionButtonPressed,
            ref bool switchToolButtonPressed,
            ref bool useToolButtonPressed,
            ref bool useToolButtonReleased,
            ref bool addItemToInventoryButtonPressed,
            ref bool cancelButtonPressed,
            ref bool moveUpPressed,
            ref bool moveRightPressed,
            ref bool moveLeftPressed,
            ref bool moveDownPressed,
            ref bool moveUpReleased,
            ref bool moveRightReleased,
            ref bool moveLeftReleased,
            ref bool moveDownReleased,
            ref bool moveUpHeld,
            ref bool moveRightHeld,
            ref bool moveLeftHeld,
            ref bool moveDownHeld)
        {
            var edges = edgeTracker.Observe(
                readDirection(),
                readTransitionTick());
            LastPressedDirection = edges.PressedDirection;
            LastReleasedDirection = edges.ReleasedDirection;
            LastHeldDirection = edges.HeldDirection;
            moveUpPressed = edges.PressedDirection == 0;
            moveRightPressed = edges.PressedDirection == 1;
            moveDownPressed = edges.PressedDirection == 2;
            moveLeftPressed = edges.PressedDirection == 3;
            moveUpReleased = edges.ReleasedDirection == 0;
            moveRightReleased = edges.ReleasedDirection == 1;
            moveDownReleased = edges.ReleasedDirection == 2;
            moveLeftReleased = edges.ReleasedDirection == 3;
            moveUpHeld = edges.HeldDirection == 0;
            moveRightHeld = edges.HeldDirection == 1;
            moveDownHeld = edges.HeldDirection == 2;
            moveLeftHeld = edges.HeldDirection == 3;
        }
    }
}
