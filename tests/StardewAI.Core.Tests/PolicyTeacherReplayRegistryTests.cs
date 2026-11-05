using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed class PolicyTeacherReplayRegistryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MixedProvidersUseTheOriginalDatasetAndPreserveTheLegacyRow()
    {
        var legacy = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        var original = JsonSerializer.Serialize(legacy, Json);
        Assert.DoesNotContain("skill_native_threshold", original);
        var skills = SkillsRow();
        var verifier = new FrozenVerifier(skills);
        var registry = new PolicyTeacherSupervisionReplayRegistry(PolicySupervisionFixture.Verifier, verifier);
        var admission = new PolicyPairwiseSupervisionAdmission(registry);
        Assert.Null(admission.Validate(legacy, out var oldPairs));
        Assert.Null(admission.Validate(skills, out var newPairs));
        Assert.Single(oldPairs);
        Assert.Single(newPairs);
        Assert.Equal(original, JsonSerializer.Serialize(legacy, Json));
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-TeacherRegistry", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input.jsonl");
        File.WriteAllLines(input, new[] { original, JsonSerializer.Serialize(skills, Json) });
        var dataset = new PolicyTrajectoryDatasetBuilder(registry).Build(input, Path.Combine(root, "dataset"));
        Assert.Equal(2, dataset.Manifest.Counts.AcceptedRows);
        Assert.Equal(0, dataset.Manifest.Counts.RejectedRows);
        var trained = new StructuredPolicyTrainer(new StructuredPolicyCheckpointStore(), registry).Train(
            dataset.ManifestPath, Path.Combine(root, "synthetic-port-test-model.json"),
            new StructuredPolicyHyperparameters { Epochs = 1 });
        Assert.Equal(2, trained.Checkpoint.Training.TrainPairs);
        Assert.Equal(PolicyTrainingObjectives.IndependentTeacherPairwise, trained.Checkpoint.Training.SupervisionObjective);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("version")]
    [InlineData("outcome")]
    [InlineData("source_hash")]
    [InlineData("candidate")]
    [InlineData("label")]
    [InlineData("partition")]
    [InlineData("run")]
    [InlineData("self_consistent_denominator")]
    public void RegistryNeverFallsBackOrTrustsARewrittenClaim(string mutation)
    {
        var row = SkillsRow();
        var registry = new PolicyTeacherSupervisionReplayRegistry(PolicySupervisionFixture.Verifier, new FrozenVerifier(row));
        var teacher = row.Audit.TeacherSupervision!;
        switch (mutation)
        {
            case "identity": teacher.TeacherId = "unknown"; break;
            case "version": teacher.TeacherVersion = "v2"; break;
            case "outcome": row.Outcome.ActualTicks++; break;
            case "source_hash": teacher.SkillNativeThresholdReceiptSha256 = new string('f', 64); break;
            case "candidate": row.Candidates[0].SourceCandidate.EstimatedTicks++; break;
            case "label": teacher.PairwisePreferences[0].PreferredCandidateId = "uncompared"; break;
            case "partition": teacher.DatasetPartition = "test"; break;
            case "run": row.RunId = "other-run"; break;
            case "self_consistent_denominator":
                row.Candidates = row.Candidates.Where(candidate => candidate.CandidateId != "uncompared").ToArray();
                teacher.CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row);
                Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
                break;
        }
        var reason = new PolicyPairwiseSupervisionAdmission(registry).Validate(row, out var pairs);
        Assert.NotNull(reason);
        if (mutation is "identity" or "version") Assert.Equal("teacher_supervision_identity_mismatch", reason);
        Assert.Empty(pairs);
    }

    [Theory]
    [InlineData("native_path")]
    [InlineData("native_hash")]
    [InlineData("before_path")]
    [InlineData("legacy_schema")]
    [InlineData("collection_hash")]
    [InlineData("collection_path")]
    [InlineData("ranking_hash")]
    public void SkillsSourceShapeCannotOmitNativeProofOrBorrowCollectionFields(string mutation)
    {
        var row = SkillsRow();
        Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
        var teacher = row.Audit.TeacherSupervision!;
        switch (mutation)
        {
            case "native_path": teacher.Sources!.SkillNativeThresholdReceiptPath = null; break;
            case "native_hash": teacher.SkillNativeThresholdReceiptSha256 = null; break;
            case "before_path": teacher.Sources!.BeforeSnapshotPath = ""; break;
            case "legacy_schema": teacher.SchemaVersion = "policy_teacher_supervision.v2"; break;
            case "collection_hash": teacher.CommunityCenterDenominatorSha256 = new string('a', 64); break;
            case "collection_path": teacher.Sources!.RequirementInventoryPath = "unrelated.json"; break;
            case "ranking_hash": teacher.SourceRankingSha256 = new string('a', 64); break;
        }
        Assert.Equal("teacher_supervision_invalid", PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
    }

    [Fact]
    public void LegacyCollectionStillRequiresAllOriginalHashes()
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        row.Audit.TeacherSupervision!.CommunityCenterDenominatorSha256 = "";
        Assert.Equal("teacher_supervision_invalid", PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
    }

    [Fact]
    public void RegistryRejectsDuplicatesEmptyProvidersAndNesting()
    {
        var verifier = PolicySupervisionFixture.Verifier;
        Assert.Throws<ArgumentException>(() => new PolicyTeacherSupervisionReplayRegistry());
        Assert.Throws<ArgumentException>(() => new PolicyTeacherSupervisionReplayRegistry(verifier, verifier));
        Assert.Throws<ArgumentException>(() => new PolicyTeacherSupervisionReplayRegistry(null!));
        Assert.Throws<ArgumentException>(() => new PolicyTeacherSupervisionReplayRegistry(new PolicyTeacherSupervisionReplayRegistry(verifier)));
    }

    private static PolicyDecisionTrajectoryEnvelope SkillsRow()
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(2), "gift", "talk");
        var teacher = row.Audit.TeacherSupervision!;
        teacher.SchemaVersion = PolicyTeacherSupervisionSchemas.Skills;
        teacher.TeacherId = "test.skills_replay_port";
        teacher.Sources = new PolicyTeacherEvidenceSources
        {
            SkillNativeThresholdReceiptPath = "fixture-native.json", PreferencePath = "fixture-preference.json",
            BeforeSnapshotPath = "before.json", ExecutionReceiptPath = "queue-receipt.json", AfterSnapshotPath = "after.json"
        };
        teacher.SkillNativeThresholdReceiptSha256 = new string('b', 64);
        teacher.RequirementInventorySha256 = teacher.AcquisitionLoweringSha256 =
            teacher.CommunityCenterDenominatorSha256 = teacher.SourceRankingSha256 = string.Empty;
        return row;
    }

    // A frozen replay-port double, never a claim of native evidence or coverage.
    private sealed class FrozenVerifier : IPolicyTeacherSupervisionReplayVerifier
    {
        private readonly string frozen;
        public string TeacherId { get; }
        public string TeacherVersion { get; }
        public FrozenVerifier(PolicyDecisionTrajectoryEnvelope row)
        {
            frozen = JsonSerializer.Serialize(row, Json);
            TeacherId = row.Audit.TeacherSupervision!.TeacherId;
            TeacherVersion = row.Audit.TeacherSupervision.TeacherVersion;
        }
        public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed) =>
            JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(frozen, Json)!;
    }
}
