namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunFullShipmentStaticCompilability(string outputRoot)
    {
        var root = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(root);
        var inventoryPath = Path.Combine(root, "requirements.json");
        var loweringPath = Path.Combine(root, "lowering.json");
        var reconciliationPath = Path.Combine(root, "action-reconciliation.json");
        var supportCoveragePath = Path.Combine(root, "support-coverage.json");

        var groups = Enumerable.Range(0, 154)
            .Select(index => FullShipmentStaticGroup(index))
            .ToArray();
        Write(inventoryPath, new AuthoritativeRequirementInventoryReport
        {
            Status = "complete",
            GoalId = "grandpa.maximum_21",
            GameVersion = "1.6.15",
            DenominatorComplete = true,
            AcquisitionRoutesComplete = true,
            RequirementSets = new[]
            {
                new GoalRequirementSet
                {
                    RequirementSetId = "full_shipment",
                    CriterionId = "achievement_full_shipment",
                    DenominatorStatus = "complete",
                    RequiredGroupCount = 154,
                    RouteCoveredGroupCount = 154,
                    AcquisitionRoutesComplete = true,
                    Groups = groups
                }
            }
        });

        var loweredGroups = groups.Select(group =>
            new AcquisitionRequirementGroupLowering(
                group.RequirementId,
                1,
                1,
                1,
                true,
                true,
                Array.Empty<string>(),
                new[]
                {
                    new AcquisitionRequirementAlternativeLowering(
                        group.Alternatives[0].ItemId,
                        group.Alternatives[0].QualifiedItemId,
                        group.Alternatives[0].DisplayName,
                        "item_id",
                        1,
                        0,
                        true,
                        true,
                        new[]
                        {
                            new AcquisitionRequirementRouteLowering(
                                "harvests_as",
                                group.Alternatives[0].AcquisitionRoutes[0]
                                    .SourceId,
                                "Data/Crops",
                                group.Alternatives[0].AcquisitionRoutes[0]
                                    .SourcePath,
                                "deterministic_dependency",
                                "source_resolved_downstream",
                                StageOneCollectionRouteDependencyAxes.Required
                                    .ToArray(),
                                new[] { "farm.maintain_crops" },
                                new[] { "economy.buy_supplies" },
                                true,
                                true)
                        })
                }))
            .ToArray();
        var loweringReport = new AcquisitionRouteOptionLoweringReport
        {
            Status = "complete",
            GoalId = "grandpa.maximum_21",
            RequirementInventorySha256 =
                CurrentTeacherFrontierSupport.HashFile(inventoryPath),
            RequirementSetCount = 1,
            RequirementGroupCount = 154,
            RouteOccurrenceCount = 154,
            DependencyAxisInventoryComplete = true,
            RequiredDownstreamDependencyAxes =
                StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ObservedRouteKindCount = 1,
            ClassifiedRouteKindCount = 1,
            AdmittedRouteKindCount = 1,
            RequirementSets = new[]
            {
                new AcquisitionRequirementSetLowering(
                    "full_shipment",
                    154,
                    154,
                    154,
                    loweredGroups)
            }
        };
        Write(loweringPath, loweringReport);
        WriteActionReconciliation(reconciliationPath, compilerReady: true);
        Write(
            supportCoveragePath,
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
                .BuildReport(AcquisitionRouteSupportingTransitionKinds.All
                    .Select((kind, index) => new
                        AcquisitionRouteSupportingTransitionTerminalCoverageRow
                        {
                            SupportTransitionKind = kind,
                            SupportRequestId = "support-" + index,
                            SupportRouteOccurrenceId = "route-" + index,
                            SupportRunId = "support-run-" + index,
                            TerminalRouteOccurrenceIds = new[]
                            {
                                "terminal-route-" + index
                            },
                            TerminalRunIds = new[]
                            {
                                "terminal-run-" + index
                            },
                            RolloutId = "rollout-" + index,
                            SupportChainRecomputed = true,
                            TerminalRolloutRecomputed = true,
                            SupervisionRecomputed = true,
                            SupportExcludedFromTerminalOutcomes = true,
                            CoverageVerified = true
                        })));

        var report = FullShipmentStaticCompilabilityInventoryBuilder.Build(
            inventoryPath,
            loweringPath,
            reconciliationPath,
            supportCoveragePath);
        Require(report.Status == "complete_static_compilability_inventory" &&
                report.RequiredGroupCount == 154 &&
                report.RouteOccurrenceCount == 154 &&
                report.RouteKindCount == 1 &&
                report.AuthoritativeSourceIdentityCount == 154 &&
                report.ReferencedOptionCount == 2 &&
                report.SourceIdentityContractComplete &&
                report.EndpointOptionCompilationComplete &&
                report.SupportingOptionCompilationComplete &&
                report.SupportTerminalLineageComplete &&
                report.AllRequirementGroupsHaveCompilableRoute &&
                report.StaticCompilabilityComplete &&
                !report.FreshSaveRecurrenceEvidenceComplete &&
                !report.FormalProductTrainingAuthorized &&
                report.BlockingReasons.Length == 0 &&
                report.RemainingEvidenceGaps.SequenceEqual(new[]
                {
                    "fresh_save_154_step_full_shipment_recurrence_not_supplied"
                }, StringComparer.Ordinal),
            "Full Shipment static compilability inventory drifted.");
        Require(report.Routes.All(route =>
                    route.SourceEvidenceMode == "harvest_source_seed_id" &&
                    route.InlineSupportTransitionKinds.SequenceEqual(new[]
                    {
                        AcquisitionRouteSupportingTransitionKinds.CropPlanting
                    }, StringComparer.Ordinal) &&
                    route.StaticCompilationReady),
            "Full Shipment route source or inline-support contract drifted.");
        Require(AcquisitionRouteDispatchCompilationBuilder
                    .DescribeAuthoritativeSourceContract("unknown_route") is null &&
                !AcquisitionRouteDispatchCompilationBuilder
                    .CanRepresentAuthoritativeSource(
                        "unknown_route",
                        "unknown:source",
                        "(O)1",
                        out _,
                        out var unknownReason) &&
                unknownReason == "unsupported_source_identity_route_kind",
            "An unknown source-identity route kind was admitted.");
        var authoritativeRouteKinds = new[]
        {
            "creates_reward_item",
            "harvests_as",
            "machine_output",
            "native_bush_shake",
            "native_crab_pot_output",
            "native_farm_animal_deluxe_produce",
            "native_farm_animal_produce",
            "native_fish_pond_output",
            "native_fruit_tree_produce",
            "native_geode_default_drop",
            "native_geode_drop",
            "native_ginger_harvest",
            "native_location_artifact_spot",
            "native_location_fish_spawn",
            "native_location_forage_spawn",
            "native_machine_flavored_output",
            "native_machine_item_query_output",
            "native_mine_buried_item",
            "native_mine_fishing_override",
            "native_money_payment",
            "native_monster_drop_table",
            "native_object_artifact_spot_chance",
            "native_radioactive_ore_node",
            "native_solar_panel_output",
            "native_spring_onion_harvest",
            "native_tea_bush_harvest",
            "native_tree_moss_harvest",
            "native_wild_tree_chop_drop",
            "native_wild_tree_seed",
            "native_wild_tree_seed_drop",
            "native_wild_tree_tapper_output",
            "recipe_output",
            "sells"
        };
        Require(authoritativeRouteKinds.Length == 33 &&
                authoritativeRouteKinds.Distinct(StringComparer.Ordinal)
                    .Count() == 33 &&
                authoritativeRouteKinds.All(routeKind =>
                    AcquisitionRouteDispatchCompilationBuilder
                        .DescribeAuthoritativeSourceContract(routeKind) is not
                        null),
            "An authoritative acquisition route kind lacks a source contract.");

        loweringReport.RouteOccurrenceCount = 153;
        Write(loweringPath, loweringReport);
        var staleSummaryRejected = false;
        try
        {
            FullShipmentStaticCompilabilityInventoryBuilder.Build(
                inventoryPath,
                loweringPath,
                reconciliationPath,
                supportCoveragePath);
        }
        catch (InvalidDataException)
        {
            staleSummaryRejected = true;
        }
        Require(staleSummaryRejected,
            "A stale lowering route-occurrence summary was admitted.");
        loweringReport.RouteOccurrenceCount = 154;
        Write(loweringPath, loweringReport);

        WriteActionReconciliation(reconciliationPath, compilerReady: false);
        var blocked = FullShipmentStaticCompilabilityInventoryBuilder.Build(
            inventoryPath,
            loweringPath,
            reconciliationPath,
            supportCoveragePath);
        Require(!blocked.StaticCompilabilityComplete &&
                !blocked.EndpointOptionCompilationComplete &&
                blocked.Options.Single(option =>
                    option.OptionId == "farm.maintain_crops")
                    .StaticCompilationReady == false &&
                blocked.BlockingReasons.Any(reason => reason.Contains(
                    "endpoint_option_not_static_compilation_ready",
                    StringComparison.Ordinal)),
            "An unbound endpoint compiler was reported as compilable.");
    }

    private static GoalRequirementGroup FullShipmentStaticGroup(int index)
    {
        var itemId = (1000 + index).ToString();
        var seedId = (2000 + index).ToString();
        return new GoalRequirementGroup
        {
            RequirementId = "full_shipment:item:" + itemId,
            SelectionRule = "all_required",
            RequiredAlternativeCount = 1,
            TransparentCompletionPath =
                "world_progress.full_shipment_progress.items[item_id=" +
                itemId + "].shipped",
            RouteCovered = true,
            Alternatives = new[]
            {
                new GoalRequirementAlternative
                {
                    ItemId = itemId,
                    QualifiedItemId = "(O)" + itemId,
                    DisplayName = "Fixture " + itemId,
                    MatchKind = "item_id",
                    Amount = 1,
                    AcquisitionRoutes = new[]
                    {
                        new RequirementAcquisitionRoute(
                            "harvests_as",
                            "crop:" + seedId,
                            "Data/Crops",
                            "payload." + seedId + ".HarvestItemId")
                    }
                }
            }
        };
    }

    private static void WriteActionReconciliation(
        string path,
        bool compilerReady) => Write(path, new
        {
            schema_version =
                "stardewai.action_implementation_reconciliation.v1",
            registered_option_count = 2,
            options = new[]
            {
                ActionReconciliationOption(
                    "farm.maintain_crops",
                    compilerReady
                        ? "StepCompilerDeclared"
                        : "Unbound"),
                ActionReconciliationOption(
                    "economy.buy_supplies",
                    "StepCompilerDeclared")
            }
        });

    private static object ActionReconciliationOption(
        string optionId,
        string compilerStatus) => new
        {
            optionId,
            registrationStatus = "Registered",
            readStatus = "RequiredFactContractDeclared",
            candidateStatus = "Declared",
            compilerStatus,
            runtimeStatus = "RuntimeVerified",
            runtimeBinding = "internal_execution_pipeline"
        };
}
