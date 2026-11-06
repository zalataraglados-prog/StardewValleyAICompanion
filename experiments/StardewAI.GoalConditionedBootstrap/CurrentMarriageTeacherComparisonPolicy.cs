using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentMarriageTeacherComparisonPolicy
{
    public static PolicyTeacherPreferencePair? Compare(CurrentMarriageTeacherCandidate preferred, CurrentMarriageTeacherCandidate alternative)
    {
        if (!Admitted(preferred) || !Admitted(alternative) || preferred.CandidateId == alternative.CandidateId ||
            preferred.NativeActionIdentity == alternative.NativeActionIdentity || preferred.NpcName != alternative.NpcName ||
            preferred.SourceStateHash != alternative.SourceStateHash || preferred.RegisteredLedgerSha256 != alternative.RegisteredLedgerSha256 ||
            preferred.FriendshipPointsBefore != alternative.FriendshipPointsBefore || preferred.FriendshipPointsAfter != alternative.FriendshipPointsAfter ||
            preferred.ConditionalJointSettlementTotalDay != alternative.ConditionalJointSettlementTotalDay ||
            preferred.NativeEffectSignature != alternative.NativeEffectSignature || preferred.TimingEvidenceId != alternative.TimingEvidenceId ||
            preferred.ContactArrivalTime > alternative.ContactArrivalTime || preferred.CompiledMinutes > alternative.CompiledMinutes ||
            preferred.EnergyCost > alternative.EnergyCost || preferred.GiftSaleValue > alternative.GiftSaleValue) return null;
        var criterion = preferred.GiftSaleValue < alternative.GiftSaleValue ? "same_native_effect_lower_unreserved_gift_liquidation_cost" :
            preferred.ContactArrivalTime < alternative.ContactArrivalTime ? "same_native_effect_lower_complete_contact_budget" :
            preferred.CompiledMinutes < alternative.CompiledMinutes ? "same_native_effect_lower_compiled_minutes" :
            preferred.EnergyCost < alternative.EnergyCost ? "same_native_effect_lower_energy" : null;
        return criterion is null ? null : new PolicyTeacherPreferencePair
        {
            PreferredCandidateId = preferred.CandidateId, AlternativeCandidateId = alternative.CandidateId,
            FirstDifferingAuthoritativeCriterion = criterion, LabelSemantics = "preferred_over_current_eligible_counterfactual"
        };
    }

    private static bool Admitted(CurrentMarriageTeacherCandidate row) => row.Status == "admitted_current_partner_preparation" &&
        row.CandidateId.Length > 0 && row.SourceStateHash.Length > 0 && row.RegisteredLedgerSha256.Length > 0 && row.OpportunityId.Length > 0 &&
        row.ProposalSourceCandidateId.Length > 0 && row.NativeActionIdentity.Length > 0 && row.NativeEffectSignature.Length > 0 && row.TimingEvidenceId.Length > 0 &&
        row.FriendshipPointsBefore is >= 0 and < 2500 && row.FriendshipPointsAfter >= 2500 &&
        row.FriendshipPointsAfter <= 999999 && row.GiftSaleValue >= 0 && row.GiftSlotIndex >= 0 && row.GiftQualifiedItemId.Length > 0 &&
        row.ConditionalJointSettlementTotalDay is >= 0 and < 224 && row.CompiledMinutes > 0 && row.EnergyCost >= 0 &&
        row.ContactArrivalTime >= 600 && row.ContactArrivalTime < 2600 && row.ContactArrivalTime % 100 < 60;
}
