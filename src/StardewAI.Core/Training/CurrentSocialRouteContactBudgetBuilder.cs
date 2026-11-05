using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    // Offline comparison only; retain one builder per snapshot, never construct it per frame.
    public sealed class CurrentSocialRouteContactBudgetBuilder
    {
        private readonly string sourceStateHash = string.Empty;
        private readonly JsonElement graph;
        private readonly FutureRouteDateEvidenceContext? context;
        private readonly FutureRouteTimingCalibration? timing;
        private readonly int day;
        private readonly int time;
        private readonly string location = string.Empty;
        private readonly int x;
        private readonly int y;
        private readonly string[] errors = Array.Empty<string>();
        private readonly Dictionary<string, CurrentSocialRouteContactBudget> budgets = new(StringComparer.Ordinal);
        private readonly Dictionary<CurrentSocialContactOpportunity, string> frontierRows = new();

        public CurrentSocialContactFrontier Frontier { get; } = new();
        internal int RouteProductionBuildCount { get; private set; }

        public CurrentSocialRouteContactBudgetBuilder(JsonElement rawSnapshot, string calibrationJson)
        {
            if (rawSnapshot.ValueKind != JsonValueKind.Object ||
                !rawSnapshot.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.String ||
                schema.GetString() != "snapshot.v1" || !rawSnapshot.TryGetProperty("game_tick", out var tick) ||
                tick.ValueKind != JsonValueKind.Number || !tick.TryGetInt64(out var gameTick) || gameTick < 0 ||
                !JsonSnapshotEnvelopeAdapter.TryCreate(rawSnapshot, out var envelope) || envelope.GameVersion != "1.6.15" ||
                !SnapshotHash.MatchesStateHash(envelope.State, envelope.StateHash))
            {
                errors = new[] { "current_social_route_snapshot_identity_invalid" };
                return;
            }
            var snapshot = envelope;
            sourceStateHash = envelope.StateHash;
            var reasons = new List<string>();
            int? ReadIntField(string section, string field) => ReadableStatus(ReadStateFieldStatus(snapshot, section, field))
                ? ReadStateFieldIntOptional(snapshot, section, field) : null;
            var clock = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
            var totalDay = ReadIntField("time", "total_days");
            var tileX = ReadIntField("player", "tile_x");
            var tileY = ReadIntField("player", "tile_y");
            location = ReadableStatus(ReadStateFieldStatus(snapshot, "player", "location_id"))
                ? ReadStateFieldString(snapshot, "player", "location_id") : string.Empty;
            if (!clock.HasValue || totalDay is null or < 0 || !tileX.HasValue || !tileY.HasValue || location.Length == 0)
                reasons.Add("current_social_route_position_or_clock_unavailable");
            day = totalDay ?? -1;
            time = clock ?? -1;
            x = tileX ?? -1;
            y = tileY ?? -1;
            graph = ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "route_graph"))
                ? ReadStateFieldValue(snapshot, "locations", "route_graph")?.Clone() ?? default : default;
            var dateEvidence = ReadableStatus(ReadStateFieldStatus(snapshot, "locations", "social_route_date_evidence"))
                ? ReadStateFieldValue(snapshot, "locations", "social_route_date_evidence")?.Clone() ?? default : default;
            if (!snapshot.State.TryGetValue("player", out var player) || player.ValueKind != JsonValueKind.Object ||
                !player.TryGetProperty("movement_timing_context", out var movement)) movement = default;
            var loaded = new FutureRouteTimingCalibrationLoader().Load(calibrationJson, movement, snapshot.GameVersion, day);
            timing = loaded.Calibration;
            reasons.AddRange(loaded.BlockingReasons);
            if (loaded.Status != FutureRouteTimingCalibrationLoadStatus.Loaded || timing is null)
                reasons.Add("current_social_route_calibration_unavailable");
            if (FutureRouteDateEvidenceProducer.TryCreateContext(graph, dateEvidence, day, out var parsed, out var routeErrors))
                context = parsed;
            else reasons.AddRange(routeErrors);
            errors = reasons.Distinct(StringComparer.Ordinal).ToArray();
            if (errors.Length == 0)
            {
                Frontier = new CurrentSocialContactFrontierProducer().Produce(rawSnapshot, calibrationJson);
                foreach (var opportunity in Frontier.Opportunities) frontierRows.Add(opportunity, JsonSerializer.Serialize(opportunity));
            }
        }

        public CurrentSocialRouteContactBudget Build(PolicyEventCandidatePrediction candidate, CurrentSocialContactOpportunity opportunity)
        {
            CurrentSocialRouteContactBudget Blocked(params string[] reasons) => new()
            {
                SourceStateHash = sourceStateHash, CandidateId = candidate.CandidateId,
                OpportunityId = opportunity.OpportunityId, BlockingReasons = reasons
            };
            if (errors.Length > 0) return Blocked(errors);
            if (!frontierRows.TryGetValue(opportunity, out var original) || original != JsonSerializer.Serialize(opportunity) ||
                !opportunity.ExecutionReady ||
                opportunity.TimingEvidenceId != timing!.EvidenceId ||
                candidate.Kind != "route_connector_tile" || !candidate.Available ||
                candidate.AllowedNow != true || candidate.AllowedToday != true || candidate.BlockReasons.Length > 0 ||
                candidate.OptionId is not ("social.talk_npc" or "social.gift_npc") ||
                candidate.Parameters.GroupBy(parameter => parameter.Name, StringComparer.Ordinal).Any(group => group.Count() > 1) ||
                Parameter(candidate, "npc_name") != opportunity.NpcName ||
                Parameter(candidate, "target_location") != opportunity.LocationName ||
                Parameter(candidate, "continuation.npc_name") != opportunity.NpcName ||
                Parameter(candidate, "continuation.target_location") != opportunity.LocationName ||
                Parameter(candidate, "continuation.option_id") != candidate.OptionId ||
                opportunity.InteractionKind != (candidate.OptionId == "social.talk_npc" ? "talk" : "gift") ||
                !Integer(candidate, "npc_tile_x", out var npcX) || npcX != opportunity.NpcTileX ||
                !Integer(candidate, "npc_tile_y", out var npcY) || npcY != opportunity.NpcTileY)
                return Blocked("current_social_route_candidate_or_frontier_binding_invalid");
            if (candidate.OptionId == "social.gift_npc" &&
                (candidate.SlotIndex != opportunity.GiftSlotIndex || candidate.QualifiedItemId != opportunity.GiftQualifiedItemId ||
                 !Integer(candidate, "expected_friendship_delta", out var delta) || delta != opportunity.ExpectedFriendshipDelta))
                return Blocked("current_social_route_gift_binding_invalid");
            if (candidate.LocationId != location || !candidate.TileX.HasValue || !candidate.TileY.HasValue ||
                !Integer(candidate, "expected_arrival_tile_x", out var arrivalX) ||
                !Integer(candidate, "expected_arrival_tile_y", out var arrivalY) ||
                string.IsNullOrWhiteSpace(Parameter(candidate, "connector_kind")) ||
                string.IsNullOrWhiteSpace(Parameter(candidate, "expected_target_location")))
                return Blocked("current_social_route_first_connector_incomplete");
            var first = new TransparentRouteEdge(Parameter(candidate, "connector_kind")!, location,
                Parameter(candidate, "expected_target_location")!, candidate.TileX, candidate.TileY, arrivalX, arrivalY);
            var request = new FutureRouteDateEvidenceRequest
            {
                TotalDays = day, StartLocation = location, StartTileX = x, StartTileY = y, EarliestDepartureTime = time,
                TargetLocation = opportunity.LocationName, TargetTileX = npcX, TargetTileY = npcY, RequiredFirstEdge = first
            };
            var key = JsonSerializer.Serialize(new { Candidate = candidate.CandidateId, Request = request, Opportunity = opportunity });
            if (budgets.TryGetValue(key, out var cached)) return cached;
            RouteProductionBuildCount++;
            var production = new FutureRouteDateEvidenceProducer().Produce(context!, request, timing!);
            if (production.Scenario is null) return Remember(Blocked(production.BlockingReasons));
            var presence = new NpcFuturePresenceWindowResolution
            {
                Status = NpcFuturePresenceWindowResolutionStatus.Exact, NpcName = opportunity.NpcName,
                SelectedScheduleKey = opportunity.SelectedScheduleKey,
                Windows = new[] { new NpcFuturePresenceWindow
                {
                    ScheduleEntryOrdinal = opportunity.ScheduleEntryOrdinal, LocationName = opportunity.LocationName,
                    TileX = npcX, TileY = npcY, WindowStartTime = opportunity.NpcPresentFromTime,
                    WindowEndTimeExclusive = opportunity.NpcPresentUntilTimeExclusive,
                    HasStableInterval = true, EndpointBehaviorComplete = true
                } }
            };
            var eligibility = new[] { new FutureNpcContactEligibilityEvidence
            {
                TotalDays = day, NpcName = opportunity.NpcName, SelectedScheduleKey = opportunity.SelectedScheduleKey,
                ScheduleEntryOrdinal = opportunity.ScheduleEntryOrdinal, LocationName = opportunity.LocationName,
                TileX = npcX, TileY = npcY, EligibleFromTime = opportunity.InteractionEligibleFromTime,
                EligibleUntilTimeExclusive = opportunity.InteractionEligibleUntilTimeExclusive,
                StateComplete = true, TalkAllowed = opportunity.InteractionKind == "talk", GiftAllowed = opportunity.InteractionKind == "gift",
                SocialQueryValueOnCaptureDate = true, SocialQueryStableThroughDay = true,
                SocialQueryEvidenceKind = "copied_from_current_social_contact_frontier_v3"
            } };
            var resolved = new NpcFutureContactWindowResolver().Resolve(graph, production.Scenario, presence,
                opportunity.InteractionKind, eligibility);
            var contact = resolved.Windows.SingleOrDefault();
            if (contact is null || resolved.Status == NpcFutureContactWindowResolutionStatus.Blocked)
                return Remember(Blocked(resolved.BlockingReasons.DefaultIfEmpty("current_social_route_contact_unavailable").ToArray()));
            if (contact.EarliestInteractionTime >= GameClockBudgetPolicy.AutonomousRecoveryStartTime)
                return Remember(Blocked("current_social_route_contact_exceeds_day_budget"));
            return Remember(new CurrentSocialRouteContactBudget
            {
                Status = "verified_for_actual_first_connector", SourceStateHash = sourceStateHash,
                CandidateId = candidate.CandidateId, OpportunityId = opportunity.OpportunityId,
                TimingEvidenceId = timing!.EvidenceId, RequiredFirstEdge = first,
                RouteScenario = production.Scenario, ContactWindow = contact
            });

            CurrentSocialRouteContactBudget Remember(CurrentSocialRouteContactBudget result)
            {
                budgets.Add(key, result);
                return result;
            }
        }

        private static string? Parameter(PolicyEventCandidatePrediction candidate, string name)
        {
            var values = candidate.Parameters.Where(parameter => parameter.Name == name).ToArray();
            return values.Length == 1 ? values[0].Value : null;
        }

        private static bool Integer(PolicyEventCandidatePrediction candidate, string name, out int value) =>
            int.TryParse(Parameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
