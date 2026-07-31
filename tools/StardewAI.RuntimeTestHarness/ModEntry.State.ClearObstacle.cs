using Microsoft.Xna.Framework;
using StardewAI.RuntimePrimitives;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed class ActiveClearObstacle
    {
        public ActiveClearObstacle(
            PendingExecution pending,
            GameLocation location,
            Point target,
            Tool tool,
            int toolSlotBefore,
            bool targetIsArtifactSpot,
            ClearanceOutputItemExpectation[]? expectedOutputItems,
            Dictionary<ClearanceOutputItemKey, int>? outputItemMultisetBefore,
            string before,
            double staminaBefore,
            int beforeForagingExperience,
            int? expectedForagingExperience,
            ClearanceOutputProjection? outputProjection,
            int primaryOutputCountBefore,
            int bonusOutputCountBefore,
            uint artifactSpotsDugBefore,
            bool defenseBookMailBefore,
            string targetTerrainFeatureBefore,
            int maxSwings)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Tool = tool;
            ToolSlotBefore = toolSlotBefore;
            TargetIsArtifactSpot = targetIsArtifactSpot;
            ExpectedOutputItems = expectedOutputItems;
            OutputItemMultisetBefore = outputItemMultisetBefore;
            Before = before;
            StaminaBefore = staminaBefore;
            BeforeForagingExperience = beforeForagingExperience;
            ExpectedForagingExperience = expectedForagingExperience;
            OutputProjection = outputProjection;
            PrimaryOutputCountBefore = primaryOutputCountBefore;
            BonusOutputCountBefore = bonusOutputCountBefore;
            ArtifactSpotsDugBefore = artifactSpotsDugBefore;
            DefenseBookMailBefore = defenseBookMailBefore;
            TargetTerrainFeatureBefore = targetTerrainFeatureBefore;
            MaxSwings = maxSwings;
            MaxTicks = maxSwings * 300 + 300;
            ObservedLabels.Add(before);
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Tool Tool { get; }
        public int ToolSlotBefore { get; }
        public bool TargetIsArtifactSpot { get; }
        public ClearanceOutputItemExpectation[]? ExpectedOutputItems { get; }
        public Dictionary<ClearanceOutputItemKey, int>? OutputItemMultisetBefore { get; }
        public string Before { get; }
        public double StaminaBefore { get; }
        public int BeforeForagingExperience { get; }
        public int? ExpectedForagingExperience { get; }
        public ClearanceOutputProjection? OutputProjection { get; }
        public int PrimaryOutputCountBefore { get; }
        public int BonusOutputCountBefore { get; }
        public uint ArtifactSpotsDugBefore { get; }
        public bool DefenseBookMailBefore { get; }
        public string TargetTerrainFeatureBefore { get; }
        public int MaxSwings { get; }
        public int MaxTicks { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public NativeToolActionLifecycle Lifecycle { get; } = new();
        public List<string> ObservedLabels { get; } = new();
        public int ElapsedTicks { get; set; }
        public int SwingCount { get; set; }
    }
}
