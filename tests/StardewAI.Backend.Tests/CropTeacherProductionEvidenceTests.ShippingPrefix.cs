using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public void ObservedShippingTailKeepsTheOriginalMixedQueueAndAllFollowingTransitions()
    {
        var root = Path.Combine(Path.GetTempPath(), "crop-shipping-tail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var harvest = TailTransition(root, "harvest", shared: false);
        var wait = TailTransition(root, "wait", shared: false);
        var other = TailTransition(root, "other-item", itemId: "(O)388");
        var first = TailTransition(root, "ship-first", mixed: true);
        var control = TailTransition(root, "control", itemId: null);
        var last = TailTransition(root, "ship-last");
        var original = new[] { harvest, wait, other, first, control, last };
        var result = ReadShippingTail(original, 0);
        Assert.NotNull(result);
        Assert.Equal(original[3..], result);
        Assert.Same(first, result![0]); Assert.Same(control, result[1]); Assert.Same(last, result[2]);
        Assert.Equal(2, Read<ActionQueueEnvelope>(first.QueuePath).Items.Length);
        Assert.Equal(first.BeforeSnapshotPath, result[0].BeforeSnapshotPath);
        Assert.Equal(last.AfterSnapshotPath, result[^1].AfterSnapshotPath);
        var descriptor = new CropTeacherObservedShippingPrefix("crop_teacher_observed_shipping_prefix.v1",
            Path.Combine(root, "unbuilt-retention-proof.json"), first.BeforeSnapshotPath, result);
        var restored = JsonSerializer.Deserialize<CropTeacherObservedShippingPrefix>(JsonSerializer.Serialize(descriptor, JsonOptions), JsonOptions)!;
        Assert.Equal(first.BeforeSnapshotPath, restored.RetentionSnapshotPath);
        Assert.Equal(3, restored.Transitions.Length);
        Assert.Equal(first.QueuePath, restored.Transitions[0].QueuePath);
        Assert.Equal(2, Read<ActionQueueEnvelope>(restored.Transitions[0].QueuePath).Items.Length);
        // Classification metadata is not an output-retention proof or permission to execute.
        Assert.False(File.Exists(descriptor.OutputRetentionProofPath));
    }

    [Theory]
    [InlineData("other_item")]
    [InlineData("other_day")]
    [InlineData("not_shared")]
    [InlineData("no_shipping")]
    [InlineData("before_last_harvest")]
    public void ObservedShippingTailDoesNotInventAPrefixForAnotherWaveOrAnEarlierAction(string shape)
    {
        var root = Path.Combine(Path.GetTempPath(), "crop-shipping-tail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var harvest = TailTransition(root, "harvest", shared: false);
        var candidate = TailTransition(root, "candidate", shape == "other_item" ? "(O)388" :
            shape == "no_shipping" ? null : "(O)400", shared: shape != "not_shared");
        if (shape == "other_day") candidate = candidate with { TotalDay = 11 };
        var transitions = shape == "before_last_harvest" ? new[] { candidate, harvest } : new[] { harvest, candidate };
        Assert.Null(ReadShippingTail(transitions, shape == "before_last_harvest" ? 1 : 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void ObservedShippingTailRequiresAnExistingHarvestPosition(int index)
    {
        var error = Assert.Throws<TargetInvocationException>(() => ReadShippingTail([], index));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest[]? ReadShippingTail(
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions, int lastHarvestIndex) =>
        (MoneyRevenueCropNativeLifecycleTransitionRequest[]?)typeof(CropTeacherManagedSeedOwner).Assembly
            .GetType("StardewAI.LiveTrainingLoop.CropTeacherProductionEvidence")!
            .GetMethod("ReadObservedShippingTail", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [transitions, lastHarvestIndex, "(O)400", 12]);

    private static MoneyRevenueCropNativeLifecycleTransitionRequest TailTransition(string root, string id,
        string? itemId = "(O)400", bool shared = true, bool mixed = false)
    {
        // Declared queue-shape fixtures only. No owner/native outcome is fabricated or claimed here.
        var items = new List<ActionQueueItem>();
        if (mixed) items.Add(new() { OptionId = "executor.move_to_tile", NormalizedCommand = new() });
        items.Add(new() { OptionId = itemId is null ? "executor.wait_ticks" : "executor.ship_inventory_item_to_bin",
            NormalizedCommand = new() { Parameters = itemId is null ? [] : [new() { Name = "qualified_item_id", Value = itemId }] } });
        var queue = Path.Combine(root, id + "-queue.json");
        File.WriteAllText(queue, JsonSerializer.Serialize(new ActionQueueEnvelope { Items = items.ToArray() }, JsonOptions));
        return new(shared ? "shared_source_return" : "crop_actions", 12, queue, Path.Combine(root, id + "-before.json"),
            Path.Combine(root, id + "-receipt.json"), Path.Combine(root, id + "-after.json"), "declared-tail", "declared-executor", id)
        { NativeSharedSource = shared ? new(Path.Combine(root, "unbuilt-request.json"), Path.Combine(root, "unbuilt-ledger.json"),
            ["declared-crop"], id, new Dictionary<string, string>()) : null };
    }
}
