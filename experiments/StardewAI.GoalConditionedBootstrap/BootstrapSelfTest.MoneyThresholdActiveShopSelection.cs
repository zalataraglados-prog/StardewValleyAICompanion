using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyThresholdActiveShopSelection()
    {
        var source = MoneyActiveShopSnapshot();
        var checkpoint = MoneyRevenueProgressionVerifier.Project(source);
        var partition = PolicyTrajectoryDatasetBuilder.PartitionFor(
            checkpoint.SaveId + ":" + checkpoint.PlayerId + ":" + checkpoint.TotalDay);
        var catalog = MoneyActiveShopCatalog(source);
        var before = JsonSerializer.Serialize(catalog);
        var sourceBefore = JsonSerializer.Serialize(source);
        var selection = MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection(
            "catalog.json", new string('b', 64), catalog, checkpoint, "money_50000", partition, source);
        var parameters = selection.ObjectiveParameters.ToDictionary(row => row.Name, row => row.Value);
        Require(selection.SelectedCandidate == catalog.Nodes[0].Candidates[0] &&
                selection.SelectedCandidate.CandidateChannel == "economic_candidate" &&
                selection.SelectedCandidate.Parameters.Length == 0 && selection.SelectedCandidate.CandidateKind == "sell_shop_item" &&
                selection.SelectedCandidate.CandidateSha256 == new string('a', 64) &&
                selection.EligibleSaleCandidateCount == 2 && selection.CrossingSaleCandidateCount == 1 &&
                selection.CandidateDenominatorCount == 2 && selection.ExpectedTotalMoneyEarnedAfter == 50_000 &&
                parameters.Count == 8 && parameters["continuation.target_location"] == "BlacksmithInterior" &&
                parameters["continuation.shop_id"] == "Blacksmith" && parameters["continuation.slot_index"] == "0" &&
                parameters["continuation.qualified_item_id"] == "(O)24" && parameters["continuation.quantity"] == "1" &&
                parameters["continuation.expected_unit_price"] == "20" &&
                JsonSerializer.Serialize(catalog) == before && JsonSerializer.Serialize(source) == sourceBefore &&
                !selection.FormalProductTrainingAuthorized,
            "Active shop sampling changed the original candidate/denominator or invented its native objective.");

        void Reject(Action<MoneyRevenueCandidateCatalogEntry>? change = null,
            Action<SnapshotEnvelope>? changeSource = null,
            Action<MoneyRevenueCandidateDenominatorCatalog>? changeCatalog = null,
            bool omitSource = false)
        {
            var snapshot = MoneyActiveShopSnapshot();
            changeSource?.Invoke(snapshot);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            var values = MoneyActiveShopCatalog(snapshot);
            change?.Invoke(values.Nodes[0].Candidates[0]);
            changeCatalog?.Invoke(values);
            var rejected = false;
            try
            {
                MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection("catalog.json", new string('b', 64),
                    values, MoneyRevenueProgressionVerifier.Project(snapshot), "money_50000", partition,
                    omitSource ? null : snapshot);
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Active shop sampling admitted a mismatched or unauthoritative source/candidate.");
        }
        Reject(omitSource: true);
        Reject(row => row.CandidateChannel = "event_candidate");
        Reject(row => row.CandidateKind = "unknown_sale");
        Reject(row => row.SemanticKindBinding = "unbound");
        Reject(row => row.Available = false);
        Reject(row => row.ComparisonEligible = false);
        Reject(row => row.CanShopSell = null);
        Reject(row => row.SlotIndex = -1);
        Reject(row => row.ShopId = "AnotherShop");
        Reject(row => row.QualifiedItemId = "");
        Reject(row => row.ItemId = "");
        Reject(row => row.Quantity = 0);
        Reject(row => row.UnitPrice = 0);
        Reject(row => row.TotalValue++);
        Reject(row => { row.Quantity = 2; row.UnitPrice = int.MaxValue; row.TotalValue = int.MaxValue; });
        Reject(row => row.BlockingReasons = new[] { "protected_inventory" });
        Reject(row => row.Parameters = new[] { new SmallModelActionParameter { Name = "debug.fixture", Value = "true" } });
        Reject(changeSource: snapshot => snapshot.State.Remove("menus"));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "player", "location_id", ""));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "active_menu", new { is_open = false, type = "ShopMenu" }));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "active_menu", new { is_open = true, type = "DialogueBox" }));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "sell_context", new { kind = "shop_sell_context", shop_id = "Blacksmith", safety_timer = 1 }));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "sell_context", new { kind = "shop_sell_context", shop_id = "Blacksmith", safety_timer = "unknown" }));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "sell_context", new { kind = "unknown_context", shop_id = "Blacksmith", safety_timer = 0 }));
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "menus", "sell_context", new { kind = "shop_sell_context", shop_id = "", safety_timer = 0 }));
        Reject(changeCatalog: value => value.SourceStateHash = new string('f', 64));
        Reject(changeCatalog: value => value.CurrentComparisonDenominatorReady = false);
        Reject(changeCatalog: value => value.FormalProductTrainingAuthorized = true);
        Reject(changeSource: snapshot => SetActiveShopTestField(snapshot, "player", "location_id", "BlacksmithInterior", "unavailable"));

        var mixed = MoneyActiveShopCatalog(source);
        mixed.Nodes[0].Candidates = mixed.Nodes[0].Candidates.Append(SaleSelectionCandidate("legacy:crossing", 30, 1)).ToArray();
        var mixedRejected = false;
        try { MoneyThresholdNativeOutcomeSelectionBuilder.BindShopSales(mixed, null); }
        catch (InvalidDataException) { mixedRejected = true; }
        Require(mixedRejected, "Missing native context silently removed an admitted economic channel from mixed sampling.");
        var checkpointRejected = false;
        try
        {
            MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection("catalog.json", new string('b', 64), catalog,
                checkpoint with { StateHash = new string('f', 64) }, "money_50000", partition, source);
        }
        catch (InvalidDataException) { checkpointRejected = true; }
        Require(checkpointRejected, "Native sale context was joined to a different checkpoint.");

        var zeroTimer = MoneyActiveShopSnapshot();
        SetActiveShopTestField(zeroTimer, "menus", "sell_context", new { kind = "shop_sell_context", shop_id = "Blacksmith", safety_timer = 0 });
        zeroTimer.StateHash = SnapshotHash.ComputeStateHash(zeroTimer.State);
        Require(MoneyThresholdNativeOutcomeSelectionBuilder.BindShopSales(MoneyActiveShopCatalog(zeroTimer), zeroTimer).Length == 2,
            "The exact-zero native timer boundary was rejected.");

        var waveCatalog = MoneyActiveShopCatalog(source);
        waveCatalog.Nodes[0].Candidates[0].UnitPrice = 1_200_000;
        waveCatalog.Nodes[0].Candidates[0].TotalValue = 1_200_000;
        var thresholds = GrandpaEvaluationGoalEvaluator.MoneyThresholds;
        var wave = new MoneyThresholdEvidenceWorkPlan
        {
            CriterionCount = 6, RequiredPartitionCount = 3, RequiredCellCount = 18,
            Cells = thresholds.Select((value, index) => MissingMoneyCell(value.CriterionId, index, value.Target, partition)).ToArray(),
            NativeCollectionWaves = new[] { new MoneyThresholdNativeCollectionWave(partition,
                thresholds.Select(value => value.CriterionId).ToArray(), 50_000, 1_000_000,
                "money_1000000", "conditional_native_outcome_collection_required", "self_test") }
        };
        var waveSelection = MoneyThresholdNativeOutcomeWaveSelectionBuilder.BuildSelection("catalog.json", new string('b', 64),
            waveCatalog, checkpoint, wave, partition, source);
        var directSelection = MoneyThresholdNativeOutcomeSelectionBuilder.BuildSelection("catalog.json", new string('b', 64),
            waveCatalog, checkpoint, "money_1000000", partition, source);
        Require(waveSelection.CriterionId == "money_1000000" && waveSelection.ExpectedCrossedCriterionIds.Length == 6 &&
                JsonSerializer.Serialize(waveSelection) == JsonSerializer.Serialize(directSelection) &&
                waveSelection.SelectedCandidate.CandidateChannel == "economic_candidate" &&
                !waveCatalog.LongHorizonDenominatorAuthoritative,
            "Wave and direct active-sale samplers disagreed or promoted long-horizon authority.");
    }

    private static SnapshotEnvelope MoneyActiveShopSnapshot()
    {
        var source = MoneyProgressionSnapshot(10, 100, 1_000, 49_980, 0, "empty", 1);
        SetActiveShopTestField(source, "player", "location_id", "BlacksmithInterior");
        SetActiveShopTestField(source, "menus", "active_menu", new { is_open = true, type = "ShopMenu" });
        SetActiveShopTestField(source, "menus", "sell_context", new { kind = "shop_sell_context", shop_id = "Blacksmith", safety_timer = -6 });
        source.StateHash = SnapshotHash.ComputeStateHash(source.State);
        return source;
    }

    private static void SetActiveShopTestField(SnapshotEnvelope source, string section, string field, object value,
        string status = "available")
    {
        var row = source.State.TryGetValue(section, out var existing)
            ? JsonNode.Parse(existing.GetRawText())!.AsObject() : new JsonObject();
        row[field] = JsonSerializer.SerializeToNode(MoneyProgressionField(value));
        row[field]!["status"] = status;
        source.State[section] = JsonSerializer.SerializeToElement(row);
    }

    private static MoneyRevenueCandidateDenominatorCatalog MoneyActiveShopCatalog(SnapshotEnvelope source)
    {
        MoneyRevenueCandidateCatalogEntry Economic(string id, int price)
        {
            var row = SaleSelectionCandidate(id, price, 0);
            row.CandidateChannel = "economic_candidate"; row.CandidateKind = "sell_shop_item";
            row.SemanticKindBinding = "direct_declared_kind"; row.LocationId = ""; row.ShopId = "Blacksmith";
            row.CanShopSell = true; row.Parameters = Array.Empty<SmallModelActionParameter>();
            return row;
        }
        var blocked = Economic("economic:protected", 1_000);
        blocked.Available = false; blocked.ComparisonEligible = false;
        blocked.BlockingReasons = new[] { "protected_inventory" };
        return new MoneyRevenueCandidateDenominatorCatalog
        {
            CurrentSnapshotDenominatorAuthoritative = true, CurrentComparisonDenominatorReady = true,
            NodeCoverageComplete = true, SourceStateHash = source.StateHash,
            SnapshotPath = "source.json", SnapshotSha256 = new string('c', 64),
            CatalogSha256 = new string('d', 64), ComparisonCandidateCount = 2, EnumeratedCandidateCount = 3,
            Nodes = new[] { new MoneyRevenueCandidateNodeCoverage
            {
                NodeId = "dependency.earn_money.sell", OptionId = "economy.sell_items", PolicyChoice = true,
                EnumerationComplete = true, Candidates = new[] { Economic("economic:exact", 20), Economic("economic:insufficient", 19), blocked }
            } }
        };
    }
}
