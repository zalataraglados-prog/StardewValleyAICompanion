using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private void VerifyRodPrerequisite()
        {
            var unrelated = CollectionCandidate(
                "rod-prerequisite-unrelated-parsnip",
                "farm.maintain_crops",
                "harvest_crop_tile",
                "24",
                "(O)24",
                1,
                1,
                Parameter("projected_harvest_quality", "1"),
                Parameter("harvest_source_seed_id", "472"));
            unrelated.LocationId = "Farm";
            unrelated.TileX = 4;
            unrelated.TileY = 5;
            unrelated.EstimatedTicks = 60;

            var purchase = RodPurchaseCandidate("rod-prerequisite-purchase", 2);
            var noRod = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-missing",
                Array.Empty<object>(),
                new[] { unrelated, purchase });
            Require(
                noRod.Frontier.MasterAngler.RequiredGroupCount == 72 &&
                noRod.Frontier.MasterAngler.Requirements.Length == 72 &&
                noRod.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "current_positive_candidate" &&
                noRod.Frontier.MasterAngler
                    .CurrentSupportingPrerequisiteBindingCount == 1 &&
                noRod.Frontier.MasterAngler.SupportingPrerequisiteBindings.Single()
                    .CandidateId == purchase.CandidateId &&
                noRod.Frontier.SelectionContract.SelectionGroups.Any(group =>
                    group.RequirementId ==
                        MasterAnglerRodEquipmentAcquisitionProtocol.RequirementId &&
                    group.SelectionRule == "hard_prerequisite") &&
                noRod.Frontier.SelectionContract.CandidateChoices.Single(choice =>
                    choice.CandidateId == purchase.CandidateId)
                    .RequirementCredits.Single().BindingKind ==
                        "authoritative_supporting_prerequisite_rolling_route_step",
                "An exact Bamboo Pole purchase was not typed as the separate Master Angler hard prerequisite.");

            var preference = CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                noRod.Denominator);
            Write(preferencePath, preference);
            Require(
                preference.Status == "ready" &&
                preference.SelectedCandidate?.CandidateId == purchase.CandidateId &&
                preference.SelectedCandidate.SelectionReason ==
                    "unsatisfied_hard_prerequisite_credit_count" &&
                preference.OrderedCandidateEvaluations[0]
                    .UnsatisfiedHardPrerequisiteCreditCount == 1 &&
                preference.CompiledPlan?.Steps.Length == 1 &&
                preference.CompiledQueue?.Status == "pending" &&
                preference.CompiledQueue.Items.Length == 1,
                "The no-rod Teacher did not select and compile the existing exact purchase route ahead of unrelated collection work.");

            const string prerequisiteRunId = "fixture-rod-prerequisite-route";
            const string prerequisiteAfterStateHash =
                "stage-one-rod-prerequisite-route-after";
            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                prerequisiteAfterStateHash,
                fish,
                gameTick: 2,
                playerLocation: "Town",
                playerTileX: 1,
                playerTileY: 5,
                rodInventory: Array.Empty<object>());
            Write(
                receiptPath,
                StageOneRouteReceipt(
                    preference,
                    prerequisiteRunId,
                    prerequisiteAfterStateHash,
                    2,
                    snapshotPath,
                    afterSnapshotPath));
            var routeReceipt = CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                preferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-rod-prerequisite-route-trajectory",
                prerequisiteRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                noRod.Denominator);
            Require(
                routeReceipt.Status == "ready" &&
                routeReceipt.TeacherTrainingRowEligible &&
                routeReceipt.VerifiedRequirementTransitions.Length == 1 &&
                routeReceipt.VerifiedRequirementTransitions[0].Verified &&
                routeReceipt.VerifiedRequirementTransitions[0].BindingKind ==
                    "authoritative_supporting_prerequisite_rolling_route_step",
                "The exact Bamboo Pole route step did not produce a fresh verified prerequisite transition.");

            var ownedRod = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-owned",
                new object[]
                {
                    new
                    {
                        slot_index = 4,
                        qualified_item_id = "(T)IridiumRod"
                    }
                },
                new[] { unrelated, purchase });
            Require(
                ownedRod.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "satisfied" &&
                ownedRod.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "An already owned fishing rod emitted a duplicate Bamboo Pole prerequisite.");

            var malformed = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-malformed",
                new object[] { new { slot_index = 2, qualified_item_id = "" } },
                new[] { unrelated, purchase });
            Require(
                malformed.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "blocked_authoritative_rod_inventory_unavailable" &&
                malformed.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "A malformed rod inventory was treated as proof that no rod is owned.");

            var missing = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-field-missing",
                Array.Empty<object>(),
                new[] { unrelated, purchase },
                snapshot => snapshot["state"]!["fishing"]!.AsObject()
                    .Remove("rod_inventory"));
            Require(
                missing.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "blocked_authoritative_rod_inventory_unavailable" &&
                missing.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "A missing rod inventory field did not fail closed.");

            var unavailable = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-field-unavailable",
                Array.Empty<object>(),
                new[] { unrelated, purchase },
                snapshot => snapshot["state"]!["fishing"]!["rod_inventory"]![
                    "status"] = "unavailable");
            Require(
                unavailable.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "blocked_authoritative_rod_inventory_unavailable" &&
                unavailable.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "An unavailable rod inventory field did not fail closed.");

            var wrongShop = RodPurchaseCandidate("rod-prerequisite-wrong-shop", 2);
            wrongShop.ShopId = "SeedShop";
            var wrongIdentity = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-wrong-identity",
                Array.Empty<object>(),
                new[] { unrelated, wrongShop });
            Require(
                wrongIdentity.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "deferred_no_current_exact_rod_purchase_candidate" &&
                wrongIdentity.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "A purchase with the wrong shop identity was admitted as the rod prerequisite.");

            var duplicate = RodPurchaseCandidate(
                "rod-prerequisite-purchase-duplicate",
                3);
            var ambiguous = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-ambiguous",
                Array.Empty<object>(),
                new[] { unrelated, purchase, duplicate });
            Require(
                ambiguous.Frontier.MasterAngler.SupportingPrerequisiteStatus ==
                    "blocked_ambiguous_current_rod_purchase_candidates" &&
                ambiguous.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "Ambiguous exact Bamboo Pole purchase candidates did not fail closed.");
        }

        private (
            CurrentStageOneCollectionTeacherFrontier Frontier,
            CurrentCommunityCenterDenominatorReport Denominator)
            BuildRodPrerequisiteFrontier(
                string sourceStateHash,
                object[] rodInventory,
                PolicyEventCandidatePrediction[] candidates,
                Action<JsonObject>? mutateSnapshot = null)
        {
            WriteStageOneCollectionSnapshot(
                snapshotPath,
                sourceStateHash,
                fish,
                rodInventory: rodInventory);
            if (mutateSnapshot is not null)
            {
                var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))!
                    .AsObject();
                mutateSnapshot(snapshot);
                snapshot["state_hash"] = sourceStateHash;
                File.WriteAllText(
                    snapshotPath,
                    snapshot.ToJsonString(JsonDefaults.Options));
            }

            Write(
                intentsPath,
                MasterAnglerTargetDateIntentBuilder.Build(
                    windowsPath,
                    snapshotPath,
                    calibrationPath));
            Write(rankingPath, CollectionRanking(sourceStateHash, candidates));
            var denominator = CollectionDenominatorFixture(
                inventoryPath,
                snapshotPath,
                sourceStateHash,
                "remixed",
                CollectionDenominatorBundle(
                    "Pantry/5", "Pantry", 5, 1,
                    CollectionDenominatorIngredient(
                        0, "24", "(O)24", "item_id", 2, 1, false,
                        CollectionDenominatorTarget(
                            "24", "(O)24", "Parsnip", "harvests_as", "crop:472")),
                    CollectionDenominatorIngredient(
                        1, "188", "(O)188", "item_id", 1, 0, false,
                        CollectionDenominatorTarget(
                            "188", "(O)188", "Green Bean", "harvests_as", "crop:473"))));
            return (
                CurrentStageOneCollectionTeacherFrontierBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    denominator),
                denominator);
        }

        private static PolicyEventCandidatePrediction RodPurchaseCandidate(
            string candidateId,
            int rank)
        {
            var candidate = CollectionCandidate(
                candidateId,
                MasterAnglerRodEquipmentAcquisitionProtocol.OptionId,
                "route_connector_tile",
                "BambooPole",
                MasterAnglerRodEquipmentAcquisitionProtocol.QualifiedItemId,
                rank,
                MasterAnglerRodEquipmentAcquisitionProtocol.RequiredQuantity,
                Parameter("execution_option_id", "executor.traverse_connector"),
                Parameter("route.source_location_id", "Farm"),
                Parameter("route.target_location_id", "Town"),
                Parameter("route.connector_resolved", "true"),
                Parameter("route.snapshot_policy", "one_connector_then_fresh_snapshot"),
                Parameter("route.training_scope", "exact_current_cross_location_connector"),
                Parameter("target_tile_x", "2"),
                Parameter("target_tile_y", "4"),
                Parameter("connector_kind", "building_door"),
                Parameter("expected_target_location", "Town"),
                Parameter("expected_arrival_tile_x", "1"),
                Parameter("expected_arrival_tile_y", "5"),
                Parameter("estimated_ticks", "120"),
                Parameter("estimated_minutes", "2"),
                Parameter("continuation.option_id", "economy.buy_supplies"),
                Parameter("continuation.shop_id", "FishShop"),
                Parameter("continuation.target_location", "FishShop"),
                Parameter("continuation.item_id", "BambooPole"),
                Parameter("continuation.qualified_item_id", "(T)BambooPole"),
                Parameter("continuation.stock_id", "(T)BambooPole"),
                Parameter("continuation.output_stack_per_purchase", "1"),
                Parameter("continuation.output_quality", "0"),
                Parameter("continuation.max_unit_price", "500"),
                Parameter("continuation.quantity", "1"));
            candidate.ShopId = MasterAnglerRodEquipmentAcquisitionProtocol.ShopId;
            candidate.UnitPrice =
                MasterAnglerRodEquipmentAcquisitionProtocol.MaximumUnitPrice;
            candidate.TotalValue = candidate.UnitPrice;
            candidate.LocationId = "Farm";
            candidate.TileX = 2;
            candidate.TileY = 4;
            candidate.EstimatedTicks = 120;
            return candidate;
        }
    }
}
