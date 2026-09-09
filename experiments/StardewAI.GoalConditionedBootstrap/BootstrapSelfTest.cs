using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void Run(Arguments options)
    {
        var outputRoot = Path.GetFullPath(options.Required("output-root"));
        Directory.CreateDirectory(outputRoot);
        var knowledgePath = options.Required("knowledge");
        var rankingPath = options.Required("ranking");
        var legacyPath = options.Required("legacy");
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        var ranking = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(
            File.ReadAllText(rankingPath), JsonDefaults.Options)
            ?? throw new InvalidDataException("Ranking response is null.");
        var plan = new TeacherPlanBuilder(24).Build(ranking, knowledge);
        var treeItems = plan.Bundles.SelectMany(value => value.Items)
            .Count(value => value.Kind == "harvest_tree_product");
        var mailItems = plan.Bundles.SelectMany(value => value.Items)
            .Count(value => value.Kind == "open_mailbox_letter");
        var optionalRemoteRoutes = plan.Bundles.SelectMany(value => value.Items)
            .Count(value => value.Kind == "route_connector_tile" && value.OptionId.StartsWith("social.", StringComparison.Ordinal));
        Require(treeItems == 6, $"Expected six shared-route tree harvests, observed {treeItems}.");
        Require(mailItems == 1, $"Expected one mailbox obligation, observed {mailItems}.");
        Require(optionalRemoteRoutes == 0, "Optional remote social routes entered the teacher day plan.");
        Require(!plan.Audit.UsesModelScoreAsLabel, "Teacher plan used the existing policy score as a label.");
        var knownGoalMethods = GrandpaDirectionCatalog.Entries
            .Select(value => value.BindingRuleId)
            .ToHashSet(StringComparer.Ordinal);
        var unknownGoalMethods = plan.Bundles
            .SelectMany(value => value.Items)
            .Select(value => value.MethodId)
            .Where(value => !value.StartsWith("method.day.", StringComparison.Ordinal))
            .Where(value => !knownGoalMethods.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Require(unknownGoalMethods.Length == 0,
            "Teacher plan invented goal-method IDs outside GrandpaDirectionCatalog: " +
            string.Join(",", unknownGoalMethods));

        var legacy = LegacyTrajectoryImporter.Import(legacyPath, knowledge);
        var validator = new DemonstrationValidator();
        var legacyAdmitted = legacy.Count(row => validator.Validate(row, knowledge).Admitted);
        Require(legacyAdmitted == 0, "Legacy AI rollouts were admitted as expert demonstrations.");

        var nearHuman = BuildSemanticizedFixture(outputRoot, knowledge);
        var farTeacher = SyntheticDemonstration(knowledge, energy: 10, season: "winter");
        var retrieval = DemonstrationLibrary.FromRows(
                new[] { nearHuman, farTeacher }.Concat(legacy.Take(1)),
                knowledge)
            .Retrieve(knowledge.GoalId, "self-test-query", Features(95, "spring"), 3, 0);
        Require(retrieval.ExpertRows == 2 && retrieval.Matches.Length == 2,
            "Retrieval library did not isolate the two trusted expert rows.");
        Require(retrieval.Matches[0].DemonstrationId == nearHuman.DemonstrationId,
            "Nearest successful human demonstration was not ranked first.");
        Require(!retrieval.Matches.Any(match => match.SourceKind == DemonstrationSourceKinds.LegacyAiRollout),
            "Legacy AI rollout leaked into expert retrieval.");

        VerifyMasterAnglerFullRouteIntent(outputRoot);
        VerifyCurrentFullShipmentTeacherFrontier(outputRoot);

        var guidedPlan = new TeacherPlanBuilder(24).Build(ranking, knowledge, HarvestGuide(knowledge));
        Require(guidedPlan.Audit.UsesExpertDemonstrationGuidance &&
            guidedPlan.Bundles.SelectMany(bundle => bundle.Items).Any(item =>
                item.Reasons.Any(reason => reason.StartsWith("expert_demonstration_guidance=", StringComparison.Ordinal))),
            "Expert demonstration guidance did not reach the teacher plan.");

        WriteJsonl(Path.Combine(outputRoot, "admitted-human-fixture.jsonl"), new[] { nearHuman });
        var planPath = Path.Combine(outputRoot, "teacher-plan-regression.json");
        Write(planPath, plan);
        Write(Path.Combine(outputRoot, "self-test-report.json"), new
        {
            schema_version = "goal_conditioned_bootstrap_self_test.v1",
            status = "pass",
            source_commit = RunProcess("git", "rev-parse HEAD").Trim(),
            knowledge_sha256 = knowledge.Sha256,
            ranking_sha256 = HashFile(rankingPath),
            legacy_sha256 = HashFile(legacyPath),
            tree_harvest_items_planned = treeItems,
            mail_items_planned = mailItems,
            optional_remote_social_routes_planned = optionalRemoteRoutes,
            legacy_rows_checked = legacy.Length,
            legacy_rows_admitted_as_expert = legacyAdmitted,
            expert_retrieval_rows = retrieval.ExpertRows,
            nearest_demonstration_id = retrieval.Matches[0].DemonstrationId,
            legacy_rows_retrieved_as_expert = 0,
            semanticized_human_demonstration_admitted = nearHuman.Audit.ExpertAdmitted,
            tampered_recording_rejected = true,
            expert_guidance_reached_teacher_plan = guidedPlan.Audit.UsesExpertDemonstrationGuidance,
            master_angler_full_route_timing_verified = true,
            current_full_shipment_teacher_frontier_verified = true,
            all_goal_methods_from_unified_catalog = true,
            teacher_plan_path = planPath
        });
    }

    private static GoalConditionedDemonstration BuildSemanticizedFixture(
        string outputRoot,
        GoalKnowledge knowledge)
    {
        var fixtureRoot = Path.Combine(outputRoot, "semanticizer-fixture");
        var snapshotRoot = Path.Combine(fixtureRoot, "snapshots");
        Directory.CreateDirectory(snapshotRoot);
        var startPath = Path.Combine(snapshotRoot, "start.json");
        var endPath = Path.Combine(snapshotRoot, "end.json");
        File.WriteAllText(startPath, JsonSerializer.Serialize(new
        {
            state_hash = "fixture-start-state",
            profile = "full"
        }, JsonDefaults.Options));
        File.WriteAllText(endPath, JsonSerializer.Serialize(new
        {
            state_hash = "fixture-end-state",
            profile = "full"
        }, JsonDefaults.Options));

        var eventPath = Path.Combine(fixtureRoot, "events.jsonl");
        var events = new object[]
        {
            new { event_type = "intent_marked", payload = new { method_id = "method.day.maintain_production" } },
            new { event_type = "semantic_boundary", payload = new { reason = "saving" } },
            new { event_type = "session_completed", payload = new { stop_reason = "self_test" } }
        };
        File.WriteAllLines(eventPath, events.Select(value => JsonSerializer.Serialize(value, JsonDefaults.Compact)));
        var manifestPath = Path.Combine(fixtureRoot, "session-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
        {
            schema_version = "human_demonstration_recording_manifest.v1",
            session_id = "self-test-human-near",
            source_kind = "human_player",
            goal_id = knowledge.GoalId,
            event_log = new { path = "events.jsonl", sha256 = HashFile(eventPath) },
            snapshots = new[]
            {
                new { path = "snapshots/start.json", sha256 = HashFile(startPath) },
                new { path = "snapshots/end.json", sha256 = HashFile(endPath) }
            }
        }, JsonDefaults.Options));

        var annotationPath = Path.Combine(fixtureRoot, "annotations.json");
        File.WriteAllText(annotationPath, JsonSerializer.Serialize(new
        {
            schema_version = "recording_admission_annotations.v1",
            goal_id = knowledge.GoalId,
            target_score = knowledge.TargetScore,
            save_id = "self-test",
            year = 1,
            season = "spring",
            day = 1,
            start_time = 600,
            end_time = 2200,
            active_criterion_ids = Array.Empty<string>(),
            state_features = Features(100, "spring"),
            segments = new[]
            {
                new
                {
                    sequence = 1,
                    bundle_id = "farm-morning",
                    method_id = "method.day.maintain_production",
                    option_id = "farm.maintain_crops",
                    candidate_kind = "water_crop",
                    candidate_id = "self-test-water",
                    location_id = "Farm",
                    start_snapshot = "snapshots/start.json",
                    end_snapshot = "snapshots/end.json",
                    verified = true,
                    semantic_confidence = 1,
                    observed_effects = new { watered = 1 }
                }
            },
            status = "completed",
            day_boundary_verified = true,
            goal_progress_before = 0,
            goal_progress_after = 0,
            terminal_goal_complete = false
        }, JsonDefaults.Options));

        var admitted = RecordingSemanticizer.Build(fixtureRoot, annotationPath, knowledge);
        var original = File.ReadAllText(endPath);
        File.AppendAllText(endPath, " ");
        try
        {
            var rejected = false;
            try
            {
                _ = RecordingSemanticizer.Build(fixtureRoot, annotationPath, knowledge);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Require(rejected, "Tampered recording evidence passed semantic admission.");
        }
        finally
        {
            File.WriteAllText(endPath, original);
        }
        return admitted;
    }

    private static GoalConditionedDemonstration SyntheticDemonstration(
        GoalKnowledge knowledge,
        double energy,
        string season)
    {
        const string id = "teacher:far";
        const string methodId = "method.day.maintain_production";
        return new GoalConditionedDemonstration
        {
            DemonstrationId = id,
            Source = new DemonstrationSource
            {
                Kind = DemonstrationSourceKinds.DeterministicTeacher,
                SessionId = id,
                SourcePaths = new[] { "self-test-fixture" },
                SourceSha256 = new[] { new string('a', 64) }
            },
            Goal = new DemonstrationGoal
            {
                GoalId = knowledge.GoalId,
                TargetScore = knowledge.TargetScore,
                MethodIds = new[] { methodId }
            },
            Context = new DemonstrationContext
            {
                SaveId = "self-test",
                Year = 1,
                Season = season,
                Day = 1,
                StartTime = 600,
                EndTime = 2200,
                StartStateHash = id + ":start",
                EndStateHash = id + ":end"
            },
            StateFeatures = Features(energy, season),
            Segments = new[]
            {
                new DemonstrationSegment
                {
                    Sequence = 1,
                    BundleId = "farm-morning",
                    MethodId = methodId,
                    OptionId = "farm.maintain_crops",
                    CandidateKind = "water_crop",
                    CandidateId = id + ":water",
                    LocationId = "Farm",
                    StartStateHash = id + ":start",
                    EndStateHash = id + ":end",
                    Verified = true,
                    SemanticConfidence = 1,
                    ObservedEffects = JsonSerializer.SerializeToElement(new { watered = 1 })
                }
            },
            Outcome = new DemonstrationOutcome
            {
                Status = "completed",
                DayBoundaryObserved = true,
                AllSegmentsVerified = true
            }
        };
    }

    private static DemonstrationRetrieval HarvestGuide(GoalKnowledge knowledge) => new()
    {
        GoalId = knowledge.GoalId,
        Matches = new[]
        {
            new DemonstrationMatch
            {
                DemonstrationId = "human:harvest-guide",
                SourceKind = DemonstrationSourceKinds.HumanPlayer,
                Similarity = 0.95,
                SharedFeatureCount = 3,
                Segments = new[]
                {
                    new DemonstrationSegment
                    {
                        Sequence = 1,
                        MethodId = "grandpa.direct.raise_skill_levels",
                        OptionId = "foraging.harvest_tree_product"
                    }
                }
            }
        }
    };

    private static FeatureVector Features(double energy, string season) => new()
    {
        Numeric = new[] { new NumericFeature { Name = "energy", Value = energy } },
        Categorical = new[] { new CategoricalFeature { Name = "season", Value = season } },
        Boolean = new[] { new BooleanFeature { Name = "world_ready", Value = true } }
    };

    private static void Write(string path, object value)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options) + Environment.NewLine);
        File.Move(temporary, fullPath, true);
        Console.WriteLine(fullPath);
    }

    private static void WriteJsonl<T>(string path, IEnumerable<T> values)
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

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(path)))).ToLowerInvariant();

    private static string RunProcess(string file, string argument)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = file,
            Arguments = argument,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start " + file + ".");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Require(process.ExitCode == 0, file + " exited with " + process.ExitCode + ".");
        return output;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
