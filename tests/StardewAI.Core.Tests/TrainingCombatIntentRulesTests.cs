using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class TrainingCombatIntentRulesTests
{
    [Theory]
    [InlineData(TrainingCombatIntents.TargetDefeat, false)]
    [InlineData(TrainingCombatIntents.TransitSelfDefense, true)]
    [InlineData(TrainingCombatIntents.TransitRouteClearance, true)]
    public void TransitDisengagementRequiresPurposeAndDistance(
        string intent,
        bool expected)
    {
        Assert.Equal(
            expected,
            TrainingCombatIntentRules.ShouldDisengage(
                intent,
                playerTargetDistance: 5,
                targetOriginDistance: 3));
        Assert.False(
            TrainingCombatIntentRules.ShouldDisengage(
                intent,
                playerTargetDistance: 4,
                targetOriginDistance: 3));
    }

    [Fact]
    public void RouteClearanceRetainsTargetInsideOriginalBlockingArea()
    {
        Assert.False(
            TrainingCombatIntentRules.ShouldDisengage(
                TrainingCombatIntents.TransitRouteClearance,
                playerTargetDistance: 9,
                targetOriginDistance: 2));
    }

    [Fact]
    public void MovementBudgetsAreSharedAcrossDungeonFamilies()
    {
        Assert.Equal(
            16,
            TrainingCombatIntentRules.BoundMovementBudget(
                TrainingCombatIntents.TransitSelfDefense,
                estimatedMovementTiles: 3,
                targetDefeatMovementBudget: 512));
        Assert.Equal(
            36,
            TrainingCombatIntentRules.BoundMovementBudget(
                TrainingCombatIntents.TransitRouteClearance,
                estimatedMovementTiles: 20,
                targetDefeatMovementBudget: 512));
        Assert.Equal(
            512,
            TrainingCombatIntentRules.BoundMovementBudget(
                TrainingCombatIntents.TargetDefeat,
                estimatedMovementTiles: 3,
                targetDefeatMovementBudget: 512));
    }
}
