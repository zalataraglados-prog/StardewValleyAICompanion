using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private void VerifyCurrentFrontier(
            AcquisitionRouteCalendarResolutionReport routeCalendar,
            AcquisitionRouteTargetDateCalendarReport targetDateCalendar)
        {
            var communityCenterDenominator = CollectionDenominatorFixture(
                inventoryPath,
                snapshotPath,
                stateHash,
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

            var calendarCommunityCenterDenominator = CollectionDenominatorFixture(
                inventoryPath,
                snapshotPath,
                stateHash,
                "remixed",
                communityCenterDenominator.ActiveBundles[0],
                CollectionDenominatorBundle(
                    "Pantry/6", "Pantry", 6, 1,
                    CollectionDenominatorIngredient(
                        0, "-5", string.Empty, "category", 1, 0, false,
                        CollectionDenominatorTarget(
                            "176", "(O)176", "Egg", "sells", "shop:FixtureShop"))));
            var currentRouteCalendar = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                snapshotPath,
                calendarCommunityCenterDenominator);
            Write(currentRouteCalendarPath, currentRouteCalendar);
            var currentCommunityCenterRoutes = currentRouteCalendar.Routes
                .Where(route => route.RequirementSetId ==
                    "community_center_standard")
                .ToArray();
            var staticNonCommunityCenterRoutes = JsonSerializer.Serialize(
                routeCalendar.Routes.Where(route => route.RequirementSetId !=
                    "community_center_standard").ToArray(),
                JsonDefaults.Options);
            var currentNonCommunityCenterRoutes = JsonSerializer.Serialize(
                currentRouteCalendar.Routes.Where(route => route.RequirementSetId !=
                    "community_center_standard").ToArray(),
                JsonDefaults.Options);
            Require(currentRouteCalendar.UsesCurrentCommunityCenterDenominator &&
                    currentRouteCalendar.CommunityCenterBundleMode == "remixed" &&
                    currentRouteCalendar.CommunityCenterDenominatorSha256 ==
                        calendarCommunityCenterDenominator.DenominatorSha256 &&
                    currentRouteCalendar.CommunityCenterSourceStateHash == stateHash &&
                    currentRouteCalendar.CommunityCenterSnapshotSha256 ==
                        HashFile(snapshotPath) &&
                    currentRouteCalendar.RouteOccurrenceCount == 77 &&
                    currentCommunityCenterRoutes.Length == 3 &&
                    currentCommunityCenterRoutes.Count(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/5") == 2 &&
                    !currentCommunityCenterRoutes.Any(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/5" &&
                        route.RouteKind == "sells") &&
                    currentCommunityCenterRoutes.Any(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/6" &&
                        route.AlternativeIndex == 0 &&
                        route.ItemId == "176" &&
                        route.QualifiedItemId == "(O)176" &&
                        route.MatchKind == "category" &&
                        route.RouteKind == "sells") &&
                    currentNonCommunityCenterRoutes ==
                        staticNonCommunityCenterRoutes,
                "Current Community Center route calendar did not replace static routes or preserve concrete category targets.");

            var currentTargetDateCalendar =
                AcquisitionRouteTargetDateCalendarBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    currentRouteCalendarPath,
                    snapshotPath,
                    calendarCommunityCenterDenominator,
                    0);
            Write(currentTargetDateCalendarPath, currentTargetDateCalendar);
            var currentTargetDateCommunityCenterRoutes = currentTargetDateCalendar.Routes
                .Where(route => route.RequirementSetId ==
                    "community_center_standard")
                .ToArray();
            var staticNonCommunityCenterTargetDateRoutes = JsonSerializer.Serialize(
                targetDateCalendar.Routes.Where(route => route.RequirementSetId !=
                    "community_center_standard").ToArray(),
                JsonDefaults.Options);
            var currentNonCommunityCenterTargetDateRoutes = JsonSerializer.Serialize(
                currentTargetDateCalendar.Routes.Where(route =>
                    route.RequirementSetId != "community_center_standard").ToArray(),
                JsonDefaults.Options);
            Require(currentTargetDateCalendar.Status ==
                        "partial_target_date_calendar_axis_source_blocks" &&
                    currentTargetDateCalendar.UsesCurrentCommunityCenterDenominator &&
                    currentTargetDateCalendar.CommunityCenterBundleMode ==
                        "remixed" &&
                    currentTargetDateCalendar.CommunityCenterDenominatorSha256 ==
                        calendarCommunityCenterDenominator.DenominatorSha256 &&
                    currentTargetDateCalendar.CommunityCenterSourceStateHash ==
                        stateHash &&
                    currentTargetDateCalendar.CommunityCenterSnapshotSha256 ==
                        HashFile(snapshotPath) &&
                    currentTargetDateCalendar.StaticCalendarResolutionSha256 ==
                        HashFile(currentRouteCalendarPath) &&
                    currentTargetDateCalendar.RouteOccurrenceCount == 77 &&
                    currentTargetDateCalendar.CalendarAxisResolvedCount == 76 &&
                    currentTargetDateCalendar.StaticWindowMatchCount == 4 &&
                    currentTargetDateCalendar.StaticWindowMissCount == 72 &&
                    currentTargetDateCalendar.BlockedStaticSourceCount == 1 &&
                    currentTargetDateCommunityCenterRoutes.Length == 3 &&
                    !currentTargetDateCommunityCenterRoutes.Any(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/5" &&
                        route.RouteKind == "sells") &&
                    currentTargetDateCommunityCenterRoutes.Any(route =>
                        route.RequirementId ==
                            "community_center:bundle:Pantry/6" &&
                        route.QualifiedItemId == "(O)176" &&
                        route.MatchKind == "category" &&
                        route.RouteKind == "sells" &&
                        !route.CalendarAxisResolved &&
                        route.CalendarAxisStatus ==
                            "blocked_static_calendar_source_unresolved") &&
                    currentNonCommunityCenterTargetDateRoutes ==
                        staticNonCommunityCenterTargetDateRoutes &&
                    !currentTargetDateCalendar.TrainingLabelEligible,
                "Current Community Center target-date calendar did not preserve the dynamic route root and provenance.");

            var currentProvenanceSnapshot = CurrentTeacherFrontierSupport.Read<
                SnapshotEnvelope>(
                snapshotPath,
                "Current Community Center provenance snapshot");
            var currentPortfolioProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.From(
                    currentTargetDateCalendar,
                    currentProvenanceSnapshot,
                    HashFile(snapshotPath));
            Require(currentPortfolioProvenance
                        .UsesCurrentCommunityCenterDenominator &&
                    currentPortfolioProvenance.CommunityCenterBundleMode ==
                        "remixed" &&
                    currentPortfolioProvenance
                        .CommunityCenterDenominatorSha256 ==
                        calendarCommunityCenterDenominator.DenominatorSha256 &&
                    currentPortfolioProvenance
                        .CommunityCenterSourceStateHash == stateHash &&
                    currentPortfolioProvenance
                        .CommunityCenterSnapshotSha256 == HashFile(snapshotPath),
                "Current Community Center portfolio provenance drifted.");
            var staleCurrentPortfolioProvenanceRejected = false;
            var staleCurrentTargetDateCalendar = JsonSerializer.Deserialize<
                AcquisitionRouteTargetDateCalendarReport>(
                    JsonSerializer.Serialize(
                        currentTargetDateCalendar,
                        JsonDefaults.Options),
                    JsonDefaults.Options)!;
            staleCurrentTargetDateCalendar.CommunityCenterSourceStateHash =
                "stale-state";
            try
            {
                AcquisitionRouteCommunityCenterProvenanceSupport.From(
                    staleCurrentTargetDateCalendar,
                    currentProvenanceSnapshot,
                    HashFile(snapshotPath));
            }
            catch (InvalidDataException)
            {
                staleCurrentPortfolioProvenanceRejected = true;
            }
            Require(staleCurrentPortfolioProvenanceRejected,
                "Stale current Community Center portfolio provenance was accepted.");

            var tamperedCurrentRouteCalendar = JsonSerializer.Deserialize<
                AcquisitionRouteCalendarResolutionReport>(
                    JsonSerializer.Serialize(
                        currentRouteCalendar,
                        JsonDefaults.Options),
                    JsonDefaults.Options)!;
            tamperedCurrentRouteCalendar.CommunityCenterDenominatorSha256 =
                new string('0', 64);
            Write(tamperedCurrentRouteCalendarPath, tamperedCurrentRouteCalendar);
            var tamperedCurrentRouteCalendarRejected = false;
            try
            {
                AcquisitionRouteTargetDateCalendarBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    windowsPath,
                    tamperedCurrentRouteCalendarPath,
                    snapshotPath,
                    calendarCommunityCenterDenominator,
                    0);
            }
            catch (InvalidDataException)
            {
                tamperedCurrentRouteCalendarRejected = true;
            }
            Require(tamperedCurrentRouteCalendarRejected,
                "A target-date calendar accepted tampered current Community Center provenance.");

            var intents = MasterAnglerTargetDateIntentBuilder.Build(
                windowsPath,
                snapshotPath,
                calibrationPath);
            Write(intentsPath, intents);
            var parameters = intents.Candidates.Single().Parameters;
            var sharedCandidate = CollectionCandidate(
                    "shared-parsnip-harvest",
                    "farm.maintain_crops",
                    "harvest_crop_tile",
                    "24",
                    "(O)24",
                    99,
                    1,
                    Parameter("projected_harvest_quality", "1"),
                    Parameter("harvest_source_seed_id", "472"));
            sharedCandidate.LocationId = "Farm";
            sharedCandidate.TileX = 4;
            sharedCandidate.TileY = 5;
            sharedCandidate.EstimatedTicks = 60;
            sharedCandidate.Score = 9000;
            sharedCandidate.ModelScore = 8000;
            sharedCandidate.ExpectedReward = 7000;
            var routeCandidate = CollectionCandidate(
                    "master-angler-route",
                    "fishing.catch_fish",
                    "route_connector_tile",
                    string.Empty,
                    string.Empty,
                    100,
                    0,
                    parameters.Concat(new[]
                    {
                        Parameter("continuation.option_id", "fishing.catch_fish"),
                        Parameter(
                            "master_angler_time_budget_status",
                            "conservative_full_remaining_connector_path_and_terminal_reserve"),
                        Parameter("connector_kind", "building_door"),
                        Parameter("expected_target_location", "Town"),
                        Parameter("expected_arrival_tile_x", "1"),
                        Parameter("expected_arrival_tile_y", "5"),
                        Parameter("estimated_minutes", "2")
                    }).ToArray());
            routeCandidate.AvailabilityClass = "master_angler_rolling_route";
            routeCandidate.LocationId = "Farm";
            routeCandidate.TileX = 2;
            routeCandidate.TileY = 4;
            routeCandidate.EstimatedTicks = 120;
            routeCandidate.Score = -9000;
            routeCandidate.ModelScore = -8000;
            routeCandidate.ExpectedReward = -7000;
            var wrongCrabCandidate = CollectionCandidate(
                    "wrong-crab-output",
                    "fishing.collect_crab_pots",
                    "collect_crab_pot",
                    "test_1",
                    "(O)test_1",
                    1,
                    1,
                    parameters.Concat(new[]
                    {
                        Parameter(
                            "continuation.option_id",
                            "fishing.collect_crab_pots"),
                        Parameter(
                            "master_angler_time_budget_status",
                            "conservative_full_remaining_connector_path_and_terminal_reserve"),
                        Parameter("expected_fish_collection_eligible", "1")
                    }).ToArray());
            wrongCrabCandidate.AvailabilityClass =
                "master_angler_exact_ready_crab_pot";
            Write(rankingPath, CollectionRanking(
                stateHash,
                sharedCandidate,
                routeCandidate,
                wrongCrabCandidate));

            var result = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                communityCenterDenominator);
            Require(result.Status == "candidate_contract_ready" &&
                    result.RequirementSetCount == 4 &&
                    result.CurrentCandidateMembershipEligible &&
                    !result.TeacherPreferenceLabelEligible &&
                    !result.UsesLearnerRankOrScore &&
                    !result.EmitsNegativeLabelsForUnavailableRoutes,
                "Unified Stage 1 collection candidate contract policy drifted.");
            Require(result.MasterAngler.RequiredGroupCount == 72 &&
                    result.MasterAngler.ObservedGroupCount == 72 &&
                    result.MasterAngler.MissingGroupCount == 1 &&
                    result.MasterAngler.CurrentIntentCount == 1 &&
                    result.MasterAngler.CandidateBindings.Length == 1 &&
                    result.MasterAngler.RejectedIntentCandidates.Single().CandidateId ==
                        "wrong-crab-output",
                "Master Angler current requirement binding or strict crab-pot identity drifted.");
            var shared = result.SelectionContract.CandidateChoices.Single(value =>
                value.CandidateId == "shared-parsnip-harvest");
            Require(shared.RequirementCredits.Length == 2 &&
                    shared.RequirementCredits.Select(value => value.RequirementSetId)
                        .ToHashSet(StringComparer.Ordinal)
                        .SetEquals(new[]
                        {
                            "full_shipment",
                            "community_center_standard"
                        }),
                "One current candidate was not deduplicated across exact requirement credits.");
            Require(result.SelectionContract.SelectionGroups.Length == 4 &&
                    result.SelectionContract.CandidateChoices.Length == 2 &&
                    result.SelectionContract.SelectionGroups.All(value =>
                        value.MaximumSelectedCandidateCountThisDecision is 0 or 1),
                "Unified collection selection cardinality drifted.");

            var preference =
                CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    communityCenterDenominator);
            Require(preference.Status == "ready" &&
                    preference.TeacherPreferenceLabelEligible &&
                    !preference.FormalTrainingAuthorized &&
                    !preference.UsesLearnerRankOrScore &&
                    !preference.EmitsNegativeLabelsForUnavailableRoutes &&
                    preference.SelectedCandidate?.CandidateId ==
                        "master-angler-route" &&
                    preference.SelectedCandidate.SelectionReason ==
                        "authoritative_current_day_deadline" &&
                    preference.PairwisePreferences.Length == 1 &&
                    preference.PairwisePreferences[0]
                        .FirstDifferingAuthoritativeCriterion ==
                        "authoritative_current_day_deadline" &&
                    preference.CompiledPlan?.Steps.Length == 1 &&
                    preference.CompiledQueue?.Status == "pending" &&
                    preference.CompiledQueue.Items.Length == 1,
                "Independent current collection Teacher preference did not select and compile the authoritative deadline candidate.");
            Write(preferencePath, preference);

            const string teacherRunId = "fixture-teacher-receipt-run";
            const string afterStateHash =
                "stage-one-collection-frontier-after-route-state";
            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                afterStateHash,
                fish,
                gameTick: 2,
                playerLocation: "Town",
                playerTileX: 1,
                playerTileY: 5);
            Write(
                receiptPath,
                StageOneRouteReceipt(
                    preference,
                    teacherRunId,
                    afterStateHash,
                    2,
                    snapshotPath,
                    afterSnapshotPath));
            var receiptAdmission =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-teacher-trajectory",
                    teacherRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    communityCenterDenominator);
            Require(receiptAdmission.Status == "ready" &&
                    receiptAdmission.TeacherTrainingRowEligible &&
                    !receiptAdmission.FormalTrainingAuthorized &&
                    receiptAdmission.VerifiedRequirementTransitions.Length == 1 &&
                    receiptAdmission.VerifiedRequirementTransitions[0].Verified &&
                    receiptAdmission.TrainingRow?.Candidates.Length == 2 &&
                    receiptAdmission.TrainingRow.Candidates.All(candidate =>
                        candidate.Available &&
                        candidate.Score == 0 &&
                        candidate.SourceCandidate.ModelScore is null) &&
                    receiptAdmission.TrainingRow.Audit.TeacherSupervision is { } teacherSupervision &&
                    teacherSupervision.SelectedCandidateId ==
                        "master-angler-route" &&
                    teacherSupervision.CommunityCenterDenominatorSha256 ==
                        communityCenterDenominator.DenominatorSha256 &&
                    teacherSupervision.RequirementTransitions.Length == 1,
                "A fresh exact route receipt did not produce a standalone Teacher-supervised policy row.");
            var teacherDatasetPath = Path.Combine(root, "teacher-receipt.jsonl");
            WriteJsonl(
                teacherDatasetPath,
                new[] { receiptAdmission.TrainingRow! });
            var dataset = new PolicyTrajectoryDatasetBuilder().Build(
                teacherDatasetPath,
                Path.Combine(root, "teacher-receipt-dataset"),
                expectedKnowledgeDictionary:
                    PolicyTrajectoryVersionPins.KnowledgeDictionary);
            Require(dataset.Manifest.Counts.AcceptedRows == 1 &&
                    dataset.Manifest.Counts.RejectedRows == 0,
                "The canonical policy dataset rejected an exact Teacher-supervised receipt row.");
            var tamperedTeacherRow = JsonSerializer.Deserialize<
                PolicyDecisionTrajectoryEnvelope>(
                JsonSerializer.Serialize(
                    receiptAdmission.TrainingRow,
                    JsonDefaults.Options),
                JsonDefaults.Options)!;
            tamperedTeacherRow.TrajectoryId = "fixture-tampered-teacher-evidence";
            tamperedTeacherRow.Audit.TeacherSupervision!.AfterSnapshotSha256 =
                "not-a-sha256";
            var tamperedDatasetPath = Path.Combine(
                root,
                "tampered-teacher-receipt.jsonl");
            WriteJsonl(
                tamperedDatasetPath,
                new[] { tamperedTeacherRow, receiptAdmission.TrainingRow! });
            var tamperedDataset = new PolicyTrajectoryDatasetBuilder().Build(
                tamperedDatasetPath,
                Path.Combine(root, "tampered-teacher-receipt-dataset"),
                expectedKnowledgeDictionary:
                    PolicyTrajectoryVersionPins.KnowledgeDictionary);
            Require(tamperedDataset.Manifest.Counts.AcceptedRows == 1 &&
                    tamperedDataset.Manifest.Counts.RejectedRows == 1 &&
                    tamperedDataset.Manifest.Rejections.Any(value =>
                        value.Reason == "teacher_supervision_invalid" &&
                        value.Count == 1),
                "The canonical policy dataset accepted tampered Teacher provenance.");

            var uppercaseHashPreference = JsonSerializer.Deserialize<
                CurrentStageOneCollectionTeacherPreferenceLabel>(
                JsonSerializer.Serialize(preference, JsonDefaults.Options),
                JsonDefaults.Options)!;
            var hashCharacters = uppercaseHashPreference
                .RequirementInventorySha256.ToCharArray();
            var letterIndex = Array.FindIndex(hashCharacters, char.IsLetter);
            Require(letterIndex >= 0,
                "The fixture SHA-256 unexpectedly contains no hexadecimal letters.");
            hashCharacters[letterIndex] = char.ToUpperInvariant(
                hashCharacters[letterIndex]);
            uppercaseHashPreference.RequirementInventorySha256 =
                new string(hashCharacters);
            var uppercaseHashPreferencePath = Path.Combine(
                root,
                "uppercase-hash-preference.json");
            Write(uppercaseHashPreferencePath, uppercaseHashPreference);
            var uppercaseHashAdmission =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    uppercaseHashPreferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-uppercase-hash",
                    teacherRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    communityCenterDenominator);
            Require(uppercaseHashAdmission.Status ==
                        "blocked_teacher_preference_not_ready" &&
                    uppercaseHashAdmission.BlockingReasons.Contains(
                        "teacher_preference_source_hash_mismatch",
                        StringComparer.Ordinal),
                "A non-canonical Teacher source hash was admitted.");

            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                "stage-one-collection-no-route-transition",
                fish,
                gameTick: 2,
                playerLocation: "Farm",
                playerTileX: 3,
                playerTileY: 5);
            Write(
                receiptPath,
                StageOneRouteReceipt(
                    preference,
                    teacherRunId,
                    "stage-one-collection-no-route-transition",
                    2,
                    snapshotPath,
                    afterSnapshotPath));
            var noTransition =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-no-transition",
                    teacherRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    communityCenterDenominator);
            Require(noTransition.Status ==
                        "blocked_exact_requirement_transition_missing" &&
                    !noTransition.TeacherTrainingRowEligible &&
                    noTransition.TrainingRow is null,
                "A successful receipt without the credited route transition emitted a training row.");

            WriteStageOneCollectionSnapshot(
                afterSnapshotPath,
                afterStateHash,
                fish,
                gameTick: 2,
                playerLocation: "Town",
                playerTileX: 1,
                playerTileY: 5);
            var mismatchedReceipt = StageOneRouteReceipt(
                preference,
                teacherRunId,
                afterStateHash,
                2,
                snapshotPath,
                afterSnapshotPath);
            mismatchedReceipt.QueueId = "wrong-queue";
            Write(receiptPath, mismatchedReceipt);
            var wrongQueue =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-wrong-queue",
                    teacherRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    communityCenterDenominator);
            Require(wrongQueue.Status == "blocked_execution_receipt_not_exact" &&
                    wrongQueue.BlockingReasons.Contains(
                        "execution_receipt_queue_id_mismatch",
                        StringComparer.Ordinal),
                "A receipt from another compiled queue was admitted.");

            var mismatchedPrimitiveReceipt = StageOneRouteReceipt(
                preference,
                teacherRunId,
                afterStateHash,
                2,
                snapshotPath,
                afterSnapshotPath);
            mismatchedPrimitiveReceipt.PrimitiveKind = "move_to_tile";
            mismatchedPrimitiveReceipt.EffectiveQueueItem = null;
            Write(receiptPath, mismatchedPrimitiveReceipt);
            var wrongPrimitive =
                CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    preferencePath,
                    receiptPath,
                    afterSnapshotPath,
                    "fixture-wrong-primitive",
                    teacherRunId,
                    PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                    communityCenterDenominator);
            Require(wrongPrimitive.Status ==
                        "blocked_execution_receipt_not_exact" &&
                    wrongPrimitive.BlockingReasons.Contains(
                        "execution_receipt_primitive_kind_mismatch",
                        StringComparer.Ordinal) &&
                    wrongPrimitive.BlockingReasons.Contains(
                        "execution_receipt_effective_queue_item_mismatch",
                        StringComparer.Ordinal),
                "A receipt for another primitive or without its effective queue item was admitted.");

            sharedCandidate.Rank = 1;
            sharedCandidate.Score = 1_000_000;
            sharedCandidate.ModelScore = 1_000_000;
            sharedCandidate.ExpectedReward = 1_000_000;
            routeCandidate.Rank = 1000;
            routeCandidate.Score = -1_000_000;
            routeCandidate.ModelScore = -1_000_000;
            routeCandidate.ExpectedReward = -1_000_000;
            Write(rankingPath, CollectionRanking(
                stateHash,
                sharedCandidate,
                routeCandidate,
                wrongCrabCandidate));
            var learnerSignalInvariant =
                CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    communityCenterDenominator);
            Require(learnerSignalInvariant.Status == "ready" &&
                    learnerSignalInvariant.SelectedCandidate?.CandidateId ==
                        "master-angler-route" &&
                    learnerSignalInvariant.CompiledPlan?.Steps.Length == 1 &&
                    learnerSignalInvariant.CompiledQueue?.Items.Length == 1,
                "Learner rank or score changed the deterministic Teacher preference.");

            var tieA = CollectionCandidate(
                "tied-parsnip-a",
                "farm.maintain_crops",
                "harvest_crop_tile",
                "24",
                "(O)24",
                1,
                1,
                Parameter("harvest_source_seed_id", "472"));
            var tieB = CollectionCandidate(
                "tied-parsnip-b",
                "farm.maintain_crops",
                "harvest_crop_tile",
                "24",
                "(O)24",
                2,
                1,
                Parameter("harvest_source_seed_id", "472"));
            foreach (var candidate in new[] { tieA, tieB })
            {
                candidate.LocationId = "Farm";
                candidate.TileX = 4;
                candidate.TileY = 5;
                candidate.EstimatedTicks = 60;
            }
            Write(rankingPath, CollectionRanking(stateHash, tieA, tieB));
            var tiedPreference =
                CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    communityCenterDenominator);
            Require(tiedPreference.Status ==
                        "blocked_authoritatively_tied_top_candidates" &&
                    !tiedPreference.TeacherPreferenceLabelEligible &&
                    tiedPreference.SelectedCandidate is null &&
                    tiedPreference.BlockingReasons.Length == 1,
                "An arbitrary candidate ID was used to manufacture a Teacher preference tie-break.");

            intents.SourceStateHash = "stale-master-angler-intent-state";
            Write(intentsPath, intents);
            var staleIntentRejected = false;
            try
            {
                _ = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
                    inventoryPath,
                    loweringPath,
                    rankingPath,
                    snapshotPath,
                    intentsPath,
                    communityCenterDenominator);
            }
            catch (InvalidDataException)
            {
                staleIntentRejected = true;
            }
            Require(staleIntentRejected,
                "A Master Angler intent from a different decision state was admitted.");
        }
    }
}
