using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MasterAnglerTerminalSaveBoundaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReportOutputCannotClobberSaveOrExistingMetadata(bool save)
    {
        var f = Fixture();
        var source = Hash(f.Request.SourceSaveFile);
        var target = Hash(f.Request.IsolatedSaveFile);
        var metadata = Hash(f.RequestPath);
        Assert.Throws<InvalidDataException>(() => MasterAnglerTerminalSaveBoundaryBuilder.Prepare(f.RequestPath,
            save ? f.Request.SourceSaveFile : f.RequestPath));
        Assert.Equal(source, Hash(f.Request.SourceSaveFile));
        Assert.Equal(target, Hash(f.Request.IsolatedSaveFile));
        Assert.Equal(metadata, Hash(f.RequestPath));
    }

    [Fact]
    public void PublicPreparationOnlyChangesFreshCloneAndCannotBeReplayedAsAnotherPreparation()
    {
        var f = Fixture();
        var sourceHash = Hash(f.Request.SourceSaveFile);
        var report = MasterAnglerTerminalSaveBoundaryBuilder.Prepare(f.RequestPath,
            Path.Combine(Path.GetDirectoryName(f.RequestPath)!, "new-report.json"));
        Assert.Equal(sourceHash, Hash(f.Request.SourceSaveFile));
        Assert.NotEqual(sourceHash, Hash(f.Request.IsolatedSaveFile));
        Assert.Equal(Hash(f.Request.IsolatedSaveFile), report.PreparedSaveSha256);
        Assert.False(report.NativeCriterionOutcomeVerified);
        Assert.False(report.PriorSpeciesAcquisitionVerified);
        Assert.False(report.FormalProductTrainingAuthorized);
        var xml = XDocument.Load(f.Request.IsolatedSaveFile);
        var ids = xml.Descendants("fishCaught").Single().Elements("item").Select(row => row.Element("key")!.Element("string")!.Value).ToArray();
        Assert.Equal(71, ids.Length);
        Assert.DoesNotContain("(O)fish71", ids);
        Assert.Equal(new[] { "24", "25" }, xml.Descendants("achievements").Single().Elements().Select(row => row.Value));
        Assert.Equal("2", xml.Root!.Element("year")!.Value);
        Assert.Equal("13", xml.Root.Element("dayOfMonth")!.Value);
        Assert.Equal("125", xml.Descendants("unsignedInt").Single().Value);
        Assert.Throws<InvalidDataException>(() => MasterAnglerTerminalSaveBoundaryBuilder.Prepare(f.RequestPath));
    }

    [Theory]
    [InlineData("no_ack")]
    [InlineData("formal")]
    [InlineData("day")]
    [InlineData("authority_hash")]
    [InlineData("already_earned")]
    [InlineData("wrong_version")]
    [InlineData("unknown_species")]
    [InlineData("nonidentical_clone")]
    public void PublicRejectionNeverChangesSourceOrTarget(string mode)
    {
        var f = Fixture();
        var request = mode switch
        {
            "no_ack" => f.Request with { DeclaredLocalPrerequisiteOnly = false },
            "formal" => f.Request with { FormalProductTrainingAuthorized = true },
            "day" => f.Request with { DecisionSpringDayYearTwo = 29 },
            "unknown_species" => f.Request with { MissingQualifiedItemId = "(O)unknown" },
            _ => f.Request
        };
        if (mode is "already_earned" or "wrong_version")
        {
            var xml = XDocument.Load(request.SourceSaveFile);
            if (mode == "already_earned") xml.Descendants("achievements").Single().Add(new XElement("int", 26));
            else xml.Root!.Element("gameVersion")!.Value = "1.6.14";
            xml.Save(request.SourceSaveFile);
            File.Copy(request.SourceSaveFile, request.IsolatedSaveFile, overwrite: true);
        }
        if (mode == "authority_hash") File.AppendAllText(request.RequirementInventoryPath, " ");
        if (mode == "nonidentical_clone") File.AppendAllText(request.IsolatedSaveFile, " ");
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request));
        var source = Hash(request.SourceSaveFile);
        var target = Hash(request.IsolatedSaveFile);
        Assert.Throws<InvalidDataException>(() => MasterAnglerTerminalSaveBoundaryBuilder.Prepare(f.RequestPath));
        Assert.Equal(source, Hash(request.SourceSaveFile));
        Assert.Equal(target, Hash(request.IsolatedSaveFile));
    }

    [Theory]
    [InlineData("source_target_same")]
    [InlineData("target_outside")]
    [InlineData("source_inside")]
    [InlineData("drive_root")]
    [InlineData("wildcard")]
    [InlineData("relative")]
    public void ScopeValidationRejectsAnyNonisolatedWrite(string mode)
    {
        var f = Fixture();
        var source = f.Request.SourceSaveFile;
        var target = f.Request.IsolatedSaveFile;
        var root = f.Request.IsolationRoot;
        switch (mode)
        {
            case "source_target_same": target = source; break;
            case "target_outside":
                target = Path.Combine(Path.GetDirectoryName(source)!, "outside", "slot");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
                break;
            case "source_inside":
                var inside = Path.Combine(root, "source", "slot");
                Directory.CreateDirectory(Path.GetDirectoryName(inside)!);
                File.Copy(source, inside);
                source = inside;
                break;
            case "drive_root": root = Path.GetPathRoot(root)!; break;
            case "wildcard": target += "*"; break;
            case "relative": target = "slot"; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => typeof(MasterAnglerTerminalSaveBoundaryBuilder)
            .GetMethod("ValidateScope", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { source, target, root }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private sealed record Files(MasterAnglerTerminalSaveBoundaryRequest Request, string RequestPath);

    private static Files Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-terminal-save-boundary", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source", "slot");
        var isolatedRoot = Path.Combine(root, "isolated");
        var target = Path.Combine(isolatedRoot, "slot", "slot");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(source, "<SaveGame><gameVersion>1.6.15</gameVersion><year>1</year><currentSeason>spring</currentSeason><dayOfMonth>7</dayOfMonth><player><fishCaught/><achievements/><stats><Values><item><key><string>daysPlayed</string></key><value><unsignedInt>7</unsignedInt></value></item></Values></stats></player></SaveGame>");
        File.Copy(source, target);
        var inventory = new AuthoritativeRequirementInventoryReport
        {
            Status = "complete", GoalId = "grandpa.maximum_21", GameVersion = "1.6.15", DenominatorComplete = true, AcquisitionRoutesComplete = true,
            RequirementSets = new[] { new GoalRequirementSet
            {
                RequirementSetId = "master_angler", RequiredGroupCount = 72, AcquisitionRoutesComplete = true,
                Groups = Enumerable.Range(0, 72).Select(i => new GoalRequirementGroup
                {
                    RequirementId = "fish" + i, SelectionRule = "all_required", RequiredAlternativeCount = 1,
                    Alternatives = new[] { new GoalRequirementAlternative { ItemId = "fish" + i, QualifiedItemId = "(O)fish" + i, MatchKind = "item_id", Amount = 1 } }
                }).ToArray()
            } }
        };
        var inventoryPath = Path.Combine(root, "inventory.json");
        File.WriteAllText(inventoryPath, JsonSerializer.Serialize(inventory));
        var groups = inventory.RequirementSets[0].Groups.Select(group =>
        {
            var item = group.Alternatives.Single();
            var route = new AcquisitionRequirementRouteLowering("fixture", "fixture", "fixture", "fixture", "fixture", "fixture",
                StageOneCollectionRouteDependencyAxes.Required.ToArray(), new[] { "executor.collect_crab_pot" }, Array.Empty<string>(), true, true);
            var alternative = new AcquisitionRequirementAlternativeLowering(item.ItemId, item.QualifiedItemId, "fixture", "item_id", 1, 0, true, true, new[] { route });
            return new AcquisitionRequirementGroupLowering(group.RequirementId, 1, 1, 1, true, true, Array.Empty<string>(), new[] { alternative });
        }).ToArray();
        var lowering = new AcquisitionRouteOptionLoweringReport
        {
            Status = "complete", GoalId = inventory.GoalId, RequirementInventorySha256 = Hash(inventoryPath), DependencyAxisInventoryComplete = true,
            RequiredDownstreamDependencyAxes = StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            RequirementSets = new[] { new AcquisitionRequirementSetLowering("master_angler", 72, 72, 72, groups) }
        };
        var loweringPath = Path.Combine(root, "lowering.json");
        File.WriteAllText(loweringPath, JsonSerializer.Serialize(lowering));
        var request = new MasterAnglerTerminalSaveBoundaryRequest
        {
            SourceSaveFile = source, IsolatedSaveFile = target, IsolationRoot = isolatedRoot, RequirementInventoryPath = inventoryPath,
            AcquisitionLoweringPath = loweringPath, MissingQualifiedItemId = "(O)fish71", DecisionSpringDayYearTwo = 13, DeclaredLocalPrerequisiteOnly = true
        };
        var path = Path.Combine(root, "request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        return new(request, path);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
