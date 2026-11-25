using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StardewAI.Contracts.State;

/// <summary>Read-only native $q/$r answers, without event callbacks or extra actions.</summary>
public sealed class NativeNpcQuestionEvidence
{
    public const string SourceId = "native_Dialogue.getNPCResponseOptions.pure_friendship.v1";
    [JsonPropertyName("source")] public string Source { get; set; } = SourceId;
    [JsonPropertyName("speaker")] public string Speaker { get; set; } = string.Empty;
    [JsonPropertyName("ordinary_question")] public bool OrdinaryQuestion { get; set; }
    [JsonPropertyName("responses")] public NativeNpcQuestionAnswer[] Responses { get; set; } = Array.Empty<NativeNpcQuestionAnswer>();

    public NativeNpcQuestionAnswer? BestAnswer()
    {
        if (Source != SourceId || !OrdinaryQuestion || string.IsNullOrWhiteSpace(Speaker) ||
            Responses is null || Responses.Length < 2 || Responses.Length > 16 ||
            Responses.Any(row => row is null || string.IsNullOrWhiteSpace(row.Key) ||
                string.IsNullOrWhiteSpace(row.SeenId) || row.SeenId == "-1" ||
                !IsDisplayOnlyReply(row.Reply)) ||
            Responses.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count() != Responses.Length)
            return null;
        return Responses.OrderByDescending(row => row.FriendshipChange)
            .ThenBy(row => row.Key, StringComparer.Ordinal).First();
    }

    public string Identity()
    {
        using var sha = SHA256.Create();
        var payload = JsonSerializer.Serialize(new { Speaker, Responses });
        return "npc_question:" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    // Response parsing can run commands. Only plain text, native portrait emotions
    // and exact standalone $e display segments are admitted here. The native parser
    // splits on '#' and consumes $e without adding an action callback; prefixes or
    // arguments are not covered. Original reply text still binds the identity hash.
    public static bool IsDisplayOnlyReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains('%') || text.Contains('[') || text.Contains(']')) return false;
        var displayOnlyText = string.Join("#", text.Split('#').Select(
            segment => string.Equals(segment, "$e", StringComparison.Ordinal) ? string.Empty : segment));
        var stripped = Regex.Replace(displayOnlyText, @"\$(?:neutral|h|s|a|u|l|n)(?=$|[^A-Za-z0-9_])", string.Empty);
        return !stripped.Contains('$');
    }
}

public sealed class NativeNpcQuestionAnswer
{
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("seen_id")] public string SeenId { get; set; } = string.Empty;
    [JsonPropertyName("friendship_change")] public int FriendshipChange { get; set; }
    [JsonPropertyName("reply")] public string Reply { get; set; } = string.Empty;
}
