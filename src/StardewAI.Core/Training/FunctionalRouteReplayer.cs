using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.NativeExecutionSourceSupport;

namespace StardewAI.Core.Training;

// Functional samples and a rollout's calendar have different clocks. Replay
// the original compiler's resource/calendar checks, without rewriting either.
internal static class FunctionalRouteReplayer
{
    internal const string SchemaVersion = "functional_native_route_evidence.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static int Verify(string path)
    {
        var evidence = Read<FunctionalRouteEvidence>(path);
        Require(evidence.SchemaVersion == SchemaVersion && evidence.NativeActions is { Length: > 0 } &&
            evidence.Snapshot is not null && evidence.CompileRequest is not null && evidence.CompileResponse is not null &&
            evidence.NativeActions.All(action => action is not null && action.Queue is not null && action.Receipt is not null && action.AfterSnapshot is not null &&
                action.BeforeSnapshot is not null && action.RequestJournal is not null && action.RequestGzip is not null) &&
            !string.IsNullOrWhiteSpace(evidence.TargetQualifiedItemId), "Functional route metadata is incomplete.");
        var artifacts = new[] { evidence.Snapshot, evidence.CompileRequest, evidence.CompileResponse }
            .Concat(evidence.NativeActions.SelectMany(action => new[] { action.Queue, action.Receipt, action.AfterSnapshot,
                action.BeforeSnapshot, action.RequestJournal, action.RequestGzip })).ToArray();
        foreach (var artifact in artifacts) VerifyArtifact(artifact!);
        Require(evidence.NativeActions.Select(action => Path.GetFullPath(action.Receipt.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() == evidence.NativeActions.Length,
            "A functional receipt was duplicated.");
        var snapshot = Read<SnapshotEnvelope>(evidence.Snapshot!.Path);
        ValidateSnapshot(snapshot);
        var request = Read<DailyPlanCompileRequest>(evidence.CompileRequest!.Path);
        Require(request.UseJointCalendar && request.CompileActionQueue && !request.PriorityStoryEventInterrupt &&
            request.StateHash == snapshot.StateHash &&
            request.ExecutionMode == ExecutionTargetProfiles.TrainingSingleplayer &&
            request.GoalId == "goal.fishing.complete_master_angler" &&
            (request.RequiredCandidateIds?.Length ?? 0) == 0,
            "Functional selection requires its original explicit joint-calendar request and exact source.");
        var available = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, Array.Empty<string>(), false, null);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), available, request.GoalId);
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(request.RankedEventCandidates, Json),
            JsonSerializer.SerializeToNode(ranked, Json)),
            "Frozen candidates/costs differ from the original complete baseline evaluation.");
        var primary = ranked.Where(row => row.Available && row.OptionId == "fishing.catch_fish")
            .Select(row => row.CandidateId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Require(primary.Length > 0 && request.PrimaryCandidateIds is not null &&
            primary.SequenceEqual(request.PrimaryCandidateIds.OrderBy(id => id, StringComparer.Ordinal)),
            "Functional primary group must retain the original available fishing alternatives.");
        var time = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
        var energy = DailyPlanResourceBudget.JointEnergyBudget(snapshot);
        Require(time.HasValue && energy.HasValue, "Native plan clock or energy is unavailable.");
        var minutes = DailyPlanResourceBudget.AvailablePlanMinutes(time!.Value, request.RankedEventCandidates);
        var compiler = new DailyPlanCompiler();
        var max = request.MaxCandidates <= 0 ? 4 : request.MaxCandidates;
        var required = request.RequiredCandidateIds ?? Array.Empty<string>();
        var goal = string.IsNullOrWhiteSpace(request.GoalId) ? "daily.closed_loop" : request.GoalId;
        var replay = request.PrimaryCandidateIds is not null
            ? compiler.CompileWithPrimaryGoal(request.RankedEventCandidates, snapshot.StateHash, time.Value,
                required, request.PrimaryCandidateIds, goal, request.ExecutionMode, max, minutes, energy)
            : compiler.CompileWithJointCalendar(request.RankedEventCandidates, snapshot.StateHash, time.Value,
                required, goal, request.ExecutionMode, max, minutes, energy);
        var response = Read<JsonElement>(evidence.CompileResponse!.Path);
        Require(response.GetProperty("schema_version").GetString() == "daily_plan_compile_response.v1" &&
            response.GetProperty("plan").ValueKind == JsonValueKind.Object &&
            response.GetProperty("action_queue").ValueKind == JsonValueKind.Object,
            "Functional compile response schema is unsupported.");
        var recorded = response.GetProperty("plan");
        Require(SamePlan(recorded, replay), "Frozen selected plan does not match the original joint compiler replay.");
        var plan = recorded.Deserialize<SmallModelPlanEnvelope>(Json)!;
        var compiled = new ActionQueueCompiler().Compile(plan, snapshot, null);
        var queue = response.GetProperty("action_queue").Deserialize<ActionQueueEnvelope>(Json)!;
        Require(queue is not null && queue.Status == "pending" && queue.Items is { Length: > 0 } &&
            SameQueue(response.GetProperty("action_queue"), compiled),
            "Frozen action queue does not match its original compiler and native source.");
        Require(!plan.Steps.Any(step => step.Kind.Contains("gourmand") || step.Kind.Contains("parrot")) &&
            !queue!.Items.Any(item => item.OptionId == "executor.advance_gourmand_request"),
            "Selected acquisition/Gourmand work requires its original acquisition evidence, not an existing-access functional proof.");

        var frames = evidence.NativeActions.Select(action => NativeExecutionFrameReader.ReadVerifiedProduct(
            action.Queue.Path, action.Receipt.Path, action.AfterSnapshot.Path)).ToArray();
        for (var index = 0; index < frames.Length; index++)
        {
            var frame = frames[index];
            var action = evidence.NativeActions[index];
            var expected = new Dictionary<string, FrozenRouteArtifact>
            {
                ["queue"] = action.Queue, ["result"] = action.Receipt, ["after"] = action.AfterSnapshot,
                ["before"] = action.BeforeSnapshot, ["journal"] = action.RequestJournal, ["request_gzip"] = action.RequestGzip
            };
            Require(frame.SourcePaths.Count == expected.Count && expected.All(pair =>
                frame.SourcePaths.TryGetValue(pair.Key, out var actual) &&
                string.Equals(Path.GetFullPath(actual), Path.GetFullPath(pair.Value.Path), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(frame.SourceSha256[pair.Key], pair.Value.Sha256, StringComparison.OrdinalIgnoreCase)),
                "Native frame dependencies differ from the frozen functional proof.");
        }
        var save = Identity(snapshot.SaveId);
        var player = Identity(snapshot.PlayerId);
        var run = Value(snapshot, "environment", "training_run_id").GetString();
        Require(frames.All(frame => frame.Result.ChangedFacts is not null && frame.Result.ChangedFacts.All(fact => fact is not null) &&
            Identity(frame.Before.SaveId) == save && Identity(frame.Before.PlayerId) == player &&
            frame.Result.RunId == run && frame.Before.GameVersion == snapshot.GameVersion),
            "Functional native sources do not bind the selected scenario identity/version.");
        var catches = frames.Where(frame => frame.Result.OptionId == "executor.catch_fish" &&
            frame.Before.StateHash == snapshot.StateHash && queue!.Items.Any(item => item.OptionId == frame.Item.OptionId &&
                SameFunctionalCommand(item.NormalizedCommand, frame.Item.NormalizedCommand)) &&
            frame.Result.ChangedFacts.Any(fact => fact.Path == "fishing.caught_qualified_item_id" &&
                fact.After == evidence.TargetQualifiedItemId)).ToArray();
        Require(catches.Length == 1, "Selected native catch does not bind the exact planned command and source.");
        var caught = catches[0];
        var location = Value(caught.Before, "player", "location_id").GetString();
        var accessKey = location switch
        {
            "IslandSouth" => "island_south_accessible", "IslandNorth" => "island_north_accessible",
            "IslandWest" => "island_west_accessible", "IslandSouthEast" => "island_south_east_accessible",
            "IslandSouthEastCave" => "island_south_east_cave_accessible", _ => string.Empty
        };
        var access = Value(caught.Before, "locations", "ginger_island_access");
        Require(accessKey.Length > 0 && access.GetProperty("schema_version").GetString() == "ginger_island_access.v1" &&
            access.GetProperty("projection_status").GetString() == "complete_locked_base_1.6.15_native_contract" &&
            access.GetProperty("target_access").GetProperty(accessKey).ValueKind == JsonValueKind.True,
            "The selected functional fish area was not already accessible.");
        Require(frames.All(frame => Wallet(frame.After) >= Wallet(frame.Before)),
            "A new walnut payment cannot be credited by this existing-access functional branch.");
        var domain = caught.Result.ChangedFacts.Single(fact => fact.Path == "fishing.planned_outcome_distribution_json");
        using var outcomes = JsonDocument.Parse(domain.After);
        Require(outcomes.RootElement.EnumerateArray().Any(row => row.GetProperty("qualified_item_id").GetString() == evidence.TargetQualifiedItemId),
            "Native catch is outside the compiled stochastic outcome domain.");
        Require(!Caught(caught.Before, evidence.TargetQualifiedItemId) && Caught(caught.After, evidence.TargetQualifiedItemId),
            "The selected native fish did not make its recorded collection transition.");
        foreach (var artifact in artifacts) VerifyArtifact(artifact!);
        foreach (var frame in frames)
            foreach (var source in frame.SourcePaths)
                Require(string.Equals(NativeFrameArtifacts.HashFile(source.Value), frame.SourceSha256[source.Key], StringComparison.OrdinalIgnoreCase),
                    "A native source changed during functional replay.");
        return frames.Length;
    }

    private static int Wallet(SnapshotEnvelope snapshot) => Value(snapshot, "world_progress", "golden_walnuts").GetProperty("current").GetInt32();
    private static bool Caught(SnapshotEnvelope snapshot, string id)
    {
        var rows = Value(snapshot, "world_progress", "fish_collection_progress").GetProperty("items").EnumerateArray()
            .Where(row => row.GetProperty("qualified_item_id").GetString() == id).ToArray();
        Require(rows.Length == 1 && rows[0].GetProperty("caught").ValueKind is JsonValueKind.False or JsonValueKind.True,
            "Functional fish collection identity is ambiguous.");
        return rows[0].GetProperty("caught").GetBoolean();
    }

    internal static void VerifyArtifact(FrozenRouteArtifact artifact)
    {
        Require(artifact is not null && Path.IsPathFullyQualified(artifact.Path) && artifact.Sha256 is { Length: 64 } &&
            artifact.Sha256.All(Uri.IsHexDigit), "Functional artifact reference is incomplete.");
        Require((File.GetAttributes(artifact!.Path) & FileAttributes.ReparsePoint) == 0 &&
            string.Equals(NativeFrameArtifacts.HashFile(artifact.Path), artifact.Sha256, StringComparison.OrdinalIgnoreCase),
            "Functional source artifact hash/identity changed: " + artifact.Path);
    }

    private static T Read<T>(string path) => NativeFrameArtifacts.Read<T>(path, "Functional route");
    private static JsonNode WithoutGeneratedStepIds(JsonNode node)
    {
        if (node["steps"] is JsonArray steps)
            foreach (var step in steps) step!.AsObject().Remove("step_id");
        return node;
    }
    // Method evidence may be captured alone and consumed in a longer plan.
    // Actuator fields still match exactly. Only regenerated IDs and planning
    // annotations independently checked by the joint compiler are excluded.
    internal static bool SameFunctionalCommand(NormalizedCommand? left, NormalizedCommand? right)
    {
        if (left is null || right is null || left.Parameters is null || right.Parameters is null ||
            left.Steps is null || right.Steps is null) return false;
        JsonNode Function(NormalizedCommand command)
        {
            var node = WithoutGeneratedStepIds(JsonSerializer.SerializeToNode(command, Json)!);
            var parameters = node["parameters"]!.AsArray();
            var budgetConstraints = new[] { "daily_plan_time_budget_checked", "daily_plan_energy_budget_checked", "daily_plan_joint_calendar_checked" };
            for (var index = parameters.Count - 1; index >= 0; index--)
            {
                var name = parameters[index]!["name"]!.GetValue<string>();
                var value = parameters[index]!["value"]!.GetValue<string>();
                if (name.StartsWith("budget.", StringComparison.Ordinal) ||
                    name is "compiler_context.is_terminal_step" or "compiler_context.plan_step_count" ||
                    name == "safety_constraint" && budgetConstraints.Contains(value)) parameters.RemoveAt(index);
            }
            return node;
        }
        return JsonNode.DeepEquals(Function(left), Function(right));
    }
    private static bool SamePlan(JsonElement recorded, SmallModelPlanEnvelope rebuilt)
    {
        var actual = JsonNode.Parse(recorded.GetRawText())!.AsObject();
        var expected = JsonSerializer.SerializeToNode(rebuilt, Json)!.AsObject();
        actual.Remove("plan_id"); expected.Remove("plan_id");
        return JsonNode.DeepEquals(actual, expected);
    }
    private static bool SameQueue(JsonElement recorded, ActionQueueEnvelope rebuilt)
    {
        static JsonNode WithoutGeneratedIds(JsonNode node)
        {
            var root = node.AsObject(); root.Remove("queue_id");
            foreach (var item in root["items"]!.AsArray())
            {
                item!.AsObject().Remove("queue_item_id");
                WithoutGeneratedStepIds(item["normalized_command"]!);
            }
            return root;
        }
        return JsonNode.DeepEquals(WithoutGeneratedIds(JsonNode.Parse(recorded.GetRawText())!),
            WithoutGeneratedIds(JsonSerializer.SerializeToNode(rebuilt, Json)!));
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }
}
