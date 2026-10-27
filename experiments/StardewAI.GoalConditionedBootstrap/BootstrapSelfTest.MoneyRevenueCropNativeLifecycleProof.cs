using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyCropNativeLifecycleProof()
    {
        var root = TempArtifactRoot("money-crop-native-lifecycle-proof");
        var snapshot = MoneyCropProductionSnapshot();
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.lifecycle.strawberry",
                SourceDecisionId = "teacher.money.lifecycle.fixture",
                SeedId = "745",
                TileCount = 2,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-18T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money crop lifecycle fixture commitment was rejected.");
        var sourcePath = Path.Combine(root, "source.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var planRequestPath = Path.Combine(root, "plan-request.json");
        var planPath = Path.Combine(root, "plan.json");
        Write(sourcePath, snapshot);
        Write(ledgerPath, mutation.Ledger!);
        Write(planRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = sourcePath,
                StrategyLedgerPath = ledgerPath
            });
        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            planRequestPath);
        Write(planPath, plan);
        var commitment = plan.Commitments.Single();

        var layoutSnapshotPath = Path.Combine(root, "layout-snapshot.json");
        var layoutRequestPath = Path.Combine(root, "layout-request.json");
        var layoutProofPath = Path.Combine(root, "layout-proof.json");
        var layoutSnapshot = MoneyCropPlantingDaySnapshot();
        Write(layoutSnapshotPath, layoutSnapshot);
        Write(layoutRequestPath,
            new MoneyRevenueCropPlantingLayoutProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingSnapshotPath = layoutSnapshotPath,
                LocationId = "Farm",
                Tiles = new[]
                {
                    new MoneyRevenueCropPlantingTile(5, 6),
                    new MoneyRevenueCropPlantingTile(6, 6)
                }
            });
        var layoutProof = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            layoutRequestPath);
        Write(layoutProofPath, layoutProof);

        var capacitySnapshotPaths = new[] { commitment.PlantingTotalDay }
            .Concat(commitment.RequiredWateringTotalDays)
            .Concat(commitment.HarvestWaves.Select(value =>
                value.HarvestTotalDay))
            .Distinct()
            .OrderBy(value => value)
            .Select(totalDay =>
            {
                var path = Path.Combine(
                    root,
                    "capacity-day-" + totalDay + ".json");
                Write(path, MoneyCropDailyCapacitySnapshot(totalDay, 100));
                return path;
            }).ToArray();
        var capacityRequestPath = Path.Combine(root, "capacity-request.json");
        var capacityProofPath = Path.Combine(root, "capacity-proof.json");
        Write(capacityRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = capacitySnapshotPaths
            });
        var capacityProof = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            capacityRequestPath);
        Write(capacityProofPath, capacityProof);
        var settlementCapacityRequestPath = Path.Combine(
            root,
            "settlement-capacity-request.json");
        var settlementCapacityProofPath = Path.Combine(
            root,
            "settlement-capacity-proof.json");
        Write(settlementCapacityRequestPath,
            new MoneyRevenueCropSettlementCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                DailyCapacityProofPath = capacityProofPath
            });
        var settlementCapacity =
            MoneyRevenueCropSettlementCapacityProofBuilder.Build(
                settlementCapacityRequestPath);
        Require(settlementCapacity.Status ==
                    "verified_aggregate_crop_settlement_capacity" &&
                settlementCapacity.SettlementDayCount == 5 &&
                settlementCapacity.Days.All(day =>
                    day.Waves.Length == 1 &&
                    day.MinimumShippingUnits == 2 &&
                    day.RequiredQueueCount == 2 &&
                    day.QueueItemCountPerTransition == 1 &&
                    day.TimeBudgetFits &&
                    day.AllTargetsReachable &&
                    day.ShippingBinReachable) &&
                settlementCapacity.AggregateSettlementCapacityVerified &&
                !settlementCapacity.FormalProductTrainingAuthorized,
            "Money crop aggregate settlement capacity lost its day denominator.");
        Write(settlementCapacityProofPath, settlementCapacity);

        var transitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        var current = layoutSnapshot;
        var sequence = 0;
        var rainOnlyVisitDay = commitment.PlantingTotalDay + 1;
        var finalHarvestDay = commitment.HarvestWaves.Max(value =>
            value.HarvestTotalDay);
        for (var totalDay = commitment.PlantingTotalDay;
             totalDay <= finalHarvestDay;
             totalDay++)
        {
            if (totalDay > commitment.PlantingTotalDay)
            {
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.traverse_connector",
                    routeTargetLocation: "Farm");
            }
            if (totalDay == commitment.PlantingTotalDay)
            {
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.plant_seed");
            }
            if (commitment.RequiredWateringTotalDays.Contains(totalDay) &&
                totalDay != rainOnlyVisitDay)
            {
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.water_crop");
            }
            if (commitment.HarvestWaves.Any(value =>
                    value.HarvestTotalDay == totalDay))
            {
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.harvest_crop");
            }
            if (totalDay < finalHarvestDay)
            {
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.traverse_connector",
                    routeTargetLocation: "FarmHouse");
                current = AddLifecycleTransition(
                    root,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.sleep",
                    rainOnNextDay: totalDay + 1 == rainOnlyVisitDay);
            }
        }

        var requestPath = Path.Combine(root, "lifecycle-request.json");
        Write(requestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = transitions.ToArray()
            });
        var proof = MoneyRevenueCropNativeLifecycleProofBuilder.Build(
            requestPath);
        Require(proof.Status == "verified_native_crop_lifecycle" &&
                proof.TransitionCount == 101 &&
                proof.CropActionQueueCount == 29 &&
                proof.SupportingRouteQueueCount == 48 &&
                proof.CropIngressRouteQueueCount == 24 &&
                proof.SleepIngressRouteQueueCount == 24 &&
                proof.NativeDayRolloverCount == 24 &&
                proof.VerifiedPlantActionCount == 2 &&
                proof.VerifiedWaterActionCount == 46 &&
                proof.VerifiedHarvestActionCount == 10 &&
                proof.NativeCropLifecycleVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop lifecycle proof lost its exact native denominator.");

        var resumeIndex = Enumerable.Range(1, transitions.Count - 1)
            .First(index =>
                transitions[index - 1].TransitionKind ==
                    "native_day_rollover" &&
                transitions[index].TransitionKind ==
                    "supporting_route_to_crop");
        var priorTransition = transitions[resumeIndex - 1];
        var resumedTransitions = transitions.ToArray();
        var resumedPriorAfterPath = Path.Combine(
            root,
            "resume-prior-after.json");
        var resumedPriorReceiptPath = Path.Combine(
            root,
            "resume-prior-receipt.json");
        var resumedPriorAfter = LifecycleChangeRuntimeIdentity(
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                priorTransition.AfterSnapshotPath,
                "Money crop resume prior after snapshot"),
            "prior-runtime-run",
            "prior-runtime-saves");
        var resumedPriorReceipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
                priorTransition.ExecutionReceiptPath,
                "Money crop resume prior execution receipt");
        resumedPriorReceipt.AfterStateHash = resumedPriorAfter.StateHash;
        resumedPriorReceipt.StepResults[^1].AfterStateHash =
            resumedPriorAfter.StateHash;
        Write(resumedPriorAfterPath, resumedPriorAfter);
        Write(resumedPriorReceiptPath, resumedPriorReceipt);
        resumedTransitions[resumeIndex - 1] = priorTransition with
        {
            ExecutionReceiptPath = resumedPriorReceiptPath,
            AfterSnapshotPath = resumedPriorAfterPath
        };
        var resumedRequestPath = Path.Combine(
            root,
            "lifecycle-resumed-request.json");
        Write(resumedRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = resumedTransitions,
                CheckpointResumes = new[]
                {
                    new MoneyRevenueCropNativeLifecycleCheckpointResumeRequest(
                        resumeIndex)
                }
            });
        var resumedProof =
            MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                resumedRequestPath);
        Require(resumedProof.CheckpointResumeCount == 1 &&
                resumedProof.CheckpointResumes.Length == 1 &&
                resumedProof.CheckpointResumes[0].TransitionIndex ==
                    resumeIndex &&
                resumedProof.CheckpointResumes[0].Verified,
            "Money crop lifecycle checkpoint resume was not preserved in proof evidence.");

        var driftedResumeBeforePath = Path.Combine(
            root,
            "lifecycle-resume-inventory-drift-before.json");
        var driftedResumeBefore = CloneSnapshot(
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                transitions[resumeIndex].BeforeSnapshotPath,
                "Money crop resumed before snapshot"));
        var driftedPlayer = JsonNode.Parse(
            driftedResumeBefore.State["player"].GetRawText())!.AsObject();
        driftedPlayer["seed_inventory"]!["value"]![0]!["stack"] = 39;
        driftedResumeBefore.State["player"] =
            JsonSerializer.SerializeToElement(
                driftedPlayer,
                JsonDefaults.Options);
        driftedResumeBefore.StateHash = SnapshotHash.ComputeStateHash(
            driftedResumeBefore.State);
        Write(driftedResumeBeforePath, driftedResumeBefore);
        var driftedResumeTransitions = resumedTransitions.ToArray();
        driftedResumeTransitions[resumeIndex] =
            driftedResumeTransitions[resumeIndex] with
            {
                BeforeSnapshotPath = driftedResumeBeforePath
            };
        var driftedResumeRequestPath = Path.Combine(
            root,
            "lifecycle-resume-inventory-drift-request.json");
        Write(driftedResumeRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = driftedResumeTransitions,
                CheckpointResumes = new[]
                {
                    new MoneyRevenueCropNativeLifecycleCheckpointResumeRequest(
                        resumeIndex)
                }
            });
        RequireRejects(
            () => MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                driftedResumeRequestPath),
            "A Money crop lifecycle resume with inventory drift was admitted.");

        var proofPath = Path.Combine(root, "lifecycle-proof.json");
        var fragmentIndexPath = Path.Combine(root, "fragment-index.json");
        var checkpointRequestPath = Path.Combine(
            root,
            "checkpoint-request.json");
        Write(proofPath, proof);
        var retentionProofPaths = commitment.HarvestWaves.Select(wave =>
        {
            var retentionRequestPath = Path.Combine(
                root,
                "retention-request-" + wave.WaveIndex + ".json");
            var retentionProofPath = Path.Combine(
                root,
                "retention-proof-" + wave.WaveIndex + ".json");
            Write(retentionRequestPath,
                new MoneyRevenueCropOutputRetentionProofRequest
                {
                    CropProductionPlanPath = planPath,
                    NativeLifecycleProofPath = proofPath,
                    CommitmentId = commitment.CommitmentId,
                    WaveIndex = wave.WaveIndex
                });
            var retentionProof =
                MoneyRevenueCropOutputRetentionProofBuilder.Build(
                    retentionRequestPath);
            Require(retentionProof.Status ==
                        "verified_native_crop_output_retention" &&
                    retentionProof.WaveIndex == wave.WaveIndex &&
                    retentionProof.HarvestTotalDay ==
                        wave.HarvestTotalDay &&
                    retentionProof.MinimumUnits == 2 &&
                    retentionProof.InventoryQuantityIncrease == 2 &&
                    retentionProof.HarvestQueueCount == 1 &&
                    retentionProof.HarvestActionCount == 2 &&
                    retentionProof.NativeOutputRetentionVerified &&
                    !retentionProof.FormalProductTrainingAuthorized,
                "Money crop output-retention proof lost its exact wave denominator.");
            Write(retentionProofPath, retentionProof);
            return retentionProofPath;
        }).ToArray();
        var retentionOnlyFragmentIndexPath = Path.Combine(
            root,
            "retention-only-fragment-index.json");
        var retentionOnlyCheckpointRequestPath = Path.Combine(
            root,
            "retention-only-checkpoint-request.json");
        Write(retentionOnlyFragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { capacityProofPath },
                NativeLifecycleProofPaths = new[] { proofPath },
                OutputRetentionProofPaths = retentionProofPaths
            });
        Write(retentionOnlyCheckpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = retentionOnlyFragmentIndexPath
            });
        var retentionOnlyCheckpoint =
            MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                retentionOnlyCheckpointRequestPath);
        Require(retentionOnlyCheckpoint.VerifiedFragmentCount == 3 &&
                retentionOnlyCheckpoint.VerifiedOutputRetentions.Length == 5 &&
                retentionOnlyCheckpoint.VerifiedTerminalSettlements.Length == 0 &&
                commitment.HarvestWaves.All(wave =>
                    retentionOnlyCheckpoint.PendingRequestIds.Contains(
                        "output-retention-terminal:" +
                        wave.OutputRetentionKey,
                        StringComparer.Ordinal)),
            "Retained crop output incorrectly closed terminal settlement requests.");
        var duplicateRetentionPath = Path.Combine(
            root,
            "retention-proof-duplicate.json");
        var duplicateRetentionIndexPath = Path.Combine(
            root,
            "retention-duplicate-fragment-index.json");
        var duplicateRetentionCheckpointRequestPath = Path.Combine(
            root,
            "retention-duplicate-checkpoint-request.json");
        File.Copy(retentionProofPaths[0], duplicateRetentionPath);
        Write(duplicateRetentionIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { capacityProofPath },
                NativeLifecycleProofPaths = new[] { proofPath },
                OutputRetentionProofPaths = retentionProofPaths
                    .Append(duplicateRetentionPath)
                    .ToArray()
            });
        Write(duplicateRetentionCheckpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = duplicateRetentionIndexPath
            });
        RequireRejects(
            () => MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                duplicateRetentionCheckpointRequestPath),
            "Duplicate crop output-retention evidence identity was admitted.");

        var settlementProofPaths = retentionProofPaths.Select(
            (retentionProofPath, index) =>
            {
                var retention = CurrentTeacherFrontierSupport.Read<
                    MoneyRevenueCropOutputRetentionProof>(
                        retentionProofPath,
                        "Money crop output-retention proof fixture");
                var saleBefore = CurrentTeacherFrontierSupport.Read<
                    SnapshotEnvelope>(
                        retention.AfterSnapshotPath,
                        "Money crop terminal sale before fixture");
                var unitPrice = checked((int)(
                    retention.MinimumGrossRevenue / retention.MinimumUnits));
                var saleAfter = LifecycleShopSale(
                    saleBefore,
                    retention.HarvestItemQualifiedId,
                    retention.MinimumUnits,
                    unitPrice);
                var fixtureId = "crop-wave-" + retention.WaveIndex;
                var episodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
                    saleBefore,
                    saleAfter,
                    fixtureId,
                    retention.HarvestItemQualifiedId,
                    slotIndex: 1,
                    quantity: retention.MinimumUnits);
                var recurrenceRoot = Path.GetDirectoryName(episodePath)!;
                var settlementRequestPath = Path.Combine(
                    root,
                    "settlement-request-" + retention.WaveIndex + ".json");
                var settlementProofPath = Path.Combine(
                    root,
                    "settlement-proof-" + retention.WaveIndex + ".json");
                Write(settlementRequestPath,
                    new MoneyRevenueCropTerminalSettlementProofRequest
                    {
                        CropProductionPlanPath = planPath,
                        OutputRetentionProofPath = retentionProofPath,
                        ShopSaleEpisodePath = episodePath,
                        RevenueRecurrenceManifestPath = Path.Combine(
                            recurrenceRoot,
                            "shop-revenue-recurrence-manifest.json"),
                        RevenueRecurrenceReceiptPath = Path.Combine(
                            recurrenceRoot,
                            "shop-revenue-recurrence-receipt.json")
                    });
                var settlement =
                    MoneyRevenueCropTerminalSettlementProofBuilder.Build(
                        settlementRequestPath);
                Require(settlement.Status ==
                            "verified_native_crop_terminal_settlement" &&
                        settlement.WaveIndex == index + 1 &&
                        settlement.SettledQuantity == 2 &&
                        settlement.SettledRevenue ==
                            retention.MinimumGrossRevenue &&
                        settlement.RouteTransitions.Length == 0 &&
                        settlement.NativeTerminalSettlementVerified &&
                        !settlement.FormalProductTrainingAuthorized,
                    "Money crop terminal-settlement proof lost its exact wave binding.");
                Write(settlementProofPath, settlement);
                return settlementProofPath;
            }).ToArray();
        settlementProofPaths[0] =
            VerifyMoneyCropShippingTerminalSettlementProof(
                root,
                planPath,
                retentionProofPaths[0]);
        var firstRetention = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropOutputRetentionProof>(
                retentionProofPaths[0],
                "Money crop first output-retention proof fixture");
        var underSaleBefore = CurrentTeacherFrontierSupport.Read<
            SnapshotEnvelope>(
                firstRetention.AfterSnapshotPath,
                "Money crop under-sale before fixture");
        var underSaleUnitPrice = checked((int)(
            firstRetention.MinimumGrossRevenue /
            firstRetention.MinimumUnits));
        var underSaleAfter = LifecycleShopSale(
            underSaleBefore,
            firstRetention.HarvestItemQualifiedId,
            quantity: 1,
            unitPrice: underSaleUnitPrice);
        var underSaleEpisodePath = VerifyMoneyRevenueShopRecurrenceArtifact(
            underSaleBefore,
            underSaleAfter,
            "crop-wave-under-sale",
            firstRetention.HarvestItemQualifiedId,
            slotIndex: 1,
            quantity: 1);
        var underSaleRoot = Path.GetDirectoryName(underSaleEpisodePath)!;
        var underSaleRequestPath = Path.Combine(
            root,
            "settlement-under-sale-request.json");
        Write(underSaleRequestPath,
            new MoneyRevenueCropTerminalSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                OutputRetentionProofPath = retentionProofPaths[0],
                ShopSaleEpisodePath = underSaleEpisodePath,
                RevenueRecurrenceManifestPath = Path.Combine(
                    underSaleRoot,
                    "shop-revenue-recurrence-manifest.json"),
                RevenueRecurrenceReceiptPath = Path.Combine(
                    underSaleRoot,
                    "shop-revenue-recurrence-receipt.json")
            });
        RequireRejects(
            () => MoneyRevenueCropTerminalSettlementProofBuilder.Build(
                underSaleRequestPath),
            "A crop terminal sale below the conservative wave quantity was admitted.");
        Write(fragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { capacityProofPath },
                NativeLifecycleProofPaths = new[] { proofPath },
                OutputRetentionProofPaths = retentionProofPaths,
                TerminalSettlementProofPaths = settlementProofPaths,
                SettlementCapacityProofPaths = new[]
                {
                    settlementCapacityProofPath
                }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
            checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 8 &&
                checkpoint.VerifiedNativeLifecycles.Length == 1 &&
                checkpoint.VerifiedOutputRetentions.Length == 5 &&
                checkpoint.VerifiedTerminalSettlements.Length == 5 &&
                checkpoint.VerifiedSettlementCapacities.Length == 1 &&
                checkpoint.VerifiedRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                !checkpoint.FutureCropProductionProofReady,
            "Money crop lifecycle proof did not enter the checkpoint denominator.");

        var missingSettlementCapacityIndexPath = Path.Combine(
            root,
            "missing-settlement-capacity-fragment-index.json");
        var missingSettlementCapacityRequestPath = Path.Combine(
            root,
            "missing-settlement-capacity-checkpoint-request.json");
        Write(missingSettlementCapacityIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { capacityProofPath },
                NativeLifecycleProofPaths = new[] { proofPath },
                OutputRetentionProofPaths = retentionProofPaths,
                TerminalSettlementProofPaths = settlementProofPaths
            });
        Write(missingSettlementCapacityRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = missingSettlementCapacityIndexPath
            });
        RequireRejects(
            () => MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                missingSettlementCapacityRequestPath),
            "A native crop shipping settlement without aggregate day capacity was admitted.");

        var missingOutputAfterPath = Path.Combine(
            root,
            "retention-missing-output-after.json");
        var missingOutputReceiptPath = Path.Combine(
            root,
            "retention-missing-output-receipt.json");
        var missingOutputLifecycleRequestPath = Path.Combine(
            root,
            "retention-missing-output-lifecycle-request.json");
        var missingOutputLifecycleProofPath = Path.Combine(
            root,
            "retention-missing-output-lifecycle-proof.json");
        var missingOutputRequestPath = Path.Combine(
            root,
            "retention-missing-output-request.json");
        var lastTransition = transitions[^1];
        var missingOutputAfter = CurrentTeacherFrontierSupport.Read<
            SnapshotEnvelope>(
                lastTransition.AfterSnapshotPath,
                "Money crop missing-output after snapshot");
        var missingOutputPlayer = JsonNode.Parse(
            missingOutputAfter.State["player"].GetRawText())!.AsObject();
        var missingOutputRow = missingOutputPlayer["inventory"]!["value"]!
            .AsArray()
            .Single(value =>
                value?["qualified_item_id"]?.GetValue<string>() ==
                    commitment.HarvestItemQualifiedId &&
                value?["quality"]?.GetValue<int>() == 0)!;
        var missingOutputStack = missingOutputRow["stack"]!;
        missingOutputStack.ReplaceWith(
            missingOutputStack.GetValue<int>() - 2);
        missingOutputAfter.State["player"] =
            JsonSerializer.SerializeToElement(
                missingOutputPlayer,
                JsonDefaults.Options);
        missingOutputAfter.StateHash = SnapshotHash.ComputeStateHash(
            missingOutputAfter.State);
        var missingOutputReceipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
                lastTransition.ExecutionReceiptPath,
                "Money crop missing-output execution receipt");
        missingOutputReceipt.AfterStateHash = missingOutputAfter.StateHash;
        missingOutputReceipt.StepResults[^1].AfterStateHash =
            missingOutputAfter.StateHash;
        Write(missingOutputAfterPath, missingOutputAfter);
        Write(missingOutputReceiptPath, missingOutputReceipt);
        var missingOutputTransitions = transitions.ToArray();
        missingOutputTransitions[^1] = lastTransition with
        {
            ExecutionReceiptPath = missingOutputReceiptPath,
            AfterSnapshotPath = missingOutputAfterPath
        };
        Write(missingOutputLifecycleRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = missingOutputTransitions
            });
        var missingOutputLifecycle =
            MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                missingOutputLifecycleRequestPath);
        Write(missingOutputLifecycleProofPath, missingOutputLifecycle);
        Write(missingOutputRequestPath,
            new MoneyRevenueCropOutputRetentionProofRequest
            {
                CropProductionPlanPath = planPath,
                NativeLifecycleProofPath = missingOutputLifecycleProofPath,
                CommitmentId = commitment.CommitmentId,
                WaveIndex = commitment.HarvestWaves[^1].WaveIndex
            });
        RequireRejects(
            () => MoneyRevenueCropOutputRetentionProofBuilder.Build(
                missingOutputRequestPath),
            "A native crop harvest without retained inventory output was admitted.");

        var incompleteRequestPath = Path.Combine(
            root,
            "lifecycle-incomplete-request.json");
        Write(incompleteRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = transitions.SkipLast(1).ToArray()
            });
        RequireRejects(
            () => MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                incompleteRequestPath),
            "An incomplete Money crop native lifecycle was admitted.");

        var firstSupportingRouteIndex = transitions.FindIndex(value =>
            value.TransitionKind == "supporting_route_to_crop");
        Require(firstSupportingRouteIndex >= 0,
            "Money crop lifecycle fixture did not contain a wake route.");
        var missingWakeRouteRequestPath = Path.Combine(
            root,
            "lifecycle-missing-wake-route-request.json");
        Write(missingWakeRouteRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = capacityProofPath,
                Transitions = transitions.Where((_, index) =>
                    index != firstSupportingRouteIndex).ToArray()
            });
        RequireRejects(
            () => MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                missingWakeRouteRequestPath),
            "A Money crop lifecycle that hid its FarmHouse-to-Farm wake route was admitted.");
    }

    private static SnapshotEnvelope AddLifecycleTransition(
        string root,
        List<MoneyRevenueCropNativeLifecycleTransitionRequest> transitions,
        SnapshotEnvelope before,
        int totalDay,
        ref int sequence,
        string optionId,
        int[]? targetXs = null,
        string routeTargetLocation = "",
        bool rainOnNextDay = false)
    {
        targetXs ??= new[] { 5, 6 };
        var after = optionId switch
        {
            "executor.plant_seed" => LifecyclePlant(before, targetXs),
            "executor.water_crop" => LifecycleWater(before, targetXs),
            "executor.harvest_crop" => LifecycleHarvest(before, targetXs),
            "executor.traverse_connector" => LifecycleRouteToLocation(
                before,
                routeTargetLocation),
            "executor.sleep" => LifecycleSleep(before, rainOnNextDay),
            _ => throw new InvalidDataException(
                "Unknown lifecycle fixture option " + optionId)
        };
        sequence++;
        var prefix = "transition-" + sequence.ToString("D3");
        var beforePath = Path.Combine(root, prefix + "-before.json");
        var queuePath = Path.Combine(root, prefix + "-queue.json");
        var receiptPath = Path.Combine(root, prefix + "-receipt.json");
        var afterPath = Path.Combine(root, prefix + "-after.json");
        var queue = LifecycleQueue(
            prefix,
            optionId,
            before,
            targetXs,
            routeTargetLocation);
        var selectedCandidateId = "candidate." + prefix;
        var runId = "run." + prefix;
        var receipt = LifecycleReceipt(
            queue,
            before,
            after,
            runId,
            selectedCandidateId,
            optionId == "executor.sleep" ? "sleep" :
                queue.Items[0].NormalizedCommand.Steps[0].StepType);
        Write(beforePath, before);
        Write(queuePath, queue);
        Write(receiptPath, receipt);
        Write(afterPath, after);
        transitions.Add(new MoneyRevenueCropNativeLifecycleTransitionRequest(
            optionId switch
            {
                "executor.sleep" => "native_day_rollover",
                "executor.traverse_connector" when
                    routeTargetLocation == "Farm" =>
                        "supporting_route_to_crop",
                "executor.traverse_connector" when
                    routeTargetLocation == "FarmHouse" =>
                        "supporting_route_to_sleep",
                _ => "crop_actions"
            },
            totalDay,
            queuePath,
            beforePath,
            receiptPath,
            afterPath,
            runId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            selectedCandidateId));
        return after;
    }

    private static ActionQueueEnvelope LifecycleQueue(
        string prefix,
        string optionId,
        SnapshotEnvelope before,
        int[] targetXs,
        string routeTargetLocation)
    {
        var stepTypes = optionId switch
        {
            "executor.sleep" => new[]
            {
                "move_to_bed_adjacent",
                "step_onto_sleep_touch_tile",
                "confirm_sleep_yes"
            },
            _ => new[] { optionId["executor.".Length..] }
        };
        var parameters = optionId == "executor.sleep"
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                new SmallModelActionParameter
                {
                    Name = "target_location",
                    Value = optionId == "executor.traverse_connector"
                        ? routeTargetLocation
                        : "Farm"
                },
                new SmallModelActionParameter
                {
                    Name = "target_tile_x",
                    Value = "5"
                },
                new SmallModelActionParameter
                {
                    Name = "target_tile_y",
                    Value = "6"
                }
            }.Concat(optionId == "executor.plant_seed"
                ? new[]
                {
                    new SmallModelActionParameter
                    {
                        Name = "seed_id",
                        Value = "745"
                    }
                }
                : Array.Empty<SmallModelActionParameter>()).ToArray();
        var queueTargetXs = optionId is "executor.sleep" or
            "executor.traverse_connector"
            ? new[] { 5 }
            : targetXs;
        var items = queueTargetXs.Select((targetX, itemIndex) =>
            new ActionQueueItem
            {
                QueueItemId = prefix + ".item." + itemIndex,
                SourceActionId = prefix + ".action." + itemIndex,
                OptionId = optionId,
                NormalizedCommand = new NormalizedCommand
                {
                    OptionId = optionId,
                    StateHash = before.StateHash,
                    Parameters = parameters.Select(parameter =>
                        parameter.Name == "target_tile_x"
                            ? new SmallModelActionParameter
                            {
                                Name = parameter.Name,
                                Value = targetX.ToString()
                            }
                            : parameter).ToArray(),
                    Steps = stepTypes.Select((stepType, index) =>
                        new CompiledActionStep
                        {
                            StepId = prefix + ".item." + itemIndex +
                                ".step." + index,
                            StepType = stepType,
                            Target = "fixture",
                            ExpectedEffect = "fixture_native_transition",
                            EstimatedTicks = 1
                        }).ToArray()
                }
            }).ToArray();
        return new ActionQueueEnvelope
        {
            QueueId = prefix + ".queue",
            SourceModelOutputId = prefix + ".model-output",
            SourceModel = "native-lifecycle-self-test",
            StateHash = before.StateHash,
            GoalId = "earn_money",
            Status = "pending",
            Items = items
        };
    }

    private static QueueExecutionReceiptEnvelope LifecycleReceipt(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string runId,
        string selectedCandidateId,
        string primitiveKind)
    {
        var items = queue.Items;
        var stepResults = items.Select((item, index) =>
        {
            var sourceStateHash = index == 0
                ? before.StateHash
                : queue.QueueId + ".intermediate." + index;
            var afterStateHash = index == items.Length - 1
                ? after.StateHash
                : queue.QueueId + ".intermediate." + (index + 1);
            return new QueueExecutionStepReceipt
            {
                QueueItemIndex = index,
                QueueItemCount = items.Length,
                OriginalPlannedItemCount = items.Length,
                QueueId = queue.QueueId,
                QueueItemId = item.QueueItemId,
                OptionId = item.OptionId,
                SourceStateHash = sourceStateHash,
                CompiledCommandStateHash = before.StateHash,
                SelectedQueueCandidateCompleted = index == items.Length - 1,
                AfterStateHash = afterStateHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick + index,
                AfterGameTick = before.GameTick + index + 1,
                AfterSnapshotFresh = true,
                Status = "applied",
                PrimitiveKind = primitiveKind,
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = new[]
                {
                    "native_crop_lifecycle_transition_observed"
                },
                EffectiveQueueItem = JsonSerializer.SerializeToElement(
                    item,
                    JsonDefaults.Options),
                ChangedFacts = JsonSerializer.SerializeToElement(
                    new[] { "native_crop_lifecycle_state_changed" },
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
            PlannedItemCount = items.Length,
            ExecutedItemCount = items.Length,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = items.Length,
            SelectedCandidateId = selectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = stepResults
        };
    }

    private static SnapshotEnvelope LifecyclePlant(
        SnapshotEnvelope before,
        int[] targetXs)
    {
        var after = LifecycleMutate(before, crops =>
        {
            foreach (var tileX in targetXs)
            {
                crops.Add(LifecycleCropRow(
                    tileX,
                    currentPhase: 0,
                    dayOfPhase: 0,
                    fullyGrown: false,
                    ready: false,
                    daysUntil: 8,
                    watered: false));
            }
        });
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        var stack = player["seed_inventory"]!["value"]![0]!["stack"]!;
        stack.ReplaceWith(stack.GetValue<int>() - targetXs.Length);
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + targetXs.Length;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope LifecycleWater(
        SnapshotEnvelope before,
        int[] targetXs)
    {
        var after = LifecycleMutate(before, crops =>
        {
            foreach (var crop in crops.Where(crop => targetXs.Contains(
                         crop!["tile_x"]!.GetValue<int>())))
                crop!["watered"] = true;
        });
        after.GameTick = before.GameTick + targetXs.Length;
        return after;
    }

    private static SnapshotEnvelope LifecycleHarvest(
        SnapshotEnvelope before,
        int[] targetXs)
    {
        var after = AddLifecycleHarvestOutput(LifecycleMutate(before, crops =>
        {
            foreach (var crop in crops.Where(crop => targetXs.Contains(
                         crop!["tile_x"]!.GetValue<int>())))
            {
                crop!["fully_grown"] = true;
                crop["ready_for_harvest"] = false;
                crop["day_of_current_phase"] = 4;
                crop["days_until_next_harvest_if_watered"] = 4;
            }
        }), targetXs.Length);
        after.GameTick = before.GameTick + targetXs.Length;
        return after;
    }

    private static SnapshotEnvelope AddLifecycleHarvestOutput(
        SnapshotEnvelope after,
        int quantity)
    {
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        var inventory = player["inventory"]!["value"]!.AsArray();
        var matches = inventory.Where(value =>
                value?["qualified_item_id"]?.GetValue<string>() == "(O)400" &&
                value?["quality"]?.GetValue<int>() == 0)
            .ToArray();
        Require(matches.Length <= 1,
            "Money crop lifecycle fixture inventory is ambiguous.");
        if (matches.Length == 0)
        {
            inventory.Add(new JsonObject
            {
                ["slot_index"] = 1,
                ["item_id"] = "400",
                ["qualified_item_id"] = "(O)400",
                ["stack"] = quantity,
                ["quality"] = 0,
                ["sell_to_store_price"] = 120,
                ["maximum_stack_size"] = 999,
                ["is_empty"] = false
            });
        }
        else
        {
            var stack = matches[0]!["stack"]!;
            stack.ReplaceWith(stack.GetValue<int>() + quantity);
        }
        player["inventory"]!["value"] = inventory;
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope LifecycleShopSale(
        SnapshotEnvelope before,
        string qualifiedItemId,
        int quantity,
        int unitPrice)
    {
        var after = CloneSnapshot(before);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        var inventory = player["inventory"]!["value"]!.AsArray();
        var matches = inventory.Where(value =>
                value?["qualified_item_id"]?.GetValue<string>() ==
                    qualifiedItemId &&
                value?["quality"]?.GetValue<int>() == 0)
            .ToArray();
        Require(matches.Length == 1 &&
                matches[0]!["stack"]!.GetValue<int>() >= quantity,
            "Money crop ShopMenu sale fixture lacks its exact inventory stack.");
        var row = matches[0]!;
        var remaining = row["stack"]!.GetValue<int>() - quantity;
        if (remaining == 0)
            inventory.Remove(row);
        else
            row["stack"] = remaining;
        var saleValue = checked(quantity * unitPrice);
        player["inventory"]!["value"] = inventory;
        player["money"]!["value"] =
            checked(player["money"]!["value"]!.GetValue<int>() + saleValue);
        player["total_money_earned"]!["value"] =
            checked(player["total_money_earned"]!["value"]!.GetValue<uint>() +
                (uint)saleValue);
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 1;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope LifecycleSleep(
        SnapshotEnvelope before,
        bool rainOnNextDay = false)
    {
        var after = LifecycleMutate(before, crops =>
        {
            foreach (var value in crops)
            {
                var crop = value!.AsObject();
                var phaseDays = crop["phase_days"]!.AsArray()
                    .Select(item => item!.GetValue<int>()).ToArray();
                var phase = crop["current_phase"]!.GetValue<int>();
                var day = crop["day_of_current_phase"]!.GetValue<int>();
                var fullyGrown = crop["fully_grown"]!.GetValue<bool>();
                if (fullyGrown)
                    day--;
                else
                    day = Math.Min(day + 1, phaseDays[phase]);
                if (day >= phaseDays[phase] && phase < phaseDays.Length - 1)
                {
                    phase++;
                    day = 0;
                }
                while (phase < phaseDays.Length - 1 && phaseDays[phase] <= 0)
                    phase++;
                var ready = (!fullyGrown || day <= 0) &&
                    phase >= phaseDays.Length - 1;
                crop["current_phase"] = phase;
                crop["day_of_current_phase"] = day;
                crop["ready_for_harvest"] = ready;
                crop["days_until_next_harvest_if_watered"] =
                    LifecycleDaysUntilHarvest(
                        phaseDays,
                        phase,
                        day,
                        fullyGrown,
                        ready);
                crop["watered"] = rainOnNextDay;
            }
        }, advanceDay: true);
        return LifecycleSetPlayerLocation(after, "FarmHouse", 0);
    }

    private static SnapshotEnvelope LifecycleRouteToLocation(
        SnapshotEnvelope before,
        string locationId)
    {
        Require(locationId is "Farm" or "FarmHouse",
            "Money crop lifecycle fixture route target is invalid.");
        return LifecycleSetPlayerLocation(before, locationId, 1);
    }

    private static SnapshotEnvelope LifecycleChangeRuntimeIdentity(
        SnapshotEnvelope source,
        string trainingRunId,
        string saveIsolationPath)
    {
        var changed = CloneSnapshot(source);
        var environment = JsonNode.Parse(
            changed.State["environment"].GetRawText())!.AsObject();
        environment["training_run_id"]!["value"] = trainingRunId;
        environment["save_isolation_path"]!["value"] = saveIsolationPath;
        changed.State["environment"] = JsonSerializer.SerializeToElement(
            environment,
            JsonDefaults.Options);
        changed.StateHash = SnapshotHash.ComputeStateHash(changed.State);
        return changed;
    }

    private static SnapshotEnvelope LifecycleSetPlayerLocation(
        SnapshotEnvelope before,
        string locationId,
        int additionalTicks)
    {
        var after = CloneSnapshot(before);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        player["location_id"]!["value"] = locationId;
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.GameTick = checked(before.GameTick + additionalTicks);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static int LifecycleDaysUntilHarvest(
        int[] phaseDays,
        int phase,
        int day,
        bool fullyGrown,
        bool ready)
    {
        if (ready)
            return 0;
        if (fullyGrown)
            return Math.Max(0, day);
        var result = 0;
        for (var index = phase; index < phaseDays.Length - 1; index++)
        {
            result += index == phase
                ? Math.Max(0, phaseDays[index] - day)
                : Math.Max(0, phaseDays[index]);
        }
        return result;
    }

    private static SnapshotEnvelope LifecycleMutate(
        SnapshotEnvelope before,
        Action<JsonArray> mutateCrops,
        bool advanceDay = false)
    {
        var after = CloneSnapshot(before);
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!.AsObject();
        var crops = farm["crops"]!["value"]!.AsArray();
        mutateCrops(crops);
        farm["crops"]!["value"] = crops;
        after.State["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        if (advanceDay)
        {
            var time = JsonNode.Parse(after.State["time"].GetRawText())!
                .AsObject();
            var totalDay = time["total_days"]!["value"]!.GetValue<int>() + 1;
            time["total_days"]!["value"] = totalDay;
            time["year"]!["value"] = totalDay / 112 + 1;
            var dayWithinYear = totalDay % 112;
            var seasons = new[] { "spring", "summer", "fall", "winter" };
            time["season"]!["value"] = seasons[dayWithinYear / 28];
            time["day"]!["value"] = dayWithinYear % 28 + 1;
            after.State["time"] = JsonSerializer.SerializeToElement(
                time,
                JsonDefaults.Options);
        }
        after.GameTick = before.GameTick +
            (advanceDay ? 1 : Math.Max(1, crops.Count));
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static JsonObject LifecycleCropRow(
        int tileX,
        int currentPhase,
        int dayOfPhase,
        bool fullyGrown,
        bool ready,
        int daysUntil,
        bool watered) => new()
    {
        ["location_id"] = "Farm",
        ["tile_x"] = tileX,
        ["tile_y"] = 6,
        ["is_garden_pot"] = false,
        ["harvest_item_id"] = "400",
        ["harvest_item_qualified_id"] = "(O)400",
        ["harvest_item_projection_status"] = "exact",
        ["harvest_outcome_stochastic"] = false,
        ["harvest_outcome_resolved"] = true,
        ["harvest_source_seed_id"] = "745",
        ["phase_days"] = new JsonArray(1, 1, 2, 2, 2, 99999),
        ["current_phase"] = currentPhase,
        ["phase_count"] = 6,
        ["day_of_current_phase"] = dayOfPhase,
        ["dead"] = false,
        ["forage_crop"] = false,
        ["forage_crop_id"] = "0",
        ["fully_grown"] = fullyGrown,
        ["ready_for_harvest"] = ready,
        ["days_until_next_harvest_if_watered"] = daysUntil,
        ["regrow_days"] = 4,
        ["watered"] = watered,
        ["needs_watering"] = !watered
    };
}
