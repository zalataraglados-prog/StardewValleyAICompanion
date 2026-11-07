using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyShopReplaySourceBindingBuilder
{
    internal static string VerifySetup(MoneyShopReplaySetupStep[] steps, string runId, string isolationRoot, DateTimeOffset measurementTime)
    {
        var roles = new[] { "time", "held_stock", "alternative_stock", "menu_input_settle" };
        Require(steps is { Length: 4 } && steps.All(row => row is not null) && steps.Select(row => row.Role).SequenceEqual(roles, StringComparer.Ordinal) &&
            steps.SelectMany(row => new[] { Path.GetFullPath(row.RequestPath), Path.GetFullPath(row.ResultPath) }).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 8,
            "Money replay needs the complete ordered declared setup record.");
        var semantic = new List<JsonObject>();
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var previous = DateTimeOffset.MinValue;
        foreach (var step in steps)
        {
            var request = Read<JsonElement>(step.RequestPath);
            var result = Read<JsonElement>(step.ResultPath);
            var option = step.Role switch { "time" => "debug.advance_time_to", "menu_input_settle" => "executor.wait_ticks", _ => "debug.setup_sale_target" };
            Require(Text(request, "schema_version") == "training_execution_request.v1" && Text(request, "run_id") == runId &&
                Text(request, "option_id") == option && Text(request, "execution_mode") == "training_singleplayer" &&
                Text(request, "actor") == "training_farmer.main" && SamePath(Text(request, "save_isolation_path"), isolationRoot) &&
                nonces.Add(Text(request, "request_nonce")) && Text(result, "schema_version") == "training_execution_result.v1" &&
                Text(result, "status") == "applied" && Text(result, "primitive_verification_status") == "verified" &&
                result.GetProperty("feedback_available").GetBoolean() &&
                new[] { "run_id", "queue_id", "queue_item_id", "before_state_hash", "option_id" }.All(name => Text(request, name) == Text(result, name)),
                "Money replay declared setup is not its own applied pre-measurement control.");
            var created = Timestamp(Text(request, "created_at"));
            var completed = Timestamp(Text(result, "completed_at"));
            Require(previous <= created && created <= completed && completed < measurementTime, "Money replay setup occurred out of order or inside measurement.");
            previous = completed;
            var projected = JsonNode.Parse(request.GetRawText())!.AsObject();
            foreach (var name in new[] { "run_id", "queue_id", "queue_item_id", "before_state_hash", "save_isolation_path", "request_nonce", "created_at" })
                projected.Remove(name);
            semantic.Add(projected);
        }
        return SnapshotHash.CanonicalizeSemantic(JsonSerializer.SerializeToElement(semantic));
    }
}
