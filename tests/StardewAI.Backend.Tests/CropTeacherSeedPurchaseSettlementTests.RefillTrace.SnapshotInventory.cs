using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillSnapshotInventoryBindsEveryOriginalFrameWithoutGrantingResourceCredit(bool repair)
    {
        using var f = new RefillTraceFixture(repair, completeSnapshotInventory: true);
        await f.Build();
        var calibration = f.Open();
        var strong = f.OpenWithSnapshotInventoryContinuity();

        Assert.False(calibration.SnapshotInventoryContinuityVerified);
        Assert.True(strong.SnapshotInventoryContinuityVerified);
        Assert.Null(typeof(CropTeacherRefillApproachNativeTrace)
            .GetProperty(nameof(strong.SnapshotInventoryContinuityVerified))!.SetMethod);
        Assert.Equal(f.Approach.Queue.Items.Select(item => item.QueueItemId), strong.Steps.Select(step => step.QueueItemId));
        Assert.Equal(repair ? 1 : 0, strong.Steps.Count(step => step.OptionId == "executor.clear_obstacle"));
        Assert.Equal(calibration.InputHashes.OrderBy(pair => pair.Key), strong.InputHashes.OrderBy(pair => pair.Key));
        Assert.Equal(f.AfterPath, strong.AfterSnapshotPath);
        Assert.All(strong.Steps, step =>
        {
            Assert.Equal(2, step.Observation.Inventory.Count);
            var wood = Assert.Single(step.Observation.Inventory, row => row.QualifiedItemId == "(O)388");
            Assert.Equal(5, wood.InventoryBefore);
            Assert.Equal(5, wood.InventoryAfter);
            Assert.Equal(0, wood.AdmissionCount);
            Assert.False(step.Observation.ResourceCreditAuthorized);
            Assert.False(step.Observation.NativeOutcomeVerified);
            Assert.False(step.Observation.FormalProductTrainingAuthorized);
        });
        Assert.Single(strong.Steps.Select(step => step.Observation.CaptureSessionId).Distinct());
        AssertNoRefillSnapshotCredit(calibration);
        AssertNoRefillSnapshotCredit(strong);
        calibration.ValidateInputsUnchanged();
        strong.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillSnapshotInventoryPreservesDefaultCalibrationOnlyFixture(bool repair)
    {
        using var f = new RefillTraceFixture(repair);
        await f.Build();
        var calibration = f.Open();
        Assert.False(calibration.SnapshotInventoryContinuityVerified);
        Assert.All(calibration.Steps, step => Assert.Empty(step.Observation.Inventory));
        AssertNoRefillSnapshotCredit(calibration);
        AssertRefillSnapshotQuantityGap(f);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefillSnapshotInventoryRejectsSelfConsistentDenominatorGapInFirstOrLastFrame(bool repair, bool lastFrame)
    {
        using var f = new RefillTraceFixture(repair, completeSnapshotInventory: true);
        await f.Build();
        if (repair) Assert.True(f.Results.Count > 1); // No-repair compilation may be one move frame.
        var path = lastFrame ? f.Results[^1] : f.Results[0];
        var result = JsonNode.Parse(File.ReadAllText(path))!;
        // Both captured ends agree and there is no admission: old observation arithmetic is valid.
        // Actual raw snapshots still contain wood 5. Do not alter snapshot hashes or request bindings.
        var observation = result["action_origin_observation"]!;
        observation["inventory_before"]![1]!["quantity"] = 6;
        observation["inventory_after"]![1]!["quantity"] = 6;
        Fixture.Save(path, result);

        var calibration = f.Open();
        Assert.False(calibration.SnapshotInventoryContinuityVerified);
        var mismatched = calibration.Steps[lastFrame ? calibration.Steps.Count - 1 : 0].Observation.Inventory;
        Assert.Equal(6, Assert.Single(mismatched, row => row.QualifiedItemId == "(O)388").InventoryAfter);
        calibration.ValidateInputsUnchanged();
        AssertRefillSnapshotQuantityGap(f);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("queue")]
    [InlineData("result")]
    [InlineData("frame")]
    [InlineData("final")]
    [InlineData("receipt")]
    [InlineData("journal")]
    [InlineData("gzip")]
    public async Task RefillSnapshotInventoryRetainsOriginalInputsAndRejectsPostAdmissionTampering(string change)
    {
        using var f = new RefillTraceFixture(repair: true, completeSnapshotInventory: true);
        await f.Build();
        var calibration = f.Open();
        var strong = f.OpenWithSnapshotInventoryContinuity();
        var path = change switch
        {
            "source" => f.Approach.Watering.Shared.Source.SourcePath,
            "queue" => f.Approach.QueuePath,
            "result" => f.Results[0],
            "frame" => f.AfterFrames[0],
            "final" => f.AfterPath,
            "receipt" => f.ReceiptPath,
            "journal" => f.Journals[0],
            "gzip" => f.Journals[0] + ".gz",
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Contains(path, strong.InputHashes.Keys);
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(strong.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(calibration.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("session")]
    [InlineData("wire")]
    [InlineData("queue-order")]
    public async Task RefillSnapshotInventoryStillRequiresOriginalScopeRequestOrderAndReferenceNamespace(string change)
    {
        using var f = new RefillTraceFixture(repair: true, completeSnapshotInventory: true);
        await f.Build();
        if (change == "queue-order")
        {
            var aggregate = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
            var rows = aggregate["step_results"]!.AsArray();
            var first = rows[0]!.DeepClone(); rows[0] = rows[1]!.DeepClone(); rows[1] = first;
            Fixture.Save(f.ReceiptPath, aggregate);
        }
        else
        {
            var result = JsonNode.Parse(File.ReadAllText(f.Results[^1]))!;
            switch (change)
            {
                case "scope": result["action_origin_observation"]!["scope_id"] = "foreign"; break;
                case "session": result["action_origin_observation"]!["capture_session_id"] = Guid.NewGuid().ToString("N"); break;
                case "wire": result["execution_request_nonce"] = Guid.NewGuid().ToString("N"); break;
            }
            Fixture.Save(f.Results[^1], result);
        }
        Assert.Throws<InvalidDataException>(() => f.OpenWithSnapshotInventoryContinuity());
    }

    private static void AssertRefillSnapshotQuantityGap(RefillTraceFixture f)
    {
        var error = Assert.Throws<InvalidDataException>(() => f.OpenWithSnapshotInventoryContinuity());
        Assert.Contains("Native snapshot inventory continuity: inventory quantities changed outside the captured native interval", error.Message);
    }

    private static void AssertNoRefillSnapshotCredit(CropTeacherRefillApproachNativeTrace trace)
    {
        Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.NativeOutcomeVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
    }
}
