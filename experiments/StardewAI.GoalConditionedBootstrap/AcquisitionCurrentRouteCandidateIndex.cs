using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionCurrentRouteCandidateIndex
{
    private static readonly string[] OptionIds =
    {
        "mining.reach_depth",
        "farm.collect_machine_outputs",
        "foraging.harvest_tree_product"
    };

    private AcquisitionCurrentRouteCandidateIndex(
        SnapshotEnvelope snapshot,
        AcquisitionCurrentRouteCandidate[] candidates,
        string[] blockingReasons)
    {
        Snapshot = snapshot;
        Candidates = candidates;
        BlockingReasons = blockingReasons;
    }

    private SnapshotEnvelope Snapshot { get; }

    private AcquisitionCurrentRouteCandidate[] Candidates { get; }

    public string[] BlockingReasons { get; }

    public static AcquisitionCurrentRouteCandidateIndex Read(
        string snapshotPath)
    {
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            Path.GetFullPath(snapshotPath),
            "Current authoritative-route candidate snapshot");
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            OptionIds,
            includeExecutorCalibrationOptions: true);
        var candidates = new List<AcquisitionCurrentRouteCandidate>();
        var reasons = new List<string>();
        foreach (var optionId in OptionIds)
        {
            var options = availability.Options
                .Where(value => value.OptionId == optionId)
                .ToArray();
            if (options.Length != 1)
            {
                reasons.Add(
                    "current_route_option_evaluation_ambiguous:" + optionId);
                continue;
            }
            foreach (var candidate in options[0].EventCandidates.Where(value =>
                         value.Available && value.BlockReasons.Length == 0))
            {
                var parameters = candidate.Parameters
                    .Where(value => value.Name ==
                        "authoritative_route_sources_json")
                    .Select(value => value.Value)
                    .ToArray();
                if (parameters.Length == 0)
                    continue;
                if (parameters.Length != 1 ||
                    !TryReadSources(parameters[0], out var sources))
                {
                    reasons.Add(
                        "current_route_candidate_source_invalid:" +
                        candidate.CandidateId);
                    continue;
                }
                candidates.AddRange(sources.Select(source => new
                    AcquisitionCurrentRouteCandidate(
                        candidate.CandidateId,
                        optionId,
                        candidate.LocationId,
                        candidate.TileX,
                        candidate.TileY,
                        candidate.EstimatedTicks,
                        candidate.EnergyCost,
                        source.RouteKind,
                        source.SourceId,
                        source.QualifiedItemId)));
            }
        }
        return new AcquisitionCurrentRouteCandidateIndex(
            snapshot,
            candidates
                .Distinct()
                .OrderBy(value => value.EstimatedTicks)
                .ThenBy(value => value.LocationId, StringComparer.Ordinal)
                .ThenBy(value => value.TargetTileY)
                .ThenBy(value => value.TargetTileX)
                .ToArray(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    public bool TryFind(
        string routeKind,
        string sourceId,
        string qualifiedItemId,
        out AcquisitionCurrentRouteCandidate[] candidates,
        out string[] blockingReasons)
    {
        candidates = Array.Empty<AcquisitionCurrentRouteCandidate>();
        var reasons = new List<string>(BlockingReasons);
        if (!CanConcludeCurrentCandidateSet(routeKind, reasons))
        {
            blockingReasons = reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return false;
        }
        candidates = Candidates.Where(value =>
                value.RouteKind == routeKind &&
                value.SourceId == sourceId &&
                value.QualifiedItemId == qualifiedItemId)
            .ToArray();
        blockingReasons = Array.Empty<string>();
        return true;
    }

    private bool CanConcludeCurrentCandidateSet(
        string routeKind,
        ICollection<string> reasons)
    {
        if (routeKind == "native_monster_drop_table")
        {
            if (TryField("mining", "monsters", out var field) &&
                ((FieldStatus(field) == "available" &&
                  FieldValue(field).ValueKind == JsonValueKind.Array) ||
                 (FieldStatus(field) == "unavailable" &&
                  ReadString(field, "reason") == "not_loaded_mineshaft")))
            {
                return reasons.Count == 0;
            }
            reasons.Add("current_mine_monster_terminal_evidence_incomplete");
            return false;
        }
        if (routeKind == "native_wild_tree_tapper_output")
        {
            var terrainComplete = TryField(
                    "current_location",
                    "terrain_features",
                    out var terrain) &&
                FieldStatus(terrain) == "available" &&
                FieldValue(terrain).ValueKind == JsonValueKind.Array;
            var machineComplete = TryField("farm", "machines", out var machines) &&
                FieldStatus(machines) == "available" &&
                FieldValue(machines).ValueKind == JsonValueKind.Array;
            if (terrainComplete && machineComplete)
                return reasons.Count == 0;
            reasons.Add("current_tree_tapper_terminal_evidence_incomplete");
            return false;
        }
        reasons.Add("current_route_candidate_kind_not_supported:" + routeKind);
        return false;
    }

    private bool TryField(
        string sectionName,
        string fieldName,
        out JsonElement field)
    {
        field = default;
        return Snapshot.State.TryGetValue(sectionName, out var section) &&
            section.ValueKind == JsonValueKind.Object &&
            section.TryGetProperty(fieldName, out field) &&
            field.ValueKind == JsonValueKind.Object;
    }

    private static string FieldStatus(JsonElement field) =>
        ReadString(field, "status");

    private static JsonElement FieldValue(JsonElement field) =>
        field.TryGetProperty("value", out var value) ? value : default;

    private static string ReadString(JsonElement value, string property) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryReadSources(
        string json,
        out CurrentRouteSource[] sources)
    {
        sources = Array.Empty<CurrentRouteSource>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            var result = new List<CurrentRouteSource>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var routeKind = ReadString(row, "route_kind");
                var sourceId = ReadString(row, "source_id");
                var qualifiedItemId = ReadString(row, "qualified_item_id");
                if (string.IsNullOrWhiteSpace(routeKind) ||
                    string.IsNullOrWhiteSpace(sourceId) ||
                    string.IsNullOrWhiteSpace(qualifiedItemId))
                {
                    return false;
                }
                result.Add(new CurrentRouteSource(
                    routeKind,
                    sourceId,
                    qualifiedItemId));
            }
            sources = result.Distinct().ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record CurrentRouteSource(
        string RouteKind,
        string SourceId,
        string QualifiedItemId);
}

internal sealed record AcquisitionCurrentRouteCandidate(
    string CandidateId,
    string OptionId,
    string LocationId,
    int? TargetTileX,
    int? TargetTileY,
    int EstimatedTicks,
    int EnergyCost,
    string RouteKind,
    string SourceId,
    string QualifiedItemId);
