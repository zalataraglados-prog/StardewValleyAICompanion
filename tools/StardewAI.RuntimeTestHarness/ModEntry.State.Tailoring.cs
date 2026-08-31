using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private enum TailoringStage
    {
        Move,
        Open,
        WaitMenu,
        LoadLeft,
        LoadRight,
        Start,
        WaitComplete,
        StoreOutput,
        ReturnLeft,
        ReturnRight,
        Close,
        Verify
    }

    private sealed class ActiveTailoring
    {
        public ActiveTailoring(
            PendingExecution pending,
            GameLocation location,
            Point target,
            Point stand,
            List<Point> path,
            Item left,
            Item right,
            Dictionary<string, int> tailoredCountsBefore)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            Left = left;
            Right = right;
            TailoredCountsBefore = tailoredCountsBefore;
            LastPosition = Game1.player.Position;
            BeforeCounts = CaptureTailoringCounts();
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public Item Left { get; }
        public Item Right { get; }
        public Dictionary<string, int> TailoredCountsBefore { get; }
        public Dictionary<string, int> BeforeCounts { get; }
        public TailoringMenu? Menu { get; set; }
        public Item? Result { get; set; }
        public TailoringStage Stage { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageStartedAt { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool NativeOperationStarted { get; set; }
    }
}
