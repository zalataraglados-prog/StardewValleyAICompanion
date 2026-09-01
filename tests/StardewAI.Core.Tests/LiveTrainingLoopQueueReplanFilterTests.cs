using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingLoopQueueReplanFilterTests
{
    [Fact]
    public void MainLoopExecutesPendingSubsetOnlyWhenContinueFlagIsEnabled()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.cs"));

        Assert.Contains("options.ContinueAfterBlockedQueueItems", source, StringComparison.Ordinal);
        Assert.Contains("ExecutableQueueItems(queue).Length", source, StringComparison.Ordinal);
        Assert.Contains("executing_pending_subset", source, StringComparison.Ordinal);
        Assert.Contains("executableSubsetCount == 0", source, StringComparison.Ordinal);

        var inspection = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.QueueInspection.cs"));
        Assert.Contains(".TakeWhile(item => item is not null && IsExecutableQueueItem(item))", inspection, StringComparison.Ordinal);
        Assert.DoesNotContain(".Where(item => item is not null && IsExecutableQueueItem(item))", inspection, StringComparison.Ordinal);
        Assert.Contains("\"clock\"", inspection, StringComparison.Ordinal);
        Assert.Contains("ReadFullSnapshotAsync", inspection, StringComparison.Ordinal);
        Assert.Contains("forceRefresh: false", inspection, StringComparison.Ordinal);
        Assert.Contains("product_after_snapshot_cache_match", inspection, StringComparison.Ordinal);
        Assert.Contains("product_after_state_hash", inspection, StringComparison.Ordinal);
        Assert.Contains("product_after_game_tick", inspection, StringComparison.Ordinal);

        var jsonHttp = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.JsonHttp.cs"));
        Assert.Contains("SnapshotUrlForProfile", jsonHttp, StringComparison.Ordinal);
        Assert.Contains("query.Add(\"fresh=1\")", jsonHttp, StringComparison.Ordinal);
        Assert.Contains("expected_state_hash=", jsonHttp, StringComparison.Ordinal);
        Assert.Contains("expected_game_tick=", jsonHttp, StringComparison.Ordinal);

        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        Assert.Contains("TryReadExpectedSnapshotCacheIdentity", bridge, StringComparison.Ordinal);
        Assert.Contains("SnapshotMatchesExpectedCacheIdentity", bridge, StringComparison.Ordinal);
        Assert.Contains("forceRefresh = true", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLoopReleasesUnavailableContinuationBeforeNoProgressBackoff()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.cs"));
        var queueBuildingSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.QueueBuilding.cs"));
        var policyTrajectorySource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.PolicyTrajectory.cs"));
        var runtimeExecutionSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs"));
        var connectorRuntimeSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MovementSleep.cs"));

        Assert.Contains("ShouldReleaseUnavailableContinuation", source, StringComparison.Ordinal);
        Assert.Contains("continuation_released", source, StringComparison.Ordinal);
        Assert.Contains("activeObjectiveContinuation = null", source, StringComparison.Ordinal);
        Assert.Contains("noProgressBackoff.Reset()", source, StringComparison.Ordinal);
        Assert.Contains("suppressedObjectiveContinuations", source, StringComparison.Ordinal);
        Assert.Contains("objective_suppression_reset", source, StringComparison.Ordinal);
        Assert.Contains("completed_objective_continuations", source, StringComparison.Ordinal);
        Assert.Contains("AddSuppressedContinuation", source, StringComparison.Ordinal);
        Assert.Contains("completed_objective_continuations", runtimeExecutionSource, StringComparison.Ordinal);
        Assert.Contains("AddSuppressedContinuation", runtimeExecutionSource, StringComparison.Ordinal);
        Assert.Contains("FilterSuppressedContinuations", queueBuildingSource, StringComparison.Ordinal);
        Assert.Contains("ContinuationRequestParameters(objectiveContinuation)", queueBuildingSource, StringComparison.Ordinal);
        Assert.Contains("\"continuation.\" + property.Key", queueBuildingSource, StringComparison.Ordinal);
        Assert.Contains("IsTrainingVerifiedExecution(execution)", source, StringComparison.Ordinal);
        Assert.Contains("IsTrainingVerifiedExecution(step)", policyTrajectorySource, StringComparison.Ordinal);
        Assert.Contains("policyAppend.AppendedCount == 0 &&", source, StringComparison.Ordinal);
        Assert.Contains("horizonObservations == 0", source, StringComparison.Ordinal);
        Assert.Contains("connector_native_arrival_tile_adjusted", connectorRuntimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("reasons.Add(\"connector_unexpected_arrival_tile\")", connectorRuntimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuationLeaseReleasesOnlyWhenActiveFilterHasNoCandidate()
    {
        var continuation = new JsonObject
        {
            ["option_id"] = "economy.buy_supplies"
        };
        var unavailable = new JsonObject
        {
            ["objective_continuation_filter"] = new JsonObject
            {
                ["active"] = true,
                ["selected_candidate_count"] = 0
            }
        };
        var available = new JsonObject
        {
            ["objective_continuation_filter"] = new JsonObject
            {
                ["active"] = true,
                ["selected_candidate_count"] = 1
            }
        };

        Assert.True(QueueReplanFilter.ShouldReleaseUnavailableContinuation(continuation, unavailable));
        Assert.False(QueueReplanFilter.ShouldReleaseUnavailableContinuation(continuation, available));
        Assert.False(QueueReplanFilter.ShouldReleaseUnavailableContinuation(null, unavailable));
        Assert.False(QueueReplanFilter.ShouldReleaseUnavailableContinuation(continuation, new JsonObject()));
    }

    [Theory]
    [InlineData("executor.move_to_tile", "verified", "applied", true)]
    [InlineData("executor.play_prairie_king", "simulated_equivalent", "applied", true)]
    [InlineData("executor.play_junimo_kart", "simulated_equivalent", "applied", true)]
    [InlineData("executor.move_to_tile", "simulated_equivalent", "applied", false)]
    [InlineData("executor.play_prairie_king", "simulated_equivalent", "blocked", false)]
    [InlineData("executor.play_prairie_king", "unverified", "applied", false)]
    public void TrainingVerificationAllowsOnlyExplicitTimedEquivalentExecutors(
        string optionId,
        string verificationStatus,
        string executionStatus,
        bool expected)
    {
        var execution = new JsonObject
        {
            ["option_id"] = optionId,
            ["primitive_verification_status"] = verificationStatus,
            ["status"] = executionStatus
        };

        Assert.Equal(
            expected,
            QueueReplanFilter.IsTrainingVerifiedExecution(execution));
    }

    [Theory]
    [InlineData("blocked", true, true, false, true, true, false, true, false, "blocked_continue_after_fresh_after_snapshot")]
    [InlineData("blocked", true, true, false, false, true, false, false, true, "stale_after_snapshot")]
    [InlineData("applied", true, true, false, true, true, false, false, false, "continuable_execution")]
    [InlineData("applied", true, true, false, true, true, true, true, false, "continuable_execution_requires_fresh_snapshot_replan")]
    [InlineData("no_op", true, true, false, true, true, true, true, false, "continuable_execution_requires_fresh_snapshot_replan")]
    [InlineData("applied", true, true, false, false, true, true, false, true, "stale_after_snapshot")]
    [InlineData("applied", true, true, false, true, false, true, false, true, "max_queue_item_attempts_reached")]
    [InlineData("applied", true, false, false, true, true, true, false, false, "non_daily_plan_fresh_snapshot_replan_not_applicable")]
    [InlineData("blocked", false, true, false, true, true, false, false, true, "continue_after_blocked_disabled")]
    [InlineData("blocked", true, false, false, true, true, false, false, false, "non_daily_plan_continue_after_blocked")]
    [InlineData("blocked", true, true, true, true, true, false, false, false, "non_daily_plan_continue_after_blocked")]
    public void DecideAfterExecutionCoversBlockedFreshStaleAndSkipCases(
        string executionStatus,
        bool continueAfterBlocked,
        bool useDailyPlan,
        bool hasExecutorOverride,
        bool afterSnapshotFresh,
        bool canAttemptMoreItems,
        bool requiresFreshSnapshotReplan,
        bool shouldReplan,
        bool shouldStop,
        string reason)
    {
        var decision = QueueReplanFilter.DecideAfterExecution(
            executionStatus,
            continueAfterBlocked,
            useDailyPlan,
            hasExecutorOverride,
            afterSnapshotFresh,
            canAttemptMoreItems,
            requiresFreshSnapshotReplan);

        Assert.Equal(shouldReplan, decision.ShouldReplan);
        Assert.Equal(shouldStop, decision.ShouldStop);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void CompletedObjectiveStopsAtTheTrainingIterationBoundary()
    {
        var decision = QueueReplanFilter.DecideAfterExecution(
            "applied",
            continueAfterBlocked: true,
            useDailyPlan: true,
            hasExecutorOverride: false,
            afterSnapshotFresh: true,
            canAttemptMoreItems: true,
            requiresFreshSnapshotReplan: true,
            objectiveContinuationCompleted: true);

        Assert.False(decision.ShouldReplan);
        Assert.True(decision.ShouldStop);
        Assert.False(decision.ShouldFilterRegeneratedQueue);
        Assert.Equal(
            "objective_continuation_completed_iteration_boundary",
            decision.Reason);

        var runtimeSource = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.cs"));
        Assert.Contains(
            "objectiveContinuationCompleted);",
            runtimeSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSnapshotReplanSignalAcceptsCompilerAndExpectedEffectForms()
    {
        var direct = QueueItem("queue.replan.direct", "executor.traverse_connector", "1", "2", string.Empty);
        direct["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("fresh_snapshot_replan_required", "true"));
        var compiler = QueueItem("queue.replan.compiler", "executor.traverse_connector", "1", "2", string.Empty);
        compiler["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("compiler_context.fresh_snapshot_replan_required", "true"));
        var expectedEffect = QueueItem("queue.replan.effect", "executor.interact", "1", "2", string.Empty);
        expectedEffect["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("expected_effect", "menu_open=true;fresh_snapshot_replan_required=true"));
        var ordinary = QueueItem("queue.replan.ordinary", "executor.move_to_tile", "1", "2", string.Empty);

        Assert.True(QueueReplanFilter.RequiresFreshSnapshotReplan(direct));
        Assert.True(QueueReplanFilter.RequiresFreshSnapshotReplan(compiler));
        Assert.True(QueueReplanFilter.RequiresFreshSnapshotReplan(expectedEffect));
        Assert.False(QueueReplanFilter.RequiresFreshSnapshotReplan(ordinary));
    }

    [Fact]
    public void MailContinuationSurvivesRouteAndCompletesOnlyAtExactNativeLetterTerminal()
    {
        var route = QueueItem(
            "queue.mail.route",
            "executor.traverse_connector",
            "3",
            "4",
            string.Empty);
        var routeParameters = route["normalized_command"]!["parameters"]!.AsArray();
        routeParameters.Add(Parameter("continuation.option_id", "mail.process_letter"));
        routeParameters.Add(Parameter("continuation.mail_id", "spring_18"));
        routeParameters.Add(Parameter("continuation.mail_data_sha256", "mail-hash"));
        routeParameters.Add(Parameter("continuation.target_location", "Farm"));
        routeParameters.Add(Parameter("fresh_snapshot_replan_required", "true"));

        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("mail", continuation!["kind"]!.GetValue<string>());
        Assert.Equal("spring_18", continuation["mail_id"]!.GetValue<string>());
        Assert.True(QueueReplanFilter.RequiresFreshSnapshotReplan(route));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            route,
            continuation,
            "applied"));

        var exact = MailCandidate("mailbox_approach", "spring_18");
        var other = MailCandidate("mailbox_approach", "spring_19");
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(
            new JsonArray { exact, other },
            continuation));
        Assert.Equal("spring_18", selected!["parameters"]![0]!["value"]!.GetValue<string>());

        var unsuppressed = QueueReplanFilter.FilterSuppressedContinuations(
            new JsonArray
            {
                MailCandidate("mailbox_approach", "spring_18"),
                MailCandidate("mailbox_approach", "spring_19")
            },
            new[] { continuation });
        var remaining = Assert.Single(unsuppressed);
        Assert.Equal("spring_19", remaining!["parameters"]![0]!["value"]!.GetValue<string>());

        var interact = QueueItem("queue.mail.open", "executor.interact", "68", "15", string.Empty);
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            interact,
            continuation,
            "applied"));

        var terminal = QueueItem("queue.mail.close", "executor.close_menu", "0", "0", string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("target_runtime_type", "LetterViewerMenu"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("mail_menu_identity_sha256", "menu-hash"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal,
            continuation,
            "applied"));
        terminal["normalized_command"]!["parameters"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == "target_runtime_type")!["value"] = "ShopMenu";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal,
            continuation,
            "applied"));
    }

    [Fact]
    public void FilterUnattemptedUsesStableSemanticIdentityInsteadOfQueueItemId()
    {
        var blockedOriginal = QueueItem("queue_item.original.blocked", "executor.collect_machine_output", "64", "15", "(O)388");
        var completedOriginal = QueueItem("queue_item.original.completed", "executor.load_machine_input", "65", "15", "(O)262");
        var regeneratedBlocked = QueueItem("queue_item.regenerated.blocked", "executor.collect_machine_output", "64", "15", "(O)388");
        var regeneratedCompleted = QueueItem("queue_item.regenerated.completed", "executor.load_machine_input", "65", "15", "(O)262");
        var differentValidRemaining = QueueItem("queue_item.regenerated.remaining", "executor.load_machine_input", "66", "15", "(O)262");
        var attempted = new HashSet<string>(StringComparer.Ordinal)
        {
            QueueReplanFilter.SemanticQueueItemKey(blockedOriginal),
            QueueReplanFilter.SemanticQueueItemKey(completedOriginal)
        };

        var filtered = QueueReplanFilter.FilterUnattempted(
            new[] { regeneratedBlocked, regeneratedCompleted, differentValidRemaining },
            attempted);

        var remaining = Assert.Single(filtered);
        Assert.Equal("queue_item.regenerated.remaining", remaining["queue_item_id"]!.GetValue<string>());
        Assert.DoesNotContain(filtered, item => item["queue_item_id"]!.GetValue<string>() == "queue_item.regenerated.blocked");
        Assert.DoesNotContain(filtered, item => item["queue_item_id"]!.GetValue<string>() == "queue_item.regenerated.completed");
    }

    [Fact]
    public void FilterUnattemptedIgnoresRecomputedBudgetMetadata()
    {
        var original = QueueItem("queue.original", "executor.clear_obstacle", "62", "21", string.Empty);
        original["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("budget.remaining_minutes_before", "859"));
        var regenerated = QueueItem("queue.regenerated", "executor.clear_obstacle", "62", "21", string.Empty);
        regenerated["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("budget.remaining_minutes_before", "857"));
        var attempted = new HashSet<string>(StringComparer.Ordinal)
        {
            QueueReplanFilter.SemanticQueueItemKey(original)
        };

        var filtered = QueueReplanFilter.FilterUnattempted(new[] { regenerated }, attempted);

        Assert.Empty(filtered);
    }

    [Fact]
    public void ContinuationFilterKeepsSameNpcAndRejectsObjectiveSwitch()
    {
        var continuation = new JsonObject
        {
            ["option_id"] = "social.talk_npc",
            ["npc_name"] = "Abigail",
            ["target_location"] = "SeedShop",
            ["slot_index"] = string.Empty,
            ["qualified_item_id"] = string.Empty
        };
        var candidates = new JsonArray
        {
            SocialCandidate("social.talk_npc", "Leah", false),
            SocialCandidate("social.talk_npc", "Abigail", true),
            SocialCandidate("social.gift_npc", "Abigail", false)
        };

        var filtered = QueueReplanFilter.FilterRankedCandidates(candidates, continuation);

        var selectedNode = Assert.Single(filtered);
        Assert.NotNull(selectedNode);
        var selected = selectedNode!.AsObject();
        var parameters = Assert.IsType<JsonArray>(selected["parameters"]);
        var parameter = Assert.IsType<JsonObject>(Assert.Single(parameters));
        Assert.Equal("Abigail", parameter["value"]!.GetValue<string>());
    }

    [Fact]
    public void ExplicitCalibrationKindFilterKeepsOnlyRequestedSlice()
    {
        var candidates = new JsonArray
        {
            MachineCandidate(
                "collect_machine_output_tile",
                "Farm",
                5,
                5),
            MachineCandidate(
                "relocate_machine_item",
                "Farm",
                15,
                5)
        };

        var filtered = QueueReplanFilter.FilterCandidateKind(
            candidates,
            "relocate_machine_item");

        var selected = Assert.Single(filtered);
        Assert.Equal(
            "relocate_machine_item",
            selected!["kind"]!.GetValue<string>());
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void AppliedWaitCarriesContinuationAndAppliedMatchingInteractionCompletesIt()
    {
        var wait = QueueItem("queue.wait", "executor.wait_ticks", "0", "0", string.Empty);
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "social.talk_npc"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.npc_name", "Abigail"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.target_location", "SeedShop"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.retry_count", "1"));
        wait["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.retry_game_time", "900"));
        var continuation = QueueReplanFilter.ReadSocialContinuation(wait);

        Assert.NotNull(continuation);
        Assert.Equal("Abigail", continuation!["npc_name"]!.GetValue<string>());
        Assert.Equal("1", continuation["retry_count"]!.GetValue<string>());
        Assert.Equal("900", continuation["retry_game_time"]!.GetValue<string>());

        var prior = new JsonObject
        {
            ["kind"] = "social",
            ["option_id"] = "social.talk_npc",
            ["npc_name"] = "Abigail",
            ["target_location"] = "SeedShop"
        };
        var refreshed = QueueReplanFilter.RefreshAppliedObjectiveContinuation(
            wait,
            prior);
        Assert.Equal("1", refreshed["retry_count"]!.GetValue<string>());
        Assert.Equal("900", refreshed["retry_game_time"]!.GetValue<string>());

        var interact = QueueItem("queue.social", "executor.social_interact", "10", "10", string.Empty);
        interact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("npc_name", "Abigail"));
        interact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("social_action_kind", "talk"));
        Assert.True(QueueReplanFilter.CompletesSocialContinuation(interact, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesSocialContinuation(interact, continuation, "blocked"));
    }

    [Fact]
    public void PartnershipContinuationKeepsIrreversibleActionKindBound()
    {
        var route = QueueItem("queue.partnership.route", "executor.traverse_connector", "2", "3", string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "social.advance_partnership"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.npc_name", "Abigail"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.target_location", "SeedShop"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.slot_index", "0"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.qualified_item_id", "(O)460"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.partnership_action_kind", "propose_marriage"));
        var continuation = QueueReplanFilter.ReadSocialContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("propose_marriage", continuation!["partnership_action_kind"]!.GetValue<string>());

        var wrong = QueueItem("queue.partnership.wrong", "executor.social_interact", "10", "10", "(O)460");
        wrong["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("npc_name", "Abigail"));
        wrong["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("social_action_kind", "bouquet"));
        Assert.False(QueueReplanFilter.CompletesSocialContinuation(wrong, continuation, "applied"));

        var exact = QueueItem("queue.partnership.exact", "executor.social_interact", "10", "10", "(O)460");
        exact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("npc_name", "Abigail"));
        exact["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("social_action_kind", "propose_marriage"));
        Assert.True(QueueReplanFilter.CompletesSocialContinuation(exact, continuation, "applied"));
    }

    [Fact]
    public void PurchaseContinuationLocksShopAndItemUntilExactNativeBuyApplies()
    {
        var route = QueueItem(
            "queue.purchase.route",
            "executor.traverse_connector",
            "2",
            "3",
            string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.option_id", "economy.buy_supplies"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.shop_id", "Blacksmith"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.target_location", "Blacksmith"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.qualified_item_id", "(O)378"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.max_unit_price", "150"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.quantity", "1"));

        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal(
            "economy_purchase",
            continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            PurchaseCandidate("Blacksmith", "(O)378"),
            PurchaseCandidate("Blacksmith", "(O)380"),
            PurchaseCandidate("SeedShop", "(O)378")
        };
        var selected = Assert.Single(
            QueueReplanFilter.FilterRankedCandidates(
                candidates,
                continuation));
        Assert.Equal(
            "(O)378",
            selected!["qualified_item_id"]!.GetValue<string>());

        var interact = QueueItem(
            "queue.purchase.interact",
            "executor.interact",
            "3",
            "5",
            string.Empty);
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            interact,
            continuation,
            "applied"));

        var buy = QueueItem(
            "queue.purchase.buy",
            "executor.buy_shop_item",
            "0",
            "0",
            "(O)378");
        buy["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("expected_shop_id", "Blacksmith"));
        buy["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("quantity", "1"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            buy,
            continuation,
            "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            buy,
            continuation,
            "blocked"));
    }

    [Fact]
    public void ReleasedPurchaseSuppressionRemovesOnlyExactShopAndItem()
    {
        var suppressed = new JsonObject
        {
            ["kind"] = "economy_purchase",
            ["option_id"] = "economy.buy_supplies",
            ["shop_id"] = "Sandy",
            ["qualified_item_id"] = "(O)494"
        };
        var candidates = new JsonArray
        {
            PurchaseCandidate("Sandy", "(O)494"),
            PurchaseCandidate("Sandy", "(O)495"),
            PurchaseCandidate("AnimalShop", "(O)494")
        };

        var selected = QueueReplanFilter.FilterSuppressedContinuations(
            candidates,
            new[] { suppressed });

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, node =>
            node!["shop_id"]!.GetValue<string>() == "Sandy" &&
            node["qualified_item_id"]!.GetValue<string>() == "(O)495");
        Assert.Contains(selected, node =>
            node!["shop_id"]!.GetValue<string>() == "AnimalShop" &&
            node["qualified_item_id"]!.GetValue<string>() == "(O)494");
    }

    [Fact]
    public void SnapshotDayKeyRequiresCompleteGameDayIdentity()
    {
        var snapshot = SnapshotWithDay(1, "spring", 4);

        Assert.Equal("1:spring:4", QueueReplanFilter.SnapshotDayKey(snapshot));

        snapshot["state"]!["time"]!["day"]!["value"] = 5;
        Assert.Equal("1:spring:5", QueueReplanFilter.SnapshotDayKey(snapshot));

        snapshot["state"]!["time"]!.AsObject().Remove("season");
        Assert.Equal(string.Empty, QueueReplanFilter.SnapshotDayKey(snapshot));
    }

    [Fact]
    public void CompletedObjectiveSuppressionIsExactAndIdempotent()
    {
        var completed = new JsonObject
        {
            ["kind"] = "economy_purchase",
            ["option_id"] = "economy.buy_supplies",
            ["shop_id"] = "AnimalShop",
            ["qualified_item_id"] = "(BC)45",
            ["quantity"] = "1"
        };
        var suppressed = new List<JsonObject>();

        Assert.True(QueueReplanFilter.AddSuppressedContinuation(suppressed, completed));
        Assert.False(QueueReplanFilter.AddSuppressedContinuation(suppressed, completed));
        Assert.Single(suppressed);

        var selected = QueueReplanFilter.FilterSuppressedContinuations(
            new JsonArray(
                PurchaseCandidate("AnimalShop", "(BC)45"),
                PurchaseCandidate("Sandy", "(O)494")),
            suppressed);

        var remaining = Assert.IsType<JsonObject>(Assert.Single(selected));
        Assert.Equal("purchase:Sandy:(O)494", remaining["candidate_id"]?.GetValue<string>());
    }

    [Fact]
    public void PrairieKingRouteKeepsObjectiveLeaseUntilExactTerminalActionApplies()
    {
        var route = QueueItem(
            "queue.prairie-king.route",
            "executor.traverse_connector",
            "3",
            "12",
            string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.option_id", "minigame.play_prairie_king"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("continuation.prairie_king_completion_goal", "complete_without_dying"));

        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("prairie_king", continuation!["kind"]!.GetValue<string>());
        var selected = QueueReplanFilter.FilterRankedCandidates(
            new JsonArray
            {
                PrairieKingCandidate("route_connector_tile", "complete_without_dying", continuationParameter: true),
                PrairieKingCandidate("play_prairie_king", "complete_without_dying", continuationParameter: false),
                PurchaseCandidate("AnimalShop", "(BC)45")
            },
            continuation);
        Assert.Equal(2, selected.Count);

        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            route,
            continuation,
            "applied"));
        var terminal = QueueItem(
            "queue.prairie-king.terminal",
            "executor.play_prairie_king",
            "30",
            "28",
            string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("prairie_king_completion_goal", "complete_without_dying"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal,
            continuation,
            "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            terminal,
            continuation,
            "blocked"));
    }

    [Fact]
    public void SaleContinuationLocksExactStackUntilVerifiedNativeSaleApplies()
    {
        var route = QueueItem(
            "queue.sale.route",
            "executor.traverse_connector",
            "2",
            "3",
            string.Empty);
        var parameters = route["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "economy.sell_items"));
        parameters.Add(Parameter("continuation.shop_id", "SeedShop"));
        parameters.Add(Parameter("continuation.target_location", "SeedShop"));
        parameters.Add(Parameter("continuation.qualified_item_id", "(O)24"));
        parameters.Add(Parameter("continuation.slot_index", "0"));
        parameters.Add(Parameter("continuation.quantity", "3"));
        parameters.Add(Parameter("continuation.expected_unit_price", "35"));

        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("economy_sale", continuation!["kind"]!.GetValue<string>());
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(
            new JsonArray
            {
                SaleCandidate("SeedShop", "(O)24", 0),
                SaleCandidate("SeedShop", "(O)24", 1),
                SaleCandidate("FishShop", "(O)24", 0)
            },
            continuation));
        Assert.Equal(0, selected!["slot_index"]!.GetValue<int>());

        var sell = QueueItem(
            "queue.sale.sell",
            "executor.sell_shop_item",
            "0",
            "0",
            "(O)24");
        var sellParameters = sell["normalized_command"]!["parameters"]!.AsArray();
        sellParameters.Add(Parameter("expected_shop_id", "SeedShop"));
        sellParameters.Add(Parameter("slot_index", "0"));
        sellParameters.Add(Parameter("quantity", "3"));
        sellParameters.Add(Parameter("expected_unit_price", "35"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            sell,
            continuation,
            "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            sell,
            continuation,
            "blocked"));

        sellParameters
            .Select(node => node!.AsObject())
            .Single(parameter =>
                parameter["name"]!.GetValue<string>() ==
                "expected_unit_price")["value"] = "34";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            sell,
            continuation,
            "applied"));
    }

    [Fact]
    public void ShippingContinuationLocksItemBinAndPriceUntilVerifiedDepositApplies()
    {
        var approach = QueueItem(
            "queue.shipping.approach",
            "executor.move_to_tile",
            "67",
            "15",
            "(O)24");
        var parameters = approach["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "economy.ship_items"));
        parameters.Add(Parameter("continuation.target_location", "Farm"));
        parameters.Add(Parameter("continuation.qualified_item_id", "(O)24"));
        parameters.Add(Parameter("continuation.slot_index", "0"));
        parameters.Add(Parameter("continuation.quantity", "1"));
        parameters.Add(Parameter("continuation.expected_unit_price", "35"));
        parameters.Add(Parameter("continuation.bin_location", "Farm"));
        parameters.Add(Parameter("continuation.bin_tile_x", "68"));
        parameters.Add(Parameter("continuation.bin_tile_y", "15"));
        parameters.Add(Parameter("continuation.stand_tile_x", "67"));
        parameters.Add(Parameter("continuation.stand_tile_y", "15"));

        var continuation = QueueReplanFilter.ReadObjectiveContinuation(approach);

        Assert.NotNull(continuation);
        Assert.Equal("economy_shipping", continuation!["kind"]!.GetValue<string>());
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(
            new JsonArray
            {
                ShippingCandidate("(O)24", 0, 35, 68, 15),
                ShippingCandidate("(O)24", 1, 35, 68, 15),
                ShippingCandidate("(O)24", 0, 34, 68, 15)
            },
            continuation));
        Assert.Equal(0, selected!["slot_index"]!.GetValue<int>());

        var deposit = QueueItem(
            "queue.shipping.deposit",
            "executor.ship_inventory_item_to_bin",
            "68",
            "15",
            "(O)24");
        var depositParameters = deposit["normalized_command"]!["parameters"]!.AsArray();
        depositParameters.Add(Parameter("slot_index", "0"));
        depositParameters.Add(Parameter("quantity", "1"));
        depositParameters.Add(Parameter("expected_unit_price", "35"));
        depositParameters.Add(Parameter("stand_tile_x", "67"));
        depositParameters.Add(Parameter("stand_tile_y", "15"));
        foreach (var continuationParameter in parameters
                     .Select(node => node!.AsObject())
                     .Where(parameter => parameter["name"]!.GetValue<string>()
                         .StartsWith("continuation.", StringComparison.Ordinal))
                     .Select(parameter => JsonNode.Parse(parameter.ToJsonString())))
        {
            depositParameters.Add(continuationParameter);
        }
        var terminalDiscoveredContinuation =
            QueueReplanFilter.ReadObjectiveContinuation(deposit);
        Assert.NotNull(terminalDiscoveredContinuation);
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            deposit,
            terminalDiscoveredContinuation,
            "applied"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            deposit,
            continuation,
            "applied"));

        depositParameters
            .Select(node => node!.AsObject())
            .Single(parameter => parameter["name"]!.GetValue<string>() == "expected_unit_price")["value"] = "34";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
            deposit,
            continuation,
            "applied"));
    }

    [Fact]
    public void MachineContinuationKeepsSameExecutorLocationAndTileUntilApplied()
    {
        var route = QueueItem("queue.machine.route", "executor.traverse_connector", "4", "8", string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "executor.collect_machine_output"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_location_id", "Cellar"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_tile_x", "5"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.machine_tile_y", "6"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("machine", continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            MachineCandidate("collect_machine_output_tile", "Cellar", 5, 7),
            MachineCandidate("collect_machine_output_tile", "Cellar", 5, 6),
            MachineCandidate("load_machine_input_tile", "Cellar", 5, 6)
        };
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(candidates, continuation));
        Assert.Equal(6, selected!["tile_y"]!.GetValue<int>());

        var collect = QueueItem("queue.machine.collect", "executor.collect_machine_output", "5", "6", string.Empty);
        collect["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("machine_location_id", "Cellar"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(collect, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(collect, continuation, "blocked"));
    }

    [Fact]
    public void MachinePlacementContinuationSelectsTargetMapThenCompletesNativePlacement()
    {
        var route = QueueItem(
            "queue.machine.place.route",
            "executor.traverse_connector",
            "2",
            "3",
            string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter(
                "continuation.option_id",
                "executor.place_machine"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter(
                "continuation.machine_location_id",
                "Cellar"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter(
                "continuation.machine_inventory_slot_index",
                "4"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter(
                "continuation.machine_qualified_item_id",
                "(BC)12"));
        var continuation =
            QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal(
            "machine_placement",
            continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            MachinePlacementCandidate("FarmHouse", 4, "(BC)12"),
            MachinePlacementCandidate("Cellar", 5, "(BC)12"),
            MachinePlacementCandidate("Cellar", 4, "(BC)12")
        };
        var selected = Assert.Single(
            QueueReplanFilter.FilterRankedCandidates(
                candidates,
                continuation));
        Assert.Equal(
            4,
            selected!["slot_index"]!.GetValue<int>());

        var place = QueueItem(
            "queue.machine.place",
            "executor.place_machine",
            "5",
            "6",
            "(BC)12");
        place["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("location_id", "Cellar"));
        place["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("inventory_slot_index", "4"));
        Assert.True(
            QueueReplanFilter.CompletesObjectiveContinuation(
                place,
                continuation,
                "applied"));
        Assert.Equal(
            string.Empty,
            QueueReplanFilter.EffectiveCandidateKindFilter(
                "route_connector_tile",
                continuation));
        Assert.Equal(
            "route_connector_tile",
            QueueReplanFilter.EffectiveCandidateKindFilter(
                "route_connector_tile",
                null));
        var exactCandidates = QueueReplanFilter.FilterCandidateId(
            new JsonArray
            {
                MachinePlacementCandidate(
                    "FarmHouse",
                    4,
                    "(BC)12"),
                MachinePlacementCandidate(
                    "Cellar",
                    4,
                    "(BC)12")
            },
            "machine-place-FarmHouse");
        Assert.Equal(
            "machine-place-FarmHouse",
            Assert.Single(exactCandidates)!
                ["candidate_id"]!
                .GetValue<string>());
        Assert.Equal(
            string.Empty,
            QueueReplanFilter.EffectiveCandidateIdFilter(
                "machine-place-FarmHouse",
                continuation));
        Assert.Equal(
            "machine-place-FarmHouse",
            QueueReplanFilter.EffectiveCandidateIdFilter(
                "machine-place-FarmHouse",
                null));
    }

    [Fact]
    public void QuestContinuationKeepsExactQuestAcrossNpcRouteUntilNativeTerminal()
    {
        var route = QueueItem("queue.quest.route", "executor.traverse_connector", "4", "8", string.Empty);
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.option_id", "quest.advance"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.quest_candidate_id", "quest:3:ItemDeliveryQuest"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.npc_name", "Robin"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("continuation.target_location", "ScienceHouse"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("quest", continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            QuestCandidate("quest:7:FishingQuest"),
            QuestCandidate("quest:3:ItemDeliveryQuest")
        };
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(candidates, continuation));
        Assert.Equal(
            "quest:3:ItemDeliveryQuest",
            selected!["parameters"]![0]!["value"]!.GetValue<string>());

        var terminal = QueueItem("queue.quest.terminal", "executor.quest_npc_interact", "10", "10", "(O)388");
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("quest_candidate_id", "quest:3:ItemDeliveryQuest"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "blocked"));

        var dropBoxTerminal = QueueItem(
            "queue.quest.dropbox",
            "executor.quest_drop_box_donate",
            "10",
            "10",
            "(O)24");
        dropBoxTerminal["normalized_command"]!["parameters"]!.AsArray().Add(
            Parameter("quest_candidate_id", "quest:3:ItemDeliveryQuest"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
            dropBoxTerminal,
            continuation,
            "applied"));
    }

    [Fact]
    public void HomeRenovationContinuationKeepsExactRenovationUntilNativeTerminal()
    {
        var route = QueueItem("queue.renovation.route", "executor.traverse_connector", "2", "3", string.Empty);
        var parameters = route["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "housing.renovate"));
        parameters.Add(Parameter("continuation.renovation_id", "remove_crib"));
        parameters.Add(Parameter("continuation.selected_index", "0"));
        parameters.Add(Parameter("continuation.renovation_reason", "explicit player request"));
        parameters.Add(Parameter("continuation.confirm_renovation", "true"));
        parameters.Add(Parameter("continuation.confirm_destructive", "true"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("home_renovation", continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            HomeRenovationCandidate("build_crib", "0", "false"),
            HomeRenovationCandidate("remove_crib", "1", "true"),
            HomeRenovationCandidate("remove_crib", "0", "true")
        };
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(candidates, continuation));
        Assert.Equal(
            "remove_crib",
            selected!["parameters"]![0]!["value"]!.GetValue<string>());

        var terminal = QueueItem("queue.renovation.terminal", "executor.renovate_home", "10", "10", string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("renovation_id", "remove_crib"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("selected_index", "0"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "blocked"));

        terminal["normalized_command"]!["parameters"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == "selected_index")!["value"] = "1";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void MultiplayerWalletContinuationKeepsExactTransferUntilNativeTerminal()
    {
        var route = QueueItem("queue.wallet.route", "executor.traverse_connector", "2", "5", string.Empty);
        var parameters = route["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "multiplayer.manage_wallet"));
        parameters.Add(Parameter("continuation.wallet_operation", "transfer"));
        parameters.Add(Parameter("continuation.wallet_reason", "explicit player request"));
        parameters.Add(Parameter("continuation.confirm_wallet_operation", "true"));
        parameters.Add(Parameter("continuation.confirm_wallet_transfer", "true"));
        parameters.Add(Parameter("continuation.wallet_recipient_player_id", "2002"));
        parameters.Add(Parameter("continuation.wallet_transfer_amount", "175"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("multiplayer_wallet", continuation!["kind"]!.GetValue<string>());
        var candidates = new JsonArray
        {
            MultiplayerWalletCandidate("transfer", "2001", "175"),
            MultiplayerWalletCandidate("transfer", "2002", "100"),
            MultiplayerWalletCandidate("transfer", "2002", "175")
        };
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(candidates, continuation));
        Assert.Equal(
            "2002",
            selected!["parameters"]![4]!["value"]!.GetValue<string>());

        var terminal = QueueItem("queue.wallet.terminal", "executor.manage_multiplayer_wallet", "2", "5", string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("wallet_operation", "transfer"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("wallet_reason", "explicit player request"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("wallet_recipient_player_id", "2002"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("wallet_transfer_amount", "175"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "blocked"));

        terminal["normalized_command"]!["parameters"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == "wallet_transfer_amount")!["value"] = "100";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void BobberContinuationKeepsExactExplicitStyleUntilNativeTerminal()
    {
        var route = QueueItem("queue.bobber.route", "executor.traverse_connector", "4", "4", string.Empty);
        var parameters = route["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "player.choose_bobber"));
        parameters.Add(Parameter("continuation.bobber_style_id", "7"));
        parameters.Add(Parameter("continuation.bobber_reason", "explicit player request"));
        parameters.Add(Parameter("continuation.confirm_bobber_style", "true"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("bobber_selection", continuation!["kind"]!.GetValue<string>());
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(new JsonArray
        {
            BobberCandidate("-2"), BobberCandidate("7")
        }, continuation));
        Assert.Equal("7", selected!["parameters"]![0]!["value"]!.GetValue<string>());

        var terminal = QueueItem("queue.bobber.terminal", "executor.choose_bobber_style", "10", "4", string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("bobber_style_id", "7"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("bobber_reason", "explicit player request"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        terminal["normalized_command"]!["parameters"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == "bobber_style_id")!["value"] = "-2";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void JukeboxContinuationKeepsExactExplicitTrackUntilNativeTerminal()
    {
        var route = QueueItem("queue.jukebox.route", "executor.traverse_connector", "4", "4", string.Empty);
        var parameters = route["normalized_command"]!["parameters"]!.AsArray();
        parameters.Add(Parameter("continuation.option_id", "player.choose_jukebox_track"));
        parameters.Add(Parameter("continuation.jukebox_track_id", "spring1"));
        parameters.Add(Parameter("continuation.jukebox_reason", "explicit player request"));
        parameters.Add(Parameter("continuation.confirm_jukebox_track", "true"));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);

        Assert.NotNull(continuation);
        Assert.Equal("jukebox_selection", continuation!["kind"]!.GetValue<string>());
        var selected = Assert.Single(QueueReplanFilter.FilterRankedCandidates(new JsonArray
        {
            JukeboxCandidate("summer1"), JukeboxCandidate("spring1")
        }, continuation));
        Assert.Equal("spring1", selected!["parameters"]![0]!["value"]!.GetValue<string>());

        var terminal = QueueItem("queue.jukebox.terminal", "executor.choose_jukebox_track", "1", "17", string.Empty);
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("jukebox_track_id", "spring1"));
        terminal["normalized_command"]!["parameters"]!.AsArray().Add(Parameter("jukebox_reason", "explicit player request"));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
        terminal["normalized_command"]!["parameters"]!.AsArray()
            .Single(node => node!["name"]!.GetValue<string>() == "jukebox_track_id")!["value"] = "summer1";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    private static JsonObject QueueItem(string queueItemId, string optionId, string targetX, string targetY, string qualifiedItemId)
    {
        return new JsonObject
        {
            ["queue_item_id"] = queueItemId,
            ["option_id"] = optionId,
            ["status"] = "pending",
            ["normalized_command"] = new JsonObject
            {
                ["command_type"] = "compiled_action_steps",
                ["parameters"] = new JsonArray
                {
                    Parameter("target_tile_x", targetX),
                    Parameter("target_tile_y", targetY),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("compiler_context.season", "spring"),
                    Parameter("estimated_minutes", "1")
                },
                ["steps"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["step_type"] = "move_to_tile",
                        ["target"] = "Farm(" + targetX + "," + targetY + ")"
                    }
                }
            }
        };
    }

    private static JsonObject MailCandidate(string kind, string mailId)
    {
        return new JsonObject
        {
            ["candidate_id"] = "mail:" + kind + ":" + mailId,
            ["option_id"] = "mail.process_letter",
            ["kind"] = kind,
            ["parameters"] = new JsonArray
            {
                Parameter("continuation.mail_id", mailId)
            }
        };
    }

    private static JsonObject PurchaseCandidate(
        string shopId,
        string qualifiedItemId)
    {
        return new JsonObject
        {
            ["candidate_id"] = "purchase:" + shopId + ":" + qualifiedItemId,
            ["option_id"] = "economy.buy_supplies",
            ["kind"] = "buy_shop_item",
            ["shop_id"] = shopId,
            ["qualified_item_id"] = qualifiedItemId,
            ["parameters"] = new JsonArray()
        };
    }

    private static JsonObject SnapshotWithDay(int year, string season, int day)
    {
        return new JsonObject
        {
            ["state"] = new JsonObject
            {
                ["time"] = new JsonObject
                {
                    ["year"] = new JsonObject { ["value"] = year },
                    ["season"] = new JsonObject { ["value"] = season },
                    ["day"] = new JsonObject { ["value"] = day }
                }
            }
        };
    }

    private static JsonObject PrairieKingCandidate(
        string kind,
        string completionGoal,
        bool continuationParameter)
    {
        return new JsonObject
        {
            ["candidate_id"] = "prairie-king:" + kind,
            ["option_id"] = "minigame.play_prairie_king",
            ["kind"] = kind,
            ["parameters"] = new JsonArray
            {
                Parameter(
                    continuationParameter
                        ? "continuation.prairie_king_completion_goal"
                        : "prairie_king_completion_goal",
                    completionGoal)
            }
        };
    }

    private static JsonObject SaleCandidate(
        string shopId,
        string qualifiedItemId,
        int slotIndex)
    {
        return new JsonObject
        {
            ["candidate_id"] = "sale:" + shopId + ":" + qualifiedItemId + ":" + slotIndex,
            ["option_id"] = "economy.sell_items",
            ["kind"] = "sell_shop_item",
            ["shop_id"] = shopId,
            ["qualified_item_id"] = qualifiedItemId,
            ["slot_index"] = slotIndex,
            ["parameters"] = new JsonArray
            {
                Parameter("continuation.slot_index", slotIndex.ToString())
            }
        };
    }

    private static JsonObject ShippingCandidate(
        string qualifiedItemId,
        int slotIndex,
        int unitPrice,
        int binTileX,
        int binTileY)
    {
        return new JsonObject
        {
            ["candidate_id"] = "shipping:" + slotIndex + ":" + unitPrice,
            ["option_id"] = "economy.ship_items",
            ["kind"] = "ship_inventory_item_to_bin",
            ["qualified_item_id"] = qualifiedItemId,
            ["slot_index"] = slotIndex,
            ["quantity"] = 1,
            ["parameters"] = new JsonArray
            {
                Parameter("continuation.qualified_item_id", qualifiedItemId),
                Parameter("continuation.slot_index", slotIndex.ToString()),
                Parameter("continuation.quantity", "1"),
                Parameter("continuation.expected_unit_price", unitPrice.ToString()),
                Parameter("continuation.bin_location", "Farm"),
                Parameter("continuation.bin_tile_x", binTileX.ToString()),
                Parameter("continuation.bin_tile_y", binTileY.ToString()),
                Parameter("continuation.stand_tile_x", "67"),
                Parameter("continuation.stand_tile_y", "15")
            }
        };
    }

    private static JsonObject MachineCandidate(string kind, string locationId, int tileX, int tileY)
    {
        return new JsonObject
        {
            ["option_id"] = "farm.process_machines",
            ["kind"] = kind,
            ["location_id"] = locationId,
            ["tile_x"] = tileX,
            ["tile_y"] = tileY,
            ["parameters"] = new JsonArray()
        };
    }

    private static JsonObject MachinePlacementCandidate(
        string locationId,
        int slotIndex,
        string qualifiedItemId)
    {
        return new JsonObject
        {
            ["candidate_id"] = "machine-place-" + locationId,
            ["option_id"] = "farm.process_machines",
            ["kind"] = "place_machine_item",
            ["location_id"] = locationId,
            ["slot_index"] = slotIndex,
            ["qualified_item_id"] = qualifiedItemId,
            ["parameters"] = new JsonArray()
        };
    }

    private static JsonObject SocialCandidate(string optionId, string npcName, bool continuationParameters)
    {
        return new JsonObject
        {
            ["candidate_id"] = "social:" + optionId + ":" + npcName,
            ["option_id"] = optionId,
            ["parameters"] = new JsonArray
            {
                Parameter(continuationParameters ? "continuation.npc_name" : "npc_name", npcName)
            }
        };
    }

    private static JsonObject QuestCandidate(string candidateId)
    {
        return new JsonObject
        {
            ["candidate_id"] = candidateId,
            ["option_id"] = "quest.advance",
            ["parameters"] = new JsonArray
            {
                Parameter("quest_candidate_id", candidateId)
            }
        };
    }

    private static JsonObject HomeRenovationCandidate(
        string renovationId,
        string selectedIndex,
        string confirmDestructive)
    {
        return new JsonObject
        {
            ["candidate_id"] = "home-renovation:" + renovationId + ":" + selectedIndex,
            ["option_id"] = "housing.renovate",
            ["kind"] = "renovate_home",
            ["parameters"] = new JsonArray
            {
                Parameter("renovation_id", renovationId),
                Parameter("selected_index", selectedIndex),
                Parameter("renovation_reason", "explicit player request"),
                Parameter("confirm_renovation", "true"),
                Parameter("confirm_destructive", confirmDestructive)
            }
        };
    }

    private static JsonObject MultiplayerWalletCandidate(
        string operation,
        string recipientPlayerId,
        string transferAmount)
    {
        return new JsonObject
        {
            ["candidate_id"] = "multiplayer-wallet:" + operation + ":" + recipientPlayerId + ":" + transferAmount,
            ["option_id"] = "multiplayer.manage_wallet",
            ["kind"] = "manage_multiplayer_wallet",
            ["parameters"] = new JsonArray
            {
                Parameter("wallet_operation", operation),
                Parameter("wallet_reason", "explicit player request"),
                Parameter("confirm_wallet_operation", "true"),
                Parameter("confirm_wallet_transfer", "true"),
                Parameter("wallet_recipient_player_id", recipientPlayerId),
                Parameter("wallet_transfer_amount", transferAmount)
            }
        };
    }

    private static JsonObject BobberCandidate(string styleId) => new()
    {
        ["candidate_id"] = "bobber-selection:" + styleId,
        ["option_id"] = "player.choose_bobber",
        ["kind"] = "choose_bobber_style",
        ["parameters"] = new JsonArray
        {
            Parameter("bobber_style_id", styleId),
            Parameter("bobber_reason", "explicit player request"),
            Parameter("confirm_bobber_style", "true")
        }
    };

    private static JsonObject JukeboxCandidate(string trackId) => new()
    {
        ["candidate_id"] = "jukebox-selection:" + trackId,
        ["option_id"] = "player.choose_jukebox_track",
        ["kind"] = "choose_jukebox_track",
        ["parameters"] = new JsonArray
        {
            Parameter("jukebox_track_id", trackId),
            Parameter("jukebox_reason", "explicit player request"),
            Parameter("confirm_jukebox_track", "true")
        }
    };

    private static JsonObject Parameter(string name, string value)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["value"] = value
        };
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Cannot find repository root.");
        }

        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
