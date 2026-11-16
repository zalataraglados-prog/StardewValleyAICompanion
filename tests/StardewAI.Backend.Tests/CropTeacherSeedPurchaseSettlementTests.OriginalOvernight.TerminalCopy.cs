using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OriginalOvernightTerminalCopyAdvancesSameOwnerThroughNativeSaveAndReopen()
    {
        using var f = new OvernightFixture(separateTerminalCopy: true);
        await using var owner = await f.Open();
        await f.Home(owner);
        var operation = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(operation, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        Assert.NotEqual(f.NightStepAfter, f.NightAfter);
        Assert.Equal(File.ReadAllBytes(f.NightAfter), File.ReadAllBytes(f.NightStepAfter));
        await f.RebindNight(owner, operation);
        Assert.Equal(f.NightAfter, owner.CurrentSnapshotPath);
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.NativeNightCalls);
        Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
        Assert.Equal("declared-completed-native-night", File.ReadAllText(f.NativeSaveFile));
        var accepted = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Shared.Files.OwnerRoot, "operation-000003-accepted.json")))!;
        Assert.True(accepted["native_overnight_control_verified"]!.GetValue<bool>());
        Assert.False(accepted["native_crop_lifecycle_verified"]!.GetValue<bool>());
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        Assert.Equal(f.NightAfter, reopened.CurrentSnapshotPath);
        Assert.False(reopened.HeadRecoveryRequired);
        Assert.Equal(3, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalOvernightTerminalCopyRejectsDifferentFullContentDespiteSameHashAndTick(bool cropContent)
    {
        using var f = new OvernightFixture(separateTerminalCopy: true);
        await using var owner = await f.Open();
        await f.Home(owner);
        var before = owner.CurrentSnapshotPath;
        var operation = await f.PrepareNight(owner);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalOvernightAsync(operation, f.Shared.Runtime.Client, async () =>
        {
            var result = await f.EmitNight(owner);
            var copy = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.NightStepAfter), JsonOptions)!;
            var summary = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.NightAfter), JsonOptions)!;
            if (cropContent)
                ChangeState(copy, "farm", farm => farm["crops"]!["value"]![0]!["ready_for_harvest"] = false);
            else copy.InGameTime.Value = 601;
            // ChangeState recomputes the fixture hash; deliberately retain the claimed summary identity.
            copy.StateHash = summary.StateHash;
            Assert.Equal(summary.StateHash, copy.StateHash);
            Assert.Equal(summary.GameTick, copy.GameTick);
            Fixture.Save(f.NightStepAfter, copy);
            return result;
        }));
        Assert.Contains("terminal snapshot differs", error.Message);
        Assert.Equal(before, owner.CurrentSnapshotPath);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalOvernightTerminalCopyRetainsBothPhysicalPathsBeforeSettlement(bool mutateSummary)
    {
        using var f = new OvernightFixture(separateTerminalCopy: true);
        await using var owner = await f.Open();
        await f.Home(owner);
        var before = owner.CurrentSnapshotPath;
        var operation = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(operation, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        // Even a logical no-op changes a retained physical artifact.
        File.AppendAllText(mutateSummary ? f.NightAfter : f.NightStepAfter, " ");
        await Assert.ThrowsAsync<InvalidDataException>(() => f.RebindNight(owner, operation));
        Assert.Equal(before, owner.CurrentSnapshotPath);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.NativeNightCalls);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("source")]
    [InlineData("queue")]
    [InlineData("outside")]
    public async Task OriginalOvernightTerminalCopyRejectsForbiddenStepPaths(string kind)
    {
        using var f = new OvernightFixture(separateTerminalCopy: true);
        await using var owner = await f.Open();
        await f.Home(owner);
        var before = owner.CurrentSnapshotPath;
        var operation = await f.PrepareNight(owner);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => owner.ExecuteOriginalOvernightAsync(operation, f.Shared.Runtime.Client, async () =>
        {
            var result = await f.EmitNight(owner);
            var receiptPath = result["execution_path"]!.GetValue<string>();
            var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(receiptPath), JsonOptions)!;
            receipt.StepResults.Single().AfterSnapshotPath = kind switch
            {
                "relative" => "after-snapshot-0003-item-0001.json",
                "source" => before,
                "queue" => f.NightQueue,
                _ => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f.NightAfter)!, "..", "outside-terminal.json"))
            };
            Fixture.Save(receiptPath, receipt);
            return result;
        }));
        Assert.Contains("Crop sleep step", error.Message);
        Assert.Equal(before, owner.CurrentSnapshotPath);
        Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
    }
}
