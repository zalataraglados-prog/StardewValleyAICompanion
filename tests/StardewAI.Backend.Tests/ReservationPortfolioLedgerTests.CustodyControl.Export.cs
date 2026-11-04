using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private static void ExportCustodyControlIfRequested(CustodyControlAttempt attempt,
        ReservationPortfolioSupportingTransitionSettlementRequest request, ReservationPortfolioSupportingTransitionSettlementResult settled)
    {
        var requested = Environment.GetEnvironmentVariable("STARDEWAI_CUSTODY_CONTROL_EXPORT_ROOT");
        if (string.IsNullOrWhiteSpace(requested)) return;
        var root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root), "Custody CLI fixtures require a new output directory.");
        Directory.CreateDirectory(root);
        void Write(string name, object value) => File.WriteAllText(Path.Combine(root, name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Write("before.json", attempt.Before);
        Write("after.json", attempt.After);
        Write("source-ledger.json", attempt.SourceLedger);
        Write("prepared-ledger.json", attempt.Commit.Ledger!);
        Write("commit-request.json", attempt.Preparation.Request!);
        Write("commit-result.json", attempt.Commit);
        Write("queue.json", attempt.Compilation.Queue!);
        Write("dispatched-item.json", attempt.Transfer);
        Write("receipt.json", attempt.Receipt());
        Write("settlement-request.json", request);
        Write("settlement-result.json", settled);
        Write("scope.json", new
        {
            synthetic_only = true, native_game_run = false, formal_training_authorized = false,
            reservation_id = attempt.Fixture.CustodyId, destination_node = CustodyTransferFixture.Chest
        });
    }
}
