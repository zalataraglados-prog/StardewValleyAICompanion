using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class SkillTerminalSaveBoundaryTests
{
    [Theory]
    [InlineData(15, 5, 3295)]
    [InlineData(25, 9, 14995)]
    public void OnlyFreshCloneDeclaresTheIncompleteFiveXpBoundary(int targetLevel, int fishingLevel, int fishingXp)
    {
        var f = Fixture(targetLevel);
        var hash = Hash(f.Request.SourceSaveFile);
        var report = JsonSerializer.SerializeToElement(SkillTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.OutputPath));
        Assert.Equal(hash, Hash(f.Request.SourceSaveFile));
        Assert.NotEqual(hash, Hash(f.Request.IsolatedSaveFile));
        var xml = XDocument.Load(f.Request.IsolatedSaveFile);
        Assert.Equal(fishingLevel.ToString(), xml.Descendants("fishingLevel").Single().Value);
        Assert.Equal(fishingXp.ToString(), xml.Descendants("experiencePoints").Single().Elements().ElementAt(1).Value);
        Assert.Equal("17", xml.Descendants("money").Single().Value);
        Assert.Empty(xml.Descendants("achievements").Single().Elements());
        Assert.False(report.GetProperty("prior_skill_acquisition_verified").GetBoolean());
        Assert.False(report.GetProperty("native_criterion_outcome_verified").GetBoolean());
        Assert.False(report.GetProperty("formal_product_training_authorized").GetBoolean());
        Assert.Equal(5, report.GetProperty("expected_native_gain").GetInt32());
        Assert.Throws<InvalidDataException>(() => SkillTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.OutputPath));
    }

    [Theory]
    [InlineData("ack")]
    [InlineData("formal")]
    [InlineData("threshold")]
    [InlineData("day")]
    [InlineData("version")]
    [InlineData("luck")]
    [InlineData("shape")]
    [InlineData("nonidentical")]
    public void InvalidBoundaryNeverWritesEitherFile(string mutation)
    {
        var f = Fixture(15);
        var request = mutation switch
        {
            "ack" => f.Request with { DeclaredLocalPrerequisiteOnly = false },
            "formal" => f.Request with { FormalProductTrainingAuthorized = true },
            "threshold" => f.Request with { TargetScoringLevel = 16 },
            "day" => f.Request with { DecisionSpringDayYearTwo = 29 },
            _ => f.Request
        };
        if (mutation is "version" or "luck" or "shape")
        {
            var xml = XDocument.Load(request.SourceSaveFile);
            if (mutation == "version") xml.Root!.Element("gameVersion")!.Value = "1.6.14";
            if (mutation == "luck") xml.Descendants("luckLevel").Single().Value = "1";
            if (mutation == "shape") xml.Descendants("experiencePoints").Single().Elements().Last().Remove();
            xml.Save(request.SourceSaveFile);
            File.Copy(request.SourceSaveFile, request.IsolatedSaveFile, overwrite: true);
        }
        if (mutation == "nonidentical") File.AppendAllText(request.IsolatedSaveFile, " ");
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request));
        var source = Hash(request.SourceSaveFile);
        var target = Hash(request.IsolatedSaveFile);
        Assert.Throws<InvalidDataException>(() => SkillTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.OutputPath));
        Assert.Equal(source, Hash(request.SourceSaveFile));
        Assert.Equal(target, Hash(request.IsolatedSaveFile));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReportCannotOverwriteSourceOrExistingRequest(bool source)
    {
        var f = Fixture(15);
        var hash = Hash(f.Request.SourceSaveFile);
        Assert.Throws<InvalidDataException>(() => SkillTerminalSaveBoundaryBuilder.Prepare(f.RequestPath,
            source ? f.Request.SourceSaveFile : f.RequestPath));
        Assert.Equal(hash, Hash(f.Request.SourceSaveFile));
        Assert.Equal(hash, Hash(f.Request.IsolatedSaveFile));
    }

    [Fact]
    public void SharedNativeLadderRetainsAllTenExactThresholds()
    {
        Assert.Equal(new[] { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 },
            Enumerable.Range(1, 10).Select(NativeSkillExperienceRules.ExperienceForLevel));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSkillExperienceRules.ExperienceForLevel(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSkillExperienceRules.ExperienceForLevel(11));
    }

    private static (SkillTerminalSaveBoundaryRequest Request, string RequestPath, string OutputPath) Fixture(int target)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SkillBoundary", Guid.NewGuid().ToString("N"));
        var original = Path.Combine(root, "source");
        var isolation = Path.Combine(root, "isolated-saves");
        Directory.CreateDirectory(original);
        Directory.CreateDirectory(isolation);
        var source = Path.Combine(original, "slot");
        var clone = Path.Combine(isolation, "slot");
        var player = new XElement("player", new XElement("money", 17), new XElement("achievements"),
            new XElement("experiencePoints", Enumerable.Range(0, 6).Select(_ => new XElement("int", 0))),
            new[] { "farmingLevel", "fishingLevel", "foragingLevel", "miningLevel", "combatLevel", "luckLevel" }
                .Select(name => new XElement(name, 0)),
            new XElement("stats", new XElement("Values", new XElement("item", new XElement("key", new XElement("string", "daysPlayed")),
                new XElement("value", new XElement("unsignedInt", 1))))));
        new XDocument(new XElement("SaveGame", new XElement("gameVersion", "1.6.15"), player,
            new XElement("year", 1), new XElement("currentSeason", "spring"), new XElement("dayOfMonth", 1))).Save(source);
        File.Copy(source, clone);
        var request = new SkillTerminalSaveBoundaryRequest("skill_terminal_save_boundary_request.v1", source, clone,
            isolation, target, 7, true, false);
        var requestPath = Path.Combine(root, "request.json");
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
        return (request, requestPath, Path.Combine(root, "report.json"));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
