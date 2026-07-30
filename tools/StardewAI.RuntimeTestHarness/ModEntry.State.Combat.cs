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
            string combatIntent,
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
            CombatIntent = combatIntent;
            RequestedEffect = requestedEffect;
            InitialTargetTile = target.TilePoint;
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
        public string CombatIntent { get; }
        public Point InitialTargetTile { get; }
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
        public ActiveCombatMonster(
            PendingExecution pending,
            string locationId,
            Monster target,
            MeleeWeapon weapon,
            int maxAttacks,
            int maxMovementTiles,
            bool manualMovement,
            int restoreSlotIndex,
            string terminalState,
            string combatIntent,
            string requestedEffect)
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
            RestoreSlotIndex = restoreSlotIndex;
            TerminalState = terminalState;
            CombatIntent = combatIntent;
            RequestedEffect = requestedEffect;
            InitialTargetTile = target.TilePoint;
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
        public int RestoreSlotIndex { get; }
        public string TerminalState { get; }
        public string CombatIntent { get; }
        public Point InitialTargetTile { get; }
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
        public bool ConfirmationButtonHeld { get; set; }
        public bool NativeConfirmationIssued { get; set; }
        public bool EatingObserved { get; set; }
        public int PreInputSettleTicks { get; set; }
    }

    private sealed class ActiveEmergencyCombatFood
    {
        public ActiveEmergencyCombatFood(
            GameLocation location,
            int slotIndex,
            string qualifiedItemId,
            int stackBefore,
            int restoreSlotIndex,
            int healthBefore)
        {
            Location = location;
            SlotIndex = slotIndex;
            QualifiedItemId = qualifiedItemId;
            StackBefore = stackBefore;
            RestoreSlotIndex = restoreSlotIndex;
            HealthBefore = healthBefore;
        }

        public GameLocation Location { get; }
        public int SlotIndex { get; }
        public string QualifiedItemId { get; }
        public int StackBefore { get; }
        public int RestoreSlotIndex { get; }
        public int HealthBefore { get; }
        public ConsumeFoodStage Stage { get; set; }
        public int ElapsedTicks { get; set; }
        public int SettleTicks { get; set; }
        public int CompletionSettleTicks { get; set; }
        public bool RightButtonHeld { get; set; }
        public bool ConfirmationButtonHeld { get; set; }
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
        public int TransientBusyTicks { get; set; }
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
            MaxTicks = Math.Max(
                1800,
                maxMovementTiles * 90 + 600);
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
        public int MaxTicks { get; }
        public int ElapsedTicks { get; set; }
        public int PreMoveSettleTicks { get; set; }
        public bool PostClaimDialogueButtonHeld { get; set; }
        public int PostClaimDialoguePressAttempts { get; set; }
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
        ReleaseConfirmation,
        WaitForPromptClose,
        WaitForCompletion
    }

}
