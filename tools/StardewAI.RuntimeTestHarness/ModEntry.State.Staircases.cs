using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActivePlaceStaircase
    {
        public ActivePlaceStaircase(
            PendingExecution pending,
            MineShaft mine,
            Point target,
            Point stand,
            List<Point> path,
            int slotIndex,
            int totalBefore,
            int maxMovementTiles,
            int restoreSlotIndex,
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Stand = stand;
            Path = path;
            SlotIndex = slotIndex;
            TotalBefore = totalBefore;
            MaxMovementTiles = maxMovementTiles;
            RestoreSlotIndex = restoreSlotIndex;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; set; }
        public int SlotIndex { get; }
        public int TotalBefore { get; }
        public int MaxMovementTiles { get; }
        public int RestoreSlotIndex { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } =
            DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public int StageEnteredTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool CombatInterrupted { get; set; }
        public StaircasePlacementStage Stage { get; set; }
    }

    private enum StaircasePlacementStage
    {
        MoveToPlacement,
        AimPlacement,
        PressPlacement,
        ReleasePlacement,
        WaitForLadder
    }
}
