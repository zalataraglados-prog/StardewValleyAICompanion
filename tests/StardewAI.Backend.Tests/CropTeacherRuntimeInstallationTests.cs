using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherRuntimeInstallationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("none")]
    [InlineData("material")]
    [InlineData("currency")]
    public void AutomaticUnmanagedDispatchCannotMistakeTotalSupplyForOwnedSupply(string kind)
    {
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var ledger = fixture.EmptyLedger();
        if (kind == "material") ledger.MaterialReservations = new[] { MoneyCropTestReservations.SeedReservation(ledger, 2) };
        if (kind == "currency") ledger.CurrencyReservations = new[] { MoneyCropTestReservations.MoneyReservation(ledger, 350) };
        if (kind == "none") CropTeacherLedgerSourceBinder.ValidateUnmanagedDispatch(ledger);
        else Assert.Throws<InvalidDataException>(() => CropTeacherLedgerSourceBinder.ValidateUnmanagedDispatch(ledger));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InstallsOriginalRequestsAndReloadsExactCompleteLedgerWithoutReplaying(bool lostCrop, bool lostResource)
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var decisionPath = Prepare(fixture, files);
        var originalDecision = File.ReadAllBytes(decisionPath);
        var originalResource = MoneyRevenueCropResourceReservationBuilder.Build(decisionPath);
        var replayLedger = originalResource.ReservationResult.Ledger!;
        using var handler = new RepositoryHandler(files.Repository, fixture.SourceSnapshot)
        {
            LoseCropReply = lostCrop, LoseResourceReply = lostResource
        };
        using var http = new HttpClient(handler);
        Assert.Equal(0, await Install(http, decisionPath, files.Output));
        Assert.Equal(1, handler.CropPosts);
        Assert.Equal(1, handler.ResourcePosts);
        Assert.Equal(lostResource ? 1 : 0, handler.Lookups);
        Assert.Equal(lostCrop ? 3 : 2, handler.Reads);
        var persisted = new FileStrategyCommitmentRepository(files.Repository).Get(fixture.SourceSnapshot);
        Assert.Equal(4, persisted.Revision);
        Assert.Single(persisted.CropPlantingCommitments);
        Assert.Equal(2, persisted.CropPlantingCommitments[0].TileCount);
        Assert.Equal(2, persisted.MaterialReservations.Single(row => row.ReservationId == "other-seeds").Quantity);
        Assert.Equal(350, persisted.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount);
        Assert.Equal(1, persisted.MaterialReservations.Single(row => row.ReservationId != "other-seeds").Quantity);
        Assert.Equal(80, persisted.CurrencyReservations.Single(row => row.ReservationId != "other-money").Amount);
        Assert.NotEqual(replayLedger.UpdatedAt, persisted.UpdatedAt);
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(files.Output, "installation-summary.json")))!;
        Assert.True(summary["runtime_ledger_installed"]!.GetValue<bool>());
        Assert.False(summary["execution_authorized"]!.GetValue<bool>());
        Assert.False(summary["native_outcome_verified"]!.GetValue<bool>());
        Assert.False(summary["formal_product_training_authorized"]!.GetValue<bool>());
        Assert.Equal(lostCrop, summary["crop_upsert_recovered_by_exact_ledger_readback"]!.GetValue<bool>());
        Assert.Equal(originalDecision, File.ReadAllBytes(decisionPath));
        Assert.False(originalResource.RuntimeLedgerInstalled);
        Assert.Equal(originalResource.ReservationRequest.PortfolioId, handler.ResourceRequest!["portfolio_id"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(originalResource.ReservationRequest, JsonOptions), handler.ResourceRequest));
        var finalPath = summary["installed_ledger_path"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(finalPath)),
            JsonSerializer.SerializeToNode(persisted, JsonOptions)));
        Assert.True(File.Exists(Path.Combine(files.Output, "crop-upsert-journal.json")));
        Assert.True(File.Exists(Path.Combine(files.Output, "crop-upsert-request.json")));
        Assert.Equal(lostCrop, File.Exists(Path.Combine(files.Output, "crop-upsert-failure.txt")));
        await Assert.ThrowsAsync<IOException>(() => Install(http, decisionPath, files.Output));
        Assert.Equal(1, handler.CropPosts);
        Assert.Equal(1, handler.ResourcePosts);
    }

    [Theory]
    [InlineData("crop_rejected", 1, 0, 1)]
    [InlineData("crop_missing", 1, 0, 2)]
    [InlineData("crop_claim_tamper", 1, 0, 1)]
    [InlineData("crop_history_tamper", 1, 0, 1)]
    [InlineData("crop_intervening", 1, 0, 2)]
    [InlineData("crop_journal_tamper", 1, 0, 1)]
    [InlineData("resource_rejected", 1, 1, 1)]
    [InlineData("resource_tamper", 1, 1, 1)]
    [InlineData("final_readback_tamper", 1, 1, 2)]
    [InlineData("base_tamper", 0, 0, 1)]
    public async Task RefusesTamperedOrUnacceptedMutationWithoutASecondAttempt(string failure, int crops, int resources, int reads)
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var decisionPath = Prepare(fixture, files);
        using var handler = new RepositoryHandler(files.Repository, fixture.SourceSnapshot) { Failure = failure };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<Exception>(() => Install(http, decisionPath, files.Output));
        Assert.Equal(crops, handler.CropPosts);
        Assert.Equal(resources, handler.ResourcePosts);
        Assert.Equal(reads, handler.Reads);
        Assert.Equal(0, handler.Lookups);
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(files.Output, "installation-summary.json")))!;
        Assert.False(summary["runtime_ledger_installed"]!.GetValue<bool>());
        Assert.Equal("installation_incomplete", summary["status"]!.GetValue<string>());
        Assert.Equal(350, new FileStrategyCommitmentRepository(files.Repository).Get(fixture.SourceSnapshot)
            .CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount);
    }

    [Fact]
    public void IndependentReplayRejectsMutatedInputsAndDoesNotExposeMutableOwnedRequests()
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var path = Prepare(fixture, files);
        var installation = CropTeacherRuntimeInstallation.Open(path);
        installation.CropRequest.TileCount++;
        installation.ResourceRequest.CurrencyClaims[0].Amount++;
        installation.BaseLedger.CurrencyReservations[0].Amount--;
        Assert.Equal(2, installation.CropRequest.TileCount);
        Assert.Equal(80, installation.ResourceRequest.CurrencyClaims[0].Amount);
        Assert.Equal(350, installation.BaseLedger.CurrencyReservations[0].Amount);
        var crop = new FileStrategyCommitmentRepository(files.Repository).Upsert(fixture.SourceSnapshot, installation.CropRequest);
        installation.VerifyCropReply(crop);
        File.AppendAllText(fixture.LayoutPath, " ");
        Assert.Throws<InvalidDataException>(() => installation.VerifyCropReply(crop));
    }

    private static string Prepare(MoneyCropTeacherFixture fixture, Files files)
    {
        fixture.AddCurrencyEvidence();
        var repository = new FileStrategyCommitmentRepository(files.Repository);
        var source = fixture.SourceSnapshot;
        var material = repository.UpsertMaterial(source, new MaterialReservationUpsertRequest
        {
            StateHash = source.StateHash, ExpectedLedgerRevision = 0, ReservationId = "other-seeds",
            SourceDecisionId = "other-root", GoalId = "complete_full_shipment", NodeId = "player:inventory",
            SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = 2, Purpose = "preserve other root seeds"
        });
        Assert.True(material.Accepted, string.Join(",", material.Errors));
        var currency = repository.UpsertCurrency(source, new CurrencyReservationUpsertRequest
        {
            StateHash = source.StateHash, ExpectedLedgerRevision = 1, ReservationId = "other-money",
            SourceDecisionId = "other-root", GoalId = "marriage_and_house_upgrade",
            CurrencyId = 0, Amount = 350, Purpose = "preserve other root cash"
        });
        Assert.True(currency.Accepted, string.Join(",", currency.Errors));
        fixture.SetLedger(currency.Ledger!);
        return fixture.SaveDecision(fixture.Build());
    }

    private static Task<int> Install(HttpClient http, string decision, string output) =>
        (Task<int>)typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetMethod("InstallCropReservationsAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new[] { "--decision", decision, "--backend-url", "http://test", "--output-dir", output }, http })!;

    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "crop-install-" + Guid.NewGuid().ToString("N"));
        public string Repository => Path.Combine(Root, "repository");
        public string Output => Path.Combine(Root, "output");

        public void Dispose()
        {
            var managed = Path.Combine(Root, "live-snapshots", "crop-teacher-managed-0001");
            foreach (var path in new[] { Path.Combine(managed, "owner"), Path.Combine(managed, "installation", "live-snapshots"),
                         Path.Combine(managed, "installation", "teacher-ledgers"), Path.Combine(managed, "installation"), managed,
                         Path.Combine(Root, "live-snapshots"), Path.Combine(Root, "dispatch"),
                         Path.Combine(Output, "live-snapshots"), Path.Combine(Output, "teacher-ledgers"),
                         Output, Repository, Root })
            {
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
                Directory.Delete(path);
            }
        }
    }
}
