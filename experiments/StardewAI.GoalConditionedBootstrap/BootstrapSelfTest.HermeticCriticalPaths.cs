using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunHermeticCriticalPaths()
    {
        VerifyHermeticStrategicPolicyAndPortfolio();
        VerifyMachineCalendarResolution();
        VerifyMachineFacilityResolution();
        VerifyMachineResourceResolution();
        RunAcquisitionRouteDispatch();
        VerifyCommunityCenterActiveRouteKindScope();
        VerifySupportingTransitionTerminalLineage();
        VerifyCommunityCenterDonationReceiptEvidence();
        RunFullShipmentSettlement();
    }

    private static void VerifySupportingTransitionTerminalLineage()
    {
        var sha256 = new string('a', 64);
        Require(AcquisitionRouteExecutionBindingBuilder
                    .VerifiedSupportingTransitionReplanSha256(
                        sha256,
                        sha256,
                        sha256,
                        sha256) == sha256 &&
                AcquisitionRouteExecutionBindingBuilder
                    .VerifiedSupportingTransitionReplanSha256(
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty) == string.Empty,
            "Supporting-transition terminal lineage did not preserve exact identity.");
        var rejected = false;
        try
        {
            AcquisitionRouteExecutionBindingBuilder
                .VerifiedSupportingTransitionReplanSha256(
                    sha256,
                    new string('b', 64),
                    sha256,
                    sha256);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Mixed supporting-transition terminal lineage was admitted.");
    }

    private static void VerifyHermeticStrategicPolicyAndPortfolio()
    {
        var policy = new StrategicPolicy();
        var request = HermeticStrategicRequest();
        var unique = HermeticStrategicScoringSet(
            incomparable: false,
            new string('1', 64));
        var first = policy.SelectFromAuthoritativeScoringSet(
            request,
            unique,
            transitionIndex: 1);
        Require(first.Status ==
                    "ready_deterministic_unique_strict_pareto_selection" &&
                first.DeterministicSelectionDisposition ==
                    AcquisitionRoutePortfolioSelectionDisposition
                        .UniqueStrictPareto &&
                first.RuntimeSelectionAuthorized &&
                !first.ModelInvoked &&
                first.StrategicInputSha256.Length == 64,
            "Hermetic StrategicPolicy unique-Pareto selection drifted.");

        var duplicateRequest = HermeticStrategicRequest();
        duplicateRequest.Replan.PreviousReplanFingerprint =
            first.ReplanFingerprint;
        var duplicate = policy.SelectFromAuthoritativeScoringSet(
            duplicateRequest,
            unique,
            transitionIndex: 1);
        Require(duplicate.ReplanDeduplicated &&
                !duplicate.ReplanRequired &&
                duplicate.Status == "deduplicated_strategic_replan",
            "Hermetic StrategicPolicy did not deduplicate identical inputs.");

        var changedInputs = HermeticStrategicScoringSet(
            incomparable: false,
            new string('2', 64));
        var changed = policy.SelectFromAuthoritativeScoringSet(
            duplicateRequest,
            changedInputs,
            transitionIndex: 1);
        Require(!changed.ReplanDeduplicated &&
                changed.ReplanRequired &&
                changed.SnapshotStateHash == first.SnapshotStateHash &&
                changed.StrategyLedgerRevision ==
                    first.StrategyLedgerRevision &&
                changed.StrategicInputSha256 !=
                    first.StrategicInputSha256 &&
                changed.ReplanFingerprint != first.ReplanFingerprint,
            "Strategic input drift reused a state/ledger-only fingerprint.");

        var incomparable = HermeticStrategicScoringSet(
            incomparable: true,
            new string('3', 64));
        incomparable.Preference.BlockingReasons = incomparable.Preference
            .BlockingReasons.Append("supplemental_diagnostic_only").ToArray();
        var learnedRequired = policy.SelectFromAuthoritativeScoringSet(
            request,
            incomparable,
            transitionIndex: 1);
        Require(learnedRequired.DeterministicSelectionDisposition ==
                    AcquisitionRoutePortfolioSelectionDisposition
                        .IncomparableFrontier &&
                learnedRequired.Status == "blocked_strategic_decision" &&
                learnedRequired.BlockingReasons.Contains(
                    "strategic_runtime_model_required_for_incomparable_frontier",
                    StringComparer.Ordinal),
            "StrategicPolicy still depended on diagnostic text to recognize an incomparable frontier.");
    }

    private static StrategicPolicySelectionRequest HermeticStrategicRequest() =>
        new()
        {
            CheckpointPath = "missing-hermetic-checkpoint.json",
            CorpusManifestPath = "missing-hermetic-corpus.json",
            Replan = new StrategicReplanContext
            {
                TriggerKinds = new[]
                {
                    StrategicReplanTriggers.ExplicitRequest
                },
                TriggerToken = "hermetic-critical-path"
            }
        };

    private static AcquisitionRoutePortfolioTeacherPreferenceBuilder
        .AcquisitionRoutePortfolioTeacherScoringSet
        HermeticStrategicScoringSet(
            bool incomparable,
            string preferenceRequestSha256)
    {
        const string goalId = "grandpa.stage1.21_points";
        const string stateHash = "hermetic-strategic-state";
        const int ledgerRevision = 7;
        var firstCost = CostVector(
            elapsedMinutes: 10,
            requiredEnergy: incomparable ? 2 : 1);
        var secondCost = CostVector(
            elapsedMinutes: 20,
            requiredEnergy: 1);
        var candidates = new[]
        {
            PortfolioCandidate("hermetic.proposal.a", firstCost),
            PortfolioCandidate("hermetic.proposal.b", secondCost)
        };
        var preference = new AcquisitionRoutePortfolioTeacherPreference
        {
            RequestId = "hermetic.preference.request",
            GoalId = goalId,
            SnapshotStateHash = stateHash,
            ExpectedLedgerRevision = ledgerRevision,
            PreferenceRequestSha256 = preferenceRequestSha256,
            RequirementInventorySha256 = new string('4', 64),
            OpportunityCostSha256 = new string('5', 64),
            StrategyLedgerSha256 = new string('6', 64),
            SnapshotSha256 = new string('7', 64),
            CandidateDenominatorCount = candidates.Length,
            CandidateDenominatorComplete = true
        };
        preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .SelectUniquePreference(
                preference,
                candidates,
                new List<string>());
        return new AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet(
                preference,
                new SnapshotEnvelope
                {
                    StateHash = stateHash,
                    State = new Dictionary<string,
                        System.Text.Json.JsonElement>(StringComparer.Ordinal)
                },
                candidates);

        AcquisitionRoutePortfolioTeacherPreferenceBuilder.PortfolioCandidate
            PortfolioCandidate(
                string proposalId,
                AcquisitionOpportunityCostVector cost)
        {
            var proposal = new AcquisitionRoutePortfolioProposal
            {
                ProposalId = proposalId,
                GoalId = goalId,
                SnapshotStateHash = stateHash,
                ExpectedLedgerRevision = ledgerRevision,
                SelectedRouteOccurrenceIds = new[]
                {
                    proposalId + ":route"
                }
            };
            return new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .PortfolioCandidate(
                    proposal,
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .ArtifactSha256(proposal),
                    new AcquisitionRoutePortfolioAdmission
                    {
                        ProposalId = proposalId,
                        GoalId = goalId,
                        SnapshotStateHash = stateHash,
                        StrategyLedgerRevision = ledgerRevision,
                        PortfolioAdmissionReady = true,
                        AggregateCostVector = cost
                    });
        }
    }

    private static AcquisitionOpportunityCostVector CostVector(
        int elapsedMinutes,
        double requiredEnergy) => new(
            elapsedMinutes,
            requiredEnergy,
            0,
            Array.Empty<AcquisitionOpportunityMaterialCost>(),
            Array.Empty<AcquisitionOpportunityCurrencyCost>(),
            new[] { "hermetic_fixture" });

    private static void VerifyCommunityCenterActiveRouteKindScope()
    {
        var activeRoute = new RequirementAcquisitionRoute(
            "hermetic_active_route",
            "hermetic-source",
            "Data/Objects",
            "24");
        var activeBundle = new CurrentCommunityCenterBundle
        {
            RequirementId = "community_center:bundle:Pantry/0",
            BundleDataKey = "Pantry/0",
            AreaName = "Pantry",
            BundleId = 0,
            InternalName = "Hermetic Bundle",
            RequiredSlotCount = 1,
            Ingredients = new[]
            {
                new CurrentCommunityCenterIngredient(
                    0,
                    "24",
                    "(O)24",
                    "item_id",
                    1,
                    0,
                    false,
                    new[]
                    {
                        new CommunityCenterIngredientAcquisitionTarget(
                            "24",
                            "(O)24",
                            "Parsnip",
                            true,
                            new[] { activeRoute })
                    })
            }
        };
        var denominator = new CurrentCommunityCenterDenominatorReport
        {
            Status = "ready",
            GoalId = "grandpa.stage1.21_points",
            IngredientAcquisitionCatalogComplete = true,
            BundleMode = "standard",
            ActiveBundleCount = 1,
            ActiveBundles = new[] { activeBundle }
        };
        denominator.DenominatorSha256 =
            CurrentCommunityCenterDenominatorBuilder.ComputeDenominatorSha256(
                denominator.BundleMode,
                denominator.ActiveBundles);
        var lowering = new AcquisitionRouteOptionLoweringReport
        {
            RouteKinds = new[]
            {
                RouteKind("hermetic_active_route", admitted: true),
                RouteKind("unrelated_future_gap", admitted: false)
            }
        };

        var authority = CurrentCommunityCenterRequirementAuthorityBuilder.Build(
            denominator,
            lowering);
        var route = authority.Lowering.Groups.Single().Alternatives.Single()
            .Routes.Single();
        Require(route.RouteKind == "hermetic_active_route" &&
                route.RuntimeAdmissionReady &&
                route.TeacherAdmissionReady,
            "An unrelated blocked route kind invalidated active Community Center authority.");

        static AcquisitionRouteKindLowering RouteKind(
            string routeKind,
            bool admitted) => new(
                routeKind,
                "hermetic",
                "deterministic_teacher",
                "none",
                admitted
                    ? StageOneCollectionRouteDependencyAxes.Required.ToArray()
                    : Array.Empty<string>(),
                1,
                1,
                new[] { "community_center_standard" },
                admitted
                    ? new[]
                    {
                        new AcquisitionLoweringOption(
                            "executor.hermetic",
                            "executor",
                            "eligible",
                            "ready",
                            true,
                            true,
                            Array.Empty<string>())
                    }
                    : Array.Empty<AcquisitionLoweringOption>(),
                Array.Empty<AcquisitionLoweringOption>(),
                admitted,
                admitted,
                admitted,
                admitted
                    ? Array.Empty<string>()
                    : new[] { "future_route_not_implemented" });
    }
}
