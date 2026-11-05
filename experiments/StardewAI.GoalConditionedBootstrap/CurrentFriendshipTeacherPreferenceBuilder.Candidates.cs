using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentFriendshipTeacherPreferenceBuilder
{
    private static CurrentFriendshipTeacherCandidate Evaluate(
        PolicyEventCandidatePrediction source, PolicyEventCandidatePrediction? bound,
        SnapshotEnvelope snapshot, CurrentFriendshipDeficit deficit, CurrentSocialContactFrontier frontier,
        int currentTime, int minutes, int availableEnergy, string[] admissionReasons)
    {
        var row = new CurrentFriendshipTeacherCandidate
        {
            CandidateId = source.CandidateId, OptionId = source.OptionId, Kind = source.Kind,
            NpcName = Parameter(source, "npc_name") ?? string.Empty,
            EnergyCost = source.EnergyCost,
            AvailableCurrentSource = source.Available && source.AllowedNow == true && source.AllowedToday == true &&
                source.TimelineStatus == "ready_now" && source.BlockReasons.Length == 0
        };
        row.ComparisonRequired = source.Available && source.TimelineStatus == "ready_now" &&
            source.BlockReasons.Length == 0 && source.AllowedNow != false && source.AllowedToday != false;
        if (!row.AvailableCurrentSource)
            return Excluded(row, new[] { "unavailable_or_not_current_no_negative_label" }.Concat(admissionReasons).ToArray());
        var native = deficit.Villagers.Where(value => value.NpcName == row.NpcName).ToArray();
        if (row.NpcName.Length > 0 && (native.Length == 0 || native.All(value => value.Qualifies)))
        {
            row.ComparisonRequired = false;
            return Excluded(row, "no_current_native_Grandpa_target_deficit");
        }
        if (native.Length != 1 || !native[0].UniqueNpcIdentity || bound is null)
            return Excluded(row, "original_friendship_binding_or_unique_native_target_unavailable");
        if (bound.Parameters.GroupBy(parameter => parameter.Name, StringComparer.Ordinal).Any(group => group.Count() > 1) ||
            !Integer(bound, "friendship_points_before", out var before) ||
            !Integer(bound, "expected_friendship_delta", out var delta) ||
            !Integer(bound, "expected_friendship_points_after", out var after) ||
            before < 0 || before >= 1975 || delta <= 0 || (long)before + delta != after || after > 999999 ||
            !bool.TryParse(Parameter(bound, "friendship_row_exists_before"), out var exists) ||
            exists != native[0].FriendshipRowExists || before != (native[0].FriendshipPoints ?? 0))
            return Excluded(row, "native_friendship_transition_missing_or_inconsistent");
        row.FriendshipPointsBefore = before;
        row.ExpectedFriendshipPointsAfter = after;
        row.ExpectedNewQualifiers = after >= 1975 ? 1 : 0;
        row.UsefulFriendshipDelta = Math.Min(1975, after) - before;
        var interaction = bound.OptionId == "social.talk_npc" ? "talk" : "gift";
        if (!Integer(bound, "npc_tile_x", out var x) || !Integer(bound, "npc_tile_y", out var y) ||
            !Integer(bound, "stand_tile_x", out var standX) || !Integer(bound, "stand_tile_y", out var standY))
            return Excluded(row, "native_social_geometry_unavailable");
        var opportunity = MatchedContacts(bound, frontier, x, y, standX, standY).FirstOrDefault();
        if (opportunity is null)
            return Excluded(row, "no_exact_current_geometry_calibrated_contact_budget");
        row.ContactBudgetArrivalTime = opportunity.EarliestInteractionTime;
        row.TimingEvidenceId = opportunity.TimingEvidenceId;
        row.TimingEvidenceKind = opportunity.RouteTimingEvidenceKind.ToString();
        row.OpportunityId = opportunity.OpportunityId;
        if (interaction == "gift")
        {
            row.GiftQualifiedItemId = opportunity.GiftQualifiedItemId;
            row.GiftQuality = opportunity.GiftQuality;
            row.GiftSlotIndex = opportunity.GiftSlotIndex;
            row.GiftNormalLimitEffect = Parameter(bound, "gift_updates_normal_limits") ?? string.Empty;
            if (!bool.TryParse(row.GiftNormalLimitEffect, out _) || !row.GiftSlotIndex.HasValue || !row.GiftQuality.HasValue)
                return Excluded(row, "gift_resource_or_limit_effect_unavailable");
        }
        if (row.EnergyCost < 0 || row.ContactBudgetArrivalTime < currentTime ||
            row.ContactBudgetArrivalTime >= GameClockBudgetPolicy.AutonomousRecoveryStartTime)
            return Excluded(row, "current_social_day_budget_unavailable_or_recovery_due");
        var plan = new DailyPlanCompiler().Compile(new[] { bound }, snapshot.StateHash,
            goalId: "goal.social.raise_friendships", maxCandidates: 1,
            availableMinutes: minutes, energyBudget: availableEnergy);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var diagnostics = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") || diagnostics.Length > 0)
            return Excluded(row, diagnostics.DefaultIfEmpty("original_social_compilation_not_dispatchable").ToArray());
        row.CompiledMinutes = plan.CandidateAudit.Single(audit => audit.Decision == "accepted").CandidateMinutes;
        row.NativeActionIdentity = JsonSerializer.Serialize(new
        {
            row.OptionId, row.NpcName, Location = Parameter(bound, "target_location"), x, y,
            row.GiftQualifiedItemId, row.GiftQuality, row.GiftSlotIndex
        });
        row.Status = "admitted_current_social_comparison";
        return row;
    }

    private static string? Parameter(PolicyEventCandidatePrediction candidate, string name)
    {
        var matches = candidate.Parameters.Where(parameter => parameter.Name == name).ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].Value) ? matches[0].Value : null;
    }

    private static PolicyEventCandidatePrediction CertifyCurrentContact(
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
        var diagnostics = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") ||
            diagnostics.Length > 0)
        {
            reasons = diagnostics.DefaultIfEmpty("current_contact_compiler_not_dispatchable").ToArray();
            return candidate;
        }
        candidate.AllowedNow = true;
        candidate.AllowedToday = true;
        candidate.Parameters = candidate.Parameters.Concat(new[]
        {
            new StardewAI.Contracts.Execution.SmallModelActionParameter
                { Name = "friendship_current_admission_state_hash", Value = snapshot.StateHash },
            new StardewAI.Contracts.Execution.SmallModelActionParameter
                { Name = "friendship_current_admission_timing_evidence", Value = contact.TimingEvidenceId },
            new StardewAI.Contracts.Execution.SmallModelActionParameter
                { Name = "friendship_current_admission_contact_time", Value = contact.EarliestInteractionTime.ToString(CultureInfo.InvariantCulture) }
        }).ToArray();
        return candidate;
    }

    private static IEnumerable<CurrentSocialContactOpportunity> MatchedContacts(
        PolicyEventCandidatePrediction candidate, CurrentSocialContactFrontier frontier,
        int x, int y, int standX, int standY)
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
                opportunity.StandTileX == standX && opportunity.StandTileY == standY &&
                (!gift || opportunity.GiftSlotIndex == candidate.SlotIndex &&
                    opportunity.GiftQualifiedItemId == candidate.QualifiedItemId && opportunity.ExpectedFriendshipDelta == delta))
            .OrderBy(opportunity => opportunity.EarliestInteractionTime)
            .ThenBy(opportunity => opportunity.RouteWaitGameMinutes)
            .ThenBy(opportunity => opportunity.RouteConnectorCount)
            .ThenBy(opportunity => opportunity.OpportunityId, StringComparer.Ordinal);
    }

    private static bool Integer(PolicyEventCandidatePrediction candidate, string name, out int value) =>
        int.TryParse(Parameter(candidate, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static CurrentFriendshipTeacherCandidate Excluded(CurrentFriendshipTeacherCandidate row, params string[] reasons)
    {
        row.Reasons = reasons;
        return row;
    }
}
