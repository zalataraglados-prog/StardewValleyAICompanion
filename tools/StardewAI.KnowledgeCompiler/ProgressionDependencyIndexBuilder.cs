using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Mail;

namespace StardewAI.KnowledgeCompiler;

internal sealed class ProgressionDependencyIndexBuilder
{
    public ProgressionDependencyIndex Build(
        IReadOnlyDictionary<string, PayloadAsset> payloads,
        string runtimeSemanticsPath)
    {
        var issues = new List<ProgressionIndexIssue>();
        var mailAsset = RequiredAsset(payloads, "Data/mail");

        using var runtimeDocument = JsonDocument.Parse(File.ReadAllBytes(runtimeSemanticsPath));
        var runtime = runtimeDocument.RootElement;
        var schema = String(runtime, "schema_version") ?? string.Empty;
        if (!string.Equals(schema, "stardewai.runtime_semantics.v3", StringComparison.Ordinal))
        {
            issues.Add(new(
                "blocking",
                "runtime_trigger_action_semantics_missing",
                "runtime-semantics",
                $"expected=stardewai.runtime_semantics.v3;actual={schema}"));
        }

        var conditions = ReadConditions(runtime);
        var conditionByPath = conditions.ToDictionary(row => row.SourcePath, StringComparer.Ordinal);
        var mail = BuildMail(mailAsset.Payload, issues);
        var triggerActions = BuildTriggerActions(runtime, conditionByPath, issues);
        var events = BuildEvents(runtime, issues);
        var references = mail.SelectMany(row => row.Directives.SelectMany(directive => directive.References))
            .Concat(triggerActions.SelectMany(row => row.References))
            .Concat(events.SelectMany(row => row.References))
            .OrderBy(row => row.SourceKind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceId, StringComparer.Ordinal)
            .ThenBy(row => row.SourcePath, StringComparer.Ordinal)
            .ThenBy(row => row.Operation, StringComparer.Ordinal)
            .ThenBy(row => row.TargetKind, StringComparer.Ordinal)
            .ThenBy(row => row.TargetId, StringComparer.Ordinal)
            .ToArray();

        return new(
            mail,
            triggerActions,
            events,
            conditions,
            references,
            issues,
            new(
                mail.Count,
                mail.Sum(row => row.Directives.Count),
                triggerActions.Count,
                triggerActions.Sum(row => row.Actions.Count),
                events.Count,
                events.Sum(row => row.Preconditions.Count),
                events.Sum(row => row.Commands.Count),
                references.Length,
                issues.Count(row => row.Severity == "blocking")));
    }

