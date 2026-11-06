using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using static StardewAI.Core.Training.CurrentSocialContactCandidateAdapter;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMarriageTeacherPreferenceBuilder
{
    private static CurrentMarriageTeacherCandidate Evaluate(PolicyEventCandidatePrediction candidate, CurrentMarriageTeacherPreference report,
        SnapshotEnvelope snapshot, CurrentSocialRouteContactBudgetBuilder routes, CurrentMarriageTeacherResources resources, int time, int minutes, int energy)
    {
        var row = new CurrentMarriageTeacherCandidate { CandidateId = candidate.CandidateId, NpcName = Parameter(candidate, "npc_name") ?? string.Empty,
            SourceStateHash = snapshot.StateHash, RegisteredLedgerSha256 = report.LedgerSha256 };
        CurrentMarriageTeacherCandidate Excluded(params string[] reasons) { row.Reasons = reasons; return row; }
        if (candidate.OptionId != "social.gift_npc" || candidate.Kind is not ("social_gift_current" or "route_connector_tile"))
            return Excluded("original_other_family_retained_not_a_negative_label");
        CertifyCurrentContact(candidate, snapshot, routes.Frontier, time, minutes, energy, out var admissionReasons);
        if (!candidate.Available || candidate.AllowedNow != true || candidate.AllowedToday != true || candidate.TimelineStatus != "ready_now" ||
            candidate.BlockReasons.Length > 0 || candidate.Parameters.GroupBy(value => value.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
            return Excluded(admissionReasons.DefaultIfEmpty("marriage_gift_current_contact_not_admitted").ToArray());
        if (!Integer(candidate, "friendship_points_before", out var before) || !Integer(candidate, "expected_friendship_delta", out var delta) ||
            !Integer(candidate, "expected_friendship_points_after", out var after) || before < 0 || before >= 2500 || delta <= 0 ||
            (long)before + delta != after || after < 2500 || after > 999999 ||
            Parameter(candidate, "gift_side_effect_risk") != "none_identified_from_transparent_branch" ||
            Parameter(candidate, "gift_updates_normal_limits") != "true" ||
            !Integer(candidate, "item_quality", out var quality) || !Integer(candidate, "item_stack_before", out var stack) ||
            !candidate.SlotIndex.HasValue || string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            return Excluded("marriage_positive_native_proposal_threshold_or_normal_gift_effect_unproven");
        var proposals = report.Intake.Availability.Options.Single(option => option.OptionId == "social.advance_partnership").SocialCandidates
            .Where(value => Parameter(value, "npc_name") == row.NpcName && Parameter(value, "partnership_action_kind") == "propose_marriage").ToArray();
        if (proposals.Length != 1 || proposals[0].Available || !proposals[0].BlockReasons.SequenceEqual(new[] { "partnership_marriage_proposal_requires_2500_points" }) ||
            Parameter(proposals[0], "friendship_points_before") != before.ToString(CultureInfo.InvariantCulture))
            return Excluded("marriage_original_proposal_has_other_or_ambiguous_unresolved_gates");
        if (!resources.HasUnreservedProposalItem(proposals[0])) return Excluded("marriage_proposal_item_unavailable_after_registered_reservations");
        if (!resources.TryGift(candidate.SlotIndex.Value, candidate.QualifiedItemId, quality, stack, out var cost, out var resourceReason)) return Excluded(resourceReason);
        if (!Integer(candidate, "npc_tile_x", out var x) || !Integer(candidate, "npc_tile_y", out var y)) return Excluded("marriage_contact_geometry_missing");
        CurrentSocialContactOpportunity? contact;
        if (candidate.Kind == "route_connector_tile")
        {
            var proofs = MatchedContacts(candidate, routes.Frontier, x, y, null, null)
                .Select(value => (Contact: value, Budget: routes.Build(candidate, value))).ToArray();
            var chosen = proofs.Where(value => value.Budget.FullContactBudgetVerified).OrderBy(value => value.Budget.ContactWindow!.EarliestInteractionTime)
                .ThenBy(value => value.Contact.OpportunityId, StringComparer.Ordinal).FirstOrDefault();
            if (chosen.Budget is null) return Excluded(proofs.SelectMany(value => value.Budget.BlockingReasons)
                .Distinct(StringComparer.Ordinal).DefaultIfEmpty("marriage_no_complete_contact_budget_for_actual_connector").ToArray());
            contact = chosen.Contact;
            row.RouteContactBudget = chosen.Budget;
        }
        else
        {
            if (!Integer(candidate, "stand_tile_x", out var standX) || !Integer(candidate, "stand_tile_y", out var standY)) return Excluded("marriage_contact_stand_missing");
            contact = MatchedContacts(candidate, routes.Frontier, x, y, standX, standY).FirstOrDefault();
        }
        if (contact is null) return Excluded("marriage_exact_contact_window_unavailable");
        row.ContactArrivalTime = row.RouteContactBudget?.ContactWindow?.EarliestInteractionTime ?? contact.EarliestInteractionTime;
        var wedding = report.Settlement.ConditionalProposalWeddingTotalDay;
        var house = report.Settlement.ExistingHouseSettlementTotalDay;
        if (!wedding.HasValue || !house.HasValue || Math.Max(wedding.Value, house.Value) >= report.Settlement.DeadlineTotalDayExclusive)
            return Excluded("marriage_joint_conditional_settlement_before_deadline_unproven");
        var compiled = CompileCandidate(candidate, snapshot, minutes, energy);
        if (compiled.Reasons.Length > 0) return Excluded(compiled.Reasons);
        row.CompiledMinutes = compiled.Plan.CandidateAudit.Single(value => value.Decision == "accepted").CandidateMinutes;
        row.EnergyCost = candidate.EnergyCost;
        row.GiftSaleValue = cost;
        row.GiftSlotIndex = candidate.SlotIndex.Value;
        row.GiftQualifiedItemId = candidate.QualifiedItemId;
        row.FriendshipPointsBefore = before;
        row.FriendshipPointsAfter = after;
        row.ConditionalJointSettlementTotalDay = Math.Max(wedding.Value, house.Value);
        row.OpportunityId = contact.OpportunityId;
        row.TimingEvidenceId = contact.TimingEvidenceId;
        row.ProposalSourceCandidateId = proposals[0].CandidateId;
        row.NativeEffectSignature = JsonSerializer.Serialize(new { row.NpcName, before, after,
            row.ConditionalJointSettlementTotalDay, LimitEffect = "one_normal_gift", contact.LocationName, x, y });
        row.NativeActionIdentity = JsonSerializer.Serialize(new { candidate.OptionId, row.NpcName, candidate.QualifiedItemId, candidate.SlotIndex, quality,
            contact.LocationName, x, y, FirstEdge = row.RouteContactBudget?.RequiredFirstEdge });
        row.Status = "admitted_current_partner_preparation";
        return row;
    }

    private static string? Parameter(PolicyEventCandidatePrediction candidate, string name)
    {
        var matches = candidate.Parameters.Where(value => value.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : null;
    }
    private static string? Parameter(EventCandidate candidate, string name)
    {
        var matches = candidate.Parameters.Where(value => value.Name == name).ToArray();
        return matches.Length == 1 ? matches[0].Value : null;
    }
    private static bool Integer(PolicyEventCandidatePrediction candidate, string name, out int value) =>
        int.TryParse(Parameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
