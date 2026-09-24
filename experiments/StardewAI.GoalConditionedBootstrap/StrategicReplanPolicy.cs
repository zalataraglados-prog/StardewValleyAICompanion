using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class StrategicReplanPolicy
{
    private static readonly HashSet<string> AllowedTriggers = new(
        StrategicReplanTriggers.All,
        StringComparer.Ordinal);

    public static StrategicReplanEvaluation Evaluate(
        StrategicReplanContext context,
        string goalId,
        string snapshotStateHash,
        int ledgerRevision)
    {
        if (context.SchemaVersion != StrategicPolicyVersionPins.ReplanSchema)
        {
            return Block("strategic_replan_schema_version_mismatch");
        }

        var triggers = context.TriggerKinds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (triggers.Length == 0)
            return Block("strategic_replan_trigger_required");
        var unknown = triggers.Where(value => !AllowedTriggers.Contains(value))
            .ToArray();
        if (unknown.Length > 0)
        {
            return Block("strategic_replan_trigger_unknown:" +
                string.Join(",", unknown));
        }

        var fingerprint = Hash(new
        {
            schema_version = context.SchemaVersion,
            goal_id = goalId,
            snapshot_state_hash = snapshotStateHash,
            strategy_ledger_revision = ledgerRevision,
            trigger_kinds = triggers,
            trigger_token = context.TriggerToken ?? string.Empty
        });
        var duplicate = string.Equals(
            fingerprint,
            context.PreviousReplanFingerprint,
            StringComparison.Ordinal);
        return new StrategicReplanEvaluation(
            !duplicate,
            duplicate,
            triggers,
            fingerprint,
            Array.Empty<string>());
    }

    private static StrategicReplanEvaluation Block(string reason) => new(
        false,
        false,
        Array.Empty<string>(),
        string.Empty,
        new[] { reason });

    private static string Hash(object value)
    {
        var payload = JsonSerializer.Serialize(value, JsonDefaults.Compact);
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}

public sealed record StrategicReplanEvaluation(
    bool ReplanRequired,
    bool Deduplicated,
    string[] TriggerKinds,
    string Fingerprint,
    string[] BlockingReasons);
