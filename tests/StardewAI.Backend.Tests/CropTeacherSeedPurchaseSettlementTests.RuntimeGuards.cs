using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("run")]
    [InlineData("executor")]
    [InlineData("day")]
    [InlineData("candidate")]
    public async Task NativeIdentityCannotDriftAfterPreparation(string target)
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition);
        var wrong = target switch
        {
            "run" => transition with { RunId = "other-run" },
            "executor" => transition with { ExecutorVersion = "other-executor" },
            "candidate" => transition with { SelectedCandidateId = "not-the-locked-candidate" },
            _ => transition with { TotalDay = transition.TotalDay + 1 }
        };
        Assert.Contains(target == "candidate" ? "receipt" : "identity",
            (await Assert.ThrowsAsync<InvalidDataException>(() => prepared.CompleteAsync(wrong))).Message);
        Assert.Equal(0, runtime.Handler.Settlements);
    }

    [Fact]
    public async Task ConcurrentBaseLedgerChangePreventsMarkerAndCompilation()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        Assert.True(runtime.Repository.CommitReservationPortfolio(f.Before, new ReservationPortfolioCommitRequest
        {
            StateHash = f.Before.StateHash, ExpectedLedgerRevision = f.Ledger.Revision,
            PortfolioId = "concurrent-step", SourceDecisionId = "concurrent-step", GoalId = "earn_money"
        }).Accepted);
        var transition = f.Transition();
        await Assert.ThrowsAsync<InvalidDataException>(() => runtime.Prepare(transition));
        Assert.Equal(0, runtime.Handler.Commits);
    }

    [Fact]
    public async Task ExistingPreparationDirectoryNeverTriggersAnotherMutation()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f);
        var transition = f.Transition();
        await runtime.Prepare(transition);
        await Assert.ThrowsAsync<IOException>(() => runtime.Prepare(transition));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
    }
}
