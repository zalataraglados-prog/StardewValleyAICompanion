using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mining;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveCalicoStatueFixture
    {
        public ActiveCalicoStatueFixture(PendingExecution pending, int effectId)
        {
            Pending = pending;
            EffectId = effectId;
        }

        public PendingExecution Pending { get; }
        public int EffectId { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks => 600;
    }

    private enum CalicoStatueStage
    {
        Move,
        WaitReceipt
    }

    private sealed class ActiveCalicoStatue : INativeObjectInteractionMovement
    {
        public ActiveCalicoStatue(
            PendingExecution pending,
            MineShaft mine,
            Point target,
            Point stand,
            List<Point> path,
            int maxMovementTiles,
            int totalBefore,
            int ratingBefore,
            int eggsBefore,
            string effectsBefore,
            string effectsAfter,
            CalicoStatueEffectDefinition expectedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            TotalBefore = totalBefore;
            RatingBefore = ratingBefore;
            EggsBefore = eggsBefore;
            EffectsBefore = effectsBefore;
            EffectsAfter = effectsAfter;
            ExpectedEffect = expectedEffect;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public GameLocation Location => Mine;
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 3600;
        public int TotalBefore { get; }
        public int RatingBefore { get; }
        public int EggsBefore { get; }
        public string EffectsBefore { get; }
        public string EffectsAfter { get; }
        public CalicoStatueEffectDefinition ExpectedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public CalicoStatueStage Stage { get; set; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int ActionIssuedAtTick { get; set; }
        public bool NativeHandled { get; set; }
    }
}
