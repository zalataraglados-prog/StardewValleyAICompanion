using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed class NoProgressBackoffPolicy
{
    private readonly int initialDelayMs;
    private readonly int maximumDelayMs;
    private string lastSignature = string.Empty;

    public NoProgressBackoffPolicy(
        int initialDelayMs,
        int maximumDelayMs)
    {
        this.initialDelayMs = Math.Max(0, initialDelayMs);
        this.maximumDelayMs = Math.Max(
            this.initialDelayMs,
            maximumDelayMs);
    }

    public int Streak { get; private set; }

    public NoProgressBackoffDecision Observe(JsonObject queue)
    {
        if (HasExecutableItem(queue))
        {
            Reset();
            return new NoProgressBackoffDecision(
                false,
                0,
                0,
                string.Empty);
        }

        var signature = SemanticSignature(queue);
        Streak = string.Equals(
            signature,
            lastSignature,
            StringComparison.Ordinal)
            ? Streak + 1
            : 1;
        lastSignature = signature;

        if (initialDelayMs == 0)
        {
            return new NoProgressBackoffDecision(
                true,
                Streak,
                0,
                signature);
        }

        var exponent = Math.Min(30, Streak - 1);
        var scaled = (long)initialDelayMs << exponent;
        return new NoProgressBackoffDecision(
            true,
            Streak,
            (int)Math.Min(maximumDelayMs, scaled),
            signature);
    }

    public void Reset()
    {
        Streak = 0;
        lastSignature = string.Empty;
    }

    public static string SemanticSignature(JsonObject queue)
    {
        var status = ReadString(queue, "status");
        var diagnostics = ReadStrings(
            queue["compiler_diagnostics"] as JsonArray);
        var items = queue["items"]?.AsArray()
            .Select(node => node?.AsObject())
            .Where(item => item is not null)
            .Select(item =>
            {
                var blocking = ReadStrings(
                    item!["blocking_reasons"] as JsonArray);
                return ReadString(item, "option_id") + ":" +
                    ReadString(item, "status") + ":" +
                    string.Join(",", blocking);
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return status + "|diagnostics:" +
            string.Join(",", diagnostics) +
            "|items:" +
            string.Join(";", items);
    }

    private static bool HasExecutableItem(JsonObject queue)
    {
        return queue["items"]?.AsArray()
            .Select(node => node?.AsObject())
            .Any(item => string.Equals(
                ReadString(item, "status"),
                "pending",
                StringComparison.Ordinal)) == true;
    }

    private static string[] ReadStrings(JsonArray? values)
    {
        return values?
            .Select(node =>
                node is JsonValue value &&
                value.TryGetValue<string>(out var text)
                    ? text
                    : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
    }

    private static string ReadString(
        JsonObject? value,
        string propertyName)
    {
        return value is not null &&
            value[propertyName] is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var text)
                ? text
                : string.Empty;
    }
}

public sealed record NoProgressBackoffDecision(
    bool NoProgress,
    int Streak,
    int DelayMs,
    string Signature);