    private static IReadOnlyList<MailProgressionEntry> BuildMail(
        JsonElement payload,
        ICollection<ProgressionIndexIssue> issues)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("blocking", "mail_payload_not_object", "Data/mail", payload.ValueKind.ToString()));
            return Array.Empty<MailProgressionEntry>();
        }

        var result = new List<MailProgressionEntry>();
        foreach (var entry in payload.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
            {
                issues.Add(new("blocking", "mail_value_not_string", entry.Name, entry.Value.ValueKind.ToString()));
                continue;
            }

            var raw = entry.Value.GetString() ?? string.Empty;
            var directives = MailDirectiveParser.Parse(raw)
                .Select(directive => ConvertMailDirective(entry.Name, directive, issues))
                .ToArray();
            result.Add(new(entry.Name, Hashing.Sha256(raw), directives));
        }

        return result.OrderBy(row => row.MailId, StringComparer.Ordinal).ToArray();
    }

    private static MailDirective ConvertMailDirective(
        string mailId,
        ParsedMailDirective directive,
        ICollection<ProgressionIndexIssue> issues)
    {
        foreach (var error in directive.Errors)
        {
            issues.Add(new(
                "blocking",
                error,
                mailId,
                $"offset={directive.SourceOffset};command={directive.Command}"));
        }

        var references = directive.Kind == "action"
            ? TriggerReferences("mail", mailId, $"directive@{directive.SourceOffset}", directive.Arguments)
            : MailItemReferences(mailId, directive.SourceOffset, directive.Command, directive.Arguments, issues);
        return new MailDirective(
            directive.Kind,
            directive.ExecutionPhase,
            directive.SourceOffset,
            directive.Raw,
            directive.Body,
            directive.Command,
            directive.Arguments,
            directive.Kind == "action"
                ? "TriggerActionManager.ParseAction and TryRunAction"
                : NativeMailResolution(directive.Command, directive.Arguments),
            references);
    }

    private static IReadOnlyList<ProgressionReference> MailItemReferences(
        string mailId,
        int offset,
        string command,
        IReadOnlyList<string> args,
        ICollection<ProgressionIndexIssue> issues)
    {
        var path = $"directive@{offset}";
        var result = new List<ProgressionReference>();
        switch (command.ToLowerInvariant())
        {
            case "id":
                if (args.Count == 1)
                {
                    result.Add(Reference("mail", mailId, path, "grant", "item", args[0], args));
                }
                else
                {
                    AddPairedItemChoices("id", mailId, path, args, result, issues);
                }
                break;
            case "object":
                AddPairedItemChoices("object", mailId, path, args, result, issues);
                break;
            case "tools":
                foreach (var tool in args)
                {
                    var itemId = tool switch
                    {
                        "Axe" or "Hoe" or "Pickaxe" => "(T)" + tool,
                        "Can" => "(T)WateringCan",
                        "Scythe" => "(W)47",
                        _ => string.Empty
                    };
                    if (itemId.Length > 0)
                        result.Add(Reference("mail", mailId, path, "grant", "item", itemId, new[] { tool }));
                }
                break;
            case "bigobject":
                foreach (var id in args)
                    result.Add(Reference("mail", mailId, path, "grant_random_choice", "item", "(BC)" + id, args));
                break;
            case "furniture":
                foreach (var id in args)
                    result.Add(Reference("mail", mailId, path, "grant_random_choice", "item", "(F)" + id, args));
                break;
            case "money":
                if (args.Count is < 1 or > 2 || args.Any(value =>
                        !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    issues.Add(new("blocking", "mail_money_arguments_invalid", mailId, string.Join(' ', args)));
                }
                result.Add(Reference("mail", mailId, path, "grant_random_range_rounded_down_to_10", "money", "Money", args));
                break;
            case "conversationtopic":
                if (args.Count < 2)
                    issues.Add(new("blocking", "mail_conversation_topic_arguments_invalid", mailId, string.Join(' ', args)));
                if (args.Count > 0)
                    result.Add(Reference("mail", mailId, path, "activate", "conversation_topic", args[0], args));
                break;
            case "cookingrecipe":
                result.Add(Reference(
                    "mail",
                    mailId,
                    path,
                    args.Count == 0 ? "learn_native_friendship_recipe_resolution" : "learn",
                    "cooking_recipe",
                    args.Count == 0 ? mailId.Replace("Cooking", string.Empty, StringComparison.Ordinal) : string.Join(' ', args),
                    args));
                break;
            case "craftingrecipe":
                if (args.Count == 0)
                    issues.Add(new("blocking", "mail_crafting_recipe_arguments_invalid", mailId, string.Empty));
                else
                    result.Add(Reference("mail", mailId, path, "learn_with_underscore_fallback", "crafting_recipe", args[0], args));
                break;
            case "itemrecovery":
                result.Add(Reference("mail", mailId, path, "grant_if_present", "recovered_item", "Farmer.recoveredItem", args));
                break;
            case "quest":
                if (args.Count == 0)
                    issues.Add(new("blocking", "mail_quest_arguments_invalid", mailId, string.Empty));
                else
                    result.Add(Reference("mail", mailId, path, args.Count > 1 ? "add_immediately" : "offer_on_letter_close", "quest", args[0], args));
                break;
            case "specialorder":
                if (args.Count == 0)
                    issues.Add(new("blocking", "mail_special_order_arguments_invalid", mailId, string.Empty));
                else
                    result.Add(Reference(
                        "mail",
                        mailId,
                        path,
                        args.Count > 1 && bool.TryParse(args[1], out var immediate) && immediate
                            ? "add_immediately"
                            : "offer_on_letter_close",
                        "special_order",
                        args[0],
                        args));
                break;
        }
        return result;
    }

    private static void AddPairedItemChoices(
        string command,
        string mailId,
        string path,
        IReadOnlyList<string> args,
        ICollection<ProgressionReference> result,
        ICollection<ProgressionIndexIssue> issues)
    {
        if (args.Count < 2 || args.Count % 2 != 0)
        {
            issues.Add(new(
                "blocking",
                "mail_item_pair_arguments_invalid",
                mailId,
                $"command={command};args={string.Join(' ', args)}"));
            return;
        }
        for (var index = 0; index < args.Count; index += 2)
        {
            if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                issues.Add(new(
                    "blocking",
                    "mail_item_amount_invalid",
                    mailId,
                    $"item={args[index]};amount={args[index + 1]}"));
            }
            result.Add(Reference(
                "mail",
                mailId,
                path,
                "grant_random_pair_choice",
                "item",
                args[index],
                new[] { args[index], args[index + 1] }));
        }
    }

    private static string NativeMailResolution(string command, IReadOnlyList<string> args) =>
        command.ToLowerInvariant() switch
        {
            "id" when args.Count == 1 => "ItemRegistry.Create(id, 1)",
            "id" or "object" => "Game1.random.Next(argument_count), rounded down to an even pair index; ItemRegistry.Create(id, amount)",
            "tools" => "fixed LetterViewerMenu tool-name switch; unrecognized names are ignored",
            "bigobject" => "Game1.random.ChooseFrom; ItemRegistry.Create('(BC)' + id)",
            "furniture" => "Game1.random.ChooseFrom; ItemRegistry.Create('(F)' + id)",
            "money" => "one fixed value or Game1.random.Next(min, maxExclusive), then rounded down to a multiple of 10",
            "conversationtopic" => "Farmer.activeDialogueEvents[topic] = duration; ElliottGone3 also adds (O)732 to home fridge",
            "cookingrecipe" when args.Count == 0 => "lowest friendship threshold recipe matching mail title, selected by native dictionary iteration",
            "cookingrecipe" => "joined argument string looked up in CraftingRecipe.cookingRecipes",
            "craftingrecipe" => "direct key lookup, then underscore-to-space fallback",
            "itemrecovery" => "move Farmer.recoveredItem to the letter grab slot if non-null",
            "quest" when args.Count > 1 => "add immediately unless NOQUEST_<id> mail exists",
            "quest" => "offer quest on letter close",
            "specialorder" when args.Count > 1 => "bool argument true adds immediately unless NOSPECIALORDER_<id> mail exists; otherwise offer on close",
            "specialorder" => "offer special order on letter close",
            _ => "unknown"
        };

    private static IReadOnlyList<NativeConditionRecord> ReadConditions(JsonElement runtime)
    {
        if (!runtime.TryGetProperty("parsed_conditions", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<NativeConditionRecord>();
        }
        return rows.EnumerateArray().Select(row => new NativeConditionRecord(
            String(row, "sourceAsset") ?? string.Empty,
            String(row, "sourcePath") ?? string.Empty,
            String(row, "rawSha256") ?? string.Empty,
            row.GetProperty("clauses").Clone())).ToArray();
    }

    private static IReadOnlyList<TriggerProgressionEntry> BuildTriggerActions(
        JsonElement runtime,
        IReadOnlyDictionary<string, NativeConditionRecord> conditions,
        ICollection<ProgressionIndexIssue> issues)
    {
        if (!runtime.TryGetProperty("parsed_trigger_actions", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TriggerProgressionEntry>();
        }

        var result = new List<TriggerProgressionEntry>();
        foreach (var row in rows.EnumerateArray())
        {
            var sourcePath = String(row, "sourcePath") ?? string.Empty;
            var id = String(row, "id") ?? string.Empty;
            var actions = row.GetProperty("actions").EnumerateArray().Select(action =>
            {
                var tokens = Strings(action, "tokens");
                var handler = action.TryGetProperty("handler", out var handlerValue) &&
                              handlerValue.ValueKind == JsonValueKind.Object
                    ? handlerValue.Clone()
                    : (JsonElement?)null;
                var error = String(action, "error");
                if (handler is null || error is not null)
                {
                    issues.Add(new(
                        "blocking",
                        "trigger_action_native_parse_unresolved",
                        id,
                        $"path={String(action, "sourcePath")};error={error ?? "missing_handler"}"));
                }
                return new NativeActionRecord(
                    String(action, "sourcePath") ?? string.Empty,
                    String(action, "raw") ?? string.Empty,
                    tokens,
                    error,
                    Bool(action, "isNullHandler"),
                    handler);
            }).ToArray();

            conditions.TryGetValue(sourcePath + ".Condition", out var condition);
            conditions.TryGetValue(sourcePath + ".SkipPermanentlyCondition", out var skip);
            var references = actions.SelectMany(action =>
                TriggerReferences("trigger_action", id, action.SourcePath, action.Tokens)).ToArray();
            result.Add(new(
                sourcePath,
                id,
                String(row, "trigger") ?? string.Empty,
                Strings(row, "triggerTokens"),
                Bool(row, "hostOnly"),
                Bool(row, "markActionApplied"),
                condition,
                skip,
                actions,
                references));
        }
        return result.OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<EventProgressionEntry> BuildEvents(
        JsonElement runtime,
        ICollection<ProgressionIndexIssue> issues)
    {
        if (!runtime.TryGetProperty("parsed_events", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("blocking", "runtime_event_semantics_missing", "runtime-semantics", "parsed_events"));
            return Array.Empty<EventProgressionEntry>();
        }

        return rows.EnumerateArray().Select(row =>
        {
            var sourceAsset = String(row, "sourceAsset") ?? string.Empty;
            var eventKey = String(row, "eventKey") ?? string.Empty;
            var sourceId = sourceAsset + ":" + eventKey;
            var preconditions = row.GetProperty("preconditions").EnumerateArray()
                .Select(NativeEventToken)
                .ToArray();
            var commands = row.GetProperty("commands").EnumerateArray()
                .Select(NativeEventToken)
                .ToArray();
            foreach (var token in preconditions.Concat(commands).Where(token => token.Handler is null))
            {
                issues.Add(new(
                    "blocking",
                    "event_native_handler_unresolved",
                    sourceId,
                    $"key={token.Key};raw={token.Raw}"));
            }
            var references = commands.SelectMany(command =>
                EventReferences(sourceId, command)).ToArray();
            return new EventProgressionEntry(
                sourceAsset,
                eventKey,
                String(row, "eventId") ?? string.Empty,
                String(row, "scriptSha256") ?? string.Empty,
                preconditions,
                commands,
                references);
        }).OrderBy(row => row.SourceAsset, StringComparer.Ordinal)
          .ThenBy(row => row.EventKey, StringComparer.Ordinal)
          .ToArray();
    }

    private static NativeEventToken NativeEventToken(JsonElement token) => new(
        String(token, "raw") ?? string.Empty,
        String(token, "key") ?? string.Empty,
        Bool(token, "negated"),
        Strings(token, "tokens"),
        token.TryGetProperty("handler", out var handler) && handler.ValueKind == JsonValueKind.Object
            ? handler.Clone()
            : null);

    private static IReadOnlyList<ProgressionReference> TriggerReferences(
        string sourceKind,
        string sourceId,
        string sourcePath,
        IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 1)
            return Array.Empty<ProgressionReference>();
        return tokens[0].ToLowerInvariant() switch
        {
            "addmail" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "add", "mail", tokens[2], tokens) },
            "removemail" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "remove", "mail", tokens[2], tokens) },
            "markeventseen" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, BoolArgument(tokens, 3, true) ? "mark_seen" : "mark_unseen", "event", tokens[2], tokens) },
            "addquest" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "add", "quest", tokens[2], tokens) },
            "removequest" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "remove", "quest", tokens[2], tokens) },
            "addspecialorder" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "add", "special_order", tokens[2], tokens) },
            "removespecialorder" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, "remove", "special_order", tokens[2], tokens) },
            "markcookingrecipeknown" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, BoolArgument(tokens, 3, true) ? "learn" : "forget", "cooking_recipe", tokens[2], tokens) },
            "markcraftingrecipeknown" when tokens.Count >= 3 =>
                new[] { Reference(sourceKind, sourceId, sourcePath, BoolArgument(tokens, 3, true) ? "learn" : "forget", "crafting_recipe", tokens[2], tokens) },
            _ => Array.Empty<ProgressionReference>()
        };
    }

    private static IReadOnlyList<ProgressionReference> EventReferences(
        string sourceId,
        NativeEventToken command)
    {
        var tokens = command.Tokens;
        if (tokens.Count < 2)
            return Array.Empty<ProgressionReference>();
        var path = "command:" + command.Key;
        return command.Key.ToLowerInvariant() switch
        {
            "addquest" => One("add", "quest", tokens[1]),
            "removequest" => One("remove", "quest", tokens[1]),
            "addspecialorder" => One("add", "special_order", tokens[1]),
            "removespecialorder" => One("remove", "special_order", tokens[1]),
            "additem" => One("add", "item", tokens[1]),
            "removeitem" => One("remove", "item", tokens[1]),
            "addmailreceived" or "mailreceived" =>
                One(BoolArgument(tokens, 2, true) ? "mark_received" : "remove_received", "mail", tokens[1]),
            "mail" or "hostmail" => One("schedule_tomorrow", "mail", tokens[1]),
            "mailtoday" => One("schedule_today", "mail", tokens[1]),
            "addworldstate" => One("add", "world_state", tokens[1]),
            "addcookingrecipe" => One("learn", "cooking_recipe", string.Join(' ', tokens.Skip(1))),
            "addcraftingrecipe" => One("learn", "crafting_recipe", string.Join(' ', tokens.Skip(1))),
            "friendship" => One("change", "friendship", tokens[1]),
            _ => Array.Empty<ProgressionReference>()
        };

        IReadOnlyList<ProgressionReference> One(string operation, string targetKind, string targetId) =>
            new[] { Reference("event", sourceId, path, operation, targetKind, targetId, tokens) };
    }

    private static ProgressionReference Reference(
        string sourceKind,
        string sourceId,
        string sourcePath,
        string operation,
        string targetKind,
        string targetId,
        IReadOnlyList<string> tokens) =>
        new(sourceKind, sourceId, sourcePath, operation, targetKind, targetId, tokens);

    private static PayloadAsset RequiredAsset(
        IReadOnlyDictionary<string, PayloadAsset> payloads,
        string name) =>
        payloads.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value
        ?? throw new InvalidDataException($"Required progression asset '{name}' was not exported.");

    private static IReadOnlyList<string> SplitBySpace(string value, int limit)
    {
        if (limit == int.MaxValue)
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return value.Split(' ', limit, StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<string> SplitQuoteAware(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<string>();
        if (!input.Contains('"'))
            return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        var escaped = false;
        foreach (var character in input)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\' && quoted)
            {
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == ' ' && !quoted)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(character);
        }
        if (current.Length > 0)
            result.Add(current.ToString());
        return result;
    }

    private static IReadOnlyList<string> Strings(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

    private static string? String(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool Bool(JsonElement value, string propertyName, bool defaultValue = false) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : defaultValue;

    private static bool BoolArgument(IReadOnlyList<string> tokens, int index, bool defaultValue) =>
        tokens.Count > index && bool.TryParse(tokens[index], out var result)
            ? result
            : defaultValue;
}

internal sealed record ProgressionDependencyIndex(
    IReadOnlyList<MailProgressionEntry> Mail,
    IReadOnlyList<TriggerProgressionEntry> TriggerActions,
    IReadOnlyList<EventProgressionEntry> Events,
    IReadOnlyList<NativeConditionRecord> Conditions,
    IReadOnlyList<ProgressionReference> References,
    IReadOnlyList<ProgressionIndexIssue> Issues,
    ProgressionDependencySummary Summary);

internal sealed record ProgressionDependencySummary(
    int MailEntryCount,
    int MailDirectiveCount,
    int TriggerEntryCount,
    int TriggerActionCount,
    int EventCount,
    int EventPreconditionCount,
    int EventCommandCount,
    int ReferenceCount,
    int BlockingIssueCount);

internal sealed record MailProgressionEntry(
    string MailId,
    string RawSha256,
    IReadOnlyList<MailDirective> Directives);

internal sealed record MailDirective(
    string Kind,
    string ExecutionPhase,
    int SourceOffset,
    string Raw,
    string Body,
    string Command,
    IReadOnlyList<string> Tokens,
    string NativeResolution,
    IReadOnlyList<ProgressionReference> References);

internal sealed record TriggerProgressionEntry(
    string SourcePath,
    string Id,
    string Trigger,
    IReadOnlyList<string> TriggerTokens,
    bool HostOnly,
    bool MarkActionApplied,
    NativeConditionRecord? Condition,
    NativeConditionRecord? SkipPermanentlyCondition,
    IReadOnlyList<NativeActionRecord> Actions,
    IReadOnlyList<ProgressionReference> References);

internal sealed record NativeConditionRecord(
    string SourceAsset,
    string SourcePath,
    string RawSha256,
    JsonElement Clauses);

internal sealed record NativeActionRecord(
    string SourcePath,
    string Raw,
    IReadOnlyList<string> Tokens,
    string? Error,
    bool IsNullHandler,
    JsonElement? Handler);

internal sealed record EventProgressionEntry(
    string SourceAsset,
    string EventKey,
    string EventId,
    string ScriptSha256,
    IReadOnlyList<NativeEventToken> Preconditions,
    IReadOnlyList<NativeEventToken> Commands,
    IReadOnlyList<ProgressionReference> References);

internal sealed record NativeEventToken(
    string Raw,
    string Key,
    bool Negated,
    IReadOnlyList<string> Tokens,
    JsonElement? Handler);

internal sealed record ProgressionReference(
    string SourceKind,
    string SourceId,
    string SourcePath,
    string Operation,
    string TargetKind,
    string TargetId,
    IReadOnlyList<string> Tokens);

internal sealed record ProgressionIndexIssue(
    string Severity,
    string Code,
    string Subject,
    string Detail);

internal static class Hashing
{
    public static string Sha256(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
