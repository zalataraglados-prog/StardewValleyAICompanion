using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public static class MiningFloorStepKinds
    {
        public const string DescendLadder = "descend_ladder";
        public const string DescendShaft = "descend_shaft";
        public const string ExitMine = "exit_mine";
        public const string MineStone = "mine_stone";
        public const string BreakContainer = "break_container";
        public const string BreakResourceClump = "break_resource_clump";
        public const string CombatMonster = "combat_monster";
        public const string ShootMonster = "shoot_monster";
        public const string PlaceBomb = "place_bomb";
        public const string PickupDebris = "pickup_debris";
        public const string ConsumeFood = "consume_food";
        public const string MoveToGoldenScytheAltar = "move_to_golden_scythe_altar";
        public const string ClaimGoldenScythe = "claim_golden_scythe";
        public const string MoveToSkullKeyChest = "move_to_skull_key_chest";
        public const string ClaimSkullKey = "claim_skull_key";
        public const string ClaimRewardChest = "claim_reward_chest";
        public const string Blocked = "blocked";
    }

    public static class MiningObjectiveKinds
    {
        public const string ReachDepth = "reach_depth";
        public const string CollectResourceOrArtifact = "collect_resource_or_artifact";
        public const string CollectMonsterDrop = "collect_monster_drop";
        public const string SlayNamedMonster = "slay_named_monster";
        public const string AcquireGoldenScythe = "acquire_golden_scythe";
        public const string AcquireSkullKey = "acquire_skull_key";
    }

    public sealed class MiningFloorObjective
    {
        public string Kind { get; set; } = MiningObjectiveKinds.ReachDepth;

        public string[] TargetQualifiedItemIds { get; set; } = Array.Empty<string>();

        public string[] TargetSourceQualifiedItemIds { get; set; } = Array.Empty<string>();

        public string[] TargetMonsterNameFragments { get; set; } = Array.Empty<string>();

        public bool MatchAnySlimeName { get; set; }

        public int MinimumReserveHealth { get; set; }

        public int ThreatRadiusTiles { get; set; } = 3;

        public int? LatestExitTime { get; set; }

        public int? MinimumReserveEnergy { get; set; }

        public int? TargetDepth { get; set; }
    }

    public sealed class MiningPathTile
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class MiningFloorStepPlan
    {
        public string Status { get; set; } = "blocked";

        public string StepKind { get; set; } = MiningFloorStepKinds.Blocked;

        public string Reason { get; set; } = string.Empty;

        public int? TargetTileX { get; set; }

        public int? TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int EstimatedMovementTiles { get; set; }

        public int EstimatedToolSwings { get; set; }

        public bool DeterministicLadderAfterBreak { get; set; }

        public string TargetRuntimeIdentity { get; set; } = string.Empty;

        public string TargetRuntimeType { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string RequiredWeaponEnchantmentRuntimeType { get; set; } = string.Empty;

        public int? CombatWeaponSlotIndex { get; set; }

        public string CombatMethod { get; set; } = string.Empty;

        public string CombatTerminalState { get; set; } = string.Empty;

        public string SkillExperienceSkillId { get; set; } = string.Empty;

        public int? ExpectedSkillExperience { get; set; }

        public int? SkillExperienceMinimum { get; set; }

        public int? SkillExperienceMaximum { get; set; }

        public string SkillExperienceCondition { get; set; } = string.Empty;

        public string SkillExperienceProjectionStatus { get; set; } = string.Empty;

        public string SecondarySkillExperienceSkillId { get; set; } = string.Empty;

        public int? SecondarySkillExperienceMinimum { get; set; }

        public int? SecondarySkillExperienceMaximum { get; set; }

        public string SecondarySkillExperienceCondition { get; set; } = string.Empty;

        public string SecondarySkillExperienceProjectionStatus { get; set; } = string.Empty;

        public int? SlingshotSlotIndex { get; set; }

        public string SlingshotAmmoQualifiedItemId { get; set; } = string.Empty;

        public int? BombSlotIndex { get; set; }

        public string BombQualifiedItemId { get; set; } = string.Empty;

        public int? BombRadiusTiles { get; set; }

        public int? EscapeTileX { get; set; }

        public int? EscapeTileY { get; set; }

        public int? ExpectedBombObjectHits { get; set; }

        public int? ExpectedBombMonsterHits { get; set; }

        public double? ExpectedCombatAttacks { get; set; }

        public double? ExpectedCombatDurationMs { get; set; }

        public double? EstimatedTargetCostMs { get; set; }

        public string CombatDurationStatus { get; set; } = string.Empty;

        public string TargetQualifiedItemId { get; set; } = string.Empty;

        public int? TargetQuantity { get; set; }

        public int? TargetQuality { get; set; }

        public string RewardBranch { get; set; } = string.Empty;

        public string ExpectedOutputItemsJson { get; set; } = string.Empty;

        public int? NativeGainExperienceCallAmount { get; set; }

        public int? ExpectedStardropMaxStaminaDelta { get; set; }

        public string[] ExpectedDropQualifiedItemIds { get; set; } = Array.Empty<string>();

        public string SourceMatchStatus { get; set; } = string.Empty;

        public double? TargetDropChancePreview { get; set; }

        public string TargetDropProbabilityStatus { get; set; } = string.Empty;

        public double? TargetExpectedQuantityPerKill { get; set; }

        public double? TargetDropEfficiencyScore { get; set; }

        public int? FoodSlotIndex { get; set; }

        public int? DebrisIndex { get; set; }

        public int? RestoreSlotIndex { get; set; }

        public int? ToolSlotIndex { get; set; }

        public string RequiredToolKind { get; set; } = string.Empty;

        public int? ResourceClumpTileX { get; set; }

        public int? ResourceClumpTileY { get; set; }

        public int? ResourceClumpWidth { get; set; }

        public int? ResourceClumpHeight { get; set; }

        public int? ResourceClumpParentSheetIndex { get; set; }

        public int? ExpectedMineLevelDelta { get; set; }

        public int? ExpectedMineLevelAfter { get; set; }

        public int? ExpectedHealthCost { get; set; }

        public int? ExpectedHealthAfter { get; set; }

        public string ExpectedTargetLocation { get; set; } = string.Empty;

        public int? ExpectedArrivalTileX { get; set; }

        public int? ExpectedArrivalTileY { get; set; }

        public string SafetyWindowStatus { get; set; } = "not_required";

        public MiningPathTile[] Path { get; set; } = Array.Empty<MiningPathTile>();
    }

}
