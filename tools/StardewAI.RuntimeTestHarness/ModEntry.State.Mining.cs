using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private sealed class ActiveMineStone
    {
        public ActiveMineStone(
            PendingExecution pending,
            string locationId,
            Point target,
            List<Point> path,
            Pickaxe pickaxe,
            string qualifiedItemId,
            int healthBefore,
            double staminaBefore,
            int maxSwings,
            int maxMovementTiles,
            Point? requestedStand,
            string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            Target = target;
            Path = path;
            Pickaxe = pickaxe;
            QualifiedItemId = qualifiedItemId;
            HealthBefore = healthBefore;
            StaminaBefore = staminaBefore;
            MaxSwings = maxSwings;
            MaxMovementTiles = maxMovementTiles;
            RequestedStand = requestedStand;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Max(180, maxMovementTiles * 90) + maxSwings * 240;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            ObservedHealth.Add(healthBefore);
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public Pickaxe Pickaxe { get; }
        public string QualifiedItemId { get; }
        public int HealthBefore { get; }
        public double StaminaBefore { get; }
        public int MaxSwings { get; }
        public int MaxMovementTiles { get; }
        public Point? RequestedStand { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int PathFailureTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int SwingCount { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public bool CombatInterrupted { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public List<int> ObservedHealth { get; } = new();
    }

    private sealed class ActiveResourceClump
    {
        public ActiveResourceClump(
            PendingExecution pending,
            GameLocation location,
            ResourceClump clump,
            Point anchor,
            Point hitTile,
            Point stand,
            List<Point> path,
            Tool tool,
            string requiredToolKind,
            int minimumUpgradeLevel,
            float healthBefore,
            int maxSwings,
            int maxMovementTiles,
            int restoreSlotIndex,
            string factPathPrefix,
            bool trackForagingExperience,
            ClearanceOutputItemExpectation[] expectedOutputs,
            int[] outputCountsBefore,
            int? expectedForagingExperienceDelta,
            string possibleSecretNoteQualifiedItemId,
            int secretNoteCountBefore,
            string requestedEffect)
        {
            Pending = pending;
            Location = location;
            Clump = clump;
            Anchor = anchor;
            HitTile = hitTile;
            Stand = stand;
            Path = path;
            Tool = tool;
            RequiredToolKind = requiredToolKind;
            MinimumUpgradeLevel = minimumUpgradeLevel;
            HealthBefore = healthBefore;
            ParentSheetIndex = clump.parentSheetIndex.Value;
            Width = clump.width.Value;
            Height = clump.height.Value;
            MaxSwings = maxSwings;
            MaxMovementTiles = maxMovementTiles;
            RestoreSlotIndex = restoreSlotIndex;
            FactPathPrefix = factPathPrefix;
            TrackForagingExperience = trackForagingExperience;
            ExpectedOutputs = expectedOutputs;
            OutputCountsBefore = outputCountsBefore;
            ExpectedForagingExperienceDelta = expectedForagingExperienceDelta;
            PossibleSecretNoteQualifiedItemId = possibleSecretNoteQualifiedItemId;
            SecretNoteCountBefore = secretNoteCountBefore;
            RequestedEffect = requestedEffect;
            StaminaBefore = Game1.player.Stamina;
            ForagingExperienceBefore = Game1.player.experiencePoints[Farmer.foragingSkill];
            IsGiantCrop = string.Equals(
                pending.Request.OptionId,
                "executor.harvest_giant_crop",
                StringComparison.Ordinal);
            DebrisCountBefore = location.debris.Count;
            LuckExperienceBefore = Game1.player.experiencePoints[Farmer.luckSkill];
            MaxTicks = Math.Max(300, path.Count * 90 + maxSwings * 240);
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            ObservedHealth.Add(healthBefore);
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public ResourceClump Clump { get; }
        public Point Anchor { get; }
        public Point HitTile { get; }
        public Point Stand { get; }
        public List<Point> Path { get; set; }
        public Tool Tool { get; }
        public string RequiredToolKind { get; }
        public int MinimumUpgradeLevel { get; }
        public float HealthBefore { get; }
        public int ParentSheetIndex { get; }
        public int Width { get; }
        public int Height { get; }
        public int MaxSwings { get; }
        public int MaxMovementTiles { get; }
        public int RestoreSlotIndex { get; }
        public string FactPathPrefix { get; }
        public bool TrackForagingExperience { get; }
        public ClearanceOutputItemExpectation[] ExpectedOutputs { get; }
        public int[] OutputCountsBefore { get; }
        public int? ExpectedForagingExperienceDelta { get; }
        public string PossibleSecretNoteQualifiedItemId { get; }
        public int SecretNoteCountBefore { get; }
        public string RequestedEffect { get; }
        public double StaminaBefore { get; }
        public int ForagingExperienceBefore { get; }
        public bool IsGiantCrop { get; }
        public int DebrisCountBefore { get; }
        public int LuckExperienceBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int MovementTiles { get; set; }
        public int PathIndex { get; set; }
        public int PathFailureTicks { get; set; }
        public int StuckTicks { get; set; }
        public int TransientBusyTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int SwingCount { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public int PostRemovalSettleTicks { get; set; }
        public List<float> ObservedHealth { get; } = new();
    }

}
