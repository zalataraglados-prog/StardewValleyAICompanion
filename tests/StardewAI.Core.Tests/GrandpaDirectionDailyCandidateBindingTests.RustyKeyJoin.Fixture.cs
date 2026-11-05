using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    private sealed record NativeJoinFixture(string Root, string RequestPath, RustyKeyTeacherNativeJoinRequest Request,
        string[] BeforePaths, string[] JournalPaths);

    private static NativeJoinFixture CreateNativeJoinFixture(bool routeWait = false)
    {
        var key = CreateNativeKeyReceiptFixture();
        var donor = MuseumDonationMainlineTests.CreateDonationTeacherFixture();
        EditNativeKeySnapshot(donor.SnapshotPath, node =>
        {
            var state = node["state"]!;
            state["player"]!["has_rusty_key"] = RustyKeyField(false);
            state["player"]!["inventory"]!["value"]![0]!["quality"] = 0;
            state["world_progress"]!["museum"]!["value"]!["donatable_items"] = JsonSerializer.SerializeToNode(
                new[] { new { qualified_item_id = "(O)96", donated = false } });
        });
        var before = JsonNode.Parse(File.ReadAllText(donor.SnapshotPath))!;
        var donated = before.DeepClone();
        donated["game_tick"] = 20;
        var museum = donated["state"]!["world_progress"]!["museum"]!["value"]!;
        museum["donated_count"] = 60;
        museum["rusty_key_reward_claimed"] = true;
        museum["rusty_key_prerequisite_event_seen"] = true;
        museum["donatable_items"]![0]!["donated"] = true;
        donated["state"]!["player"]!["inventory"]!["value"]![0]!["stack"] = 1;
        var donatedPath = Path.Combine(key.Root, "donated.json");
        WriteJoinSnapshot(donatedPath, donated);
        var morning = donated.DeepClone();
        morning["game_tick"] = 60;
        morning["state"]!["time"]!["time"]!["value"] = 600;
        morning["state"]!["time"]!["total_days"]!["value"] = 7;
        morning["state"]!["player"]!["location_id"]!["value"] = "FarmHouse";
        var morningPath = Path.Combine(key.Root, "morning.json");
        WriteJoinSnapshot(morningPath, morning);
        foreach (var path in new[] { key.Request.BeforeSnapshotPath, key.Request.AfterSnapshotPath })
            EditNativeKeySnapshot(path, node =>
            {
                node["save_id"]!["value"] = "MuseumFarm";
                node["player_id"]!["value"] = "123";
                node["bridge_version"] = "test.v1";
                node["state"]!["identity"] = before["state"]!["identity"]!.DeepClone();
                node["state"]!["player"]!["inventory"] = donated["state"]!["player"]!["inventory"]!.DeepClone();
            });
        var keyBefore = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(key.Request.BeforeSnapshotPath), JsonOptions)!;
        var binding = BindRustyKey(keyBefore, RankRustyKey(keyBefore, "story.advance_event"));
        var keyQueue = new ActionQueueCompiler().Compile(new DailyPlanCompiler().Compile(binding.BoundCandidates,
            keyBefore.StateHash, "grandpa.maximum_21", maxCandidates: 1), keyBefore);
        WriteNativeKeyJson(key.Request.ActionQueuePath, keyQueue);
        EditNativeKeyJson(key.Request.ExecutionReceiptPath, node =>
        {
            node["queue_id"] = keyQueue.QueueId;
            node["queue_item_id"] = keyQueue.Items[0].QueueItemId;
            node["before_state_hash"] = keyBefore.StateHash;
            var after = JsonNode.Parse(File.ReadAllText(key.Request.AfterSnapshotPath))!;
            node["after_state_hash"] = after["state_hash"]!.DeepClone();
        });
        var comparisonPath = Path.Combine(key.Root, "comparison.json");
        var comparison = RustyKeyDonationTeacherComparisonBuilder.Build(donor.RequestPath);
        WriteNativeKeyJson(comparisonPath, comparison);
        var donationQueue = comparison.CandidateEvaluations[0].CompiledQueue!;
        var donationQueuePath = Path.Combine(key.Root, "donation-queue.json");
        WriteNativeKeyJson(donationQueuePath, donationQueue);
        var sleepQueuePath = WriteJoinQueue(key.Root, "sleep", donatedPath);
        var routeBeforePath = morningPath;
        string? waitQueuePath = null;
        string? waitAfterPath = null;
        if (routeWait)
        {
            var waitAfter = morning.DeepClone();
            waitAfter["game_tick"] = 70;
            waitAfterPath = Path.Combine(key.Root, "route-wait-after.json");
            WriteJoinSnapshot(waitAfterPath, waitAfter);
            waitQueuePath = WriteJoinQueue(key.Root, "wait_ticks", morningPath);
            EditNativeKeyJson(waitQueuePath, queue => queue["items"]![0]!["normalized_command"]!["parameters"] =
                JsonSerializer.SerializeToNode(new[] { Parameter("wait_ticks", "10"),
                    Parameter("route_wait_until_time", "600"), Parameter("route_wait_max_slices", "1") }, JsonOptions));
            routeBeforePath = waitAfterPath;
        }
        var routeQueuePath = WriteJoinQueue(key.Root, "traverse_connector", routeBeforePath);
        var sourceList = new List<RustyKeyNativeActionSource>
        {
            new RustyKeyNativeActionSource(donationQueuePath, Path.Combine(key.Root, "donation-result.json"), donatedPath),
            new RustyKeyNativeActionSource(sleepQueuePath, Path.Combine(key.Root, "sleep-result.json"), morningPath)
        };
        var beforeList = new List<string> { donor.SnapshotPath, donatedPath };
        if (routeWait)
        {
            sourceList.Add(new(waitQueuePath!, Path.Combine(key.Root, "route-wait-result.json"), waitAfterPath!));
            beforeList.Add(morningPath);
        }
        sourceList.Add(new(routeQueuePath, Path.Combine(key.Root, "route-result.json"), key.Request.BeforeSnapshotPath));
        sourceList.Add(new(key.Request.ActionQueuePath, key.Request.ExecutionReceiptPath, key.Request.AfterSnapshotPath));
        beforeList.Add(routeBeforePath);
        beforeList.Add(key.Request.BeforeSnapshotPath);
        var sources = sourceList.ToArray();
        var beforePaths = beforeList.ToArray();
        var journals = new string[sources.Length];
        for (var index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(source.ActionQueuePath), JsonOptions)!;
            var actionBefore = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(beforePaths[index]), JsonOptions)!;
            var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(source.AfterSnapshotPath), JsonOptions)!;
            var kind = queue.Items[0].OptionId["executor.".Length..];
            var raw = index == sources.Length - 1 ? JsonNode.Parse(File.ReadAllText(source.ExecutionReceiptPath))! :
                JsonSerializer.SerializeToNode(new TrainingExecutionResult
                {
                    RunId = key.Request.RunId, QueueId = queue.QueueId, QueueItemId = queue.Items[0].QueueItemId,
                    BeforeStateHash = actionBefore.StateHash, OptionId = queue.Items[0].OptionId, Status = "applied",
                    FeedbackAvailable = true, PrimitiveKind = kind, PrimitiveVerificationStatus = "verified"
                }, JsonOptions)!;
            if (index == 0) raw["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(new[]
                { "LibraryMuseum.OpenDonationMenu_completed", "MuseumMenu.receiveLeftClick_inventory_completed",
                    "MuseumMenu.receiveLeftClick_display_completed", "Game1.exitActiveMenu_reward_settlement_completed" });
            if (index == 1)
            {
                raw["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(new[]
                    { "sleep_yes_confirmed", "new_day_observed", "native_save_committed", "post_sleep_menu_closed", "native_new_day_world_stable" });
                raw["end_of_night_transaction_id"] = "fixture-night";
                raw["end_of_night_transaction_status"] = "ready_for_safe_next_day_action";
                raw["end_of_night_transaction_evidence_path"] = "retained-night-not-a-global-handoff-claim.json";
                raw["changed_facts"] = JsonSerializer.SerializeToNode(new[] { new SimulatedFactChange
                    { Path = "time.total_days", Before = "6", After = "7" } }, JsonOptions);
            }
            if (kind == "traverse_connector") raw["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(new[] { "connector_location_changed_as_expected" });
            if (kind == "wait_ticks") raw["primitive_verification_reasons"] = JsonSerializer.SerializeToNode(new[] { "wait_until_time_reached" });
            raw["before_game_tick"] = actionBefore.GameTick;
            raw["after_game_tick"] = after.GameTick;
            raw["after_state_hash"] = after.StateHash;
            raw["after_snapshot_fresh"] = true;
            raw["after_snapshot_path"] = source.AfterSnapshotPath;
            raw["mechanical_compiled_queue_path"] = source.ActionQueuePath;
            journals[index] = Path.Combine(key.Root, "journal-" + index + ".json");
            WriteJoinJournal(journals[index], new TrainingExecutionRequest
            {
                RunId = key.Request.RunId, QueueId = queue.QueueId, QueueItemId = queue.Items[0].QueueItemId,
                BeforeStateHash = actionBefore.StateHash, OptionId = queue.Items[0].OptionId,
                SaveIsolationPath = Path.Combine(key.Root, "isolated-saves"), RequestNonce = Guid.NewGuid().ToString("N"),
                WaitUntilTime = kind == "wait_ticks" ? 600 : null, WaitMaxSlices = kind == "wait_ticks" ? 1 : null
            }, beforePaths[index], payload =>
            {
                foreach (var parameter in queue.Items[0].NormalizedCommand.Parameters)
                {
                    var property = typeof(TrainingExecutionRequest).GetProperties().SingleOrDefault(value =>
                        value.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == parameter.Name);
                    if (property is null) continue;
                    var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    if (type == typeof(string) || type == typeof(int) || type == typeof(bool))
                        payload[parameter.Name] = JsonSerializer.SerializeToNode(Convert.ChangeType(parameter.Value, type, CultureInfo.InvariantCulture));
                }
            });
            var journal = ExecutionRequestJournalReader.Read(journals[index]);
            raw["execution_request_journal_path"] = journals[index];
            raw["execution_request_sha256"] = journal.Metadata.GetProperty("raw_request_sha256").GetString();
            raw["execution_request_nonce"] = journal.Request.RequestNonce;
            WriteNativeKeyJson(source.ExecutionReceiptPath, raw);
        }
        var keyReceiptPath = Path.Combine(key.Root, "native-key.json");
        WriteNativeKeyJson(keyReceiptPath, RustyKeyNativeKeyReceiptBuilder.Build(key.RequestPath));
        var request = new RustyKeyTeacherNativeJoinRequest
            { ComparisonPath = comparisonPath, NativeKeyReceiptPath = keyReceiptPath, Actions = sources };
        var requestPath = Path.Combine(key.Root, "join-request.json");
        WriteNativeKeyJson(requestPath, request);
        return new(key.Root, requestPath, request, beforePaths, journals);
    }

    private static void WriteJoinSnapshot(string path, JsonNode raw)
    {
        raw["state_hash"] = SnapshotHash.ComputeStateHash(raw["state"]!.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!);
        WriteNativeKeyJson(path, raw);
    }

    private static string WriteJoinQueue(string root, string kind, string beforePath)
    {
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(beforePath), JsonOptions)!;
        var actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer);
        var spec = new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired("executor." + kind);
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue-" + kind, GoalId = "grandpa.maximum_21", StateHash = before.StateHash, Status = "pending", Actor = actor,
            Items = new[] { new ActionQueueItem
            {
                QueueItemId = "item-" + kind, OptionId = "executor." + kind, Status = "pending",
                BehaviorCategory = spec.BehaviorCategory, CompilerResponsibility = spec.CompilerResponsibility, TrainingRole = spec.TrainingRole,
                NormalizedCommand = new NormalizedCommand
                {
                    OptionId = "executor." + kind, StateHash = before.StateHash, GoalId = "grandpa.maximum_21", Actor = actor,
                    BehaviorCategory = spec.BehaviorCategory, CompilerResponsibility = spec.CompilerResponsibility, TrainingRole = spec.TrainingRole,
                    Steps = new[] { new CompiledActionStep { StepId = "step-" + kind, StepType = kind, EstimatedTicks = 1 } }
                }
            } }
        };
        var path = Path.Combine(root, kind + "-queue.json");
        WriteNativeKeyJson(path, queue);
        return path;
    }

    private static void WriteJoinJournal(string path, TrainingExecutionRequest request, string beforePath, Action<JsonObject>? editPayload = null)
    {
        var payload = JsonSerializer.SerializeToNode(request, JsonOptions)!.AsObject();
        editPayload?.Invoke(payload);
        var raw = payload.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(raw);
        using (var file = File.Create(path + ".gz"))
        using (var gzip = new GZipStream(file, CompressionMode.Compress)) gzip.Write(bytes);
        string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
        WriteNativeKeyJson(path, new
        {
            schema_version = ExecutionRequestJournalReader.SchemaVersion, blob_file_name = Path.GetFileName(path) + ".gz",
            request_bytes = bytes.Length, raw_request_sha256 = Hash(bytes),
            product_request_sha256 = Hash(Encoding.UTF8.GetBytes(JsonNode.Parse(raw)!.ToJsonString())),
            run_id = request.RunId, queue_id = request.QueueId, queue_item_id = request.QueueItemId,
            request_nonce = request.RequestNonce, option_id = request.OptionId,
            source_state_hash = request.BeforeStateHash, before_snapshot_path = beforePath,
            executor_url = "http://127.0.0.1:8767/api/v1/training/execute"
        });
    }
}
