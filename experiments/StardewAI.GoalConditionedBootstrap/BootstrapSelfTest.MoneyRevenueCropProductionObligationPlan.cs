using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCropProductionObligationPlan()
    {
        var root = TempArtifactRoot("money-crop-production-obligation-plan");
        var snapshot = MoneyCropProductionSnapshot();
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.year2.spring.strawberry",
                SourceDecisionId = "teacher.money.crop.year2.spring.v1",
                SeedId = "745",
                TileCount = 100,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-16T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money crop-production fixture commitment was rejected.");
        var ledger = mutation.Ledger!;

        var snapshotPath = Path.Combine(root, "snapshot.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var requestPath = Path.Combine(root, "request.json");
        Write(snapshotPath, snapshot);
        Write(ledgerPath, ledger);
        Write(requestPath, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = snapshotPath,
            StrategyLedgerPath = ledgerPath
        });

        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            requestPath);
        var planPath = Path.Combine(root, "plan.json");
        Write(planPath, plan);
        var commitment = plan.Commitments.Single();
        Require(plan.Status == "crop_production_evidence_collection_ready" &&
                plan.ActiveCommitmentCount == 1 &&
                plan.MinimumGrossRevenue == 60_000 &&
                !plan.MinimumGrossRevenueReachesTarget &&
                !plan.FutureCropProductionProofReady &&
                !plan.FreshSaveRevenueScheduleReady &&
                !plan.FormalProductTrainingAuthorized &&
                commitment.CurrentUnreservedSeedQuantity == 40 &&
                commitment.CurrentReservedSeedQuantity == 0 &&
                commitment.SeedQuantityShortfall == 60 &&
                commitment.RequiredWateringTotalDays.Length == 24 &&
                commitment.HarvestWaves.Select(value =>
                    value.HarvestTotalDay).SequenceEqual(
                        new[] { 120, 124, 128, 132, 136 }) &&
                commitment.MinimumTotalUnits == 500 &&
                commitment.PendingEvidenceRequestIds.Length == 10 &&
                commitment.PendingEvidenceRequestIds.Contains(
                    "planting-layout:money.year2.spring.strawberry",
                    StringComparer.Ordinal) &&
                commitment.PendingEvidenceRequestIds.Contains(
                    "seed-acquisition:money.year2.spring.strawberry:60",
                    StringComparer.Ordinal),
            "Money crop-production obligation plan lost an exact future obligation.");
        VerifyMoneyCropSitePreparationPlan(root, planPath);
        VerifyMoneyCropPlantingLayoutCheckpoint(root, planPath, plan);
        VerifyMoneyCropDailyCapacityProof(root, planPath, plan);
        VerifyMoneyCropSeedAcquisitionProof(
            root,
            planPath,
            plan,
            snapshot);
        VerifyMoneyCropSeedReservationProof(root, snapshot);
        VerifyMoneyCropNativeLifecycleProof();

        var pastPlantingSnapshot = CloneSnapshot(snapshot);
        var pastTime = JsonNode.Parse(
            pastPlantingSnapshot.State["time"].GetRawText())!.AsObject();
        pastTime["total_days"]!["value"] = 113;
        pastTime["year"]!["value"] = 2;
        pastTime["season"]!["value"] = "spring";
        pastTime["day"]!["value"] = 2;
        pastPlantingSnapshot.State["time"] =
            JsonSerializer.SerializeToElement(pastTime, JsonDefaults.Options);
        pastPlantingSnapshot.StateHash = SnapshotHash.ComputeStateHash(
            pastPlantingSnapshot.State);
        var materialGraph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
            pastPlantingSnapshot.State["farm"]
                .GetProperty("material_inventory_graph")
                .GetProperty("value")
                .GetRawText(),
            JsonDefaults.Options)!;
        var pastSupply = new MaterialSupplyProjection().Project(
            materialGraph,
            ledger.MaterialReservations);
        var pastPlantingRejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.BuildPlan(
                requestPath,
                new string('1', 64),
                snapshotPath,
                new string('2', 64),
                ledgerPath,
                new string('3', 64),
                new MoneyRevenueCropProductionObligationPlanRequest(),
                pastPlantingSnapshot,
                ledger,
                pastSupply);
        }
        catch (InvalidDataException)
        {
            pastPlantingRejected = true;
        }
        Require(pastPlantingRejected,
            "A Money crop commitment with a past planting day was admitted.");

        ledger.CropPlantingCommitments[0].HarvestTotalDays =
            new[] { 120 };
        var tamperedLedgerPath = Path.Combine(root, "ledger-tampered.json");
        var tamperedRequestPath = Path.Combine(root, "request-tampered.json");
        Write(tamperedLedgerPath, ledger);
        Write(tamperedRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = snapshotPath,
                StrategyLedgerPath = tamperedLedgerPath
            });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Money crop-production harvest schedule was admitted.");

        ledger.CropPlantingCommitments[0].HarvestTotalDays = null!;
        Write(tamperedLedgerPath, ledger);
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A null Money crop-production harvest schedule was admitted.");
    }

    private static void VerifyMoneyCropSeedAcquisitionProof(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan,
        SnapshotEnvelope before)
    {
        var commitment = plan.Commitments.Single();
        var after = CloneSnapshot(before);
        var farm = JsonNode.Parse(
            after.State["farm"].GetRawText())!.AsObject();
        farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]![
            "slots"]![0]!["stack"] = 100;
        after.State["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 1;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);

        var transition = new AcquisitionTerminalTransitionEvidence
        {
            TransitionKind = "exact_player_inventory_quantity_increase",
            QualifiedItemId = commitment.SeedQualifiedItemId,
            RequiredAmount = commitment.SeedQuantityShortfall,
            MinimumQuality = 0,
            BeforeQuantity = 40,
            AfterQuantity = 100,
            QuantityIncrease = commitment.SeedQuantityShortfall,
            Resolved = true,
            Verified = true
        };
        var freshReceipt = new AcquisitionRouteFreshTerminalReceiptAdmission
        {
            Status = "verified_fresh_terminal_receipt",
            GoalId = "earn_money",
            RouteOccurrenceId = "route.money.seed-acquisition.fixture",
            AfterSnapshotSha256 = new string('a', 64),
            TerminalTransition = transition,
            QueueExecutionVerified = true,
            FreshTerminalReceiptVerified = true,
            RouteTrainingEvidenceEligible = true
        };
        var rolloutReceipt = new AcquisitionRoutePortfolioRolloutProofReceipt
        {
            Status = "verified_complete_rollout_proof_chain",
            GoalId = "earn_money",
            LatestStateHash = after.StateHash,
            TransitionCount = 1,
            PortfolioCompletionVerified = true,
            ProofChainVerified = true
        };
        var proof = MoneyRevenueCropSeedAcquisitionProofBuilder.BuildProof(
            "seed-acquisition-request.json",
            new string('1', 64),
            planPath,
            CurrentTeacherFrontierSupport.HashFile(planPath),
            plan,
            commitment,
            plan.SnapshotPath,
            new string('2', 64),
            "seed-acquisition-rollout-receipt.json",
            new string('3', 64),
            plan.SnapshotPath,
            plan.SnapshotSha256,
            before,
            Path.Combine(root, "seed-acquisition-after.json"),
            freshReceipt.AfterSnapshotSha256,
            after,
            "seed-acquisition-fresh-terminal-receipt.json",
            new string('4', 64),
            freshReceipt,
            rolloutReceipt);
        Require(proof.Status ==
                    "verified_exact_seed_shortfall_acquisition" &&
                proof.EvidenceRequestId ==
                    "seed-acquisition:money.year2.spring.strawberry:60" &&
                proof.RequiredShortfallQuantity == 60 &&
                proof.VerifiedQuantityIncrease == 60 &&
                proof.SeedAcquisitionVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop seed-acquisition proof lost the exact shortfall.");

        transition.QuantityIncrease = 59;
        var partialRejected = false;
        try
        {
            _ = MoneyRevenueCropSeedAcquisitionProofBuilder.BuildProof(
                "seed-acquisition-request.json",
                new string('1', 64),
                planPath,
                CurrentTeacherFrontierSupport.HashFile(planPath),
                plan,
                commitment,
                plan.SnapshotPath,
                new string('2', 64),
                "seed-acquisition-rollout-receipt.json",
                new string('3', 64),
                plan.SnapshotPath,
                plan.SnapshotSha256,
                before,
                Path.Combine(root, "seed-acquisition-after.json"),
                freshReceipt.AfterSnapshotSha256,
                after,
                "seed-acquisition-fresh-terminal-receipt.json",
                new string('4', 64),
                freshReceipt,
                rolloutReceipt);
        }
        catch (InvalidDataException)
        {
            partialRejected = true;
        }
        Require(partialRejected,
            "A partial Money crop seed acquisition was admitted.");

        transition.QuantityIncrease = commitment.SeedQuantityShortfall;
        var wrongBoundaryRejected = false;
        try
        {
            _ = MoneyRevenueCropSeedAcquisitionProofBuilder.BuildProof(
                "seed-acquisition-request.json",
                new string('1', 64),
                planPath,
                CurrentTeacherFrontierSupport.HashFile(planPath),
                plan,
                commitment,
                "wrong-source-snapshot.json",
                new string('2', 64),
                "seed-acquisition-rollout-receipt.json",
                new string('3', 64),
                "wrong-source-snapshot.json",
                plan.SnapshotSha256,
                before,
                Path.Combine(root, "seed-acquisition-after.json"),
                freshReceipt.AfterSnapshotSha256,
                after,
                "seed-acquisition-fresh-terminal-receipt.json",
                new string('4', 64),
                freshReceipt,
                rolloutReceipt);
        }
        catch (InvalidDataException)
        {
            wrongBoundaryRejected = true;
        }
        Require(wrongBoundaryRejected,
            "A Money crop seed acquisition from the wrong plan boundary was admitted.");
    }

    private static void VerifyMoneyCropSeedReservationProof(
        string root,
        SnapshotEnvelope snapshot)
    {
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.seed-reservation.fixture",
                SourceDecisionId = "teacher.money.seed-reservation.v1",
                SeedId = "745",
                TileCount = 40,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-16T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money seed-reservation fixture commitment was rejected.");
        var sourceLedger = mutation.Ledger!;
        var snapshotPath = Path.Combine(root, "seed-reservation-snapshot.json");
        var sourceLedgerPath = Path.Combine(root, "seed-reservation-source-ledger.json");
        var planRequestPath = Path.Combine(root, "seed-reservation-plan-request.json");
        Write(snapshotPath, snapshot);
        Write(sourceLedgerPath, sourceLedger);
        Write(planRequestPath, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = snapshotPath,
            StrategyLedgerPath = sourceLedgerPath
        });
        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(planRequestPath);
        var planPath = Path.Combine(root, "seed-reservation-plan.json");
        Write(planPath, plan);

        var commitRequest = new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = sourceLedger.Revision,
            PortfolioId = "money.seed-reservation.fixture:portfolio",
            GoalId = "earn_money",
            SourceDecisionId = "teacher.money.seed-reservation.v1",
            MaterialClaims = new[]
            {
                new MaterialReservationUpsertRequest
                {
                    StateHash = snapshot.StateHash,
                    ExpectedLedgerRevision = sourceLedger.Revision,
                    ReservationId = "money.seed-reservation.fixture:seed:0",
                    SourceDecisionId = "teacher.money.seed-reservation.v1",
                    GoalId = "earn_money",
                    NodeId = "player:123",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)745",
                    Quantity = 40,
                    Purpose = "crop_seed_supply:money.seed-reservation.fixture"
                }
            }
        };
        var commitResult = new ReservationPortfolioLedgerService().Commit(
            sourceLedger,
            snapshot,
            commitRequest,
            "2026-10-16T00:01:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "Money seed-reservation atomic commit was rejected.");
        var committedLedger = commitResult.Ledger!;
        var committedLedgerPath = Path.Combine(root, "seed-reservation-committed-ledger.json");
        var commitRequestPath = Path.Combine(root, "seed-reservation-commit-request.json");
        var commitResultPath = Path.Combine(root, "seed-reservation-commit-result.json");
        Write(committedLedgerPath, committedLedger);
        Write(commitRequestPath, commitRequest);
        Write(commitResultPath, commitResult);
        var resumePlanRequestPath = Path.Combine(
            root,
            "seed-reservation-resume-plan-request.json");
        Write(resumePlanRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = snapshotPath,
                StrategyLedgerPath = committedLedgerPath
            });
        var resumePlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            resumePlanRequestPath);
        var resumedCommitment = resumePlan.Commitments.Single();
        Require(resumedCommitment.CurrentReservedSeedQuantity == 40 &&
                resumedCommitment.CurrentUnreservedSeedQuantity == 0 &&
                resumedCommitment.SeedQuantityShortfall == 0 &&
                resumedCommitment.SeedSupplyStatus ==
                    "existing_exact_reservation_satisfies_commitment" &&
                !resumedCommitment.PendingEvidenceRequestIds.Any(id =>
                    id.StartsWith("seed-acquisition:",
                        StringComparison.Ordinal)),
            "An existing exact seed reservation became a replacement-seed shortfall on resume.");
        var proofRequestPath = Path.Combine(root, "seed-reservation-proof-request.json");
        Write(proofRequestPath, new MoneyRevenueCropSeedReservationProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.seed-reservation.fixture",
            ReservationSnapshotPath = snapshotPath,
            CommittedStrategyLedgerPath = committedLedgerPath,
            AtomicCommitRequestPath = commitRequestPath,
            AtomicCommitResultPath = commitResultPath
        });
        var proof = MoneyRevenueCropSeedReservationProofBuilder.Build(proofRequestPath);
        Require(proof.SeedReservationVerified && proof.RequiredQuantity == 40 &&
                proof.ReservedQuantity == 40 && proof.ReservationIds.Length == 1 &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop seed-reservation proof lost its exact committed quantity.");

        var proofPath = Path.Combine(root, "seed-reservation-proof.json");
        var indexPath = Path.Combine(root, "seed-reservation-fragment-index.json");
        var checkpointRequestPath = Path.Combine(root, "seed-reservation-checkpoint-request.json");
        Write(proofPath, proof);
        Write(indexPath, new MoneyRevenueCropProductionProofFragmentIndex
        {
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 =
                CurrentTeacherFrontierSupport.HashFile(planPath),
            SeedReservationProofPaths = new[] { proofPath }
        });
        Write(checkpointRequestPath, new MoneyRevenueCropProductionProofCheckpointRequest
        {
            CropProductionPlanPath = planPath,
            FragmentIndexPath = indexPath
        });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 1 &&
                checkpoint.VerifiedSeedReservations.Length == 1 &&
                checkpoint.VerifiedRequestIds.SequenceEqual(
                    new[] { "seed-reservation:money.seed-reservation.fixture" },
                    StringComparer.Ordinal),
            "Money crop seed-reservation proof did not enter the exact checkpoint denominator.");

        var staleSnapshot = CloneSnapshot(snapshot);
        var staleTime = JsonNode.Parse(
            staleSnapshot.State["time"].GetRawText())!.AsObject();
        staleTime["total_days"]!["value"] = 102;
        staleSnapshot.State["time"] = JsonSerializer.SerializeToElement(
            staleTime,
            JsonDefaults.Options);
        staleSnapshot.StateHash = SnapshotHash.ComputeStateHash(
            staleSnapshot.State);
        var staleCommitRequest = JsonSerializer.Deserialize<
            ReservationPortfolioCommitRequest>(
                JsonSerializer.Serialize(commitRequest, JsonDefaults.Options),
                JsonDefaults.Options) ?? throw new InvalidDataException(
                    "Money seed-reservation stale request clone failed.");
        staleCommitRequest.StateHash = staleSnapshot.StateHash;
        foreach (var claim in staleCommitRequest.MaterialClaims)
        {
            claim.StateHash = staleSnapshot.StateHash;
        }
        var staleCommitResult = new ReservationPortfolioLedgerService().Commit(
            sourceLedger,
            staleSnapshot,
            staleCommitRequest,
            "2026-10-15T23:59:00Z");
        Require(staleCommitResult.Accepted &&
                staleCommitResult.Ledger is not null,
            "Money seed-reservation stale fixture commit was rejected before proof validation.");
        var staleSnapshotPath = Path.Combine(
            root,
            "seed-reservation-stale-snapshot.json");
        var staleLedgerPath = Path.Combine(
            root,
            "seed-reservation-stale-ledger.json");
        var staleCommitRequestPath = Path.Combine(
            root,
            "seed-reservation-stale-commit-request.json");
        var staleCommitResultPath = Path.Combine(
            root,
            "seed-reservation-stale-commit-result.json");
        var staleProofRequestPath = Path.Combine(
            root,
            "seed-reservation-stale-proof-request.json");
        Write(staleSnapshotPath, staleSnapshot);
        Write(staleLedgerPath, staleCommitResult.Ledger!);
        Write(staleCommitRequestPath, staleCommitRequest);
        Write(staleCommitResultPath, staleCommitResult);
        Write(staleProofRequestPath,
            new MoneyRevenueCropSeedReservationProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = "money.seed-reservation.fixture",
                ReservationSnapshotPath = staleSnapshotPath,
                CommittedStrategyLedgerPath = staleLedgerPath,
                AtomicCommitRequestPath = staleCommitRequestPath,
                AtomicCommitResultPath = staleCommitResultPath
            });
        var staleRejected = false;
        try
        {
            _ = MoneyRevenueCropSeedReservationProofBuilder.Build(
                staleProofRequestPath);
        }
        catch (InvalidDataException)
        {
            staleRejected = true;
        }
        Require(staleRejected,
            "A predated Money crop seed reservation snapshot was admitted.");

        committedLedger.MaterialReservations[0].Quantity = 39;
        var tamperedLedgerPath = Path.Combine(root, "seed-reservation-ledger-tampered.json");
        Write(tamperedLedgerPath, committedLedger);
        var tamperedRequestPath = Path.Combine(root, "seed-reservation-request-tampered.json");
        Write(tamperedRequestPath, new MoneyRevenueCropSeedReservationProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.seed-reservation.fixture",
            ReservationSnapshotPath = snapshotPath,
            CommittedStrategyLedgerPath = tamperedLedgerPath,
            AtomicCommitRequestPath = commitRequestPath,
            AtomicCommitResultPath = commitResultPath
        });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropSeedReservationProofBuilder.Build(tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A hand-authored partial Money crop seed reservation ledger was admitted as full proof.");
    }

    private static void VerifyMoneyCropSitePreparationPlan(
        string root,
        string planPath)
    {
        var snapshotPath = Path.Combine(
            root,
            "site-preparation-snapshot.json");
        var requestPath = Path.Combine(
            root,
            "site-preparation-request.json");
        var snapshot = MoneyCropSitePreparationSnapshot();
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new MoneyRevenueCropPlantingTile(
                5 + index % 10,
                6 + index / 10))
            .ToArray();
        Write(snapshotPath, snapshot);
        Write(requestPath, new MoneyRevenueCropSitePreparationPlanRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.year2.spring.strawberry",
            PreparationSnapshotPath = snapshotPath,
            LocationId = "Farm",
            Tiles = tiles
        });

        var plan = MoneyRevenueCropSitePreparationPlanBuilder.Build(
            requestPath);

        Require(plan.Status == "site_preparation_plan_ready" &&
                plan.TileCount == 100 &&
                plan.AlreadyPreparedCount == 0 &&
                plan.DirectTillCount == 100 &&
                plan.ClearThenRebindCount == 0 &&
                plan.BlockedCount == 0 &&
                plan.SitePreparationPlanReady &&
                plan.PlantingLayoutProofStillRequired &&
                !plan.FormalProductTrainingAuthorized &&
                plan.Steps.All(step =>
                    step.ActionOptionId == "executor.till_soil" &&
                    step.Parameters.Any(parameter =>
                        parameter.Name == "fresh_snapshot_rebind_required" &&
                        parameter.Value == "true")),
            "Money crop site-preparation plan lost its bounded till bindings.");
    }

    private static void VerifyMoneyCropPlantingLayoutCheckpoint(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var plantingSnapshotPath = Path.Combine(
            root,
            "planting-day-snapshot.json");
        var plantingSnapshot = MoneyCropPlantingDaySnapshot();
        Write(plantingSnapshotPath, plantingSnapshot);
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new MoneyRevenueCropPlantingTile(
                5 + index % 10,
                6 + index / 10))
            .ToArray();
        var layoutRequestPath = Path.Combine(root, "layout-request.json");
        var layoutRequest = new MoneyRevenueCropPlantingLayoutProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.year2.spring.strawberry",
            PlantingSnapshotPath = plantingSnapshotPath,
            LocationId = "Farm",
            Tiles = tiles
        };
        Write(layoutRequestPath, layoutRequest);
        var proof = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            layoutRequestPath);
        Require(proof.Status == "verified_planting_layout" &&
                proof.EvidenceRequestId ==
                    "planting-layout:money.year2.spring.strawberry" &&
                proof.PlantingTotalDay == 112 &&
                proof.TileCount == 100 &&
                proof.PlantingLayoutVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop planting-layout proof lost exact planting-day evidence.");

        var proofPath = Path.Combine(root, "layout-proof.json");
        var fragmentIndexPath = Path.Combine(root, "fragment-index.json");
        var checkpointRequestPath = Path.Combine(
            root,
            "checkpoint-request.json");
        Write(proofPath, proof);
        Write(fragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { proofPath }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
            checkpointRequestPath);
        Require(checkpoint.Status ==
                    "crop_production_proof_collection_in_progress" &&
                checkpoint.RequestCount ==
                    plan.PendingEvidenceRequestIds.Length &&
                checkpoint.VerifiedFragmentCount == 1 &&
                checkpoint.PendingRequestCount == 9 &&
                checkpoint.VerifiedRequestIds.SequenceEqual(
                    new[]
                    {
                        "planting-layout:money.year2.spring.strawberry"
                    },
                    StringComparer.Ordinal) &&
                checkpoint.VerifiedPlantingLayouts.Length == 1 &&
                !checkpoint.FutureCropProductionProofReady &&
                !checkpoint.FreshSaveRevenueScheduleReady &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money crop-production proof checkpoint admitted incomplete future production.");

        var wrongPlanHashIndexPath = Path.Combine(
            root,
            "fragment-index-wrong-plan-hash.json");
        var wrongPlanHashRequestPath = Path.Combine(
            root,
            "checkpoint-request-wrong-plan-hash.json");
        Write(wrongPlanHashIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 = new string('0', 64),
                PlantingLayoutProofPaths = new[] { proofPath }
            });
        Write(wrongPlanHashRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = wrongPlanHashIndexPath
            });
        var wrongPlanHashRejected = false;
        try
        {
            _ = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                wrongPlanHashRequestPath);
        }
        catch (InvalidDataException)
        {
            wrongPlanHashRejected = true;
        }
        Require(wrongPlanHashRejected,
            "A crop proof fragment index bound to the wrong plan hash was admitted.");

        layoutRequest.Tiles[0] = new MoneyRevenueCropPlantingTile(999, 999);
        var invalidLayoutRequestPath = Path.Combine(
            root,
            "layout-request-invalid.json");
        Write(invalidLayoutRequestPath, layoutRequest);
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
                invalidLayoutRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "An unavailable Money crop planting-layout tile was admitted.");

        proof.PlantingLayoutVerified = false;
        var tamperedProofPath = Path.Combine(root, "layout-proof-tampered.json");
        var tamperedFragmentIndexPath = Path.Combine(
            root,
            "fragment-index-tampered.json");
        var tamperedCheckpointRequestPath = Path.Combine(
            root,
            "checkpoint-request-tampered.json");
        Write(tamperedProofPath, proof);
        Write(tamperedFragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { tamperedProofPath }
            });
        Write(tamperedCheckpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = tamperedFragmentIndexPath
            });
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                tamperedCheckpointRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Money crop planting-layout proof was admitted.");
    }

    private static SnapshotEnvelope MoneyCropPlantingDaySnapshot()
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropField(112),
            year = MoneyCropField(2),
            season = MoneyCropField("spring"),
            day = MoneyCropField(1)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropField("Farm"),
            money = MoneyCropRuntimeField(0),
            total_money_earned = MoneyCropRuntimeField(0U),
            seed_inventory = MoneyCropRuntimeField(new[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "745",
                    seed_id = "745",
                    stack = 40
                }
            }),
            inventory = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        var farm = JsonNode.Parse(state["farm"].GetRawText())!.AsObject();
        farm["crops"] = JsonSerializer.SerializeToNode(
            MoneyCropRuntimeField(Array.Empty<object>()),
            JsonDefaults.Options);
        farm["shipping_bins"] = JsonSerializer.SerializeToNode(
            MoneyCropRuntimeField(new[]
            {
                new
                {
                    completed = true,
                    bin_scope = "personal",
                    player_id = "123",
                    pending_settlement_value = 0,
                    pending_settlement_value_complete = true,
                    pending_settlement_timing =
                        "next_native_day_transition",
                    contents_signature = "empty-shipping-scope",
                    contents_truncated = false
                }
            }),
            JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        var hoeDirtTiles = Enumerable.Range(0, 100).Select(index => new
        {
            tile_x = 5 + index % 10,
            tile_y = 6 + index / 10,
            has_crop = false,
            is_garden_pot = false,
            occupied_object_qualified_id = (string?)null,
            seed_results = new[]
            {
                new
                {
                    slot_index = 0,
                    seed_id = "745",
                    hard_rule_allows_planting = true,
                    can_mature_before_season_end_with_paddy_if_eligible = true,
                    adjusted_grow_days_with_paddy_if_eligible = 7,
                    days_remaining_in_season = 27
                }
            }
        }).ToArray();
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            planting_context = MoneyCropField(new
            {
                location_id = "Farm",
                season = "spring",
                is_outdoors = true,
                is_farm = true,
                is_greenhouse = false,
                hoe_dirt_tiles = hoeDirtTiles
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 200;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static SnapshotEnvelope MoneyCropSitePreparationSnapshot()
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropRuntimeField(111),
            year = MoneyCropRuntimeField(1),
            season = MoneyCropRuntimeField("winter"),
            day = MoneyCropRuntimeField(28),
            time = MoneyCropRuntimeField(900)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropRuntimeField("Farm"),
            tile_x = MoneyCropRuntimeField(4),
            tile_y = MoneyCropRuntimeField(6),
            energy = MoneyCropRuntimeField(270),
            inventory = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        var siteTiles = Enumerable.Range(0, 100).Select(index => new
        {
            tile_x = 5 + index % 10,
            tile_y = 6 + index / 10,
            status = "ready_to_till",
            clearable_blocker_kinds = Array.Empty<string>()
        }).ToArray();
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            map = MoneyCropRuntimeField(new { id = "Farm" }),
            objects = MoneyCropRuntimeField(Array.Empty<object>()),
            terrain_features = MoneyCropRuntimeField(Array.Empty<object>()),
            site_preparation_context = MoneyCropRuntimeField(new
            {
                projection_status =
                    "complete_bounded_current_farm_region",
                location_id = "Farm",
                hoe_tool_slot_index = 2,
                hoe_tool_qualified_item_id = "(T)Hoe",
                hoe_energy_cost_per_single_tile_action = 1.7,
                tiles = siteTiles
            })
        }, JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(new
        {
            crop_catalog = MoneyCropRuntimeField(new[]
            {
                new
                {
                    seed_id = "745",
                    seasons = new[] { "spring" },
                    grow_days = 8,
                    regrow_days = 4,
                    harvest_item_id = "400",
                    harvest_item_qualified_id = "(O)400",
                    harvest_context_tags = new[]
                    {
                        "category_fruits",
                        "item_strawberry"
                    },
                    harvest_unit_sale_price = 120,
                    harvest_min_stack = 1,
                    needs_watering = true
                }
            }),
            material_inventory_graph = snapshot.State["farm"]
                .GetProperty("material_inventory_graph"),
            resource_clumps = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        state["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = MoneyCropRuntimeField(new
            {
                is_open = false,
                type = "none"
            })
        }, JsonDefaults.Options);
        state["locations"] = JsonSerializer.SerializeToElement(new
        {
            collision_grid = MoneyCropRuntimeField(new
            {
                location_id = "Farm",
                width = 100,
                height = 100,
                notable_tiles = Array.Empty<object>()
            }),
            route_action_branch_coverage = MoneyCropRuntimeField(new
            {
                rows = Array.Empty<object>()
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 150;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static object MoneyCropField<T>(T value) => new
    {
        value,
        status = FieldStatus.Available
    };

    private static object MoneyCropRuntimeField<T>(T value) => new
    {
        value,
        status = FieldStatus.Available,
        source = new
        {
            kind = "test",
            path = "money_crop_site_preparation_self_test"
        },
        adapter = "test",
        read_at_tick = 150,
        confidence = 1.0
    };

    private static SnapshotEnvelope MoneyCropProductionSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "identity": {
                "save_id": {"value":"MoneyCropFarm","status":"available"},
                "player_id": {"value":"123","status":"available"}
              },
              "time": {
                "total_days": {"value":103,"status":"available"},
                "year": {"value":1,"status":"available"},
                "season": {"value":"winter","status":"available"},
                "day": {"value":20,"status":"available"}
              },
              "farm": {
                "crop_catalog": {"value":[{
                  "seed_id":"745",
                  "seasons":["spring"],
                  "grow_days":8,
                  "regrow_days":4,
                  "harvest_item_id":"400",
                  "harvest_item_qualified_id":"(O)400",
                  "harvest_context_tags":["category_fruits","item_strawberry"],
                  "harvest_unit_sale_price":120,
                  "harvest_min_stack":1,
                  "is_raised":false,
                  "needs_watering":true
                }],"status":"available"},
                "material_inventory_graph": {"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "ownership_class":"actor_owned",
                    "actor_use_authorized":true,
                    "slots":[{
                      "slot_index":0,
                      "item_id":"745",
                      "qualified_item_id":"(O)745",
                      "context_tags":[],
                      "stack":40
                    }]
                  }],
                  "access_points":[],
                  "workbench_links":[],
                  "quantity_rows":[],
                  "physical_inventory_count":1,
                  "access_point_count":0,
                  "deduplicated_access_point_count":0,
                  "default_shared_resource_policy":"deny_without_explicit_authorization"
                },"status":"available"}
              }
            }
            """,
            JsonDefaults.Options)!;
        var snapshot = new SnapshotEnvelope
        {
            BridgeVersion = "self-test",
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "MoneyCropFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = 100,
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
