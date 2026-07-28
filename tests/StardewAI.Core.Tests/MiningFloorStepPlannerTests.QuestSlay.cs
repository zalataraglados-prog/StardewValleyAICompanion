using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class MiningFloorStepPlannerTests
{
    [Fact]
    public void QuestSlayObjectiveSelectsOnlyNativeNameContainsMatch()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.SlayNamedMonster,
                TargetMonsterNameFragments = new[] { "Dust Spirit" }
            },
            monsters: """
            [
              {"runtime_identity":"near","runtime_type":"StardewValley.Monsters.Bat","name":"Bat","tile_x":2,"tile_y":2},
              {"runtime_identity":"quest-target","runtime_type":"StardewValley.Monsters.DustSpirit","name":"Dangerous Dust Spirit","tile_x":5,"tile_y":2}
            ]
            """);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("quest_target_monster_reachable", plan.Reason);
        Assert.Equal("quest-target", plan.TargetRuntimeIdentity);
        Assert.Equal("Dangerous Dust Spirit", plan.TargetName);
        Assert.Equal("native_monster_name_contains", plan.SourceMatchStatus);
    }

    [Fact]
    public void QuestFifteenUsesNativeSlimeJellySludgeFallback()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.SlayNamedMonster,
                TargetMonsterNameFragments = new[] { "Green Slime" },
                MatchAnySlimeName = true
            },
            monsters: """
            [{"runtime_identity":"quest-target","runtime_type":"StardewValley.Monsters.GreenSlime","name":"Frost Jelly","tile_x":4,"tile_y":2}]
            """);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("quest-target", plan.TargetRuntimeIdentity);
        Assert.Equal("native_quest15_slime_name_match", plan.SourceMatchStatus);
    }

    [Fact]
    public void QuestSlayObjectiveKeepsRollingFloorSearchWhenTargetIsAbsent()
    {
        var plan = ObjectivePlan(
            new MiningFloorObjective
            {
                Kind = MiningObjectiveKinds.SlayNamedMonster,
                TargetMonsterNameFragments = new[] { "Skeleton" }
            },
            objects: """
            [{"tile_x":4,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"ladder_preview":{"creates_ladder":true}}]
            """,
            monsters: """
            [{"runtime_identity":"irrelevant","runtime_type":"StardewValley.Monsters.Bat","name":"Bat","tile_x":3,"tile_y":3}]
            """);

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal(4, plan.TargetTileX);
        Assert.Equal("deterministic_ladder_stone_reachable", plan.Reason);
        Assert.Empty(plan.SourceMatchStatus);
    }
}
