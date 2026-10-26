using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record CropTeacherControlQueueBuildResult(
        JsonObject? Queue,
        string ModelPlanPath,
        string RankingPath,
        string BeforeSnapshotPath,
        JsonObject BeforeSnapshot,
        string StateHash,
        string SnapshotProfile,
        string SnapshotUrl,
        bool SitePreparationComplete,
        string InterruptKind = "");

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherControlQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            MoneyRevenueCropNativeProductionRunPlan runPlan,
            MoneyRevenueCropNativeProductionRunPlanRequest runPlanRequest,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        if (HasPriorityStoryEventInterrupt(beforeSnapshot))
        {
            return await BuildCropTeacherStoryEventQueueAsync(
                http,
                options,
                iteration,
                beforeSnapshotPath,
                beforeSnapshot,
                stateHash);
        }

        if (CropTeacherControlInterruptClassifier
            .HasSafeSpeakerlessDialogue(beforeSnapshot))
        {
            return await BuildCropTeacherDialogueRecoveryQueueAsync(
                http,
                options,
                iteration,
                beforeSnapshotPath,
                beforeSnapshot,
                stateHash);
        }

        if (step.Stage == CropTeacherRunStageKinds.ExecuteCropBatch &&
            string.Equals(
                step.ActionBatch?.OptionId,
                "executor.harvest_crop",
                StringComparison.Ordinal) &&
            StageOneInventoryCapacityRecovery.HasFullInventory(
                beforeSnapshot))
        {
            var recovery =
                await TryBuildStageOneCollectionTeacherQueueAsync(
                    http,
                    options,
                    iteration,
                    beforeSnapshot.ToJsonString(JsonOptions),
                    beforeSnapshot,
                    stateHash,
                    capacityRecoveryOnly: true,
                    allowObjectiveSafeSingletonCapacityRecovery: true,
                    requireFishingAttemptBlockedForCapacityRecovery: false);
            if (recovery.Queue is null)
            {
                throw new InvalidDataException(
                    "crop_teacher_harvest_inventory_capacity_recovery_" +
                    recovery.Status + ":" + recovery.Detail);
            }

            return StandardResult(
                recovery.Queue,
                recovery.PlanPath,
                recovery.RankingPath,
                recovery.SnapshotPath,
                beforeSnapshot,
                stateHash,
                options,
                interruptKind:
                    "crop_teacher_harvest_inventory_capacity_recovery");
        }

        CropTeacherWateringRefillRecoveryDecision? refillRecovery = null;
        if (step.Stage == CropTeacherRunStageKinds.ExecuteCropBatch &&
            step.ActionBatch?.OptionId == "executor.water_crop")
        {
            refillRecovery =
                CropTeacherWateringRefillRecoveryPlanner.Build(
                step,
                runPlanRequest,
                ReadSnapshotEnvelopeNestedInt(
                    beforeSnapshot,
                    "player",
                    "watering_can_state",
                    "water_left"),
                ReadSnapshotEnvelopeInt(
                    beforeSnapshot,
                    "player",
                    "tile_x"),
                ReadSnapshotEnvelopeInt(
                    beforeSnapshot,
                    "player",
                    "tile_y"));
        }
        if (refillRecovery?.ApproachTile is not null)
        {
            return await BuildCropTeacherWateringRefillApproachQueueAsync(
                http,
                options,
                iteration,
                beforeSnapshotPath,
                beforeSnapshot,
                stateHash,
                refillRecovery.ApproachTile);
        }

        return step.Stage switch
        {
            CropTeacherRunStageKinds.AcquireSeed =>
                await BuildCropTeacherSeedQueueAsync(
                    http,
                    options,
                    iteration,
                    step,
                    beforeSnapshotPath,
                    beforeSnapshot,
                    stateHash),
            CropTeacherRunStageKinds.RouteToFarm or
            CropTeacherRunStageKinds.RouteHome =>
                await BuildCropTeacherRouteQueueAsync(
                    http,
                    options,
                    iteration,
                    step,
                    beforeSnapshotPath,
                    beforeSnapshot,
                    stateHash),
            CropTeacherRunStageKinds.PrepareSite =>
                await BuildCropTeacherSitePreparationQueueAsync(
                    http,
                    options,
                    iteration,
                    step,
                    runPlan,
                    runPlanRequest),
            CropTeacherRunStageKinds.ExecuteCropBatch =>
                await BuildCropTeacherActionBatchQueueAsync(
                    http,
                    options,
                    iteration,
                    step,
                    beforeSnapshotPath,
                    beforeSnapshot,
                    stateHash,
                    refillRecovery?.ExecutableTileCount),
            CropTeacherRunStageKinds.NativeSleep =>
                await BuildCropTeacherSleepQueueAsync(
                    http,
                    options,
                    iteration,
                    beforeSnapshotPath,
                    beforeSnapshot,
                    stateHash),
            _ => throw new InvalidDataException(
                "crop_teacher_control_stage_not_executable:" + step.Stage)
        };
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherStoryEventQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash,
            int? executableTileCount = null)
    {
        var interrupt = await BuildPriorityStoryEventQueueAsync(
            http,
            options,
            stateHash);
        var prefix = CropTeacherArtifactPrefix(
            options,
            iteration,
            "story-event");
        var planPath = prefix + "-daily-plan.json";
        var responsePath = prefix + "-daily-plan-response.json";
        var rankingPath = prefix + "-ranking.json";
        await WriteJsonAsync(planPath, interrupt.Plan);
        await WriteJsonAsync(responsePath, interrupt.Response);
        await WriteJsonAsync(rankingPath, interrupt.Ranking);
        return StandardResult(
            interrupt.Queue,
            planPath,
            rankingPath,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options,
            interruptKind: "priority_story_event");
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherDialogueRecoveryQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        var candidateId = $"crop-dialogue-recovery:{iteration}";
        var plan = JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_plan.v1",
            plan_id = "plan:" + candidateId,
            source_model = "authoritative_crop_native_recovery.v1",
            state_hash = stateHash,
            goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            plan_type = "mechanical_plan",
            steps = new[]
            {
                new
                {
                    step_id = candidateId + ":close",
                    kind = "close_menu",
                    estimated_minutes = 1,
                    preconditions = new[]
                    {
                        "candidate_id:" + candidateId,
                        "menus.active_menu.is_open=true"
                    },
                    expected_effects = new[]
                    {
                        "menus.active_menu.is_open=false"
                    },
                    safety_constraints = new[]
                    {
                        "close_only_verified_supported_menu",
                        "recovery_menu_close"
                    },
                    failure_policy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    parameters = Array.Empty<object>()
                }
            },
            candidate_audit = Array.Empty<object>()
        }, JsonOptions)!.AsObject();
        var prefix = CropTeacherArtifactPrefix(
            options,
            iteration,
            "dialogue-recovery");
        var planPath = prefix + "-model-plan.json";
        var queuePath = prefix + "-compiled-queue.json";
        await WriteJsonAsync(planPath, plan);
        var queue = await PostJsonStringAsync(
            http,
            options.BackendUrl +
                "/api/v1/small-model/plan/action-queue/compile",
            plan.ToJsonString(JsonOptions));
        await WriteJsonAsync(queuePath, queue);
        RequireCompiledQueue(
            queue,
            new[] { "executor.close_menu" },
            expectedItemCount: 1,
            "crop_teacher_dialogue_recovery_compile_invalid");
        return StandardResult(
            queue,
            planPath,
            string.Empty,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options,
            interruptKind: "safe_dialogue_recovery");
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherSeedQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        if (step.RemainingQuantity <= 0 ||
            string.IsNullOrWhiteSpace(step.SeedQualifiedItemId) ||
            string.IsNullOrWhiteSpace(step.ShopId) ||
            step.MaxUnitPrice <= 0)
        {
            throw new InvalidDataException(
                "crop_teacher_seed_control_binding_invalid");
        }

        var parameters = new[]
        {
            Parameter("continuation.option_id", "economy.buy_supplies"),
            Parameter(
                "continuation.qualified_item_id",
                step.SeedQualifiedItemId),
            Parameter("continuation.shop_id", step.ShopId),
            Parameter(
                "continuation.max_unit_price",
                step.MaxUnitPrice.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("continuation.quantity", "1")
        };
        var compiled = await BuildQueueFromDailyPlanAsync(
            http,
            options,
            stateHash,
            Array.Empty<JsonObject>(),
            new[] { "economy.buy_supplies" },
            parameters,
            candidateKindOverride: string.Empty,
            candidateIdOverride: string.Empty,
            maxCandidatesOverride: 1,
            goalIdOverride: "earn_money");
        var prefix = CropTeacherArtifactPrefix(options, iteration, "seed");
        var planPath = prefix + "-daily-plan.json";
        var responsePath = prefix + "-daily-plan-response.json";
        var rankingPath = prefix + "-ranking.json";
        await WriteJsonAsync(planPath, compiled.Plan);
        await WriteJsonAsync(responsePath, compiled.Response);
        await WriteJsonAsync(rankingPath, compiled.Ranking);
        return StandardResult(
            compiled.Queue,
            planPath,
            rankingPath,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options);
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherRouteQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        if (string.IsNullOrWhiteSpace(step.TargetLocationId))
        {
            throw new InvalidDataException(
                "crop_teacher_route_target_missing");
        }
        var candidateId =
            $"crop-route:{step.TotalDay}:{iteration}:{step.TargetLocationId}";
        var plan = JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_plan.v1",
            plan_id = "plan:" + candidateId,
            source_model = "authoritative_crop_native_route.v1",
            state_hash = stateHash,
            goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            plan_type = "mechanical_plan",
            steps = new[]
            {
                new
                {
                    step_id = candidateId + ":move",
                    kind = "move_to_tile",
                    target_location = step.TargetLocationId,
                    target_tile_x = 0,
                    target_tile_y = 0,
                    estimated_minutes = 1,
                    preconditions = new[]
                    {
                        "candidate_id:" + candidateId,
                        "world_ready"
                    },
                    expected_effects = new[]
                    {
                        "player.location_id=" + step.TargetLocationId
                    },
                    safety_constraints = new[]
                    {
                        "transparent_route_graph_only"
                    },
                    failure_policy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    parameters = Array.Empty<object>()
                }
            },
            candidate_audit = Array.Empty<object>()
        }, JsonOptions)!.AsObject();
        var planPath = CropTeacherArtifactPrefix(options, iteration, "route") +
            "-model-plan.json";
        await WriteJsonAsync(planPath, plan);
        var queue = await PostJsonStringAsync(
            http,
            options.BackendUrl +
                "/api/v1/small-model/plan/action-queue/compile",
            plan.ToJsonString(JsonOptions));
        await WriteJsonAsync(
            CropTeacherArtifactPrefix(options, iteration, "route") +
                "-compiled-queue.json",
            queue);
        RequireCropTeacherRouteQueue(queue);
        return StandardResult(
            queue,
            planPath,
            string.Empty,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options);
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherSitePreparationQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            MoneyRevenueCropNativeProductionRunPlan runPlan,
            MoneyRevenueCropNativeProductionRunPlanRequest runPlanRequest)
    {
        var layout = runPlanRequest.LayoutIntents.SingleOrDefault(value =>
            value.CommitmentId == step.CommitmentId) ??
            throw new InvalidDataException(
                "crop_teacher_site_preparation_layout_missing");
        if (layout.Tiles.Length == 0)
        {
            throw new InvalidDataException(
                "crop_teacher_site_preparation_layout_empty");
        }
        var refillPreparationTiles =
            layout.WateringRefillPreparationTiles ??
            Array.Empty<MoneyRevenueCropPlantingTile>();
        var preparationTiles = layout.Tiles
            .Concat(refillPreparationTiles)
            .Distinct()
            .ToArray();
        if (preparationTiles.Length !=
            layout.Tiles.Length + refillPreparationTiles.Length)
        {
            throw new InvalidDataException(
                "crop_teacher_site_preparation_layout_overlap");
        }
        var (minX, maxX) = ExpandSitePreparationBounds(
            preparationTiles.Min(tile => tile.TileX),
            preparationTiles.Max(tile => tile.TileX));
        var (minY, maxY) = ExpandSitePreparationBounds(
            preparationTiles.Min(tile => tile.TileY),
            preparationTiles.Max(tile => tile.TileY));
        var snapshotUrl = PlanningHandoffCapture.BuildFarmLayoutSnapshotUrl(
            options.BridgeSnapshotUrl,
            minX,
            minY,
            maxX,
            maxY);
        var snapshotJson = await http.GetStringAsync(snapshotUrl);
        var snapshot = JsonNode.Parse(snapshotJson)?.AsObject() ??
            throw new InvalidDataException(
                "crop_teacher_site_preparation_snapshot_invalid");
        var snapshotPath = CropTeacherArtifactPrefix(options, iteration, "site") +
            "-snapshot.json";
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        await File.WriteAllTextAsync(
            snapshotPath,
            snapshotJson,
            new UTF8Encoding(false));
        var ingest = await PostJsonStringAsync(
            http,
            SnapshotIngestUrl(options, "farm_layout"),
            snapshotJson);
        var stateHash = ReadString(ingest, "state_hash");
        if (string.IsNullOrWhiteSpace(stateHash) ||
            !string.Equals(
                stateHash,
                ReadString(snapshot, "state_hash"),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "crop_teacher_site_preparation_ingest_drifted");
        }

        var approach = SelectCropTeacherSiteApproach(
            snapshot,
            preparationTiles);
        if (approach is not null)
        {
            return await BuildCropTeacherSiteApproachQueueAsync(
                http,
                options,
                iteration,
                snapshotPath,
                snapshot,
                stateHash,
                snapshotUrl,
                approach);
        }

        var request = new MoneyRevenueCropSitePreparationPlanRequest
        {
            CropProductionPlanPath = runPlan.CropProductionPlanPath,
            CommitmentId = layout.CommitmentId,
            PreparationSnapshotPath = Path.GetFullPath(snapshotPath),
            LocationId = layout.LocationId,
            Tiles = layout.Tiles,
            WateringRefillPreparationTiles = refillPreparationTiles,
            FormalProductTrainingAuthorized = false
        };
        var prefix = CropTeacherArtifactPrefix(options, iteration, "site");
        var requestPath = prefix + "-request.json";
        var planPath = prefix + "-plan.json";
        await WriteTypedJsonAsync(requestPath, request);
        var plan = MoneyRevenueCropSitePreparationPlanBuilder.Build(requestPath);
        await WriteTypedJsonAsync(planPath, plan);
        if (plan.PreparationTotalDay != step.TotalDay ||
            plan.CommitmentId != step.CommitmentId ||
            plan.PreparationStateHash != stateHash ||
            plan.TileCount != preparationTiles.Length ||
            plan.PlantingTileCount != layout.Tiles.Length ||
            plan.WateringRefillPreparationTileCount !=
                refillPreparationTiles.Length ||
            plan.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "crop_teacher_site_preparation_plan_invalid");
        }
        var pending = CropTeacherSitePreparationFrontier
            .SelectNextExecutableStep(plan);
        if (pending is null)
        {
            return new CropTeacherControlQueueBuildResult(
                null,
                planPath,
                string.Empty,
                snapshotPath,
                snapshot,
                stateHash,
                "farm_layout",
                snapshotUrl,
                true);
        }
        if (pending.PlanStatus is not (
                "direct_till_ready" or
                "clear_then_fresh_snapshot_rebind" or
                "clear_access_then_fresh_snapshot_rebind") ||
            pending.ActionOptionId is not (
                "executor.clear_obstacle" or
                "executor.break_farm_resource_clump" or
                "executor.till_soil") ||
            string.IsNullOrWhiteSpace(pending.CandidateId) ||
            pending.Parameters.Any(parameter =>
                parameter.Name == "precondition"))
        {
            throw new InvalidDataException(
                "crop_teacher_site_preparation_step_invalid");
        }
        var parameters = pending.Parameters.Concat(new[]
        {
            Parameter(
                "precondition",
                "candidate_id:" + pending.CandidateId)
        }).ToArray();
        var model = BuildCropTeacherActionModel(
            options,
            stateHash,
            "authoritative_crop_site_preparation_plan.v1",
            pending.CandidateId,
            pending.ActionOptionId,
            parameters);
        var modelPath = prefix + "-model-action.json";
        await WriteJsonAsync(modelPath, model);
        var queue = await PostJsonStringAsync(
            http,
            options.BackendUrl +
                "/api/v1/small-model/action-queue/compile",
            model.ToJsonString(JsonOptions));
        await WriteJsonAsync(
            CropTeacherArtifactPrefix(options, iteration, "batch") +
                "-compile-response.json",
            queue);
        RequireCompiledQueue(
            queue,
            new[] { pending.ActionOptionId },
            1,
            "crop_teacher_site_preparation_compile_invalid");
        return new CropTeacherControlQueueBuildResult(
            queue,
            modelPath,
            string.Empty,
            snapshotPath,
            snapshot,
            stateHash,
            "farm_layout",
            snapshotUrl,
            false);
    }

    private static CropTeacherSiteApproachTile? SelectCropTeacherSiteApproach(
        JsonObject snapshot,
        IReadOnlyCollection<MoneyRevenueCropPlantingTile> preparationTiles)
    {
        var playerX = ReadSnapshotEnvelopeInt(snapshot, "player", "tile_x");
        var playerY = ReadSnapshotEnvelopeInt(snapshot, "player", "tile_y");
        var rows = snapshot["state"]?["current_location"]?
            ["site_preparation_context"]?["value"]?["tiles"] as JsonArray ??
            throw new InvalidDataException(
                "crop_teacher_site_approach_context_missing");
        var contextTiles = rows
            .Select(node => node?.AsObject())
            .Where(row => row is not null)
            .Select(row => new CropTeacherSiteApproachTile(
                ReadJsonInt(row!, "tile_x"),
                ReadJsonInt(row!, "tile_y"),
                ReadJsonBool(row!, "map_passable"),
                ReadJsonBool(row!, "native_blocked_without_characters"),
                ReadJsonBool(row!, "watering_refill_approach_standable")))
            .ToArray();
        return CropTeacherSiteApproachPlanner.SelectApproach(
            playerX,
            playerY,
            preparationTiles.Select(tile => (tile.TileX, tile.TileY)).ToArray(),
            contextTiles);
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherSiteApproachQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string snapshotPath,
            JsonObject snapshot,
            string stateHash,
            string snapshotUrl,
            CropTeacherSiteApproachTile approach)
    {
        return await BuildCropTeacherBoundedApproachQueueAsync(
            http,
            options,
            iteration,
            snapshotPath,
            snapshot,
            stateHash,
            snapshotUrl,
            approach.TileX,
            approach.TileY,
            "site-approach",
            "crop-site-approach",
            "authoritative_crop_site_approach.v1",
            "site_preparation_starts_only_after_approach",
            "farm_layout");
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherWateringRefillApproachQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string snapshotPath,
            JsonObject snapshot,
            string stateHash,
            MoneyRevenueCropPlantingTile approach)
    {
        return await BuildCropTeacherBoundedApproachQueueAsync(
            http,
            options,
            iteration,
            snapshotPath,
            snapshot,
            stateHash,
            options.BridgeSnapshotUrl,
            approach.TileX,
            approach.TileY,
            "watering-refill-approach",
            "crop-watering-refill-approach",
            "authoritative_crop_watering_refill_approach.v1",
            "water_crop_retries_only_after_native_refill_approach",
            options.ExecutionSnapshotProfile,
            "crop_teacher_watering_refill_approach");
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherBoundedApproachQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string snapshotPath,
            JsonObject snapshot,
            string stateHash,
            string snapshotUrl,
            int tileX,
            int tileY,
            string artifactKind,
            string candidateKind,
            string sourceModel,
            string purposeConstraint,
            string snapshotProfile,
            string interruptKind = "")
    {
        var candidateId =
            $"{candidateKind}:{iteration}:{tileX},{tileY}";
        var plan = JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_plan.v1",
            plan_id = "plan:" + candidateId,
            source_model = sourceModel,
            state_hash = stateHash,
            goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            plan_type = "mechanical_plan",
            steps = new[]
            {
                new
                {
                    step_id = candidateId + ":move",
                    kind = "move_to_tile",
                    target_location = "Farm",
                    target_tile_x = tileX,
                    target_tile_y = tileY,
                    estimated_minutes = 12,
                    preconditions = new[]
                    {
                        "candidate_id:" + candidateId,
                        "world_ready"
                    },
                    expected_effects = new[]
                    {
                        $"player.tile={tileX},{tileY}",
                        "fresh_snapshot_replan_required=true"
                    },
                    safety_constraints = new[]
                    {
                        "transparent_current_map_route_only",
                        "bounded_route_repair_clears=4",
                        purposeConstraint
                    },
                    failure_policy = new[]
                    {
                        "refresh_snapshot_and_replan"
                    },
                    parameters = new[]
                    {
                        new
                        {
                            name = "max_route_repair_clears",
                            value = "4"
                        },
                        new
                        {
                            name = "max_route_repair_minutes",
                            value = "12"
                        },
                        new
                        {
                            name = "fresh_snapshot_replan_required",
                            value = "true"
                        }
                    }
                }
            },
            candidate_audit = Array.Empty<object>()
        }, JsonOptions)!.AsObject();
        var prefix = CropTeacherArtifactPrefix(
            options,
            iteration,
            artifactKind);
        var planPath = prefix + "-model-plan.json";
        await WriteJsonAsync(planPath, plan);
        var queue = await PostJsonStringAsync(
            http,
            options.BackendUrl +
                "/api/v1/small-model/plan/action-queue/compile",
            plan.ToJsonString(JsonOptions));
        await WriteJsonAsync(prefix + "-compiled-queue.json", queue);
        RequireCropTeacherBoundedApproachQueue(
            queue,
            artifactKind == "watering-refill-approach"
                ? "crop_teacher_watering_refill_approach_compile_invalid"
                : "crop_teacher_site_approach_compile_invalid");
        return new CropTeacherControlQueueBuildResult(
            queue,
            planPath,
            string.Empty,
            snapshotPath,
            snapshot,
            stateHash,
            snapshotProfile,
            snapshotUrl,
            false,
            interruptKind);
    }

    private static (int Min, int Max) ExpandSitePreparationBounds(
        int min,
        int max)
    {
        const int snapshotChunkSize = 32;
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(
                nameof(max),
                "site preparation bounds are inverted");
        }

        if (max - min + 1 < snapshotChunkSize && min > 0)
        {
            min--;
        }
        if (max - min + 1 < snapshotChunkSize)
        {
            max++;
        }
        return (min, max);
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherActionBatchQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash,
            int? executableTileCount = null)
    {
        var batch = step.ActionBatch ?? throw new InvalidDataException(
            "crop_teacher_action_batch_missing");
        var tiles = executableTileCount.HasValue
            ? batch.Tiles.Take(executableTileCount.Value).ToArray()
            : batch.Tiles;
        if (batch.Tiles.Length is < 1 or > 8 ||
            tiles.Length is < 1 or > 8 ||
            batch.OptionId is not (
                "executor.plant_seed" or
                "executor.water_crop" or
                "executor.harvest_crop"))
        {
            throw new InvalidDataException(
                "crop_teacher_action_batch_invalid");
        }
        var candidateId =
            $"crop:{step.TotalDay}:{batch.CommitmentId}:{batch.BatchIndex}";
        var actions = tiles.Select(tile =>
        {
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("target_tile_x", tile.TileX.ToString()),
                Parameter("target_tile_y", tile.TileY.ToString()),
                Parameter("target_location", batch.LocationId),
                Parameter("action_commitment_id", batch.CommitmentId),
                Parameter("precondition", "candidate_id:" + candidateId)
            };
            if (batch.OptionId == "executor.plant_seed")
            {
                parameters.Add(Parameter(
                    "seed_id",
                    NormalizeQualifiedObjectId(batch.SeedQualifiedItemId)));
            }
            return new
            {
                action_id = $"{candidateId}:{tile.TileX}:{tile.TileY}",
                option_id = batch.OptionId,
                rationale = "authoritative_crop_native_run_plan",
                parameters = parameters.ToArray()
            };
        }).ToArray();
        var model = JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_action.v1",
            model_output_id = "model-output:" + candidateId,
            source_model = "authoritative_crop_native_run_plan.v1",
            state_hash = stateHash,
            goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            actions
        }, JsonOptions)!.AsObject();
        var modelPath = CropTeacherArtifactPrefix(options, iteration, "batch") +
            "-model-action.json";
        await WriteJsonAsync(modelPath, model);
        var queue = await PostJsonStringAsync(
            http,
            options.BackendUrl +
                "/api/v1/small-model/action-queue/compile",
            model.ToJsonString(JsonOptions));
        RequireCompiledQueue(
            queue,
            Enumerable.Repeat(batch.OptionId, tiles.Length).ToArray(),
            tiles.Length,
            "crop_teacher_action_batch_compile_invalid");
        return StandardResult(
            queue,
            modelPath,
            string.Empty,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options);
    }

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherSleepQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        var compiled = await BuildQueueFromDailyPlanAsync(
            http,
            options,
            stateHash,
            Array.Empty<JsonObject>(),
            new[] { "recovery.stabilize_day" },
            new[]
            {
                Parameter(
                    "control_plane.native_save_boundary",
                    "true")
            },
            candidateKindOverride: "recovery_sleep_immediately",
            candidateIdOverride: "recovery:native_save_boundary",
            maxCandidatesOverride: 1);
        var prefix = CropTeacherArtifactPrefix(options, iteration, "sleep");
        var planPath = prefix + "-daily-plan.json";
        var responsePath = prefix + "-daily-plan-response.json";
        var rankingPath = prefix + "-ranking.json";
        await WriteJsonAsync(planPath, compiled.Plan);
        await WriteJsonAsync(responsePath, compiled.Response);
        await WriteJsonAsync(rankingPath, compiled.Ranking);
        return StandardResult(
            compiled.Queue,
            planPath,
            rankingPath,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options);
    }

    private static JsonObject BuildCropTeacherActionModel(
        LiveTrainingOptions options,
        string stateHash,
        string sourceModel,
        string candidateId,
        string optionId,
        SmallModelActionParameter[] parameters) =>
        JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_action.v1",
            model_output_id = "model-output:" + candidateId,
            source_model = sourceModel,
            state_hash = stateHash,
            goal_id = "daily.closed_loop",
            execution_mode = options.TargetExecutionMode,
            actor = options.TargetActor,
            actions = new[]
            {
                new
                {
                    action_id = candidateId + ":action",
                    option_id = optionId,
                    rationale = sourceModel,
                    parameters
                }
            }
        }, JsonOptions)!.AsObject();

    private static CropTeacherControlQueueBuildResult StandardResult(
        JsonObject queue,
        string modelPlanPath,
        string rankingPath,
        string beforeSnapshotPath,
        JsonObject beforeSnapshot,
        string stateHash,
        LiveTrainingOptions options,
        string interruptKind = "") => new(
            queue,
            modelPlanPath,
            rankingPath,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options.ExecutionSnapshotProfile,
            options.BridgeSnapshotUrl,
            false,
            interruptKind);

    private static void RequireCompiledQueue(
        JsonObject queue,
        IReadOnlyList<string> expectedOptionIds,
        int expectedItemCount,
        string error)
    {
        var items = ExecutableQueueItems(queue);
        if (ReadString(queue, "schema_version") != "action_queue.v1" ||
            ReadString(queue, "status") != "pending" ||
            items.Length != expectedItemCount ||
            !items.Select(item => ReadString(item, "option_id"))
                .SequenceEqual(expectedOptionIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(error);
        }
    }

    private static void RequireCropTeacherRouteQueue(JsonObject queue)
    {
        var items = ExecutableQueueItems(queue);
        if (ReadString(queue, "schema_version") != "action_queue.v1" ||
            ReadString(queue, "status") != "pending" ||
            items.Length != 1)
        {
            throw new InvalidDataException("crop_teacher_route_compile_invalid");
        }

        var item = items[0];
        var optionId = ReadString(item, "option_id");
        if (string.Equals(
                optionId,
                "executor.traverse_connector",
                StringComparison.Ordinal))
        {
            return;
        }

        var parameters = item["normalized_command"]?["parameters"] as JsonArray;
        var festivalWait = string.Equals(
                optionId,
                "executor.wait_ticks",
                StringComparison.Ordinal) &&
            parameters?.Any(node =>
            {
                var parameter = node?.AsObject();
                return string.Equals(
                        ReadString(parameter, "name"),
                        "route_wait_reason",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ReadString(parameter, "value"),
                        "recovery_current_connector_active_festival_transit_closed",
                        StringComparison.Ordinal);
            }) == true;
        if (!festivalWait)
        {
            throw new InvalidDataException("crop_teacher_route_compile_invalid");
        }
    }

    private static void RequireCropTeacherBoundedApproachQueue(
        JsonObject queue,
        string error)
    {
        var items = ExecutableQueueItems(queue);
        var optionIds = items
            .Select(item => ReadString(item, "option_id"))
            .ToArray();
        if (ReadString(queue, "schema_version") != "action_queue.v1" ||
            ReadString(queue, "status") != "pending" ||
            items.Length == 0 ||
            items.Length > 9 ||
            optionIds[^1] != "executor.move_to_tile" ||
            optionIds.Any(optionId => optionId is not (
                "executor.move_to_tile" or
                "executor.clear_obstacle")))
        {
            throw new InvalidDataException(error);
        }
    }

    private static int ReadSnapshotEnvelopeInt(
        JsonObject snapshot,
        string section,
        string field) =>
        snapshot["state"]?[section]?[field]?["value"] is JsonValue value &&
        value.TryGetValue<int>(out var result)
            ? result
            : throw new InvalidDataException(
                $"crop_teacher_snapshot_field_unavailable:{section}.{field}");

    private static int ReadSnapshotEnvelopeNestedInt(
        JsonObject snapshot,
        string section,
        string field,
        string nestedField) =>
        snapshot["state"]?[section]?[field]?["value"]?[nestedField] is
            JsonValue value &&
        value.TryGetValue<int>(out var result)
            ? result
            : throw new InvalidDataException(
                "crop_teacher_snapshot_field_unavailable:" +
                $"{section}.{field}.{nestedField}");

    private static int ReadJsonInt(JsonObject value, string property) =>
        value[property] is JsonValue json &&
        json.TryGetValue<int>(out var result)
            ? result
            : int.MinValue;

    private static bool ReadJsonBool(JsonObject value, string property) =>
        value[property] is JsonValue json &&
        json.TryGetValue<bool>(out var result) &&
        result;

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static string CropTeacherArtifactPrefix(
        LiveTrainingOptions options,
        int iteration,
        string stage) => Path.Combine(
            options.SnapshotDir,
            $"crop-teacher-{stage}-{iteration:D4}");

    private static string NormalizeQualifiedObjectId(string value) =>
        value.StartsWith("(O)", StringComparison.Ordinal)
            ? value[3..]
            : value;

    private static async Task WriteJsonAsync(string path, JsonObject value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            value.ToJsonString(JsonOptions),
            new UTF8Encoding(false));
    }

    private static async Task WriteTypedJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions),
            new UTF8Encoding(false));
    }
}
