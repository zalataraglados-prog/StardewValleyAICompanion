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
    private sealed class PendingExecution
    {
        public PendingExecution(TrainingExecutionRequest request)
        {
            Request = request;
        }

        public TrainingExecutionRequest Request { get; }
        public TaskCompletionSource<TrainingExecutionResult> Completion { get; } = new();
        public List<SimulatedFactChange> ChangedFacts { get; } = new();
        public int MovementClearanceActions { get; set; }
        public int MovementExtraTicks { get; set; }
    }

    private sealed class ActiveTileMove
    {
        public ActiveTileMove(PendingExecution pending, Point startTile, Point targetTile, List<Point> path, Point? connectorActionTile = null, int? connectorExitDirection = null)
        {
            Pending = pending;
            StartTile = startTile;
            TargetTile = targetTile;
            Path = path;
            ConnectorActionTile = connectorActionTile;
            ConnectorExitDirection = connectorExitDirection;
            LastPosition = Game1.player.Position;
            LocationId = Game1.currentLocation.NameOrUniqueName;
            MaxTicks = Math.Max(120, path.Count * 90);
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public Point StartTile { get; }
        public Point TargetTile { get; }
        public Point? ConnectorActionTile { get; }
        public int? ConnectorExitDirection { get; }
        public List<Point> Path { get; set; }
        public string LocationId { get; }
        public bool AllowsLocationChange => Pending.Request.OptionId == "executor.traverse_connector";
        public bool ConnectorActionAttempted { get; set; }
        public int Tick { get; set; }
        public int PathIndex { get; set; }
        public int? CurrentDirection { get; set; }
        public int StuckTicks { get; set; }
        public int SoftObstacleTicks { get; set; }
        public int MaxTicks { get; }
        public Vector2 LastPosition { get; set; }
        public string StartedAt { get; }
    }

    private sealed class ActiveNativeFarmTool
    {
        private ActiveNativeFarmTool(PendingExecution pending, string primitiveKind, string locationId, Point target, List<Point> path, Tool tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool? beforeWatered, bool? beforeHadHoeDirt)
        {
            Pending = pending;
            PrimitiveKind = primitiveKind;
            LocationId = locationId;
            Target = target;
            Path = path;
            Tool = tool;
            StaminaBefore = staminaBefore;
            WaterBefore = waterBefore;
            StartedAt = startedAt;
            EstimatedTicks = estimatedTicks;
            RequestedEffect = requestedEffect;
            BeforeWatered = beforeWatered;
            BeforeHadHoeDirt = beforeHadHoeDirt;
            LastPosition = Game1.player.Position;
            MaxMovementTicks = Math.Max(120, path.Count * 90);
            MaxTicks = MaxMovementTicks + 240;
        }

        public static ActiveNativeFarmTool Water(PendingExecution pending, string locationId, Point target, List<Point> path, WateringCan tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeWatered)
        {
            return new ActiveNativeFarmTool(pending, "water_crop", locationId, target, path, tool, staminaBefore, waterBefore, startedAt, estimatedTicks, requestedEffect, beforeWatered, null);
        }

        public static ActiveNativeFarmTool Till(PendingExecution pending, string locationId, Point target, List<Point> path, Hoe tool, double staminaBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeHadHoeDirt)
        {
            return new ActiveNativeFarmTool(pending, "till_soil", locationId, target, path, tool, staminaBefore, null, startedAt, estimatedTicks, requestedEffect, null, beforeHadHoeDirt);
        }

        public PendingExecution Pending { get; }
        public string PrimitiveKind { get; }
        public string LocationId { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public Tool Tool { get; }
        public double StaminaBefore { get; }
        public int? WaterBefore { get; }
        public string StartedAt { get; }
        public int EstimatedTicks { get; }
        public string RequestedEffect { get; }
        public bool? BeforeWatered { get; }
        public bool? BeforeHadHoeDirt { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MaxMovementTicks { get; }
        public int MaxTicks { get; }
        public Vector2 LastPosition { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
    }

    private sealed class ActiveCatchFish
    {
        public ActiveCatchFish(PendingExecution pending, Point standTile, Point bobberTile, FishingRod rod, float desiredCastingPower, bool maxCastRequested, string beforeInventory, float beforeStamina, int beforeExpectedCaughtCount)
        {
            Pending = pending;
            StandTile = standTile;
            BobberTile = bobberTile;
            Rod = rod;
            DesiredCastingPower = desiredCastingPower;
            MaxCastRequested = maxCastRequested;
            BeforeInventory = beforeInventory;
            BeforeStamina = beforeStamina;
            BeforeExpectedCaughtCount = beforeExpectedCaughtCount;
        }

        public PendingExecution Pending { get; }
        public Point StandTile { get; }
        public Point BobberTile { get; }
        public FishingRod Rod { get; }
        public float DesiredCastingPower { get; }
        public bool MaxCastRequested { get; }
        public string BeforeInventory { get; }
        public float BeforeStamina { get; }
        public int BeforeExpectedCaughtCount { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 9000;
        public bool SawNibble { get; set; }
        public bool HookIssuedForNibble { get; set; }
        public int HookAttemptCount { get; set; }
        public bool SawTimingCast { get; set; }
        public bool WasTimingCastLastTick { get; set; }
        public bool SawCasting { get; set; }
        public bool SawCastingAir { get; set; }
        public bool SawFishing { get; set; }
        public bool SawPullingOutOfWater { get; set; }
        public bool SawJunkOrSpecialPullWithoutBobberBar { get; set; }
        public Point? LastBobberTile { get; set; }
        public bool SawBobberBar { get; set; }
        public bool SawBobberBarSuccess { get; set; }
        public int BobberBarTicks { get; set; }
        public int BobberInBarTicks { get; set; }
        public int BobberControlTicks { get; set; }
        public int BobberControlPressedTicks { get; set; }
        public float BobberInBarRatio => BobberBarTicks == 0 ? 0f : BobberInBarTicks / (float)BobberBarTicks;
        public float MinDistanceFromCatching { get; set; } = 1f;
        public float LastDistanceFromCatching { get; set; }
        public float TerminalBobberBarProgress { get; set; } = -1f;
        public string TerminalCatchResult { get; set; } = "none";
        public float LastFishPosition { get; set; }
        public float LastFishSpeed { get; set; }
        public float LastBarPosition { get; set; }
        public float LastBarSpeed { get; set; }
        public int LastBarHeight { get; set; }
        public float ObservedPeakCastingPower { get; set; }
        public float ObservedReleaseCastingPower { get; set; } = -1f;
        public bool ObservedMaxCast { get; set; }
        public bool IdleCleanupComplete { get; set; }
        public bool SawFishCaughtHold { get; set; }
        public string ObservedQualifiedItemId { get; set; } = string.Empty;
    }

    private sealed class ActiveMineFishingSetup
    {
        public ActiveMineFishingSetup(PendingExecution pending, int mineLevel, string beforeLocation, MineFishingFixtureFacts prerequisiteFacts)
        {
            Pending = pending;
            MineLevel = mineLevel;
            BeforeLocation = beforeLocation;
            PrerequisiteFacts = prerequisiteFacts;
        }

        public PendingExecution Pending { get; }
        public int MineLevel { get; }
        public string BeforeLocation { get; }
        public MineFishingFixtureFacts PrerequisiteFacts { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 600;
    }

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
            MineShaft mine,
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
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
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
            RequestedEffect = requestedEffect;
            StaminaBefore = Game1.player.Stamina;
            MaxTicks = Math.Max(300, path.Count * 90 + maxSwings * 240);
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            ObservedHealth.Add(healthBefore);
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public ResourceClump Clump { get; }
        public Point Anchor { get; }
        public Point HitTile { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
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
        public string RequestedEffect { get; }
        public double StaminaBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int MovementTiles { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int SwingCount { get; set; }
        public bool BeginIssued { get; set; }
        public bool ReleaseIssued { get; set; }
        public List<float> ObservedHealth { get; } = new();
    }

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

    private sealed class ActiveBreakContainer
    {
        public ActiveBreakContainer(PendingExecution pending, MineShaft mine, Point target, List<Point> path, BreakableContainer container, Tool tool, int healthBefore, int maxSwings, int restoreSlotIndex, string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Path = path;
            Container = container;
            HeavyHitterAction = new NativeHeavyHitterActionState(tool, healthBefore, maxSwings);
            HealthBefore = healthBefore;
            RestoreSlotIndex = restoreSlotIndex;
            RequestedEffect = requestedEffect;
            DebrisCountBefore = mine.debris.Count;
            MaxTicks = Math.Max(300, path.Count * 90 + maxSwings * 180);
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public List<Point> Path { get; }
        public BreakableContainer Container { get; }
        public NativeHeavyHitterActionState HeavyHitterAction { get; }
        public Tool Tool => HeavyHitterAction.Tool;
        public int HealthBefore { get; }
        public int MaxSwings => HeavyHitterAction.MaxSwings;
        public int RestoreSlotIndex { get; }
        public string RequestedEffect { get; }
        public int DebrisCountBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int SwingCount => HeavyHitterAction.SwingCount;
        public bool CombatInterrupted { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public IReadOnlyList<int> ObservedHealth => HeavyHitterAction.ObservedHealth;
    }

    private sealed class ActiveShootMonster
    {
        public ActiveShootMonster(
            PendingExecution pending,
            MineShaft mine,
            Monster target,
            Slingshot slingshot,
            string ammoQualifiedItemId,
            int ammoCountBefore,
            int restoreSlotIndex,
            int maxAttacks,
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Slingshot = slingshot;
            SlingshotSlotIndex = Game1.player.Items.IndexOf(slingshot);
            AmmoQualifiedItemId = ammoQualifiedItemId;
            AmmoCountBefore = ammoCountBefore;
            RestoreSlotIndex = restoreSlotIndex;
            MaxAttacks = maxAttacks;
            RequestedEffect = requestedEffect;
            TargetHealthBefore = target.Health;
            LastTargetHealth = target.Health;
            TargetHealthSequence.Add(target.Health);
            MaxTicks = Math.Clamp(1200 + maxAttacks * 180, 1800, 7200);
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Monster Target { get; }
        public Slingshot Slingshot { get; }
        public int SlingshotSlotIndex { get; }
        public string AmmoQualifiedItemId { get; }
        public int AmmoCountBefore { get; }
        public int RestoreSlotIndex { get; }
        public int MaxAttacks { get; }
        public string RequestedEffect { get; }
        public int TargetHealthBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public bool ButtonHeld { get; set; }
        public int HoldTicks { get; set; }
        public int CooldownTicks { get; set; }
        public bool AimPrepared { get; set; }
        public int AttackCount { get; set; }
        public int HitCount { get; set; }
        public int LastTargetHealth { get; set; }
        public List<int> TargetHealthSequence { get; } = new();
    }

    private sealed class ActivePlaceBomb
    {
        public ActivePlaceBomb(
            PendingExecution pending,
            MineShaft mine,
            Point target,
            Point stand,
            Point escape,
            List<Point> path,
            int bombSlotIndex,
            StardewValley.Object bomb,
            int radius,
            int restoreSlotIndex,
            int objectCountBefore,
            Monster? targetMonster,
            string terminalState,
            string requestedEffect)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            Stand = stand;
            Escape = escape;
            Path = path;
            BombSlotIndex = bombSlotIndex;
            BombQualifiedItemId = bomb.QualifiedItemId;
            BombStackBefore = bomb.Stack;
            Radius = radius;
            RestoreSlotIndex = restoreSlotIndex;
            ObjectCountBefore = objectCountBefore;
            TargetMonster = targetMonster;
            TerminalState = terminalState;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public Point Escape { get; }
        public List<Point> Path { get; }
        public List<Point> EscapePath { get; set; } = new();
        public int BombSlotIndex { get; }
        public string BombQualifiedItemId { get; }
        public int BombStackBefore { get; }
        public int Radius { get; }
        public int RestoreSlotIndex { get; }
        public int ObjectCountBefore { get; }
        public Monster? TargetMonster { get; }
        public string TerminalState { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public int PlacedAtTick { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public PlaceBombStage Stage { get; set; }
    }

    private enum PlaceBombStage
    {
        MoveToPlacement,
        AimPlacement,
        PressPlacement,
        ReleasePlacement,
        Escape,
        WaitForExplosion
    }

    private sealed class ActiveCombatMonster
    {
        public ActiveCombatMonster(PendingExecution pending, string locationId, Monster target, MeleeWeapon weapon, int maxAttacks, int maxMovementTiles, bool manualMovement, string terminalState, string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            Target = target;
            Weapon = weapon;
            TargetRuntimeType = target.GetType().FullName ?? target.GetType().Name;
            TargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target).ToString("X8");
            TargetName = target.Name;
            TargetHealthBefore = target.Health;
            PlayerHealthBefore = Game1.player.health;
            MaxAttacks = maxAttacks;
            MaxMovementTiles = maxMovementTiles;
            ManualMovement = manualMovement;
            TerminalState = terminalState;
            RequestedEffect = requestedEffect;
            MaxTicks = Math.Clamp(1200 + maxAttacks * 120, 1800, 7200);
            LastProgressPosition = Game1.player.Position;
            LastMovementPosition = Game1.player.Position;
            LastMovementTile = Game1.player.TilePoint;
            LastProgressTargetHealth = target.Health;
            InventoryBefore = InventoryStackSignature();
            TargetHealthSequence.Add(target.Health);
            PlayerHealthSequence.Add(Game1.player.health);
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public Monster Target { get; private set; }
        public MeleeWeapon Weapon { get; }
        public string TargetRuntimeType { get; private set; }
        public string TargetRuntimeIdentity { get; private set; }
        public string TargetName { get; private set; }
        public int TargetHealthBefore { get; private set; }
        public int PlayerHealthBefore { get; }
        public int MaxAttacks { get; }
        public int MaxMovementTiles { get; }
        public bool ManualMovement { get; }
        public string TerminalState { get; }
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
        public string InventoryBefore { get; }
        public Point? ClearanceTarget { get; set; }
        public Tool? ClearanceTool { get; set; }
        public string ClearanceBefore { get; set; } = string.Empty;
        public bool ClearanceButtonHeld { get; set; }
        public int ClearanceSwings { get; set; }
        public string LastNoProgressReason { get; set; } = string.Empty;
        public List<int> TargetHealthSequence { get; } = new();
        public List<int> PlayerHealthSequence { get; } = new();

        public void Retarget(Monster target)
        {
            Target = target;
            TargetRuntimeType = target.GetType().FullName ?? target.GetType().Name;
            TargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target).ToString("X8");
            TargetName = target.Name;
            TargetHealthBefore = target.Health;
            TargetHealthSequence.Clear();
            TargetHealthSequence.Add(target.Health);
        }
    }

    private sealed class ActiveConsumeFood
    {
        public ActiveConsumeFood(PendingExecution pending, string locationId, int foodSlotIndex, string foodQualifiedItemId, int foodStackBefore, int restoreSlotIndex, int healthBefore, double energyBefore, string requestedEffect)
        {
            Pending = pending;
            LocationId = locationId;
            FoodSlotIndex = foodSlotIndex;
            FoodQualifiedItemId = foodQualifiedItemId;
            FoodStackBefore = foodStackBefore;
            RestoreSlotIndex = restoreSlotIndex;
            HealthBefore = healthBefore;
            EnergyBefore = energyBefore;
            RequestedEffect = requestedEffect;
        }

        public PendingExecution Pending { get; }
        public string LocationId { get; }
        public int FoodSlotIndex { get; }
        public string FoodQualifiedItemId { get; }
        public int FoodStackBefore { get; }
        public int RestoreSlotIndex { get; }
        public int HealthBefore { get; }
        public double EnergyBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 900;
        public int ElapsedTicks { get; set; }
        public ConsumeFoodStage Stage { get; set; }
        public bool RightButtonHeld { get; set; }
        public bool NativeConfirmationIssued { get; set; }
        public bool EatingObserved { get; set; }
    }

    private sealed class ActivePickupDebris
    {
        public ActivePickupDebris(PendingExecution pending, GameLocation location, Debris debris, Chunk chunk, Point initialTarget, string qualifiedItemId, int debrisCountBefore, int chunkCountBefore, int itemCountBefore, string inventoryBefore, string requestedEffect)
        {
            Pending = pending;
            Location = location;
            LocationId = location.NameOrUniqueName;
            Debris = debris;
            Chunk = chunk;
            PathTarget = initialTarget;
            QualifiedItemId = qualifiedItemId;
            DebrisCountBefore = debrisCountBefore;
            ChunkCountBefore = chunkCountBefore;
            ItemCountBefore = itemCountBefore;
            InventoryBefore = inventoryBefore;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public string LocationId { get; }
        public Debris Debris { get; }
        public Chunk Chunk { get; }
        public string QualifiedItemId { get; }
        public int DebrisCountBefore { get; }
        public int ChunkCountBefore { get; }
        public int ItemCountBefore { get; }
        public string InventoryBefore { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public List<Point> Path { get; set; } = new();
        public int PathIndex { get; set; }
        public Point PathTarget { get; set; }
        public int PathFailures { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int WaitAtTargetTicks { get; set; }
    }

    private sealed class ActiveDescendLadder
    {
        public ActiveDescendLadder(
            PendingExecution pending,
            MineShaft mineBefore,
            int mineLevelBefore,
            Point target,
            List<Point> path,
            int maxMovementTiles,
            Point? requestedStand,
            string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            Target = target;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            RequestedStand = requestedStand;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public int MaxMovementTiles { get; }
        public Point? RequestedStand { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool ActionIssued { get; set; }
    }

    private sealed class ActiveDescendShaft
    {
        public ActiveDescendShaft(
            PendingExecution pending,
            MineShaft mineBefore,
            int mineLevelBefore,
            int healthBefore,
            Point target,
            List<Point> path,
            int maxMovementTiles,
            Point? requestedStand,
            int expectedMineLevelDelta,
            int expectedMineLevelAfter,
            int expectedHealthCost,
            int expectedHealthAfter,
            string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            HealthBefore = healthBefore;
            Target = target;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            RequestedStand = requestedStand;
            ExpectedMineLevelDelta = expectedMineLevelDelta;
            ExpectedMineLevelAfter = expectedMineLevelAfter;
            ExpectedHealthCost = expectedHealthCost;
            ExpectedHealthAfter = expectedHealthAfter;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public int HealthBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public int MaxMovementTiles { get; }
        public Point? RequestedStand { get; }
        public int ExpectedMineLevelDelta { get; }
        public int ExpectedMineLevelAfter { get; }
        public int ExpectedHealthCost { get; }
        public int ExpectedHealthAfter { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool PromptOpened { get; set; }
        public bool DialogueConfirmed { get; set; }
        public bool FallDialogueSeen { get; set; }
        public bool FallDialogueButtonHeld { get; set; }
        public int FallDialoguePressAttempts { get; set; }
    }

    private sealed class ActiveExitMine
    {
        public ActiveExitMine(
            PendingExecution pending,
            MineShaft mineBefore,
            int mineLevelBefore,
            int timeBefore,
            int healthBefore,
            float energyBefore,
            Point playerTileBefore,
            Point target,
            List<Point> path,
            int maxMovementTiles,
            Point? requestedStand,
            string expectedLocationId,
            int expectedTileX,
            int expectedTileY,
            string retreatReason,
            string requestedEffect)
        {
            Pending = pending;
            MineBefore = mineBefore;
            MineLevelBefore = mineLevelBefore;
            TimeBefore = timeBefore;
            HealthBefore = healthBefore;
            EnergyBefore = energyBefore;
            PlayerTileBefore = playerTileBefore;
            Target = target;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            RequestedStand = requestedStand;
            ExpectedLocationId = expectedLocationId;
            ExpectedTileX = expectedTileX;
            ExpectedTileY = expectedTileY;
            RetreatReason = retreatReason;
            RequestedEffect = requestedEffect;
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public MineShaft MineBefore { get; }
        public int MineLevelBefore { get; }
        public int TimeBefore { get; }
        public int HealthBefore { get; }
        public float EnergyBefore { get; }
        public Point PlayerTileBefore { get; }
        public Point Target { get; }
        public List<Point> Path { get; set; }
        public int MaxMovementTiles { get; }
        public Point? RequestedStand { get; }
        public string ExpectedLocationId { get; }
        public int ExpectedTileX { get; }
        public int ExpectedTileY { get; }
        public string RetreatReason { get; }
        public string RequestedEffect { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
        public int CombatInterruptedTicks { get; set; }
        public bool CombatInterrupted { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public bool PromptOpened { get; set; }
        public bool DialogueConfirmed { get; set; }
    }

    private enum ConsumeFoodStage
    {
        PressUse,
        ReleaseUse,
        WaitForPrompt,
        ConfirmPrompt,
        WaitForCompletion
    }

    private sealed class ActiveMineSetup
    {
        public ActiveMineSetup(
            PendingExecution pending,
            int mineLevel,
            string expectedMineKind,
            string beforeLocation,
            MiningCalibrationLoadoutFacts calibrationLoadout,
            bool createForcedShaft)
        {
            Pending = pending;
            MineLevel = mineLevel;
            ExpectedMineKind = expectedMineKind;
            BeforeLocation = beforeLocation;
            CalibrationLoadout = calibrationLoadout;
            CreateForcedShaft = createForcedShaft;
        }

        public PendingExecution Pending { get; }
        public int MineLevel { get; }
        public string ExpectedMineKind { get; }
        public string BeforeLocation { get; }
        public MiningCalibrationLoadoutFacts CalibrationLoadout { get; }
        public bool CreateForcedShaft { get; }
        public bool ShaftCreationIssued { get; set; }
        public Point? ShaftTile { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 600;
    }

    private sealed class ActiveQuarrySetup
    {
        public ActiveQuarrySetup(
            PendingExecution pending,
            string beforeLocation,
            MiningCalibrationLoadoutFacts calibrationLoadout,
            GoldenScytheFixtureFacts fixture)
        {
            Pending = pending;
            BeforeLocation = beforeLocation;
            CalibrationLoadout = calibrationLoadout;
            Fixture = fixture;
        }

        public PendingExecution Pending { get; }
        public string BeforeLocation { get; }
        public MiningCalibrationLoadoutFacts CalibrationLoadout { get; }
        public GoldenScytheFixtureFacts Fixture { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 1800;
    }

    private sealed class ActiveVolcanoSetup
    {
        public ActiveVolcanoSetup(
            PendingExecution pending,
            int level,
            string beforeLocation,
            VolcanoCalibrationLoadoutFacts calibrationLoadout)
        {
            Pending = pending;
            Level = level;
            BeforeLocation = beforeLocation;
            CalibrationLoadout = calibrationLoadout;
        }

        public PendingExecution Pending { get; }
        public int Level { get; }
        public string BeforeLocation { get; }
        public VolcanoCalibrationLoadoutFacts CalibrationLoadout { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1800;
        public int ElapsedTicks { get; set; }
    }

    private sealed record MiningCalibrationLoadoutFacts(
        bool Enabled,
        int WeaponSlot,
        string WeaponQualifiedItemId,
        int WeaponMaxDamage,
        int FoodSlot,
        string FoodQualifiedItemId,
        int FoodHealthRecovery,
        int FoodStack)
    {
        public static MiningCalibrationLoadoutFacts Disabled { get; } = new(false, -1, string.Empty, 0, -1, string.Empty, 0, 0);
    }

    private sealed record GoldenScytheFixtureFacts(
        bool ResetEnabled,
        bool ClaimedBefore,
        int CountBefore,
        bool ClaimedAfterReset,
        int CountAfterReset,
        int EmptySlotsAfterReset)
    {
        public string ToAuditString()
        {
            return "reset_enabled=" + ResetEnabled.ToString().ToLowerInvariant() +
                ";claimed_before=" + ClaimedBefore.ToString().ToLowerInvariant() +
                ";count_before=" + CountBefore +
                ";claimed_after_reset=" + ClaimedAfterReset.ToString().ToLowerInvariant() +
                ";count_after_reset=" + CountAfterReset +
                ";empty_slots_after_reset=" + EmptySlotsAfterReset;
        }
    }

    private sealed record VolcanoCalibrationLoadoutFacts(
        bool Enabled,
        int PickaxeSlot,
        string PickaxeQualifiedItemId,
        int PickaxeUpgradeLevel,
        int WateringCanSlot,
        string WateringCanQualifiedItemId,
        int WaterLeft,
        int WeaponSlot,
        string WeaponQualifiedItemId,
        int WeaponMaximumDamage,
        int FoodSlot,
        string FoodQualifiedItemId,
        int FoodStack)
    {
        public static VolcanoCalibrationLoadoutFacts Disabled { get; } = new(
            false,
            -1,
            string.Empty,
            0,
            -1,
            string.Empty,
            0,
            -1,
            string.Empty,
            0,
            -1,
            string.Empty,
            0);

        public string ToAuditString()
        {
            return "enabled=" + Enabled.ToString().ToLowerInvariant() +
                ";pickaxe_slot=" + PickaxeSlot +
                ";pickaxe=" + PickaxeQualifiedItemId +
                ";pickaxe_upgrade=" + PickaxeUpgradeLevel +
                ";watering_can_slot=" + WateringCanSlot +
                ";watering_can=" + WateringCanQualifiedItemId +
                ";water_left=" + WaterLeft +
                ";weapon_slot=" + WeaponSlot +
                ";weapon=" + WeaponQualifiedItemId +
                ";weapon_max_damage=" + WeaponMaximumDamage +
                ";food_slot=" + FoodSlot +
                ";food=" + FoodQualifiedItemId +
                ";food_stack=" + FoodStack;
        }
    }

    private sealed record MineFishingFixtureFacts(MineFishingFixtureSnapshot Before, MineFishingFixtureSnapshot After);

    private sealed record MineFishingFixtureSnapshot(
        int BackpackMaxItems,
        int BackpackEmptySlots,
        int SelectedRodSlot,
        string SelectedRodQualifiedItemId,
        int SelectedRodUpgradeLevel,
        int SelectedRodAttachmentSlots,
        string SpecificBaitTargetItemId,
        string BaitInternalName,
        bool LavaEelNativeNameCondition,
        bool CuriosityLureEquipped,
        bool CorkBobberEquipped,
        float Stamina);

    private sealed class ActiveSleep
    {
        public ActiveSleep(PendingExecution pending, Point startTile, Point bedTile, Point standTile, List<Point> path, int startYear, int startDay, int startTime, string startSeason)
        {
            Pending = pending;
            StartTile = startTile;
            BedTile = bedTile;
            StandTile = standTile;
            Path = path;
            StartYear = startYear;
            StartDay = startDay;
            StartTime = startTime;
            StartSeason = startSeason;
            MaxTicks = Math.Max(600, path.Count * 90 + 600);
            LastPosition = Game1.player.Position;
        }

        public PendingExecution Pending { get; }
        public Point StartTile { get; }
        public Point BedTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int StartYear { get; }
        public int StartDay { get; }
        public int StartTime { get; }
        public string StartSeason { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public SleepStage Stage { get; set; } = SleepStage.MoveToStand;
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public Vector2 LastPosition { get; set; }
        public int PromptWaitTicks { get; set; }
        public int PostSleepWaitTicks { get; set; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public ShipSummaryClosePhase SummaryPhase { get; set; }
        public int SummaryPhaseStartTick { get; set; }
        public bool SummaryPositionSet { get; set; }
        public bool SummaryPositionVerified { get; set; }
        public Point SummaryPositionTarget { get; set; }
        public bool SummaryButtonPressed { get; set; }
        public bool SummaryButtonReleased { get; set; }
        public int SummaryReleaseRetries { get; set; }
    }

    private enum ShipSummaryClosePhase
    {
        WaitReady,
        Position,
        PositionVerify,
        Press,
        Release,
        WaitClose
    }

    private enum SleepStage
    {
        MoveToStand,
        StepOntoSleepTouchTile,
        TriggerPrompt,
        ConfirmPrompt,
        WaitForNewDay,
        WaitForPostSleepStable
    }

    private sealed class SleepTarget
    {
        public SleepTarget(Point bedTile, Point standTile)
        {
            BedTile = bedTile;
            StandTile = standTile;
        }

        public Point BedTile { get; }
        public Point StandTile { get; }
    }

    private sealed class ActiveWait
    {
        public ActiveWait(PendingExecution pending, int targetTicks)
        {
            Pending = pending;
            TargetTicks = targetTicks;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public int TargetTicks { get; }
        public int ElapsedTicks { get; set; }
        public string StartedAt { get; }
    }

    private enum ShipPhase
    {
        BinPosition,
        BinPositionVerify,
        BinPress,
        BinRelease,
        WaitForShippingMenu,
        SlotPosition,
        SlotPositionVerify,
        SlotPress,
        SlotRelease,
        WaitForSlotDispatch,
        VerifyAndClose
    }

    private enum DialogueAdvanceStage
    {
        WaitTransition,
        Press,
        ReleaseAfterAdvance,
        WaitAdvanceEffect,
        CheckClose
    }

    private enum SkullKeyChestStage
    {
        OpenChest,
        WaitForOpenAnimation,
        ClaimItem,
        WaitForPostcondition
    }

    private sealed class ActiveSkullKeyChestInteraction
    {
        public ActiveSkullKeyChestInteraction(PendingExecution pending, MineShaft mine, Point target)
        {
            Pending = pending;
            Mine = mine;
            Target = target;
            HasSkullKeyBefore = Game1.player.hasSkullKey;
        }

        public PendingExecution Pending { get; }
        public MineShaft Mine { get; }
        public Point Target { get; }
        public bool HasSkullKeyBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 360;
        public int ElapsedTicks { get; set; }
        public int StageStartedAtTick { get; set; }
        public int ClaimAttempts { get; set; }
        public bool OpenHandled { get; set; }
        public bool ClaimHandled { get; set; }
        public int KeyObservedAtTick { get; set; }
        public int LastDismissAttemptTick { get; set; }
        public int DismissAttempts { get; set; }
        public bool DismissActionHeld { get; set; }
        public SkullKeyChestStage Stage { get; set; }
    }

    private sealed class ActiveDialogueAdvance
    {
        public ActiveDialogueAdvance(PendingExecution pending, DialogueBox initialMenu)
        {
            Pending = pending;
            InitialMenu = initialMenu;
            InitialSpeakerName = initialMenu.characterDialogue?.speaker?.Name ?? string.Empty;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            MaxTicks = 600;
            MaxPressAttempts = 60;
            BeforeMenuType = "DialogueBox";
            BeforeIsQuestion = initialMenu.isQuestion;
            BeforeResponseCount = initialMenu.responses?.Length ?? 0;
            BeforeSpeakerName = InitialSpeakerName;
            BeforeEventUp = Game1.eventUp;
        }

        public PendingExecution Pending { get; }
        public DialogueBox InitialMenu { get; }
        public string InitialSpeakerName { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; }
        public int MaxPressAttempts { get; }
        public int PressAttempts { get; set; }
        public int AdvanceWaitTicks { get; set; }
        public int TransitionWaitTicks { get; set; }
        public int CheckCloseTicks { get; set; }
        public DialogueAdvanceStage Stage { get; set; } = DialogueAdvanceStage.WaitTransition;
        public bool SawDialogueFinishedBeforePress { get; set; }
        public bool SawShowTypingBeforePress { get; set; }
        public bool SawTransitioningBeforePress { get; set; }
        public string BeforeMenuType { get; }
        public bool BeforeIsQuestion { get; }
        public int BeforeResponseCount { get; }
        public string BeforeSpeakerName { get; }
        public bool BeforeEventUp { get; }
    }

    private sealed class ActiveShipInventoryToBin
    {
        public ActiveShipInventoryToBin(PendingExecution pending, ShippingBin bin, int slotIndex,
            string qualifiedItemId, string unqualifiedItemId, int quantity,
            int inventoryCountBefore, int binCountBefore, int binTotalCountBefore,
            int binDistinctCountBefore, string binSignatureBefore, int basicShippedCountBefore,
            string beforeSlotQualifiedId, int beforeSlotStack, string beforeSlotItemId)
        {
            Pending = pending;
            Bin = bin;
            SlotIndex = slotIndex;
            QualifiedItemId = qualifiedItemId;
            UnqualifiedItemId = unqualifiedItemId;
            Quantity = quantity;
            InventoryCountBefore = inventoryCountBefore;
            BinCountBefore = binCountBefore;
            BinTotalCountBefore = binTotalCountBefore;
            BinDistinctCountBefore = binDistinctCountBefore;
            BinSignatureBefore = binSignatureBefore;
            BasicShippedCountBefore = basicShippedCountBefore;
            BeforeSlotQualifiedId = beforeSlotQualifiedId;
            BeforeSlotStack = beforeSlotStack;
            BeforeSlotItemId = beforeSlotItemId;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
            Phase = ShipPhase.BinPosition;
            PhaseStartTick = 0;
        }

        public PendingExecution Pending { get; }
        public ShippingBin Bin { get; }
        public int SlotIndex { get; }
        public string QualifiedItemId { get; }
        public string UnqualifiedItemId { get; }
        public int Quantity { get; }
        public int InventoryCountBefore { get; }
        public int BinCountBefore { get; }
        public int BinTotalCountBefore { get; }
        public int BinDistinctCountBefore { get; }
        public string BinSignatureBefore { get; }
        public int BasicShippedCountBefore { get; }
        public string BeforeSlotQualifiedId { get; }
        public int BeforeSlotStack { get; }
        public string BeforeSlotItemId { get; }
        public string StartedAt { get; }
        public ShipPhase Phase { get; set; }
        public int PhaseStartTick { get; set; }
        public bool PositionSet { get; set; }
        public bool PositionVerified { get; set; }
        public Point PositionTarget { get; set; }
        public bool ButtonPressed { get; set; }
        public bool ButtonReleased { get; set; }
        public int ReleaseRetries { get; set; }
        public bool SawShippingMenu { get; set; }
        public bool SlotClickDispatched { get; set; }
        public bool SawShipDispatch { get; set; }
        public int AfterSlotStack { get; set; }
        public string AfterSlotQualifiedId { get; set; } = string.Empty;
        public int ElapsedTicks { get; set; }
        public int MaxTicks { get; } = 300;
    }

    private sealed class ShippingReceipt
    {
        [System.Text.Json.Serialization.JsonPropertyName("receipt_id")]
        public string ReceiptId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [System.Text.Json.Serialization.JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("feedback_appended")]
        public bool FeedbackAppended { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("unqualified_item_id")]
        public string UnqualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source_date")]
        public string SourceDate { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_season")]
        public string SourceSeason { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_day_of_month")]
        public string SourceDayOfMonth { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source_year")]
        public string SourceYear { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_basic_shipped_count")]
        public int PreBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_inventory_count")]
        public int PreInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_count")]
        public int PreBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_total")]
        public int PreBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_distinct")]
        public int PreBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_bin_signature")]
        public string PreBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_stack")]
        public int PreSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pre_slot_qualified_id")]
        public string PreSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_inventory_count")]
        public int AfterInventoryCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_count")]
        public int AfterBinCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_total")]
        public int AfterBinTotal { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_distinct")]
        public int AfterBinDistinct { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_bin_signature")]
        public string AfterBinSignature { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_stack")]
        public int AfterSlotStack { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("after_slot_qualified_id")]
        public string AfterSlotQualifiedId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("slot_index")]
        public int SlotIndex { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_at")]
        public string? SettledAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_status")]
        public string? SettlementStatus { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settlement_reason")]
        public string? SettlementReason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_basic_shipped_count")]
        public int? SettledBasicShippedCount { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_game_date")]
        public string? SettledGameDate { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_season")]
        public string? SettledSeason { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_day_of_month")]
        public string? SettledDayOfMonth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("settled_year")]
        public string? SettledYear { get; set; }
    }
}
