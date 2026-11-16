using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRefillSyntheticProductExplicitModesRebindWithoutProductionCredit(bool selected)
    {
        using var f = new NativeRefillProductFixture();
        await f.Build();
        var trace = f.OpenTrace(selected, PolicyTrajectoryVersionPins.ProductExecutor);
        Assert.Equal(selected, trace.SelectedResourceEffectsVerified);
        Assert.Equal(!selected, trace.ResourceNeutralBoundaryVerified);
        Assert.Empty(trace.Steps); // No action-origin observations were requested or fabricated.
        Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.NativeOutcomeVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        if (selected)
        {
            Assert.Equal(f.Trace.Approach.Queue.Items.Select(row => row.QueueItemId),
                trace.ResourceEffects.Select(row => row.QueueItemId));
            foreach (var row in trace.ResourceEffects) AssertSelectedRefillEffectsHaveNoCredit(row.Effects);
        }
        else Assert.Empty(trace.ResourceEffects);

        var preparation = f.OpenPreparation(selected, native: true);
        Assert.True(preparation.UsesNativeRefillExecution);
        var outcome = preparation.VerifyTransition(f.Transition(preparation, PolicyTrajectoryVersionPins.ProductExecutor));
        Assert.True(outcome.RefillApproachVerified);
        Assert.False(outcome.RefillControlVerified);
        Assert.Equal(0, outcome.AppliedTileCount);
        Assert.Empty(outcome.AppliedTiles);
        Assert.False(outcome.CompleteBatch);
        Assert.False(outcome.NativeCropLifecycleVerified);
        Assert.False(outcome.FormalProductTrainingAuthorized);
        var shared = f.Trace.Approach.Watering.Shared;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        var reply = new ReservationPortfolioLedgerService().Commit(shared.Source.Ledger, after, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(shared.Source.Ledger, outcome.VerifyRebindReply(reply));
        trace.ValidateInputsUnchanged();
        outcome.ValidateInputsUnchanged();
        File.AppendAllText(f.Journals[0] + ".gz", "changed");
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(outcome.ValidateInputsUnchanged);
    }

    [Fact]
    public async Task NativeRefillSyntheticProductDoesNotWidenEitherDefaultFactory()
    {
        using var f = new NativeRefillProductFixture();
        await f.Build();
        foreach (var selected in new[] { false, true })
        {
            var preparation = f.OpenPreparation(selected, native: false);
            Assert.False(preparation.UsesNativeRefillExecution);
            Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(
                f.Transition(preparation, PolicyTrajectoryVersionPins.ProductExecutor)));
            Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(
                f.Transition(preparation, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor)));
        }
        var compilation = f.Trace.Approach.Open();
        Assert.Throws<InvalidDataException>(() => CropTeacherRefillApproachNativeTrace.OpenResourceNeutral(
            compilation, f.ReceiptPath, f.AfterPath, f.RunId));
        Assert.Throws<InvalidDataException>(() => CropTeacherRefillApproachNativeTrace.OpenWithSelectedResourceEffects(
            compilation, f.ReceiptPath, f.AfterPath, f.RunId));
    }

    [Theory]
    [InlineData(false, PolicyTrajectoryVersionPins.ProductExecutor)]
    [InlineData(true, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor)]
    [InlineData(true, "unknown-executor")]
    public async Task NativeRefillSyntheticEndpointMustMatchDeclaredExecutor(bool product, string executorVersion)
    {
        using var f = new NativeRefillProductFixture(product);
        await f.Build();
        foreach (var selected in new[] { false, true })
        {
            Assert.Throws<InvalidDataException>(() => f.OpenTrace(selected, executorVersion));
            var preparation = f.OpenPreparation(selected, native: true);
            Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(f.Transition(preparation, executorVersion)));
        }
    }

    [Theory]
    [InlineData("authorization")]
    [InlineData("nonce")]
    [InlineData("source")]
    public async Task NativeRefillSyntheticProductRequiresOriginalAuthorizationAndBindings(string change)
    {
        using var f = new NativeRefillProductFixture();
        await f.Build();
        var path = f.Results[^1];
        var result = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (change == "authorization") result.Remove("product_authorization_status");
        else if (change == "nonce") result["execution_request_nonce"] = Guid.NewGuid().ToString("N");
        else result["product_request_before_state_hash"] = "foreign-source";
        Fixture.Save(path, result);
        foreach (var selected in new[] { false, true })
        {
            Assert.Throws<InvalidDataException>(() => f.OpenTrace(selected, PolicyTrajectoryVersionPins.ProductExecutor));
            var preparation = f.OpenPreparation(selected, native: true);
            Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(
                f.Transition(preparation, PolicyTrajectoryVersionPins.ProductExecutor)));
        }
    }
}
