using System;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Contracts.Training
{
    public sealed class GrandpaDirectionBindingRequest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "grandpa_direction_binding_request.v1";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("direction_id")]
        public string DirectionId { get; set; } = string.Empty;

        [JsonPropertyName("ranked_candidates")]
        public PolicyEventCandidatePrediction[] RankedCandidates { get; set; } = Array.Empty<PolicyEventCandidatePrediction>();
    }

    public sealed class GrandpaDirectionBindingResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "grandpa_direction_binding_result.v1";

        [JsonPropertyName("direction_id")]
        public string DirectionId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("binding_status")]
        public string BindingStatus { get; set; } = "blocked";

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("missing_transparent_fields")]
        public string[] MissingTransparentFields { get; set; } = Array.Empty<string>();

        [JsonPropertyName("covered_transparent_fields")]
        public string[] CoveredTransparentFields { get; set; } = Array.Empty<string>();

        [JsonPropertyName("missing_capabilities")]
        public string[] MissingCapabilities { get; set; } = Array.Empty<string>();

        [JsonPropertyName("target_already_complete")]
        public bool TargetAlreadyComplete { get; set; }

        [JsonPropertyName("bound_candidates")]
        public PolicyEventCandidatePrediction[] BoundCandidates { get; set; } = Array.Empty<PolicyEventCandidatePrediction>();

        [JsonPropertyName("direction_domain")]
        public string DirectionDomain { get; set; } = string.Empty;

        [JsonPropertyName("direction_label")]
        public string DirectionLabel { get; set; } = string.Empty;

        [JsonPropertyName("related_factor_ids")]
        public string[] RelatedFactorIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("potential_points")]
        public int PotentialPoints { get; set; }

        [JsonPropertyName("direction_priority_score")]
        public double DirectionPriorityScore { get; set; }

        [JsonPropertyName("direction_known")]
        public bool DirectionKnown { get; set; }

        [JsonPropertyName("direction_blocked")]
        public bool DirectionBlocked { get; set; }

        [JsonPropertyName("feedback_key")]
        public string FeedbackKey { get; set; } = string.Empty;

        [JsonPropertyName("direction_horizon_required_minutes")]
        public int DirectionHorizonRequiredMinutes { get; set; }

        [JsonPropertyName("binding_coverage_status")]
        public string BindingCoverageStatus { get; set; } = "blocked";

        [JsonPropertyName("binding_rule_id")]
        public string BindingRuleId { get; set; } = string.Empty;

        [JsonPropertyName("audit")]
        public GrandpaDirectionBindingAudit Audit { get; set; } = new();
    }

    public sealed class GrandpaDirectionBindingAudit
    {
        [JsonPropertyName("binder")]
        public string Binder { get; set; } = "StardewAI.Core.Training.GrandpaDirectionDailyCandidateBinding";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Conservative direct binding for seven evidence-backed directions: earn_money, raise_friendships, complete_master_angler, complete_full_shipment, obtain_skull_key, raise_skill_levels, and earn_pet_love. Direction-specific typed evidence is mandatory; five remaining directions are blocked as planned contract gaps.";

        [JsonPropertyName("catalog_version")]
        public string CatalogVersion { get; set; } = "grandpa_direction_catalog.v3";

        [JsonPropertyName("state_hash_verified")]
        public bool StateHashVerified { get; set; }

        [JsonPropertyName("state_hash_empty_or_unknown")]
        public bool StateHashEmptyOrUnknown { get; set; }

        [JsonPropertyName("direction_set_rebuilt_from_snapshot")]
        public bool DirectionSetRebuiltFromSnapshot { get; set; }

        [JsonPropertyName("direction_rejected_reason")]
        public string DirectionRejectedReason { get; set; } = string.Empty;

        [JsonPropertyName("cc_joja_route_commitment_resolved")]
        public bool CcJojaRouteCommitmentResolved { get; set; }
    }

}
