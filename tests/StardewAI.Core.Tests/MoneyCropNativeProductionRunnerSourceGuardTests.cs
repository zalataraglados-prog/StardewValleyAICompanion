namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionRunnerSourceGuardTests
{
    private static readonly string Runner = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeMoneyCropNativeProduction.ps1"));
    private static readonly string CropRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropNative.ps1"));
    private static readonly string CommonRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeEvidenceCommon.ps1"));
    private static readonly string SeedAcquisitionRuntime = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropSeedAcquisition.ps1"));
    private static readonly string SeedAcquisitionProofStages =
        File.ReadAllText(
            FindRepositoryFile(
                "experiments",
                "StardewAI.GoalConditionedBootstrap",
                "MoneyRevenueCropSeedAcquisitionProofBuilder.Stages.cs"));
    private static readonly string ProductionInput = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropProductionInput.ps1"));
    private static readonly string EvidenceFinalization = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "lib",
            "RuntimeMoneyCropEvidenceFinalization.ps1"));
    private static readonly string PersistentBoundaryNormalization =
        File.ReadAllText(
            FindRepositoryFile(
                "experiments",
                "StardewAI.GoalConditionedBootstrap",
                "FullShipmentRecoverySettlementBoundary.Normalization.cs"));
    private static readonly string TerminalSettlementRuntime =
        File.ReadAllText(
            FindRepositoryFile(
                "scripts",
                "lib",
                "RuntimeMoneyCropTerminalSettlement.ps1"));
    private static readonly string EvidenceFinalizer = File.ReadAllText(
        FindRepositoryFile(
            "scripts",
            "Finalize-RuntimeMoneyCropNativeProductionEvidence.ps1"));
    private static readonly string QueueReceiptValidator = File.ReadAllText(
        FindRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "QueueExecutionReceiptValidator.cs"));
    private static readonly string ContinuationReceiptValidator =
        File.ReadAllText(
            FindRepositoryFile(
                "experiments",
                "StardewAI.GoalConditionedBootstrap",
                "QueueExecutionReceiptValidator.Continuation.cs"));
    private static readonly string SeedReservationCommitBuilder =
        File.ReadAllText(
            FindRepositoryFile(
                "experiments",
                "StardewAI.GoalConditionedBootstrap",
                "MoneyRevenueCropSeedReservationCommitBuilder.cs"));
    private static readonly string CropProjection = File.ReadAllText(
        FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.CropsWorld.cs"));
    private static readonly string FarmProjection = File.ReadAllText(
        FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "FarmReadAdapter.cs"));

    [Fact]
    public void CropCatalogUsesNativeSellToStoreRevenueSemantics()
    {
        Assert.Contains(
            "harvest_unit_base_price = harvestItem.BasePrice",
            CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "harvest_unit_sale_price = harvestItem.SellToStorePrice",
            CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.sellToStorePrice(-1L)",
            CropProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "harvest_unit_sale_price = harvestItem.SalePrice",
            CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "Item.sellToStorePrice(-1)",
            FarmProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "vanilla_1_6_crop_data",
            FarmProjection,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerUsesImmutableSaveAndExactMinimalModSet()
    {
        Assert.Contains("[string] $ProductionInputManifestPath", Runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain("[string] $SourceSaveSlotPath", Runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain("[string] $RunPlanRequestPath", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Read-MoneyCropNativeProductionInputManifest", Runner,
            StringComparison.Ordinal);
        Assert.Equal(3,
            Runner.Split(
                "Read-MoneyCropNativeProductionInputManifest",
                StringSplitOptions.None).Length - 1);
        Assert.Contains(
            "production input manifest changed before process startup",
            Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "production input manifest changed during native production",
            Runner,
            StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $sourceSave", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Get-RuntimeExactModAllowlist", Runner,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.TransparentBridge\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("\"StardewAI.RuntimeTestHarness\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"",
            Runner, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", Runner,
            StringComparison.Ordinal);
        Assert.Contains("source_save_unchanged", Runner,
            StringComparison.Ordinal);
        Assert.Contains("money_crop_native_source_preflight.v2", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Get-MoneyCropSourceBoundaryHash", Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "$observedSourceBoundaryHash -eq $expectedSourceBoundaryHash",
            Runner, StringComparison.Ordinal);
        Assert.Contains("environment.training_run_id", Runner,
            StringComparison.Ordinal);
        Assert.Contains("environment.save_isolation_path", Runner,
            StringComparison.Ordinal);
        Assert.Contains("$initialDay -eq [int]$runPlan.source_total_day",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Stop-RuntimeEvidenceProcesses", Runner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerCompilesExistingActionsAndRoutesWithoutASecondExecutor()
    {
        Assert.DoesNotContain(
            "build-money-revenue-crop-native-production-run-plan",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -LiteralPath $productionInput.RunPlanPath",
            Runner, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropActionBatch", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSeedAcquisition", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSitePreparation", Runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-site-preparation-plan",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropSitePreparationAction",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("executor.till_soil", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("executor.clear_obstacle", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("executor.break_farm_resource_clump", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("fresh=true", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("-BeforeSnapshotProfile \"farm_layout\"", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("--snapshot-file-profile", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$step = $pending[0]", CropRuntime,
            StringComparison.Ordinal);
        Assert.True(
            Runner.IndexOf(
                "Invoke-RuntimeCropSitePreparation",
                StringComparison.Ordinal) <
            Runner.IndexOf(
                "daily-capacity-day-$totalDay.json",
                StringComparison.Ordinal),
            "Site preparation must finish before the planting-day capacity boundary is captured.");
        Assert.Contains("Invoke-RuntimeCropRoute", Runner,
            StringComparison.Ordinal);
        Assert.Contains("api/v1/small-model/action-queue/compile",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("api/v1/small-model/plan/action-queue/compile",
            CropRuntime, StringComparison.Ordinal);
        Assert.Contains("executor.traverse_connector", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("-ExecutionTarget \"runtime_harness\"", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("crop_needs_watering", CropRuntime,
            StringComparison.Ordinal);
        Assert.Contains("ConvertTo-RuntimeCropSeedId", CropRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute", Runner + CropRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("debug.", Runner + CropRuntime,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SeedShortfallsReuseBoundedPurchasePlanningAndExactInventoryDeltas()
    {
        Assert.Contains("Invoke-RuntimeRanking", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeDailyPlanStep", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("-AllowVerifiedContinuation", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("VerifiedContinuationPending", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("VerifiedObjectiveCompleted", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("objective_continuation_completed", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("selected_queue_decision_complete", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains(
            "selected_queue_continuation_retry_next_iteration",
            CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$terminalBlockReasons", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains(
            "@($stepResults[$stepResults.Count - 1].block_reasons)",
            CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$boundedContinuationPending", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("\"--max-queue-item-attempts\", \"12\"",
            CommonRuntime, StringComparison.Ordinal);
        Assert.Contains("max_queue_item_attempts_reached", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]$execution.objective_continuation.kind",
            CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]$lastStep.primitive_verification_status -eq \"verified\"",
            CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("economy.buy_supplies", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("continuation.quantity", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("continuation.shop_id", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("continuation.max_unit_price", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("timeline_status -eq \"deferred\"",
            SeedAcquisitionRuntime, StringComparison.Ordinal);
        Assert.Contains("value = \"1\"", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("Import-RuntimeSnapshotFile", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("PurchaseSliceCount", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains("exceeded its exact target quantity",
            SeedAcquisitionRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventory", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Money", SeedAcquisitionRuntime,
            StringComparison.Ordinal);
        Assert.Contains(
            "continuation.option_id\") == \"economy.buy_supplies\"",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsStoryInterruptContinuation",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "optionId == \"executor.advance_story_event\"",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "VerifyTransitionStepBoundaries",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowPurchaseMutation: true",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryProjectExpectedResourceMutationInvariant",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsVerifiedNativeStoryInterrupt",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "runtime_priority_interrupt",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "receiptDirectory + Path.DirectorySeparatorChar",
            SeedAcquisitionProofStages,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeMachineProjectionCache(value)",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.Remove(\"machine_probe_cache_tick\")",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "calendar.Remove(\"time_of_day\")",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeIslandFieldOfficeRuntimeState(value)",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"menu_clear\"",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeRemoteRewardRuntimeState(value)",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeRemoteQuestRuntimeState(value)",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeSpecialOrderBoardRuntimeState(value)",
            PersistentBoundaryNormalization,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestVerifierClosesEveryProductionInputLineage()
    {
        Assert.Contains(
            "money_crop_native_production_input_manifest.v1",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("Assert-RuntimeFileIdentity", ProductionInput,
            StringComparison.Ordinal);
        foreach (var property in new[]
        {
            "teacher_request_sha256",
            "decision_request_sha256",
            "decision_receipt_sha256",
            "snapshot_sha256",
            "strategy_ledger_sha256",
            "crop_production_obligation_request_sha256",
            "crop_production_obligation_plan_sha256",
            "crop_native_run_plan_request_sha256",
            "crop_native_run_plan_sha256",
            "source_save_tree_sha256"
        })
        {
            Assert.Contains(property, ProductionInput,
                StringComparison.Ordinal);
        }
        Assert.Contains("Get-RuntimeRawJsonProperty", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("Assert-MoneyCropInputIdSet", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("Get-MoneyCropSourceBoundaryHash", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("__runtime_identity_excluded__", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains(
            "Remove-MoneyCropNonSemanticBoundaryMetadata",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("CanonicalJson", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("JsonDocument.Parse", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("StringComparer.Ordinal", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("machine_probe_cache_tick", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("player.movie_theater", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("formal_product_training_authorized", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_crop_production_decision_request.v2",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_crop_production_decision_receipt.v2",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("teacher_selection_policy", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("seed_quantity_shortfall", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains("seed_supply_deadline_total_day", ProductionInput,
            StringComparison.Ordinal);
        Assert.Contains(
            "economy.buy_supplies_one_unit_fresh_snapshot_slices",
            ProductionInput,
            StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            ProductionInput,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RunnerRetainsExactProofChainWithoutTrainingAuthorization()
    {
        var proofChain = Runner + EvidenceFinalization;

        Assert.Contains("supporting_route_to_crop", Runner,
            StringComparison.Ordinal);
        Assert.Contains("supporting_route_to_sleep", Runner,
            StringComparison.Ordinal);
        Assert.Contains("native_day_rollover", Runner,
            StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-planting-layout-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-daily-capacity-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("watering_can_refill_receipts", Runner,
            StringComparison.Ordinal);
        Assert.Contains("primitive_verification_status", Runner,
            StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-native-lifecycle-proof",
            Runner, StringComparison.Ordinal);
        Assert.Contains("build-money-revenue-crop-output-retention-proof",
            proofChain, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-production-proof-checkpoint",
            proofChain, StringComparison.Ordinal);
        Assert.Contains("New-MoneyCropOutputRetentionProofs", Runner,
            StringComparison.Ordinal);
        Assert.Contains("New-RuntimeCropSettlementCapacityProof", Runner,
            StringComparison.Ordinal);
        Assert.Contains("Invoke-RuntimeCropTerminalSettlement", Runner,
            StringComparison.Ordinal);
        Assert.Contains("executor.ship_inventory_item_to_bin",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("name = \"target_location\"; value = \"Farm\"",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("continuation.target_location",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("terminal-settlement-home-ingress",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("native_shipping_settlement",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("before_snapshot_path = $currentPath",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("before_snapshot_path = $sleep.BeforePath",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-terminal-settlement-proof",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.Contains("Complete-MoneyCropProductionProofCheckpoint", Runner,
            StringComparison.Ordinal);
        Assert.Contains("-TerminalSettlementProofPaths", Runner,
            StringComparison.Ordinal);
        Assert.Contains("-SettlementCapacityProofPaths", Runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/training/execute",
            TerminalSettlementRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            proofChain + TerminalSettlementRuntime, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingRunFinalizerReusesProofCodeWithoutLaunchingTheGame()
    {
        Assert.Contains("Read-MoneyCropNativeProductionInputManifest",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains("Assert-RuntimeFileIdentity -Path $runPlanPath",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains("Assert-MoneyCropNativeRunPlan",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains("New-MoneyCropOutputRetentionProofs",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains("Complete-MoneyCropProductionProofCheckpoint",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains("money_crop_native_evidence_finalization_summary.v1",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI", EvidenceFinalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", EvidenceFinalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-RuntimeCropActionBatch",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.DoesNotContain("formal_product_training_authorized = $true",
            EvidenceFinalizer, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedFinalizerKeepsSettlementFragmentsExplicitlyEmptyByDefault()
    {
        Assert.Contains(
            "money_revenue_crop_production_proof_fragment_index.v13",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains(
            "money_revenue_crop_production_proof_checkpoint_request.v12",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains("[object[]] $TerminalSettlementProofPaths = @()",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains("[object[]] $SettlementCapacityProofPaths = @()",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains(
            "[object[]] $SharedShippingSettlementProofPaths = @()",
            EvidenceFinalization, StringComparison.Ordinal);
    }

    [Fact]
    public void SeedReservationUsesTypedCoreCommitReplayForRunnerAndRecovery()
    {
        Assert.Contains("New-MoneyCropSeedReservationProof", Runner,
            StringComparison.Ordinal);
        Assert.Contains("New-MoneyCropSeedReservationProof",
            EvidenceFinalizer, StringComparison.Ordinal);
        Assert.Contains(
            "build-money-revenue-crop-seed-reservation-commit-request",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains(
            "replay-money-revenue-crop-seed-reservation-commit",
            EvidenceFinalization, StringComparison.Ordinal);
        Assert.Contains("ReservationPortfolioLedgerService().Commit",
            SeedReservationCommitBuilder, StringComparison.Ordinal);
        Assert.Contains("MaterialSupplyProjection().Project",
            SeedReservationCommitBuilder, StringComparison.Ordinal);
        Assert.Contains("crop_seed_supply:", SeedReservationCommitBuilder,
            StringComparison.Ordinal);
        Assert.DoesNotContain("material_reservations =",
            EvidenceFinalization, StringComparison.Ordinal);
    }

    [Fact]
    public void CommonQueueHelperKeepsProductDefaultAndAllowsHarnessExplicitly()
    {
        Assert.Contains("[string] $ExecutionTarget = \"product\"",
            CommonRuntime, StringComparison.Ordinal);
        Assert.Contains("[ValidateSet(\"product\", \"runtime_harness\")]",
            CommonRuntime, StringComparison.Ordinal);
        Assert.Contains("$Context.ExecutorRoot", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("$arguments.Add(\"--use-product-executor\")",
            CommonRuntime, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpFailuresPreserveModernResponseBodies()
    {
        Assert.Contains("[Net.Http.HttpResponseMessage]", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("Content.ReadAsStringAsync()", CommonRuntime,
            StringComparison.Ordinal);
        Assert.Contains("GetResponseStream", CommonRuntime,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NativeReceiptAllowsSameTickStateChangingSubStepsOnly()
    {
        Assert.Contains(
            "receipt.AfterGameTick <= receipt.BeforeGameTick",
            QueueReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "step.AfterGameTick < step.BeforeGameTick",
            QueueReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "receipt.AfterGameTick <= receipt.BeforeGameTick",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "step.AfterGameTick < step.BeforeGameTick",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "IsRecoverablePriorityStoryInterruptStep",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "movement_interrupted_by_story_event:",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "priority_story_event_interrupt",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "continuation.HasActiveObjectiveContinuation",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
        Assert.Contains(
            "var completionMarkerMatches = verifiedCompleted",
            ContinuationReceiptValidator,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CropWaterDemandDistinguishesNativeRequirementFromCurrentState()
    {
        Assert.Contains("var watered = dirt.isWatered();", CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "var requiresWatering = dirt.needsWatering();",
            CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "requires_watering_for_growth = requiresWatering",
            CropProjection,
            StringComparison.Ordinal);
        Assert.Contains(
            "needs_watering = requiresWatering && !watered",
            CropProjection,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "needs_watering = dirt.needsWatering()",
            CropProjection,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
