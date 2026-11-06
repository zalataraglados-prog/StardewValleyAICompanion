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

namespace StardewAI.Core.Training;

public sealed class FriendshipPolicyInputProjector
{
    public const string GoalId = "goal.social.raise_friendships";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SnapshotEnvelope snapshot;
    private readonly CurrentSocialRouteContactBudgetBuilder? routes;
    private readonly int time;
    private readonly int minutes;
    private readonly int energy;

    public FriendshipPolicyInputProjector(SnapshotEnvelope snapshot, string? calibrationJson)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (!SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash))
            throw new ArgumentException("Friendship input snapshot hash is invalid.", nameof(snapshot));
        var original = JsonSerializer.SerializeToElement(snapshot, JsonOptions);
        this.snapshot = snapshot = original.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        time = SnapshotGameClockBudgetReader.CurrentTime(snapshot) ?? throw new ArgumentException("Friendship input clock is unavailable.");
        minutes = GameClockBudgetPolicy.ClockMinutesBetween(time, GameClockBudgetPolicy.AutonomousRecoveryStartTime);
        var stamina = ReadStateFieldValue(snapshot, "player", "energy");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "energy")) || stamina is null ||
            !stamina.Value.TryGetDouble(out var available) || double.IsNaN(available) || double.IsInfinity(available) ||
            available < 0 || available > int.MaxValue)
            throw new ArgumentException("Friendship input energy is unavailable.", nameof(snapshot));
        energy = (int)Math.Floor(available);
        if (!string.IsNullOrWhiteSpace(calibrationJson))
        {
            routes = new CurrentSocialRouteContactBudgetBuilder(original, calibrationJson!);
            if (routes.BlockingReasons.Length > 0)
                throw new InvalidOperationException("Friendship policy calibration/context is unavailable: " + string.Join(",", routes.BlockingReasons));
        }
    }

    public PolicyEventCandidatePrediction[] CertifyCurrentCandidates(IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var copies = Clone(candidates);
        if (routes is not null)
            foreach (var candidate in copies)
                CurrentSocialContactCandidateAdapter.CertifyCurrentContact(candidate, snapshot, routes.Frontier, time, minutes, energy, out _);
        return copies;
    }

    public PolicyEventCandidatePrediction[] ProjectCandidates(IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var copies = Clone(candidates);
        foreach (var candidate in copies)
        {
            var status = routes is null ? "calibration_missing" : "contact_unproved";
            int? arrival = null;
            var connectors = 0;
            var kind = string.Empty;
            if (routes is not null && candidate.OptionId is "social.talk_npc" or "social.gift_npc" &&
                candidate.Available && candidate.AllowedNow == true && candidate.AllowedToday == true &&
                candidate.BlockReasons.Length == 0 && Integer(candidate, "npc_tile_x", out var x) && Integer(candidate, "npc_tile_y", out var y))
            {
                var local = candidate.Kind is "social_talk_current" or "social_gift_current";
                if (local && Integer(candidate, "stand_tile_x", out var standX) && Integer(candidate, "stand_tile_y", out var standY))
                {
                    var contact = CurrentSocialContactCandidateAdapter.MatchedContacts(candidate, routes.Frontier, x, y, standX, standY)
                        .FirstOrDefault(row => row.LocationName == candidate.LocationId && row.EarliestInteractionTime >= time &&
                            row.EarliestInteractionTime < GameClockBudgetPolicy.AutonomousRecoveryStartTime);
                    if (contact is not null) { arrival = contact.EarliestInteractionTime; connectors = contact.RouteConnectorCount; kind = contact.RouteTimingEvidenceKind.ToString(); }
                }
                else if (candidate.Kind == "route_connector_tile")
                {
                    var budget = CurrentSocialContactCandidateAdapter.MatchedContacts(candidate, routes.Frontier, x, y, null, null)
                        .Select(contact => routes.Build(candidate, contact)).Where(row => row.FullContactBudgetVerified)
                        .OrderBy(row => row.ContactWindow!.EarliestInteractionTime).FirstOrDefault();
                    if (budget is not null)
                    {
                        arrival = budget.ContactWindow!.EarliestInteractionTime;
                        connectors = budget.RouteScenario!.ProducedPaths!.Single().Length;
                        kind = budget.ContactWindow.RouteTimingEvidenceKind.ToString();
                    }
                }
            }
            var numeric = new List<NumericFeature>();
            if (arrival.HasValue)
            {
                status = "verified_complete_contact_arrival_budget";
                numeric.Add(new() { Name = "social.contact.arrival_time", Value = arrival.Value });
                numeric.Add(new() { Name = "social.contact.arrival_budget_minutes", Value = GameClockBudgetPolicy.ClockMinutesBetween(time, arrival.Value) });
                numeric.Add(new() { Name = "social.contact.connector_count", Value = connectors });
            }
            candidate.PolicyFeatures = new()
            {
                Numeric = numeric.ToArray(),
                Boolean = new[] { new BooleanFeature { Name = "social.contact.budget_verified", Value = arrival.HasValue } },
                Categorical = new[]
                {
                    new CategoricalFeature { Name = "social.contact.budget_status", Value = status },
                    new CategoricalFeature { Name = "social.contact.timing_kind", Value = kind.Length == 0 ? "unknown" : kind }
                }
            };
        }
        return copies;
    }

    private static PolicyEventCandidatePrediction[] Clone(IEnumerable<PolicyEventCandidatePrediction> candidates) =>
        candidates.Select(candidate => JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(JsonSerializer.Serialize(candidate, JsonOptions), JsonOptions)!).ToArray();

    private static bool Integer(PolicyEventCandidatePrediction candidate, string name, out int value)
    {
        var matches = candidate.Parameters.Where(parameter => parameter.Name == name).ToArray();
        value = 0;
        return matches.Length == 1 && int.TryParse(matches[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
