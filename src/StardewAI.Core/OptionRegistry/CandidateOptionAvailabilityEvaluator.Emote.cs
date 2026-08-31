using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] PlayerEmoteCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var key = EmoteIntent(intent, "emote_key");
        var reason = EmoteIntent(intent, "emote_reason");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(reason) ||
            EmoteIntent(intent, "confirm_emote") != "true")
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "player", "emote");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only" ||
            !HasCompleteLockedPlayerEmoteCatalog(projection.Value))
            return Array.Empty<EventCandidate>();
        var option = FindEmoteOption(projection.Value, key);
        if (!option.HasValue || ReadBool(option.Value, "native_command_accepted") != true)
            return Array.Empty<EventCandidate>();

        var parameters = EmoteCandidateParameters(projection.Value, option.Value, reason);
        var reasons = ReadString(projection.Value, "service_status") == "ready"
            ? Array.Empty<string>()
            : new[] { "emote_service_not_ready:" + ReadString(projection.Value, "service_status") };
        reasons = reasons.Concat(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "social.emote",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        })).Distinct(StringComparer.Ordinal).ToArray();
        var fingerprint = ReadString(option.Value, "option_fingerprint");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "player-emote:" + key + ":" + (fingerprint.Length >= 12 ? fingerprint[..12] : "invalid"),
                Kind = "perform_emote",
                Available = reasons.Length == 0,
                AllowedNow = reasons.Length == 0,
                AllowedToday = reasons.Length == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                EstimatedTicks = Math.Max(60, ReadInt(option.Value, "animation_duration_milliseconds") / 16 + 30),
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_network_emote",
                DisplayName = "Perform emote: " + ReadString(option.Value, "display_name"),
                ExpectedEffect = "emote=" + key + ";performed_emote_recorded=true;native_icon_or_animation_observed=true",
                BlockReasons = reasons,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] EmoteCandidateParameters(
        JsonElement projection,
        JsonElement option,
        string reason) => new[]
    {
        Parameter("emote_key", ReadString(option, "emote_key")),
        Parameter("emote_reason", reason),
        Parameter("confirm_emote", "true"),
        Parameter("emote_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
        Parameter("emote_option_fingerprint", ReadString(option, "option_fingerprint")),
        Parameter("emote_index", ReadInt(option, "emote_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_icon_index", ReadInt(option, "icon_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_has_animation", (ReadBool(option, "has_animation") == true).ToString().ToLowerInvariant()),
        Parameter("emote_animation_facing_direction", ReadInt(option, "animation_facing_direction").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_animation_duration_milliseconds", ReadInt(option, "animation_duration_milliseconds").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_hidden", (ReadBool(option, "hidden") == true).ToString().ToLowerInvariant()),
        Parameter("emote_performed_entry_before", (ReadBool(option, "performed_entry_present") == true).ToString().ToLowerInvariant()),
        Parameter("emote_performed_value_before", (ReadBool(option, "performed_value") == true).ToString().ToLowerInvariant()),
        Parameter("emote_player_id", ReadEmoteLong(projection, "player_id").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_language_code", ReadInt(projection, "language_code").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_network_role", ReadString(projection, "network_role")),
        Parameter("emote_chat_input_width_pixels", ReadInt(projection, "chat_input_width_pixels").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_chat_input_content_width_pixels", ReadInt(projection, "chat_input_content_width_pixels").ToString(CultureInfo.InvariantCulture)),
        Parameter("emote_native_input", "/emote " + ReadString(option, "emote_key")),
        Parameter("native_contract", ReadString(projection, "native_contract"))
    };

    private static JsonElement? FindEmoteOption(JsonElement projection, string key)
    {
        if (!projection.TryGetProperty("emotes", out var options) || options.ValueKind != JsonValueKind.Array)
            return null;
        var row = options.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "emote_key"), key, StringComparison.Ordinal));
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static string EmoteIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => parameter.Name == name)?.Value ?? string.Empty;

    private static long ReadEmoteLong(JsonElement value, string propertyName) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var parsed)
            ? parsed
            : 0;

    private static bool HasCompleteLockedPlayerEmoteCatalog(JsonElement projection)
    {
        if (!projection.TryGetProperty("emotes", out var options) || options.ValueKind != JsonValueKind.Array)
            return false;
        var rows = options.EnumerateArray().ToArray();
        return rows.Select(row => ReadString(row, "emote_key"))
                .SequenceEqual(PlayerEmoteIdentity.LockedBaseEmoteKeys, StringComparer.Ordinal) &&
            rows.Where(row => ReadBool(row, "hidden") == true).Select(row => ReadString(row, "emote_key"))
                .SequenceEqual(PlayerEmoteIdentity.LockedBaseHiddenEmoteKeys, StringComparer.Ordinal);
    }
}
