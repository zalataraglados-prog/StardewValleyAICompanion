using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string RecordedAt = "2026-11-07T03:00:00Z";
    private const string Id = "crop:owned-purchase";

    internal sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "crop-seed-settlement-" + Guid.NewGuid().ToString("N"));
        private int sequence;
        public SnapshotEnvelope Before { get; private set; }
        public SnapshotEnvelope After { get; private set; } = null!;
        public StrategyCommitmentLedger Ledger { get; set; }
        public string SourcePath => Path.Combine(root, "source.json");
        public string LedgerPath => Path.Combine(root, "ledger.json");
        public string RequestPath => Path.Combine(root, "run-request.json");
        public string ProofPath => Path.Combine(root, "proof-" + sequence + ".json");
        public string RuntimePath => Path.Combine(root, "runtime");
        public string RepositoryPath => Path.Combine(root, "repository");
        public string? PriorSourcePath { get; private set; }
        public CropPlantingCommitmentUpsertRequest CropRequest { get; }
        public ReservationPortfolioCommitRequest OtherRequest { get; }
        public ReservationPortfolioCommitRequest OwnRequest { get; }

        public Fixture(int tiles = 2, int budget = 80, int ownedSeeds = 1, int otherSeeds = 2)
        {
            Directory.CreateDirectory(root);
            Before = MoneyCropTeacherFixture.CreateSnapshot("SeedAcquisitionRuntimeBoundary",
                MoneyCropTeacherFixture.CreateSnapshot("MoneyCropProductionSnapshot"));
            CropTeacherNativeSupportFixture.CompleteResources(Before, 350 + budget);
            CropRequest = new CropPlantingCommitmentUpsertRequest
            {
                StateHash = Before.StateHash, CommitmentId = Id, SourceDecisionId = "teacher.crop.owner", SeedId = "745",
                TileCount = tiles, PlantingYear = 2, PlantingSeason = "spring", PlantingDayOfMonth = 1
            };
            var crop = new CropCommitmentLedgerService().Upsert(null, Before, CropRequest, RecordedAt);
            Assert.True(crop.Accepted, string.Join(",", crop.Errors));
            Ledger = crop.Ledger!;
            OtherRequest = new ReservationPortfolioCommitRequest
            {
                StateHash = Before.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                PortfolioId = "other-resources", SourceDecisionId = "other-root", GoalId = "complete_full_shipment",
                MaterialClaims = new[] { SeedClaim("other-seeds", "other-root", "complete_full_shipment", "other supply", otherSeeds) },
                CurrencyClaims = new[] { CashClaim("other-money", "other-root", "complete_full_shipment", "other funds", 350) }
            };
            var other = new ReservationPortfolioLedgerService().Commit(Ledger, Before, OtherRequest, RecordedAt);
            Assert.True(other.Accepted, string.Join(",", other.Errors));
            Ledger = other.Ledger!;
            OwnRequest = new ReservationPortfolioCommitRequest
            {
                StateHash = Before.StateHash, ExpectedLedgerRevision = Ledger.Revision,
                PortfolioId = "initial-crop-resources", SourceDecisionId = "teacher.crop.owner", GoalId = "earn_money",
                MaterialClaims = ownedSeeds > 0 ? new[] { SeedClaim("owned-seed", "teacher.crop.owner", "earn_money",
                    "crop_seed_supply:" + Id, ownedSeeds) } : Array.Empty<MaterialReservationUpsertRequest>(),
                CurrencyClaims = new[] { CashClaim(Id + ":seed-purchase:money", "teacher.crop.owner", "earn_money", "crop_seed_purchase:" + Id, budget) }
            };
            var own = new ReservationPortfolioLedgerService().Commit(Ledger, Before, OwnRequest, RecordedAt);
            Assert.True(own.Accepted, string.Join(",", own.Errors));
            Ledger = own.Ledger!;
            Save(SourcePath, Before);
            Save(LedgerPath, Ledger);
            Save(RequestPath, new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                LayoutIntents = new[] { new MoneyRevenueCropNativeProductionLayoutIntent(Id, "Farm",
                    Enumerable.Range(0, tiles).Select(x => new MoneyRevenueCropPlantingTile(x, 0)).ToArray()) },
                SeedAcquisitionIntents = new[] { new MoneyRevenueCropNativeSeedAcquisitionIntent(Id, "(O)745", "SeedShop", 80) }
            });
        }

        private MaterialReservationUpsertRequest SeedClaim(string id, string owner, string goal, string purpose, int quantity) => new()
        {
            StateHash = Before.StateHash, ExpectedLedgerRevision = Ledger.Revision, ReservationId = id,
            SourceDecisionId = owner, GoalId = goal, NodeId = "player:123", SlotIndex = 0,
            QualifiedItemId = "(O)745", Quantity = quantity, Purpose = purpose
        };

        private CurrencyReservationUpsertRequest CashClaim(string id, string owner, string goal, string purpose, int amount) => new()
        {
            StateHash = Before.StateHash, ExpectedLedgerRevision = Ledger.Revision, ReservationId = id,
            SourceDecisionId = owner, GoalId = goal, CurrencyId = 0, Amount = amount, Purpose = purpose
        };

        public CropTeacherSeedPurchaseSupport Open(string step = "support.step.1") =>
            CropTeacherSeedPurchaseSupport.Open(RequestPath, SourcePath, LedgerPath, Id, step, PriorSourcePath);

        public void Recapture(SnapshotEnvelope observed)
        {
            PriorSourcePath = Path.Combine(root, "prior-source.json");
            Save(PriorSourcePath, Before);
            Before = observed;
            Save(SourcePath, Before);
        }

        public ReservationPortfolioCommitResult Mark(CropTeacherSeedPurchaseSupport support) =>
            new ReservationPortfolioLedgerService().Commit(Ledger, Before, support.MarkerRequest, RecordedAt);

        public MoneyRevenueCropNativeLifecycleTransitionRequest Transition(int quantity = 1, int price = 75,
            Action<SnapshotEnvelope>? afterChange = null, string supportingOptionId = "executor.wait_ticks")
        {
            var transitions = new List<MoneyRevenueCropNativeLifecycleTransitionRequest>();
            After = MoneyCropTeacherFixture.CreateSnapshot("AddSeedAcquisitionTransition", root, transitions,
                Before, ++sequence, quantity, price, supportingOptionId, afterChange!);
            return transitions.Single();
        }

        public CropTeacherSeedPurchaseSettlement Verify(CropTeacherSeedPurchaseSupport support,
            ReservationPortfolioCommitResult marker, MoneyRevenueCropNativeLifecycleTransitionRequest transition) =>
            support.VerifyTransition(marker, transition, ProofPath);

        public ReservationPortfolioSupportingTransitionSettlementResult Settle(CropTeacherSeedPurchaseSettlement context,
            ReservationPortfolioCommitResult marker) => new ReservationPortfolioLedgerService().SettleSupportingTransition(
                marker.Ledger, After, context.SettlementRequest, RecordedAt);

        public ReservationPortfolioCommitResult Acquire(CropTeacherSeedPurchaseCustody custody) =>
            new ReservationPortfolioLedgerService().Commit(custody.Ledger, After, custody.AcquiredSeedRequest, RecordedAt);

        public void Adopt(StrategyCommitmentLedger ledger)
        {
            Ledger = ledger;
            Before = After;
            Save(SourcePath, Before);
            Save(LedgerPath, ledger);
        }

        public static void Save<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

        public void Dispose()
        {
            foreach (var directory in new[] { Path.Combine(RuntimePath, "live-snapshots"),
                         Path.Combine(RuntimePath, "teacher-ledgers"), RuntimePath, RepositoryPath, root })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (var path in Directory.EnumerateFiles(directory)) File.Delete(path);
                Directory.Delete(directory);
            }
        }
    }
}
