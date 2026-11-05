using System;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public sealed class CurrentSocialRouteContactBudget
    {
        public string Status { get; set; } = "blocked";
        public string SourceStateHash { get; set; } = string.Empty;
        public string CandidateId { get; set; } = string.Empty;
        public string OpportunityId { get; set; } = string.Empty;
        public string TimingEvidenceId { get; set; } = string.Empty;
        public TransparentRouteEdge? RequiredFirstEdge { get; set; }
        public FutureRouteAccessScenario? RouteScenario { get; set; }
        public NpcFutureContactWindow? ContactWindow { get; set; }
        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
        public bool FullContactBudgetVerified => Status == "verified_for_actual_first_connector" &&
            RequiredFirstEdge is not null && RouteScenario is not null && ContactWindow is not null &&
            BlockingReasons.Length == 0;
        public string Scope => "current_snapshot_complete_calibrated_contact_via_actual_first_connector_not_native_outcome";
    }
}
