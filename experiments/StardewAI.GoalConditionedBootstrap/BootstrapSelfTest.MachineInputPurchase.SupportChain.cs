using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputPurchaseSupportChain()
    {
        const string goalId = "grandpa.stage1.21_points";
        var route = MachineFacilityStaticRoute() with
        {
            RequiredAmount = 2
        };
        var facility = MachineResourceFacilityRoute(route);
        var resources = MachineResourceState(
            MachineResourceSlot(0, "(O)262", 1));
        var resource = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            facility,
            route,
            resources);
        var currencies = MachineInputPurchaseCurrencyState(1_000);
        var currency = AcquisitionRouteTargetDateCurrencyBuilder.Evaluate(
            resource,
            route,
            currencies);
        var before = MachineInputPurchaseReceiptSnapshot(1, 1_000);
        before.GameTick = 10;
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "machine-input-purchase-support-ledger",
            SaveId = "machine-input-purchase-save",
            PlayerId = "42",
            Revision = 0,
            SourceStateHash = before.StateHash
        };
        var reservation = AcquisitionRouteTargetDateReservationBuilder
            .Evaluate(
                currency,
                goalId,
                before.StateHash,
                new AcquisitionStrategyLedgerState(ledger, 42),
                resources,
                currencies);
        var processing = AcquisitionRouteTargetDateProcessingBuilder.Evaluate(
            reservation,
            route,
            MachineProcessingState(900,
                Array.Empty<Dictionary<string, object?>>()),
            targetTotalDay: 1);
        var requirement = facility.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var lowering = BushLowering() with
        {
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            SupervisionMode = "policy_option",
            UncertaintyMode = requirement.UncertaintyMode,
            EndpointOptionIds = new[] { "farm.collect_machine_outputs" },
            SupportingOptionIds = new[] { "economy.buy_supplies" }
        };
        var binding = currency.CurrencyEvaluation!.PurchasePrerequisite!;
        var candidate = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    before,
                    new[] { "economy.buy_supplies" },
                    includeExecutorCalibrationOptions: true,
                    commitmentLedger: ledger),
                goalId)
            .Single(value =>
                value.OptionId == "economy.buy_supplies" &&
                value.Kind == "buy_shop_item" &&
                value.ShopId == binding.ShopId &&
                value.QualifiedItemId == binding.QualifiedItemId &&
                value.UnitPrice == binding.UnitPrice);
        Require(candidate.Parameters.Any(parameter =>
                    parameter.Name == "continuation.stock_id" &&
                    parameter.Value == binding.StockId) &&
                candidate.Parameters.Any(parameter =>
                    parameter.Name ==
                        "continuation.output_stack_per_purchase" &&
                    parameter.Value ==
                        binding.OutputStackPerPurchase.ToString()) &&
                candidate.Parameters.Any(parameter =>
                    parameter.Name == "continuation.output_quality" &&
                    parameter.Value == binding.OutputQuality.ToString()),
            "Purchase candidate lost exact stock-row continuation identity.");
        var mismatchedStockCandidate = JsonSerializer.Deserialize<
            PolicyEventCandidatePrediction>(
                JsonSerializer.Serialize(candidate, JsonDefaults.Options),
                JsonDefaults.Options) ?? throw new InvalidDataException(
                    "Machine-input purchase mismatch candidate clone failed.");
        mismatchedStockCandidate.Parameters = mismatchedStockCandidate
            .Parameters.Select(parameter =>
                parameter.Name == "continuation.stock_id"
                    ? Parameter(
                        "continuation.stock_id",
                        "same-item-same-price-other-stock")
                    : parameter)
            .ToArray();
        Require(!AcquisitionRouteSupportingTransitionRequestBuilder
                .PurchaseCandidateMatchesBinding(
                    mismatchedStockCandidate,
                    binding),
            "A same-item/same-price candidate substituted another stock row.");
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowering,
                new[] { candidate });
        Require(matches.Length == 1,
            "Exact machine-input purchase candidate was not classified as support.");

        var request = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                goalId,
                requirement,
                lowering,
                reservation,
                processing,
                before,
                ledger,
                matches,
                supportDeadlineTotalDay: 2,
                rankingSha256: new string('a', 64));
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind == "machine_input_purchase" &&
                request.PurchaseStage == "purchase" &&
                request.PurchasePrerequisite == binding &&
                request.SupportCurrencyConsumptions is
                    [{ ConsumedAmount: 80 }],
            "Exact machine-input purchase did not produce a commit-ready support request: " +
            string.Join(",", request.BlockingReasons));

        var service = new ReservationPortfolioLedgerService();
        var commitResult = service.Commit(
            ledger,
            before,
            request.AtomicCommitRequest!,
            "2026-09-27T02:00:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "Machine-input purchase claims failed atomic commit.");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                before,
                commitResult,
                requestSha256: new string('b', 64));
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                before,
                commitResult.Ledger!,
                matches,
                new string('a', 64),
                new string('b', 64),
                new string('c', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Select(item => item.OptionId)
                    .SequenceEqual(new[]
                    {
                        "executor.buy_shop_item",
                        "executor.close_menu"
                    }, StringComparer.Ordinal) == true,
            "Machine-input purchase did not compile through the existing native executor: " +
            string.Join(",", compilation.BlockingReasons) + ";queue=" +
            compilation.ActionQueue?.Status + ";diagnostics=" +
            string.Join("|", compilation.ActionQueue?.CompilerDiagnostics ??
                Array.Empty<string>()) + ";items=" +
            string.Join("|", compilation.ActionQueue?.Items.Select(item =>
                item.OptionId + ":" + item.Status + ":missing=" +
                string.Join("+", item.MissingStateFactors) + ":blocked=" +
                string.Join("+", item.BlockingReasons) + ":command=" +
                item.NormalizedCommand?.CommandType) ??
                Array.Empty<string>()));
        var purchaseQueueItem = compilation.ActionQueue!.Items.Single(item =>
            item.OptionId == "executor.buy_shop_item");
        Require(purchaseQueueItem.NormalizedCommand?.Parameters.Any(
                    parameter =>
                        parameter.Name == "expected_stock_id" &&
                        parameter.Value == binding.StockId) == true &&
                purchaseQueueItem.NormalizedCommand.Parameters.Any(
                    parameter =>
                        parameter.Name == "expected_output_stack" &&
                        parameter.Value ==
                            binding.OutputStackPerPurchase.ToString()) &&
                purchaseQueueItem.NormalizedCommand.Parameters.Any(
                    parameter =>
                        parameter.Name == "expected_output_quality" &&
                        parameter.Value == binding.OutputQuality.ToString()),
            "Compiled native purchase lost exact stock-row identity.");

        var after = MachineInputPurchaseReceiptSnapshot(2, 920, false);
        after.GameTick = 12;
        var execution = PurchaseExecutionReceipt(
            compilation,
            before,
            after);
        var transition = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                execution.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transition.SupportingTransitionVerified &&
                transition.PurchaseTransition is
                    { Verified: true,
                      StockId: "wheat-seed",
                      ObservedCurrencyDecrease: 80,
                      ObservedItemIncrease: 1 },
            "Machine-input purchase fresh receipt was not verified: " +
            string.Join(",", transition.BlockingReasons));

        var settlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildRequestCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commitResult.Ledger!,
                    new string('d', 64));
        var settlementResult = service.SettleSupportingTransition(
            commitResult.Ledger,
            after,
            settlementRequest,
            "2026-09-27T02:01:00Z");
        Require(settlementResult.Accepted &&
                settlementResult.CurrencySettlements is
                    [{ ConsumedAmount: 80, RemainingAmount: 0 }] &&
                settlementResult.CompletedCurrencyReservationIds.Length == 1 &&
                settlementResult.ReboundActiveReservationIds.SequenceEqual(
                    request.ReservationMaterialClaims.Select(value =>
                        value.ReservationId),
                    StringComparer.Ordinal),
            "Machine-input purchase claim settlement failed: " +
            string.Join(",", settlementResult.Errors));
        var settlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder
                .BuildReceiptCore(
                    request,
                    commitReceipt,
                    compilation,
                    transition,
                    after,
                    commitResult.Ledger!,
                    settlementRequest,
                    settlementResult,
                    settlementResult.Ledger!,
                    new string('d', 64));
        Require(settlementReceipt.ReservationLifecycleVerified &&
                settlementReceipt.FreshReplanRequired &&
                !settlementReceipt.TerminalReceiptEligible,
            "Machine-input purchase settlement did not require a full fresh replan: " +
            string.Join(",", settlementReceipt.BlockingReasons));
    }

    private static QueueExecutionReceiptEnvelope PurchaseExecutionReceipt(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue!;
        var intermediateStateHash = new string('e', 64);
        var stepResults = queue.Items.Select((item, index) =>
        {
            var sourceStateHash = index == 0
                ? before.StateHash
                : intermediateStateHash;
            var afterStateHash = index == queue.Items.Length - 1
                ? after.StateHash
                : intermediateStateHash;
            var effectiveItem = JsonSerializer.Deserialize<ActionQueueItem>(
                JsonSerializer.Serialize(item, JsonDefaults.Options),
                JsonDefaults.Options) ?? throw new InvalidDataException(
                "Machine-input purchase queue item clone failed.");
            effectiveItem.NormalizedCommand.StateHash = sourceStateHash;
            return new QueueExecutionStepReceipt
            {
                QueueItemIndex = index,
                QueueItemCount = queue.Items.Length,
                OriginalPlannedItemCount = queue.Items.Length,
                QueueId = queue.QueueId,
                QueueItemId = item.QueueItemId,
                OptionId = item.OptionId,
                SourceStateHash = sourceStateHash,
                CompiledCommandStateHash = before.StateHash,
                SelectedQueueCandidateCompleted =
                    index == queue.Items.Length - 1,
                AfterStateHash = afterStateHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick + index,
                AfterGameTick = before.GameTick + index + 1,
                AfterSnapshotFresh = true,
                Status = "applied",
                PrimitiveKind = item.NormalizedCommand.Steps.Single().StepType,
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = new[]
                {
                    "native_shop_purchase_macro_step_observed"
                },
                EffectiveQueueItem = JsonSerializer.SerializeToElement(
                    effectiveItem,
                    JsonDefaults.Options),
                ChangedFacts = JsonSerializer.SerializeToElement(new[]
                {
                    index == 0
                        ? "player.inventory_and_currency_changed"
                        : "menus.active_menu_closed"
                })
            };
        }).ToArray();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.machine-input-purchase.self-test",
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
            SelectedCandidateId = compilation.SelectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = stepResults
        };
    }
}
