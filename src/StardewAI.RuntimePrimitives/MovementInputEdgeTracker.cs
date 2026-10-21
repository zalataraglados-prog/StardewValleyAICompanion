namespace StardewAI.RuntimePrimitives
{
    public readonly struct MovementInputEdges
    {
        public MovementInputEdges(
            int? pressedDirection,
            int? releasedDirection,
            int? heldDirection)
        {
            PressedDirection = pressedDirection;
            ReleasedDirection = releasedDirection;
            HeldDirection = heldDirection;
        }

        public int? PressedDirection { get; }
        public int? ReleasedDirection { get; }
        public int? HeldDirection { get; }
    }

    public sealed class MovementInputEdgeTracker
    {
        private int? previousDirection;
        private long previousTransitionTick = long.MinValue;

        public MovementInputEdges Observe(int? direction, long transitionTick)
        {
            var leaseTransitionWasSkipped =
                direction.HasValue &&
                direction == previousDirection &&
                transitionTick != previousTransitionTick;
            var pressedDirection =
                direction.HasValue &&
                (direction != previousDirection || leaseTransitionWasSkipped)
                    ? direction
                    : null;
            var releasedDirection =
                previousDirection.HasValue && direction != previousDirection
                    ? previousDirection
                    : null;

            previousDirection = direction;
            previousTransitionTick = transitionTick;
            return new MovementInputEdges(
                pressedDirection,
                releasedDirection,
                direction);
        }
    }
}
