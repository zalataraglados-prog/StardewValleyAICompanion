using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed class MuseumDonationRouteScheduleBuilder
{
    private readonly SnapshotEnvelope snapshot;
    private readonly JsonElement graph;
    private readonly FutureRouteDateEvidenceContext? context;
    private readonly FutureRouteTimingCalibration? timing;
    private readonly int day;
    private readonly int startTime;
    private readonly string startLocation;
    private readonly int startX;
    private readonly int startY;
    private readonly string targetLocation;
    private readonly int targetX;
    private readonly int targetY;
    private readonly string[] errors;
    private readonly Dictionary<string, MuseumDonationRouteSchedule> schedules = new(StringComparer.Ordinal);

    public MuseumDonationRouteScheduleBuilder(SnapshotEnvelope snapshot, string calibrationJson)
    {
        this.snapshot = snapshot;
        var reasons = new List<string>();
        int? ReadFieldInt(string section, string field) => ReadableStatus(ReadStateFieldStatus(snapshot, section, field))
            ? ReadStateFieldIntOptional(snapshot, section, field) : null;
        var clock = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
        var totalDay = ReadFieldInt("time", "total_days");
        var playerX = ReadFieldInt("player", "tile_x");
        var playerY = ReadFieldInt("player", "tile_y");
        startLocation = ReadableStatus(ReadStateFieldStatus(snapshot, "player", "location_id"))
            ? ReadStateFieldString(snapshot, "player", "location_id") : string.Empty;
        if (!clock.HasValue || totalDay is null or < 0 || !playerX.HasValue || !playerY.HasValue || string.IsNullOrWhiteSpace(startLocation))
            reasons.Add("museum_donation_route_source_position_or_clock_unavailable");
        day = totalDay ?? -1;
        startTime = clock ?? -1;
        startX = playerX ?? -1;
        startY = playerY ?? -1;
        var museum = ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "museum"))
            ? ReadStateFieldValue(snapshot, "world_progress", "museum") : null;
        targetLocation = museum.HasValue ? ReadString(museum.Value, "museum_location_id") : string.Empty;
        targetX = museum.HasValue ? ReadInt(museum.Value, "gunther_action_tile_x", -1) : -1;
        targetY = museum.HasValue ? ReadInt(museum.Value, "gunther_action_tile_y", -1) : -1;
        if (string.IsNullOrWhiteSpace(targetLocation) || targetX < 0 || targetY < 0 ||
            !museum.HasValue || !string.Equals(ReadString(museum.Value, "gunther_action_raw")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "Gunther", StringComparison.OrdinalIgnoreCase) ||
            ReadInt(museum.Value, "free_donation_tile_count") <= 0)
            reasons.Add("museum_donation_route_counter_projection_unavailable");
        graph = ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "route_graph"))
            ? ReadStateFieldValue(snapshot, "locations", "route_graph") ?? default : default;
        var dateEvidence = ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "social_route_date_evidence"))
            ? ReadStateFieldValue(snapshot, "locations", "social_route_date_evidence") ?? default : default;
        if (!snapshot.State.TryGetValue("player", out var player) || player.ValueKind != JsonValueKind.Object ||
            !player.TryGetProperty("movement_timing_context", out var movement))
            movement = default;
        var loaded = new FutureRouteTimingCalibrationLoader().Load(calibrationJson, movement, snapshot.GameVersion, day);
        timing = loaded.Calibration;
        reasons.AddRange(loaded.BlockingReasons);
        if (FutureRouteDateEvidenceProducer.TryCreateContext(graph, dateEvidence, day, out var parsed, out var routeErrors))
            context = parsed;
        else reasons.AddRange(routeErrors);
        errors = reasons.Distinct(StringComparer.Ordinal).OrderBy(reason => reason, StringComparer.Ordinal).ToArray();
    }

    public MuseumDonationRouteSchedule Build(PolicyEventCandidatePrediction candidate)
    {
        if (errors.Length > 0) return Blocked(errors);
        var required = new[] { "connector_kind", "expected_target_location", "expected_arrival_tile_x", "expected_arrival_tile_y" };
        var values = candidate.Parameters.Where(parameter => required.Contains(parameter.Name, StringComparer.Ordinal)).ToArray();
        if (candidate.Kind != "route_connector_tile" || candidate.LocationId != startLocation ||
            !candidate.TileX.HasValue || !candidate.TileY.HasValue || values.Length != required.Length ||
            values.Select(value => value.Name).Distinct(StringComparer.Ordinal).Count() != required.Length)
            return Blocked(new[] { "museum_donation_route_first_connector_incomplete" });
        var parameters = values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        if (!int.TryParse(parameters["expected_arrival_tile_x"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var arrivalX) ||
            !int.TryParse(parameters["expected_arrival_tile_y"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var arrivalY))
            return Blocked(new[] { "museum_donation_route_first_connector_incomplete" });
        var first = new TransparentRouteEdge(parameters["connector_kind"], startLocation, parameters["expected_target_location"],
            candidate.TileX, candidate.TileY, arrivalX, arrivalY);
        var key = JsonSerializer.Serialize(first);
        if (schedules.TryGetValue(key, out var cached)) return cached;
        var production = new FutureRouteDateEvidenceProducer().Produce(context!, new FutureRouteDateEvidenceRequest
        {
            TotalDays = day, StartLocation = startLocation, StartTileX = startX, StartTileY = startY,
            EarliestDepartureTime = startTime, TargetLocation = targetLocation, TargetTileX = targetX, TargetTileY = targetY,
            RequiredFirstEdge = first
        }, timing!);
        MuseumDonationRouteSchedule result;
        if (production.Scenario is null) result = Blocked(production.BlockingReasons);
        else
        {
            var route = new FutureRouteAccessWindowResolver().Resolve(graph, production.Scenario, targetLocation, targetX, targetY);
            if (!route.GuaranteedArrivalByTime.HasValue || route.Status == FutureRouteAccessResolutionStatus.Blocked)
                result = Blocked(route.BlockingReasons);
            else
            {
                var completion = GameClockBudgetPolicy.AddClockMinutes(route.GuaranteedArrivalByTime.Value,
                    GameClockBudgetPolicy.TicksToGameMinutes(MuseumDonationBudgetPolicy.InteractionTicks));
                result = completion > GameClockBudgetPolicy.AutonomousRecoveryStartTime
                    ? Blocked(new[] { "museum_donation_full_route_exceeds_day_budget" })
                    : new(snapshot.StateHash, day, startTime, route.GuaranteedArrivalByTime, completion,
                        timing!.EvidenceSha256, production.Scenario, Array.Empty<string>());
            }
        }
        schedules.Add(key, result);
        return result;
    }

    private MuseumDonationRouteSchedule Blocked(string[] reasons) =>
        new(snapshot.StateHash, day, startTime, null, null, timing?.EvidenceSha256 ?? string.Empty, null, reasons);
}

public sealed record MuseumDonationRouteSchedule(
    [property: JsonPropertyName("snapshot_state_hash")] string SnapshotStateHash,
    [property: JsonPropertyName("source_total_day")] int SourceTotalDay,
    [property: JsonPropertyName("departure_time")] int DepartureTime,
    [property: JsonPropertyName("counter_arrival_by_time")] int? CounterArrivalByTime,
    [property: JsonPropertyName("donation_completion_by_time")] int? DonationCompletionByTime,
    [property: JsonPropertyName("calibration_sha256")] string CalibrationSha256,
    [property: JsonPropertyName("route_scenario")] FutureRouteAccessScenario? RouteScenario,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons)
{
    [JsonPropertyName("current_day_route_ready")]
    public bool CurrentDayRouteReady => RouteScenario is not null && DonationCompletionByTime.HasValue && BlockingReasons.Length == 0;
    [JsonPropertyName("native_donation_verified")]
    public bool NativeDonationVerified => false;
    [JsonPropertyName("native_key_handoff_verified")]
    public bool NativeKeyHandoffVerified => false;
}
