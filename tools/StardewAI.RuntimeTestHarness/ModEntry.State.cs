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

    private sealed class ActiveNativeTool
    {
        private ActiveNativeTool(PendingExecution pending, string primitiveKind, string locationId, Point target, List<Point> path, Tool tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool? beforeWatered, bool? beforeHadHoeDirt, bool beforeGinger = false, int beforeGingerDebrisCount = 0, int beforeGingerInventoryCount = 0, int beforeForagingExperience = 0, int? beforeHoeDirtState = null, double expectedEnergyCost = 0d)
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
            BeforeGinger = beforeGinger;
            BeforeGingerDebrisCount = beforeGingerDebrisCount;
            BeforeGingerInventoryCount = beforeGingerInventoryCount;
            BeforeForagingExperience = beforeForagingExperience;
            BeforeHoeDirtState = beforeHoeDirtState;
            ExpectedEnergyCost = expectedEnergyCost;
            LastPosition = Game1.player.Position;
            MaxMovementTicks = Math.Max(120, path.Count * 90);
            MaxTicks = MaxMovementTicks + 240;
        }

        public static ActiveNativeTool Water(PendingExecution pending, string locationId, Point target, List<Point> path, WateringCan tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeWatered)
        {
            return new ActiveNativeTool(pending, "water_crop", locationId, target, path, tool, staminaBefore, waterBefore, startedAt, estimatedTicks, requestedEffect, beforeWatered, null);
        }

        public static ActiveNativeTool WaterPetBowl(PendingExecution pending, string locationId, Point target, List<Point> path, WateringCan tool, double staminaBefore, int? waterBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeWatered, double expectedEnergyCost)
        {
            return new ActiveNativeTool(pending, "fill_pet_bowl", locationId, target, path, tool, staminaBefore, waterBefore, startedAt, estimatedTicks, requestedEffect, beforeWatered, null, expectedEnergyCost: expectedEnergyCost);
        }

        public static ActiveNativeTool Till(PendingExecution pending, string locationId, Point target, List<Point> path, Hoe tool, double staminaBefore, string startedAt, int estimatedTicks, string requestedEffect, bool beforeHadHoeDirt)
        {
            return new ActiveNativeTool(pending, "till_soil", locationId, target, path, tool, staminaBefore, null, startedAt, estimatedTicks, requestedEffect, null, beforeHadHoeDirt);
        }

        public static ActiveNativeTool Ginger(PendingExecution pending, string locationId, Point target, List<Point> path, Hoe tool, double staminaBefore, string startedAt, int estimatedTicks, string requestedEffect, int beforeGingerDebrisCount, int beforeGingerInventoryCount, int beforeForagingExperience, int beforeHoeDirtState, double expectedEnergyCost)
        {
            return new ActiveNativeTool(pending, "harvest_ginger", locationId, target, path, tool, staminaBefore, null, startedAt, estimatedTicks, requestedEffect, null, true, true, beforeGingerDebrisCount, beforeGingerInventoryCount, beforeForagingExperience, beforeHoeDirtState, expectedEnergyCost);
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
        public bool BeforeGinger { get; }
        public int BeforeGingerDebrisCount { get; }
        public int BeforeGingerInventoryCount { get; }
        public int BeforeForagingExperience { get; }
        public int? BeforeHoeDirtState { get; }
        public double ExpectedEnergyCost { get; }
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
        public ActiveCatchFish(
            PendingExecution pending,
            Point standTile,
            Point bobberTile,
            FishingRod rod,
            float desiredCastingPower,
            bool maxCastRequested,
            string beforeInventory,
            float beforeStamina,
            int beforeExpectedCaughtCount,
            int beforeFishingExperience,
            int beforeLuckExperience)
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
            BeforeFishingExperience = beforeFishingExperience;
            BeforeLuckExperience = beforeLuckExperience;
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
        public int BeforeFishingExperience { get; }
        public int BeforeLuckExperience { get; }
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

}
