using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StardewAI.Contracts.Mail;

public sealed class ParsedMailDirective
{
    public string Kind { get; set; } = string.Empty;
    public string ExecutionPhase { get; set; } = string.Empty;
    public int SourceOffset { get; set; }
    public string Raw { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = Array.Empty<string>();
    public string[] Errors { get; set; } = Array.Empty<string>();
}

public static class MailDirectiveParser
{
    private static readonly HashSet<string> ItemCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "object",
        "tools",
        "bigobject",
        "furniture",
        "money",
        "conversationtopic",
        "cookingrecipe",
        "craftingrecipe",
        "itemrecovery",
        "quest",
        "specialorder"
    };

    private static readonly HashSet<string> NoInventorySlotAttachmentIds = new(StringComparer.Ordinal)
    {
        "(O)73",
        "(O)930",
        "(O)102",
        "(O)858",
        "(O)434",
        "(O)GoldCoin"
    };

    public static IReadOnlyCollection<string> KnownItemCommands => ItemCommands;

    public static bool AttachmentRequiresInventorySlot(string qualifiedItemId) =>
        !NoInventorySlotAttachmentIds.Contains(qualifiedItemId);

    public static ParsedMailDirective[] Parse(string raw)
    {
        raw ??= string.Empty;
        return Extract(raw, "%action", "action", "0_action")
            .Concat(Extract(raw, "%item", "item", "1_item"))
            .OrderBy(row => row.ExecutionPhase, StringComparer.Ordinal)
            .ThenBy(row => row.SourceOffset)
            .ToArray();
    }

    private static IEnumerable<ParsedMailDirective> Extract(
        string raw,
        string marker,
        string kind,
        string phase)
    {
        var startIndex = 0;
        while (true)
        {
            var start = raw.IndexOf(marker, startIndex, StringComparison.InvariantCulture);
            if (start < 0)
                yield break;

            var end = raw.IndexOf("%%", start, StringComparison.InvariantCulture);
            if (end < 0)
            {
                yield return new ParsedMailDirective
                {
                    Kind = kind,
                    ExecutionPhase = phase,
                    SourceOffset = start,
                    Raw = raw[start..],
                    Body = raw[(start + marker.Length)..],
                    Errors = new[] { "mail_directive_missing_terminator" }
                };
                yield break;
            }

            var full = raw.Substring(start, end + 2 - start);
            var body = full.Substring(marker.Length, full.Length - marker.Length - 2);
            yield return kind == "action"
                ? ParseAction(start, phase, full, body)
                : ParseItem(start, phase, full, body);
            startIndex = end + 2;
        }
    }

    private static ParsedMailDirective ParseAction(
        int offset,
        string phase,
        string raw,
        string body)
    {
        var arguments = SplitQuoteAware(body).ToArray();
        return new ParsedMailDirective
        {
            Kind = "action",
            ExecutionPhase = phase,
            SourceOffset = offset,
            Raw = raw,
            Body = body,
            Command = arguments.FirstOrDefault() ?? string.Empty,
            Arguments = arguments,
            Errors = arguments.Length == 0
                ? new[] { "mail_action_empty" }
                : Array.Empty<string>()
        };
    }

    private static ParsedMailDirective ParseItem(
        int offset,
        string phase,
        string raw,
        string body)
    {
        var firstSplit = body.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = firstSplit.FirstOrDefault() ?? string.Empty;
        var arguments = firstSplit.Length > 1
            ? firstSplit[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();
        return new ParsedMailDirective
        {
            Kind = "item",
            ExecutionPhase = phase,
            SourceOffset = offset,
            Raw = raw,
            Body = body,
            Command = command,
            Arguments = arguments,
            Errors = ValidateItem(command, arguments)
        };
    }

    private static string[] ValidateItem(string command, IReadOnlyList<string> arguments)
    {
        var errors = new List<string>();
        if (!ItemCommands.Contains(command))
        {
            errors.Add("mail_item_command_unknown");
            return errors.ToArray();
        }

        switch (command.ToLowerInvariant())
        {
            case "id":
                if (arguments.Count != 1 && !IsValidItemAmountPairs(arguments))
                    errors.Add("mail_item_pair_arguments_invalid");
                break;
            case "object":
                if (!IsValidItemAmountPairs(arguments))
                    errors.Add("mail_item_pair_arguments_invalid");
                break;
            case "tools":
                if (arguments.Count == 0)
                    errors.Add("mail_tools_arguments_invalid");
                break;
            case "bigobject":
            case "furniture":
                if (arguments.Count == 0)
                    errors.Add("mail_item_choice_arguments_invalid");
                break;
            case "money":
                if (arguments.Count is < 1 or > 2 || arguments.Any(value => !int.TryParse(value, out _)))
                    errors.Add("mail_money_arguments_invalid");
                break;
            case "conversationtopic":
                if (arguments.Count < 2 || !int.TryParse(arguments[1], out _))
                    errors.Add("mail_conversation_topic_arguments_invalid");
                break;
            case "craftingrecipe":
                if (arguments.Count == 0)
                    errors.Add("mail_crafting_recipe_arguments_invalid");
                break;
            case "quest":
                if (arguments.Count == 0)
                    errors.Add("mail_quest_arguments_invalid");
                break;
            case "specialorder":
                if (arguments.Count == 0 || arguments.Count > 1 && !bool.TryParse(arguments[1], out _))
                    errors.Add("mail_special_order_arguments_invalid");
                break;
        }

        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool IsValidItemAmountPairs(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || arguments.Count % 2 != 0)
            return false;
        for (var index = 1; index < arguments.Count; index += 2)
        {
            if (!int.TryParse(arguments[index], out _))
                return false;
        }
        return true;
    }

    private static IReadOnlyList<string> SplitQuoteAware(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<string>();
        if (!input.Contains('"'))
            return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var result = new List<string>();
        var current = new StringBuilder();
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
}
