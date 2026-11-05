using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed class PolicyIndependentDecisionDatasetTests
{
    [Fact]
    public void ExistingFourParameterBinaryEntryPointIsPreserved()
    {
        Assert.NotNull(typeof(PolicyTrajectoryDatasetBuilder).GetMethod("Build",
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) }));
        var root = Root();
        var result = PolicySupervisionFixture.DatasetBuilder().Build(Write(root, new[] { Row(1), Row(2) }),
            Path.Combine(root, "dataset"), default, default);
        Assert.Equal(1, result.Manifest.Returns.DayComplete);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void ExplicitIndependentModeDoesNotChangeLegacySaveContinuity(bool independent, int daysClosed)
    {
        var root = Root();
        var rows = new[] { Row(1), Row(2) };
        var input = Write(root, rows);
        var result = PolicySupervisionFixture.DatasetBuilder().Build(input, Path.Combine(root, "dataset"),
            horizonObservationPath: null, expectedKnowledgeDictionary: null, independentDecisions: independent);
        Assert.Equal(2, result.Manifest.Counts.AcceptedRows);
        Assert.Equal(0, result.Manifest.Counts.RejectedRows);
        Assert.Equal(daysClosed, result.Manifest.Returns.DayComplete);
        Assert.Equal(independent, result.Manifest.IndependentDecisions);
        using var manifest = JsonDocument.Parse(File.ReadAllText(result.ManifestPath));
        Assert.Equal(independent, manifest.RootElement.TryGetProperty("independent_decisions", out _));
        var cleaned = Read(result.Manifest.Cleaned.Path);
        foreach (var row in cleaned)
        {
            var source = rows.Single(original => original.TrajectoryId == row.TrajectoryId);
            Assert.Equal(source.RunId, row.RunId);
            Assert.Equal(source.Context.SaveId, row.Context.SaveId);
            Assert.Equal(source.Context.SplitKey, row.Context.SplitKey);
            Assert.Equal(PolicyTrajectoryDatasetBuilder.PartitionFor(source.Context.SplitKey), row.Context.DatasetPartition);
            Assert.Equal(source.Returns.Immediate, row.Returns.Immediate);
        }
    }

    [Fact]
    public void IndependentModeRejectsAnyContinuityObservationInputBeforeReadingIt()
    {
        var root = Root();
        Assert.Throws<ArgumentException>(() => PolicySupervisionFixture.DatasetBuilder().Build(
            Path.Combine(root, "missing-input"), Path.Combine(root, "dataset"),
            horizonObservationPath: Path.Combine(root, "missing-observations"), expectedKnowledgeDictionary: null,
            independentDecisions: true));
        Assert.False(Directory.Exists(Path.Combine(root, "dataset")));
    }

    [Fact]
    public void IndependentModeClearsDerivedHorizonClaimsInsteadOfTrustingInput()
    {
        var root = Root();
        var row = Row(1);
        row.Returns.Day = row.Returns.Season = row.Returns.Year = 99;
        row.Returns.Grandpa21 = 1;
        row.Returns.DayStatus = row.Returns.SeasonStatus = row.Returns.YearStatus = "observed_closed";
        row.Returns.Grandpa21Status = "terminal_evaluation_observed";
        row.Returns.LongHorizonStatus = "complete";
        var input = Write(root, new[] { row });
        var bytes = File.ReadAllBytes(input);
        var result = PolicySupervisionFixture.DatasetBuilder().Build(input, Path.Combine(root, "dataset"),
            horizonObservationPath: null, expectedKnowledgeDictionary: null, independentDecisions: true);
        var actual = Assert.Single(Read(result.Manifest.Cleaned.Path));
        Assert.Null(actual.Returns.Day);
        Assert.Null(actual.Returns.Season);
        Assert.Null(actual.Returns.Year);
        Assert.Null(actual.Returns.Grandpa21);
        Assert.Equal("pending", actual.Returns.LongHorizonStatus);
        Assert.Equal(0, result.Manifest.Returns.DayComplete);
        Assert.Equal(0, result.Manifest.Returns.Grandpa21Complete);
        Assert.Equal(bytes, File.ReadAllBytes(input));
    }

    [Fact]
    public void IndependentModeDoesNotBypassOriginalTeacherAdmission()
    {
        var root = Root();
        var row = Row(1);
        row.Audit.TeacherSupervision = null;
        var error = Assert.Throws<InvalidOperationException>(() => PolicySupervisionFixture.DatasetBuilder().Build(
            Write(root, new[] { row }), Path.Combine(root, "dataset"),
            horizonObservationPath: null, expectedKnowledgeDictionary: null, independentDecisions: true));
        Assert.Contains("no valid", error.Message);
    }

    private static PolicyDecisionTrajectoryEnvelope Row(int day)
    {
        var row = PolicySupervisionFixture.Observation(day);
        row.RunId = "independent-run-" + day;
        row.Context.SaveId = "same-base-save";
        row.Context.SplitKey = row.Context.SaveId + ":1:spring:" + day;
        return PolicySupervisionFixture.Attach(row, "talk", "gift");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string Write(string root, IEnumerable<PolicyDecisionTrajectoryEnvelope> rows)
    {
        var path = Path.Combine(root, "input.jsonl");
        File.WriteAllLines(path, rows.Select(row => JsonSerializer.Serialize(row, JsonOptions)));
        return path;
    }

    private static PolicyDecisionTrajectoryEnvelope[] Read(string path) => File.ReadAllLines(path)
        .Select(line => JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(line, JsonOptions)!).ToArray();

    private static string Root()
    {
        var path = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
