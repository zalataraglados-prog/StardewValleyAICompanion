using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData("normal", true, 0)]
    [InlineData("lost", true, 1)]
    [InlineData("http-server-error", true, 1)]
    [InlineData("malformed-reply", true, 1)]
    [InlineData("slow-body", true, 1)]
    [InlineData("http-rejected", false, 0)]
    [InlineData("http-rejected-bad-body", false, 0)]
    [InlineData("unavailable", false, 1)]
    [InlineData("cancelled", false, 1)]
    [InlineData("not-found", false, 1)]
    [InlineData("wrong-reply", false, 1)]
    [InlineData("claims-write", false, 1)]
    [InlineData("request-drift", false, 0)]
    [InlineData("journal-drift", false, 0)]
    public async Task ActualLiveMuseumHookRecoversOnlyExactPublishedSettlementWithoutReplay(string mode, bool completed, int lookups)
    {
        var root = NewPortfolioReplyRoot();
        var persisted = new FileStrategyCommitmentRepository(root);
        var fixture = MintMuseumCustody(protectSpare: true, repository: persisted);
        var item = CustodyDonationItem(fixture);
        var after = AfterCustodyDonation(fixture.Snapshot);
        await using var app = PortfolioReplyApplication(root);
        using var setup = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots[after.StateHash] = after;
        var options = new LiveTrainingOptions { Root = NewPortfolioReplyRoot(), RunId = "isolated-live-custody", BackendUrl = "http://localhost" };
        using var transport = new LivePortfolioSettlementTestHandler(app.Server.CreateHandler(), mode, options.SnapshotDir);
        using var http = new HttpClient(transport);
        if (mode == "slow-body") http.Timeout = TimeSpan.FromSeconds(2);
        var execution = JsonSerializer.SerializeToNode(CustodyReceipt(item, fixture.Snapshot, after))!.AsObject();
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("SettleMuseumOutputCustodyAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

        await ((Task)method.Invoke(null, new object[]
        {
            http, options, JsonSerializer.SerializeToNode(item)!.AsObject(), execution,
            JsonSerializer.SerializeToNode(fixture.Snapshot)!.AsObject(), JsonSerializer.SerializeToNode(after)!.AsObject()
        })!).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(1, transport.MutationCalls);
        Assert.Equal(lookups, transport.LookupCalls);
        Assert.Equal(completed ? "completed" : "failed", execution["material_reservation_settlement_status"]!.GetValue<string>());
        Assert.True(File.Exists(execution["material_reservation_settlement_request_journal_path"]!.GetValue<string>()));
        Assert.Equal(completed && lookups == 1, execution["material_reservation_settlement_reply_recovered"]?.GetValue<bool>() == true);
        var ledger = new FileStrategyCommitmentRepository(root).Get(after);
        Assert.Equal(mode.StartsWith("http-rejected", StringComparison.Ordinal) ? StrategyCommitmentStatuses.Active : StrategyCommitmentStatuses.Completed,
            Assert.Single(ledger.MaterialReservations.Where(row => row.OutputCustody is not null)).Status);
        Assert.Equal(StrategyCommitmentStatuses.Active, Assert.Single(ledger.MaterialReservations.Where(row => row.ReservationId == "other-goal-spare")).Status);
        if (!completed)
        {
            Assert.True(execution["reservation_settlement_retry_required"]!.GetValue<bool>());
            Assert.Equal("reservation_settlement_failed", execution["primitive_verification_status"]!.GetValue<string>());
            if (!mode.StartsWith("http-rejected", StringComparison.Ordinal))
            {
                Assert.False(string.IsNullOrWhiteSpace(execution["material_reservation_settlement_lookup_failure"]!.GetValue<string>()));
                Assert.True(File.Exists(execution["material_reservation_settlement_lookup_path"]!.GetValue<string>()));
                Assert.Contains("museum_output_custody_settlement_exception:HttpRequestException",
                    execution["primitive_verification_reasons"]!.AsArray().Select(value => value!.GetValue<string>()));
            }
        }
        if (completed && lookups == 1)
        {
            var observed = JsonNode.Parse(File.ReadAllText(execution["material_reservation_settlement_lookup_path"]!.GetValue<string>()))!;
            Assert.False(observed["native_outcome_verified"]!.GetValue<bool>());
            Assert.False(observed["ledger_mutation_performed"]!.GetValue<bool>());
        }
        var journalPath = execution["material_reservation_settlement_request_journal_path"]!.GetValue<string>();
        if (mode == "normal")
        {
            var journal = JsonNode.Parse(File.ReadAllText(journalPath))!;
            var original = JsonSerializer.Deserialize<ReservationPortfolioSupportingTransitionSettlementRequest>(
                File.ReadAllText(Path.Combine(options.SnapshotDir, journal["request_file"]!.GetValue<string>())))!;
            var send = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
                .GetMethod("PostJournaledPortfolioSettlementAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            await Assert.ThrowsAsync<IOException>(async () => await (Task)send.Invoke(null,
                new object[] { http, options, new JsonObject(), original })!);
            Assert.Equal(1, transport.MutationCalls);
            Assert.Equal(0, transport.LookupCalls);
        }
        if (mode == "lost" && OperatingSystem.IsWindows())
            await AssertPowerShellReadsLivePortfolioJournal(options, journalPath,
                execution["material_reservation_settlement_lookup_path"]!.GetValue<string>());
    }

    private static async Task AssertPowerShellReadsLivePortfolioJournal(LiveTrainingOptions options, string journalPath, string lookupPath)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "scripts", "lib", "RuntimePortfolioMutationRecovery.ps1")))
            root = root.Parent;
        Assert.NotNull(root);
        var start = new System.Diagnostics.ProcessStartInfo("pwsh")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-Command", """
            $ErrorActionPreference = 'Stop'
            . (Join-Path $env:PORTFOLIO_REPO 'scripts/lib/RuntimeEvidenceCommon.ps1')
            . (Join-Path $env:PORTFOLIO_REPO 'scripts/lib/RuntimePortfolioMutationRecovery.ps1')
            function Invoke-JsonPost {
                param($Url, $Body, $TimeoutSeconds)
                if ($Url -cne ($env:PORTFOLIO_BACKEND + '/api/v1/strategy/commitments/reservation-portfolios/lookup-mutation') -or $TimeoutSeconds -ne 15) {
                    throw 'Unexpected non-read-only request.'
                }
                $journal = [IO.File]::ReadAllText($env:PORTFOLIO_JOURNAL) | ConvertFrom-Json
                $original = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $env:PORTFOLIO_JOURNAL) $journal.request_file))
                if ((Get-RuntimeRawJsonProperty -RawJson $Body -PropertyName 'request') -cne $original) {
                    throw 'Original request was changed.'
                }
                $raw = [IO.File]::ReadAllText($env:PORTFOLIO_REPLY)
                return [pscustomobject]@{ Raw = $raw; Value = ($raw | ConvertFrom-Json) }
            }
            $reply = Get-RuntimePortfolioMutationReply -Context ([pscustomobject]@{ BackendUrl = $env:PORTFOLIO_BACKEND }) `
                -JournalPath $env:PORTFOLIO_JOURNAL -ObservationPath ($env:PORTFOLIO_REPLY + '.powershell-read.json')
            if ($reply.Raw -cne (Get-RuntimeRawJsonProperty -RawJson ([IO.File]::ReadAllText($env:PORTFOLIO_REPLY)) -PropertyName 'reply')) {
                throw 'Raw stored reply changed.'
            }
            'read-only-interop-ok'
            """ }) start.ArgumentList.Add(argument);
        start.Environment["PORTFOLIO_REPO"] = root!.FullName;
        start.Environment["PORTFOLIO_BACKEND"] = options.BackendUrl;
        start.Environment["PORTFOLIO_JOURNAL"] = journalPath;
        start.Environment["PORTFOLIO_REPLY"] = lookupPath;
        using var process = System.Diagnostics.Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("read-only-interop-ok", await stdout);
            Assert.True(string.IsNullOrWhiteSpace(await stderr), await stderr);
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
}
