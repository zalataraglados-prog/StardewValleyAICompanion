using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    internal static PolicyEventCandidatePrediction[]
        RebuildVerifiedCurrentCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger ledger,
            string goalId,
            AcquisitionRouteTargetDateUnlock requirement,
            string[] endpointOptionIds,
            IEnumerable<PolicyEventCandidatePrediction> rankedCandidates,
            out string[] blockingReasons) =>
        RebuildVerifiedCurrentCandidates(
            snapshot,
            ledger,
            goalId,
            requirement,
            endpointOptionIds,
            rankedCandidates,
            out blockingReasons,
            out _);

    internal static PolicyEventCandidatePrediction[]
        RebuildVerifiedCurrentCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger ledger,
            string goalId,
            AcquisitionRouteTargetDateUnlock requirement,
            string[] endpointOptionIds,
            IEnumerable<PolicyEventCandidatePrediction> rankedCandidates,
            out string[] blockingReasons,
            out PolicyEventCandidatePrediction[] rebuiltEvidence)
    {
        var allowed = endpointOptionIds.ToHashSet(StringComparer.Ordinal);
        var supplied = rankedCandidates
            .Where(candidate => allowed.Contains(candidate.OptionId))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var evaluationCandidates = RebuildEvaluationCandidates(
            snapshot,
            requirement,
            endpointOptionIds,
            supplied,
            out var intentReasons);
        var rebuilt = intentReasons.Length > 0
            ? Array.Empty<PolicyEventCandidatePrediction>()
            : new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    new CandidateOptionAvailabilityEvaluator().Evaluate(
                        snapshot,
                        evaluationCandidates,
                        includeExecutorCalibrationOptions: true,
                        commitmentLedger: ledger),
                    goalId)
                .Where(candidate => allowed.Contains(candidate.OptionId))
                .OrderBy(candidate => candidate.CandidateId,
                    StringComparer.Ordinal)
                .ToArray();
        rebuiltEvidence = rebuilt;
        var reasons = new List<string>(intentReasons);
        if (supplied.GroupBy(candidate => candidate.CandidateId,
                StringComparer.Ordinal).Any(group => group.Count() != 1) ||
            rebuilt.GroupBy(candidate => candidate.CandidateId,
                StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            reasons.Add("route_dispatch_endpoint_candidate_identity_ambiguous");
        }

        var suppliedById = supplied
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);
        var rebuiltById = rebuilt
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);
        if (suppliedById.Keys.Except(rebuiltById.Keys, StringComparer.Ordinal)
            .Any())
        {
            reasons.Add("ranking_endpoint_candidate_not_rebuilt");
        }
        if (rebuiltById.Keys.Except(suppliedById.Keys, StringComparer.Ordinal)
            .Any())
        {
            reasons.Add("rebuilt_endpoint_candidate_missing_from_ranking");
        }
        if (suppliedById.Keys.Intersect(rebuiltById.Keys,
                StringComparer.Ordinal).Any(id =>
                !CandidateEvidenceMatches(
                    suppliedById[id],
                    rebuiltById[id])))
        {
            reasons.Add("ranking_endpoint_candidate_evidence_mismatch");
        }

        blockingReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return blockingReasons.Length == 0
            ? rebuilt
            : Array.Empty<PolicyEventCandidatePrediction>();
    }

    private static OptionAvailabilityCandidate[] RebuildEvaluationCandidates(
        SnapshotEnvelope snapshot,
        AcquisitionRouteTargetDateUnlock requirement,
        string[] endpointOptionIds,
        PolicyEventCandidatePrediction[] supplied,
        out string[] blockingReasons)
    {
        if (requirement.RouteKind == "native_geode_drop")
        {
            if (!endpointOptionIds.SequenceEqual(
                    new[] { "processing.crack_geode" },
                    StringComparer.Ordinal))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_geode_acquisition_endpoint_invalid"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            if (!TryParseGeodeSourceInput(
                    requirement.SourceId,
                    out var geodeQualifiedItemId))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_geode_source_invalid"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }

            blockingReasons = Array.Empty<string>();
            return new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "processing.crack_geode",
                    Parameters = new[]
                    {
                        Parameter(
                            "geode_qualified_item_id",
                            geodeQualifiedItemId),
                        Parameter(
                            "geode_purpose",
                            requirement.RequirementSetId == "full_shipment"
                                ? "open_for_full_shipment"
                                : "open_for_projected_value")
                    }
                }
            };
        }

        if (requirement.RouteKind == "sells")
        {
            if (!endpointOptionIds.SequenceEqual(
                    new[] { "economy.buy_supplies" },
                    StringComparer.Ordinal))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_shop_purchase_endpoint_invalid"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            const string prefix = "shop:";
            var shopId = requirement.SourceId.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                ? requirement.SourceId[prefix.Length..]
                : string.Empty;
            var stockIds = requirement.MatchingWindows
                .Where(window => window.SourceKind == "shop_stock_rule" &&
                    window.SourceKey.StartsWith(
                        shopId + ":",
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(window.RuleId))
                .Select(window => window.RuleId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (string.IsNullOrWhiteSpace(shopId) || stockIds.Length != 1)
            {
                blockingReasons = new[]
                {
                    "route_dispatch_shop_source_invalid_or_ambiguous"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }

            using var stateDocument = JsonDocument.Parse(
                JsonSerializer.Serialize(snapshot.State, JsonDefaults.Options));
            var quoteState = new AcquisitionShopQuoteSnapshotState(
                stateDocument.RootElement);
            var quotes = quoteState.ShopQuotes(requirement.QualifiedItemId);
            if (!quotes.EvidenceAvailable)
            {
                blockingReasons = quotes.BlockingReasons;
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            var matches = quotes.Quotes.Where(quote =>
                    quote.ShopId == shopId &&
                    quote.StockId == stockIds[0] &&
                    quote.CanBuyItem &&
                    quote.ExecutorPurchaseEnabled &&
                    quote.ExecutorBlockReasons.Length == 0 &&
                    quote.OutputStack >= requirement.RequiredAmount &&
                    quote.OutputQuality >= requirement.MinimumQuality &&
                    (quote.InfiniteStock || quote.Stock >= 1))
                .ToArray();
            if (matches.Length != 1)
            {
                blockingReasons = new[]
                {
                    matches.Length == 0
                        ? "route_dispatch_current_shop_quote_missing"
                        : "route_dispatch_current_shop_quote_ambiguous"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }

            var quote = matches[0];
            blockingReasons = Array.Empty<string>();
            return new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "economy.buy_supplies",
                    Parameters = new[]
                    {
                        Parameter("continuation.shop_id", quote.ShopId),
                        Parameter(
                            "continuation.qualified_item_id",
                            quote.QualifiedItemId),
                        Parameter("continuation.stock_id", quote.StockId),
                        Parameter(
                            "continuation.output_stack_per_purchase",
                            quote.OutputStack.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter(
                            "continuation.output_quality",
                            quote.OutputQuality.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter(
                            "continuation.max_unit_price",
                            quote.Price.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("continuation.quantity", "1")
                    }
                }
            };
        }

        if (requirement.RouteKind is not
            ("native_location_fish_spawn" or
             "native_mine_fishing_override" or
             "native_monster_drop_table" or
             "native_radioactive_ore_node" or
             "recipe_output"))
        {
            blockingReasons = Array.Empty<string>();
            return endpointOptionIds
                .Select(optionId => new OptionAvailabilityCandidate
                {
                    OptionId = optionId
                })
                .ToArray();
        }

        if (requirement.RouteKind is
            "native_radioactive_ore_node" or
            "native_monster_drop_table")
        {
            if (!endpointOptionIds.SequenceEqual(
                    new[] { "mining.reach_depth" },
                    StringComparer.Ordinal))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_mining_acquisition_endpoint_invalid"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            var parameters = requirement.RouteKind ==
                    "native_monster_drop_table"
                ? AcquisitionCurrentRouteCandidateIndex
                    .BuildRollingMiningParameters(
                        snapshot,
                        requirement.RouteKind,
                        requirement.SourceId,
                        requirement.QualifiedItemId)
                : AcquisitionCurrentRouteCandidateIndex
                    .BuildRollingMiningParameters(snapshot);
            if (parameters.Length == 0)
            {
                blockingReasons = new[]
                {
                    "route_dispatch_current_mine_intent_unavailable"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            blockingReasons = Array.Empty<string>();
            return new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.reach_depth",
                    Parameters = parameters
                }
            };
        }

        if (requirement.RouteKind == "recipe_output")
        {
            const string prefix = "cooking_recipe:";
            var recipeName = requirement.SourceId.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                ? requirement.SourceId[prefix.Length..]
                : string.Empty;
            var cookingReason = requirement.RequirementSetId;
            if (string.IsNullOrWhiteSpace(recipeName) ||
                string.IsNullOrWhiteSpace(cookingReason))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_verified_recipe_intent_missing_or_ambiguous"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            blockingReasons = Array.Empty<string>();
            return new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "crafting.cook_recipe",
                    ExplicitConfirmationGranted = true,
                    Parameters = new[]
                    {
                        Parameter("recipe_name", recipeName),
                        Parameter("craft_count", "1"),
                        Parameter("cooking_reason", cookingReason)
                    }
                }
            };
        }

        var locationFishSpeciesStatus = requirement.RouteKind ==
                "native_location_fish_spawn"
            ? SnapshotFishCollectionSpeciesStatus(
                snapshot,
                requirement.QualifiedItemId)
            : FishCollectionSpeciesStatus.Unavailable;
        if (requirement.RouteKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Unavailable)
        {
            blockingReasons = new[]
            {
                "route_dispatch_fish_collection_catalog_unavailable"
            };
            return Array.Empty<OptionAvailabilityCandidate>();
        }
        if (requirement.RouteKind == "native_location_fish_spawn" &&
            locationFishSpeciesStatus == FishCollectionSpeciesStatus.Absent)
        {
            if (!supplied.Any(candidate =>
                    TryMatchCompleteLocationFishingOutcome(
                        requirement.SourceId,
                        requirement.QualifiedItemId,
                        candidate,
                        out _)))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_complete_location_fishing_outcome_missing"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            blockingReasons = Array.Empty<string>();
            return endpointOptionIds
                .Select(optionId => new OptionAvailabilityCandidate
                {
                    OptionId = optionId
                })
                .ToArray();
        }

        var intents = supplied
            .Select(candidate =>
            {
                var valid = MasterAnglerCurrentCandidateMatcher.TryMatch(
                    snapshot,
                    candidate,
                    out var match,
                    out _);
                return new { Candidate = candidate, Match = match, Valid = valid };
            })
            .Where(value => value.Valid &&
                string.Equals(
                    value.Match.Intent.TargetQualifiedItemId,
                    requirement.QualifiedItemId,
                    StringComparison.Ordinal) &&
                ((requirement.RouteKind == "native_location_fish_spawn" &&
                  value.Match.Intent.SourceKind == "location_rule" &&
                  requirement.SourceId == "location_fish:" +
                    value.Match.Intent.SourceKey) ||
                 (requirement.RouteKind == "native_mine_fishing_override" &&
                  value.Match.Intent.SourceKind == "mine_override" &&
                  requirement.SourceId == value.Match.Intent.SourceKey)))
            .GroupBy(value =>
                value.Match.Intent.SourceKind + "|" +
                value.Match.Intent.SourceKey + "|" +
                value.Match.Intent.TargetQualifiedItemId,
                StringComparer.Ordinal)
            .ToArray();
        if (intents.Length != 1)
        {
            blockingReasons = new[]
            {
                "route_dispatch_verified_bound_intent_missing_or_ambiguous"
            };
            return Array.Empty<OptionAvailabilityCandidate>();
        }

        blockingReasons = Array.Empty<string>();
        var source = intents[0]
            .OrderBy(value => value.Candidate.CandidateId,
                StringComparer.Ordinal)
            .First()
            .Candidate;
        return new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = source.OptionId,
                Parameters = source.Parameters
            }
        };
    }

    private static bool TryParseGeodeSourceInput(
        string sourceId,
        out string qualifiedItemId)
    {
        qualifiedItemId = string.Empty;
        var parts = sourceId.Split(':', StringSplitOptions.None);
        if (parts.Length is not (3 or 5) ||
            parts[0] != "geode" ||
            !uint.TryParse(
                parts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var itemId) ||
            !int.TryParse(
                parts[2],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var rowIndex) ||
            rowIndex < 0 ||
            (parts.Length == 5 &&
             (parts[3] != "random" ||
              !int.TryParse(
                  parts[4],
                  System.Globalization.NumberStyles.None,
                  System.Globalization.CultureInfo.InvariantCulture,
                  out var randomIndex) ||
              randomIndex < 0)))
        {
            return false;
        }

        qualifiedItemId = "(O)" + itemId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    private static bool CandidateEvidenceMatches(
        PolicyEventCandidatePrediction supplied,
        PolicyEventCandidatePrediction rebuilt) =>
        string.Equals(
            System.Text.Json.JsonSerializer.Serialize(
                NeutralizeLearnerSignals(supplied, supplied.CandidateId),
                JsonDefaults.Options),
            System.Text.Json.JsonSerializer.Serialize(
                NeutralizeLearnerSignals(rebuilt, rebuilt.CandidateId),
                JsonDefaults.Options),
            StringComparison.Ordinal);
}
