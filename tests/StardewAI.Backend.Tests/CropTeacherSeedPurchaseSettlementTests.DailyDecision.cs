using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DailyDecisionReopensTheSameChoiceAndDerivesCompletionOnlyFromAcceptedNativeReturn(bool product)
    {
        var f = new SharedYieldFixture(product); using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var reference = await SaveDaily(owner, f.Selection);
        Assert.Equal(reference, await SaveDaily(owner, f.Selection));
        Assert.Empty(DailyCompleted((await ReadDaily(owner))!));
        Assert.Null(owner.Pending); Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        f.Selection = f.Selection with { DecisionReference = reference };
        var operation = await f.Admit(owner);
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        await f.Native.Build(); f.Native.Files.F.Runtime.Store.Snapshots[f.Native.After.StateHash] = f.Native.After;
        await owner.VerifySharedSourceExecutionAsync(operation);
        await RunObservedSharedReturn(f.Native, owner, operation);
        Assert.Equal(f.Native.Preparation.CandidateId, Assert.Single(DailyCompleted((await ReadDaily(owner))!)));
        var repeatedRoot = Path.Combine(Path.GetDirectoryName(reference.Path)!, "repeat-selected-daily-work");
        var repeated = await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSharedSourceYieldAsync(f.Selection,
            repeatedRoot + "-receipt.json", repeatedRoot + "-after.json", repeatedRoot));
        Assert.Contains("already", repeated.Message);
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadDaily(reopened));
        await reopened.RestoreHeadAsync(f.Native.Files.F.Runtime.Client, f.Native.Files.F.Files.Recovery, f.Native.Files.F.Files.Post);
        var restored = (await ReadDaily(reopened))!;
        Assert.Equal(reference, restored.GetType().GetProperty("Reference")!.GetValue(restored));
        Assert.Equal(f.Selection.DecisionPlanPath, restored.GetType().GetProperty("PlanPath")!.GetValue(restored));
        Assert.Equal(f.Native.Preparation.CandidateId, Assert.Single(DailyCompleted(restored)));
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DailyDecisionNeverRetiresAnUnacceptedOrUnknownNativeClaim(bool claim)
    {
        var f = new SharedYieldFixture(); using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        f.Selection = f.Selection with { DecisionReference = await SaveDaily(owner, f.Selection) };
        var operation = await f.Admit(owner);
        if (claim) await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadDaily(owner));
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadDaily(reopened));
        Assert.NotNull(reopened.Pending); Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("record")]
    [InlineData("plan")]
    [InlineData("ranking")]
    [InlineData("reference")]
    public async Task DailyDecisionRejectsChangedInputsBeforeAnyNativeDispatch(string fault)
    {
        var f = new SharedYieldFixture(); using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var reference = await SaveDaily(owner, f.Selection);
        if (fault == "reference")
        {
            f.Selection = f.Selection with { DecisionReference = reference with { Sha256 = new string('a', 64) } };
            await Assert.ThrowsAsync<InvalidDataException>(() => f.Admit(owner));
        }
        else
        {
            File.AppendAllText(fault == "record" ? reference.Path : fault == "plan" ? f.Selection.DecisionPlanPath : f.Selection.DecisionRankingPath, "\n");
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadDaily(owner));
        }
        Assert.Null(owner.Pending); Assert.False(File.Exists(f.Native.ReceiptPath));
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task DailyDecisionKeepsLegacySelectionWireUnmarkedAndDoesNotPermitOrdinalReplacement()
    {
        var f = new SharedYieldFixture(); using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        Assert.DoesNotContain("decisionReference", JsonSerializer.Serialize(f.Selection, JsonOptions));
        await SaveDaily(owner, f.Selection);
        await Assert.ThrowsAsync<IOException>(() => SaveDaily(owner, f.Selection with { OwnedCandidateIds = ["another-owned-annotation"] }));
        Assert.Null(owner.Pending);
    }

    private static Task<CropTeacherDailyDecisionReference> SaveDaily(CropTeacherManagedSeedOwner owner,
        CropTeacherSharedSourceSelection selection, int iteration = 7) => (Task<CropTeacherDailyDecisionReference>)typeof(CropTeacherManagedSeedOwner)
            .GetMethod("SaveDailyDecisionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner,
                [iteration, selection.DecisionSnapshotPath, selection.DecisionPlanPath, selection.DecisionRankingPath, selection.OwnedCandidateIds])!;
    private static async Task<object?> ReadDaily(CropTeacherManagedSeedOwner owner)
    {
        var task = (Task)typeof(CropTeacherManagedSeedOwner).GetMethod("ReadDailyDecisionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(owner, [])!;
        await task; return task.GetType().GetProperty("Result")!.GetValue(task);
    }
    private static IReadOnlySet<string> DailyCompleted(object value) =>
        (IReadOnlySet<string>)value.GetType().GetProperty("CompletedForeignCandidateIds")!.GetValue(value)!;
}
