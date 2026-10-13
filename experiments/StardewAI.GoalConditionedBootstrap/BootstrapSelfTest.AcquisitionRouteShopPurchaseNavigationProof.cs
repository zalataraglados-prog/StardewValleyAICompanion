using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunShopPurchaseNavigationTerminalCoverage(
        string executionInputsPath)
    {
        var inputs = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBindingInputs>(
            Path.GetFullPath(executionInputsPath),
            "Shop-purchase navigation terminal coverage execution inputs");
        _ = VerifyShopPurchaseNavigationSupportingTransitionProof(inputs);
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageSource
        VerifyShopPurchaseNavigationSupportingTransitionProof(
            AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "shop-purchase-navigation-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var supportRoot = Path.Combine(root, "navigation-support");
        Directory.CreateDirectory(supportRoot);
        var beforeSnapshotPath = Path.Combine(
            supportRoot,
            "before-snapshot.json");
        var before = WriteShopPurchaseNavigationProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(supportRoot, "strategy-ledger.json");
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.shop-purchase-navigation-file-backed",
            SaveId = before.SaveId.Value!,
            PlayerId = before.PlayerId.Value!,
            Revision = 3,
            UpdatedAt = "2026-09-28T00:00:00Z",
            SourceStateHash = before.StateHash
        };
        Write(baseLedgerPath, baseLedger);
        var portfolioInputs = BuildTargetDatePortfolioInputs(
            authority.Template,
            beforeSnapshotPath,
            baseLedgerPath,
            Path.Combine(supportRoot, "unused-proposal.json"),
            supportRoot,
            targetTotalDay: 0);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            before,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "economy.buy_supplies",
                    Parameters = new[]
                    {
                        Parameter("continuation.shop_id", "FixtureShop"),
                        Parameter(
                            "continuation.qualified_item_id",
                            "(O)346"),
                        Parameter("continuation.stock_id", "fixture-beer"),
                        Parameter(
                            "continuation.output_stack_per_purchase",
                            "1"),
                        Parameter("continuation.output_quality", "0"),
                        Parameter("continuation.max_unit_price", "200"),
                        Parameter("continuation.quantity", "1")
                    }
                }
            },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: baseLedger);
        var ranking = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            authority.GoalId);
        var navigation = ranking.SingleOrDefault(candidate =>
            candidate.OptionId == "economy.buy_supplies" &&
            candidate.Kind == "route_connector_tile" &&
            candidate.Available &&
            candidate.ShopId == "FixtureShop" &&
            candidate.QualifiedItemId == "(O)346" &&
            candidate.UnitPrice == 200);
        Require(navigation is not null,
            "File-backed direct shop route emitted no exact navigation " +
            "candidate: " + string.Join("|", availability.Options.Select(
                option => option.OptionId + "[" +
                    string.Join(",", option.BlockingReasons) + "]<missing=" +
                    string.Join(",", option.MissingStateFactors) + ">{" +
                    string.Join(";", option.EventCandidates.Select(candidate =>
                        candidate.Kind + ":" + candidate.Available + ":" +
                        candidate.QualifiedItemId + ":" +
                        string.Join(",", candidate.BlockReasons))) + "}")));
        var rankingPath = Path.Combine(supportRoot, "ranking.json");
        Write(rankingPath, CollectionRanking(before.StateHash, ranking));
        var supportInputs = SupportingTransitionProofInputs(
            portfolioInputs,
            rankingPath,
            authority.ShopRouteOccurrenceId);
        var built = BuildFileBackedSupportingTransitionProof(
            supportRoot,
            supportInputs,
            baseLedger,
            before,
            (compilation, afterPath) =>
            {
                var after = WriteShopPurchaseNavigationProofAfterSnapshot(
                    beforeSnapshotPath,
                    afterPath);
                return new FileBackedSupportingTransitionExecution(
                    after,
                    ShopPurchaseNavigationExecutionReceipt(
                        compilation,
                        before,
                        after));
            });
        Require(built.Request.SupportTransitionKind ==
                    AcquisitionRouteSupportingTransitionKinds
                        .ShopPurchaseNavigation &&
                built.Request.PurchaseStage == "route_connector" &&
                built.Compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.traverse_connector" &&
                built.TransitionReceipt.PurchaseTransition is
                {
                    Verified: true,
                    Stage: "route_connector",
                    ShopId: "FixtureShop",
                    StockId: "fixture-beer",
                    QualifiedItemId: "(O)346",
                    ObservedCurrencyDecrease: 0,
                    ObservedItemIncrease: 0
                },
            "File-backed shop navigation lost its exact direct-purchase " +
            "continuation identity.");

        return VerifySupportingTransitionTerminalRoute(
            authority,
            supportInputs,
            built.Proof,
            built.AfterSnapshotPath,
            built.SettledLedgerPath,
            root,
            terminalRouteOccurrenceId: authority.ShopRouteOccurrenceId,
            supportRouteOccurrenceId: authority.ShopRouteOccurrenceId);
    }

    private static QueueExecutionReceiptEnvelope
        ShopPurchaseNavigationExecutionReceipt(
            AcquisitionRouteDispatchCompilation compilation,
            StardewAI.Contracts.State.SnapshotEnvelope before,
            StardewAI.Contracts.State.SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Shop navigation self-test queue is null.");
        var item = queue.Items.Single();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.shop-purchase-navigation.self-test",
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 1,
            ExecutedItemCount = 1,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 1,
            SelectedCandidateId = compilation.SelectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = new[]
            {
                new QueueExecutionStepReceipt
                {
                    QueueItemIndex = 0,
                    QueueItemCount = 1,
                    OriginalPlannedItemCount = 1,
                    QueueId = queue.QueueId,
                    QueueItemId = item.QueueItemId,
                    OptionId = item.OptionId,
                    SourceStateHash = before.StateHash,
                    CompiledCommandStateHash = before.StateHash,
                    TeacherPreferenceStateRebound = true,
                    SelectedQueueCandidateCompleted = true,
                    AfterStateHash = after.StateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = after.GameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = item.NormalizedCommand.Steps.Single()
                        .StepType,
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_route_connector_transition_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "player.location_id=Town",
                        "fresh_snapshot_after_route_connector=true"
                    })
                }
            }
        };
    }
}
