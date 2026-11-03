using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed class PolicyPairwiseSupervisionAdmissionTests
{
    [Fact]
    public void SelectedSuccessfulObservationCannotProduceDatasetOrOverwriteManifest()
    {
        var root = Root();
        var row = PolicySupervisionFixture.Observation();
        var input = Path.Combine(root, "observation.jsonl");
        File.WriteAllText(input, JsonSerializer.Serialize(row));
        var output = Path.Combine(root, "dataset");
        Directory.CreateDirectory(output);
        var manifest = Path.Combine(output, "policy-dataset-manifest.json");
        File.WriteAllText(manifest, "unchanged-manifest");
        var error = Assert.Throws<InvalidOperationException>(() => PolicySupervisionFixture.DatasetBuilder().Build(input, output));
        Assert.Contains("independent_teacher_preference_required", error.Message);
        Assert.Equal("unchanged-manifest", File.ReadAllText(manifest));
        Assert.Single(Directory.GetFiles(output));
        Assert.Null(row.Audit.TeacherSupervision);
        Assert.True(row.Outcome.Success);
    }

    [Fact]
    public void RehashedSelectedOnlyPartitionCannotOverwriteCheckpoint()
    {
        var root = Root();
        var dataset = PolicySupervisionFixture.Dataset(root,
            PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk"));
        PolicySupervisionFixture.StripTeacherFromTrainingPartition(dataset);
        var manifestBefore = File.ReadAllBytes(dataset.ManifestPath);
        var checkpoint = Path.Combine(root, "checkpoint.json");
        File.WriteAllText(checkpoint, "unchanged-checkpoint");
        var error = Assert.Throws<InvalidOperationException>(() => PolicySupervisionFixture.Trainer().Train(dataset.ManifestPath, checkpoint));
        Assert.Contains("independent_teacher_preference_required", error.Message);
        Assert.Equal("unchanged-checkpoint", File.ReadAllText(checkpoint));
        Assert.Equal(manifestBefore, File.ReadAllBytes(dataset.ManifestPath));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("missing_identity")]
    [InlineData("version")]
    [InlineData("missing_version")]
    [InlineData("state")]
    [InlineData("denominator")]
    [InlineData("partition")]
    [InlineData("missing_partition")]
    [InlineData("receipt")]
    [InlineData("label")]
    [InlineData("sources")]
    [InlineData("legacy")]
    [InlineData("candidate")]
    public void MissingOrTamperedIndependentBindingFailsClosed(string field)
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        var teacher = row.Audit.TeacherSupervision!;
        switch (field)
        {
            case "identity": teacher.TeacherId = "student"; break;
            case "missing_identity": teacher.TeacherId = string.Empty; break;
            case "version": teacher.TeacherVersion = "unknown"; break;
            case "missing_version": teacher.TeacherVersion = string.Empty; break;
            case "state": teacher.SourceStateHash = "stale"; break;
            case "denominator": teacher.CandidateDenominatorSha256 = new string('f', 64); break;
            case "partition": teacher.DatasetPartition = "validation"; break;
            case "missing_partition": teacher.DatasetPartition = string.Empty; break;
            case "receipt": teacher.ExecutionReceiptSha256 = new string('f', 64); break;
            case "label": teacher.PairwisePreferences[0].PreferredCandidateId = "uncompared"; break;
            case "sources": teacher.Sources = null; break;
            case "legacy": teacher.SchemaVersion = "policy_teacher_supervision.v1"; break;
            case "candidate": row.Candidates[0].SourceCandidate.EstimatedTicks++; break;
        }
        var reason = new PolicyPairwiseSupervisionAdmission(PolicySupervisionFixture.Verifier).Validate(row, out var pairs);
        Assert.NotNull(reason);
        Assert.Empty(pairs);
    }

    [Fact]
    public void ProvenanceStringsCannotReplaceAnIndependentReplayVerifier()
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        Assert.Equal("teacher_replay_verifier_unavailable",
            new PolicyPairwiseSupervisionAdmission().Validate(row, out var pairs));
        Assert.Empty(pairs);
    }

    [Fact]
    public void OnlyExplicitComparisonTrainsEvenWhenTheStudentSelectedItsAlternative()
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(selected: "talk"), "gift", "talk");
        var admission = new PolicyPairwiseSupervisionAdmission(PolicySupervisionFixture.Verifier);
        Assert.Null(admission.Validate(row, out var pairs));
        Assert.Single(pairs);
        Assert.DoesNotContain(pairs, pair => pair.AlternativeCandidateId == "uncompared");
        var root = Root();
        var dataset = PolicySupervisionFixture.Dataset(root, row);
        var result = PolicySupervisionFixture.Trainer().Train(dataset.ManifestPath, Path.Combine(root, "model.json"));
        Assert.Equal(1, result.Checkpoint.Training.TrainPairs);
        Assert.Equal(PolicyTrainingObjectives.IndependentTeacherPairwise, result.Checkpoint.Training.SupervisionObjective);
        var ranked = new StructuredPolicyRanker().Rank(result.Checkpoint, row.StateFeatures,
            row.Candidates.Select(candidate => candidate.SourceCandidate).ToArray());
        Assert.True(ranked.Single(candidate => candidate.CandidateId == "gift").ModelScore >
                    ranked.Single(candidate => candidate.CandidateId == "talk").ModelScore);
    }

    [Fact]
    public void TieOrIncomparableContextDoesNotManufactureComparisons()
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift");
        Assert.Equal("teacher_strict_comparison_missing",
            new PolicyPairwiseSupervisionAdmission(PolicySupervisionFixture.Verifier).Validate(row, out var pairs));
        Assert.Empty(pairs);
    }

    [Fact]
    public void LegacyCheckpointCannotBeSilentlyReusedAsSupervisedInitialization()
    {
        var root = Root();
        var dataset = PolicySupervisionFixture.Dataset(root,
            PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk"));
        var trainer = PolicySupervisionFixture.Trainer();
        var trained = trainer.Train(dataset.ManifestPath, Path.Combine(root, "initial.json"));
        trained.Checkpoint.Training.SupervisionObjective = string.Empty;
        var legacy = Path.Combine(root, "legacy.json");
        new StructuredPolicyCheckpointStore().Save(legacy, trained.Checkpoint);
        var output = Path.Combine(root, "must-not-exist.json");
        Assert.Throws<InvalidOperationException>(() => trainer.Train(
            dataset.ManifestPath, output, initializationCheckpointPath: legacy));
        Assert.False(File.Exists(output));
    }

    private static string Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOrInaccessibleSourceEvidenceRejectsWithoutAdmittingPairs(bool invalidData)
    {
        var row = PolicySupervisionFixture.Attach(PolicySupervisionFixture.Observation(), "gift", "talk");
        Assert.Equal("teacher_supervision_replay_failed",
            new PolicyPairwiseSupervisionAdmission(new InaccessibleEvidenceVerifier(invalidData)).Validate(row, out var pairs));
        Assert.Empty(pairs);
    }

    private sealed class InaccessibleEvidenceVerifier : IPolicyTeacherSupervisionReplayVerifier
    {
        private readonly bool invalidData;
        public InaccessibleEvidenceVerifier(bool invalidData) => this.invalidData = invalidData;
        public string TeacherId => PolicySupervisionFixture.Verifier.TeacherId;
        public string TeacherVersion => PolicySupervisionFixture.Verifier.TeacherVersion;
        public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
        {
            if (invalidData) throw new InvalidDataException("Fixture source is invalid.");
            throw new UnauthorizedAccessException("Fixture source cannot be read.");
        }
    }
}
