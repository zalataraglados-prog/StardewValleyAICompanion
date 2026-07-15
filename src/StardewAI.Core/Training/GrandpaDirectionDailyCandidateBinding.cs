using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.WorldModel;

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
                "grandpa_four_candles_year3",
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

            if (catalogEntry.CcJojaSensitive)
            {
                var result = BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { "cc_joja_route_commitment_unavailable" },
                    "cc_joja_route_commitment_unresolved",
                    direction);
                result.Audit.CcJojaRouteCommitmentResolved = false;
                result.Audit.DirectionRejectedReason = "cc_joja_route_commitment_unresolved";
                return result;
            }

            if (!catalogEntry.DirectBindingEnabled)
            {
                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    new[] { catalogEntry.BlockReasonTemplate },
                    "direct_binding_disabled_planned_contract_gap",
                    direction);
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
                return BuildBlocked(
                    request,
                    snapshot,
                    catalogEntry,
                    reasons.ToArray(),
                    "no_current_permitted_candidate",
                    direction);
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
                    CcJojaRouteCommitmentResolved = false
                }
            };
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
