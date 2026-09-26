using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

var command = args.FirstOrDefault() ?? string.Empty;
var options = Arguments.Parse(args.Skip(1).ToArray());
var commandDefinitions = new CommandDefinition[]
{
    new("audit-knowledge", AuditKnowledge),
    new("audit-evidence", AuditEvidence),
    new("audit-claims", AuditClaims),
    new("audit-schedules", AuditSchedules),
    new("audit-current-schedules", AuditCurrentSchedules),
    new("audit-friendship-day-transition", AuditFriendshipDayTransition),
    new("validate-route-timing", ValidateRouteTiming),
    new("audit-current-social-frontier", AuditCurrentSocialFrontier),
    new("plan-current-social-day", PlanCurrentSocialDay),
    new("build-current-social-teacher-label", BuildCurrentSocialTeacherLabel),
    new("build-goal-method-graph", BuildGoalMethodGraph),
    new("build-goal-method-teacher-coverage", BuildGoalMethodTeacherCoverage),
    new("build-goal-method-coverage-reconciliation", BuildGoalMethodCoverageReconciliation),
    new("build-pet-love-teacher-corpus", BuildPetLoveTeacherCorpus),
    new("build-requirement-inventory", BuildRequirementInventory),
    new("build-acquisition-route-lowering", BuildAcquisitionRouteLowering),
    new("build-acquisition-route-calendar-resolution", BuildAcquisitionRouteCalendarResolution),
    new("build-current-acquisition-route-calendar-resolution", BuildCurrentAcquisitionRouteCalendarResolution),
    new("build-acquisition-route-target-date-calendar", BuildAcquisitionRouteTargetDateCalendar),
    new("build-current-acquisition-route-target-date-calendar", BuildCurrentAcquisitionRouteTargetDateCalendar),
    new("build-acquisition-route-target-date-unlock-state", BuildAcquisitionRouteTargetDateUnlockState),
    new("build-acquisition-route-target-date-festival-state", BuildAcquisitionRouteTargetDateFestivalState),
    new("build-acquisition-route-target-date-location-route", BuildAcquisitionRouteTargetDateLocationRoute),
    new("build-acquisition-route-target-date-facility-capacity", BuildAcquisitionRouteTargetDateFacilityCapacity),
    new("build-acquisition-route-target-date-resource-inputs", BuildAcquisitionRouteTargetDateResourceInputs),
    new("build-acquisition-route-target-date-currency-budget", BuildAcquisitionRouteTargetDateCurrencyBudget),
    new("build-acquisition-route-target-date-inventory-reservation", BuildAcquisitionRouteTargetDateInventoryReservation),
    new("build-acquisition-route-target-date-processing-lead-time", BuildAcquisitionRouteTargetDateProcessingLeadTime),
    new("build-acquisition-route-target-date-fishing-probability", BuildAcquisitionRouteTargetDateFishingProbability),
    new("build-acquisition-route-target-date-stochastic-retry-budget", BuildAcquisitionRouteTargetDateStochasticRetryBudget),
    new("build-acquisition-route-target-date-daily-time-energy-budget", BuildAcquisitionRouteTargetDateDailyTimeEnergyBudget),
    new("build-acquisition-route-target-date-opportunity-cost", BuildAcquisitionRouteTargetDateOpportunityCost),
    new("build-acquisition-route-portfolio-admission", BuildAcquisitionRoutePortfolioAdmission),
    new("build-acquisition-route-portfolio-teacher-preference", BuildAcquisitionRoutePortfolioTeacherPreference),
    new("build-acquisition-route-portfolio-commit-receipt", BuildAcquisitionRoutePortfolioCommitReceipt),
    new("compile-acquisition-route-dispatch", CompileAcquisitionRouteDispatch),
    new("build-acquisition-route-execution-binding", BuildAcquisitionRouteExecutionBinding),
    new("build-acquisition-route-fresh-terminal-receipt", BuildAcquisitionRouteFreshTerminalReceipt),
    new("build-acquisition-route-supporting-transition-request", BuildAcquisitionRouteSupportingTransitionRequest),
    new("build-acquisition-route-supporting-transition-commit-receipt", BuildAcquisitionRouteSupportingTransitionCommitReceipt),
    new("compile-acquisition-route-supporting-transition", CompileAcquisitionRouteSupportingTransition),
    new("build-acquisition-route-supporting-transition-receipt", BuildAcquisitionRouteSupportingTransitionReceipt),
    new("build-acquisition-route-supporting-transition-settlement-request", BuildAcquisitionRouteSupportingTransitionSettlementRequest),
    new("build-acquisition-route-supporting-transition-settlement-receipt", BuildAcquisitionRouteSupportingTransitionSettlementReceipt),
    new("build-acquisition-route-supporting-transition-replan", BuildAcquisitionRouteSupportingTransitionReplan),
    new("build-acquisition-route-supporting-transition-portfolio-teacher-preference", BuildAcquisitionRouteSupportingTransitionPortfolioTeacherPreference),
    new("build-acquisition-route-supporting-transition-portfolio-commit-receipt", BuildAcquisitionRouteSupportingTransitionPortfolioCommitReceipt),
    new("build-acquisition-route-portfolio-settlement-request", BuildAcquisitionRoutePortfolioSettlementRequest),
    new("build-acquisition-route-portfolio-settlement-receipt", BuildAcquisitionRoutePortfolioSettlementReceipt),
    new("build-acquisition-route-portfolio-rollout-checkpoint", BuildAcquisitionRoutePortfolioRolloutCheckpoint),
    new("build-acquisition-route-portfolio-rollout-proof-receipt", BuildAcquisitionRoutePortfolioRolloutProofReceipt),
    new("build-acquisition-route-portfolio-rollout-admission-receipt", BuildAcquisitionRoutePortfolioRolloutAdmissionReceipt),
    new("build-acquisition-route-portfolio-supervision-dataset", BuildAcquisitionRoutePortfolioSupervisionDataset),
    new("build-acquisition-route-portfolio-supervision-corpus", BuildAcquisitionRoutePortfolioSupervisionCorpus),
    new("train-acquisition-route-goal-method", TrainAcquisitionRouteGoalMethod),
    new("score-acquisition-route-goal-method-row", ScoreAcquisitionRouteGoalMethodRow),
    new("score-live-acquisition-route-goal-method-shadow", ScoreLiveAcquisitionRouteGoalMethodShadow),
    new("select-strategic-method", SelectStrategicMethod),
    new("build-acquisition-route-portfolio-continuation-teacher-request", BuildAcquisitionRoutePortfolioContinuationTeacherRequest),
    new("build-acquisition-route-portfolio-continuation-teacher-preference", BuildAcquisitionRoutePortfolioContinuationTeacherPreference),
    new("build-acquisition-route-portfolio-continuation-commit-receipt", BuildAcquisitionRoutePortfolioContinuationCommitReceipt),
    new("build-acquisition-route-continuation-execution-binding", BuildAcquisitionRouteContinuationExecutionBinding),
    new("build-acquisition-route-continuation-fresh-terminal-receipt", BuildAcquisitionRouteContinuationFreshTerminalReceipt),
    new("build-acquisition-route-portfolio-continuation-settlement-request", BuildAcquisitionRoutePortfolioContinuationSettlementRequest),
    new("build-acquisition-route-portfolio-continuation-settlement-receipt", BuildAcquisitionRoutePortfolioContinuationSettlementReceipt),
    new("build-acquisition-route-portfolio-continuation-rollout-checkpoint", BuildAcquisitionRoutePortfolioContinuationRolloutCheckpoint),
    new("build-current-full-shipment-teacher-frontier", BuildCurrentFullShipmentTeacherFrontier),
    new("build-current-community-center-denominator", BuildCurrentCommunityCenterDenominator),
    new("build-current-collection-teacher-frontier", BuildCurrentCollectionTeacherFrontier),
    new("build-current-master-angler-teacher-frontier", BuildCurrentMasterAnglerTeacherFrontier),
    new("build-current-stage-one-collection-teacher-frontier", BuildCurrentStageOneCollectionTeacherFrontier),
    new("build-current-stage-one-collection-teacher-preference", BuildCurrentStageOneCollectionTeacherPreference),
    new("build-current-stage-one-collection-teacher-receipt", BuildCurrentStageOneCollectionTeacherReceipt),
    new("build-community-center-lifecycle-receipt", BuildCommunityCenterLifecycleReceipt),
    new("build-full-shipment-settlement-receipt", BuildFullShipmentSettlementReceipt),
    new("build-full-shipment-recurrence-proof-receipt", BuildFullShipmentRecurrenceProofReceipt),
    new("build-full-shipment-terminal-settlement-receipt", BuildFullShipmentTerminalSettlementReceipt),
    new("build-master-angler-opportunity-catalog", BuildMasterAnglerOpportunityCatalog),
    new("build-master-angler-stage-one-windows", BuildMasterAnglerStageOneWindows),
    new("build-master-angler-target-date-intents", BuildMasterAnglerTargetDateIntents),
    new("rehash-content", RehashContent),
    new("teacher-plan", BuildTeacherPlan),
    new("import-legacy", ImportLegacy),
    new("validate", Validate),
    new("semanticize-recording", SemanticizeRecording),
    new("retrieve", Retrieve),
    new("self-test", SelfTest),
    new("self-test-current-collection", SelfTestCurrentCollection),
    new("self-test-current-community-center-denominator", SelfTestCurrentCommunityCenterDenominator),
    new("self-test-current-stage-one-collection", SelfTestCurrentStageOneCollection),
    new("self-test-full-shipment-settlement", _ => BootstrapSelfTest.RunFullShipmentSettlement()),
    new("self-test-acquisition-route-dispatch", _ => BootstrapSelfTest.RunAcquisitionRouteDispatch()),
    new("self-test-bootstrap-hermetic", _ => BootstrapSelfTest.RunHermeticCriticalPaths()),
    new("self-test-goal-method-incomparable-live-shadow", SelfTestGoalMethodIncomparableLiveShadow),
    new("self-test-goal-method-teacher-coverage", SelfTestGoalMethodTeacherCoverage),
};
var commandRegistry = commandDefinitions.ToDictionary(
    definition => definition.Name,
    StringComparer.Ordinal);
