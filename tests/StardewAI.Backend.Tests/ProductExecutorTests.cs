using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.ProductExecutor;

namespace StardewAI.Backend.Tests;

public sealed class ProductExecutorTests
{
    [Fact]
    public void ProductCatalogOwnsEveryNativeHarnessDispatchWithoutChangingTrainingEvidence()
    {
        Assert.Equal(
            RuntimeTestHarnessDispatchCatalog.OptionIds.OrderBy(value => value, StringComparer.Ordinal),
            ProductExecutorCapabilityCatalog.OptionIds.OrderBy(value => value, StringComparer.Ordinal));
        Assert.NotEmpty(ProductExecutorCapabilityCatalog.OptionIds);
        var unverified = OptionCapabilityRegistrySource.GetRequired("executor.interact");
        Assert.True(unverified.ProductExecutorSupported);
        Assert.Equal(OptionRuntimeStatus.RegisteredOnly, unverified.RuntimeEvidenceStatus);
        Assert.DoesNotContain(unverified.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void PolicyBindsCapabilityActorRunNonceTimeAndExactSaveRoot()
    {
        var options = Options(requiredRunId: "run-1");
        var policy = new ProductExecutionPolicy(options);
        var request = Request(options, "run-1");

        Assert.Empty(policy.Authorize(request, DateTimeOffset.UtcNow));

        request.Actor = "ai_host.main";
        request.OptionId = "debug.setup_tailoring_fixture";
        request.SaveIsolationPath = Path.Combine(options.AllowedSaveRoot, "nested");
        var reasons = policy.Authorize(request, DateTimeOffset.UtcNow);
        Assert.Contains("actor_execution_mode_mismatch", reasons);
        Assert.Contains("option_not_product_authorized", reasons);
        Assert.Contains("save_root_not_authorized", reasons);
    }

    [Fact]
    public async Task ReceiptStorePersistsPendingFinalAndIdempotentReplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-product-receipt-" + Guid.NewGuid().ToString("N"));
        var options = Options(journalRoot: root);
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(request))!.AsObject();
        var requestHash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var store = new ProductReceiptStore(options);

        await store.WritePendingAsync(request, raw, requestHash, request.BeforeStateHash, 10, default);
        var pending = await store.ReadAsync(request, requestHash, default);
        Assert.Equal(ProductReceiptState.Pending, pending.State);

        var response = new JsonObject { ["status"] = "applied", ["source"] = "product_executor" };
        await store.WriteFinalAsync(request, requestHash, response, default);
        var final = await store.ReadAsync(request, requestHash, default);
        Assert.Equal(ProductReceiptState.Final, final.State);
        Assert.True(final.IdempotentReplay);
        Assert.Equal("applied", final.Response!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProductServiceJournalsLiveSnapshotDriftAndDelegatesActionGuardToNativeExecutor()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-product-service-" + Guid.NewGuid().ToString("N"));
        var options = new ProductExecutorOptions
        {
            JournalRoot = root,
            AllowedSaveRoot = Path.Combine(Path.GetTempPath(), "stardewai-product-saves"),
            BridgeSnapshotUrl = "http://127.0.0.1:9001/snapshot",
            NativeExecutorUrl = "http://127.0.0.1:9002"
        };
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(request))!.AsObject();
        var liveBeforeHash = new string('b', 64);
        var liveAfterHash = new string('c', 64);
        var handler = new ProductHttpHandler(request, liveBeforeHash, liveAfterHash);
        var store = new ProductReceiptStore(options);
        var service = new ProductExecutionService(
            options,
            new ProductExecutionPolicy(options),
            store,
            new HttpClient(handler));

        var result = await service.ExecuteAsync(raw, default);

        Assert.Equal("applied", result["status"]!.GetValue<string>());
        Assert.Equal("verified", result["primitive_verification_status"]!.GetValue<string>());
        Assert.Equal(request.BeforeStateHash, result["product_request_before_state_hash"]!.GetValue<string>());
        Assert.Equal(liveBeforeHash, result["product_before_state_hash"]!.GetValue<string>());
        Assert.Equal(liveAfterHash, result["product_after_state_hash"]!.GetValue<string>());
        Assert.True(result["product_request_state_hash_drift"]!.GetValue<bool>());
        Assert.True(result["product_replan_required"]!.GetValue<bool>());
        Assert.Equal("native_action_preconditions", result["product_dispatch_guard"]!.GetValue<string>());
        Assert.Equal(1, handler.NativeDispatchCount);

        var requestHash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var receipt = await store.ReadAsync(request, requestHash, default);
        Assert.Equal(ProductReceiptState.Final, receipt.State);
        Assert.True(File.Exists(Path.Combine(root, receipt.ReceiptId + ".pending.json.resolved")));

        var replay = await service.ExecuteAsync(raw, default);
        Assert.True(replay["product_idempotent_replay"]!.GetValue<bool>());
        Assert.Equal(
            result["product_receipt_id"]!.GetValue<string>(),
            replay["product_receipt_id"]!.GetValue<string>());
        Assert.Equal(1, handler.NativeDispatchCount);
    }

