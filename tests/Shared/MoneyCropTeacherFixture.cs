using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Testing;

internal sealed class MoneyCropTeacherFixture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static SnapshotEnvelope CreateSnapshot(string name, params object[] arguments) => Create<SnapshotEnvelope>(name, arguments);

    internal static T Create<T>(string name, params object[] arguments) =>
        (T)typeof(MoneyRevenueCropProductionTeacherDecisionBuilder).Assembly
            .GetType("StardewAI.GoalConditionedBootstrap.BootstrapSelfTest")!
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private readonly string root = Path.Combine(Path.GetTempPath(),
        "stardew-money-supply-" + Guid.NewGuid().ToString("N"));
    private readonly string sourcePath;
    private readonly string requestPath;
    private readonly MoneyRevenueCropProductionTeacherRequest request;
    private readonly SnapshotEnvelope source;

    public MoneyCropTeacherFixture(string seed = "476", int quantity = 1)
    {
        Directory.CreateDirectory(root);
        sourcePath = Path.Combine(root, "source.json");
        requestPath = Path.Combine(root, "request.json");
        var layoutPath = Path.Combine(root, "layout.json");
        source = CreateSnapshot("MoneyCropTeacherSourceSnapshot", 40, seed, quantity);
        Write(sourcePath, source);
        Write(layoutPath, CreateSnapshot("MoneyCropTeacherLayoutSnapshot",
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
        => MoneyRevenueCropProductionDecisionReceiptBuilder.Build(SaveDecision(decision));

    public string SaveDecision(MoneyRevenueCropProductionDecisionRequest decision)
    {
        var path = Path.Combine(root, "decision.json");
        Write(path, decision);
        return path;
    }

    public SnapshotEnvelope SourceSnapshot => source;
    public string SnapshotPath => sourcePath;
    public string LayoutPath => Path.Combine(root, "layout.json");
    public string LedgerPath => request.BaseStrategyLedgerPath;

    public string SaveResourceReceipt(MoneyRevenueCropResourceReservationReceipt receipt)
    {
        var path = Path.Combine(root, "resource-receipt.json");
        Write(path, receipt);
        return path;
    }

    public MoneyRevenueCropProductionObligationPlan ResourcePlan(
        MoneyRevenueCropResourceReservationReceipt receipt)
    {
        var ledgerPath = Path.Combine(root, "reserved-ledger.json");
        Write(ledgerPath, receipt.ReservationResult.Ledger);
        var path = Path.Combine(root, "obligation-request.json");
        Write(path, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = sourcePath, StrategyLedgerPath = ledgerPath
        });
        return MoneyRevenueCropProductionObligationPlanBuilder.Build(path);
    }

    private static void Write<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(root)) File.Delete(path);
        Directory.Delete(root);
    }
}

internal static class MoneyCropTestReservations
{
    public static CurrencyReservation MoneyReservation(StrategyCommitmentLedger ledger, int amount) => new()
    {
        ReservationId = "house-upgrade-budget", Revision = 1,
        SourceDecisionId = "house-upgrade", SourceStateHash = ledger.SourceStateHash,
        GoalId = "marriage_and_house_upgrade", OwnerPlayerId = long.Parse(ledger.PlayerId),
        CurrencyId = NativeShopCurrencies.Money, CurrencyKey = "money", Amount = amount,
        Purpose = "preserve another root's construction budget"
    };

    public static MaterialReservation SeedReservation(StrategyCommitmentLedger ledger, int quantity) => new()
    {
        ReservationId = "collection-seed-budget", Revision = 1,
        SourceDecisionId = "collection-supply", SourceStateHash = ledger.SourceStateHash,
        GoalId = "complete_full_shipment", OwnerPlayerId = long.Parse(ledger.PlayerId),
        NodeId = "player:inventory", SlotIndex = 0, QualifiedItemId = "(O)474", Quantity = quantity,
        Purpose = "preserve another root's exact material allocation"
    };
}