try
{
    if (!commandRegistry.TryGetValue(command, out var definition))
    {
        throw new ArgumentException(
            "Command must be " + string.Join(", ",
                commandDefinitions.Select(value => value.Name)) + ".");
    }
    definition.Execute(options);
}
catch (Exception ex)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new
    {
        status = "error",
        error = ex.GetType().Name,
        message = ex.Message
    }, JsonDefaults.Options));
    Environment.ExitCode = 1;
}

static void AuditKnowledge(Arguments options)
{
    var knowledgePath = options.Required("knowledge");
    var output = options.Required("output");
    var knowledge = KnowledgeIndex.Load(knowledgePath);
    Write(output, new
    {
        schema_version = BootstrapSchemas.KnowledgeAudit,
        status = "pass",
        knowledge_path = Path.GetFullPath(knowledgePath),
        knowledge_sha256 = knowledge.Sha256,
        knowledge_schema = knowledge.SchemaVersion,
        goal_id = knowledge.GoalId,
        target_score = knowledge.TargetScore,
        criterion_count = knowledge.CriterionCount,
        criterion_point_sum = knowledge.Criteria.Sum(value => value.Points),
        criteria = knowledge.Criteria
    });
}

static void AuditEvidence(Arguments options)
{
    var report = EvidenceAuditor.Audit(
        options.Required("lock"),
        options.Required("knowledge-root"),
        options.Required("content-root"));
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void AuditClaims(Arguments options)
{
    var report = ClaimConflictAuditor.Audit(
        options.Required("ledger"),
        options.Required("knowledge"),
        options.Required("decompile-root"),
        options.Required("knowledge-root"));
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void AuditSchedules(Arguments options)
{
    var report = NpcScheduleGrammarAuditor.Audit(
        options.Required("contract"),
        options.Required("manifest"),
        options.Required("decompile-root"));
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void AuditCurrentSchedules(Arguments options)
{
    using var snapshot = JsonDocument.Parse(File.ReadAllText(options.Required("snapshot")));
    var report = new NpcCurrentScheduleSnapshotAuditor().Audit(snapshot.RootElement);
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void AuditFriendshipDayTransition(Arguments options)
{
    using var before = JsonDocument.Parse(File.ReadAllText(options.Required("before")));
    using var after = JsonDocument.Parse(File.ReadAllText(options.Required("after")));
    var report = new FriendshipDayTransitionSnapshotAuditor().Audit(
        before.RootElement,
        after.RootElement);
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void ValidateRouteTiming(Arguments options)
{
    var artifactPath = Path.GetFullPath(options.Required("artifact"));
    var snapshotPath = Path.GetFullPath(options.Required("snapshot"));
    using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotPath));
    var root = snapshot.RootElement;
    var gameVersion = root.GetProperty("game_version").GetString()
        ?? throw new InvalidDataException("Snapshot game_version is missing.");
    var totalDays = root
        .GetProperty("state")
        .GetProperty("time")
        .GetProperty("total_days")
        .GetProperty("value")
        .GetInt32();
    var movementContext = root
        .GetProperty("state")
        .GetProperty("player")
        .GetProperty("movement_timing_context");
    var result = new FutureRouteTimingCalibrationLoader().Load(
        File.ReadAllText(artifactPath),
        movementContext,
        gameVersion,
        totalDays);
    Write(options.Required("output"), new
    {
        schema_version = "stardewai.route_timing_calibration_validation.v1",
        status = result.Status == FutureRouteTimingCalibrationLoadStatus.Loaded
            ? "pass"
            : "blocked",
        artifact_path = artifactPath,
        snapshot_path = snapshotPath,
        game_version = gameVersion,
        target_total_days = totalDays,
        calibration = result.Calibration,
        blocking_reasons = result.BlockingReasons
    });
    if (result.Status != FutureRouteTimingCalibrationLoadStatus.Loaded)
        Environment.ExitCode = 2;
}

static void AuditCurrentSocialFrontier(Arguments options)
{
    using var snapshot = JsonDocument.Parse(
        File.ReadAllText(options.Required("snapshot")));
    var frontier = new CurrentSocialContactFrontierProducer().Produce(
        snapshot.RootElement,
        File.ReadAllText(options.Required("calibration")));
    Write(options.Required("output"), frontier);
    if (!frontier.RankingAdmissionReady)
        Environment.ExitCode = 2;
}

static void PlanCurrentSocialDay(Arguments options)
{
    using var snapshot = JsonDocument.Parse(
        File.ReadAllText(options.Required("snapshot")));
    var plan = new CurrentSocialDayItineraryPlanner().Plan(
        snapshot.RootElement,
        File.ReadAllText(options.Required("calibration")));
    Write(options.Required("output"), plan);
    if (!plan.TrainingLabelEligible &&
        plan.Status != "goal_already_satisfied")
    {
        Environment.ExitCode = 2;
    }
}

static void BuildCurrentSocialTeacherLabel(Arguments options)
{
    using var snapshot = JsonDocument.Parse(
        File.ReadAllText(options.Required("snapshot")));
    var label = new CurrentSocialDayTeacherLabelBuilder().Build(
        snapshot.RootElement,
        File.ReadAllText(options.Required("calibration")));
    Write(options.Required("output"), label);
    if (!label.TrainingLabelEligible)
        Environment.ExitCode = 2;
}

static void BuildGoalMethodGraph(Arguments options)
{
    var inputs = GoalMethodFrontierInputs(options);
    var report = GoalMethodFrontierBuilder.Build(
        inputs.ExpansionPath,
        inputs.DependencyExpansionPath,
        inputs.IsolatedTrainingAuthorizationPath,
        inputs.RequirementInventoryPath,
        inputs.AcquisitionLoweringPath,
        inputs.AcquisitionLoweringCatalogPath,
        inputs.KnowledgePath,
        inputs.OptionMatrixPath,
        inputs.ClaimLedgerPath,
        inputs.DirectionCatalogSourcePath);
    Write(options.Required("output"), report);
}

static void BuildGoalMethodTeacherCoverage(Arguments options)
{
    var report = GoalMethodTeacherCoverageBuilder.Build(
        GoalMethodFrontierInputs(options),
        options.Required("request"));
    Write(options.Required("output"), report);
    if (!report.CoverageGateSatisfied)
        Environment.ExitCode = 2;
}

static void BuildGoalMethodCoverageReconciliation(Arguments options)
{
    var report = GoalMethodCoverageReconciliationBuilder.Build(
        GoalMethodFrontierInputs(options),
        options.Required("request"));
    Write(options.Required("output"), report);
}

static void BuildPetLoveTeacherCorpus(Arguments options)
{
    var corpus = PetLoveTeacherCorpusBuilder.Build(
        options.Required("request"));
    Write(options.Required("output"), corpus);
}

static GoalMethodFrontierBuildInputs GoalMethodFrontierInputs(
    Arguments options) => new()
    {
        ExpansionPath = options.Required("expansion"),
        DependencyExpansionPath = options.Required("dependencies"),
        IsolatedTrainingAuthorizationPath =
            options.Required("isolated-training-authorization"),
        RequirementInventoryPath = options.Required("requirement-inventory"),
        AcquisitionLoweringPath = options.Required("acquisition-lowering"),
        AcquisitionLoweringCatalogPath =
            options.Required("acquisition-lowering-catalog"),
        KnowledgePath = options.Required("knowledge"),
        OptionMatrixPath = options.Required("option-matrix"),
        ClaimLedgerPath = options.Required("claim-ledger"),
        DirectionCatalogSourcePath =
            options.Required("direction-catalog-source")
    };

static void BuildRequirementInventory(Arguments options)
{
    var report = AuthoritativeRequirementInventoryBuilder.Build(
        options.Required("manifest"),
        options.Required("knowledge"),
        options.Required("authoritative-graph"),
        options.Required("decompile-root"));
    Write(options.Required("output"), report);
    if (!report.DenominatorComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteLowering(Arguments options)
{
    var report = AcquisitionRouteOptionLoweringBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("catalog"),
        options.Required("option-matrix"),
        options.Required("isolated-training-authorization"));
    Write(options.Required("output"), report);
}

static void BuildAcquisitionRouteCalendarResolution(Arguments options)
{
    var report = AcquisitionRouteCalendarResolutionBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildCurrentAcquisitionRouteCalendarResolution(Arguments options)
{
    var report = AcquisitionRouteCalendarResolutionBuilder.BuildCurrent(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateCalendar(Arguments options)
{
    var report = AcquisitionRouteTargetDateCalendarBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Int("target-total-day", -1));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildCurrentAcquisitionRouteTargetDateCalendar(Arguments options)
{
    var report = AcquisitionRouteTargetDateCalendarBuilder.BuildCurrent(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("snapshot"),
        options.Int("target-total-day", -1));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateUnlockState(Arguments options)
{
    var report = AcquisitionRouteTargetDateUnlockBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateFestivalState(Arguments options)
{
    var report = AcquisitionRouteTargetDateFestivalBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateLocationRoute(Arguments options)
{
    var report = AcquisitionRouteTargetDateLocationBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateFacilityCapacity(Arguments options)
{
    var report = AcquisitionRouteTargetDateFacilityBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateResourceInputs(Arguments options)
{
    var report = AcquisitionRouteTargetDateResourceBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateCurrencyBudget(Arguments options)
{
    var report = AcquisitionRouteTargetDateCurrencyBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateInventoryReservation(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateReservationBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateProcessingLeadTime(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateProcessingBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("target-date-reservation"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateStochasticRetryBudget(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateStochasticRetryBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("target-date-reservation"),
        options.Required("target-date-processing"),
        options.Required("target-date-fishing-probability"),
        options.Required("fishing-forecast-manifest"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateDailyTimeEnergyBudget(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("target-date-reservation"),
        options.Required("target-date-processing"),
        options.Required("target-date-fishing-probability"),
        options.Required("target-date-stochastic-retry"),
        options.Required("fishing-forecast-manifest"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteTargetDateOpportunityCost(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateOpportunityCostBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("target-date-reservation"),
        options.Required("target-date-processing"),
        options.Required("target-date-fishing-probability"),
        options.Required("target-date-stochastic-retry"),
        options.Required("target-date-daily-time-energy"),
        options.Required("fishing-forecast-manifest"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioAdmission(Arguments options)
{
    var report = AcquisitionRoutePortfolioBuilder.Build(
        RoutePortfolioInputs(options));
    Write(options.Required("output"), report);
    if (!report.PortfolioAdmissionReady)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioTeacherPreference(Arguments options)
{
    var report = AcquisitionRoutePortfolioTeacherPreferenceBuilder.Build(
        RoutePortfolioInputs(options, requireProposal: false),
        options.Required("preference-request"));
    Write(options.Required("output"), report);
    if (report.TeacherPreferenceLabelEligible)
    {
        if (options.Optional("selected-proposal-output") is { } proposalOutput)
            Write(proposalOutput, report.SelectedProposal!);
        if (options.Optional("selected-admission-output") is { } admissionOutput)
            Write(admissionOutput, report.SelectedAdmission!);
    }
    else
    {
        Environment.ExitCode = 2;
    }
}

static void BuildAcquisitionRoutePortfolioCommitReceipt(Arguments options)
{
    var report = AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
        RoutePortfolioInputs(options),
        options.Required("portfolio-admission"),
        options.Required("committed-ledger"),
        options.Optional("commit-result"));
    Write(options.Required("output"), report);
    if (!report.PortfolioCommitVerified)
        Environment.ExitCode = 2;
}

static AcquisitionRoutePortfolioInputs RoutePortfolioInputs(
    Arguments options,
    bool requireProposal = true) => new()
    {
        RequirementInventoryPath = options.Required("requirement-inventory"),
        AcquisitionLoweringPath = options.Required("acquisition-lowering"),
        MasterAnglerWindowsPath = options.Required("master-angler-windows"),
        CalendarResolutionPath = options.Required("calendar-resolution"),
        TargetDateCalendarPath = options.Required("target-date-calendar"),
        TargetDateUnlockPath = options.Required("target-date-unlock"),
        TargetDateFestivalPath = options.Required("target-date-festival"),
        TargetDateLocationPath = options.Required("target-date-location"),
        TargetDateFacilityPath = options.Required("target-date-facility"),
        TargetDateResourcePath = options.Required("target-date-resource"),
        TargetDateCurrencyPath = options.Required("target-date-currency"),
        TargetDateReservationPath = options.Required("target-date-reservation"),
        TargetDateProcessingPath = options.Required("target-date-processing"),
        TargetDateFishingProbabilityPath = options.Required(
            "target-date-fishing-probability"),
        TargetDateStochasticRetryPath = options.Required(
            "target-date-stochastic-retry"),
        TargetDateDailyTimeEnergyPath = options.Required(
            "target-date-daily-time-energy"),
        TargetDateOpportunityCostPath = options.Required(
            "target-date-opportunity-cost"),
        FishingForecastManifestPath = options.Required(
            "fishing-forecast-manifest"),
        StrategyLedgerPath = options.Required("strategy-ledger"),
        SnapshotPath = options.Required("snapshot"),
        RouteTimingCalibrationPath = options.Required(
            "route-timing-calibration"),
        ProposalPath = requireProposal
            ? options.Required("proposal")
            : options.Optional("proposal") ?? string.Empty
    };

static void BuildAcquisitionRouteExecutionBinding(Arguments options)
{
    var binding = AcquisitionRouteExecutionBindingBuilder.Build(
        RouteExecutionBindingInputs(options));
    Write(options.Required("output"), binding);
    if (!binding.DispatchBindingReady)
        Environment.ExitCode = 2;
}

static void CompileAcquisitionRouteDispatch(Arguments options)
{
    var queueOutput = options.Required("queue-output");
    var reportOutput = options.Required("output");
    if (string.Equals(
            Path.GetFullPath(queueOutput),
            Path.GetFullPath(reportOutput),
            StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException(
            "--queue-output and --output must be different paths.");
    }
    var compilation = AcquisitionRouteDispatchCompilationBuilder.Build(
        RouteExecutionBindingInputs(options, queueOutput),
        options.Required("ranking"));
    Write(reportOutput, compilation);
    if (compilation.DispatchReady && compilation.ActionQueue is not null)
        Write(queueOutput, compilation.ActionQueue);
    else
    {
        Write(queueOutput, BlockedAcquisitionRouteQueue(compilation));
        Environment.ExitCode = 2;
    }
}

static ActionQueueEnvelope BlockedAcquisitionRouteQueue(
    AcquisitionRouteDispatchCompilation compilation) => new()
    {
        QueueId = "blocked:acquisition-route:" +
            compilation.RouteOccurrenceId,
        SourceModelOutputId = compilation.SelectedCandidateId,
        SourceModel =
            "deterministic_teacher.acquisition_route_dispatch.v1",
        StateHash = compilation.SourceStateHash,
        GoalId = compilation.GoalId,
        ExecutionMode = "training_singleplayer",
        Status = "blocked",
        CompilerDiagnostics = compilation.BlockingReasons.Length > 0
            ? compilation.BlockingReasons
            : new[] { "acquisition_route_dispatch_not_ready" }
    };

static void BuildAcquisitionRouteFreshTerminalReceipt(Arguments options)
{
    var report = AcquisitionRouteFreshTerminalReceiptBuilder.Build(
        RouteExecutionBindingInputs(options),
        options.Required("execution-binding"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("run-id"),
        options.Required("executor-version"));
    Write(options.Required("output"), report);
    if (!report.FreshTerminalReceiptVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteSupportingTransitionReceipt(Arguments options)
{
    var report = AcquisitionRouteSupportingTransitionReceiptBuilder.Build(
        options.Required("compilation"),
        options.Required("before-snapshot"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("run-id"),
        options.Required("executor-version"));
    Write(options.Required("output"), report);
    if (!report.SupportingTransitionVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteSupportingTransitionSettlementRequest(
    Arguments options)
{
    var request = AcquisitionRouteSupportingTransitionSettlementBuilder
        .BuildRequest(
            RouteSupportingTransitionInputs(options),
            options.Required("support-request"),
            options.Required("support-commit-receipt"),
            options.Required("committed-ledger"),
            options.Required("commit-result"),
            options.Required("compilation"),
            options.Required("execution-receipt"),
            options.Required("after-snapshot"),
            options.Required("supporting-transition-receipt"),
            options.Required("run-id"),
            options.Required("executor-version"));
    Write(options.Required("output"), request);
}

static void BuildAcquisitionRouteSupportingTransitionSettlementReceipt(
    Arguments options)
{
    var receipt = AcquisitionRouteSupportingTransitionSettlementBuilder
        .BuildReceipt(
            RouteSupportingTransitionInputs(options),
            options.Required("support-request"),
            options.Required("support-commit-receipt"),
            options.Required("committed-ledger"),
            options.Required("commit-result"),
            options.Required("compilation"),
            options.Required("execution-receipt"),
            options.Required("after-snapshot"),
            options.Required("supporting-transition-receipt"),
            options.Required("run-id"),
            options.Required("executor-version"),
            options.Required("settlement-request"),
            options.Required("settlement-result"),
            options.Required("settled-ledger"));
    Write(options.Required("output"), receipt);
    if (!receipt.ReservationLifecycleVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteSupportingTransitionReplan(Arguments options)
{
    var output = options.Required("output");
    var requestOutput = options.Required("preference-request-output");
    if (string.Equals(
            Path.GetFullPath(output),
            Path.GetFullPath(requestOutput),
            StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException(
            "--output and --preference-request-output must be different paths.");
    }
    var admission = AcquisitionRouteSupportingTransitionReplanBuilder.Build(
        RouteSupportingTransitionInputs(options),
        SupportingTransitionSettlementProof(options),
        ContinuationRoutePortfolioInputs(options));
    Write(output, admission);
    if (admission.FreshTeacherRequestReady &&
        admission.NextTeacherPreferenceRequest is not null)
    {
        Write(requestOutput, admission.NextTeacherPreferenceRequest);
    }
    else
    {
        Environment.ExitCode = 2;
    }
}

static void BuildAcquisitionRouteSupportingTransitionPortfolioTeacherPreference(
    Arguments options)
{
    var preference = AcquisitionRouteSupportingTransitionPortfolioBuilder
        .BuildTeacherPreference(
            RouteSupportingTransitionInputs(options),
            SupportingTransitionSettlementProof(options),
            ContinuationRoutePortfolioInputs(options),
            options.Required("replan-admission"),
            options.Required("preference-request"));
    Write(options.Required("output"), preference);
    if (preference.TeacherPreferenceLabelEligible)
    {
        if (options.Optional("selected-proposal-output") is { } proposalOutput)
            Write(proposalOutput, preference.SelectedProposal!);
        if (options.Optional("selected-admission-output") is { } admissionOutput)
            Write(admissionOutput, preference.SelectedAdmission!);
    }
    else
    {
        Environment.ExitCode = 2;
    }
}

static void BuildAcquisitionRouteSupportingTransitionPortfolioCommitReceipt(
    Arguments options)
{
    var receipt = AcquisitionRouteSupportingTransitionPortfolioBuilder
        .BuildCommitReceipt(
            RouteSupportingTransitionInputs(options),
            SupportingTransitionSettlementProof(options),
            ContinuationRoutePortfolioInputs(options, requireProposal: true),
            options.Required("replan-admission"),
            options.Required("preference-request"),
            options.Required("preference"),
            options.Required("next-portfolio-admission"),
            options.Required("next-committed-ledger"),
            options.Optional("next-commit-result"));
    Write(options.Required("output"), receipt);
    if (!receipt.PortfolioCommitVerified)
        Environment.ExitCode = 2;
}

static AcquisitionRouteSupportingTransitionSettlementProof
    SupportingTransitionSettlementProof(Arguments options) => new()
    {
        SupportRequestPath = options.Required("support-request"),
        SupportCommitReceiptPath = options.Required(
            "support-commit-receipt"),
        CommittedLedgerPath = options.Required("committed-ledger"),
        CommitResultPath = options.Required("commit-result"),
        CompilationPath = options.Required("compilation"),
        ExecutionReceiptPath = options.Required("execution-receipt"),
        AfterSnapshotPath = options.Required("after-snapshot"),
        SupportingTransitionReceiptPath = options.Required(
            "supporting-transition-receipt"),
        RunId = options.Required("run-id"),
        ExecutorVersion = options.Required("executor-version"),
        SettlementRequestPath = options.Required("settlement-request"),
        SettlementResultPath = options.Required("settlement-result"),
        SettledLedgerPath = options.Required("settled-ledger"),
        SettlementReceiptPath = options.Required("settlement-receipt")
    };

static void BuildAcquisitionRouteSupportingTransitionRequest(Arguments options)
{
    var report = AcquisitionRouteSupportingTransitionRequestBuilder.Build(
        RouteSupportingTransitionInputs(options));
    Write(options.Required("output"), report);
    if (!report.SupportRequestReady)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteSupportingTransitionCommitReceipt(
    Arguments options)
{
    var report =
        AcquisitionRouteSupportingTransitionCommitReceiptBuilder.Build(
            RouteSupportingTransitionInputs(options),
            options.Required("support-request"),
            options.Required("committed-ledger"),
            options.Required("commit-result"));
    Write(options.Required("output"), report);
    if (!report.SupportReservationCommitVerified)
        Environment.ExitCode = 2;
}

static void CompileAcquisitionRouteSupportingTransition(Arguments options)
{
    var queueOutput = options.Required("queue-output");
    var reportOutput = options.Required("output");
    if (string.Equals(
            Path.GetFullPath(queueOutput),
            Path.GetFullPath(reportOutput),
            StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException(
            "--queue-output and --output must be different paths.");
    }
    var compilation =
        AcquisitionRouteSupportingTransitionCompilationBuilder.Build(
            RouteSupportingTransitionInputs(options),
            options.Required("support-request"),
            options.Required("support-commit-receipt"),
            options.Required("committed-ledger"),
            options.Required("commit-result"));
    Write(reportOutput, compilation);
    if (compilation.DispatchReady && compilation.ActionQueue is not null)
        Write(queueOutput, compilation.ActionQueue);
    else
    {
        Write(queueOutput, BlockedAcquisitionRouteQueue(compilation));
        Environment.ExitCode = 2;
    }
}

static AcquisitionRouteSupportingTransitionRequestInputs
    RouteSupportingTransitionInputs(Arguments options) => new()
    {
        RequirementInventoryPath = options.Required("requirement-inventory"),
        AcquisitionLoweringPath = options.Required("acquisition-lowering"),
        MasterAnglerWindowsPath = options.Required("master-angler-windows"),
        CalendarResolutionPath = options.Required("calendar-resolution"),
        TargetDateCalendarPath = options.Required("target-date-calendar"),
        TargetDateUnlockPath = options.Required("target-date-unlock"),
        TargetDateFestivalPath = options.Required("target-date-festival"),
        TargetDateLocationPath = options.Required("target-date-location"),
        TargetDateFacilityPath = options.Required("target-date-facility"),
        TargetDateResourcePath = options.Required("target-date-resource"),
        TargetDateCurrencyPath = options.Required("target-date-currency"),
        TargetDateReservationPath = options.Required(
            "target-date-reservation"),
        TargetDateProcessingPath = options.Required("target-date-processing"),
        StrategyLedgerPath = options.Required("strategy-ledger"),
        SnapshotPath = options.Required("snapshot"),
        RouteTimingCalibrationPath = options.Required(
            "route-timing-calibration"),
        RankingPath = options.Required("ranking"),
        RouteOccurrenceId = options.Required("route-occurrence-id"),
        SupportDeadlineTotalDay = options.Int(
            "support-deadline-total-day",
            -1)
    };

static void BuildAcquisitionRoutePortfolioSettlementRequest(Arguments options)
{
    var request = AcquisitionRoutePortfolioSettlementBuilder.BuildRequest(
        RouteExecutionBindingInputs(options),
        options.Required("execution-binding"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("fresh-terminal-receipt"),
        options.Required("run-id"),
        options.Required("executor-version"));
    Write(options.Required("output"), request);
}

static void BuildAcquisitionRoutePortfolioSettlementReceipt(Arguments options)
{
    var receipt = AcquisitionRoutePortfolioSettlementBuilder.BuildReceipt(
        RouteExecutionBindingInputs(options),
        options.Required("execution-binding"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("fresh-terminal-receipt"),
        options.Required("run-id"),
        options.Required("executor-version"),
        options.Required("settlement-request"),
        options.Required("settlement-result"),
        options.Required("settled-ledger"));
    Write(options.Required("output"), receipt);
    if (!receipt.ReservationLifecycleVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioRolloutCheckpoint(Arguments options)
{
    var checkpoint =
        AcquisitionRoutePortfolioRolloutCheckpointBuilder.BuildInitial(
            RouteExecutionBindingInputs(options),
            options.Required("execution-binding"),
            options.Required("execution-receipt"),
            options.Required("after-snapshot"),
            options.Required("fresh-terminal-receipt"),
            options.Required("run-id"),
            options.Required("executor-version"),
            options.Required("settlement-request"),
            options.Required("settlement-result"),
            options.Required("settled-ledger"),
            options.Required("settlement-receipt"));
    Write(options.Required("output"), checkpoint);
}

static void BuildAcquisitionRoutePortfolioContinuationTeacherRequest(
    Arguments options)
{
    var currentInputs = ContinuationRoutePortfolioInputs(options);
    var request = AcquisitionRoutePortfolioContinuationBuilder.BuildRequest(
        VerifiedContinuationCheckpoint(options),
        currentInputs);
    Write(options.Required("output"), request);
}

static void BuildAcquisitionRoutePortfolioContinuationTeacherPreference(
    Arguments options)
{
    var currentInputs = ContinuationRoutePortfolioInputs(options);
    var requestPath = options.Required("continuation-request");
    var preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
        .BuildContinuation(
            VerifiedContinuationCheckpoint(options),
            currentInputs,
            requestPath);
    Write(options.Required("output"), preference);
    if (preference.TeacherPreferenceLabelEligible)
    {
        if (options.Optional("selected-proposal-output") is { } proposalOutput)
            Write(proposalOutput, preference.SelectedProposal!);
        if (options.Optional("selected-admission-output") is { } admissionOutput)
            Write(admissionOutput, preference.SelectedAdmission!);
    }
    else
    {
        Environment.ExitCode = 2;
    }
}

static void BuildAcquisitionRoutePortfolioContinuationCommitReceipt(
    Arguments options)
{
    var currentInputs = ContinuationRoutePortfolioInputs(
        options,
        requireProposal: true);
    var requestPath = options.Required("continuation-request");
    var preferencePath = options.Required("continuation-preference");
    var admissionPath = options.Required("next-portfolio-admission");
    var ledgerPath = options.Required("next-committed-ledger");
    var resultPath = options.Required("next-commit-result");
    var receipt = AcquisitionRoutePortfolioCommitReceiptBuilder
        .BuildContinuation(
            VerifiedContinuationCheckpoint(options),
            currentInputs,
            requestPath,
            preferencePath,
            admissionPath,
            ledgerPath,
            resultPath);
    Write(options.Required("output"), receipt);
    if (!receipt.PortfolioCommitVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteContinuationExecutionBinding(
    Arguments options)
{
    var requestPath = options.Required("continuation-request");
    var inputs = ContinuationRouteExecutionBindingInputs(options);
    var binding = AcquisitionRouteExecutionBindingBuilder.BuildContinuation(
        VerifiedContinuationCheckpoint(options),
        requestPath,
        inputs);
    Write(options.Required("output"), binding);
    if (!binding.DispatchBindingReady)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRouteContinuationFreshTerminalReceipt(
    Arguments options)
{
    var requestPath = options.Required("continuation-request");
    var inputs = ContinuationRouteExecutionBindingInputs(options);
    var bindingPath = options.Required("next-execution-binding");
    var executionReceiptPath = options.Required("next-execution-receipt");
    var afterPath = options.Required("next-after-snapshot");
    var runId = options.Required("next-run-id");
    var executorVersion = options.Required("next-executor-version");
    var receipt = AcquisitionRouteFreshTerminalReceiptBuilder
        .BuildContinuation(
            VerifiedContinuationCheckpoint(options),
            requestPath,
            inputs,
            bindingPath,
            executionReceiptPath,
            afterPath,
            runId,
            executorVersion);
    Write(options.Required("output"), receipt);
    if (!receipt.FreshTerminalReceiptVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioContinuationSettlementRequest(
    Arguments options)
{
    var continuationRequestPath = options.Required("continuation-request");
    var inputs = ContinuationRouteExecutionBindingInputs(options);
    var bindingPath = options.Required("next-execution-binding");
    var receiptPath = options.Required("next-execution-receipt");
    var afterPath = options.Required("next-after-snapshot");
    var freshPath = options.Required("next-fresh-terminal-receipt");
    var runId = options.Required("next-run-id");
    var executorVersion = options.Required("next-executor-version");
    var request = AcquisitionRoutePortfolioSettlementBuilder
        .BuildContinuationRequest(
            VerifiedContinuationCheckpoint(options),
            continuationRequestPath,
            inputs,
            bindingPath,
            receiptPath,
            afterPath,
            freshPath,
            runId,
            executorVersion);
    Write(options.Required("output"), request);
}

static void BuildAcquisitionRoutePortfolioContinuationSettlementReceipt(
    Arguments options)
{
    var continuationRequestPath = options.Required("continuation-request");
    var inputs = ContinuationRouteExecutionBindingInputs(options);
    var bindingPath = options.Required("next-execution-binding");
    var executionReceiptPath = options.Required("next-execution-receipt");
    var afterPath = options.Required("next-after-snapshot");
    var freshPath = options.Required("next-fresh-terminal-receipt");
    var runId = options.Required("next-run-id");
    var executorVersion = options.Required("next-executor-version");
    var settlementRequestPath = options.Required("next-settlement-request");
    var settlementResultPath = options.Required("next-settlement-result");
    var settledLedgerPath = options.Required("next-settled-ledger");
    var receipt = AcquisitionRoutePortfolioSettlementBuilder
        .BuildContinuationReceipt(
            VerifiedContinuationCheckpoint(options),
            continuationRequestPath,
            inputs,
            bindingPath,
            executionReceiptPath,
            afterPath,
            freshPath,
            runId,
            executorVersion,
            settlementRequestPath,
            settlementResultPath,
            settledLedgerPath);
    Write(options.Required("output"), receipt);
    if (!receipt.ReservationLifecycleVerified)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioContinuationRolloutCheckpoint(
    Arguments options)
{
    var continuationRequestPath = options.Required("continuation-request");
    var inputs = ContinuationRouteExecutionBindingInputs(options);
    var bindingPath = options.Required("next-execution-binding");
    var executionReceiptPath = options.Required("next-execution-receipt");
    var afterPath = options.Required("next-after-snapshot");
    var freshPath = options.Required("next-fresh-terminal-receipt");
    var runId = options.Required("next-run-id");
    var executorVersion = options.Required("next-executor-version");
    var settlementRequestPath = options.Required("next-settlement-request");
    var settlementResultPath = options.Required("next-settlement-result");
    var settledLedgerPath = options.Required("next-settled-ledger");
    var settlementReceiptPath = options.Required("next-settlement-receipt");
    var checkpoint = AcquisitionRoutePortfolioRolloutCheckpointBuilder
        .BuildContinuation(
            VerifiedContinuationCheckpoint(options),
            continuationRequestPath,
            inputs,
            bindingPath,
            executionReceiptPath,
            afterPath,
            freshPath,
            runId,
            executorVersion,
            settlementRequestPath,
            settlementResultPath,
            settledLedgerPath,
            settlementReceiptPath);
    Write(options.Required("output"), checkpoint);
}

static void BuildAcquisitionRoutePortfolioRolloutProofReceipt(
    Arguments options)
{
    var receipt = AcquisitionRoutePortfolioRolloutProofBuilder.BuildReceipt(
        options.Required("rollout-proof-manifest"));
    Write(options.Required("output"), receipt);
}

static void BuildAcquisitionRoutePortfolioRolloutAdmissionReceipt(
    Arguments options)
{
    var receipt = AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
        options.Required("rollout-proof-manifest"),
        options.Required("rollout-proof-receipt"));
    Write(options.Required("output"), receipt);
    if (!receipt.ControllerAdmissionGranted)
        Environment.ExitCode = 2;
}

static void BuildAcquisitionRoutePortfolioSupervisionDataset(
    Arguments options)
{
    var dataset = AcquisitionRoutePortfolioSupervisionBuilder.Build(
        options.Required("rollout-proof-manifest"),
        options.Required("rollout-proof-receipt"),
        options.Required("rollout-admission-receipt"));
    Write(options.Required("output"), dataset);
}

static void BuildAcquisitionRoutePortfolioSupervisionCorpus(
    Arguments options)
{
    var result = AcquisitionRoutePortfolioSupervisionCorpusBuilder.Build(
        options.Required("request"),
        options.Required("output-root"));
    Console.WriteLine(result.ManifestPath);
}

static void TrainAcquisitionRouteGoalMethod(Arguments options)
{
    var hyperparameters = new GoalMethodPairwiseHyperparameters
    {
        Epochs = options.Int("epochs", 200),
        LearningRate = options.Double("learning-rate", 0.05),
        L2Regularization = options.Double("l2", 0.001)
    };
    var result = new GoalMethodPairwiseTrainer().Train(
        options.Required("corpus-manifest"),
        options.Required("checkpoint"),
        hyperparameters,
        options.Optional("initialize-from-checkpoint"));
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = "ok",
        checkpoint_path = result.CheckpointPath,
        checkpoint_sha256 = result.CheckpointSha256,
        checkpoint_id = result.Checkpoint.CheckpointId,
        model_kind = result.Checkpoint.ModelKind,
        training = result.Checkpoint.Training,
        formal_product_training_authorized =
            result.Checkpoint.FormalProductTrainingAuthorized
    }, JsonDefaults.Options));
}

static void ScoreAcquisitionRouteGoalMethodRow(Arguments options)
{
    var result = new GoalMethodPairwiseRanker().RankVerifiedCorpusRow(
        options.Required("checkpoint"),
        options.Required("corpus-manifest"),
        options.Required("row-id"));
    Write(options.Required("output"), result);
}

static void ScoreLiveAcquisitionRouteGoalMethodShadow(Arguments options)
{
    var result = new GoalMethodPairwiseRanker().RankLiveShadow(
        options.Required("checkpoint"),
        options.Required("corpus-manifest"),
        RoutePortfolioInputs(options, requireProposal: false),
        options.Required("preference-request"),
        options.Optional("prior-rollout-proof-manifest"));
    Write(options.Required("output"), result);
    if (result.ShadowSelectedProposal is null ||
        result.ShadowSelectedAdmission is null)
    {
        Environment.ExitCode = 2;
    }
}

static void SelectStrategicMethod(Arguments options)
{
    var triggerKinds = (options.Optional("replan-triggers") ??
            StrategicReplanTriggers.ExplicitRequest)
        .Split(',', StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    var result = new StrategicPolicy().SelectMethod(
        new StrategicPolicySelectionRequest
        {
            CurrentInputs = RoutePortfolioInputs(
                options,
                requireProposal: false),
            PreferenceRequestPath = options.Required("preference-request"),
            PriorRolloutProofManifestPath =
                options.Optional("prior-rollout-proof-manifest") ??
                string.Empty,
            CheckpointPath = options.Optional("checkpoint") ?? string.Empty,
            CorpusManifestPath = options.Optional("corpus-manifest") ??
                string.Empty,
            EnableDeterministicShadowAudit = string.Equals(
                options.Optional("deterministic-shadow-audit"),
                "true",
                StringComparison.OrdinalIgnoreCase),
            Replan = new StrategicReplanContext
            {
                TriggerKinds = triggerKinds,
                TriggerToken = options.Optional("replan-trigger-token") ??
                    string.Empty,
                PreviousReplanFingerprint = options.Optional(
                    "previous-replan-fingerprint") ?? string.Empty
            }
        });
    Write(options.Required("output"), result);
    if (result.SelectedProposal is null && !result.ReplanDeduplicated)
        Environment.ExitCode = 2;
}

static AcquisitionRoutePortfolioVerifiedCheckpoint
    VerifiedContinuationCheckpoint(Arguments options)
{
    var manifest = options.Optional("rollout-proof-manifest");
    return string.IsNullOrWhiteSpace(manifest)
        ? AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
            InitialCheckpointProof(options),
            options.Required("rollout-checkpoint"))
        : AcquisitionRoutePortfolioRolloutProofBuilder.Verify(manifest);
}

static AcquisitionRoutePortfolioInitialCheckpointProof InitialCheckpointProof(
    Arguments options) => new()
    {
        ExecutionInputs = RouteExecutionBindingInputs(options),
        ExecutionBindingPath = options.Required("execution-binding"),
        ExecutionReceiptPath = options.Required("execution-receipt"),
        AfterSnapshotPath = options.Required("after-snapshot"),
        FreshTerminalReceiptPath = options.Required("fresh-terminal-receipt"),
        RunId = options.Required("run-id"),
        ExecutorVersion = options.Required("executor-version"),
        SettlementRequestPath = options.Required("settlement-request"),
        SettlementResultPath = options.Required("settlement-result"),
        SettledLedgerPath = options.Required("settled-ledger"),
        SettlementReceiptPath = options.Required("settlement-receipt")
    };

static AcquisitionRoutePortfolioInputs ContinuationRoutePortfolioInputs(
    Arguments options,
    bool requireProposal = false) => new()
    {
        RequirementInventoryPath = options.Required(
            "next-requirement-inventory"),
        AcquisitionLoweringPath = options.Required(
            "next-acquisition-lowering"),
        MasterAnglerWindowsPath = options.Required(
            "next-master-angler-windows"),
        CalendarResolutionPath = options.Required(
            "next-calendar-resolution"),
        TargetDateCalendarPath = options.Required(
            "next-target-date-calendar"),
        TargetDateUnlockPath = options.Required("next-target-date-unlock"),
        TargetDateFestivalPath = options.Required(
            "next-target-date-festival"),
        TargetDateLocationPath = options.Required(
            "next-target-date-location"),
        TargetDateFacilityPath = options.Required(
            "next-target-date-facility"),
        TargetDateResourcePath = options.Required(
            "next-target-date-resource"),
        TargetDateCurrencyPath = options.Required(
            "next-target-date-currency"),
        TargetDateReservationPath = options.Required(
            "next-target-date-reservation"),
        TargetDateProcessingPath = options.Required(
            "next-target-date-processing"),
        TargetDateFishingProbabilityPath = options.Required(
            "next-target-date-fishing-probability"),
        TargetDateStochasticRetryPath = options.Required(
            "next-target-date-stochastic-retry"),
        TargetDateDailyTimeEnergyPath = options.Required(
            "next-target-date-daily-time-energy"),
        TargetDateOpportunityCostPath = options.Required(
            "next-target-date-opportunity-cost"),
        FishingForecastManifestPath = options.Required(
            "next-fishing-forecast-manifest"),
        StrategyLedgerPath = options.Required("next-strategy-ledger"),
        SnapshotPath = options.Required("next-snapshot"),
        RouteTimingCalibrationPath = options.Required(
            "next-route-timing-calibration"),
        ProposalPath = requireProposal
            ? options.Required("next-proposal")
            : options.Optional("next-proposal") ?? string.Empty
    };

static AcquisitionRouteExecutionBindingInputs
    ContinuationRouteExecutionBindingInputs(Arguments options)
{
    var portfolio = ContinuationRoutePortfolioInputs(
        options,
        requireProposal: true);
    return new AcquisitionRouteExecutionBindingInputs
    {
        RequirementInventoryPath = portfolio.RequirementInventoryPath,
        AcquisitionLoweringPath = portfolio.AcquisitionLoweringPath,
        MasterAnglerWindowsPath = portfolio.MasterAnglerWindowsPath,
        CalendarResolutionPath = portfolio.CalendarResolutionPath,
        TargetDateCalendarPath = portfolio.TargetDateCalendarPath,
        TargetDateUnlockPath = portfolio.TargetDateUnlockPath,
        TargetDateFestivalPath = portfolio.TargetDateFestivalPath,
        TargetDateLocationPath = portfolio.TargetDateLocationPath,
        TargetDateFacilityPath = portfolio.TargetDateFacilityPath,
        TargetDateResourcePath = portfolio.TargetDateResourcePath,
        TargetDateCurrencyPath = portfolio.TargetDateCurrencyPath,
        TargetDateReservationPath = portfolio.TargetDateReservationPath,
        TargetDateProcessingPath = portfolio.TargetDateProcessingPath,
        TargetDateFishingProbabilityPath =
            portfolio.TargetDateFishingProbabilityPath,
        TargetDateStochasticRetryPath =
            portfolio.TargetDateStochasticRetryPath,
        TargetDateDailyTimeEnergyPath =
            portfolio.TargetDateDailyTimeEnergyPath,
        TargetDateOpportunityCostPath =
            portfolio.TargetDateOpportunityCostPath,
        FishingForecastManifestPath = portfolio.FishingForecastManifestPath,
        StrategyLedgerPath = portfolio.StrategyLedgerPath,
        BeforeSnapshotPath = portfolio.SnapshotPath,
        RouteTimingCalibrationPath = portfolio.RouteTimingCalibrationPath,
        PortfolioProposalPath = portfolio.ProposalPath,
        PortfolioAdmissionPath = options.Required(
            "next-portfolio-admission"),
        PortfolioPreferenceRequestPath = options.Required(
            "continuation-request"),
        PortfolioTeacherPreferencePath = options.Required(
            "continuation-preference"),
        PortfolioCommitReceiptPath = options.Required(
            "continuation-commit-receipt"),
        CommittedStrategyLedgerPath = options.Required(
            "next-committed-ledger"),
        PortfolioCommitResultPath = options.Required(
            "next-commit-result"),
        ActionQueuePath = options.Required("next-action-queue"),
        RouteOccurrenceId = options.Required("next-route-occurrence-id")
    };
}

static AcquisitionRouteExecutionBindingInputs RouteExecutionBindingInputs(
    Arguments options,
    string? actionQueuePath = null) => new()
    {
        RequirementInventoryPath = options.Required("requirement-inventory"),
        AcquisitionLoweringPath = options.Required("acquisition-lowering"),
        MasterAnglerWindowsPath = options.Required("master-angler-windows"),
        CalendarResolutionPath = options.Required("calendar-resolution"),
        TargetDateCalendarPath = options.Required("target-date-calendar"),
        TargetDateUnlockPath = options.Required("target-date-unlock"),
        TargetDateFestivalPath = options.Required("target-date-festival"),
        TargetDateLocationPath = options.Required("target-date-location"),
        TargetDateFacilityPath = options.Required("target-date-facility"),
        TargetDateResourcePath = options.Required("target-date-resource"),
        TargetDateCurrencyPath = options.Required("target-date-currency"),
        TargetDateReservationPath = options.Required("target-date-reservation"),
        TargetDateProcessingPath = options.Required("target-date-processing"),
        TargetDateFishingProbabilityPath = options.Required(
            "target-date-fishing-probability"),
        TargetDateStochasticRetryPath = options.Required(
            "target-date-stochastic-retry"),
        TargetDateDailyTimeEnergyPath = options.Required(
            "target-date-daily-time-energy"),
        TargetDateOpportunityCostPath = options.Required(
            "target-date-opportunity-cost"),
        FishingForecastManifestPath = options.Required(
            "fishing-forecast-manifest"),
        StrategyLedgerPath = options.Required("strategy-ledger"),
        BeforeSnapshotPath = options.Required("snapshot"),
        RouteTimingCalibrationPath = options.Required(
            "route-timing-calibration"),
        PortfolioProposalPath = options.Required("portfolio-proposal"),
        PortfolioAdmissionPath = options.Required("portfolio-admission"),
        PortfolioPreferenceRequestPath = options.Required(
            "portfolio-preference-request"),
        PortfolioTeacherPreferencePath = options.Required(
            "portfolio-teacher-preference"),
        PortfolioCommitReceiptPath = options.Required(
            "portfolio-commit-receipt"),
        CommittedStrategyLedgerPath = options.Required(
            "committed-strategy-ledger"),
        PortfolioCommitResultPath = options.Optional(
            "portfolio-commit-result") ?? string.Empty,
        ActionQueuePath = actionQueuePath ?? options.Required("action-queue"),
        RouteOccurrenceId = options.Required("route-occurrence-id")
    };

static void BuildAcquisitionRouteTargetDateFishingProbability(
    Arguments options)
{
    var report = AcquisitionRouteTargetDateFishingProbabilityBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("master-angler-windows"),
        options.Required("calendar-resolution"),
        options.Required("target-date-calendar"),
        options.Required("target-date-unlock"),
        options.Required("target-date-festival"),
        options.Required("target-date-location"),
        options.Required("target-date-facility"),
        options.Required("target-date-resource"),
        options.Required("target-date-currency"),
        options.Required("target-date-reservation"),
        options.Required("target-date-processing"),
        options.Required("strategy-ledger"),
        options.Required("snapshot"),
        options.Required("route-timing-calibration"),
        options.Required("fishing-forecast-manifest"));
    Write(options.Required("output"), report);
    if (!report.RouteOccurrenceInventoryComplete)
        Environment.ExitCode = 2;
}

static void BuildCurrentFullShipmentTeacherFrontier(Arguments options)
{
    var report = CurrentFullShipmentTeacherFrontierBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
}

static void BuildCurrentCommunityCenterDenominator(Arguments options)
{
    var report = CurrentCommunityCenterDenominatorBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
}

static void BuildCurrentCollectionTeacherFrontier(Arguments options)
{
    var report = CurrentCollectionTeacherFrontierBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("snapshot"));
    Write(options.Required("output"), report);
}

static void BuildCurrentMasterAnglerTeacherFrontier(Arguments options)
{
    var report = CurrentMasterAnglerTeacherFrontierBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("snapshot"),
        options.Required("target-date-intents"));
    Write(options.Required("output"), report);
}

static void BuildCurrentStageOneCollectionTeacherFrontier(Arguments options)
{
    var report = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("snapshot"),
        options.Required("master-angler-target-date-intents"));
    Write(options.Required("output"), report);
}

static void BuildCurrentStageOneCollectionTeacherPreference(Arguments options)
{
    var report = CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("snapshot"),
        options.Required("master-angler-target-date-intents"));
    Write(options.Required("output"), report);
}

static void BuildCurrentStageOneCollectionTeacherReceipt(Arguments options)
{
    var report = CurrentStageOneCollectionTeacherReceiptBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("ranking"),
        options.Required("before-snapshot"),
        options.Required("master-angler-target-date-intents"),
        options.Required("preference"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("trajectory-id"),
        options.Required("run-id"),
        options.Required("knowledge-dictionary-version"),
        options.Required("executor-version"));
    Write(options.Required("output"), report);
    if (options.Optional("dataset-output") is { } datasetOutput)
    {
        if (!report.TeacherTrainingRowEligible || report.TrainingRow is null)
        {
            throw new InvalidDataException(
                "Teacher receipt is not eligible for dataset output: " +
                string.Join(",", report.BlockingReasons));
        }
        WriteJsonl(datasetOutput, new[] { report.TrainingRow });
    }
}

static void BuildCommunityCenterLifecycleReceipt(Arguments options)
{
    var report = CommunityCenterLifecycleReceiptBuilder.Build(
        options.Required("queue"),
        options.Required("before-snapshot"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("transition-kind"),
        options.Required("run-id"),
        options.Required("executor-version"));
    Write(options.Required("output"), report);
    if (!report.TrainingLabelEligible)
        Environment.ExitCode = 2;
}

static void BuildFullShipmentTerminalSettlementReceipt(Arguments options)
{
    var report = FullShipmentTerminalSettlementReceiptBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("queue"),
        options.Required("before-snapshot"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("run-id"),
        options.Required("executor-version"),
        options.Required("selected-candidate-id"));
    Write(options.Required("output"), report);
    if (!report.TrainingLabelEligible)
        Environment.ExitCode = 2;
}

static void BuildFullShipmentSettlementReceipt(Arguments options)
{
    var report = FullShipmentSettlementReceiptBuilder.Build(
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("queue"),
        options.Required("before-snapshot"),
        options.Required("execution-receipt"),
        options.Required("after-snapshot"),
        options.Required("run-id"),
        options.Required("executor-version"),
        options.Required("selected-candidate-id"),
        options.Required("expected-qualified-item-id"));
    Write(options.Required("output"), report);
    if (!report.RecurrenceEvidenceEligible)
        Environment.ExitCode = 2;
}

static void BuildFullShipmentRecurrenceProofReceipt(Arguments options)
{
    var report = FullShipmentRecurrenceProofBuilder.Build(
        options.Required("manifest"));
    Write(options.Required("output"), report);
    if (!report.RecurrenceProofVerified)
        Environment.ExitCode = 2;
}

static void BuildMasterAnglerOpportunityCatalog(Arguments options)
{
    var report = MasterAnglerOpportunityCatalogBuilder.Build(
        options.Required("requirement-inventory"));
    Write(options.Required("output"), report);
    if (!report.SourceInventoryComplete || !report.StaticCalendarConstraintComplete ||
        !report.LocationRuleSpawnChanceInputInventoryComplete ||
        report.UnresolvedSpeciesIds.Length > 0 || report.UnresolvedCalendarRuleIds.Length > 0 ||
        report.UnresolvedSpawnChanceInputRuleIds.Length > 0)
        Environment.ExitCode = 2;
}

static void BuildMasterAnglerStageOneWindows(Arguments options)
{
    var report = MasterAnglerStageOneWindowIndexBuilder.Build(
        options.Required("catalog"),
        options.Int("deadline-year", 3));
    Write(options.Required("output"), report);
    if (!report.StaticWindowCoverageComplete || report.UnresolvedSpeciesIds.Length > 0)
        Environment.ExitCode = 2;
}

static void BuildMasterAnglerTargetDateIntents(Arguments options)
{
    var report = MasterAnglerTargetDateIntentBuilder.Build(
        options.Required("windows"),
        options.Required("snapshot"),
        options.Required("timing-calibration"));
    Write(options.Required("output"), report);
    if (report.CandidateCount == 0)
        Environment.ExitCode = 2;
}

static void RehashContent(Arguments options)
{
    var report = ContentInventoryVerifier.Verify(
        options.Required("manifest"),
        options.Required("content-root"));
    Write(options.Required("output"), report);
    if (report.Status != "pass")
        Environment.ExitCode = 2;
}

static void BuildTeacherPlan(Arguments options)
{
    var knowledge = KnowledgeIndex.Load(options.Required("knowledge"));
    var rankingPath = Path.GetFullPath(options.Required("ranking"));
    var ranking = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(
        File.ReadAllText(rankingPath), JsonDefaults.Options)
        ?? throw new InvalidDataException("Ranking response is null.");
    var maxItems = options.Int("max-items", 24);
    DemonstrationRetrieval? guidance = null;
    var demonstrationPath = options.Optional("demonstrations");
    if (demonstrationPath is not null)
    {
        var queryPath = options.Required("query");
        using var queryDocument = JsonDocument.Parse(File.ReadAllText(queryPath));
        var queryElement = queryDocument.RootElement.TryGetProperty("state_features", out var nested)
            ? nested
            : queryDocument.RootElement;
        var query = queryElement.Deserialize<FeatureVector>(JsonDefaults.Options)
            ?? throw new InvalidDataException("Teacher-plan query feature vector is null.");
        guidance = DemonstrationLibrary.Load(demonstrationPath, knowledge).Retrieve(
            knowledge.GoalId,
            ranking.Availability.StateHash,
            query,
            limit: 1);
    }
    var plan = new TeacherPlanBuilder(maxItems).Build(ranking, knowledge, guidance);
    Write(options.Required("output"), plan);
}

static void ImportLegacy(Arguments options)
{
    var knowledge = KnowledgeIndex.Load(options.Required("knowledge"));
    var rows = LegacyTrajectoryImporter.Import(options.Required("input"), knowledge);
    WriteJsonl(options.Required("output"), rows);
    Write(options.Required("report"), new
    {
        schema_version = "legacy_import_report.v1",
        status = "pass",
        imported_rows = rows.Length,
        expert_admitted_rows = rows.Count(value => value.Audit.ExpertAdmitted),
        policy = "Legacy AI rollout rows are retained for evaluation and executor evidence only."
    });
}

static void Validate(Arguments options)
{
    var knowledge = KnowledgeIndex.Load(options.Required("knowledge"));
    var input = Path.GetFullPath(options.Required("input"));
    var rows = File.ReadLines(input)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(line => JsonSerializer.Deserialize<GoalConditionedDemonstration>(line, JsonDefaults.Options)
            ?? throw new InvalidDataException("Demonstration row is null."))
        .ToArray();
    var validator = new DemonstrationValidator();
    var results = rows.Select(row => new
    {
        demonstration_id = row.DemonstrationId,
        result = validator.Validate(row, knowledge)
    }).ToArray();
    Write(options.Required("output"), new
    {
        schema_version = "goal_conditioned_demonstration_validation.v1",
        status = results.All(value => value.result.Admitted) ? "pass" : "contains_rejections",
        input_rows = rows.Length,
        admitted_rows = results.Count(value => value.result.Admitted),
        rejected_rows = results.Count(value => !value.result.Admitted),
        rows = results
    });
}

static void SemanticizeRecording(Arguments options)
{
    var knowledge = KnowledgeIndex.Load(options.Required("knowledge"));
    var row = RecordingSemanticizer.Build(
        options.Required("recording"),
        options.Required("annotations"),
        knowledge);
    WriteJsonl(options.Required("output"), new[] { row });
}

static void Retrieve(Arguments options)
{
    var knowledge = KnowledgeIndex.Load(options.Required("knowledge"));
    using var queryDocument = JsonDocument.Parse(File.ReadAllText(options.Required("query")));
    var queryElement = queryDocument.RootElement.TryGetProperty("state_features", out var nested)
        ? nested
        : queryDocument.RootElement;
    var features = queryElement.Deserialize<FeatureVector>(JsonDefaults.Options)
        ?? throw new InvalidDataException("Query feature vector is null.");
    var result = DemonstrationLibrary.Load(options.Required("input"), knowledge).Retrieve(
        knowledge.GoalId,
        options.Required("state-hash"),
        features,
        options.Int("limit", 3));
    Write(options.Required("output"), result);
}

static void SelfTest(Arguments options)
    => BootstrapSelfTest.Run(options);

static void SelfTestCurrentCollection(Arguments options)
    => BootstrapSelfTest.RunCurrentCollection(options.Required("output-root"));

static void SelfTestCurrentCommunityCenterDenominator(Arguments options) =>
    BootstrapSelfTest.RunCurrentCommunityCenterDenominator(
        options.Required("requirement-inventory"),
        options.Required("snapshot"),
        options.Required("output-root"));

static void SelfTestCurrentStageOneCollection(Arguments options)
    => BootstrapSelfTest.RunCurrentStageOneCollection(
        options.Required("output-root"));

static void SelfTestGoalMethodIncomparableLiveShadow(Arguments options)
    => BootstrapSelfTest.RunGoalMethodIncomparableLiveShadow(
        options.Required("checkpoint"),
        options.Required("corpus-manifest"),
        options.Required("rollout-proof-manifest"),
        options.Required("output-root"));

static void SelfTestGoalMethodTeacherCoverage(Arguments options)
    => BootstrapSelfTest.RunGoalMethodTeacherCoverage(
        GoalMethodFrontierInputs(options),
        options.Required("corpus-manifest"),
        options.Required("output-root"));

static void Write(string path, object value)
{
    var fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
    File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options) + Environment.NewLine);
    File.Move(temporary, fullPath, true);
    Console.WriteLine(fullPath);
}

static void WriteJsonl<T>(string path, IEnumerable<T> values)
{
    var fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
    using (var writer = new StreamWriter(temporary))
    {
        foreach (var value in values)
            writer.WriteLine(JsonSerializer.Serialize(value, JsonDefaults.Compact));
    }
    File.Move(temporary, fullPath, true);
    Console.WriteLine(fullPath);
}

internal sealed record CommandDefinition(
    string Name,
    Action<Arguments> Execute);

internal sealed class Arguments
{
    private readonly Dictionary<string, string> values;

    private Arguments(Dictionary<string, string> values) => this.values = values;

    public static Arguments Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Arguments must be --name value pairs.");
            values.Add(args[index][2..], args[index + 1]);
        }
        return new Arguments(values);
    }

    public string Required(string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException("Missing --" + name + ".");

    public int Int(string name, int fallback) =>
        values.TryGetValue(name, out var value)
            ? int.TryParse(value, out var parsed)
                ? parsed
                : throw new ArgumentException("--" + name + " must be an integer.")
            : fallback;

    public double Double(string name, double fallback) =>
        values.TryGetValue(name, out var value)
            ? double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : throw new ArgumentException(
                    "--" + name + " must be a number.")
            : fallback;

    public string? Optional(string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
