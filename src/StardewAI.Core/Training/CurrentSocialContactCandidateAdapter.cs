using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Training;

public static class CurrentSocialContactCandidateAdapter
{
    public static PolicyEventCandidatePrediction CertifyCurrentContact(
        PolicyEventCandidatePrediction candidate, SnapshotEnvelope snapshot, CurrentSocialContactFrontier frontier,
        int currentTime, int minutes, int energy, out string[] reasons)
    {
        reasons = Array.Empty<string>();
        if (!candidate.Available || candidate.TimelineStatus != "ready_now" || candidate.BlockReasons.Length > 0 ||
            candidate.AllowedNow == false || candidate.AllowedToday == false ||
            candidate.AllowedNow == true && candidate.AllowedToday == true ||
            candidate.Kind is not ("social_talk_current" or "social_gift_current") ||
            candidate.Parameters.Any(parameter => parameter.Name.StartsWith("friendship_current_admission_", StringComparison.Ordinal)) ||
            !Integer(candidate, "npc_tile_x", out var x) || !Integer(candidate, "npc_tile_y", out var y) ||
            !Integer(candidate, "stand_tile_x", out var standX) || !Integer(candidate, "stand_tile_y", out var standY))
            return candidate;
        var contact = MatchedContacts(candidate, frontier, x, y, standX, standY).Where(opportunity =>
                opportunity.LocationName == candidate.LocationId &&
                opportunity.NpcPresentFromTime <= currentTime && opportunity.InteractionEligibleFromTime <= currentTime &&
                opportunity.EarliestInteractionTime >= currentTime &&
                opportunity.EarliestInteractionTime < GameClockBudgetPolicy.AutonomousRecoveryStartTime).FirstOrDefault();
        if (contact is null)
        {
            reasons = new[] { "calibrated_current_contact_window_absent" };
            return candidate;
        }
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, snapshot.StateHash,
            goalId: "goal.social.raise_friendships", maxCandidates: 1, availableMinutes: minutes, energyBudget: energy);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var diagnostics = plan.CandidateAudit.Where(row => row.Decision != "accepted").SelectMany(row => row.Reasons)
            .Concat(queue.CompilerDiagnostics)
            .Concat(queue.Items.SelectMany(row => row.BlockingReasons.Concat(row.MissingStateFactors)))
            .Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).ToArray();
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") || diagnostics.Length > 0)
        {
            reasons = diagnostics.DefaultIfEmpty("current_contact_compiler_not_dispatchable").ToArray();
            return candidate;
        }
        candidate.AllowedNow = true;
        candidate.AllowedToday = true;
        candidate.Parameters = candidate.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "friendship_current_admission_state_hash", Value = snapshot.StateHash },
            new SmallModelActionParameter { Name = "friendship_current_admission_timing_evidence", Value = contact.TimingEvidenceId },
            new SmallModelActionParameter { Name = "friendship_current_admission_contact_time", Value = contact.EarliestInteractionTime.ToString(CultureInfo.InvariantCulture) }
        }).ToArray();
        return candidate;
    }

    public static IEnumerable<CurrentSocialContactOpportunity> MatchedContacts(
        PolicyEventCandidatePrediction candidate, CurrentSocialContactFrontier frontier,
        int x, int y, int? standX, int? standY)
    {
        var gift = candidate.OptionId == "social.gift_npc";
        var npcName = Parameter(candidate, "npc_name");
        var location = Parameter(candidate, "target_location");
        Integer(candidate, "expected_friendship_delta", out var delta);
        return frontier.Opportunities.Where(opportunity => opportunity.ExecutionReady &&
                opportunity.NpcName == npcName &&
                opportunity.InteractionKind == (gift ? "gift" : "talk") &&
                opportunity.LocationName == location &&
                opportunity.NpcTileX == x && opportunity.NpcTileY == y &&
                (!standX.HasValue || opportunity.StandTileX == standX) && (!standY.HasValue || opportunity.StandTileY == standY) &&
                (!gift || opportunity.GiftSlotIndex == candidate.SlotIndex &&
                    opportunity.GiftQualifiedItemId == candidate.QualifiedItemId && opportunity.ExpectedFriendshipDelta == delta))
            .OrderBy(opportunity => opportunity.EarliestInteractionTime)
            .ThenBy(opportunity => opportunity.RouteWaitGameMinutes)
            .ThenBy(opportunity => opportunity.RouteConnectorCount)
            .ThenBy(opportunity => opportunity.OpportunityId, StringComparer.Ordinal);
    }

    private static string? Parameter(PolicyEventCandidatePrediction candidate, string name)
    {
        var matches = candidate.Parameters.Where(parameter => parameter.Name == name).ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].Value) ? matches[0].Value : null;
    }

    private static bool Integer(PolicyEventCandidatePrediction candidate, string name, out int value) =>
        int.TryParse(Parameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
