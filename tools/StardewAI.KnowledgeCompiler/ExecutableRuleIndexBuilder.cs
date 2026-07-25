using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class ExecutableRuleIndexBuilder
{
    public ExecutableRuleIndex Build(
        string runtimeSemanticsPath,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies,
        HandlerOperationCatalog operationCatalog,
        IReadOnlyList<RuntimeMethodReferenceEvidence> dataMethodReferences)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeSemanticsPath));
        var root = document.RootElement;
        var assemblyShaByMvid = assemblies.ToDictionary(
            row => row.ModuleVersionId,
            row => row.AssemblySha256,
            StringComparer.OrdinalIgnoreCase);
        var ruleIdByIdentity = operationCatalog.Rules.ToDictionary(
            row => row.Identity,
            row => row.RuleId,
            StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();

        var conditions = root.GetProperty("parsed_conditions").EnumerateArray()
            .Select(row => new ExecutableConditionRule(
                row.GetProperty("sourceAsset").GetString() ?? string.Empty,
                row.GetProperty("sourcePath").GetString() ?? string.Empty,
                row.GetProperty("rawSha256").GetString() ?? string.Empty,
                row.GetProperty("clauses").EnumerateArray()
                    .Select(clause => ConditionClause(
                        clause,
                        assemblyShaByMvid,
                        ruleIdByIdentity,
                        unresolved))
                    .ToArray()))
            .ToArray();

        var events = root.GetProperty("parsed_events").EnumerateArray()
            .Select(row =>
            {
                var sourceAsset = row.GetProperty("sourceAsset").GetString() ?? string.Empty;
                var eventKey = row.GetProperty("eventKey").GetString() ?? string.Empty;
                return new ExecutableEventRule(
                    sourceAsset,
                    eventKey,
                    row.GetProperty("eventId").GetString() ?? string.Empty,
                    row.GetProperty("scriptSha256").GetString() ?? string.Empty,
                    row.GetProperty("preconditions").EnumerateArray()
                        .Select(token => EventToken(
                            token,
                            sourceAsset + ":" + eventKey + ":precondition",
                            assemblyShaByMvid,
                            ruleIdByIdentity,
                            unresolved))
                        .ToArray(),
                    row.GetProperty("commands").EnumerateArray()
                        .Select(token => EventToken(
                            token,
                            sourceAsset + ":" + eventKey + ":command",
                            assemblyShaByMvid,
                            ruleIdByIdentity,
                            unresolved))
                        .ToArray());
            })
            .ToArray();

        var triggerActions = root.TryGetProperty("parsed_trigger_actions", out var triggerRows) &&
                             triggerRows.ValueKind == JsonValueKind.Array
            ? triggerRows.EnumerateArray().Select(row =>
            {
                var sourcePath = row.GetProperty("sourcePath").GetString() ?? string.Empty;
                var id = row.GetProperty("id").GetString() ?? string.Empty;
                return new ExecutableTriggerActionRule(
                    sourcePath,
                    id,
                    row.GetProperty("triggerTokens").EnumerateArray()
                        .Select(token => token.GetString() ?? string.Empty)
                        .ToArray(),
                    row.GetProperty("hostOnly").GetBoolean(),
                    row.GetProperty("markActionApplied").GetBoolean(),
                    row.GetProperty("actions").EnumerateArray().Select(action =>
                        new ExecutableTriggerActionToken(
                            action.GetProperty("sourcePath").GetString() ?? string.Empty,
                            action.GetProperty("raw").GetString() ?? string.Empty,
                            action.GetProperty("tokens").EnumerateArray()
                                .Select(token => token.GetString() ?? string.Empty)
                                .ToArray(),
                            action.TryGetProperty("error", out var error) &&
                            error.ValueKind == JsonValueKind.String
                                ? error.GetString()
                                : null,
                            HandlerBinding(
                                action.GetProperty("handler"),
                                "trigger_action:" + id,
                                assemblyShaByMvid,
                                ruleIdByIdentity,
                                unresolved)))
                        .ToArray());
            }).ToArray()
            : Array.Empty<ExecutableTriggerActionRule>();

        var dataMethods = dataMethodReferences.Select(reference => new ExecutableDataMethodRule(
            reference.SourceAsset,
            reference.SourcePath,
            reference.RawReference,
            reference.ResolutionStatus,
            reference.Matches.Select(match =>
            {
                var identity = Identity(match.AssemblySha256, match.MetadataToken);
                if (!ruleIdByIdentity.TryGetValue(identity, out var ruleId))
                {
                    unresolved.Add($"{reference.SourceAsset}:{reference.SourcePath}:{identity}");
                    return new ExecutableDataMethodBinding(identity, null);
                }
                return new ExecutableDataMethodBinding(identity, ruleId);
            }).ToArray())).ToArray();

        return new(
            conditions,
            events,
            triggerActions,
            dataMethods,
            unresolved.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static ExecutableConditionClause ConditionClause(
        JsonElement clause,
        IReadOnlyDictionary<string, string> assemblyShaByMvid,
        IReadOnlyDictionary<string, int> ruleIdByIdentity,
        ICollection<string> unresolved)
    {
        var binding = HandlerBinding(
            clause.GetProperty("handler"),
            "condition",
            assemblyShaByMvid,
            ruleIdByIdentity,
            unresolved);
        return new(
            clause.GetProperty("negated").GetBoolean(),
            clause.GetProperty("tokens").EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToArray(),
            clause.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null,
            binding);
    }

    private static ExecutableEventToken EventToken(
        JsonElement token,
        string source,
        IReadOnlyDictionary<string, string> assemblyShaByMvid,
        IReadOnlyDictionary<string, int> ruleIdByIdentity,
        ICollection<string> unresolved) => new(
            token.GetProperty("raw").GetString() ?? string.Empty,
            token.GetProperty("key").GetString() ?? string.Empty,
            token.GetProperty("negated").GetBoolean(),
            token.GetProperty("tokens").EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToArray(),
            HandlerBinding(token.GetProperty("handler"), source, assemblyShaByMvid, ruleIdByIdentity, unresolved));

    private static ExecutableHandlerBinding? HandlerBinding(
        JsonElement handler,
        string source,
        IReadOnlyDictionary<string, string> assemblyShaByMvid,
        IReadOnlyDictionary<string, int> ruleIdByIdentity,
        ICollection<string> unresolved)
    {
        if (handler.ValueKind != JsonValueKind.Object)
        {
            unresolved.Add(source + ":missing_handler");
            return null;
        }

        var mvid = handler.GetProperty("moduleVersionId").GetString() ?? string.Empty;
        var token = handler.GetProperty("metadataToken").GetString() ?? string.Empty;
        var family = handler.GetProperty("family").GetString() ?? string.Empty;
        var key = handler.GetProperty("key").GetString() ?? string.Empty;
        if (!assemblyShaByMvid.TryGetValue(mvid, out var assemblySha))
        {
            unresolved.Add($"{source}:{family}:{key}:mvid={mvid}");
            return new(family, key, string.Empty, null);
        }

        var identity = Identity(assemblySha, token);
        if (!ruleIdByIdentity.TryGetValue(identity, out var ruleId))
        {
            unresolved.Add($"{source}:{family}:{key}:{identity}");
            return new(family, key, identity, null);
        }
        return new(family, key, identity, ruleId);
    }

    private static string Identity(string assemblySha256, string metadataToken) =>
        assemblySha256 + ":" + metadataToken.ToLowerInvariant();
}

internal sealed record ExecutableRuleIndex(
    IReadOnlyList<ExecutableConditionRule> Conditions,
    IReadOnlyList<ExecutableEventRule> Events,
    IReadOnlyList<ExecutableTriggerActionRule> TriggerActions,
    IReadOnlyList<ExecutableDataMethodRule> DataMethods,
    IReadOnlyList<string> UnresolvedBindings);

internal sealed record ExecutableConditionRule(
    string SourceAsset,
    string SourcePath,
    string RawSha256,
    IReadOnlyList<ExecutableConditionClause> Clauses);

internal sealed record ExecutableConditionClause(
    bool Negated,
    IReadOnlyList<string> Tokens,
    string? Error,
    ExecutableHandlerBinding? Handler);

internal sealed record ExecutableEventRule(
    string SourceAsset,
    string EventKey,
    string EventId,
    string ScriptSha256,
    IReadOnlyList<ExecutableEventToken> Preconditions,
    IReadOnlyList<ExecutableEventToken> Commands);

internal sealed record ExecutableEventToken(
    string Raw,
    string Key,
    bool Negated,
    IReadOnlyList<string> Tokens,
    ExecutableHandlerBinding? Handler);

internal sealed record ExecutableTriggerActionRule(
    string SourcePath,
    string Id,
    IReadOnlyList<string> TriggerTokens,
    bool HostOnly,
    bool MarkActionApplied,
    IReadOnlyList<ExecutableTriggerActionToken> Actions);

internal sealed record ExecutableTriggerActionToken(
    string SourcePath,
    string Raw,
    IReadOnlyList<string> Tokens,
    string? Error,
    ExecutableHandlerBinding? Handler);

internal sealed record ExecutableHandlerBinding(
    string Family,
    string Key,
    string Identity,
    int? OperationRuleId);

internal sealed record ExecutableDataMethodRule(
    string SourceAsset,
    string SourcePath,
    string RawReference,
    string ResolutionStatus,
    IReadOnlyList<ExecutableDataMethodBinding> Bindings);

internal sealed record ExecutableDataMethodBinding(string Identity, int? OperationRuleId);