    [Fact]
    public async Task ProductServiceNeverRedispatchesAnOrphanedPendingReceipt()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-product-pending-" + Guid.NewGuid().ToString("N"));
        var options = new ProductExecutorOptions
        {
            JournalRoot = root,
            AllowedSaveRoot = Path.Combine(Path.GetTempPath(), "stardewai-product-saves"),
            BridgeSnapshotUrl = "http://127.0.0.1:9001/snapshot",
            NativeExecutorUrl = "http://127.0.0.1:9002"
        };
        var request = Request(options, "run-1");
        var raw = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(request))!.AsObject();
        var requestHash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var store = new ProductReceiptStore(options);
        await store.WritePendingAsync(request, raw, requestHash, request.BeforeStateHash, 10, default);
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64));
        var service = new ProductExecutionService(
            options,
            new ProductExecutionPolicy(options),
            store,
            new HttpClient(handler));

        var result = await service.ExecuteAsync(raw, default);

        Assert.Equal("blocked", result["status"]!.GetValue<string>());
        Assert.Equal("product_pending_recovery_blocked", result["primitive_verification_status"]!.GetValue<string>());
        Assert.Contains(
            "native_dispatch_indeterminate_no_replay",
            result["block_reasons"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal(0, handler.NativeDispatchCount);
        Assert.Equal(0, handler.SnapshotReadCount);
    }

    [Fact]
    public async Task ProductServiceCanReplayAnExpiredFinalReceiptWithoutDispatch()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-product-expired-" + Guid.NewGuid().ToString("N"));
        var options = new ProductExecutorOptions
        {
            JournalRoot = root,
            AllowedSaveRoot = Path.Combine(Path.GetTempPath(), "stardewai-product-saves"),
            BridgeSnapshotUrl = "http://127.0.0.1:9001/snapshot",
            NativeExecutorUrl = "http://127.0.0.1:9002",
            MaxRequestAgeSeconds = 1
        };
        var request = Request(options, "run-1");
        request.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        var raw = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(request))!.AsObject();
        var requestHash = ProductReceiptStore.Sha256(raw.ToJsonString());
        var store = new ProductReceiptStore(options);
        await store.WriteFinalAsync(
            request,
            requestHash,
            new JsonObject
            {
                ["status"] = "applied",
                ["product_receipt_id"] = store.ReceiptId(request)
            },
            default);
        var handler = new ProductHttpHandler(request, new string('b', 64), new string('c', 64));
        var service = new ProductExecutionService(
            options,
            new ProductExecutionPolicy(options),
            store,
            new HttpClient(handler));

        var replay = await service.ExecuteAsync(raw, default);

        Assert.Equal("applied", replay["status"]!.GetValue<string>());
        Assert.True(replay["product_idempotent_replay"]!.GetValue<bool>());
        Assert.Equal(0, handler.NativeDispatchCount);
        Assert.Equal(0, handler.SnapshotReadCount);
    }

    [Fact]
    public void FormalTrainingRequiresProductExecutorWhileCalibrationCanUseHarness()
    {
        var harness = LiveTrainingOptions.Parse(new[] { "--skip-training" });
        var product = LiveTrainingOptions.Parse(new[]
        {
            "--use-product-executor",
            "--policy-checkpoint-path", "structured-policy.json",
            "--require-structured-policy",
            "--manifest-path", "training-run-manifest.json"
        });

        Assert.False(harness.UseProductExecutor);
        Assert.Equal("/api/v1/training/execute", harness.ExecutorEndpointPath);
        Assert.True(product.UseProductExecutor);
        Assert.Equal("/api/v1/product/execute", product.ExecutorEndpointPath);
        Assert.Equal("product_executor", product.ExecutorFeedbackSource);
        Assert.Equal(PolicyTrajectoryVersionPins.ProductExecutor, product.PolicyTrajectoryExecutorVersion);
        Assert.Equal(PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, harness.PolicyTrajectoryExecutorVersion);
        Assert.Equal("product_executor_unverified", product.ExecutorUnverifiedSource);
        Assert.Equal("runtime_test_harness_unverified", harness.ExecutorUnverifiedSource);
        harness.ValidateFormalExecutionBoundary();
        product.ValidateFormalExecutionBoundary();

        var unsafeHarness = LiveTrainingOptions.Parse(Array.Empty<string>());
        var harnessError = Assert.Throws<InvalidOperationException>(
            unsafeHarness.ValidateFormalExecutionBoundary);
        Assert.Contains("formal_training_requires_product_executor", harnessError.Message);

        var feedbacklessProduct = LiveTrainingOptions.Parse(new[]
        {
            "--use-product-executor",
            "--no-executor-feedback-required"
        });
        var feedbackError = Assert.Throws<InvalidOperationException>(
            feedbacklessProduct.ValidateFormalExecutionBoundary);
        Assert.Contains("formal_training_requires_executor_feedback", feedbackError.Message);

        var unstructuredProduct = LiveTrainingOptions.Parse(new[] { "--use-product-executor" });
        var structuredError = Assert.Throws<InvalidOperationException>(
            unstructuredProduct.ValidateFormalExecutionBoundary);
        Assert.Contains("formal_training_requires_structured_policy_checkpoint", structuredError.Message);
    }

    private static ProductExecutorOptions Options(
        string? requiredRunId = null,
        string? journalRoot = null) => new()
        {
            JournalRoot = journalRoot ?? Path.Combine(Path.GetTempPath(), "stardewai-product-journal"),
            AllowedSaveRoot = Path.Combine(Path.GetTempPath(), "stardewai-product-saves"),
            RequiredRunId = requiredRunId ?? string.Empty
        };

    private static TrainingExecutionRequest Request(ProductExecutorOptions options, string runId) => new()
    {
        RunId = runId,
        QueueId = "queue-1",
        QueueItemId = "item-1",
        BeforeStateHash = new string('a', 64),
        OptionId = "executor.wait_ticks",
        ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
        Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer).ActorId,
        SaveIsolationPath = options.AllowedSaveRoot,
        RequestNonce = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTimeOffset.UtcNow.ToString("O")
    };

    private sealed class ProductHttpHandler : HttpMessageHandler
    {
        private readonly TrainingExecutionRequest request;
        private readonly Queue<string> snapshotHashes;

        public ProductHttpHandler(
            TrainingExecutionRequest request,
            string beforeHash,
            string afterHash)
        {
            this.request = request;
            snapshotHashes = new Queue<string>(new[] { beforeHash, afterHash });
        }

        public int NativeDispatchCount { get; private set; }
        public int SnapshotReadCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage message,
            CancellationToken cancellationToken)
        {
            JsonObject response;
            if (message.Method == HttpMethod.Get)
            {
                SnapshotReadCount++;
                response = new JsonObject
                {
                    ["schema_version"] = "snapshot.v1",
                    ["state_hash"] = snapshotHashes.Dequeue(),
                    ["game_tick"] = snapshotHashes.Count == 1 ? 20 : 21
                };
            }
            else
            {
                NativeDispatchCount++;
                response = new JsonObject
                {
                    ["schema_version"] = "training_execution_result.v1",
                    ["run_id"] = request.RunId,
                    ["queue_id"] = request.QueueId,
                    ["queue_item_id"] = request.QueueItemId,
                    ["before_state_hash"] = request.BeforeStateHash,
                    ["option_id"] = request.OptionId,
                    ["status"] = "applied",
                    ["feedback_available"] = true,
                    ["primitive_verification_status"] = "verified"
                };
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToJsonString(), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
