using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
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

            var endpoint = RodPurchaseCandidate(
                "rod-prerequisite-shop-endpoint",
                2);
            endpoint.Kind = "interact_endpoint";
            endpoint.TileX = 1;
            endpoint.TileY = 4;
            Require(
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    endpoint,
                    out var endpointBindingKind) &&
                endpointBindingKind ==
                    "authoritative_supporting_prerequisite_shop_endpoint_open",
                "The exact FishShop endpoint was not classified as the rod-prerequisite interaction stage.");

            var terminal = RodPurchaseCandidate(
                "rod-prerequisite-terminal-purchase",
                2);
            terminal.Kind = "buy_shop_item";
            terminal.UnitPrice = 450;
            Require(
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    terminal,
                    out var terminalBindingKind) &&
                terminalBindingKind ==
                    "authoritative_supporting_prerequisite_terminal_purchase",
                "A valid sub-cap Bamboo Pole purchase was not classified as the terminal prerequisite stage.");

            var overpriced = RodPurchaseCandidate(
                "rod-prerequisite-overpriced",
                2);
            overpriced.Kind = "buy_shop_item";
            overpriced.UnitPrice = 501;
            Require(
                !MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    overpriced,
                    out _),
                "An over-cap Bamboo Pole purchase was admitted as the rod prerequisite.");

            var starterMail = StarterRodCandidate(
                "rod-prerequisite-starter-mail",
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailOptionId,
                "mailbox_approach",
                3,
                Parameter("continuation.mail_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId),
                Parameter("target_runtime_identity",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId));
            var nativePreferred = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-native-mail-preferred",
                Array.Empty<object>(),
                new[] { purchase, starterMail });
            Require(
                nativePreferred.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Single().CandidateId ==
                        starterMail.CandidateId &&
                nativePreferred.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings[0].BindingKind ==
                        "authoritative_supporting_prerequisite_starter_mail_approach" &&
                nativePreferred.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings[0].ShopId.Length == 0,
                "The native free starter-rod mail did not outrank the FishShop purchase fallback.");

            var springOnePurchase = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-spring-one-purchase-blocked",
                Array.Empty<object>(),
                new[] { purchase },
                totalDays: 0);
            Require(
                springOnePurchase.Frontier.MasterAngler
                    .SupportingPrerequisiteStatus ==
                        "deferred_no_current_exact_rod_acquisition_candidate" &&
                springOnePurchase.Frontier.MasterAngler
                    .SupportingPrerequisiteBindings.Length == 0,
                "The recovery purchase fallback spent starting money before the native spring-2 rod chain could arrive.");

            var starterMailWait = StarterRodCandidate(
                "rod-prerequisite-starter-mail-wait",
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailOptionId,
                "mail_input_wait",
                1,
                Parameter("target_runtime_type", "LetterViewerMenu"),
                Parameter("target_runtime_identity",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId),
                Parameter("continuation.option_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailOptionId));
            var starterMailProcess = StarterRodCandidate(
                "rod-prerequisite-starter-mail-process",
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailOptionId,
                "process_open_letter",
                1,
                Parameter("target_runtime_type", "LetterViewerMenu"),
                Parameter("target_runtime_identity",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId),
                Parameter("continuation.option_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailOptionId));
            var starterQuest = StarterRodCandidate(
                "rod-prerequisite-starter-quest",
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterQuestOptionId,
                "route_connector_tile",
                1,
                Parameter("quest_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterQuestId),
                Parameter("quest_runtime_type", "Quest"),
                Parameter("quest_next_action", "trigger_location_event"),
                Parameter("quest_target_location", "Beach"),
                Parameter("quest_event_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterEventId),
                Parameter("quest_event_required_mail_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId),
                Parameter("quest_event_source_asset", "Data/Events/Beach"),
                Parameter("quest_event_source_key",
                    "739330/t 600 1710/*n spring_2_1"));
            var starterEvent = StarterRodCandidate(
                "rod-prerequisite-starter-event",
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterEventOptionId,
                "advance_story_event_automatic",
                1,
                Parameter("story_event_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterEventId),
                Parameter("continuation.story_event_id",
                    MasterAnglerRodEquipmentAcquisitionProtocol.StarterEventId));
            Require(
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    starterMailWait,
                    out var waitBinding) &&
                waitBinding ==
                    "authoritative_supporting_prerequisite_starter_mail_wait" &&
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    starterMailProcess,
                    out var processBinding) &&
                processBinding ==
                    "authoritative_supporting_prerequisite_starter_mail_process" &&
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    starterQuest,
                    out var questBinding) &&
                questBinding ==
                    "authoritative_supporting_prerequisite_starter_quest_route_step" &&
                MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    starterEvent,
                    out var eventBinding) &&
                eventBinding ==
                    "authoritative_supporting_prerequisite_starter_event_grant",
                "One or more exact native first-rod stages were not admitted by identity.");

            starterQuest.Parameters = starterQuest.Parameters
                .Where(value => value.Name != "quest_target_location")
                .Append(Parameter("quest_target_location", "Mountain"))
                .ToArray();
            Require(
                !MasterAnglerRodEquipmentAcquisitionProtocol.TryMatchCandidate(
                    starterQuest,
                    out _),
                "A quest 13 route with the wrong target location was admitted as the native rod chain.");

            var endpointFrontier = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-endpoint",
                Array.Empty<object>(),
                new[] { endpoint },
                SetInteractionContext);
            var endpointPreference =
                CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    endpointFrontier.Denominator);
            Write(preferencePath, endpointPreference);
            Require(
                endpointPreference.CompiledQueue?.Items.Length == 1 &&
                endpointPreference.CompiledQueue.Items[0]
                    .NormalizedCommand.Steps.Single().StepType ==
                        "interact",
                "The exact FishShop endpoint did not compile through the existing single interaction primitive.");
            const string endpointRunId = "fixture-rod-prerequisite-endpoint";
            const string endpointAfterStateHash =
                "stage-one-rod-prerequisite-endpoint-after";
            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                endpointAfterStateHash,
                fish,
                gameTick: 2,
                rodInventory: Array.Empty<object>());
            MutateSnapshot(
                afterSnapshotPath,
                endpointAfterStateHash,
                SetOpenFishShop);
            Write(
                receiptPath,
                StageOneRouteReceipt(
                    endpointPreference,
                    endpointRunId,
                    endpointAfterStateHash,
                    2,
                    snapshotPath,
                    afterSnapshotPath));
            var endpointReceipt =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-rod-prerequisite-endpoint-trajectory",
                    endpointRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    endpointFrontier.Denominator);
            Require(
                endpointReceipt.Status == "ready" &&
                endpointReceipt.TeacherTrainingRowEligible &&
                endpointReceipt.VerifiedRequirementTransitions.Single()
                    .BindingKind ==
                        "authoritative_supporting_prerequisite_shop_endpoint_open" &&
                endpointReceipt.VerifiedRequirementTransitions[0].Verified,
                "Opening the exact writable FishShop menu did not produce a verified prerequisite transition.");

            var terminalFrontier = BuildRodPrerequisiteFrontier(
                "stage-one-rod-prerequisite-terminal",
                Array.Empty<object>(),
                new[] { terminal },
                SetOpenFishShop);
            var terminalPreference =
                CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    terminalFrontier.Denominator);
            Write(preferencePath, terminalPreference);
            var terminalQueue = terminalPreference.CompiledQueue ??
                throw new InvalidDataException(
                    "The Bamboo Pole terminal queue is missing.");
            Require(
                terminalQueue.Items.Length == 2 &&
                terminalQueue.Items[0]
                    .NormalizedCommand.Steps.Single().StepType ==
                        "buy_shop_item" &&
                terminalQueue.Items[1]
                    .NormalizedCommand.Steps.Single().StepType ==
                        "close_menu",
                "The Bamboo Pole terminal stage did not reuse the existing purchase-and-close queue.");
            const string terminalRunId = "fixture-rod-prerequisite-terminal";
            const string terminalAfterStateHash =
                "stage-one-rod-prerequisite-terminal-after";
            var terminalAfterTick = 1 + terminalQueue.Items.Length;
            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                terminalAfterStateHash,
                fish,
                gameTick: terminalAfterTick,
                rodInventory: new object[]
                {
                    new
                    {
                        slot_index = 4,
                        qualified_item_id = "(T)BambooPole"
                    }
                });
            var terminalBefore = CurrentTeacherFrontierSupport.Read<
                SnapshotEnvelope>(snapshotPath, "Rod purchase before snapshot");
            var terminalAfter = CurrentTeacherFrontierSupport.Read<
                SnapshotEnvelope>(afterSnapshotPath, "Rod purchase after snapshot");
            Write(
                receiptPath,
                RodPurchaseQueueReceipt(
                    terminalPreference,
                    terminalBefore,
                    terminalAfter,
                    terminalRunId));
            var terminalReceipt =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-rod-prerequisite-terminal-trajectory",
                    terminalRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    terminalFrontier.Denominator);
            Write(
                Path.Combine(root, "rod-terminal-receipt-admission.json"),
                terminalReceipt);
            Require(
                terminalReceipt.Status == "ready" &&
                terminalReceipt.TeacherTrainingRowEligible &&
                terminalReceipt.VerifiedRequirementTransitions.Single()
                    .BindingKind ==
                        "authoritative_supporting_prerequisite_terminal_purchase" &&
                terminalReceipt.VerifiedRequirementTransitions[0].Verified,
                "The exact Bamboo Pole zero-to-owned transition did not produce a verified prerequisite receipt: " +
                terminalReceipt.Status + ":" +
                string.Join(",", terminalReceipt.BlockingReasons));

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
                    "deferred_no_current_exact_rod_acquisition_candidate" &&
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
                    "blocked_ambiguous_current_rod_acquisition_candidates" &&
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
                Action<JsonObject>? mutateSnapshot = null,
                int totalDays = 5)
        {
            WriteStageOneCollectionSnapshot(
                snapshotPath,
                sourceStateHash,
                fish,
                totalDays: totalDays,
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

        private static PolicyEventCandidatePrediction StarterRodCandidate(
            string candidateId,
            string optionId,
            string kind,
            int rank,
            params SmallModelActionParameter[] parameters)
        {
            var candidate = CollectionCandidate(
                candidateId,
                optionId,
                kind,
                string.Empty,
                string.Empty,
                rank,
                0,
                parameters);
            candidate.LocationId = "Farm";
            candidate.EstimatedTicks = 60;
            return candidate;
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

        private static void SetOpenFishShop(JsonObject snapshot)
        {
            SetInteractionContext(snapshot);
            var player = snapshot["state"]!["player"]!.AsObject();
            player["money"] = JsonSerializer.SerializeToNode(
                NativeField(500, "vanilla_1_6"),
                JsonDefaults.Options);
            player["inventory"] = JsonSerializer.SerializeToNode(
                NativeField(Array.Empty<object>(), "vanilla_1_6"),
                JsonDefaults.Options);
            var menus = snapshot["state"]!["menus"]!.AsObject();
            menus["active_menu"] = JsonSerializer.SerializeToNode(
                NativeField(new { is_open = true, type = "ShopMenu" },
                    "vanilla_1_6"),
                JsonDefaults.Options);
            menus["sleep_prompt_context"] = JsonSerializer.SerializeToNode(
                NativeField(
                    new
                    {
                        prompt_open = false,
                        prompt_text = string.Empty,
                        yes_response_key = string.Empty
                    },
                    "vanilla_1_6"),
                JsonDefaults.Options);
            menus["shop_stock"] = JsonSerializer.SerializeToNode(
                NativeField(new
                {
                    shop_id = MasterAnglerRodEquipmentAcquisitionProtocol.ShopId,
                    read_only = false,
                    safety_timer = 0,
                    held_item_present = false,
                    currency = 0,
                    shop_on_purchase_present = false,
                    executor_purchase_enabled = true,
                    entries = new[]
                    {
                        new
                        {
                            item_id = "BambooPole",
                            qualified_item_id = "(T)BambooPole",
                            synced_key = "(T)BambooPole",
                            stack = 1,
                            quality = 0,
                            price = 450,
                            stock = int.MaxValue,
                            infinite_stock = true,
                            can_buy_item = true,
                            can_afford_one_with_currency = true,
                            can_afford_one_with_trade_item = true,
                            could_inventory_accept = true,
                            executor_purchase_enabled = true
                        }
                    }
                }, "vanilla_1_6"),
                JsonDefaults.Options);
        }

        private static void SetInteractionContext(JsonObject snapshot)
        {
            var state = snapshot["state"]!.AsObject();
            var player = state["player"]!.AsObject();
            player["facing_direction"] = JsonSerializer.SerializeToNode(
                NativeField(0, "vanilla_1_6"),
                JsonDefaults.Options);
            var currentLocation = state["current_location"]!.AsObject();
            currentLocation["route_context"] = JsonSerializer.SerializeToNode(
                NativeField(
                    new { probes = Array.Empty<object>() },
                    "vanilla_1_6_route"),
                JsonDefaults.Options);
            var menus = state["menus"]!.AsObject();
            menus["active_menu"] = JsonSerializer.SerializeToNode(
                NativeField(
                    new { is_open = false, type = "none" },
                    "vanilla_1_6"),
                JsonDefaults.Options);
            var locations = state["locations"]!.AsObject();
            locations["route_action_branch_coverage"] =
                JsonSerializer.SerializeToNode(
                    NativeField(new
                    {
                        rows = new[]
                        {
                            new
                            {
                                tile_x = 1,
                                tile_y = 4,
                                branch = "OpenShop",
                                route_training_blocked = false
                            }
                        }
                    }, "vanilla_1_6_route"),
                    JsonDefaults.Options);
        }

        private static void MutateSnapshot(
            string path,
            string stateHash,
            Action<JsonObject> mutation)
        {
            var snapshot = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mutation(snapshot);
            snapshot["state_hash"] = stateHash;
            File.WriteAllText(
                path,
                snapshot.ToJsonString(JsonDefaults.Options));
        }

        private static QueueExecutionReceiptEnvelope RodPurchaseQueueReceipt(
            CurrentStageOneCollectionTeacherPreferenceLabel preference,
            SnapshotEnvelope before,
            SnapshotEnvelope after,
            string runId)
        {
            var queue = preference.CompiledQueue ?? throw new InvalidDataException(
                "Rod purchase queue is missing.");
            var boundaries = new List<string> { before.StateHash };
            boundaries.AddRange(Enumerable.Range(0, queue.Items.Length - 1)
                .Select(index => new string((char)('a' + index), 64)));
            boundaries.Add(after.StateHash);
            var steps = queue.Items.Select((item, index) =>
            {
                var effectiveItem = JsonSerializer.Deserialize<ActionQueueItem>(
                    JsonSerializer.Serialize(item, JsonDefaults.Options),
                    JsonDefaults.Options) ?? throw new InvalidDataException(
                    "Rod purchase queue item clone failed.");
                effectiveItem.NormalizedCommand.StateHash = boundaries[index];
                return new QueueExecutionStepReceipt
                {
                    QueueItemIndex = index,
                    QueueItemCount = queue.Items.Length,
                    OriginalPlannedItemCount = queue.Items.Length,
                    QueueId = queue.QueueId,
                    QueueItemId = item.QueueItemId,
                    OptionId = item.OptionId,
                    SourceStateHash = boundaries[index],
                    CompiledCommandStateHash = before.StateHash,
                    TeacherPreferenceStateRebound = true,
                    SelectedQueueCandidateCompleted =
                        index == queue.Items.Length - 1,
                    AfterStateHash = boundaries[index + 1],
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick + index,
                    AfterGameTick = before.GameTick + index + 1,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = item.NormalizedCommand.Steps.Single().StepType,
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_rod_purchase_macro_step_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        effectiveItem,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(
                        new[] { "native_rod_purchase_state_changed" },
                        JsonDefaults.Options)
                };
            }).ToArray();
            return new QueueExecutionReceiptEnvelope
            {
                RunId = runId,
                QueueId = queue.QueueId,
                SourceStateHash = before.StateHash,
                AfterStateHash = after.StateHash,
                BeforeGameTick = before.GameTick,
                AfterGameTick = after.GameTick,
                AfterSnapshotFresh = true,
                QueueExecutionMode = "sequential_queue_items",
                Status = "applied",
                Success = true,
                PlannedItemCount = queue.Items.Length,
                ExecutedItemCount = queue.Items.Length,
                FinalPendingItemCount = 0,
                MaxQueueItemAttempts = queue.Items.Length,
                SelectedCandidateId = preference.SelectedCandidate!.CandidateId,
                SelectedCandidateCompleted = true,
                StepResults = steps
            };
        }
    }
}
