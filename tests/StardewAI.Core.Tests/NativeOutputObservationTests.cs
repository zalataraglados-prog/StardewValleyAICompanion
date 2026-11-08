using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeOutputObservationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DefaultTransportDoesNotEmitObservationFields()
    {
        Assert.DoesNotContain("action_origin_observation", JsonSerializer.Serialize(new TrainingExecutionRequest(), Json));
        Assert.DoesNotContain("action_origin_observation", JsonSerializer.Serialize(new TrainingExecutionResult(), Json));
    }

    [Fact]
    public void ExplicitScopeAndNativeBranchesRoundTripWithoutGrantingCredit()
    {
        var request = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            JsonSerializer.Serialize(new TrainingExecutionRequest { ActionOriginObservationScopeId = "refill:original:1" }, Json), Json)!;
        Assert.Equal("refill:original:1", request.ActionOriginObservationScopeId);
        var receipt = JsonSerializer.Deserialize<ActionOriginObservationReceipt>("""
            {"scope_id":"refill:original:1","native_outcome_verified":true,"formal_product_training_authorized":true,
             "collections":[{"native_return":true,"inventory_admissions":[]},
               {"native_return":false,"inventory_admissions":[{"native_return":false,"offered_quantity":6,
                  "item_stack_after_call":4,"inventory_before":10,"inventory_after":12}]}]}
            """, Json)!;
        Assert.False(receipt.NativeOutcomeVerified);
        Assert.False(receipt.FormalProductTrainingAuthorized);
        Assert.Empty(receipt.Collections[0].InventoryAdmissions);
        Assert.False(receipt.Collections[1].NativeReturn);
        Assert.False(receipt.Collections[1].InventoryAdmissions.Single().NativeReturn);
        Assert.Equal(4, receipt.Collections[1].InventoryAdmissions.Single().ItemStackAfterCall);
        var result = new TrainingExecutionResult { ActionOriginObservation = receipt };
        var restored = JsonSerializer.Deserialize<TrainingExecutionResult>(JsonSerializer.Serialize(result, Json), Json)!;
        Assert.Equal(12, restored.ActionOriginObservation!.Collections[1].InventoryAdmissions.Single().InventoryAfter);
    }

    // Full merge, empty-slot insertion, partial merge, and no-inventory receive.
    [Theory]
    [InlineData(0, 16, true)]
    [InlineData(6, 16, true)]
    [InlineData(4, 12, false)]
    [InlineData(6, 10, true)]
    public void ActualPostCallStackAndReturnAreIndependentOfInventoryDelta(int stackAfter, int inventoryAfter, bool nativeReturn)
    {
        var admission = new ActionOriginInventoryAdmission { OfferedQuantity = 6, ItemStackAfterCall = stackAfter,
            InventoryBefore = 10, InventoryAfter = inventoryAfter, NativeReturn = nativeReturn };
        var raw = JsonSerializer.Serialize(admission, Json);
        Assert.DoesNotContain("remaining_item_stack", raw);
        Assert.Contains("item_stack_after_call", raw);
        var restored = JsonSerializer.Deserialize<ActionOriginInventoryAdmission>(raw, Json)!;
        Assert.Equal(stackAfter, restored.ItemStackAfterCall);
        Assert.Equal(inventoryAfter - 10, restored.InventoryAfter - restored.InventoryBefore);
        Assert.Equal(nativeReturn, restored.NativeReturn);
    }

    [Theory]
    [InlineData(null, "unsupported", "anything", null)]
    [InlineData("refill:1", "training_singleplayer", "executor.move_to_tile", null)]
    [InlineData("refill:1", "training_singleplayer", "executor.clear_obstacle", null)]
    [InlineData("", "training_singleplayer", "executor.clear_obstacle", "action_origin_observation_scope_invalid")]
    [InlineData("bad\nvalue", "training_singleplayer", "executor.clear_obstacle", "action_origin_observation_scope_invalid")]
    [InlineData("refill:1", "coop_companion", "executor.clear_obstacle", "action_origin_observation_scope_unsupported")]
    [InlineData("refill:1", "training_singleplayer", "executor.place_bomb", "action_origin_observation_scope_unsupported")]
    public void ObservationScopeDoesNotBroadenNativeExecution(string? scope, string mode, string option, string? expected)
    {
        Assert.Equal(expected, NativeObservationScopePolicy.Rejection(scope, mode, option, Enumerable.Repeat("bound", 6)));
    }

    [Fact]
    public void ScopeRequiresBoundedNameAndEveryIdentity()
    {
        Assert.Equal("action_origin_observation_scope_invalid", NativeObservationScopePolicy.Rejection(new string('a', 129),
            "training_singleplayer", "executor.move_to_tile", Enumerable.Repeat("bound", 6)));
        Assert.Equal("action_origin_observation_identity_required", NativeObservationScopePolicy.Rejection("refill:1",
            "training_singleplayer", "executor.move_to_tile", Array.Empty<string>()));
        Assert.Equal("action_origin_observation_identity_required", NativeObservationScopePolicy.Rejection("refill:1",
            "training_singleplayer", "executor.move_to_tile", new[] { "run", "queue", "item", "", "source", "save" }));
    }

    [Fact]
    public void OverflowIsExplicitAndCannotDiscardAnEarlierNativeEvent()
    {
        var buffer = new BoundedNativeObservationBuffer<string>(2);
        Assert.True(buffer.TryAdd("first")); Assert.True(buffer.TryAdd("second")); Assert.False(buffer.TryAdd("third"));
        Assert.True(buffer.Overflowed);
        Assert.Equal(new[] { "first", "second" }, buffer.Snapshot());
        var snapshot = buffer.Snapshot(); snapshot[0] = "changed";
        Assert.Equal("first", buffer.Snapshot()[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedNativeObservationBuffer<string>(0));
    }

    [Fact]
    public void ReferenceIdentityIsNotObjectEqualityOrArrayIndex()
    {
        var ids = new WeakNativeReferenceIds();
        var first = new SameValue(); var second = new SameValue();
        Assert.Equal(first, second);
        Assert.Equal(ids.For(first), ids.For(first));
        Assert.NotEqual(ids.For(first), ids.For(second));
    }

    [Fact]
    public void ItemPayloadWriterRejectsOverflowBeforeRetainingIt()
    {
        using var writer = new CappedNativeObservationWriter(4);
        writer.Write("ab"); writer.Write(new[] { 'c', 'd' }, 0, 2);
        Assert.Throws<InvalidDataException>(() => writer.Write('e'));
        Assert.True(writer.LimitExceeded);
        Assert.Equal("abcd", writer.ToString());
        using var span = new CappedNativeObservationWriter(2);
        Assert.Throws<InvalidDataException>(() => span.Write("abc".AsSpan()));
        Assert.Empty(span.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseWaitsForBothNativeCompletionAndFinalCapture(bool captureFirst)
    {
        var native = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var value = new object();
        var response = NativeObservationCompletion.AwaitFinalObservationAsync(native.Task, capture.Task);
        if (captureFirst) capture.SetResult(true); else native.SetResult(value);
        Assert.False(response.IsCompleted);
        if (captureFirst) native.SetResult(value); else capture.SetResult(true);
        Assert.Same(value, await response);
    }

    [Fact]
    public async Task CompletionDoesNotHideNativeOrFinalizationFailures()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => NativeObservationCompletion.AwaitFinalObservationAsync(
            Task.FromException<int>(new InvalidDataException()), new TaskCompletionSource<bool>().Task));
        await Assert.ThrowsAsync<InvalidDataException>(() => NativeObservationCompletion.AwaitFinalObservationAsync(
            Task.FromResult(1), Task.FromException(new InvalidDataException())));
    }

    private sealed class SameValue
    {
        public override bool Equals(object? obj) => obj is SameValue;
        public override int GetHashCode() => 1;
    }
}
