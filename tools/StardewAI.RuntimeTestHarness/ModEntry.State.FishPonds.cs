using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveFishPondService
    {
        public ActiveFishPondService(
            PendingExecution pending,
            GameLocation location,
            FishPond pond,
            Point target,
            Point stand,
            List<Point> path,
            string mode,
            int restoreSlotIndex,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Pond = pond;
            Target = target;
            Stand = stand;
            Path = path;
            Mode = mode;
            RestoreSlotIndex = restoreSlotIndex;
            MaxMovementTiles = maxMovementTiles;
            FishingExperienceBefore = Game1.player.experiencePoints[Farmer.fishingSkill];
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public FishPond Pond { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public string Mode { get; }
        public int RestoreSlotIndex { get; }
        public int MaxMovementTiles { get; }
        public int FishingExperienceBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public ClearanceOutputItemKey? OutputKey { get; set; }
        public int OutputCountBefore { get; set; }
        public int RequestItemCountBefore { get; set; }
        public int[] RequestSlots { get; set; } = Array.Empty<int>();
        public int DeliveredCount { get; set; }
        public int NextInteractionTick { get; set; }
        public bool FinalInteractionIssued { get; set; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
    }
}
