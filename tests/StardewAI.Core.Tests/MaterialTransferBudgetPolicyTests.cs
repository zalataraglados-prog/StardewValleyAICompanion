using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MaterialTransferBudgetPolicyTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(10, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void NativeMenuAndUnlockWaitsFitTheCandidatePlanAndPrimitive(int quantity, bool withdraw)
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(false);
        var parameters = MaterialTransferTestFixture.Parameters(false, withdraw);
        parameters.Single(row => row.Name == "quantity").Value = quantity.ToString(CultureInfo.InvariantCulture);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = parameters } }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        var expectedTicks = 180 + 1 + 120 + 1 + 4 + quantity;
        Assert.Equal(expectedTicks, candidate.EstimatedTicks);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var compiler = new DailyPlanCompiler();
        var tooLate = compiler.Compile(ranked, snapshot.StateHash, availableMinutes: 7);
        Assert.Empty(tooLate.Steps);
        Assert.Contains(tooLate.CandidateAudit, row => row.Reasons.Contains("aggregate_time_budget_exceeded"));
        var plan = compiler.Compile(ranked, snapshot.StateHash, availableMinutes: 8);
        Assert.Collection(plan.Steps,
            move => Assert.Equal(0, move.EstimatedMinutes),
            transfer => Assert.Equal(8, transfer.EstimatedMinutes));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Equal(expectedTicks, Assert.Single(queue.Items.Single(item => item.OptionId == "executor.transfer_material")
            .NormalizedCommand.Steps).EstimatedTicks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(20)]
    public void LocalMovementAndTerminalInteractionAreBudgetedOnceEach(int distance)
    {
        var snapshot = MaterialTransferTestFixture.Snapshot(false);
        var player = JsonSerializer.SerializeToNode(snapshot.State["player"])!;
        player["tile_y"]!["value"] = 6 + distance;
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(snapshot.State);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { new OptionAvailabilityCandidate
            {
                OptionId = "inventory.transfer_item", Parameters = MaterialTransferTestFixture.Parameters(false)
            } }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal(distance * 16 + 316, candidate.EstimatedTicks);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Collection(plan.Steps,
            move => Assert.Equal(distance == 0 ? 0 : GameClockBudgetPolicy.TicksToGameMinutes(distance * 16), move.EstimatedMinutes),
            transfer => Assert.Equal(8, transfer.EstimatedMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidQuantityStaysAStructuredBlockedCandidate(int quantity)
    {
        var parameters = MaterialTransferTestFixture.Parameters(false);
        parameters.Single(row => row.Name == "quantity").Value = quantity.ToString(CultureInfo.InvariantCulture);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(MaterialTransferTestFixture.Snapshot(false),
            new[] { new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = parameters } }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.NotEmpty(candidate.BlockReasons);
        Assert.True(candidate.EstimatedTicks <= 0);
    }

    [Fact]
    public void LargeBudgetCannotOverflowIntoAShortOrNegativeEstimate()
    {
        Assert.Equal(int.MaxValue, MaterialTransferBudgetPolicy.InteractionTicks(int.MaxValue));
        Assert.Equal(int.MaxValue, MaterialTransferBudgetPolicy.EstimatedTicks(int.MaxValue, 1));
    }
}
