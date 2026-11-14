using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalSelectedDispatchUsesOriginalExecutorAndMarkerWithoutCropAuthority(bool product)
    {
        var f = new SharedYieldFixture(product);
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        var selection = SharedDailySelection(f);
        var early = Assert.Throws<TargetInvocationException>(() => InvokeLive<object>("CompleteCropTeacherSharedSelection", owner, operation, selection));
        Assert.IsType<InvalidOperationException>(early.InnerException);
        var queueHash = SiteTillNativeFixture.FileHash(f.Selection.QueuePath);
        var calls = 0;
        await DispatchShared(f, owner, operation, async () =>
        {
            calls++;
            Assert.True(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
            Assert.Same(operation, owner.Pending);
            await f.Native.Build();
            return new JsonObject { ["execution_path"] = f.Native.ReceiptPath, ["after_snapshot_path"] = f.Native.AfterPath };
        });
        Assert.Equal(1, calls);
        Assert.Equal(queueHash, SiteTillNativeFixture.FileHash(f.Selection.QueuePath));
        Assert.Null(owner.Pending);
        Assert.Equal(f.Native.AfterPath, owner.CurrentSnapshotPath);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
        var completed = InvokeLive<object>("CompleteCropTeacherSharedSelection", owner, operation, selection);
        Assert.Empty(SelectionProperty<IReadOnlySet<string>>(selection, "CompletedForeignCandidateIds")!);
        Assert.Contains(f.Native.Preparation.CandidateId, SelectionProperty<IReadOnlySet<string>>(completed, "CompletedForeignCandidateIds")!);
        Assert.Empty((await owner.ReadJointProductionTraceAsync()).CropTrace.Transitions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DispatchShared(f, owner, operation,
            () => { calls++; return Task.FromResult(new JsonObject()); }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GlobalSelectedDispatchRetainsUnknownOrMisreportedOutcomeWithoutAnotherCallback(bool wrongPaths)
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        var calls = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => DispatchShared(f, owner, operation, async () =>
        {
            calls++;
            if (wrongPaths) await f.Native.Build();
            return new JsonObject
            {
                ["execution_path"] = wrongPaths ? f.Native.AfterPath : f.Native.ReceiptPath,
                ["after_snapshot_path"] = f.Native.AfterPath, ["success"] = true
            };
        }));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(f.Native.Files.F.Source.SourcePath, owner.CurrentSnapshotPath);
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DispatchShared(f, owner, operation,
            () => { calls++; return Task.FromResult(new JsonObject()); }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GlobalSelectedDispatchRejectsAQueueCopyBeforeClaimOrCallback()
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync",
            f.Native.Files.F.Runtime.Client, f.Native.Files.F.Runtime.LiveOptions, owner, operation, f.Selection.ModelPlanPath,
            (Func<Task<JsonObject>>)(() => { calls++; return Task.FromResult(new JsonObject()); })));
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(operation.RuntimeDirectory));
    }

    private static Task<JsonObject> DispatchShared(SharedYieldFixture f, CropTeacherManagedSeedOwner owner,
        CropTeacherManagedSeedOperation operation, Func<Task<JsonObject>> execute) =>
        InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Native.Files.F.Runtime.Client,
            f.Native.Files.F.Runtime.LiveOptions, owner, operation, f.Selection.QueuePath, execute);

    private static object SharedDailySelection(SharedYieldFixture f)
    {
        var plan = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(File.ReadAllText(f.Selection.DecisionPlanPath), JsonOptions)!;
        var ranking = JsonNode.Parse(File.ReadAllText(f.Selection.DecisionRankingPath))!.AsObject();
        var type = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetNestedType("CropTeacherDailySelection", BindingFlags.NonPublic)!;
        var selection = Activator.CreateInstance(type, f.Native.Preparation.TotalDay, plan.GoalId,
            SelectedQueueDecisionLease.CreateFromPlan(plan, ranking), new HashSet<string>(StringComparer.Ordinal),
            f.Selection.DecisionPlanPath, f.Selection.DecisionRankingPath)!;
        type.GetProperty("SourceSnapshotPath")!.SetValue(selection, f.Selection.DecisionSnapshotPath);
        return selection;
    }
}
