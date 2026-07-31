namespace StardewAI.RuntimePrimitives
{
    public enum NativeToolActionPhase
    {
        Ready,
        WaitingForNativeStart,
        WaitingForRelease,
        WaitingForNativeCompletion,
        Completed,
        Blocked
    }

    public enum NativeToolActionCommand
    {
        None,
        Press,
        Release,
        CycleCompleted,
        Block
    }

    public readonly struct NativeToolActionObservation
    {
        public NativeToolActionObservation(
            bool usingTool,
            bool canMove,
            bool canReleaseTool,
            bool pauseForSingleAnimation)
        {
            UsingTool = usingTool;
            CanMove = canMove;
            CanReleaseTool = canReleaseTool;
            PauseForSingleAnimation = pauseForSingleAnimation;
        }

        public bool UsingTool { get; }
        public bool CanMove { get; }
        public bool CanReleaseTool { get; }
        public bool PauseForSingleAnimation { get; }
    }

    public readonly struct NativeToolActionDecision
    {
        public NativeToolActionDecision(
            NativeToolActionCommand command,
            string reason = "")
        {
            Command = command;
            Reason = reason;
        }

        public NativeToolActionCommand Command { get; }
        public string Reason { get; }
    }

    public sealed class NativeToolActionLifecycle
    {
        private readonly int nativeStartTimeoutTicks;
        private readonly int nativeCompletionTimeoutTicks;
        private int phaseTicks;

        public NativeToolActionLifecycle(
            int nativeStartTimeoutTicks = 30,
            int nativeCompletionTimeoutTicks = 240)
        {
            this.nativeStartTimeoutTicks = nativeStartTimeoutTicks;
            this.nativeCompletionTimeoutTicks = nativeCompletionTimeoutTicks;
        }

        public NativeToolActionPhase Phase { get; private set; } =
            NativeToolActionPhase.Ready;

        public NativeToolActionDecision Advance(
            NativeToolActionObservation observation)
        {
            switch (Phase)
            {
                case NativeToolActionPhase.Ready:
                    if (observation.UsingTool ||
                        !observation.CanMove ||
                        observation.PauseForSingleAnimation)
                    {
                        return new NativeToolActionDecision(
                            NativeToolActionCommand.None);
                    }

                    Phase = NativeToolActionPhase.WaitingForNativeStart;
                    phaseTicks = 0;
                    return new NativeToolActionDecision(
                        NativeToolActionCommand.Press);

                case NativeToolActionPhase.WaitingForNativeStart:
                    if (!observation.UsingTool)
                    {
                        return WaitOrBlock(
                            nativeStartTimeoutTicks,
                            "native_tool_action_did_not_start");
                    }

                    Phase = NativeToolActionPhase.WaitingForRelease;
                    phaseTicks = 0;
                    return observation.CanReleaseTool
                        ? RequestRelease()
                        : new NativeToolActionDecision(
                            NativeToolActionCommand.None);

                case NativeToolActionPhase.WaitingForRelease:
                    if (!observation.UsingTool)
                    {
                        if (observation.CanMove &&
                            !observation.PauseForSingleAnimation)
                        {
                            Phase = NativeToolActionPhase.Completed;
                            phaseTicks = 0;
                            return new NativeToolActionDecision(
                                NativeToolActionCommand.CycleCompleted,
                                "native_non_charge_tool_completed_without_explicit_release");
                        }

                        return WaitOrBlock(
                            nativeCompletionTimeoutTicks,
                            "native_tool_action_ended_while_player_remained_locked");
                    }

                    return observation.CanReleaseTool
                        ? RequestRelease()
                        : WaitOrBlock(
                            nativeCompletionTimeoutTicks,
                            "native_tool_action_never_became_releasable");

                case NativeToolActionPhase.WaitingForNativeCompletion:
                    if (observation.UsingTool ||
                        !observation.CanMove ||
                        observation.PauseForSingleAnimation)
                    {
                        return WaitOrBlock(
                            nativeCompletionTimeoutTicks,
                            "native_tool_action_completion_timeout");
                    }

                    Phase = NativeToolActionPhase.Completed;
                    phaseTicks = 0;
                    return new NativeToolActionDecision(
                        NativeToolActionCommand.CycleCompleted);

                case NativeToolActionPhase.Blocked:
                    return new NativeToolActionDecision(
                        NativeToolActionCommand.Block,
                        "native_tool_action_already_blocked");

                default:
                    return new NativeToolActionDecision(
                        NativeToolActionCommand.None);
            }
        }

        public void Reset()
        {
            Phase = NativeToolActionPhase.Ready;
            phaseTicks = 0;
        }

        private NativeToolActionDecision RequestRelease()
        {
            Phase = NativeToolActionPhase.WaitingForNativeCompletion;
            phaseTicks = 0;
            return new NativeToolActionDecision(
                NativeToolActionCommand.Release);
        }

        private NativeToolActionDecision WaitOrBlock(
            int timeoutTicks,
            string reason)
        {
            phaseTicks++;
            return phaseTicks > timeoutTicks
                ? Block(reason)
                : new NativeToolActionDecision(NativeToolActionCommand.None);
        }

        private NativeToolActionDecision Block(string reason)
        {
            Phase = NativeToolActionPhase.Blocked;
            return new NativeToolActionDecision(
                NativeToolActionCommand.Block,
                reason);
        }
    }
}
