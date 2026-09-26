namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest
        VerifyAcquisitionCorpus(
            GoalMethodTeacherCoverageSource source,
            string corpusManifestPath,
            GoalMethodFrontierReport frontier,
            IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var corpus = GoalMethodPairwiseTrainer.VerifyCorpus(
            corpusManifestPath);
        if (corpus.Manifest.Sources.Any(sourceDigest =>
                !IsSameGrandpaGoal(
                    sourceDigest.GoalId,
                    frontier.GoalId)))
        {
            throw new InvalidDataException(
                "Coverage corpus goal differs from the authoritative frontier.");
        }
        var methodsByRequirementSet = frontier.Methods
            .SelectMany(method => method.RequirementSetReadiness.Select(
                requirement => (requirement.RequirementSetId, method)))
            .GroupBy(value => value.RequirementSetId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var methods = group.Select(value => value.method)
                        .DistinctBy(method => method.MethodId)
                        .ToArray();
                    if (methods.Length != 1)
                    {
                        throw new InvalidDataException(
                            "Requirement set does not map to exactly one goal method: " +
                            group.Key);
                    }
                    return methods[0];
                },
                StringComparer.Ordinal);
        foreach (var method in methodsByRequirementSet.Values
                     .DistinctBy(method => method.MethodId))
        {
            evidence[method.MethodId].TeacherSourceKinds.Add(
                source.SourceKind);
        }
        var sourceByDataset = corpus.Manifest.Sources.ToDictionary(
            digest => digest.DatasetSha256,
            StringComparer.Ordinal);
        var manifests = new Dictionary<string,
            AcquisitionRoutePortfolioRolloutProofManifest>(
            StringComparer.Ordinal);
        var opportunities = new Dictionary<string,
            AcquisitionRouteTargetDateOpportunityCostReport>(
            StringComparer.Ordinal);
        var rows = corpus.TrainRows
            .Concat(corpus.ValidationRows)
            .Concat(corpus.TestRows)
            .ToArray();
        var pairCount = 0;
        foreach (var row in rows)
        {
            if (!sourceByDataset.TryGetValue(
                    row.SourceDatasetSha256,
                    out var sourceDigest))
            {
                throw new InvalidDataException(
                    "Coverage row source is absent from the verified corpus.");
            }
            if (!manifests.TryGetValue(
                    sourceDigest.ProofManifestSha256,
                    out var manifest))
            {
                manifest = CurrentTeacherFrontierSupport.Read<
                    AcquisitionRoutePortfolioRolloutProofManifest>(
                    sourceDigest.ProofManifestPath,
                    "Goal-method Teacher coverage rollout manifest");
                manifests.Add(sourceDigest.ProofManifestSha256, manifest);
            }
            var executionInputs = ExecutionInputsFor(
                manifest,
                row.SupervisionRow.TransitionIndex);
            var opportunityPath = Path.GetFullPath(
                executionInputs.TargetDateOpportunityCostPath);
            if (!opportunities.TryGetValue(opportunityPath, out var opportunity))
            {
                opportunity = CurrentTeacherFrontierSupport.Read<
                    AcquisitionRouteTargetDateOpportunityCostReport>(
                    opportunityPath,
                    "Goal-method Teacher coverage opportunity cost");
                ValidateOpportunity(opportunity);
                opportunities.Add(opportunityPath, opportunity);
            }
            if (!IsSameGrandpaGoal(
                    opportunity.GoalId,
                    frontier.GoalId) ||
                opportunity.SnapshotStateHash !=
                    row.SupervisionRow.Payload.DecisionStateHash)
            {
                throw new InvalidDataException(
                    "Coverage opportunity context differs from its supervision row.");
            }
            var requirementByRoute = opportunity.Routes.ToDictionary(
                route => route.RouteOccurrenceId,
                route => AcquisitionRoutePortfolioBuilder
                    .RequirementRoute(route).RequirementSetId,
                StringComparer.Ordinal);
            var teacher = row.SupervisionRow.Payload.TeacherPreference;
            var candidates = teacher.CandidateEvaluations.ToDictionary(
                candidate => candidate.ProposalId,
                StringComparer.Ordinal);
            foreach (var pair in teacher.PairwisePreferences)
            {
                pairCount++;
                if (!candidates.TryGetValue(
                        pair.PreferredProposalId,
                        out var preferred) ||
                    !candidates.TryGetValue(
                        pair.AlternativeProposalId,
                        out var alternative))
                {
                    throw new InvalidDataException(
                        "Coverage pair references a missing admitted candidate.");
                }
                foreach (var entry in methodsByRequirementSet)
                {
                    var preferredRoutes = RoutesForRequirementSet(
                        preferred.SelectedRouteOccurrenceIds,
                        entry.Key,
                        requirementByRoute);
                    var alternativeRoutes = RoutesForRequirementSet(
                        alternative.SelectedRouteOccurrenceIds,
                        entry.Key,
                        requirementByRoute);
                    if (!preferredRoutes.SequenceEqual(
                            alternativeRoutes,
                            StringComparer.Ordinal))
                    {
                        evidence[entry.Value.MethodId]
                            .TeacherComparisonPartitions.Add(
                                row.DatasetPartition);
                    }
                }
            }
            var outcomeRoute = row.SupervisionRow.Payload.NativeOutcome
                .RouteOccurrenceId;
            if (!requirementByRoute.TryGetValue(
                    outcomeRoute,
                    out var outcomeRequirementSet))
            {
                throw new InvalidDataException(
                    "Coverage native outcome route is absent from its opportunity denominator.");
            }
            pairCount += CreditStrictNextRouteComparisons(
                row,
                teacher,
                outcomeRoute,
                opportunity,
                requirementByRoute,
                methodsByRequirementSet,
                evidence);
            if (methodsByRequirementSet.TryGetValue(
                    outcomeRequirementSet,
                    out var outcomeMethod))
            {
                evidence[outcomeMethod.MethodId]
                    .NativeOutcomePartitions.Add(row.DatasetPartition);
            }
        }

        return new GoalMethodTeacherCoverageSourceDigest(
            source.SourceId,
            source.SourceKind,
            corpusManifestPath,
            CurrentTeacherFrontierSupport.HashFile(corpusManifestPath),
            rows.Length,
            pairCount);
    }

    private static int CreditStrictNextRouteComparisons(
        AcquisitionRoutePortfolioSupervisionCorpusRow row,
        AcquisitionRoutePortfolioTeacherSupervision teacher,
        string outcomeRouteOccurrenceId,
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        IReadOnlyDictionary<string, string> requirementByRoute,
        IReadOnlyDictionary<string, GoalMethodFrontierMethod>
            methodsByRequirementSet,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var selectedRouteIds = teacher.SelectedRouteOccurrenceIds
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (selectedRouteIds.Length < 2)
            return 0;
        if (!selectedRouteIds.Contains(
                outcomeRouteOccurrenceId,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Coverage native outcome is absent from the selected Teacher portfolio.");
        }

        var routesById = opportunity.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        var selectedRoutes = selectedRouteIds.Select(routeId =>
        {
            if (!routesById.TryGetValue(routeId, out var route) ||
                !route.OpportunityCostAxisResolved ||
                route.OpportunityCostMatchesTargetDate != true ||
                route.CostVector is null ||
                route.NonMatchingReasons.Length != 0 ||
                route.BlockingReasons.Length != 0)
            {
                throw new InvalidDataException(
                    "Coverage selected Teacher route lacks complete opportunity-cost evidence: " +
                    routeId);
            }
            return route;
        }).ToArray();
        var strictWinners = selectedRoutes.Where(candidate =>
                selectedRoutes.Where(other => other.RouteOccurrenceId !=
                        candidate.RouteOccurrenceId)
                    .All(other => AcquisitionRouteTargetDateOpportunityCostBuilder
                        .OpportunityCostDominates(
                            candidate.CostVector!,
                            other.CostVector!)))
            .ToArray();
        if (strictWinners.Length != 1 ||
            strictWinners[0].RouteOccurrenceId != outcomeRouteOccurrenceId)
        {
            return 0;
        }

        var preferredRequirementSet = requirementByRoute[
            outcomeRouteOccurrenceId];
        foreach (var alternative in selectedRoutes.Where(route =>
                     route.RouteOccurrenceId != outcomeRouteOccurrenceId))
        {
            CreditMethod(preferredRequirementSet);
            CreditMethod(requirementByRoute[alternative.RouteOccurrenceId]);
        }
        return selectedRoutes.Length - 1;

        void CreditMethod(string requirementSetId)
        {
            if (methodsByRequirementSet.TryGetValue(
                    requirementSetId,
                    out var method))
            {
                evidence[method.MethodId].TeacherComparisonPartitions.Add(
                    row.DatasetPartition);
            }
        }
    }

    private static AcquisitionRouteExecutionBindingInputs ExecutionInputsFor(
        AcquisitionRoutePortfolioRolloutProofManifest manifest,
        int transitionIndex)
    {
        if (transitionIndex == 1)
            return manifest.InitialCheckpointProof.ExecutionInputs;
        var continuationIndex = transitionIndex - 2;
        if (continuationIndex < 0 ||
            continuationIndex >= manifest.ContinuationTransitions.Length)
        {
            throw new InvalidDataException(
                "Coverage transition index is outside its rollout proof.");
        }
        return manifest.ContinuationTransitions[continuationIndex]
            .ExecutionInputs;
    }

    private static void ValidateOpportunity(
        AcquisitionRouteTargetDateOpportunityCostReport opportunity)
    {
        if (opportunity.SchemaVersion !=
                "acquisition_route_target_date_opportunity_cost.v1" ||
            !opportunity.RouteOccurrenceInventoryComplete ||
            opportunity.Routes.Length != opportunity.RouteOccurrenceCount ||
            opportunity.Routes.Select(route => route.RouteOccurrenceId)
                .Distinct(StringComparer.Ordinal).Count() !=
                opportunity.Routes.Length)
        {
            throw new InvalidDataException(
                "Coverage opportunity-cost denominator is incomplete.");
        }
    }

    private static string[] RoutesForRequirementSet(
        IEnumerable<string> routeOccurrenceIds,
        string requirementSetId,
        IReadOnlyDictionary<string, string> requirementByRoute)
    {
        var result = new List<string>();
        foreach (var routeOccurrenceId in routeOccurrenceIds)
        {
            if (!requirementByRoute.TryGetValue(
                    routeOccurrenceId,
                    out var routeRequirementSet))
            {
                throw new InvalidDataException(
                    "Coverage candidate route is absent from its opportunity denominator.");
            }
            if (routeRequirementSet == requirementSetId)
                result.Add(routeOccurrenceId);
        }
        return result.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsSameGrandpaGoal(
        string corpusGoalId,
        string frontierGoalId) =>
        corpusGoalId == frontierGoalId ||
        corpusGoalId == GoalMethodTeacherCoverageGoalIds.AcquisitionCorpus &&
        frontierGoalId == GoalMethodTeacherCoverageGoalIds.Authoritative;
}
