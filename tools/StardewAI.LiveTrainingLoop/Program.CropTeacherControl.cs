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
        bool SitePreparationComplete);

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
                    stateHash),
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
        RequireCompiledQueue(
            queue,
            new[] { "executor.traverse_connector" },
            expectedItemCount: 1,
            "crop_teacher_route_compile_invalid");
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
        var minX = preparationTiles.Min(tile => tile.TileX);
        var minY = preparationTiles.Min(tile => tile.TileY);
        var maxX = preparationTiles.Max(tile => tile.TileX);
        var maxY = preparationTiles.Max(tile => tile.TileY);
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
                "clear_then_fresh_snapshot_rebind") ||
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

    private static async Task<CropTeacherControlQueueBuildResult>
        BuildCropTeacherActionBatchQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            CropTeacherRunStep step,
            string beforeSnapshotPath,
            JsonObject beforeSnapshot,
            string stateHash)
    {
        var batch = step.ActionBatch ?? throw new InvalidDataException(
            "crop_teacher_action_batch_missing");
        if (batch.Tiles.Length is < 1 or > 8 ||
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
        var actions = batch.Tiles.Select(tile =>
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
            Enumerable.Repeat(batch.OptionId, batch.Tiles.Length).ToArray(),
            batch.Tiles.Length,
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
        LiveTrainingOptions options) => new(
            queue,
            modelPlanPath,
            rankingPath,
            beforeSnapshotPath,
            beforeSnapshot,
            stateHash,
            options.ExecutionSnapshotProfile,
            options.BridgeSnapshotUrl,
            false);

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
