using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

var command = args.FirstOrDefault() ?? string.Empty;
var options = Arguments.Parse(args.Skip(1).ToArray());
try
{
    switch (command)
    {
        case "audit-knowledge":
            AuditKnowledge(options);
            break;
        case "audit-evidence":
            AuditEvidence(options);
            break;
        case "audit-claims":
            AuditClaims(options);
            break;
        case "audit-schedules":
            AuditSchedules(options);
            break;
        case "audit-current-schedules":
            AuditCurrentSchedules(options);
            break;
        case "audit-friendship-day-transition":
            AuditFriendshipDayTransition(options);
            break;
        case "validate-route-timing":
            ValidateRouteTiming(options);
            break;
        case "audit-current-social-frontier":
            AuditCurrentSocialFrontier(options);
            break;
        case "plan-current-social-day":
            PlanCurrentSocialDay(options);
            break;
        case "build-current-social-teacher-label":
            BuildCurrentSocialTeacherLabel(options);
            break;
        case "build-goal-method-graph":
            BuildGoalMethodGraph(options);
            break;
        case "build-requirement-inventory":
            BuildRequirementInventory(options);
            break;
        case "build-acquisition-route-lowering":
            BuildAcquisitionRouteLowering(options);
            break;
        case "build-acquisition-route-calendar-resolution":
            BuildAcquisitionRouteCalendarResolution(options);
            break;
        case "build-acquisition-route-target-date-calendar":
            BuildAcquisitionRouteTargetDateCalendar(options);
            break;
        case "build-acquisition-route-target-date-unlock-state":
            BuildAcquisitionRouteTargetDateUnlockState(options);
            break;
        case "build-acquisition-route-target-date-festival-state":
            BuildAcquisitionRouteTargetDateFestivalState(options);
            break;
        case "build-acquisition-route-target-date-location-route":
            BuildAcquisitionRouteTargetDateLocationRoute(options);
            break;
        case "build-acquisition-route-target-date-facility-capacity":
            BuildAcquisitionRouteTargetDateFacilityCapacity(options);
            break;
        case "build-acquisition-route-target-date-resource-inputs":
            BuildAcquisitionRouteTargetDateResourceInputs(options);
            break;
        case "build-acquisition-route-target-date-currency-budget":
            BuildAcquisitionRouteTargetDateCurrencyBudget(options);
            break;
        case "build-acquisition-route-target-date-inventory-reservation":
            BuildAcquisitionRouteTargetDateInventoryReservation(options);
            break;
        case "build-acquisition-route-target-date-processing-lead-time":
            BuildAcquisitionRouteTargetDateProcessingLeadTime(options);
            break;
        case "build-acquisition-route-target-date-fishing-probability":
            BuildAcquisitionRouteTargetDateFishingProbability(options);
            break;
        case "build-acquisition-route-target-date-stochastic-retry-budget":
            BuildAcquisitionRouteTargetDateStochasticRetryBudget(options);
            break;
        case "build-current-full-shipment-teacher-frontier":
            BuildCurrentFullShipmentTeacherFrontier(options);
            break;
        case "build-current-collection-teacher-frontier":
            BuildCurrentCollectionTeacherFrontier(options);
            break;
        case "build-current-master-angler-teacher-frontier":
            BuildCurrentMasterAnglerTeacherFrontier(options);
            break;
        case "build-current-stage-one-collection-teacher-frontier":
            BuildCurrentStageOneCollectionTeacherFrontier(options);
            break;
        case "build-current-stage-one-collection-teacher-preference":
            BuildCurrentStageOneCollectionTeacherPreference(options);
            break;
        case "build-current-stage-one-collection-teacher-receipt":
            BuildCurrentStageOneCollectionTeacherReceipt(options);
            break;
        case "build-master-angler-opportunity-catalog":
            BuildMasterAnglerOpportunityCatalog(options);
            break;
        case "build-master-angler-stage-one-windows":
            BuildMasterAnglerStageOneWindows(options);
            break;
        case "build-master-angler-target-date-intents":
            BuildMasterAnglerTargetDateIntents(options);
            break;
        case "rehash-content":
            RehashContent(options);
            break;
        case "teacher-plan":
            BuildTeacherPlan(options);
            break;
        case "import-legacy":
            ImportLegacy(options);
            break;
        case "validate":
            Validate(options);
            break;
        case "semanticize-recording":
            SemanticizeRecording(options);
            break;
        case "retrieve":
            Retrieve(options);
            break;
        case "self-test":
            SelfTest(options);
            break;
        case "self-test-current-collection":
            SelfTestCurrentCollection(options);
            break;
        case "self-test-current-stage-one-collection":
            SelfTestCurrentStageOneCollection(options);
            break;
        default:
            throw new ArgumentException(
                "Command must be audit-knowledge, audit-evidence, audit-claims, audit-schedules, audit-current-schedules, audit-friendship-day-transition, validate-route-timing, audit-current-social-frontier, plan-current-social-day, build-current-social-teacher-label, build-goal-method-graph, build-requirement-inventory, build-acquisition-route-lowering, build-acquisition-route-calendar-resolution, build-acquisition-route-target-date-calendar, build-acquisition-route-target-date-unlock-state, build-acquisition-route-target-date-festival-state, build-acquisition-route-target-date-location-route, build-acquisition-route-target-date-facility-capacity, build-acquisition-route-target-date-resource-inputs, build-acquisition-route-target-date-currency-budget, build-acquisition-route-target-date-inventory-reservation, build-acquisition-route-target-date-processing-lead-time, build-acquisition-route-target-date-fishing-probability, build-acquisition-route-target-date-stochastic-retry-budget, build-current-full-shipment-teacher-frontier, build-current-collection-teacher-frontier, build-current-master-angler-teacher-frontier, build-current-stage-one-collection-teacher-frontier, build-current-stage-one-collection-teacher-preference, build-current-stage-one-collection-teacher-receipt, build-master-angler-opportunity-catalog, build-master-angler-stage-one-windows, build-master-angler-target-date-intents, rehash-content, teacher-plan, import-legacy, validate, semanticize-recording, retrieve, self-test, self-test-current-collection, or self-test-current-stage-one-collection.");
    }
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
    var report = GoalMethodFrontierBuilder.Build(
        options.Required("expansion"),
        options.Required("dependencies"),
        options.Required("isolated-training-authorization"),
        options.Required("requirement-inventory"),
        options.Required("acquisition-lowering"),
        options.Required("acquisition-lowering-catalog"),
        options.Required("knowledge"),
        options.Required("option-matrix"),
        options.Required("claim-ledger"),
        options.Required("direction-catalog-source"));
    Write(options.Required("output"), report);
}

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

static void SelfTestCurrentStageOneCollection(Arguments options)
    => BootstrapSelfTest.RunCurrentStageOneCollection(
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
