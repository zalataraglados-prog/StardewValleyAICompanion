using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task OwnedRefillApproachAdmitsOriginalPlanAndRestoresPendingWithoutDispatch()
    {
        using var f = new RefillApproachFixture(repair: false);
        await f.Compile();
        var shared = f.Watering.Shared;
        await using var owner = await shared.Files.Open();
        var operation = await AdmitNeutralRefill(owner, f);
        Assert.True(operation.IsWateringMaintenance);
        Assert.True(operation.IsSupportingTransition);
        Assert.False(operation.IsSupportingRoute);
        Assert.Equal(1, operation.Sequence);
        Assert.Same(operation, owner.Pending);
        Assert.Equal(shared.Source.SourcePath, owner.CurrentSnapshotPath);
        Assert.False(File.Exists(shared.ReceiptPath));
        Assert.False(File.Exists(Path.Combine(shared.Files.Native, "maintenance-native-claim.json")));
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CompileAndAdmitOriginalWateringAsync(() =>
        {
            called = true;
            throw new InvalidOperationException("Must not recompile a pending original phase.");
        }));
        Assert.False(called);
        await owner.DisposeAsync();
        await using var reopened = await shared.Files.Open();
        Assert.Equal(operation.SupportStepId, reopened.Pending!.SupportStepId);
        Assert.Equal(shared.Source.SourcePath, reopened.CurrentSnapshotPath);
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(shared.Files.OwnerRoot, "operation-000001-intent.json")))!;
        Assert.Equal("refill_approach", record["maintenance_control_kind"]!.GetValue<string>());
        Assert.False(record["native_dispatch_performed"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedRefillApproachCannotAdmitRepairOrMislabelItsOriginalPlan(bool repair)
    {
        using var f = new RefillApproachFixture(repair);
        await f.Compile();
        await using var owner = await f.Watering.Shared.Files.Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => AdmitNeutralRefill(owner, f, isRefillApproach: repair));
        Assert.Null(owner.Pending);
        Assert.False(File.Exists(Path.Combine(f.Watering.Shared.Files.Native, "maintenance-native-claim.json")));
    }

    private static Task<CropTeacherManagedSeedOperation> AdmitNeutralRefill(CropTeacherManagedSeedOwner owner,
        RefillApproachFixture f, bool isRefillApproach = true) => owner.CompileAndAdmitOriginalWateringAsync(() =>
            Task.FromResult(new CropTeacherOriginalWateringCompilation(f.Watering.Step, f.ModelPath, f.QueuePath,
                f.Watering.Shared.ReceiptPath, f.Watering.Shared.AfterPath, f.Watering.Shared.Files.Native)
                { IsRefillApproach = isRefillApproach }));
}
