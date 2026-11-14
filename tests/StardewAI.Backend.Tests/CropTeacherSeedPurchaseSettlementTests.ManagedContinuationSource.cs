using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedContinuationSourceReturnsOriginalPlanAndCurrentAcceptedPathsWithoutWrites()
    {
        using var f = new SavedReadbackOwnerFixture();
        var writes = 0;
        await using var owner = await f.Night.Open(async (path, json) =>
        {
            writes++;
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.CompleteNight(owner);
        await f.Run(owner, await f.Admit(owner));
        var beforeWrites = writes;
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        var source = ReadManagedContinuationSource(owner);
        AssertManagedContinuationSource(f, owner, source);
        Assert.Equal(beforeWrites, writes);
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(f.LoadedPath, ContinuationProperty<string>(source, "SnapshotPath"));
        var originalHash = JsonNode.Parse(File.ReadAllText(f.Night.Options.InitialSnapshotPath))!["state_hash"]!.GetValue<string>();
        Assert.Equal(originalHash, ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(source, "RunPlan").SourceStateHash);
        Assert.NotEqual(f.Night.Options.InitialSnapshotPath, owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task ManagedContinuationSourceRejectsBeforeAcceptedReadback()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        Assert.Null(owner.Pending);
        Assert.False(owner.HeadRecoveryRequired);
        Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(owner));
    }

    [Fact]
    public async Task ManagedContinuationSourceRequiresOriginalHeadRestoreAfterReopen()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using (var owner = await f.Night.Open())
        {
            await f.CompleteNight(owner);
            await f.Run(owner, await f.Admit(owner));
        }
        await using var reopened = await f.Night.Open();
        Assert.True(reopened.HeadRecoveryRequired);
        Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(reopened));
        await reopened.RestoreHeadAsync(f.Night.Shared.Runtime.Client, f.Night.Shared.Files.Recovery, f.Night.Shared.Files.Post);
        AssertManagedContinuationSource(f, reopened, ReadManagedContinuationSource(reopened));
    }

    [Fact]
    public async Task ManagedContinuationSourceRejectsPendingOriginalReadback()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        var operation = await f.Admit(owner);
        Assert.Same(operation, owner.Pending);
        Assert.Throws<InvalidOperationException>(() => ReadManagedContinuationSource(owner));
        Assert.Same(operation, owner.Pending);
        Assert.Equal(3, f.Night.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ManagedContinuationSourceRejectsTamperedRetainedOriginalPlan()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        await f.Run(owner, await f.Admit(owner));
        var path = f.Night.Options.NativeRunPlanPath;
        var bytes = File.ReadAllBytes(path);
        try
        {
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidDataException>(() => ReadManagedContinuationSource(owner));
        }
        finally { File.WriteAllBytes(path, bytes); }
    }

    [Fact]
    public async Task ManagedContinuationSourceReturnsFreshMutableObjectsWithoutCorruptingOwnerOrInputs()
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        await f.Run(owner, await f.Admit(owner));
        var source = ReadManagedContinuationSource(owner);
        var plan = ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(source, "RunPlan");
        var request = ContinuationProperty<MoneyRevenueCropNativeProductionRunPlanRequest>(source, "RunPlanRequest");
        var inputs = new[] { f.Night.Options.NativeRunPlanPath, plan.RequestPath }
            .ToDictionary(path => path, File.ReadAllBytes);
        plan.SourceStateHash = "caller-mutated-returned-plan";
        var mutableRequestProperty = request.GetType().GetProperties().First(property =>
            property.CanWrite && property.PropertyType == typeof(string));
        mutableRequestProperty.SetValue(request, "caller-mutated-returned-request");
        var next = ReadManagedContinuationSource(owner);
        Assert.NotSame(plan, ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(next, "RunPlan"));
        Assert.NotSame(request, ContinuationProperty<MoneyRevenueCropNativeProductionRunPlanRequest>(next, "RunPlanRequest"));
        AssertManagedContinuationSource(f, owner, next);
        foreach (var (path, bytes) in inputs) Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    private static object ReadManagedContinuationSource(CropTeacherManagedSeedOwner owner)
    {
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ReadContinuationSource", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try { return method!.Invoke(owner, null)!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static T ContinuationProperty<T>(object source, string name) =>
        Assert.IsType<T>(source.GetType().GetProperty(name)!.GetValue(source));

    private static void AssertManagedContinuationSource(SavedReadbackOwnerFixture f, CropTeacherManagedSeedOwner owner, object source)
    {
        var path = ContinuationProperty<string>(source, "NativeRunPlanPath");
        Assert.True(Path.IsPathFullyQualified(path));
        Assert.Equal(Path.GetFullPath(f.Night.Options.NativeRunPlanPath), path);
        Assert.Equal(owner.CurrentSnapshotPath, ContinuationProperty<string>(source, "SnapshotPath"));
        Assert.Equal(owner.CurrentLedgerPath, ContinuationProperty<string>(source, "LedgerPath"));
        var originalPlan = JsonNode.Parse(File.ReadAllText(path))!;
        var originalRequest = JsonNode.Parse(File.ReadAllText(originalPlan["request_path"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(originalPlan,
            JsonSerializer.SerializeToNode(ContinuationProperty<MoneyRevenueCropNativeProductionRunPlan>(source, "RunPlan"), JsonOptions)));
        Assert.True(JsonNode.DeepEquals(originalRequest,
            JsonSerializer.SerializeToNode(ContinuationProperty<MoneyRevenueCropNativeProductionRunPlanRequest>(source, "RunPlanRequest"), JsonOptions)));
    }
}
