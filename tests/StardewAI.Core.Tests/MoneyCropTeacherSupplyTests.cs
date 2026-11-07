using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MoneyCropTeacherSupplyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static SnapshotEnvelope OriginalFixture(string name, params object[] arguments) =>
        (SnapshotEnvelope)typeof(MoneyRevenueCropProductionTeacherDecisionBuilder)
            .Assembly.GetType("StardewAI.GoalConditionedBootstrap.BootstrapSelfTest")!
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, arguments)!;
    [Theory]
    [InlineData(2, false, 2, 160)]
    [InlineData(1, false, 1, 80)]
    [InlineData(0, false, 0, 0)]
    [InlineData(-1, false, 0, 0)]
    [InlineData(2, true, 5, 400)]
    public void CapsFinitePurchasesWithoutChangingExplicitInfiniteStock(
        int stock, bool infinite, int tiles, long cost)
    {
        using var fixture = new Fixture();
        fixture.ChangeStock(rows =>
        {
            rows[2]!["stock"] = stock;
            rows[2]!["infinite_stock"] = infinite;
        });
        var candidate = fixture.Build().TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474");
        Assert.Equal(tiles, candidate.TileCount);
        Assert.Equal(cost, candidate.SeedAcquisitionCost);
        Assert.Equal(tiles > 0, candidate.Admitted);
        Assert.True(fixture.Receipt(fixture.Build()).AllCommitmentsAccepted);
    }

    [Theory]
    [InlineData("stock", true)]
    [InlineData("infinite_stock", false)]
    [InlineData("stack", false)]
    [InlineData("quality", false)]
    public void MissingPurchaseFactsAreNotInvented(string property, bool finite)
    {
        using var fixture = new Fixture();
        fixture.ChangeStock(rows =>
        {
            rows[2]!["infinite_stock"] = !finite;
            rows[2]!.AsObject().Remove(property);
        });
        var candidate = fixture.Build().TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474");
        Assert.False(candidate.Admitted);
        Assert.Contains("no_current_seed_supply", candidate.BlockReasons);
    }

    [Theory]
    [InlineData("stack", 2)]
    [InlineData("quality", 1)]
    public void OneSeedPerPurchaseCannotBorrowBundleOrQualityRows(
        string property, int value)
    {
        using var fixture = new Fixture();
        fixture.ChangeStock(rows => rows[2]![property] = value);
        Assert.False(fixture.Build().TeacherCandidateEvaluations
            .Single(candidate => candidate.SeedId == "474").Admitted);
    }

    [Fact]
    public void SamePriceBlockedStockCannotBorrowAnAdmittedIdentity()
    {
        using var fixture = new Fixture();
        fixture.ChangeStock(rows =>
        {
            rows[2]!["synced_key"] = "allowed";
            rows[2]!["infinite_stock"] = false;
            rows[2]!["stock"] = 2;
            var blocked = rows[2]!.DeepClone();
            blocked["synced_key"] = "blocked";
            blocked["stock"] = 100;
            blocked["executor_block_reasons"] = new JsonArray("blocked");
            rows.Add(blocked);
        });
        var candidate = fixture.Build().TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474");
        Assert.Equal(2, candidate.TileCount);
        Assert.Equal(160, candidate.SeedAcquisitionCost);
    }

    [Fact]
    public void IndistinguishableRowsAreNotAddedOrResolvedByInputOrder()
    {
        using var fixture = new Fixture();
        fixture.ChangeStock(rows =>
        {
            rows[2]!["infinite_stock"] = false;
            rows[2]!["stock"] = 4;
            var second = rows[2]!.DeepClone();
            second["stock"] = 2;
            rows.Add(second);
        });
        var first = fixture.Build();
        fixture.ChangeStock(rows =>
        {
            var second = rows[3]!.DeepClone();
            rows.RemoveAt(3);
            rows.Insert(2, second);
        });
        var second = fixture.Build();
        Assert.Equal(2, first.TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474").TileCount);
        Assert.Equal(JsonSerializer.Serialize(first, JsonOptions),
            JsonSerializer.Serialize(second, JsonOptions));
    }

    [Fact]
    public void ExistingSeedsRemainUsableWhenShopIsSoldOut()
    {
        using var fixture = new Fixture("474", 1);
        fixture.ChangeStock(rows =>
        {
            rows[2]!["infinite_stock"] = false;
            rows[2]!["stock"] = 0;
        });
        var candidate = fixture.Build().TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474");
        Assert.True(candidate.Admitted);
        Assert.Equal(1, candidate.TileCount);
        Assert.Equal(0, candidate.SeedAcquisitionCost);
    }

    [Theory]
    [InlineData(3, 5, 160)]
    [InlineData(int.MaxValue, 8, 0)]
    public void OwnSeedSupplyOnlyChargesTheFiniteShortfall(
        int existing, int tiles, long cost)
    {
        using var fixture = new Fixture("474", existing);
        fixture.ChangeStock(rows =>
        {
            rows[2]!["infinite_stock"] = false;
            rows[2]!["stock"] = 2;
        });
        var candidate = fixture.Build().TeacherCandidateEvaluations
            .Single(value => value.SeedId == "474");
        Assert.Equal(tiles, candidate.TileCount);
        Assert.Equal(cost, candidate.SeedAcquisitionCost);
    }

    [Fact]
    public void LegacyDecisionStillPassesOriginalReceipt()
    {
        using var fixture = new Fixture();
        var decision = fixture.Build();
        Assert.Equal(5, Assert.Single(decision.Commitments).TileCount);
        Assert.Equal("474", decision.Commitments[0].SeedId);
        Assert.True(fixture.Receipt(decision).AllCommitmentsAccepted);
    }

    [Fact]
    public void ActiveBaseCommitmentIsRejectedBeforeDecisionEmission()
    {
        using var fixture = new Fixture();
        var ledger = fixture.Receipt(fixture.Build()).FinalLedger!;
        Assert.Equal(StrategyCommitmentStatuses.Active,
            Assert.Single(ledger.CropPlantingCommitments).Status);
        fixture.SetLedger(ledger);
        Assert.Contains("without their validated layouts",
            Assert.Throws<InvalidDataException>(() => fixture.Build()).Message);
    }

    [Fact]
    public void OriginalEmptyBaseLedgerIsStillAccepted()
    {
        using var fixture = new Fixture();
        var original = fixture.Receipt(fixture.Build()).FinalLedger!;
        fixture.SetLedger(new StrategyCommitmentLedger
        {
            LedgerId = "empty-money-teacher-ledger",
            SaveId = original.SaveId,
            PlayerId = original.PlayerId,
            SourceStateHash = original.SourceStateHash
        });
        Assert.True(fixture.Receipt(fixture.Build()).AllCommitmentsAccepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BaseLedgerUsesOriginalIdentityAndStateValidation(bool wrongPlayer)
    {
        using var fixture = new Fixture();
        var ledger = fixture.Receipt(fixture.Build()).FinalLedger!;
        ledger.CropPlantingCommitments = Array.Empty<CropPlantingCommitment>();
        if (wrongPlayer) ledger.PlayerId = "999";
        else ledger.SourceStateHash = new string('e', 64);
        fixture.SetLedger(ledger);
        Assert.Throws<InvalidDataException>(() => fixture.Build());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "stardew-money-supply-" + Guid.NewGuid().ToString("N"));
        private readonly string sourcePath;
        private readonly string requestPath;
        private readonly MoneyRevenueCropProductionTeacherRequest request;
        private readonly SnapshotEnvelope source;

        public Fixture(string seed = "476", int quantity = 1)
        {
            Directory.CreateDirectory(root);
            sourcePath = Path.Combine(root, "source.json");
            requestPath = Path.Combine(root, "request.json");
            var layoutPath = Path.Combine(root, "layout.json");
            source = OriginalFixture("MoneyCropTeacherSourceSnapshot", 40, seed, quantity);
            Write(sourcePath, source);
            Write(layoutPath, OriginalFixture("MoneyCropTeacherLayoutSnapshot",
                40, true, seed, quantity, false, false, false));
            request = new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = sourcePath,
                FarmLayoutSnapshotPaths = new[] { layoutPath },
                DecisionId = "teacher.money.crop.supply",
                DecisionRecordedAt = "2026-11-07T00:00:00Z"
            };
            Write(requestPath, request);
        }

        public void ChangeStock(Action<JsonArray> change)
        {
            var locations = JsonNode.Parse(source.State["locations"].GetRawText())!;
            change(locations["shops"]!["value"]!["shops"]![0]!
                ["stock_preview"]!["entries"]!.AsArray());
            source.State["locations"] = JsonSerializer.SerializeToElement(locations);
            source.StateHash = SnapshotHash.ComputeStateHash(source.State);
            Write(sourcePath, source);
        }

        public void SetLedger(StrategyCommitmentLedger ledger)
        {
            request.BaseStrategyLedgerPath = Path.Combine(root, "ledger.json");
            Write(request.BaseStrategyLedgerPath, ledger);
            Write(requestPath, request);
        }

        public StrategyCommitmentLedger EmptyLedger() => new()
        {
            LedgerId = "shared-crop-budget",
            SaveId = source.SaveId.Value!,
            PlayerId = source.PlayerId.Value!,
            SourceStateHash = source.StateHash
        };

        public void ChangePlayer(Action<JsonObject> change)
        {
            var player = JsonNode.Parse(source.State["player"].GetRawText())!.AsObject();
            change(player);
            source.State["player"] = JsonSerializer.SerializeToElement(player);
            source.StateHash = SnapshotHash.ComputeStateHash(source.State);
            Write(sourcePath, source);
        }

        public void AddCurrencyEvidence(int money = 450, int qiGems = 0)
        {
            ChangePlayer(player => player["shop_currency_balances"] =
                JsonSerializer.SerializeToNode(new
                {
                    status = "available",
                    confidence = 1d,
                    value = new
                    {
                        schema_version = "shop_currency_balances.v1",
                        projection_status = "complete_locked_base_1.6.15_shop_menu_currency_domain",
                        supported_currency_ids = NativeShopCurrencies.All.Select(row => row.Id),
                        rows = NativeShopCurrencies.All.Select(row => new
                        {
                            currency_id = row.Id,
                            currency_key = row.Key,
                            balance = row.Id == NativeShopCurrencies.Money ? money :
                                row.Id == NativeShopCurrencies.QiGems ? qiGems : 0
                        })
                    }
                }, JsonOptions));
        }

        public void SetLayouts(params SnapshotEnvelope[] layouts)
        {
            request.FarmLayoutSnapshotPaths = layouts.Select((layout, index) =>
            {
                var path = Path.Combine(root, $"layout-{index}.json");
                Write(path, layout);
                return path;
            }).ToArray();
            Write(requestPath, request);
        }

        public MoneyRevenueCropProductionDecisionRequest Build() =>
            MoneyRevenueCropProductionTeacherDecisionBuilder.Build(requestPath);

        public MoneyRevenueCropProductionDecisionReceipt Receipt(
            MoneyRevenueCropProductionDecisionRequest decision)
        {
            var path = Path.Combine(root, "decision.json");
            Write(path, decision);
            return MoneyRevenueCropProductionDecisionReceiptBuilder.Build(path);
        }

        private static void Write<T>(string path, T value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

        public void Dispose()
        {
            foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
