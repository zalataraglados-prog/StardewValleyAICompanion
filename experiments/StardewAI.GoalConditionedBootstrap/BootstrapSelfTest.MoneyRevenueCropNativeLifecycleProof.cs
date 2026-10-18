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

        var transitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        var current = layoutSnapshot;
        var sequence = 0;
        var finalHarvestDay = commitment.HarvestWaves.Max(value =>
            value.HarvestTotalDay);
        for (var totalDay = commitment.PlantingTotalDay;
             totalDay <= finalHarvestDay;
             totalDay++)
        {
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
            if (commitment.RequiredWateringTotalDays.Contains(totalDay))
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
                    "executor.sleep");
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
                proof.TransitionCount == 54 &&
                proof.CropActionQueueCount == 30 &&
                proof.NativeDayRolloverCount == 24 &&
                proof.VerifiedPlantActionCount == 2 &&
                proof.VerifiedWaterActionCount == 48 &&
                proof.VerifiedHarvestActionCount == 10 &&
                proof.NativeCropLifecycleVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop lifecycle proof lost its exact native denominator.");

        var proofPath = Path.Combine(root, "lifecycle-proof.json");
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
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { capacityProofPath },
                NativeLifecycleProofPaths = new[] { proofPath }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
            checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 3 &&
                checkpoint.VerifiedNativeLifecycles.Length == 1 &&
                checkpoint.VerifiedRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                !checkpoint.FutureCropProductionProofReady,
            "Money crop lifecycle proof did not enter the checkpoint denominator.");

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
    }

    private static SnapshotEnvelope AddLifecycleTransition(
        string root,
        List<MoneyRevenueCropNativeLifecycleTransitionRequest> transitions,
        SnapshotEnvelope before,
        int totalDay,
        ref int sequence,
        string optionId)
    {
        var after = optionId switch
        {
            "executor.plant_seed" => LifecyclePlant(before),
            "executor.water_crop" => LifecycleWater(before),
            "executor.harvest_crop" => LifecycleHarvest(before),
            "executor.sleep" => LifecycleSleep(before),
            _ => throw new InvalidDataException(
                "Unknown lifecycle fixture option " + optionId)
        };
        sequence++;
        var prefix = "transition-" + sequence.ToString("D3");
        var beforePath = Path.Combine(root, prefix + "-before.json");
        var queuePath = Path.Combine(root, prefix + "-queue.json");
        var receiptPath = Path.Combine(root, prefix + "-receipt.json");
        var afterPath = Path.Combine(root, prefix + "-after.json");
        var queue = LifecycleQueue(prefix, optionId, before);
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
            optionId == "executor.sleep"
                ? "native_day_rollover"
                : "crop_actions",
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
        SnapshotEnvelope before)
    {
        var stepTypes = optionId == "executor.sleep"
            ? new[]
            {
                "move_to_bed_adjacent",
                "step_onto_sleep_touch_tile",
                "confirm_sleep_yes"
            }
            : new[] { optionId["executor.".Length..] };
        var parameters = optionId == "executor.sleep"
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                new SmallModelActionParameter
                {
                    Name = "target_location",
                    Value = "Farm"
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
        var targetXs = optionId == "executor.sleep"
            ? new[] { 5 }
            : new[] { 5, 6 };
        var items = targetXs.Select((targetX, itemIndex) =>
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

    private static SnapshotEnvelope LifecyclePlant(SnapshotEnvelope before)
    {
        var after = LifecycleMutate(before, crops =>
        {
            foreach (var tileX in new[] { 5, 6 })
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
        stack.ReplaceWith(stack.GetValue<int>() - 2);
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope LifecycleWater(SnapshotEnvelope before) =>
        LifecycleMutate(before, crops =>
        {
            foreach (var crop in crops)
                crop!["watered"] = true;
        });

    private static SnapshotEnvelope LifecycleHarvest(SnapshotEnvelope before) =>
        LifecycleMutate(before, crops =>
        {
            foreach (var crop in crops)
            {
                crop!["fully_grown"] = true;
                crop["ready_for_harvest"] = false;
                crop["day_of_current_phase"] = 4;
                crop["days_until_next_harvest_if_watered"] = 4;
            }
        });

    private static SnapshotEnvelope LifecycleSleep(SnapshotEnvelope before) =>
        LifecycleMutate(before, crops =>
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
                crop["watered"] = false;
            }
        }, advanceDay: true);

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
