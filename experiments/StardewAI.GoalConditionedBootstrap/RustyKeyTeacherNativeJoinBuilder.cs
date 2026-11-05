using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyTeacherNativeJoinBuilder
{
    public static RustyKeyTeacherNativeJoin Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<RustyKeyTeacherNativeJoinRequest>(requestPath, "rusty_key_teacher_native_join_request.v1");
        Require(request.Actions is { Length: >= 3 } &&
            request.Actions.Length <= TeacherEvidenceRolloutLimits.MaxQueueItems * TeacherEvidenceRolloutLimits.MaxEpisodes &&
            request.Actions.All(row => row is not null), "Rusty Key join requires a bounded explicit native continuation.");
        var comparisonPath = RequiredFile(request.ComparisonPath);
        var keyPath = RequiredFile(request.NativeKeyReceiptPath);
        var comparison = RustyKeyDonationTeacherComparisonBuilder.Verify(comparisonPath);
        var key = RustyKeyNativeKeyReceiptBuilder.Verify(keyPath);
        Require(comparison.TeacherComparisonVerified && comparison.SaveId == key.SaveId && comparison.PlayerId == key.PlayerId &&
            key.TotalDay == comparison.TotalDay + 1, "Rusty Key comparison and terminal have incompatible identity or dates.");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["comparison"] = CurrentTeacherFrontierSupport.HashFile(comparisonPath),
            ["native_key_receipt"] = CurrentTeacherFrontierSupport.HashFile(keyPath)
        };
        var rows = new List<RustyKeyJoinedNativeAction>();
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var sleepCount = 0;
        string? endpoint = null;
        string? isolation = null;
        string? preferred = null;
        long donationElapsed = 0;
        RustyKeyJoinedNativeAction? previous = null;
        for (var index = 0; index < request.Actions.Length; index++)
        {
            var action = ReadAction(request.Actions[index], key, hashes, index);
            Require(nonces.Add(action.Request.RequestNonce) && identities.Add(action.Queue.QueueId + ":" + action.Item.QueueItemId),
                "Rusty Key continuation repeats a native action or nonce.");
            endpoint ??= action.Endpoint;
            isolation ??= action.Request.SaveIsolationPath;
            Require(action.Endpoint == endpoint && action.Request.SaveIsolationPath == isolation,
                "Rusty Key continuation changed its executor or save isolation.");
            if (previous is not null)
                Require(previous.AfterStateHash == action.Before.StateHash && previous.AfterGameTick == action.Before.GameTick,
                    "Rusty Key continuation has an unrecorded gap or cross-source splice.");
            if (index == 0)
            {
                Require(hashes["action_0.before"] == comparison.SourceSha256["snapshot"],
                    "Rusty Key decision snapshot is not the exact native request source.");
                preferred = ValidateDonation(action, comparison);
                donationElapsed = action.After.GameTick - action.Before.GameTick;
            }
            else
            {
                Require(action.Result.OptionId != "executor.donate_museum_item", "Rusty Key continuation contains another donation.");
                if (action.Result.OptionId == "executor.sleep")
                {
                    ValidateSleep(action, comparison.TotalDay);
                    sleepCount++;
                }
                else Require(TotalDay(action.Before) == TotalDay(action.After), "Rusty Key continuation changes day without native sleep.");
                Require(TotalDay(action.Before) == comparison.TotalDay + sleepCount - (action.Result.OptionId == "executor.sleep" ? 1 : 0) &&
                    TotalDay(action.After) == comparison.TotalDay + sleepCount, "Rusty Key continuation date ordering drifted.");
            }
            var terminal = index == request.Actions.Length - 1;
            ValidateProgress(action.After, terminal);
            Require(terminal == (action.Result.OptionId == "executor.advance_story_event"),
                "Rusty Key event must be the exact terminal native action.");
            var row = new RustyKeyJoinedNativeAction(action.Result.OptionId, action.Queue.QueueId, action.Item.QueueItemId,
                action.Request.RequestNonce, action.Before.StateHash, action.After.StateHash, action.Before.GameTick, action.After.GameTick);
            rows.Add(row);
            previous = row;
        }
        var last = rows[^1];
        var prefix = "action_" + (rows.Count - 1) + ".";
        Require(sleepCount == 1 && last.QueueId == key.QueueId && last.QueueItemId == key.QueueItemId &&
            last.BeforeStateHash == key.BeforeStateHash && last.AfterStateHash == key.AfterStateHash &&
            last.BeforeGameTick == key.BeforeGameTick && last.AfterGameTick == key.AfterGameTick &&
            hashes[prefix + "queue"] == key.SourceSha256["action_queue"] &&
            hashes[prefix + "result"] == key.SourceSha256["execution_receipt"] &&
            hashes[prefix + "before"] == key.SourceSha256["before_snapshot"] &&
            hashes[prefix + "after"] == key.SourceSha256["after_snapshot"],
            "Rusty Key receipt is not the exact terminal of this native continuation.");
        return new(requestPath, hashes, key.RunId, key.SaveId, key.PlayerId, comparison.TotalDay, key.TotalDay,
            comparison.SplitKey, comparison.DatasetPartition, preferred!, donationElapsed, rows.ToArray());
    }

    public static RustyKeyTeacherNativeJoin Verify(string receiptPath)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(RequiredFile(receiptPath), "Rusty Key Teacher/native join");
        Require(raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("request_path", out var request) &&
            request.ValueKind == JsonValueKind.String, "Rusty Key join request path is missing.");
        var rebuilt = Build(raw.GetProperty("request_path").GetString()!);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Rusty Key join drifted from original comparison and native sources.");
        return rebuilt;
    }

}
