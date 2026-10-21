using System;
using System.Collections.Generic;

namespace StardewAI.RuntimePrimitives
{
    public sealed class ExecutorDiagnosticFrame
    {
        public long Tick { get; set; }
        public string Phase { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string MovementOwner { get; set; } = string.Empty;
        public int? MovementDirection { get; set; }
        public float PixelX { get; set; }
        public float PixelY { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int FacingDirection { get; set; }
        public bool UsingTool { get; set; }
        public bool CanMove { get; set; }
        public bool CanReleaseTool { get; set; }
        public bool PauseForSingleAnimation { get; set; }
        public bool GamePaused { get; set; }
        public bool GlobalFade { get; set; }
        public bool FreezeControls { get; set; }
        public bool IsWarping { get; set; }
        public bool EventUp { get; set; }
        public int FreezePause { get; set; }
        public string MovementDirections { get; set; } = string.Empty;
        public long MovementTransitionTick { get; set; }
        public int? HeadlessPressedDirection { get; set; }
        public int? HeadlessReleasedDirection { get; set; }
        public int? HeadlessHeldDirection { get; set; }
        public string MovementTransitionReason { get; set; } = string.Empty;
    }

    public sealed class ExecutorDiagnosticRingBuffer
    {
        private readonly ExecutorDiagnosticFrame[] frames;
        private int nextIndex;
        private int count;

        public ExecutorDiagnosticRingBuffer(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            frames = new ExecutorDiagnosticFrame[capacity];
        }

        public int Capacity => frames.Length;
        public int Count => count;

        public void Add(ExecutorDiagnosticFrame frame)
        {
            frames[nextIndex] = frame;
            nextIndex = (nextIndex + 1) % frames.Length;
            if (count < frames.Length)
            {
                count++;
            }
        }

        public IReadOnlyList<ExecutorDiagnosticFrame> Snapshot()
        {
            var result = new List<ExecutorDiagnosticFrame>(count);
            var start = (nextIndex - count + frames.Length) % frames.Length;
            for (var index = 0; index < count; index++)
            {
                result.Add(frames[(start + index) % frames.Length]);
            }

            return result;
        }
    }
}
