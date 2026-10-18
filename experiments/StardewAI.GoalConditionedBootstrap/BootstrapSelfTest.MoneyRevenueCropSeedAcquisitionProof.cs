using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyCropSeedAcquisitionProof(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan,
        SnapshotEnvelope before,
        StrategyCommitmentLedger continuationLedger)
    {
        var commitment = plan.Commitments.Single();
        VerifySeedAcquisitionRuntimeBoundaryNormalization(before);
        var runtimeBefore = SeedAcquisitionRuntimeBoundary(before);
        var transitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        var current = AddSeedAcquisitionTransition(
            root,
            transitions,
            runtimeBefore,
            sequence: 1,
            purchaseQuantity: 0,
            unitPrice: 200,
            supportingOptionId: "executor.wait_ticks");
        current = AddSeedAcquisitionTransition(
            root,
            transitions,
            current,
            sequence: 2,
            purchaseQuantity: 30,
            unitPrice: 200);
        current = AddSeedAcquisitionTransition(
            root,
            transitions,
            current,
            sequence: 3,
            purchaseQuantity: 30,
            unitPrice: 200);
        var manifestPath = Path.Combine(
            root,
            "seed-acquisition-execution-manifest.json");
        var requestPath = Path.Combine(
            root,
            "seed-acquisition-proof-request.json");
        var manifest = new MoneyRevenueCropSeedAcquisitionExecutionManifest
        {
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 =
                CurrentTeacherFrontierSupport.HashFile(planPath),
            CommitmentId = commitment.CommitmentId,
            SourceStateHash = runtimeBefore.StateHash,
            RunId = "run.seed-acquisition.fixture",
            Transitions = transitions.ToArray()
        };
        Write(manifestPath, manifest);
        Write(requestPath, new MoneyRevenueCropSeedAcquisitionProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = commitment.CommitmentId,
            ExecutionManifestPath = manifestPath
        });
        var proof = MoneyRevenueCropSeedAcquisitionProofBuilder.Build(
            requestPath);
        Require(proof.Status ==
                    "verified_exact_seed_shortfall_native_acquisition" &&
                proof.EvidenceRequestId ==
                    "seed-acquisition:money.year2.spring.strawberry:60" &&
                proof.RequiredShortfallQuantity == 60 &&
                proof.VerifiedQuantityIncrease == 60 &&
                proof.InitialSeedQuantity == 40 &&
                proof.FinalSeedQuantity == 100 &&
                proof.TransitionCount == 3 &&
                proof.SupportingTransitionCount == 1 &&
                proof.PurchaseSliceCount == 2 &&
                proof.Transitions.Select(value => value.Stage)
                    .SequenceEqual(new[]
                    {
                        "supporting_route_or_shop",
                        "purchase_slice",
                        "purchase_slice"
                    }) &&
                proof.SeedAcquisitionVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop seed-acquisition proof lost the exact shortfall.");

        var completeTransitions = manifest.Transitions.ToArray();
        RequireSeedAcquisitionRejects(
            root,
            "partial",
            planPath,
            commitment,
            runtimeBefore.StateHash,
            completeTransitions.Take(2).ToArray(),
            "A partial Money crop seed acquisition was admitted.");

        var discontinuousTransitions = completeTransitions.ToArray();
        discontinuousTransitions[1] = discontinuousTransitions[1] with
        {
            BeforeSnapshotPath = completeTransitions[0].BeforeSnapshotPath
        };
        RequireSeedAcquisitionRejects(
            root,
            "discontinuous",
            planPath,
            commitment,
            runtimeBefore.StateHash,
            discontinuousTransitions,
            "A discontinuous Money crop seed acquisition was admitted.");

        var purchaseQueuePath = completeTransitions[1].QueuePath;
        var purchaseQueue = CurrentTeacherFrontierSupport.Read<
            ActionQueueEnvelope>(
                purchaseQueuePath,
                "Money crop seed-acquisition purchase queue fixture");
        var purchaseItem = purchaseQueue.Items.Single(value =>
            value.OptionId == "executor.buy_shop_item");
        purchaseItem.NormalizedCommand.Parameters.Single(value =>
            value.Name == "continuation.quantity").Value = "2";
        var nonUnitQueuePath = Path.Combine(
            root,
            "seed-acquisition-non-unit-queue.json");
        Write(nonUnitQueuePath, purchaseQueue);
        var purchaseReceipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
                completeTransitions[1].ExecutionReceiptPath,
                "Money crop seed-acquisition purchase receipt fixture");
        purchaseReceipt.StepResults.Single(value =>
            value.QueueItemIndex == 0).EffectiveQueueItem =
                JsonSerializer.SerializeToElement(
                    purchaseItem,
                    JsonDefaults.Options);
        var nonUnitReceiptPath = Path.Combine(
            root,
            "seed-acquisition-non-unit-receipt.json");
        Write(nonUnitReceiptPath, purchaseReceipt);
        var nonUnitTransitions = completeTransitions.ToArray();
        nonUnitTransitions[1] = nonUnitTransitions[1] with
        {
            QueuePath = nonUnitQueuePath,
            ExecutionReceiptPath = nonUnitReceiptPath
        };
        RequireSeedAcquisitionRejects(
            root,
            "non-unit",
            planPath,
            commitment,
            runtimeBefore.StateHash,
            nonUnitTransitions,
            "A Money crop seed acquisition with a non-unit purchase slice was admitted.");

        var driftedBoundary = SeedAcquisitionSourceDrift(runtimeBefore);
        var driftedBoundaryPath = Path.Combine(
            root,
            "seed-acquisition-drifted-source.json");
        Write(driftedBoundaryPath, driftedBoundary);
        var driftedTransitions = completeTransitions.ToArray();
        driftedTransitions[0] = driftedTransitions[0] with
        {
            BeforeSnapshotPath = driftedBoundaryPath
        };
        RequireSeedAcquisitionRejects(
            root,
            "source-drift",
            planPath,
            commitment,
            driftedBoundary.StateHash,
            driftedTransitions,
            "A Money crop seed acquisition with non-runtime source drift was admitted.");

        VerifyLinkedSeedAcquisitionProofs(
            root,
            before,
            continuationLedger);
    }

    private static void VerifySeedAcquisitionRuntimeBoundaryNormalization(
        SnapshotEnvelope source)
    {
        var planned = CloneSnapshot(source);
        var observed = CloneSnapshot(source);
        SetSeedAcquisitionMovieTheaterBoundary(
            planned,
            projectionFingerprint: "legacy-tag-order",
            openStatus: "closed");
        SetSeedAcquisitionMovieTheaterBoundary(
            observed,
            projectionFingerprint: "canonical-tag-order",
            openStatus: "closed");
        observed = SeedAcquisitionRuntimeBoundary(observed);
        Require(
            MoneyRevenueCropSeedAcquisitionProofBuilder
                .SameRuntimeSourceBoundary(planned, observed),
            "Money crop seed-acquisition rejected a movie-theater fingerprint-only source drift.");

        SetSeedAcquisitionMovieTheaterBoundary(
            observed,
            projectionFingerprint: "canonical-tag-order",
            openStatus: "open");
        Require(
            !MoneyRevenueCropSeedAcquisitionProofBuilder
                .SameRuntimeSourceBoundary(planned, observed),
            "Money crop seed-acquisition ignored semantic movie-theater source drift.");
    }

    private static void SetSeedAcquisitionMovieTheaterBoundary(
        SnapshotEnvelope snapshot,
        string projectionFingerprint,
        string openStatus)
    {
        var player = JsonNode.Parse(
            snapshot.State["player"].GetRawText())!.AsObject();
        player["movie_theater"] = new JsonObject
        {
            ["status"] = "available",
            ["value"] = new JsonObject
            {
                ["projection_fingerprint"] = projectionFingerprint,
                ["open_status"] = openStatus
            }
        };
        snapshot.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static void RequireSeedAcquisitionRejects(
        string root,
        string name,
        string planPath,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        string sourceStateHash,
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions,
        string message)
    {
        var manifestPath = Path.Combine(
            root,
            "seed-acquisition-" + name + "-manifest.json");
        var requestPath = Path.Combine(
            root,
            "seed-acquisition-" + name + "-request.json");
        Write(manifestPath,
            new MoneyRevenueCropSeedAcquisitionExecutionManifest
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                CommitmentId = commitment.CommitmentId,
                SourceStateHash = sourceStateHash,
                RunId = "run.seed-acquisition.fixture",
                Transitions = transitions
            });
        Write(requestPath,
            new MoneyRevenueCropSeedAcquisitionProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                ExecutionManifestPath = manifestPath
            });
        RequireRejects(
            () => MoneyRevenueCropSeedAcquisitionProofBuilder.Build(
                requestPath),
            message);
    }

    private static SnapshotEnvelope SeedAcquisitionRuntimeBoundary(
        SnapshotEnvelope source)
    {
        var runtime = CloneSnapshot(source);
        var environment = JsonNode.Parse(
            runtime.State["environment"].GetRawText())!.AsObject();
        environment["training_run_id"]!["value"] =
            "run.seed-acquisition.fixture";
        environment["save_isolation_path"]!["value"] =
            Path.Combine(Path.GetTempPath(), "seed-acquisition-fixture");
        runtime.State["environment"] = JsonSerializer.SerializeToElement(
            environment,
            JsonDefaults.Options);
        runtime.GameTick = source.GameTick + 10;
        runtime.StateHash = SnapshotHash.ComputeStateHash(runtime.State);
        return runtime;
    }

    private static SnapshotEnvelope SeedAcquisitionSourceDrift(
        SnapshotEnvelope source)
    {
        var drifted = CloneSnapshot(source);
        var player = JsonNode.Parse(
            drifted.State["player"].GetRawText())!.AsObject();
        player["money"]!["value"] =
            player["money"]!["value"]!.GetValue<int>() - 1;
        drifted.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        drifted.StateHash = SnapshotHash.ComputeStateHash(drifted.State);
        return drifted;
    }

    private static void VerifyLinkedSeedAcquisitionProofs(
        string parentRoot,
        SnapshotEnvelope source,
        StrategyCommitmentLedger ledger)
    {
        var root = Path.Combine(parentRoot, "linked-seed-acquisitions");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "source.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var planRequestPath = Path.Combine(root, "plan-request.json");
        var planPath = Path.Combine(root, "plan.json");
        Write(sourcePath, source);
        Write(ledgerPath, ledger);
        Write(planRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = sourcePath,
                StrategyLedgerPath = ledgerPath
            });
        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            planRequestPath);
        Write(planPath, plan);
        var commitments = plan.Commitments.Where(value =>
                value.SeedQuantityShortfall > 0)
            .ToArray();
        Require(commitments.Length == 2 &&
                commitments[0].SeedQuantityShortfall == 60 &&
                commitments[1].SeedQuantityShortfall == 2,
            "Linked Money crop seed-acquisition fixture lost its denominator.");

        var firstTransitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        var current = AddSeedAcquisitionTransition(
            root,
            firstTransitions,
            source,
            sequence: 1,
            purchaseQuantity: 60,
            unitPrice: 200);
        var firstProofPath = BuildSeedAcquisitionFixtureProof(
            root,
            "first",
            planPath,
            plan,
            commitments[0],
            firstTransitions,
            priorProofPath: string.Empty);

        var secondTransitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        _ = AddSeedAcquisitionTransition(
            root,
            secondTransitions,
            current,
            sequence: 2,
            purchaseQuantity: 2,
            unitPrice: 200);
        var secondProofPath = BuildSeedAcquisitionFixtureProof(
            root,
            "second",
            planPath,
            plan,
            commitments[1],
            secondTransitions,
            firstProofPath);
        var secondProof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionProof>(
                secondProofPath,
                "Linked Money crop seed-acquisition proof fixture");
        Require(secondProof.PriorSeedAcquisitionProofPath ==
                    Path.GetFullPath(firstProofPath) &&
                secondProof.PriorSeedAcquisitionProofSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(firstProofPath) &&
                secondProof.PriorSeedAcquisitionEvidenceRequestId ==
                    "seed-acquisition:" + commitments[0].CommitmentId +
                    ":60" &&
                secondProof.VerifiedQuantityIncrease == 2 &&
                secondProof.SeedAcquisitionVerified,
            "Linked Money crop seed acquisition lost its prior-proof lineage.");

        var secondRequestPath = Path.Combine(
            root,
            "second-proof-request.json");
        var secondRequest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionProofRequest>(
                secondRequestPath,
                "Linked Money crop seed-acquisition request fixture");
        secondRequest.PriorSeedAcquisitionProofPath = string.Empty;
        var missingPriorRequestPath = Path.Combine(
            root,
            "second-missing-prior-proof-request.json");
        Write(missingPriorRequestPath, secondRequest);
        RequireRejects(
            () => MoneyRevenueCropSeedAcquisitionProofBuilder.Build(
                missingPriorRequestPath),
            "A later Money crop seed acquisition without prior-proof lineage was admitted.");
    }

    private static string BuildSeedAcquisitionFixtureProof(
        string root,
        string name,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        List<MoneyRevenueCropNativeLifecycleTransitionRequest> transitions,
        string priorProofPath)
    {
        var manifestPath = Path.Combine(
            root,
            name + "-execution-manifest.json");
        var requestPath = Path.Combine(
            root,
            name + "-proof-request.json");
        var proofPath = Path.Combine(root, name + "-proof.json");
        Write(manifestPath,
            new MoneyRevenueCropSeedAcquisitionExecutionManifest
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                CommitmentId = commitment.CommitmentId,
                SourceStateHash = CurrentTeacherFrontierSupport.Read<
                    SnapshotEnvelope>(
                        transitions[0].BeforeSnapshotPath,
                        "Money crop seed-acquisition fixture source")
                    .StateHash,
                RunId = "run.seed-acquisition.fixture",
                Transitions = transitions.ToArray()
            });
        Write(requestPath,
            new MoneyRevenueCropSeedAcquisitionProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                ExecutionManifestPath = manifestPath,
                PriorSeedAcquisitionProofPath = priorProofPath
            });
        var proof = MoneyRevenueCropSeedAcquisitionProofBuilder.Build(
            requestPath);
        Write(proofPath, proof);
        return proofPath;
    }


}
