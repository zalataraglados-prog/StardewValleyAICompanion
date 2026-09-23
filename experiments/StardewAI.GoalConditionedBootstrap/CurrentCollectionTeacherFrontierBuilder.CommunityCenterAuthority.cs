namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentCollectionTeacherFrontierBuilder
{
    private static CurrentCommunityCenterFrontierAuthority
        BuildCurrentCommunityCenterAuthority(
            CurrentCommunityCenterDenominatorReport denominator,
            AcquisitionRouteOptionLoweringReport lowering)
    {
        ValidateCurrentCommunityCenterDenominator(denominator);
        var routeKinds = BuildRouteKindDescriptors(lowering);
        var alternativesByKey = new Dictionary<
            string,
            CurrentCommunityCenterAlternativeAuthority>(StringComparer.Ordinal);
        var groups = denominator.ActiveBundles
            .OrderBy(value => value.RequirementId, StringComparer.Ordinal)
            .Select(bundle =>
            {
                var alternatives = bundle.Ingredients
                    .OrderBy(value => value.IngredientIndex)
                    .Select(ingredient =>
                    {
                        var acceptedTargets = ingredient.AcquisitionTargets
                            .Where(value => value.RouteCovered)
                            .Select(target => new CurrentCommunityCenterAcceptedTarget(
                                target.ItemId,
                                target.QualifiedItemId,
                                target.DisplayName,
                                LowerRoutes(target.AcquisitionRoutes, routeKinds)))
                            .OrderBy(value => value.QualifiedItemId,
                                StringComparer.Ordinal)
                            .ThenBy(value => value.ItemId, StringComparer.Ordinal)
                            .ToArray();
                        if (acceptedTargets.Length == 0)
                        {
                            throw new InvalidDataException(
                                "A current Community Center ingredient has no admitted acquisition target: " +
                                bundle.RequirementId + ":" + ingredient.IngredientIndex);
                        }

                        var routes = acceptedTargets
                            .SelectMany(value => value.Routes)
                            .GroupBy(RouteIdentity, StringComparer.Ordinal)
                            .Select(value => value.First())
                            .OrderBy(value => value.RouteKind, StringComparer.Ordinal)
                            .ThenBy(value => value.SourceId, StringComparer.Ordinal)
                            .ToArray();
                        var displayName = ingredient.MatchKind switch
                        {
                            "money_payment" => "Money",
                            "category" => "Object category " +
                                ingredient.ItemIdOrCategory,
                            _ => acceptedTargets.Single().DisplayName
                        };
                        var alternative = new GoalRequirementAlternative
                        {
                            ItemId = ingredient.ItemIdOrCategory,
                            QualifiedItemId = ingredient.QualifiedItemId,
                            DisplayName = displayName,
                            MatchKind = ingredient.MatchKind,
                            Amount = ingredient.RequiredStack,
                            MinimumQuality = ingredient.MinimumQuality,
                            AcquisitionRoutes = routes.Select(value =>
                                    new RequirementAcquisitionRoute(
                                        value.RouteKind,
                                        value.SourceId,
                                        value.SourceAsset,
                                        value.SourcePath))
                                .ToArray()
                        };
                        var lowered = new AcquisitionRequirementAlternativeLowering(
                            alternative.ItemId,
                            alternative.QualifiedItemId,
                            alternative.DisplayName,
                            alternative.MatchKind,
                            alternative.Amount,
                            alternative.MinimumQuality,
                            routes.Any(value => value.RuntimeAdmissionReady),
                            routes.Any(value => value.TeacherAdmissionReady),
                            routes);
                        var key = AlternativeAuthorityKey(
                            bundle.RequirementId,
                            ingredient.IngredientIndex);
                        if (!alternativesByKey.TryAdd(
                                key,
                                new CurrentCommunityCenterAlternativeAuthority(
                                    ingredient.IngredientIndex,
                                    ingredient.MatchKind,
                                    acceptedTargets)))
                        {
                            throw new InvalidDataException(
                                "A current Community Center alternative authority is duplicated: " +
                                key);
                        }
                        return new CurrentCommunityCenterBuiltAlternative(
                            alternative,
                            lowered);
                    })
                    .ToArray();
                var runtimeCount = alternatives.Count(value =>
                    value.Lowering.RuntimeAdmissionReady);
                var teacherCount = alternatives.Count(value =>
                    value.Lowering.TeacherAdmissionReady);
                var group = new GoalRequirementGroup
                {
                    RequirementId = bundle.RequirementId,
                    SelectionRule = "choose_at_least_required_slots",
                    RequiredAlternativeCount = bundle.RequiredSlotCount,
                    TransparentCompletionPath =
                        "world_progress.community_center.bundle_rows[bundle_data_key=" +
                        bundle.BundleDataKey + "].complete",
                    RouteCovered = teacherCount >= bundle.RequiredSlotCount,
                    Alternatives = alternatives.Select(value => value.Inventory).ToArray()
                };
                var lowered = new AcquisitionRequirementGroupLowering(
                    bundle.RequirementId,
                    bundle.RequiredSlotCount,
                    runtimeCount,
                    teacherCount,
                    runtimeCount >= bundle.RequiredSlotCount,
                    teacherCount >= bundle.RequiredSlotCount,
                    alternatives.SelectMany(value => value.Lowering.Routes)
                        .Where(value => !value.TeacherAdmissionReady)
                        .Select(value => value.RouteKind)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    alternatives.Select(value => value.Lowering).ToArray());
                return new CurrentCommunityCenterBuiltGroup(group, lowered);
            })
            .ToArray();

        var inventory = new GoalRequirementSet
        {
            RequirementSetId = CommunityCenterSetId,
            CriterionId = "community_center_access_or_completion",
            NativeCompletionRule =
                "CommunityCenter.ccIsComplete and event/mail settlement on the current save-bound bundle set",
            TransparentStatePath = "world_progress.community_center.bundle_rows",
            DenominatorStatus = "complete_current_save_bound_" +
                denominator.BundleMode,
            RequiredGroupCount = groups.Length,
            RouteCoveredGroupCount = groups.Count(value =>
                value.Inventory.RouteCovered),
            AcquisitionRoutesComplete = groups.All(value =>
                value.Inventory.RouteCovered),
            Groups = groups.Select(value => value.Inventory).ToArray()
        };
        var loweredSet = new AcquisitionRequirementSetLowering(
            CommunityCenterSetId,
            groups.Length,
            groups.Count(value => value.Lowering.RuntimeAdmissionReady),
            groups.Count(value => value.Lowering.TeacherAdmissionReady),
            groups.Select(value => value.Lowering).ToArray());
        return new CurrentCommunityCenterFrontierAuthority(
            inventory,
            loweredSet,
            alternativesByKey);
    }

    private static IReadOnlyDictionary<string, CurrentRouteKindDescriptor>
        BuildRouteKindDescriptors(AcquisitionRouteOptionLoweringReport lowering)
    {
        var result = new Dictionary<string, CurrentRouteKindDescriptor>(
            StringComparer.Ordinal);
        foreach (var routeKind in lowering.RouteKinds)
        {
            AddRouteKindDescriptor(
                result,
                routeKind.RouteKind,
                new CurrentRouteKindDescriptor(
                    routeKind.SupervisionMode,
                    routeKind.UncertaintyMode,
                    routeKind.RequiredDownstreamDependencyAxes,
                    routeKind.EndpointOptions.Select(value => value.OptionId).ToArray(),
                    routeKind.SupportingOptions.Select(value => value.OptionId).ToArray(),
                    routeKind.RuntimeAdmissionReady,
                    routeKind.TeacherAdmissionReady));
        }
        foreach (var route in lowering.RequirementSets
                     .SelectMany(value => value.Groups)
                     .SelectMany(value => value.Alternatives)
                     .SelectMany(value => value.Routes))
        {
            AddRouteKindDescriptor(
                result,
                route.RouteKind,
                new CurrentRouteKindDescriptor(
                    route.SupervisionMode,
                    route.UncertaintyMode,
                    route.RequiredDownstreamDependencyAxes,
                    route.EndpointOptionIds,
                    route.SupportingOptionIds,
                    route.RuntimeAdmissionReady,
                    route.TeacherAdmissionReady));
        }
        return result;
    }

    private static void AddRouteKindDescriptor(
        IDictionary<string, CurrentRouteKindDescriptor> values,
        string routeKind,
        CurrentRouteKindDescriptor candidate)
    {
        if (string.IsNullOrWhiteSpace(routeKind) ||
            !StageOneCollectionRouteDependencyAxes.IsComplete(
                candidate.RequiredDownstreamDependencyAxes) ||
            !candidate.RuntimeAdmissionReady ||
            !candidate.TeacherAdmissionReady ||
            candidate.EndpointOptionIds.Length == 0)
        {
            throw new InvalidDataException(
                "A Community Center route kind is not fully admitted: " + routeKind);
        }
        if (values.TryGetValue(routeKind, out var existing))
        {
            if (!existing.SemanticallyEquals(candidate))
            {
                throw new InvalidDataException(
                    "A Community Center route kind has inconsistent lowering: " +
                    routeKind);
            }
            return;
        }
        values.Add(routeKind, candidate);
    }

    private static AcquisitionRequirementRouteLowering[] LowerRoutes(
        IEnumerable<RequirementAcquisitionRoute> routes,
        IReadOnlyDictionary<string, CurrentRouteKindDescriptor> routeKinds) => routes
        .Select(route =>
        {
            if (!routeKinds.TryGetValue(route.Kind, out var descriptor))
            {
                throw new InvalidDataException(
                    "A current Community Center acquisition route kind has no admitted lowering: " +
                    route.Kind);
            }
            return new AcquisitionRequirementRouteLowering(
                route.Kind,
                route.SourceId,
                route.SourceAsset,
                route.SourcePath,
                descriptor.SupervisionMode,
                descriptor.UncertaintyMode,
                descriptor.RequiredDownstreamDependencyAxes,
                descriptor.EndpointOptionIds,
                descriptor.SupportingOptionIds,
                descriptor.RuntimeAdmissionReady,
                descriptor.TeacherAdmissionReady);
        })
        .GroupBy(RouteIdentity, StringComparer.Ordinal)
        .Select(value => value.First())
        .OrderBy(value => value.RouteKind, StringComparer.Ordinal)
        .ThenBy(value => value.SourceId, StringComparer.Ordinal)
        .ToArray();

    private static void ValidateCurrentCommunityCenterDenominator(
        CurrentCommunityCenterDenominatorReport denominator)
    {
        if (denominator.SchemaVersion != "current_community_center_denominator.v1" ||
            denominator.Status != "ready" ||
            denominator.BundleMode is not ("standard" or "remixed") ||
            !denominator.IngredientAcquisitionCatalogComplete ||
            denominator.ActiveBundleCount <= 0 ||
            denominator.ActiveBundleCount != denominator.ActiveBundles.Length ||
            denominator.CompletedActiveBundleCount != denominator.ActiveBundles.Count(
                value => value.Complete) ||
            !string.Equals(
                denominator.DenominatorSha256,
                CurrentCommunityCenterDenominatorBuilder.ComputeDenominatorSha256(
                    denominator.BundleMode,
                    denominator.ActiveBundles),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The current Community Center denominator identity is incomplete.");
        }
        if (denominator.ActiveBundles
            .GroupBy(value => value.RequirementId, StringComparer.Ordinal)
            .Any(value => value.Count() != 1))
        {
            throw new InvalidDataException(
                "The current Community Center denominator has duplicate requirement IDs.");
        }
        foreach (var bundle in denominator.ActiveBundles)
        {
            if (bundle.RequirementId !=
                    "community_center:bundle:" + bundle.BundleDataKey ||
                bundle.RequiredSlotCount < 1 ||
                bundle.RequiredSlotCount > bundle.Ingredients.Length ||
                bundle.CompletedIngredientCount != bundle.Ingredients.Count(
                    value => value.Completed) ||
                bundle.Complete !=
                    (bundle.CompletedIngredientCount >= bundle.RequiredSlotCount) ||
                !bundle.Ingredients.Select(value => value.IngredientIndex)
                    .SequenceEqual(Enumerable.Range(0, bundle.Ingredients.Length)))
            {
                throw new InvalidDataException(
                    "A current Community Center bundle denominator is inconsistent: " +
                    bundle.RequirementId);
            }
            foreach (var ingredient in bundle.Ingredients)
                ValidateCurrentCommunityCenterIngredient(bundle, ingredient);
        }
    }

    private static void ValidateCurrentCommunityCenterIngredient(
        CurrentCommunityCenterBundle bundle,
        CurrentCommunityCenterIngredient ingredient)
    {
        var validIdentity = ingredient.MatchKind switch
        {
            "item_id" =>
                ingredient.QualifiedItemId == "(O)" + ingredient.ItemIdOrCategory,
            "category" =>
                ingredient.ItemIdOrCategory.StartsWith("-", StringComparison.Ordinal) &&
                ingredient.ItemIdOrCategory != "-1" &&
                ingredient.QualifiedItemId.Length == 0,
            "money_payment" => ingredient.ItemIdOrCategory == "-1" &&
                ingredient.QualifiedItemId.Length == 0,
            _ => false
        };
        if (!validIdentity || ingredient.RequiredStack < 1 ||
            ingredient.MinimumQuality < 0 ||
            ingredient.AcquisitionTargets.Length == 0 ||
            ingredient.AcquisitionTargets.Any(value =>
                string.IsNullOrWhiteSpace(value.ItemId) ||
                value.RouteCovered != (value.AcquisitionRoutes.Length > 0)) ||
            !ingredient.AcquisitionTargets.Any(value => value.RouteCovered) ||
            ingredient.AcquisitionTargets
                .Where(value => value.QualifiedItemId.Length > 0)
                .GroupBy(value => value.QualifiedItemId, StringComparer.Ordinal)
                .Any(value => value.Count() != 1))
        {
            throw new InvalidDataException(
                "A current Community Center ingredient denominator is inconsistent: " +
                bundle.RequirementId + ":" + ingredient.IngredientIndex);
        }
    }

    private static string AlternativeAuthorityKey(
        string requirementId,
        int alternativeIndex) => requirementId + "\u001f" + alternativeIndex;

    private static string RouteIdentity(
        AcquisitionRequirementRouteLowering value) => string.Join(
        "\u001f",
        value.RouteKind,
        value.SourceId,
        value.SourceAsset,
        value.SourcePath);

    private sealed record CurrentCommunityCenterFrontierAuthority(
        GoalRequirementSet Inventory,
        AcquisitionRequirementSetLowering Lowering,
        IReadOnlyDictionary<string, CurrentCommunityCenterAlternativeAuthority>
            AlternativesByKey);

    private sealed record CurrentCommunityCenterAlternativeAuthority(
        int AlternativeIndex,
        string MatchKind,
        CurrentCommunityCenterAcceptedTarget[] AcceptedTargets);

    private sealed record CurrentCommunityCenterAcceptedTarget(
        string ItemId,
        string QualifiedItemId,
        string DisplayName,
        AcquisitionRequirementRouteLowering[] Routes);

    private sealed record CurrentCommunityCenterBuiltAlternative(
        GoalRequirementAlternative Inventory,
        AcquisitionRequirementAlternativeLowering Lowering);

    private sealed record CurrentCommunityCenterBuiltGroup(
        GoalRequirementGroup Inventory,
        AcquisitionRequirementGroupLowering Lowering);

    private sealed record CurrentRouteKindDescriptor(
        string SupervisionMode,
        string UncertaintyMode,
        string[] RequiredDownstreamDependencyAxes,
        string[] EndpointOptionIds,
        string[] SupportingOptionIds,
        bool RuntimeAdmissionReady,
        bool TeacherAdmissionReady)
    {
        public bool SemanticallyEquals(CurrentRouteKindDescriptor other) =>
            SupervisionMode == other.SupervisionMode &&
            UncertaintyMode == other.UncertaintyMode &&
            RuntimeAdmissionReady == other.RuntimeAdmissionReady &&
            TeacherAdmissionReady == other.TeacherAdmissionReady &&
            RequiredDownstreamDependencyAxes.ToHashSet(StringComparer.Ordinal)
                .SetEquals(other.RequiredDownstreamDependencyAxes) &&
            EndpointOptionIds.SequenceEqual(other.EndpointOptionIds) &&
            SupportingOptionIds.SequenceEqual(other.SupportingOptionIds);
    }
}
