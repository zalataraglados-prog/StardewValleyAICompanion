using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task ObservedTerminalSelectionDerivesOnlyOriginalAcceptedDepositsWithoutDispatch(int count)
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(count);
        var before = f.Owner.CurrentSnapshotPath; var commits = f.F.Shared.Runtime.Handler.Commits;
        var selection = await f.Select();
        Assert.Equal(count, selection.GetType().GetProperty("InitialDepositCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(selection));
        Assert.Equal(count, f.Owner.AcceptedTerminalDepositCount);
        Assert.Equal(before, f.Owner.CurrentSnapshotPath); Assert.Equal(commits, f.F.Shared.Runtime.Handler.Commits);
        var target = ObservedTerminalOwnerFixture.Target(selection, count);
        Assert.Equal(6, target.RequiredQuantity);
        Assert.Equal(count == 0 ? "crop_teacher_native_terminal_variants.v1" : "crop_teacher_native_terminal_variants.v2", target.NativeVariants!.SchemaVersion);
        target.VerifyNativeVariantSource(LifecycleRead<SnapshotEnvelope>(before), count, 1050);
        Assert.False(File.Exists(Path.Combine(f.F.Shared.Files.OwnerRoot, $"operation-{count + 1:D6}-intent.json")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ObservedTerminalReopenKeepsLaterAcceptedCountAndOriginalProductionInsteadOfRebuilding(int count)
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(count);
        var selected = await f.Select();
        var target = ObservedTerminalOwnerFixture.Target(selected, count);
        var paths = f.Prepare(target, count);
        var operation = await f.Owner.CompileAndAdmitOriginalTerminalAsync(target, () => Task.FromResult(
            new CropTeacherOriginalTerminalCompilation(paths.ModelPath, paths.QueuePath, paths.ReceiptPath, paths.AfterSnapshotPath,
                f.F.Shared.Files.Native + "-after-prefix")), new(f.Evidence.ManifestPath, f.Evidence.ManifestSha256));
        Assert.NotNull(f.ReadIntent(operation)["observed_terminal_selection"]);
        await f.Owner.ExecuteOriginalTerminalAsync(operation, f.F.Shared.Runtime.Client, async () =>
        {
            await f.Emit(target, count, paths);
            return new JsonObject { ["execution_path"] = paths.ReceiptPath, ["after_snapshot_path"] = paths.AfterSnapshotPath };
        });
        await f.Owner.RunTerminalShippingAsync(operation, f.F.Shared.Runtime.Client, (diagnostic, request) =>
            InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.F.Shared.Runtime.Client,
                f.F.Shared.Runtime.LiveOptions, diagnostic, request), f.F.Shared.Files.Post);
        var accepted = f.Owner.CurrentSnapshotPath;
        await f.Reopen();
        Assert.Equal(count + 1, f.Owner.AcceptedTerminalDepositCount);
        Assert.Equal(accepted, f.Owner.CurrentSnapshotPath);
        var production = await ReadObservedProduction(f.Owner);
        Assert.NotNull(production);
        Assert.Equal(f.Evidence.ManifestPath, production!.ManifestPath);
        Assert.Equal(f.Evidence.ManifestSha256, production.ManifestSha256);
        Assert.Equal(f.Evidence.OutputRetentionProofPaths, production.OutputRetentionProofPaths);
        var reopenedSelection = await f.Select();
        Assert.Equal(count + 1, f.Owner.AcceptedTerminalDepositCount);
        Assert.Equal(target with { AcceptedDepositCount = count + 1 }, ObservedTerminalOwnerFixture.Target(reopenedSelection, count + 1));
        var trace = await f.Owner.ReadJointProductionTraceAsync();
        Assert.Contains(f.Evidence.ManifestPath, trace.CropTrace.InputHashes.Keys);
        Assert.Equal(count + 1, f.F.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ObservedTerminalAllDepositedPersistsViaRealHomeRouteWithoutInventingAnotherDeposit()
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(6);
        var selection = await f.Select();
        Assert.Equal(6, f.Owner.AcceptedTerminalDepositCount);
        var source = f.Owner.CurrentSnapshotPath;
        var route = await f.F.Shared.Prepare(f.Owner, 42, runtimeDirectory: f.F.Shared.Files.Native + "-actual-home");
        Assert.True(route.IsSupportingRoute); Assert.NotNull(f.ReadIntent(route)["observed_terminal_selection"]);
        Assert.Null(f.ReadIntent(route)["terminal_target"]);
        await f.Owner.ExecuteOriginalRouteAsync(route, f.F.Shared.Runtime.Client, () => f.F.Shared.EmitNative(42, source));
        await f.F.Shared.Rebind(f.Owner, route);
        await f.Reopen();
        Assert.Equal(6, f.Owner.AcceptedTerminalDepositCount);
        var production = await ReadObservedProduction(f.Owner);
        Assert.NotNull(production); Assert.Equal(f.Evidence.ManifestPath, production!.ManifestPath);
        Assert.Equal(f.Evidence.ManifestSha256, production.ManifestSha256);
        var trace = await f.Owner.ReadJointProductionTraceAsync();
        Assert.Single(trace.CropTrace.Transitions);
        Assert.DoesNotContain(trace.CropTrace.Transitions, row => row.OperationKind == "terminal_shipping");
        Assert.Contains(f.Evidence.ManifestPath, trace.CropTrace.InputHashes.Keys);
        Assert.Equal("FarmHouse", LifecycleRead<SnapshotEnvelope>(f.Owner.CurrentSnapshotPath).State["player"].GetProperty("location_id").GetProperty("value").GetString());
        Assert.Equal(ObservedTerminalOwnerFixture.Target(selection, 6), ObservedTerminalOwnerFixture.Target(await f.Select(), 6));
    }

    [Fact]
    public async Task ObservedTerminalCannotCreditAChangedOwnerSequenceEvenWhenNativePrefixIsValid()
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(2);
        var manifest = JsonNode.Parse(File.ReadAllText(f.Evidence.ManifestPath))!;
        manifest["owner_shared_source_returns"]![0]!["ownerSequence"] = 999;
        Fixture.Save(f.Evidence.ManifestPath, manifest);
        // The changed descriptor is deliberately not vouched for by the old evidence hash.
        var changed = f.Evidence with { ManifestSha256 = ObservedTerminalOwnerFixture.Hash(f.Evidence.ManifestPath) };
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("SelectJointTerminalShippingAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(f.Owner, [changed])!;
        await Assert.ThrowsAsync<InvalidDataException>(async () => await task);
        Assert.Equal(0, f.Owner.AcceptedTerminalDepositCount); Assert.Null(f.Owner.Pending);
        Assert.False(File.Exists(Path.Combine(f.F.Shared.Files.OwnerRoot, "operation-000003-intent.json")));
    }

    [Fact]
    public async Task ObservedTerminalPendingReopenRejectsInventedInitialCursorBeforeAnyNativeClaim()
    {
        await using var f = await ObservedTerminalOwnerFixture.Create(2);
        var selection = await f.Select(); var target = ObservedTerminalOwnerFixture.Target(selection, 2);
        var paths = f.Prepare(target, 2);
        var operation = await f.Owner.CompileAndAdmitOriginalTerminalAsync(target, () => Task.FromResult(
            new CropTeacherOriginalTerminalCompilation(paths.ModelPath, paths.QueuePath, paths.ReceiptPath, paths.AfterSnapshotPath,
                f.F.Shared.Files.Native + "-pending-prefix")), new(f.Evidence.ManifestPath, f.Evidence.ManifestSha256));
        await f.Owner.DisposeAsync();
        var intentPath = Path.Combine(f.F.Shared.Files.OwnerRoot, $"operation-{operation.Sequence:D6}-intent.json");
        var intent = f.ReadIntent(operation);
        intent["observed_terminal_selection"]!["target"]!["acceptedDepositCount"] = 3;
        Fixture.Save(intentPath, intent);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.F.Shared.Files.Open());
        Assert.Equal(2, f.F.Shared.Runtime.Handler.Commits);
        Assert.False(File.Exists(paths.ReceiptPath)); Assert.False(File.Exists(paths.AfterSnapshotPath));
    }

    private static async Task<CropTeacherManagedProductionEvidence?> ReadObservedProduction(CropTeacherManagedSeedOwner owner)
    {
        var task = (Task)typeof(CropTeacherManagedSeedOwner).GetMethod("ReadObservedTerminalProductionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, [])!;
        await task; return (CropTeacherManagedProductionEvidence?)task.GetType().GetProperty("Result")!.GetValue(task);
    }
}
