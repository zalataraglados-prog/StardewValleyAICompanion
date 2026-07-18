using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaDirectionDailyCandidateBinding
    {
        private readonly GrandpaTrainingSampleAdapter adapter;
        private readonly WorldModelProjector projector;
        private readonly GrandpaEvaluationGoalEvaluator evaluator;
        private readonly IReadOnlyDictionary<string, GrandpaDirectionCatalogEntry> catalogByDirectionId;

        public GrandpaDirectionDailyCandidateBinding()
            : this(
                new GrandpaTrainingSampleAdapter(),
                new WorldModelProjector(),
                new GrandpaEvaluationGoalEvaluator())
        {
        }

        public GrandpaDirectionDailyCandidateBinding(
            GrandpaTrainingSampleAdapter adapter,
            WorldModelProjector projector,
            GrandpaEvaluationGoalEvaluator evaluator)
        {
            this.adapter = adapter;
            this.projector = projector;
            this.evaluator = evaluator;
            catalogByDirectionId = GrandpaDirectionCatalog.Entries.ToDictionary(
                entry => entry.DirectionId,
                StringComparer.Ordinal);
        }

        public GrandpaDirectionBindingResult Bind(GrandpaDirectionBindingRequest request, SnapshotEnvelope? snapshot)
        {
            if (string.IsNullOrWhiteSpace(request.StateHash))
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = request.DirectionId,
                    SourceStateHash = string.Empty,
                    BindingStatus = "blocked",
                    BlockReasons = new[] { "state_hash_is_empty" },
                    BindingCoverageStatus = "blocked",
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = false,
                        StateHashEmptyOrUnknown = true,
                        DirectionSetRebuiltFromSnapshot = false,
                        DirectionRejectedReason = "state_hash_is_empty"
                    }
                };
            }

            if (snapshot is null)
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = request.DirectionId,
                    SourceStateHash = request.StateHash,
                    BindingStatus = "blocked",
                    BlockReasons = new[] { "state_hash_unknown_backend_resolves_no_snapshot" },
                    BindingCoverageStatus = "blocked",
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = false,
                        StateHashEmptyOrUnknown = true,
                        DirectionSetRebuiltFromSnapshot = false,
                        DirectionRejectedReason = "state_hash_unknown"
                    }
                };
            }

            if (!string.Equals(request.StateHash, snapshot.StateHash, StringComparison.Ordinal))
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = request.DirectionId,
                    SourceStateHash = request.StateHash,
                    BindingStatus = "blocked",
                    BlockReasons = new[] { "state_hash_mismatch_request_state_hash_does_not_match_snapshot_state_hash" },
                    BindingCoverageStatus = "blocked",
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = false,
                        StateHashEmptyOrUnknown = false,
                        DirectionSetRebuiltFromSnapshot = false,
                        DirectionRejectedReason = "state_hash_mismatch"
                    }
                };
            }

            if (string.IsNullOrWhiteSpace(request.DirectionId))
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = string.Empty,
                    SourceStateHash = snapshot.StateHash,
                    BindingStatus = "blocked",
                    BlockReasons = new[] { "direction_id_is_empty" },
                    BindingCoverageStatus = "blocked",
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = true,
                        DirectionSetRebuiltFromSnapshot = false,
                        DirectionRejectedReason = "direction_id_is_empty"
                    }
                };
            }

            if (!catalogByDirectionId.TryGetValue(request.DirectionId, out var catalogEntry))
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = request.DirectionId,
                    SourceStateHash = snapshot.StateHash,
                    BindingStatus = "blocked",
                    BlockReasons = new[] { "unknown_direction_id:" + request.DirectionId },
                    BindingCoverageStatus = "blocked",
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = true,
                        DirectionSetRebuiltFromSnapshot = false,
                        DirectionRejectedReason = "unknown_direction_id:" + request.DirectionId
                    }
                };
            }

            var worldModel = projector.Project(
                snapshot,
                GrandpaEvaluationGoalDefinition.StrategicGoal,
                "strategic");

            var report = evaluator.Evaluate(worldModel);
            var sample = adapter.Build(worldModel, report);
            var direction = sample.CandidateDirections
                .FirstOrDefault(c => string.Equals(c.DirectionId, request.DirectionId, StringComparison.Ordinal));

            if (direction is null)
            {
                if (sample.Target.Complete)
                {
                    return new GrandpaDirectionBindingResult
                    {
                        DirectionId = request.DirectionId,
                        SourceStateHash = snapshot.StateHash,
                        BindingStatus = "blocked",
                        TargetAlreadyComplete = true,
                        BlockReasons = new[] { "target_already_complete" },
                        BindingCoverageStatus = "blocked",
                        BindingRuleId = catalogEntry.BindingRuleId,
                        Audit = new GrandpaDirectionBindingAudit
                        {
                            StateHashVerified = true,
                            DirectionSetRebuiltFromSnapshot = true,
                            DirectionRejectedReason = "target_already_complete",
                            CcJojaRouteCommitmentResolved = false
                        }
                    };
                }

                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { "direction_absent_from_snapshot_candidate_set" },
                    "direction_absent_from_snapshot_candidate_set",
                    direction: null);
            }

            var sourceMetadata = SourceMetadata(direction);

            if (!direction.Known)
            {
                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { "direction_has_unknown_factors" },
                    "direction_has_unknown_factors",
                    direction);
            }

            if (direction.Blocked)
            {
                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    direction.BlockReasons.Length > 0
                        ? direction.BlockReasons
                        : new[] { "direction_blocked" },
                    "direction_blocked",
                    direction);
            }

            if (direction.PotentialPoints <= 0)
            {
                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { "direction_has_no_potential_points" },
                    "direction_has_no_potential_points",
                    direction);
            }

            if (sample.Target.Complete)
            {
                return new GrandpaDirectionBindingResult
                {
                    DirectionId = request.DirectionId,
                    SourceStateHash = snapshot.StateHash,
                    BindingStatus = "blocked",
                    TargetAlreadyComplete = true,
                    BlockReasons = new[] { "target_already_complete" },
                    DirectionDomain = sourceMetadata.Domain,
                    DirectionLabel = sourceMetadata.Label,
                    RelatedFactorIds = sourceMetadata.RelatedFactorIds,
                    PotentialPoints = sourceMetadata.PotentialPoints,
                    DirectionPriorityScore = sourceMetadata.PriorityScore,
                    DirectionKnown = sourceMetadata.Known,
                    DirectionBlocked = sourceMetadata.Blocked,
                    FeedbackKey = sourceMetadata.FeedbackKey,
                    DirectionHorizonRequiredMinutes = sourceMetadata.HorizonRequiredMinutes,
                    BindingCoverageStatus = "blocked",
                    BindingRuleId = catalogEntry.BindingRuleId,
                    Audit = new GrandpaDirectionBindingAudit
                    {
                        StateHashVerified = true,
                        DirectionSetRebuiltFromSnapshot = true,
                        DirectionRejectedReason = "target_already_complete",
                        CcJojaRouteCommitmentResolved = false
                    }
                };
            }

            var ccJojaRouteCommitmentResolved = false;
            if (catalogEntry.CcJojaSensitive)
            {
                var communityCenter = ReadStateFieldValue(snapshot, "world_progress", "community_center");
                var routeState = communityCenter.HasValue && communityCenter.Value.ValueKind == JsonValueKind.Object
                    ? ReadString(communityCenter.Value, "route_state")
                    : string.Empty;
                if (routeState is not ("undecided" or "community_center_locked" or "joja_locked"))
                {
                    var result = BuildBlocked(
                        request,
                        snapshot,
                        catalogEntry,
                        new[] { routeState == "conflicting_irreversible_flags" ? "cc_joja_route_commitment_conflict" : "cc_joja_route_commitment_unavailable" },
                        routeState == "conflicting_irreversible_flags" ? "cc_joja_route_commitment_conflict" : "cc_joja_route_commitment_unresolved",
                        direction);
                    result.Audit.CcJojaRouteCommitmentResolved = false;
                    return result;
                }
                ccJojaRouteCommitmentResolved = true;
                var routeAllowsDirection = request.DirectionId == "complete_community_center"
                    ? routeState is "undecided" or "community_center_locked"
                    : routeState == "joja_locked";
                if (!routeAllowsDirection)
                {
                    var result = BuildBlocked(
                        request,
                        snapshot,
                        catalogEntry,
                        new[] { "cc_joja_direction_locked_out_by_irreversible_route" },
                        "cc_joja_direction_locked_out_by_irreversible_route",
                        direction);
                    result.Audit.CcJojaRouteCommitmentResolved = true;
                    return result;
                }
            }

            if (!catalogEntry.DirectBindingEnabled)
            {
                var result = BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { catalogEntry.BlockReasonTemplate },
                    "direct_binding_disabled_planned_contract_gap",
                    direction);
                result.Audit.CcJojaRouteCommitmentResolved = ccJojaRouteCommitmentResolved;
                return result;
            }

            var rankedCandidates = request.RankedCandidates ?? Array.Empty<PolicyEventCandidatePrediction>();
            var boundCandidates = new List<PolicyEventCandidatePrediction>();
            var rejectionDetails = new List<string>();

            foreach (var candidate in rankedCandidates)
            {
                if (candidate.TimelineStatus == "blocked")
                {
                    rejectionDetails.Add("candidate_blocked_timeline:" + candidate.CandidateId);
                    continue;
                }

                if (candidate.BlockReasons is not null && candidate.BlockReasons.Length > 0)
                {
                    rejectionDetails.Add("candidate_has_block_reasons:" + candidate.CandidateId);
                    continue;
                }

                if (!candidate.Available)
                {
                    rejectionDetails.Add("candidate_unavailable:" + candidate.CandidateId);
                    continue;
                }

                if (candidate.AllowedNow != true)
                {
                    rejectionDetails.Add("candidate_not_allowed_now:" + candidate.CandidateId);
                    continue;
                }

                if (candidate.AllowedToday != true)
                {
                    rejectionDetails.Add("candidate_not_allowed_today:" + candidate.CandidateId);
                    continue;
                }

                if (!catalogEntry.PermittedCandidateKinds.Contains(candidate.Kind, StringComparer.Ordinal))
                {
                    rejectionDetails.Add("candidate_kind_not_permitted:" + candidate.CandidateId + " kind=" + candidate.Kind);
                    continue;
                }

                if (!catalogEntry.PermittedOptionIds.Contains(candidate.OptionId, StringComparer.Ordinal))
                {
                    rejectionDetails.Add("candidate_option_id_not_permitted:" + candidate.CandidateId + " option_id=" + candidate.OptionId);
                    continue;
                }

                if (!HasDirectionSpecificEvidence(request.DirectionId, candidate, out var evidenceReason))
                {
                    rejectionDetails.Add("candidate_direction_evidence_rejected:" + candidate.CandidateId + ":" + evidenceReason);
                    continue;
                }

                var bound = CloneCandidate(candidate);

                var expectedProvenance = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "grandpa_direction_id", request.DirectionId },
                    { "grandpa_source_state_hash", snapshot.StateHash },
                    { "grandpa_related_factor_ids", string.Join(",", direction.RelatedFactorIds) },
                    { "grandpa_binding_rule_id", catalogEntry.BindingRuleId }
                };
                var provenanceNames = new HashSet<string>(expectedProvenance.Keys, StringComparer.Ordinal);
                var seenProvenanceNames = new HashSet<string>(StringComparer.Ordinal);
                var hasProvenanceConflict = false;
                foreach (var param in bound.Parameters ?? Array.Empty<SmallModelActionParameter>())
                {
                    if (expectedProvenance.ContainsKey(param.Name))
                    {
                        if (!seenProvenanceNames.Add(param.Name))
                        {
                            rejectionDetails.Add("candidate_provenance_duplicate:" + candidate.CandidateId + ":" + param.Name);
                            hasProvenanceConflict = true;
                            break;
                        }

                        if (!string.Equals(param.Value, expectedProvenance[param.Name], StringComparison.Ordinal))
                        {
                            rejectionDetails.Add("candidate_provenance_conflict:" + candidate.CandidateId + ":" + param.Name);
                            hasProvenanceConflict = true;
                            break;
                        }
                        provenanceNames.Remove(param.Name);
                    }
                }

                if (hasProvenanceConflict)
                {
                    continue;
                }

                var extendedParams = new List<SmallModelActionParameter>(bound.Parameters ?? Array.Empty<SmallModelActionParameter>());

                if (provenanceNames.Contains("grandpa_direction_id"))
                {
                    extendedParams.Add(Parameter("grandpa_direction_id", request.DirectionId));
                }

                if (provenanceNames.Contains("grandpa_source_state_hash"))
                {
                    extendedParams.Add(Parameter("grandpa_source_state_hash", snapshot.StateHash));
                }

                if (provenanceNames.Contains("grandpa_related_factor_ids"))
                {
                    extendedParams.Add(Parameter("grandpa_related_factor_ids", string.Join(",", direction.RelatedFactorIds)));
                }

                if (provenanceNames.Contains("grandpa_binding_rule_id"))
                {
                    extendedParams.Add(Parameter("grandpa_binding_rule_id", catalogEntry.BindingRuleId));
                }

                bound.Parameters = extendedParams.ToArray();
                boundCandidates.Add(bound);
            }

            if (boundCandidates.Count == 0)
            {
                var reasons = new List<string> { "no_current_permitted_candidate" };
                reasons.AddRange(rejectionDetails);
                var result = BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    reasons.ToArray(),
                    "no_current_permitted_candidate",
                    direction);
                result.Audit.CcJojaRouteCommitmentResolved = ccJojaRouteCommitmentResolved;
                return result;
            }

            return new GrandpaDirectionBindingResult
            {
                DirectionId = request.DirectionId,
                SourceStateHash = snapshot.StateHash,
                BindingStatus = "ready",
                BlockReasons = Array.Empty<string>(),
                MissingTransparentFields = Array.Empty<string>(),
                CoveredTransparentFields = (string[])catalogEntry.CoveredTransparentFields.Clone(),
                MissingCapabilities = Array.Empty<string>(),
                TargetAlreadyComplete = false,
                BoundCandidates = boundCandidates.ToArray(),
                DirectionDomain = sourceMetadata.Domain,
                DirectionLabel = sourceMetadata.Label,
                RelatedFactorIds = sourceMetadata.RelatedFactorIds,
                PotentialPoints = sourceMetadata.PotentialPoints,
                DirectionPriorityScore = sourceMetadata.PriorityScore,
                DirectionKnown = sourceMetadata.Known,
                DirectionBlocked = sourceMetadata.Blocked,
                FeedbackKey = sourceMetadata.FeedbackKey,
                DirectionHorizonRequiredMinutes = sourceMetadata.HorizonRequiredMinutes,
                BindingCoverageStatus = "ready",
                BindingRuleId = catalogEntry.BindingRuleId,
                Audit = new GrandpaDirectionBindingAudit
                {
                    StateHashVerified = true,
                    DirectionSetRebuiltFromSnapshot = true,
                    DirectionRejectedReason = string.Empty,
                    CcJojaRouteCommitmentResolved = ccJojaRouteCommitmentResolved
                }
            };
        }

        private static bool HasDirectionSpecificEvidence(
            string directionId,
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (string.Equals(directionId, "obtain_skull_key", StringComparison.Ordinal))
            {
                return HasSkullKeyAcquisitionEvidence(candidate, out rejectionReason);
            }
            if (string.Equals(directionId, "raise_skill_levels", StringComparison.Ordinal))
            {
                return HasSkillExperienceEvidence(candidate, out rejectionReason);
            }
            if (string.Equals(directionId, "earn_pet_love", StringComparison.Ordinal))
            {
                return HasPetLoveEvidence(candidate, out rejectionReason);
            }
            if (string.Equals(directionId, "complete_museum_collection", StringComparison.Ordinal))
            {
                return HasMuseumCollectionEvidence(candidate, out rejectionReason);
            }
            if (string.Equals(directionId, "obtain_rusty_key", StringComparison.Ordinal))
            {
                return HasRustyKeyDonationEvidence(candidate, out rejectionReason);
            }
            if (!string.Equals(directionId, "complete_full_shipment", StringComparison.Ordinal))
            {
                return true;
            }

            if (candidate.FullShipmentKnown != true)
            {
                rejectionReason = "full_shipment_evidence_unknown";
                return false;
            }

            if (candidate.FullShipmentEligible != true)
            {
                rejectionReason = "full_shipment_item_ineligible";
                return false;
            }

            if (candidate.FullShipmentCurrentShippedCount != 0)
            {
                rejectionReason = "full_shipment_current_shipped_count_not_zero";
                return false;
            }

            if (candidate.FullShipmentAlreadyShipped != false)
            {
                rejectionReason = "full_shipment_item_already_shipped_or_unknown";
                return false;
            }

            if (candidate.FullShipmentContributes != true)
            {
                rejectionReason = "full_shipment_candidate_does_not_contribute";
                return false;
            }

            if (!candidate.CanShip)
            {
                rejectionReason = "full_shipment_candidate_cannot_ship";
                return false;
            }

            return true;
        }

        private static bool HasPetLoveEvidence(
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadUniqueParameter(candidate, "pet_love_progress_delta", out var deltaText) ||
                !int.TryParse(deltaText, out var delta) || delta <= 0)
            {
                rejectionReason = "pet_love_positive_progress_missing";
                return false;
            }
            if (!TryReadUniqueParameter(candidate, "target_runtime_identity", out var petId) ||
                !Guid.TryParse(petId, out _))
            {
                rejectionReason = "pet_love_pet_identity_missing";
                return false;
            }
            if (candidate.Kind == "fill_pet_bowl" &&
                (!TryReadUniqueParameter(candidate, "delayed_settlement", out var settlement) ||
                 settlement != "Pet.dayUpdate consumes watered=true and applies min(1000,friendship+6)"))
            {
                rejectionReason = "pet_love_delayed_settlement_missing";
                return false;
            }
            return true;
        }

        private static bool HasMuseumCollectionEvidence(
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadUniqueIntParameter(candidate, "expected_donated_count_before", out var before) ||
                !TryReadUniqueIntParameter(candidate, "expected_donated_count_after", out var after) ||
                !TryReadUniqueIntParameter(candidate, "museum_total_donatable_items", out var total) ||
                before < 0 || after != before + 1 || after > total)
            {
                rejectionReason = "museum_collection_positive_progress_missing";
                return false;
            }
            return true;
        }

        private static bool HasRustyKeyDonationEvidence(
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadUniqueIntParameter(candidate, "expected_donated_count_before", out var before) ||
                !TryReadUniqueIntParameter(candidate, "expected_donated_count_after", out var after) ||
                !TryReadUniqueIntParameter(candidate, "rusty_key_donation_threshold", out var threshold) ||
                before < 0 || before >= threshold || after != before + 1 || after > threshold)
            {
                rejectionReason = "rusty_key_threshold_progress_missing";
                return false;
            }
            if (!TryReadUniqueParameter(candidate, "rusty_key_reward_action", out var action) ||
                action != "MarkEventSeen Host 295672")
            {
                rejectionReason = "rusty_key_native_reward_action_missing";
                return false;
            }
            return true;
        }

        private static bool TryReadUniqueIntParameter(
            PolicyEventCandidatePrediction candidate,
            string name,
            out int value)
        {
            value = 0;
            return TryReadUniqueParameter(candidate, name, out var text) && int.TryParse(text, out value);
        }

        private static bool HasSkillExperienceEvidence(
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadUniqueParameter(candidate, "skill_experience_projection_status", out var status) ||
                !IsCompleteSkillExperienceStatus(status))
            {
                rejectionReason = "skill_experience_projection_not_complete";
                return false;
            }
            if (!TryReadUniqueParameter(candidate, "skill_experience_condition", out _))
            {
                rejectionReason = "skill_experience_condition_missing_or_ambiguous";
                return false;
            }

            if (TryReadUniqueParameter(candidate, "expected_skill_experience_deltas_json", out var deltasJson))
            {
                return HasStructuredSkillExperienceEvidence(deltasJson, out rejectionReason);
            }

            if (TryReadUniqueParameter(candidate, "skill_experience_skill_id", out var skillId))
            {
                if (!IsVanillaSkillId(skillId))
                {
                    rejectionReason = "skill_experience_skill_id_invalid";
                    return false;
                }
                if (!TryReadPositiveExperienceBounds(
                    candidate,
                    "skill_experience_on_success_min",
                    "skill_experience_on_success_max",
                    out rejectionReason))
                {
                    return false;
                }
                return true;
            }

            var foragingValid = TryReadExperienceBounds(
                candidate,
                "foraging_experience_on_success_min",
                "foraging_experience_on_success_max",
                out var foragingMinimum,
                out var foragingMaximum);
            var farmingValid = TryReadExperienceBounds(
                candidate,
                "farming_experience_on_success_min",
                "farming_experience_on_success_max",
                out var farmingMinimum,
                out var farmingMaximum);
            if (!foragingValid || !farmingValid)
            {
                rejectionReason = "multi_skill_experience_bounds_missing_or_invalid";
                return false;
            }
            if (Math.Max(foragingMaximum, farmingMaximum) <= 0)
            {
                rejectionReason = "multi_skill_experience_not_positive";
                return false;
            }
            if (foragingMinimum < 0 || farmingMinimum < 0)
            {
                rejectionReason = "multi_skill_experience_minimum_negative";
                return false;
            }
            return true;
        }

        private static bool HasStructuredSkillExperienceEvidence(string json, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    rejectionReason = "structured_skill_experience_not_array";
                    return false;
                }

                var seenSkillIndexes = new HashSet<int>();
                var hasPositive = false;
                foreach (var row in document.RootElement.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object ||
                        !TryReadStructuredString(row, "skillId", "SkillId", out var rowSkillId) ||
                        !TryReadStructuredInt(row, "skillIndex", "SkillIndex", out var skillIndex) ||
                        !TryReadStructuredInt(row, "delta", "Delta", out var delta) ||
                        skillIndex is < 0 or > 5 || delta < 0 ||
                        !string.Equals(rowSkillId, SkillIdFromIndex(skillIndex), StringComparison.Ordinal) ||
                        !seenSkillIndexes.Add(skillIndex))
                    {
                        rejectionReason = "structured_skill_experience_row_invalid";
                        return false;
                    }
                    hasPositive |= delta > 0;
                }

                if (!hasPositive)
                {
                    rejectionReason = "multi_skill_experience_not_positive";
                    return false;
                }
                return true;
            }
            catch (JsonException)
            {
                rejectionReason = "structured_skill_experience_json_invalid";
                return false;
            }
        }

        private static bool TryReadStructuredString(JsonElement row, string camelName, string pascalName, out string value)
        {
            value = string.Empty;
            if ((!row.TryGetProperty(camelName, out var property) && !row.TryGetProperty(pascalName, out property)) ||
                property.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadStructuredInt(JsonElement row, string camelName, string pascalName, out int value)
        {
            value = 0;
            return (row.TryGetProperty(camelName, out var property) || row.TryGetProperty(pascalName, out property)) &&
                property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
        }

        private static string SkillIdFromIndex(int index) => index switch
        {
            0 => "farming",
            1 => "fishing",
            2 => "foraging",
            3 => "mining",
            4 => "combat",
            5 => "luck",
            _ => string.Empty
        };

        private static bool TryReadPositiveExperienceBounds(
            PolicyEventCandidatePrediction candidate,
            string minimumName,
            string maximumName,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadExperienceBounds(candidate, minimumName, maximumName, out _, out var maximum))
            {
                rejectionReason = "skill_experience_bounds_missing_or_invalid";
                return false;
            }
            if (maximum <= 0)
            {
                rejectionReason = "skill_experience_not_positive";
                return false;
            }
            return true;
        }

        private static bool TryReadExperienceBounds(
            PolicyEventCandidatePrediction candidate,
            string minimumName,
            string maximumName,
            out int minimum,
            out int maximum)
        {
            minimum = 0;
            maximum = 0;
            return TryReadUniqueParameter(candidate, minimumName, out var minimumText) &&
                TryReadUniqueParameter(candidate, maximumName, out var maximumText) &&
                int.TryParse(minimumText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out minimum) &&
                int.TryParse(maximumText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maximum) &&
                minimum >= 0 &&
                maximum >= minimum;
        }

        private static bool TryReadUniqueParameter(
            PolicyEventCandidatePrediction candidate,
            string name,
            out string value)
        {
            var values = (candidate.Parameters ?? Array.Empty<SmallModelActionParameter>())
                .Where(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                .Select(parameter => parameter.Value)
                .ToArray();
            value = values.Length == 1 ? values[0] : string.Empty;
            return values.Length == 1 && !string.IsNullOrWhiteSpace(value);
        }

        private static bool IsCompleteSkillExperienceStatus(string status)
        {
            return string.Equals(status, "exact", StringComparison.Ordinal) ||
                status.StartsWith("exact_", StringComparison.Ordinal);
        }

        private static bool IsVanillaSkillId(string skillId)
        {
            return skillId is "farming" or "mining" or "foraging" or "fishing" or "combat" or "luck";
        }

        private static bool HasSkullKeyAcquisitionEvidence(
            PolicyEventCandidatePrediction candidate,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            var required = new[]
            {
                (Name: "target_location_family", Value: "ordinary_mines"),
                (Name: "target_depth", Value: "120"),
                (Name: "required_terminal_interaction", Value: "skull_key_reward_chest"),
                (Name: "required_postcondition", Value: "player.has_skull_key=true"),
                (Name: "required_executor_profile", Value: "mining_perfect_executor"),
                (Name: "runtime_boundary", Value: "current_floor_step_executable")
            };
            foreach (var contract in required)
            {
                var values = candidate.Parameters
                    .Where(parameter => string.Equals(parameter.Name, contract.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(parameter => parameter.Value)
                    .ToArray();
                if (values.Length != 1)
                {
                    rejectionReason = "skull_key_contract_parameter_count_invalid:" + contract.Name;
                    return false;
                }
                if (!string.Equals(values[0], contract.Value, StringComparison.Ordinal))
                {
                    rejectionReason = "skull_key_contract_parameter_mismatch:" + contract.Name;
                    return false;
                }
            }

            return true;
        }

        private static DirectionMetadata SourceMetadata(CandidateDirection direction)
        {
            return new DirectionMetadata
            {
                Domain = direction.Domain,
                Label = direction.Label,
                RelatedFactorIds = direction.RelatedFactorIds,
                PotentialPoints = direction.PotentialPoints,
                PriorityScore = direction.PriorityScore,
                Known = direction.Known,
                Blocked = direction.Blocked,
                FeedbackKey = direction.FeedbackKey,
                HorizonRequiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(direction)
            };
        }

        private sealed class DirectionMetadata
        {
            public string Domain { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string[] RelatedFactorIds { get; set; } = Array.Empty<string>();
            public int PotentialPoints { get; set; }
            public double PriorityScore { get; set; }
            public bool Known { get; set; }
            public bool Blocked { get; set; }
            public string FeedbackKey { get; set; } = string.Empty;
            public int HorizonRequiredMinutes { get; set; }
        }

        private GrandpaDirectionBindingResult BuildBlocked(
            GrandpaDirectionBindingRequest request,
            SnapshotEnvelope snapshot,
            GrandpaDirectionCatalogEntry catalogEntry,
            string[] blockReasons,
            string directionRejectedReason,
            CandidateDirection? direction)
        {
            var sourceMetadata = direction is not null ? SourceMetadata(direction) : null;
            return new GrandpaDirectionBindingResult
            {
                DirectionId = request.DirectionId,
                SourceStateHash = snapshot.StateHash,
                BindingStatus = "blocked",
                BlockReasons = blockReasons,
                MissingTransparentFields = (string[])catalogEntry.RequiredTransparentFields.Clone(),
                CoveredTransparentFields = (string[])catalogEntry.CoveredTransparentFields.Clone(),
                MissingCapabilities = (string[])catalogEntry.RequiredCapabilities.Clone(),
                TargetAlreadyComplete = false,
                BoundCandidates = Array.Empty<PolicyEventCandidatePrediction>(),
                DirectionDomain = sourceMetadata?.Domain ?? string.Empty,
                DirectionLabel = sourceMetadata?.Label ?? string.Empty,
                RelatedFactorIds = sourceMetadata?.RelatedFactorIds ?? Array.Empty<string>(),
                PotentialPoints = sourceMetadata?.PotentialPoints ?? 0,
                DirectionPriorityScore = sourceMetadata?.PriorityScore ?? 0,
                DirectionKnown = sourceMetadata?.Known ?? false,
                DirectionBlocked = sourceMetadata?.Blocked ?? true,
                FeedbackKey = sourceMetadata?.FeedbackKey ?? string.Empty,
                DirectionHorizonRequiredMinutes = sourceMetadata?.HorizonRequiredMinutes ?? 0,
                BindingCoverageStatus = "blocked",
                BindingRuleId = catalogEntry.BindingRuleId,
                Audit = new GrandpaDirectionBindingAudit
                {
                    StateHashVerified = true,
                    DirectionSetRebuiltFromSnapshot = direction is not null,
                    DirectionRejectedReason = directionRejectedReason,
                    CcJojaRouteCommitmentResolved = false
                }
            };
        }

        private static PolicyEventCandidatePrediction CloneCandidate(PolicyEventCandidatePrediction source)
        {
            return new PolicyEventCandidatePrediction
            {
                CandidateId = source.CandidateId,
                OptionId = source.OptionId,
                Kind = source.Kind,
                Rank = source.Rank,
                Score = source.Score,
                ExpectedReward = source.ExpectedReward,
                Available = source.Available,
                ItemId = source.ItemId,
                QualifiedItemId = source.QualifiedItemId,
                DisplayName = source.DisplayName,
                ShopId = source.ShopId,
                SlotIndex = source.SlotIndex,
                Quantity = source.Quantity,
                UnitPrice = source.UnitPrice,
                TotalValue = source.TotalValue,
                CanShip = source.CanShip,
                CanShopSell = source.CanShopSell,
                FullShipmentKnown = source.FullShipmentKnown,
                FullShipmentEligible = source.FullShipmentEligible,
                FullShipmentCurrentShippedCount = source.FullShipmentCurrentShippedCount,
                FullShipmentAlreadyShipped = source.FullShipmentAlreadyShipped,
                FullShipmentContributes = source.FullShipmentContributes,
                LocationId = source.LocationId,
                TileX = source.TileX,
                TileY = source.TileY,
                ExpectedEffect = source.ExpectedEffect,
                EstimatedTicks = source.EstimatedTicks,
                EnergyCost = source.EnergyCost,
                AvailabilityClass = source.AvailabilityClass,
                AllowedNow = source.AllowedNow,
                AllowedToday = source.AllowedToday,
                NextOpenTime = source.NextOpenTime,
                EffectiveOpenTime = source.EffectiveOpenTime,
                ClosesAt = source.ClosesAt,
                WaitCost = source.WaitCost,
                GateReasons = source.GateReasons is not null
                    ? (string[])source.GateReasons.Clone()
                    : Array.Empty<string>(),
                BlockReasons = source.BlockReasons is not null
                    ? (string[])source.BlockReasons.Clone()
                    : Array.Empty<string>(),
                Parameters = source.Parameters is not null
                    ? CloneParameters(source.Parameters)
                    : Array.Empty<SmallModelActionParameter>(),
                TimelineStatus = source.TimelineStatus,
                ScheduledStartTime = source.ScheduledStartTime,
                ScheduledWaitCost = source.ScheduledWaitCost,
                TimelineReasons = source.TimelineReasons is not null
                    ? (string[])source.TimelineReasons.Clone()
                    : Array.Empty<string>()
            };
        }

        private static SmallModelActionParameter[] CloneParameters(SmallModelActionParameter[] source)
        {
            var result = new SmallModelActionParameter[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = new SmallModelActionParameter
                {
                    Name = source[i].Name,
                    Value = source[i].Value
                };
            }

            return result;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value
            };
        }
    }
}
