using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class NativeWeddingCalendarProjection
{
    private readonly string[] festivals;
    private readonly string[] blocked;

    [JsonConstructor]
    public NativeWeddingCalendarProjection(ulong saveUniqueId, int sourceYear, string[] festivalDateKeys, string[] blockedDateKeys)
    {
        SaveUniqueId = saveUniqueId;
        SourceYear = sourceYear;
        festivals = (festivalDateKeys ?? throw new ArgumentNullException(nameof(festivalDateKeys))).ToArray();
        blocked = (blockedDateKeys ?? throw new ArgumentNullException(nameof(blockedDateKeys))).ToArray();
    }

    [JsonPropertyName("schema_version")] public string SchemaVersion => "native_wedding_calendar.v1";
    [JsonPropertyName("native_predicate")] public string NativePredicate => "Game1.canHaveWeddingOnDay";
    [JsonPropertyName("save_unique_id")] public ulong SaveUniqueId { get; }
    [JsonPropertyName("source_year")] public int SourceYear { get; }
    [JsonPropertyName("evaluated_date_count")] public int EvaluatedDateCount => 112;
    [JsonPropertyName("festival_date_keys")] public string[] FestivalDateKeys => festivals.ToArray();
    [JsonPropertyName("blocked_date_keys")] public string[] BlockedDateKeys => blocked.ToArray();
}
