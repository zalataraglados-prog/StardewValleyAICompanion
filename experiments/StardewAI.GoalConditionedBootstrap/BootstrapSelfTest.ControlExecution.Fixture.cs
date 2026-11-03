using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private AcquisitionRouteExecutionBindingInputs PrepareControlExecution()
        {
            _ = BuildStaticInputs();
            // A synthetic shop isolates control admission from vanilla acquisition availability.
            var items = Enumerable.Range(0, 95).Select(index => index == 94 ? "96" : "fixture_" + index).ToArray();
            var shop = JsonNode.Parse(File.ReadAllText(shopsPath))!;
            var stock = shop["payload"]!["FixtureShop"]!["Items"]![0]!;
            stock["ItemId"] = "(O)96";
            stock["Condition"] = "TIME 600 1800";
            stock["AvailableStock"] = -1;
            stock["AvailableStockLimit"] = -1;
            stock["TradeItemId"] = null;
            stock["TradeItemAmount"] = null;
            Write(shopsPath, shop);
            var access = JsonNode.Parse(File.ReadAllText(accessConstraintPath))!;
            var accessStock = access["shops"]![0]!["stock"]![0]!;
            accessStock["itemId"] = "(O)96";
            accessStock["condition"] = "TIME 600 1800";
            accessStock["parsedCondition"]!["clauses"] = JsonSerializer.SerializeToNode(new[] { NativeConditionClause("TIME") });
            Write(accessConstraintPath, access);
            var inventory = JsonNode.Parse(File.ReadAllText(inventoryPath))!;
            var sets = inventory["requirement_sets"]!.AsArray();
            sets[sets.IndexOf(sets.Single(set => set!["requirement_set_id"]!.GetValue<string>() == "museum_collection"))] =
                JsonSerializer.SerializeToNode(RequirementSet("museum_collection", items.Select(id => CollectionRequirementGroup(
                    "museum:item:" + id, "all_required", 1, CollectionAlternative(
                        id, "(O)" + id, id, "item_id", 1, 0, "sells", "shop:FixtureShop"))).ToArray()), JsonDefaults.Options);
            sets.Single(set => set!["requirement_set_id"]!.GetValue<string>() == "museum_collection")!["denominator_status"] = "complete";
            foreach (var source in inventory["source_evidence"]!.AsArray())
                source!["sha256"] = HashFile(source["path"]!.GetValue<string>());
            Write(inventoryPath, inventory);
            var lowering = JsonNode.Parse(File.ReadAllText(loweringPath))!;
            lowering["requirement_inventory_sha256"] = HashFile(inventoryPath);
            lowering["route_occurrence_count"] = 77 - 1 + items.Length;
            var loweredSets = lowering["requirement_sets"]!.AsArray();
            loweredSets[loweredSets.IndexOf(loweredSets.Single(set => set!["requirement_set_id"]!.GetValue<string>() == "museum_collection"))] =
                JsonSerializer.SerializeToNode(LoweringSet("museum_collection", items.Select(id => CollectionLoweredGroup(
                    "museum:item:" + id, 1, CollectionLoweredAlternative(
                        id, "(O)" + id, id, "item_id", 1, 0, "sells", "shop:FixtureShop", "economy.buy_supplies"))).ToArray()), JsonDefaults.Options);
            Write(loweringPath, lowering);
            var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
            catalog["requirement_inventory_sha256"] = HashFile(inventoryPath);
            Write(catalogPath, catalog);
            Write(windowsPath, MasterAnglerStageOneWindowIndexBuilder.Build(catalogPath, 3));
            Write(routeCalendarPath, AcquisitionRouteCalendarResolutionBuilder.Build(inventoryPath, loweringPath, windowsPath));
            Write(targetDateCalendarPath, AcquisitionRouteTargetDateCalendarBuilder.Build(
                inventoryPath, loweringPath, windowsPath, routeCalendarPath, 0));
            Write(targetDateRouteCalibrationPath, StageOneRouteTimingCalibration(0));
            WriteStageOneCollectionSnapshot(targetDateUnlockSnapshotPath, "control-fixture", fish,
                playerLocation: "FixtureShop", totalDays: 0, timeOfDay: 900);
            var before = JsonNode.Parse(File.ReadAllText(targetDateUnlockSnapshotPath))!;
            var state = before["state"]!;
            state["world_progress"]!["museum"] = JsonSerializer.SerializeToNode(Field(new
            {
                donated_count = 59, total_donatable_items = 95, missing_item_count = 36,
                donatable_items = items.Select((id, index) => new { item_id = id, qualified_item_id = "(O)" + id, donated = index < 59 }),
                missing_item_ids = items.Skip(59), collection_complete = false,
                rusty_key_donation_threshold = 60, rusty_key_reward_id = "museum60",
                rusty_key_reward_action = "MarkEventSeen Host 295672"
            }), JsonDefaults.Options);
            var preview = state["locations"]!["shops"]!["value"]!["shops"]![0]!["stock_preview"]!;
            var quote = preview["entries"]![0]!;
            quote["qualified_item_id"] = "(O)96";
            quote["item_id"] = "96";
            quote["quality"] = 0;
            quote["infinite_stock"] = true;
            quote["stock"] = int.MaxValue;
            quote["executor_purchase_preview_enabled"] = true;
            quote["executor_purchase_enabled"] = true;
            quote["executor_block_reasons"] = new JsonArray();
            quote["can_afford_one_with_currency"] = true;
            quote["can_afford_one_with_trade_item"] = true;
            quote["could_inventory_accept"] = true;
            quote["trade_item_qualified_id"] = null;
            quote["effective_trade_item_count"] = null;
            quote["currency_balance"] = 500;
            state["menus"] = JsonSerializer.SerializeToNode(new
            {
                active_menu = Field(new { is_open = true, type = "ShopMenu" }),
                sleep_prompt_context = NativeField(new { is_sleep_prompt = false }, "test"),
                shop_stock = Field(preview)
            }, JsonDefaults.Options);
            state["player"]!["seed_inventory"] = JsonSerializer.SerializeToNode(NativeField(Array.Empty<object>(), "test"));
            state["farm"]!["crop_catalog"] = JsonSerializer.SerializeToNode(NativeField(Array.Empty<object>(), "test"));
            foreach (var section in state.AsObject().Select(entry => entry.Value).OfType<JsonObject>())
                foreach (var field in section.Select(entry => entry.Value).OfType<JsonObject>().Where(field => field.ContainsKey("adapter")))
                    if (field["adapter"]!.GetValue<string>() == "self-test") field["adapter"] = "test";
            var snapshot = before.Deserialize<SnapshotEnvelope>(JsonDefaults.Options)!;
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            Write(targetDateUnlockSnapshotPath, snapshot);
            WriteEmptyStrategyLedger(strategyLedgerPath, snapshot.StateHash);
            Write(targetDateProcessingPath, BuildProcessingFixtureChain(inventoryPath, loweringPath, windowsPath,
                routeCalendarPath, targetDateCalendarPath, targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath, strategyLedgerPath, root));
            File.Copy(Path.Combine(root, "target-date-reservation.json"), targetDateReservationPath, true);
            Write(fishingForecastManifestPath, new FishingForecastSnapshotManifest());
            Write(targetDateFishingProbabilityPath, BuildFishingProbability());
            Write(targetDateStochasticRetryPath, BuildStochasticRetry(targetDateProcessingPath));
            Write(targetDateDailyTimeEnergyPath, BuildDailyBudget(targetDateStochasticRetryPath));
            Write(targetDateOpportunityCostPath, BuildOpportunityCost(targetDateDailyTimeEnergyPath));
            return new()
            {
                RequirementInventoryPath = inventoryPath, AcquisitionLoweringPath = loweringPath,
                MasterAnglerWindowsPath = windowsPath, CalendarResolutionPath = routeCalendarPath,
                TargetDateCalendarPath = targetDateCalendarPath, TargetDateUnlockPath = targetDateUnlockPath,
                TargetDateFestivalPath = targetDateFestivalPath, TargetDateLocationPath = targetDateLocationPath,
                TargetDateFacilityPath = targetDateFacilityPath, TargetDateResourcePath = targetDateResourcePath,
                TargetDateCurrencyPath = targetDateCurrencyPath, TargetDateReservationPath = targetDateReservationPath,
                TargetDateProcessingPath = targetDateProcessingPath, TargetDateFishingProbabilityPath = targetDateFishingProbabilityPath,
                TargetDateStochasticRetryPath = targetDateStochasticRetryPath, TargetDateDailyTimeEnergyPath = targetDateDailyTimeEnergyPath,
                TargetDateOpportunityCostPath = targetDateOpportunityCostPath, FishingForecastManifestPath = fishingForecastManifestPath,
                StrategyLedgerPath = strategyLedgerPath, BeforeSnapshotPath = targetDateUnlockSnapshotPath,
                RouteTimingCalibrationPath = targetDateRouteCalibrationPath,
                PortfolioControlSelectionPath = Path.Combine(root, "rolling-control.json"),
                PortfolioProposalPath = targetDatePortfolioProposalPath, PortfolioAdmissionPath = targetDatePortfolioAdmissionPath,
                PortfolioCommitResultPath = targetDatePortfolioCommitResultPath, PortfolioCommitReceiptPath = targetDatePortfolioCommitReceiptPath,
                CommittedStrategyLedgerPath = targetDatePortfolioCommittedLedgerPath, ActionQueuePath = targetDateRouteQueuePath
            };
        }
    }
}
