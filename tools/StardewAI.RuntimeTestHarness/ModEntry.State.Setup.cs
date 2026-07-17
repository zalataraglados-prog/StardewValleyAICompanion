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

}
