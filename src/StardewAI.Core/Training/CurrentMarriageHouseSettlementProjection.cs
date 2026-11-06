using System;

namespace StardewAI.Core.Training;

public sealed class CurrentMarriageHouseSettlementProjection
{
    public string SchemaVersion { get; set; } = "current_marriage_house_settlement_projection.v1";
    public string Status { get; set; } = "blocked";
    public string SourceStateHash { get; set; } = string.Empty;
    public int? CurrentTotalDay { get; set; }
    public int? ConditionalProposalWeddingTotalDay { get; set; }
    public int[] SkippedProposalWeddingDays { get; set; } = Array.Empty<int>();
    public int? ExistingPartnershipSettlementTotalDay { get; set; }
    public int? ExistingHouseSettlementTotalDay { get; set; }
    public int? ConditionalNextHousePurchaseSettlementTotalDay { get; set; }
    public int? ExistingJointSettlementTotalDay { get; set; }
    public bool? ConditionalProposalBeforeEvaluation { get; set; }
    public bool? ExistingJointBeforeEvaluation { get; set; }
    public int DeadlineTotalDayExclusive => 224;
    public string Scope => "native_calendar_and_existing_pending_state_not_proposal_authorization_or_complete_method_budget";
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    public bool ProposalLegalityVerified => false;
    public bool PurchaseLegalityVerified => false;
    public bool TrainingLabelEligible => false;
    public bool NativeOutcomeVerified => false;
}
