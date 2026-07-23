using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewValley;
using StardewValley.Triggers;

namespace StardewAI.KnowledgeExporter;

internal sealed class RuntimeSemanticInventory
{
    private readonly List<RuntimeHandlerRecord> handlers = new();
    private readonly List<ParsedConditionRecord> conditions = new();
    private readonly List<ParsedEventRecord> events = new();
    private readonly List<ParsedTriggerActionRecord> triggerActions = new();
    private readonly Dictionary<string, RuntimeAssemblyIdentity> runtimeAssemblies =
        new(StringComparer.OrdinalIgnoreCase);

    public RuntimeSemanticInventory()
    {
        BuildHandlerRegistry();
    }

    public void Inspect(string assetName, byte[] payloadBytes)
    {
        using var document = JsonDocument.Parse(payloadBytes);
        WalkConditions(assetName, document.RootElement, "payload");
        if (assetName.Equals("Data/TriggerActions", StringComparison.OrdinalIgnoreCase))
            InspectTriggerActions(document.RootElement);
        if (assetName.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase) &&
            document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                    continue;
                InspectEvent(assetName, entry.Name, entry.Value.GetString() ?? string.Empty);
            }
        }
    }

    public object BuildOutput() => new
    {
        schema_version = "stardewai.runtime_semantics.v3",
        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
        authority = "installed runtime registries and native parsers; parsing only, no condition evaluation or command execution",
        handler_count = handlers.Count,
        parsed_condition_count = conditions.Count,
        condition_parse_error_count = conditions.Count(row => row.Clauses.Any(clause => clause.Error is not null)),
        parsed_event_count = events.Count,
        unresolved_event_precondition_count = events.Sum(row => row.Preconditions.Count(item => item.Handler is null)),
        unresolved_event_command_count = events.Sum(row => row.Commands.Count(item => item.Handler is null)),
        parsed_trigger_action_count = triggerActions.Count,
        unresolved_trigger_action_count = triggerActions.Sum(row => row.Actions.Count(item => item.Handler is null)),
        runtime_assemblies = runtimeAssemblies.Values.OrderBy(row => row.AssemblyName, StringComparer.Ordinal).ToArray(),
        handlers,
        parsed_conditions = conditions,
        parsed_events = events,
        parsed_trigger_actions = triggerActions
    };

    private void BuildHandlerRegistry()
    {
        _ = GameStateQuery.Parse(string.Empty);
        _ = Event.TryGetPreconditionHandler("__stardewai_registry_probe__", out _);

        AddRegistry("game_state_query", typeof(GameStateQuery), "QueryTypeLookup", "Aliases");
        AddRegistry("event_precondition", typeof(Event), "Preconditions", "PreconditionAliases");
        AddRegistry("event_command", typeof(Event), "Commands", "CommandAliases");
        AddRegistry("trigger_action", typeof(TriggerActionManager), "ActionHandlers", aliasField: null);
    }

    private void AddRegistry(
        string family,
        Type owner,
        string registryField,
        string? aliasField,
        IDictionary<string, RuntimeHandlerRecord>? lookup = null)
    {
        var registry = ReadDictionary(owner, registryField);
        var aliases = aliasField is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : ReadStringDictionary(owner, aliasField);
        foreach (DictionaryEntry entry in registry)
        {
            if (entry.Key is not string key || entry.Value is not Delegate handler)
                continue;
            var record = Handler(family, key, key, handler.Method, isAlias: false);
            handlers.Add(record);
            if (lookup is not null)
                lookup[key] = record;
        }

        foreach (var alias in aliases)
        {
            if (!registry.Contains(alias.Value) || registry[alias.Value] is not Delegate handler)
                continue;
            var record = Handler(family, alias.Key, alias.Value, handler.Method, isAlias: true);
            handlers.Add(record);
            if (lookup is not null)
                lookup[alias.Key] = record;
        }
    }

    private static IDictionary ReadDictionary(Type owner, string fieldName)
    {
        var field = owner.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new MissingFieldException(owner.FullName, fieldName);
        return field.GetValue(null) as IDictionary
               ?? throw new InvalidDataException($"Runtime registry {owner.FullName}.{fieldName} is not IDictionary.");
    }

    private static Dictionary<string, string> ReadStringDictionary(Type owner, string fieldName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in ReadDictionary(owner, fieldName))
        {
            if (entry.Key is string key && entry.Value is string value)
                result[key] = value;
        }
        return result;
    }

    private void WalkConditions(string assetName, JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = path + "." + Escape(property.Name);
                if (IsConditionProperty(property.Name) && property.Value.ValueKind == JsonValueKind.String)
                    ParseCondition(assetName, childPath, property.Value.GetString() ?? string.Empty);
                WalkConditions(assetName, property.Value, childPath);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
                WalkConditions(assetName, child, path + "[" + index++ + "]");
        }
    }

    private void ParseCondition(string assetName, string path, string raw)
    {
        var clauses = GameStateQuery.Parse(raw).Select(parsed => new ParsedQueryClause(
            parsed.Negated,
            parsed.Query,
            string.IsNullOrWhiteSpace(parsed.Error) ? null : parsed.Error,
            parsed.Resolver is null ? null : Handler("game_state_query", parsed.Query.FirstOrDefault() ?? string.Empty,
                parsed.Query.FirstOrDefault() ?? string.Empty, parsed.Resolver.Method, isAlias: false))).ToArray();
        conditions.Add(new ParsedConditionRecord(assetName, path, Hash(raw), clauses));
    }

    private void InspectEvent(string assetName, string eventKey, string script)
    {
        var keyParts = Event.SplitPreconditions(eventKey);
        var preconditions = new List<ParsedEventToken>();
        foreach (var raw in keyParts.Skip(1))
        {
            var args = ArgUtility.SplitBySpaceQuoteAware(raw);
            var rawKey = args.FirstOrDefault() ?? string.Empty;
            var negated = rawKey.StartsWith('!');
            var key = negated ? rawKey[1..] : rawKey;
            RuntimeHandlerRecord? handler = null;
            if (Event.TryGetPreconditionHandler(key, out var resolved))
                handler = Handler("event_precondition", key, key, resolved.Method, isAlias: false);
            preconditions.Add(new ParsedEventToken(raw, key, negated, args, handler));
        }

        var commands = new List<ParsedEventToken>();
        foreach (var raw in Event.ParseCommands(script, Game1.player).Skip(3))
        {
            var args = ArgUtility.SplitBySpaceQuoteAware(raw);
            var key = args.FirstOrDefault() ?? string.Empty;
            RuntimeHandlerRecord? handler = null;
            if (Event.TryGetEventCommandHandler(key, out var resolved))
                handler = Handler("event_command", key, key, resolved.Method, isAlias: false);
            commands.Add(new ParsedEventToken(raw, key, false, args, handler));
        }

        events.Add(new ParsedEventRecord(
            assetName,
            eventKey,
            keyParts.FirstOrDefault() ?? string.Empty,
            Hash(script),
            preconditions,
            commands));
    }

    private void InspectTriggerActions(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in payload.EnumerateArray())
        {
            var sourcePath = $"payload[{index}]";
            var id = String(entry, "Id") ?? string.Empty;
            var trigger = String(entry, "Trigger") ?? string.Empty;
            var actions = new List<ParsedTriggerActionToken>();

            if (entry.TryGetProperty("Action", out var action) &&
                action.ValueKind == JsonValueKind.String)
            {
                actions.Add(ParseTriggerAction(
                    sourcePath + ".Action",
                    action.GetString() ?? string.Empty));
            }

            if (entry.TryGetProperty("Actions", out var actionList) &&
                actionList.ValueKind == JsonValueKind.Array)
            {
                var actionIndex = 0;
                foreach (var actionValue in actionList.EnumerateArray())
                {
                    if (actionValue.ValueKind == JsonValueKind.String)
                    {
                        actions.Add(ParseTriggerAction(
                            $"{sourcePath}.Actions[{actionIndex}]",
                            actionValue.GetString() ?? string.Empty));
                    }
                    actionIndex++;
                }
            }

            triggerActions.Add(new(
                sourcePath,
                id,
                trigger,
                ArgUtility.SplitBySpace(trigger),
                Bool(entry, "HostOnly"),
                Bool(entry, "MarkActionApplied", defaultValue: true),
                actions));
            index++;
        }
    }

    private ParsedTriggerActionToken ParseTriggerAction(string sourcePath, string raw)
    {
        var parsed = TriggerActionManager.ParseAction(raw);
        RuntimeHandlerRecord? handler = null;
        if (parsed.Error is null && !parsed.IsNullHandler)
        {
            var key = parsed.Args.FirstOrDefault() ?? string.Empty;
            handler = Handler("trigger_action", key, key, parsed.Handler.Method, isAlias: false);
        }

        return new(
            sourcePath,
            raw,
            parsed.Args,
            parsed.Error,
            parsed.IsNullHandler,
            handler);
    }

    private RuntimeHandlerRecord Handler(
        string family,
        string key,
        string canonicalKey,
        MethodInfo method,
        bool isAlias)
    {
        RecordAssembly(method.Module);
        return new(
            family,
            key,
            canonicalKey,
            isAlias,
            method.DeclaringType?.FullName ?? string.Empty,
            method.Name,
            $"0x{method.MetadataToken:X8}",
            method.Module.ModuleVersionId.ToString("D"),
            method.Module.Assembly.GetName().Name ?? string.Empty);
    }

    private void RecordAssembly(Module module)
    {
        var assembly = module.Assembly;
        var name = assembly.GetName();
        var path = assembly.Location;
        var key = (name.Name ?? string.Empty) + ":" + module.ModuleVersionId.ToString("D");
        if (runtimeAssemblies.ContainsKey(key))
            return;

        long? bytes = null;
        string? sha256 = null;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var info = new FileInfo(path);
            bytes = info.Length;
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            sha256 = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        runtimeAssemblies[key] = new(
            name.Name ?? string.Empty,
            name.Version?.ToString() ?? string.Empty,
            module.ModuleVersionId.ToString("D"),
            bytes,
            sha256);
    }

    private static bool IsConditionProperty(string name) =>
        !string.Equals(name, "conditions", StringComparison.Ordinal) &&
        (name.EndsWith("Condition", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Conditions", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Queries", StringComparison.OrdinalIgnoreCase));

    private static string? String(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool Bool(JsonElement value, string propertyName, bool defaultValue = false) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : defaultValue;

    private static string Escape(string value) => value.Replace(".", "\\.", StringComparison.Ordinal);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

internal sealed record RuntimeHandlerRecord(
    string Family,
    string Key,
    string CanonicalKey,
    bool IsAlias,
    string DeclaringType,
    string MethodName,
    string MetadataToken,
    string ModuleVersionId,
    string AssemblyName);

internal sealed record RuntimeAssemblyIdentity(
    string AssemblyName,
    string AssemblyVersion,
    string ModuleVersionId,
    long? Bytes,
    string? Sha256);

internal sealed record ParsedConditionRecord(
    string SourceAsset,
    string SourcePath,
    string RawSha256,
    IReadOnlyList<ParsedQueryClause> Clauses);

internal sealed record ParsedQueryClause(
    bool Negated,
    IReadOnlyList<string> Tokens,
    string? Error,
    RuntimeHandlerRecord? Handler);

internal sealed record ParsedEventRecord(
    string SourceAsset,
    string EventKey,
    string EventId,
    string ScriptSha256,
    IReadOnlyList<ParsedEventToken> Preconditions,
    IReadOnlyList<ParsedEventToken> Commands);

internal sealed record ParsedEventToken(
    string Raw,
    string Key,
    bool Negated,
    IReadOnlyList<string> Tokens,
    RuntimeHandlerRecord? Handler);

internal sealed record ParsedTriggerActionRecord(
    string SourcePath,
    string Id,
    string Trigger,
    IReadOnlyList<string> TriggerTokens,
    bool HostOnly,
    bool MarkActionApplied,
    IReadOnlyList<ParsedTriggerActionToken> Actions);

internal sealed record ParsedTriggerActionToken(
    string SourcePath,
    string Raw,
    IReadOnlyList<string> Tokens,
    string? Error,
    bool IsNullHandler,
    RuntimeHandlerRecord? Handler);
