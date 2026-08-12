using System.Reflection;
using StardewValley;
using StardewValley.Util;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo? GameInputSimulatorField = typeof(Game1)
        .GetField("inputSimulator", BindingFlags.Static | BindingFlags.NonPublic);

    private IInputSimulator? headlessInputPump;

    private void EnsureHeadlessInputPump()
    {
        if (!IsAiHostRuntimeMode() ||
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
            () => executorMovementLease.Direction);
        if (!ReferenceEquals(GameInputSimulatorField.GetValue(null), headlessInputPump))
        {
            GameInputSimulatorField.SetValue(null, headlessInputPump);
        }
    }

    private sealed class HeadlessMovementInputSimulator : IInputSimulator
    {
        private readonly Func<int?> readDirection;
        private int? previousDirection;

        public HeadlessMovementInputSimulator(Func<int?> readDirection)
        {
            this.readDirection = readDirection;
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
            var direction = readDirection();
            moveUpPressed = direction == 0 && previousDirection != 0;
            moveRightPressed = direction == 1 && previousDirection != 1;
            moveDownPressed = direction == 2 && previousDirection != 2;
            moveLeftPressed = direction == 3 && previousDirection != 3;
            moveUpReleased = previousDirection == 0 && direction != 0;
            moveRightReleased = previousDirection == 1 && direction != 1;
            moveDownReleased = previousDirection == 2 && direction != 2;
            moveLeftReleased = previousDirection == 3 && direction != 3;
            moveUpHeld = direction == 0;
            moveRightHeld = direction == 1;
            moveDownHeld = direction == 2;
            moveLeftHeld = direction == 3;
            previousDirection = direction;
        }
    }
}
