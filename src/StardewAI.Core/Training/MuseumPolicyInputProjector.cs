using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed class MuseumPolicyInputProjector
{
    public const string GoalId = "goal.world_progress.complete_museum_collection";
    public const string DeferKind = "bounded_no_donation_counterfactual";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly int donated;
    private readonly int missing;
    private readonly bool complete;
    private readonly bool achievement;
    private readonly Dictionary<string, bool> observedItems = new(StringComparer.Ordinal);

    public MuseumPolicyInputProjector(SnapshotEnvelope snapshot)
    {
        if (snapshot is null || snapshot.GameVersion != "1.6.15" || !SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash))
            throw new ArgumentException("Museum policy input needs its exact locked snapshot.", nameof(snapshot));
        var value = ReadStateFieldValue(snapshot, "world_progress", "museum");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "museum")) || value?.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Museum policy observations are unreadable.");
        var museum = value.Value;
        // This encodes live observations, not the authoritative inventory or a native outcome proof.
        if (!Int(museum, "total_donatable_items", out var total) || total != 95 ||
            !Int(museum, "donated_count", out donated) || !Int(museum, "missing_item_count", out missing) ||
            !Bool(museum, "collection_complete", out complete) || !Bool(museum, "complete_collection_achievement_received", out achievement) ||
            !museum.TryGetProperty("donatable_items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != total)
            throw new InvalidOperationException("Museum policy observation shape is incomplete.");
        foreach (var row in items.EnumerateArray())
        {
            if (!row.TryGetProperty("qualified_item_id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) ||
                !Bool(row, "donated", out var done) || !observedItems.TryAdd(id.GetString()!, done))
                throw new InvalidOperationException("Museum policy item observations are incomplete or duplicated.");
        }
        if (observedItems.Count(row => row.Value) != donated || observedItems.Count(row => !row.Value) != missing || complete != (missing == 0))
            throw new InvalidOperationException("Museum policy aggregate observations disagree.");
    }

    public FeatureVector ProjectState(FeatureVector features)
    {
        features.Numeric = features.Numeric.Where(row => !row.Name.StartsWith("museum.collection.", StringComparison.Ordinal)).Concat(new[]
        {
            new NumericFeature { Name = "museum.collection.total", Value = 95 },
            new NumericFeature { Name = "museum.collection.donated_count", Value = donated },
            new NumericFeature { Name = "museum.collection.missing_count", Value = missing }
        }).ToArray();
        features.Boolean = features.Boolean.Where(row => !row.Name.StartsWith("museum.collection.", StringComparison.Ordinal)).Concat(new[]
        {
            new BooleanFeature { Name = "museum.collection.complete", Value = complete },
            new BooleanFeature { Name = "museum.collection.achievement_5", Value = achievement }
        }).Concat(observedItems.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new BooleanFeature
            { Name = "museum.collection.item." + row.Key + ".donated", Value = row.Value })).ToArray();
        return features;
    }

    public PolicyEventCandidatePrediction[] ProjectCandidates(IEnumerable<PolicyEventCandidatePrediction> candidates) => candidates.Select(source =>
    {
        var candidate = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(JsonSerializer.Serialize(source, Json), Json)!;
        var previous = candidate.PolicyFeatures ?? new FeatureVector();
        var numbers = previous.Numeric.Where(row => !row.Name.StartsWith("museum.", StringComparison.Ordinal)).ToList();
        var flags = previous.Boolean.Where(row => !row.Name.StartsWith("museum.", StringComparison.Ordinal)).ToList();
        var categories = previous.Categorical.Where(row => !row.Name.StartsWith("museum.", StringComparison.Ordinal)).ToList();
        foreach (var name in new[] { "expected_donated_count_before", "expected_donated_count_after", "comparison_horizon_ticks" })
        {
            var parameters = candidate.Parameters.Where(row => row.Name == name).ToArray();
            var known = parameters.Length == 1 && int.TryParse(parameters[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            if (known) numbers.Add(new() { Name = "museum.donation." + name, Value = int.Parse(parameters[0].Value, CultureInfo.InvariantCulture) });
            flags.Add(new() { Name = "museum.donation." + name + ".known", Value = known });
        }
        foreach (var name in new[] { "expected_collection_complete_after", "expected_complete_collection_achievement_after" })
        {
            var parameters = candidate.Parameters.Where(row => row.Name == name).ToArray();
            var known = parameters.Length == 1 && bool.TryParse(parameters[0].Value, out _);
            if (known) flags.Add(new() { Name = "museum.donation." + name, Value = bool.Parse(parameters[0].Value) });
            flags.Add(new() { Name = "museum.donation." + name + ".known", Value = known });
        }
        var targetKnown = observedItems.TryGetValue(candidate.QualifiedItemId ?? string.Empty, out var targetDonated);
        flags.Add(new() { Name = "museum.donation.target_known", Value = targetKnown });
        if (targetKnown) flags.Add(new() { Name = "museum.donation.target_already_donated", Value = targetDonated });
        flags.Add(new() { Name = "museum.comparison.counterfactual_only", Value = candidate.Kind == DeferKind });
        categories.Add(new() { Name = "museum.comparison.role", Value = candidate.Kind == DeferKind ? "defer_current_arrangement_reference_not_runtime_candidate" : "original_current_candidate" });
        candidate.PolicyFeatures = new() { Numeric = numbers.ToArray(), Boolean = flags.ToArray(), Categorical = categories.ToArray() };
        return candidate;
    }).ToArray();

    private static bool Int(JsonElement row, string name, out int value)
    {
        value = 0;
        return row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }

    private static bool Bool(JsonElement row, string name, out bool value)
    {
        value = false;
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(name, out var field) || field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        value = field.GetBoolean(); return true;
    }
}
