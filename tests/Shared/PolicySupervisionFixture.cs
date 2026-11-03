using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Testing;

// A frozen test double for the replay port. Production always replays real source artifacts.
internal sealed class PolicySupervisionFixture : IPolicyTeacherSupervisionReplayVerifier
{
    public static readonly PolicySupervisionFixture Verifier = new();
    private static readonly ConcurrentDictionary<string, string> Frozen = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string TeacherId => "test.explicit_independent_teacher";
    public string TeacherVersion => "v1";

    public static PolicyTrajectoryDatasetBuilder DatasetBuilder() => new(Verifier);
    public static StructuredPolicyTrainer Trainer() => new(new StructuredPolicyCheckpointStore(), Verifier);

    public static PolicyDecisionTrajectoryEnvelope Observation(int day = 1, string selected = "talk")
    {
        var save = "fixture.supervision";
        while (PolicyTrajectoryDatasetBuilder.PartitionFor(save + ":1:spring:" + day) != PolicyDatasetPartitions.Train)
            save += "x";
        var stateHash = "fixture-state-" + day;
        return new PolicyDecisionTrajectoryBuilder().Build(
            "fixture-row-" + day, "fixture-run", new PolicyTrajectoryContext
            {
                SaveId = save, Year = 1, Season = "spring", Day = day, Time = 900
            },
            new FeatureVector { Numeric = new[] { new NumericFeature { Name = "player.money", Value = 1000 } } },
            new PolicyTrajectoryVersions
            {
                FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                CandidateVocabulary = "capability_registry.v3", CapabilityRegistry = "capability_registry.v3",
                KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                Compiler = PolicyTrajectoryVersionPins.Compiler, Executor = PolicyTrajectoryVersionPins.ProductExecutor
            }, stateHash,
            new AvailabilityAwarePolicyPredictionEnvelope
            {
                RankedEventCandidates = new[] { Candidate("gift", "social.gift_npc", 1),
                    Candidate("talk", "social.talk_npc", 2), Candidate("uncompared", "social.talk_npc", 3) }
            }, selected, new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = "fixture-native-" + day, QueueId = "fixture-queue-" + day,
                OptionId = "executor.social_interact", SourceStateHash = stateHash,
                Status = "applied", Success = true, AfterSnapshotFresh = true,
                BeforeGameTick = 1, AfterGameTick = 2, Reward = 1000,
                ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone()
            });
    }

    private static PolicyEventCandidatePrediction Candidate(string id, string option, int rank) => new()
    {
        CandidateId = id, OptionId = option, Kind = "fixture", Rank = rank, Available = true,
        EstimatedTicks = 10, Parameters = new[] { new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" } }
    };

    public static PolicyDatasetBuildResult Dataset(string root, PolicyDecisionTrajectoryEnvelope row)
    {
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "raw.jsonl");
        File.WriteAllText(input, JsonSerializer.Serialize(row, JsonOptions) + "\n");
        return DatasetBuilder().Build(input, Path.Combine(root, "dataset"));
    }

    public static void StripTeacherFromTrainingPartition(PolicyDatasetBuildResult dataset)
    {
        var partition = dataset.Manifest.Partitions.Single(value => value.Partition == PolicyDatasetPartitions.Train);
        var rows = File.ReadAllLines(partition.Path).Select(line =>
            JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(line, JsonOptions)!).ToArray();
        foreach (var row in rows) row.Audit.TeacherSupervision = null;
        File.WriteAllText(partition.Path, string.Join("\n", rows.Select(row => JsonSerializer.Serialize(row, JsonOptions))) + "\n");
        partition.Bytes = new FileInfo(partition.Path).Length;
        partition.Sha256 = StructuredPolicyCheckpointStore.HashFile(partition.Path);
        File.WriteAllText(dataset.ManifestPath, JsonSerializer.Serialize(dataset.Manifest, JsonOptions));
    }

    public static PolicyDecisionTrajectoryEnvelope Attach(
        PolicyDecisionTrajectoryEnvelope row, string preferred, params string[] alternatives)
    {
        var hash = StructuredPolicyCheckpointStore.HashText(Guid.NewGuid().ToString("N"));
        row.Context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey);
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            SchemaVersion = "policy_teacher_supervision.v2",
            TeacherId = Verifier.TeacherId,
            TeacherVersion = Verifier.TeacherVersion,
            SourceStateHash = row.SourceStateHash,
            CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row),
            DatasetPartition = row.Context.DatasetPartition,
            Sources = new PolicyTeacherEvidenceSources(),
            ProvenanceClass = "independent_deterministic_teacher_preference",
            SelectionPolicyId = "test.explicit_comparison.v1",
            PreferenceArtifactSha256 = hash,
            RequirementInventorySha256 = hash,
            AcquisitionLoweringSha256 = hash,
            CommunityCenterDenominatorSha256 = hash,
            SourceRankingSha256 = hash,
            BeforeSnapshotSha256 = hash,
            ExecutionReceiptSha256 = hash,
            AfterSnapshotSha256 = hash,
            SelectedCandidateId = row.Selection.CandidateId,
            SelectedQueueItemId = "fixture-native-item",
            RequirementTransitions = new[] { new PolicyTeacherRequirementTransition
            {
                RequirementSetId = "fixture", RequirementId = "fixture.required", AlternativeIndex = 0,
                BindingKind = "fixture_native_outcome", TransitionKind = "observed",
                BeforeValue = "false", AfterValue = "true", Verified = true
            } },
            PairwisePreferences = alternatives.Select(alternative => new PolicyTeacherPreferencePair
            {
                PreferredCandidateId = preferred, AlternativeCandidateId = alternative,
                FirstDifferingAuthoritativeCriterion = "test_independent_rule",
                LabelSemantics = "preferred_over_current_eligible_counterfactual"
            }).ToArray()
        };
        Frozen[hash] = JsonSerializer.Serialize(row, JsonOptions);
        return row;
    }

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var hash = claimed.Audit.TeacherSupervision?.PreferenceArtifactSha256 ?? string.Empty;
        if (!Frozen.TryGetValue(hash, out var json))
            throw new InvalidDataException("Independent test evidence was not frozen.");
        var expected = JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(json, JsonOptions)!;
        expected.TrajectoryId = claimed.TrajectoryId;
        return expected;
    }
}
