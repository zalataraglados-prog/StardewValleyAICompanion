using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedSourceReturnReusesOriginalNativeAndMarkerKernelsWithoutCropOrTeacherCredit(bool product)
    {
        var f = new SiteTillNativeFixture(productTransport: product);
        using var host = f.Files.F.Runtime;
        await f.Build();
        var ledgerBefore = JsonSerializer.Serialize(f.Files.F.Source.Ledger, JsonOptions);
        var scope = OpenSharedReturn(f);
        Assert.Equal(f.Files.F.Source.Before.StateHash, scope.SourceStateHash);
        Assert.Equal(f.After.StateHash, scope.AfterStateHash);
        Assert.Equal(1, scope.VerifiedNativeStepCount);
        Assert.False(scope.NativeCropLifecycleVerified);
        Assert.False(scope.CropProductionCreditAuthorized);
        Assert.False(scope.TeacherPreferenceLabelEligible);
        Assert.False(scope.FormalProductTrainingAuthorized);
        Assert.All(f.NativePaths, path => Assert.Equal(SiteTillNativeFixture.FileHash(path), scope.InputHashes[path]));
        var reply = new ReservationPortfolioLedgerService().Commit(f.Files.F.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var rebound = scope.VerifyRebindReply(reply);
        Assert.Equal(f.After.StateHash, rebound.SourceStateHash);
        AssertOtherRootsUnchanged(f.Files.F.Source.Ledger, rebound);
        Assert.Equal(ledgerBefore, JsonSerializer.Serialize(f.Files.F.Source.Ledger, JsonOptions));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("executor")]
    [InlineData("day")]
    [InlineData("candidate")]
    [InlineData("source")]
    [InlineData("output_alias")]
    public async Task SharedSourceReturnRejectsDifferentNativeOwnershipOrAliasedRoles(string fault)
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        await f.Build();
        var transition = fault switch
        {
            "run" => f.Transition with { RunId = "other-run" },
            "executor" => f.Transition with { ExecutorVersion = "other-executor" },
            "day" => f.Transition with { TotalDay = f.Transition.TotalDay + 1 },
            "candidate" => f.Transition with { SelectedCandidateId = "other-candidate" },
            "source" => f.Transition with { BeforeSnapshotPath = f.AfterPath },
            "output_alias" => f.Transition with { AfterSnapshotPath = f.Transition.BeforeSnapshotPath },
            _ => throw new InvalidDataException("Unknown synthetic source-return fault.")
        };
        Assert.Throws<InvalidDataException>(() => OpenSharedReturn(f, transition));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("journal")]
    [InlineData("request_blob")]
    [InlineData("native_result")]
    [InlineData("step_snapshot")]
    public async Task SharedSourceReturnPinsOriginalNativeInputsBeforeAnyMarker(string fault)
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        await f.Build();
        var scope = OpenSharedReturn(f);
        var path = fault switch
        {
            "journal" => f.JournalPath,
            "request_blob" => f.JournalPath + ".gz",
            "native_result" => f.ResultPath,
            "step_snapshot" => f.FramePath,
            _ => throw new InvalidDataException("Unknown synthetic retained-input fault.")
        };
        File.AppendAllText(path, "\n");
        Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => { _ = scope.RebindRequest; });
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task SharedSourceReturnCannotAcceptAReplyThatCompletesTheOriginalCropCommitment()
    {
        var f = new SiteTillNativeFixture();
        using var host = f.Files.F.Runtime;
        await f.Build();
        var scope = OpenSharedReturn(f);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Files.F.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted);
        reply.Ledger!.CropPlantingCommitments[0].Status = StrategyCommitmentStatuses.Completed;
        Assert.Throws<InvalidDataException>(() => scope.VerifyRebindReply(reply));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    private static CropTeacherSharedSourceReturn OpenSharedReturn(SiteTillNativeFixture f,
        MoneyRevenueCropNativeLifecycleTransitionRequest? transition = null) =>
        CropTeacherSharedSourceReturn.Open(f.Files.F.Inputs.Plan.RequestPath, f.Files.F.Source.SourcePath,
            f.Files.F.Source.LedgerPath, f.Files.F.Inputs.Plan.CoveredCommitmentIds, transition ?? f.Transition, "shared.source.return.fixture");
}
