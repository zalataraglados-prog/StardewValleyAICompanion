using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class FormalTrainingNativeAdmissionTests
{
    private static readonly string[] NativeBlockers =
    {
        "golden_walnut_master_angler_native_admission_not_bound",
        "gourmand_orders_2_3_native_admission_not_bound"
    };

    [Fact]
    public void CompleteExistingNineteenCriterionFixtureStillRequiresBothNativeGates()
    {
        var report = VerifyPolicyFixture(0);
        Assert.Equal(19, report.CoverageReadyCriterionCount);
        Assert.False(report.ReadyForSeparatePromotionReview);
        Assert.False(report.FormalProductTrainingAuthorized);
        var originalGates = report.Gates.Where(value => !NativeBlockers.Contains(value.GateId)).ToArray();
        Assert.Equal(9, originalGates.Length);
        Assert.All(originalGates, gate =>
        {
            Assert.True(gate.RequiredForPromotionReview);
            Assert.True(gate.Satisfied);
        });
        foreach (var blocker in NativeBlockers)
        {
            var gate = Assert.Single(report.Gates.Where(value => value.GateId == blocker));
            Assert.True(gate.RequiredForPromotionReview);
            Assert.False(gate.Satisfied);
            Assert.Contains(blocker, report.DownstreamPromotionBlockingReasons);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)]
    public void EveryExistingCriterionRemainsMandatory(int incompleteCriterionIndex)
    {
        // The existing self-test independently verifies Teacher coverage fails
        // when this criterion is removed, even though native locks also remain.
        Assert.False(VerifyPolicyFixture(incompleteCriterionIndex).FormalProductTrainingAuthorized);
    }

    private static GoalMethodFormalTrainingAdmissionReport VerifyPolicyFixture(int index)
    {
        var selfTest = typeof(GoalMethodFormalTrainingAdmissionBuilder).Assembly
            .GetType("StardewAI.GoalConditionedBootstrap.BootstrapSelfTest")!;
        return Assert.IsType<GoalMethodFormalTrainingAdmissionReport>(selfTest.GetMethod(
            "VerifyFormalTrainingAdmissionPolicy", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { index }));
    }

    [Theory]
    [InlineData(false, false, "diagnostic")]
    [InlineData(true, false, "diagnostic")]
    [InlineData(false, true, "diagnostic")]
    [InlineData(true, true, "diagnostic")]
    [InlineData(true, true, "native_control_settle_selection.v1")]
    [InlineData(true, true, "plan_execution_episode.v1")]
    public void FormalBoundaryCannotUseEitherSideOrSelfAttestedArtifacts(bool schedule, bool gourmand, string artifactKind)
    {
        using var fixture = new Fixture();
        var node = JsonSerializer.SerializeToNode(fixture.Manifest)!.AsObject();
        node["golden_walnut_master_angler_verified"] = schedule;
        node["gourmand_orders_2_3_verified"] = gourmand;
        node["native_evidence"] = new JsonObject
        {
            ["schema_version"] = artifactKind, ["formal_product_training"] = false,
            ["success"] = false, ["after_snapshot_fresh"] = true
        };
        var reasons = ValidateBoundary(node.Deserialize<TrainingRunManifest>()!);
        Assert.Equal(NativeBlockers, reasons);
    }

    [Fact]
    public void NativeLocksDoNotReplaceExistingFormalBoundaryChecks()
    {
        using var fixture = new Fixture();
        fixture.Manifest.WindowStyle = "normal";
        fixture.Manifest.NativeExecutorUrl = "https://example.invalid";
        var reasons = ValidateBoundary(fixture.Manifest);
        Assert.Contains("formal_training_window_style_must_be_hidden", reasons);
        Assert.Contains("formal_native_executor_url_must_be_loopback", reasons);
        Assert.All(NativeBlockers, blocker => Assert.Contains(blocker, reasons));
    }

    private static string[] ValidateBoundary(TrainingRunManifest manifest)
    {
        var reasons = new List<string>();
        typeof(StardewTrainingSessionLauncher).GetMethod("ValidateFormalBoundary",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { manifest, reasons });
        return reasons.ToArray();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "formal-native-gates-" + Guid.NewGuid().ToString("N"));
        public TrainingRunManifest Manifest { get; }

        public Fixture()
        {
            Directory.CreateDirectory(root);
            string PathFor(string name) => Path.Combine(root, name);
            var dataset = PathFor("dataset.json");
            File.WriteAllText(dataset, "{}");
            var digest = StructuredPolicyCheckpointStore.HashFile(dataset);
            var checkpoint = new StructuredPolicyCheckpointEnvelope
            {
                CheckpointId = "hermetic-not-native",
                Dataset = new StructuredPolicyDatasetBinding
                {
                    ManifestPath = dataset, ManifestSha256 = digest, CleanedSha256 = digest,
                    TrainSha256 = digest, ValidationSha256 = digest, TestSha256 = digest
                },
                Versions = new PolicyDatasetVersionSet
                {
                    FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                    CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                    CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                    KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    Compiler = PolicyTrajectoryVersionPins.Compiler,
                    Executor = PolicyTrajectoryVersionPins.ProductExecutor
                },
                Model = new StructuredPolicyLinearModel
                {
                    FeatureNames = new[] { "fixture" }, FeatureMeans = new[] { 0d },
                    FeatureScales = new[] { 1d }, Weights = new[] { 0d }
                },
                Training = new StructuredPolicyTrainingSummary { TrainRows = 1, TrainPairs = 1, FeatureCount = 1 }
            };
            var checkpointPath = PathFor("checkpoint.json");
            new StructuredPolicyCheckpointStore().Save(checkpointPath, checkpoint);
            Manifest = new TrainingRunManifest
            {
                RootPath = root, WindowStyle = "hidden",
                ProductExecutorUrl = "http://127.0.0.1:8768", NativeExecutorUrl = "http://127.0.0.1:8767",
                ProductExecutorExecutablePath = dataset, LiveTrainingLoopExecutablePath = dataset,
                DatasetPath = dataset, ReportPath = PathFor("report.json"), CheckpointPath = checkpointPath,
                PolicyTrajectoryPath = dataset, PolicyDatasetManifestPath = dataset,
                ProductReceiptRoot = PathFor("receipts"), ManifestPath = PathFor("manifest.json"),
                PolicyDatasetManifestSha256 = digest, CheckpointSha256 = StructuredPolicyCheckpointStore.HashFile(checkpointPath)
            };
        }

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
