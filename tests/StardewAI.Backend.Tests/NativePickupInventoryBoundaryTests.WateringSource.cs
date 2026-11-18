using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    public async Task WateringSourceBindsOriginalJournalAndPhysicalResourceFrame(bool product, bool applied, bool drift)
    {
        var f = await WriteWateringSource(product, applied, drift);
        Assert.NotNull(ReadWateringSource(f));
        Assert.True(f.Resources.Read().PersistedToolMetadataVerified);
        if (!applied)
        {
            var error = Assert.Throws<TargetInvocationException>(() => SourceMethod("Read").Invoke(null,
                [f.Queue, f.Result, f.After]));
            Assert.IsType<InvalidDataException>(error.InnerException);
        }
    }

    [Theory]
    [InlineData("option")]
    [InlineData("missing_observation")]
    [InlineData("different_executor")]
    [InlineData("result_status")]
    [InlineData("request_hash")]
    [InlineData("product_before")]
    public async Task WateringSourceDoesNotBroadenExistingNativeAdmission(string fault)
    {
        var f = await WriteWateringSource(product: true, applied: true, drift: false);
        var raw = JsonNode.Parse(File.ReadAllText(f.Result))!.AsObject();
        switch (fault)
        {
            case "option": raw["option_id"] = "executor.till_soil"; break;
            case "missing_observation": raw.Remove("action_origin_observation"); break;
            case "result_status": raw["status"] = "failed"; break;
            case "request_hash": raw["execution_request_sha256"] = new string('f', 64); break;
            case "product_before": raw["product_before_game_tick"] = f.Resources.Stock.After.GameTick + 1; break;
        }
        File.WriteAllText(f.Result, raw.ToJsonString());
        var error = Assert.Throws<TargetInvocationException>(() => ReadWateringSource(f,
            fault == "different_executor" ? PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor : null));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private sealed record WateringSourceFixture(WateringWireFixture Resources, string Queue, string Result, string After, string Executor);

    private static async Task<WateringSourceFixture> WriteWateringSource(bool product, bool applied, bool drift)
    {
        var f = WateringMetadataFixture(applied: applied, water: applied ? 20 : 0, refillOnly: !applied);
        var directory = Path.Combine(Path.GetTempPath(), "stardewai-water-source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var queuePath = Path.Combine(directory, "queue.json"); var beforePath = Path.Combine(directory, "before.json");
        var afterPath = Path.Combine(directory, "after.json"); var resultPath = Path.Combine(directory, "result.json");
        var request = f.Stock.Request; var result = f.Result;
        var actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer);
        request.Actor = actor.ActorId;
        var spec = new OptionRegistry().GetRequired(request.OptionId);
        var command = new NormalizedCommand { StateHash = f.Stock.Before.StateHash, OptionId = request.OptionId,
            ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer, Actor = actor,
            BehaviorCategory = spec.BehaviorCategory, CompilerResponsibility = spec.CompilerResponsibility,
            TrainingRole = spec.TrainingRole, Steps = [new() { StepId = "original-water" }] };
        var queue = new ActionQueueEnvelope { QueueId = request.QueueId, StateHash = f.Stock.Before.StateHash, Actor = actor,
            Items = [new() { QueueItemId = request.QueueItemId, OptionId = request.OptionId, BehaviorCategory = spec.BehaviorCategory,
                CompilerResponsibility = spec.CompilerResponsibility, TrainingRole = spec.TrainingRole, NormalizedCommand = command }] };
        void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
        Save(queuePath, queue); Save(beforePath, f.Stock.Before); Save(afterPath, f.Stock.After);
        var journal = await RuntimeExecutionRequestJournal.WriteAsync(directory, 263, 1, JsonSerializer.Serialize(request),
            "http://test/api/v1/" + (product ? "product" : "training") + "/execute", beforePath);
        result.FeedbackAvailable = true;
        result.BlockReasons = applied ? [] : ["declared_native_refill_control"];
        var raw = JsonSerializer.SerializeToNode(result)!.AsObject();
        raw["execution_request_journal_path"] = journal.JournalPath; raw["execution_request_nonce"] = request.RequestNonce;
        raw["execution_request_sha256"] = journal.RawRequestSha256; raw["effective_compiled_queue_path"] = queuePath;
        raw["after_snapshot_path"] = afterPath; raw["after_state_hash"] = f.Stock.After.StateHash;
        raw["before_game_tick"] = f.Stock.Before.GameTick; raw["after_game_tick"] = f.Stock.After.GameTick;
        raw["after_snapshot_fresh"] = true;
        if (product)
        {
            raw["source"] = "product_executor"; raw["product_executor_schema_version"] = "stardewai.product_executor.v1";
            raw["product_authorization_status"] = "authorized";
            raw["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
            raw["product_request_sha256"] = journal.ProductRequestSha256;
            raw["product_request_before_state_hash"] = request.BeforeStateHash;
            raw["product_before_state_hash"] = drift ? new string('d', 64) : request.BeforeStateHash;
            raw["product_before_game_tick"] = f.Stock.Before.GameTick;
            raw["product_request_state_hash_drift"] = drift; raw["product_dispatch_guard"] = "native_action_preconditions";
            raw["product_replan_required"] = TrainingExecutionVerification.RequiresProductReplan(drift, result.Status, result.PrimitiveVerificationStatus);
            raw["product_idempotent_replay"] = false;
        }
        File.WriteAllText(resultPath, raw.ToJsonString());
        return new(f, queuePath, resultPath, afterPath, product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
    }

    private static object? ReadWateringSource(WateringSourceFixture f, string? executor = null) =>
        SourceMethod("ReadSelectedWateringFrame").Invoke(null, [f.Queue, f.Result, f.After, f.Resources.Stock.Before.StateHash, executor ?? f.Executor]);
    private static MethodInfo SourceMethod(string name) => typeof(NativeOutputObservationBoundary).Assembly
        .GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
}
