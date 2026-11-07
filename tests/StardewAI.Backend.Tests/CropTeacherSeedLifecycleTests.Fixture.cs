using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "crop-seed-lifecycle-" + Guid.NewGuid().ToString("N"));
        private readonly CropTeacherSeedTransferTests.Fixture source = new(nativeEvidence: true);
        internal SnapshotEnvelope Snapshot { get; }
        internal StrategyCommitmentLedger Ledger { get; private set; }
        internal FileStrategyCommitmentRepository Repository { get; }
        internal string RepositoryPath => Path.Combine(root, "repository");
        internal string SnapshotPath => Path.Combine(root, "source.json");
        internal string LedgerPath => Path.Combine(root, "ledger.json");
        internal string Id => source.CropRequest.CommitmentId;
        internal string CashId => Id + ":seed-purchase:money";
        internal string Step => "support.crop.lifecycle.release";

        internal Fixture(bool due = false, bool noResources = false, bool otherExpiresDuringCancel = false)
        {
            Directory.CreateDirectory(root);
            Repository = new(RepositoryPath);
            Snapshot = Copy(source.Before);
            var cropRequest = Copy(source.CropRequest);
            if (otherExpiresDuringCancel) cropRequest.PlantingDayOfMonth = 2;
            Assert.True(Repository.Upsert(Snapshot, cropRequest).Accepted);
            Assert.True(Repository.CommitReservationPortfolio(Snapshot, source.ResourceRequest).Accepted);
            Ledger = Repository.Get(Snapshot);
            var cash = Repository.UpsertCurrency(Snapshot, new CurrencyReservationUpsertRequest
            {
                StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision, ReservationId = CashId,
                SourceDecisionId = Ledger.CropPlantingCommitments.Single().SourceDecisionId, GoalId = "earn_money",
                CurrencyId = 0, Amount = 100, Purpose = "crop_seed_purchase:" + Id
            });
            Assert.True(cash.Accepted, string.Join(",", cash.Errors));
            Ledger = cash.Ledger!;
            if (otherExpiresDuringCancel)
            {
                var other = Copy(source.CropRequest);
                other.CommitmentId = "crop:other";
                other.SourceDecisionId = "other-root";
                other.ExpectedLedgerRevision = Ledger.Revision;
                var added = Repository.Upsert(Snapshot, other);
                Assert.True(added.Accepted, string.Join(",", added.Errors));
                Ledger = added.Ledger!;
            }
            if (noResources)
            {
                var released = Repository.CommitReservationPortfolio(Snapshot, new ReservationPortfolioCommitRequest
                {
                    StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision, PortfolioId = "fixture.resources.released",
                    SourceDecisionId = Ledger.CropPlantingCommitments.Single().SourceDecisionId, GoalId = "earn_money",
                    ReleaseReservationIds = new[] { "owned-seeds", CashId }
                });
                Assert.True(released.Accepted, string.Join(",", released.Errors));
                Ledger = released.Ledger!;
            }
            if (due || otherExpiresDuringCancel)
            {
                var day = Ledger.CropPlantingCommitments.Single(row => row.CommitmentId ==
                    (otherExpiresDuringCancel ? "crop:other" : Id)).LastInSeasonHarvestTotalDay + 1;
                var time = JsonNode.Parse(Snapshot.State["time"].GetRawText())!;
                time["total_days"]!["value"] = day;
                time["year"]!["value"] = day / 112 + 1;
                time["season"]!["value"] = new[] { "spring", "summer", "fall", "winter" }[day % 112 / 28];
                time["day"]!["value"] = day % 28 + 1;
                Snapshot.State["time"] = JsonSerializer.SerializeToElement(time, JsonOptions);
                Snapshot.GameTick++;
                Snapshot.StateHash = SnapshotHash.ComputeStateHash(Snapshot.State);
                // Establish the current source with the original service, not by editing ledger.SourceStateHash.
                var marked = Repository.CommitReservationPortfolio(Snapshot, new ReservationPortfolioCommitRequest
                {
                    StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision, PortfolioId = "fixture.current-day.source",
                    SourceDecisionId = "fixture.current-day.source", GoalId = "earn_money"
                });
                Assert.True(marked.Accepted, string.Join(",", marked.Errors));
                Ledger = marked.Ledger!;
            }
            Save(SnapshotPath, Snapshot);
            Save(LedgerPath, Ledger);
        }

        internal CropTeacherSeedLifecycleSupport Open() => CropTeacherSeedLifecycleSupport.Open(SnapshotPath, LedgerPath, Id, Step);
        internal static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
        internal static void Save<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

        public void Dispose()
        {
            source.Dispose();
            foreach (var path in Directory.EnumerateFiles(RepositoryPath)) File.Delete(path);
            Directory.Delete(RepositoryPath);
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
