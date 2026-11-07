using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleRuntimeTests
{
    [Theory]
    [InlineData("mode")]
    [InlineData("reason")]
    [InlineData("completion_reason")]
    [InlineData("origin")]
    [InlineData("run")]
    public async Task ExplicitRuntimeIntentIsValidatedBeforeAnyFileOrMutation(string fault)
    {
        using var f = new RuntimeFixture();
        if (fault == "mode") f.Mode = "guessed";
        if (fault == "reason") f.Reason = "";
        if (fault == "completion_reason") f.Mode = "calendar_completion";
        if (fault == "origin") f.Options.BackendUrl = "http://test?unsafe=origin";
        if (fault == "run") f.Options.RunId = "";
        await Assert.ThrowsAsync<ArgumentException>(() => f.Run());
        Assert.Empty(f.Handler.Events);
        Assert.False(Directory.Exists(f.Output));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("missing")]
    public async Task CommandRejectsAmbiguousOrIncompleteArguments(string fault)
    {
        using var f = new RuntimeFixture();
        var arguments = fault switch
        {
            "duplicate" => f.Arguments().Concat(new[] { "--mode", f.Mode }).ToArray(),
            "unknown" => f.Arguments().Concat(new[] { "--made-up-flag", "yes" }).ToArray(),
            _ => f.Arguments().Skip(2).ToArray()
        };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Command(arguments));
        Assert.Empty(f.Handler.Events);
        Assert.False(Directory.Exists(f.Output));
    }

    [Fact]
    public async Task ConcurrentNewStartsCannotOwnTheSameIntentOrRepeatCancellation()
    {
        using var f = new RuntimeFixture();
        async Task<Exception?> Attempt()
        {
            try { await f.Run(); return null; }
            catch (Exception error) { return error; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, result => result is null);
        Assert.IsType<IOException>(Assert.Single(results, result => result is not null));
        Assert.Equal(1, f.Handler.Cancellations);
        Assert.Equal(1, f.Handler.Releases);
        Assert.True(f.Summary()["resource_release_verified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task RevisionConflictDoesNotTriggerReconcilingReadOrBlindCancellationRecovery()
    {
        using var f = new RuntimeFixture();
        var advanced = f.Files.Repository.CommitReservationPortfolio(f.Files.Snapshot, new()
        {
            StateHash = f.Files.Snapshot.StateHash, ExpectedLedgerRevision = f.Files.Ledger.Revision,
            PortfolioId = "fixture.concurrent.advance", SourceDecisionId = "fixture.concurrent.advance", GoalId = "earn_money"
        });
        Assert.True(advanced.Accepted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Run());
        Assert.Equal(new[] { "cancel" }, f.Handler.Events);
        Assert.Equal(0, f.Handler.Releases);
        Assert.False(f.Summary()["crop_closure_verified"]!.GetValue<bool>());
        Assert.Equal(JsonSerializer.Serialize(advanced.Ledger, JsonOptions), JsonSerializer.Serialize(f.Files.Repository.Get(f.Files.Snapshot), JsonOptions));
    }

    [Fact]
    public async Task CalendarReadCannotCancelOrReleaseACropThatIsStillActive()
    {
        using var f = new RuntimeFixture();
        f.Mode = CropTeacherSeedLifecycleRuntime.CalendarCompletion;
        f.Reason = "";
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Run());
        Assert.Equal(new[] { "get" }, f.Handler.Events);
        Assert.False(f.Summary()["crop_closure_verified"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedSourceAfterDurableIntentStopsBeforeAnyBackendCoordination(bool completion)
    {
        using var f = new RuntimeFixture(completion: completion);
        await Assert.ThrowsAsync<InvalidDataException>(() => CropTeacherSeedLifecycleRuntime.RunAsync(f.Client,
            new(f.Options.BackendUrl, f.Output, f.Options.RunId, f.Mode, f.Reason), f.Files.Open(),
            (diagnostic, request) => InvokeLive<Task<System.Text.Json.Nodes.JsonObject>>("PostJournaledPortfolioCommitAsync", f.Client, f.Options, diagnostic, request),
            (endpoint, raw) => InvokeLive<Task<System.Text.Json.Nodes.JsonObject>>("PostJsonStringAsync", f.Client, endpoint, raw, CancellationToken.None),
            async (path, json) =>
            {
                await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
                if (Path.GetFileName(path) == "lifecycle-intent.json") await File.AppendAllTextAsync(f.Files.SnapshotPath, "\n");
            }));
        Assert.Empty(f.Handler.Events);
        Assert.False(f.Summary()["crop_closure_verified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task MissingReleaseLookupDoesNotFallBackToReconcilingGetOrResend()
    {
        using var f = new RuntimeFixture(loseRelease: true, fault: "release_lookup");
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => f.Run());
        Assert.NotNull(error.Data["portfolio_lookup_failure"]);
        Assert.Equal(new[] { "cancel", "release", "lookup" }, f.Handler.Events);
        Assert.True(f.Summary()["crop_closure_verified"]!.GetValue<bool>());
        Assert.False(f.Summary()["resource_release_verified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task MalformedLostCancellationReplyStillRequiresExactOriginalHistoryRecovery()
    {
        using var f = new RuntimeFixture(fault: "bad_cancel_json");
        await f.Run();
        Assert.Equal(new[] { "cancel", "get", "release", "get" }, f.Handler.Events);
        Assert.True(f.Summary()["cancellation_recovered_by_exact_readback"]!.GetValue<bool>());
        Assert.Equal(1, f.Handler.Cancellations);
    }
}
