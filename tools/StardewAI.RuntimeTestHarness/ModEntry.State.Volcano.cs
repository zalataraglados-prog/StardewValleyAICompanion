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
    private sealed class ActiveVolcanoCoolLava
    {
        public ActiveVolcanoCoolLava(
            PendingExecution pending,
            VolcanoDungeon volcano,
            Point target,
            List<Point> path,
            WateringCan wateringCan,
            int wateringCanSlotIndex,
            int restoreSlotIndex,
            int waterBefore,
            double staminaBefore,
            int maxMovementTiles,
            string requestedEffect)
        {
            Pending = pending;
            Volcano = volcano;
            Target = target;
            Path = path;
            WateringCan = wateringCan;
            WateringCanSlotIndex = wateringCanSlotIndex;
            RestoreSlotIndex = restoreSlotIndex;
            WaterBefore = waterBefore;
            StaminaBefore = staminaBefore;
            MaxMovementTiles = maxMovementTiles;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Max(360, maxMovementTiles * 90 + 360);
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public VolcanoDungeon Volcano { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public WateringCan WateringCan { get; }
        public int WateringCanSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public int WaterBefore { get; }
        public double StaminaBefore { get; }
        public int MaxMovementTiles { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public int CompletionWaitTicks { get; set; }
    }

    private sealed class ActiveVolcanoObstacle
    {
        public ActiveVolcanoObstacle(
            PendingExecution pending,
            VolcanoDungeon volcano,
            Point target,
            Point stand,
            List<Point> path,
            StardewValley.Object targetObject,
            Tool tool,
            int toolSlotIndex,
            int restoreSlotIndex,
            bool isStone,
            int healthBefore,
            double staminaBefore,
            int maxSwings,
            int maxMovementTiles,
            string requestedEffect)
        {
            Pending = pending;
            Volcano = volcano;
            Target = target;
            Stand = stand;
            Path = path;
            TargetObject = targetObject;
            Tool = tool;
            HeavyHitterAction = isStone ? null : new NativeHeavyHitterActionState(tool, healthBefore, maxSwings);
            ToolSlotIndex = toolSlotIndex;
            RestoreSlotIndex = restoreSlotIndex;
            IsStone = isStone;
            HealthBefore = healthBefore;
            StaminaBefore = staminaBefore;
            MaxSwings = maxSwings;
            MaxMovementTiles = maxMovementTiles;
            RequestedEffect = requestedEffect;
            DebrisCountBefore = volcano.debris.Count;
            MaxTicks = Math.Max(360, maxMovementTiles * 90 + maxSwings * 240);
            LastPosition = Game1.player.Position;
            if (isStone)
            {
                ObservedHealth.Add(healthBefore);
            }
        }

        public PendingExecution Pending { get; }
        public VolcanoDungeon Volcano { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public StardewValley.Object TargetObject { get; }
        public Tool Tool { get; }
        public NativeHeavyHitterActionState? HeavyHitterAction { get; }
        public int ToolSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public bool IsStone { get; }
        public int HealthBefore { get; }
        public double StaminaBefore { get; }
        public int MaxSwings { get; }
        public int MaxMovementTiles { get; }
        public int DebrisCountBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int SwingCount { get; set; }
        public int EffectiveSwingCount => HeavyHitterAction?.SwingCount ?? SwingCount;
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public List<int> ObservedHealth { get; } = new();
        public IReadOnlyList<int> EffectiveObservedHealth => HeavyHitterAction?.ObservedHealth ?? ObservedHealth;
    }

    private sealed class ActiveVolcanoCombat
    {
        public ActiveVolcanoCombat(
            PendingExecution pending,
            VolcanoDungeon volcano,
            Monster target,
            MeleeWeapon weapon,
            int weaponSlotIndex,
            int restoreSlotIndex,
            int maxAttacks,
            int maxMovementTiles,
            string requestedEffect)
        {
            Pending = pending;
            Volcano = volcano;
            Target = target;
            Weapon = weapon;
            WeaponSlotIndex = weaponSlotIndex;
            RestoreSlotIndex = restoreSlotIndex;
            TargetRuntimeType = target.GetType().FullName ?? target.GetType().Name;
            TargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target).ToString("X8");
            TargetName = target.Name;
            TargetHealthBefore = target.Health;
            PlayerHealthBefore = Game1.player.health;
            MaxAttacks = maxAttacks;
            MaxMovementTiles = maxMovementTiles;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Clamp(1200 + maxAttacks * 120, 1800, 7200);
            LastProgressPosition = Game1.player.Position;
            LastMovementPosition = Game1.player.Position;
            LastMovementTile = Game1.player.TilePoint;
            LastProgressTargetHealth = target.Health;
            TargetHealthSequence.Add(target.Health);
            PlayerHealthSequence.Add(Game1.player.health);
        }

        public PendingExecution Pending { get; }
        public VolcanoDungeon Volcano { get; }
        public Monster Target { get; }
        public MeleeWeapon Weapon { get; }
        public int WeaponSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public string TargetRuntimeType { get; }
        public string TargetRuntimeIdentity { get; }
        public string TargetName { get; }
        public int TargetHealthBefore { get; }
        public int PlayerHealthBefore { get; }
        public int MaxAttacks { get; }
        public int MaxMovementTiles { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int MovementTiles { get; set; }
        public List<Point> Path { get; set; } = new();
        public int PathIndex { get; set; }
        public Point PathTarget { get; set; } = new(-1, -1);
        public int PathFailures { get; set; }
        public int StuckTicks { get; set; }
        public bool AttackButtonHeld { get; set; }
        public int AttackCount { get; set; }
        public int HitCount { get; set; }
        public Vector2 LastProgressPosition { get; set; }
        public Vector2 LastMovementPosition { get; set; }
        public Point LastMovementTile { get; set; }
        public int LastProgressTargetHealth { get; set; }
        public int NoProgressTicks { get; set; }
        public List<int> TargetHealthSequence { get; } = new();
        public List<int> PlayerHealthSequence { get; } = new();
    }

}
