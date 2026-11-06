using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class FriendshipTerminalSaveBoundaryTests
{
    [Fact]
    public void CalendarIsOptionalAndTheOriginalConstructorAndDefaultDateRemainAvailable()
    {
        var f = Fixture(5);
        Assert.NotNull(typeof(FriendshipTerminalSaveBoundaryRequest).GetConstructor(new[]
            { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool) }));
        Assert.DoesNotContain("decision_winter_day_year_two", JsonSerializer.Serialize(f.Request, JsonOptions));
        var xml = XDocument.Load(f.Request.SourceSaveFile);
        xml.Root!.Element("player")!.Element("stats")!.Remove();
        xml.Save(f.Request.SourceSaveFile);
        File.Copy(f.Request.SourceSaveFile, f.Request.IsolatedSaveFile, overwrite: true);
        var hash = Hash(f.Request.SourceSaveFile);
        var report = FriendshipTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output);
        Assert.Equal(hash, Hash(f.Request.SourceSaveFile));
        Assert.Equal("28", XDocument.Load(f.Request.IsolatedSaveFile).Root!.Element("dayOfMonth")!.Value);
        Assert.False(JsonSerializer.SerializeToElement(report).TryGetProperty("decision_winter_day_year_two", out _));
    }

    [Theory]
    [InlineData(5, 21)]
    [InlineData(10, 21)]
    [InlineData(5, 17)]
    [InlineData(10, 17)]
    [InlineData(5, 12)]
    public void DeclaredWinterCalendarOnlyChangesTheFreshCloneAndIsNotAcquisition(int target, int day)
    {
        var f = Fixture(target);
        var request = f.Request with { DecisionWinterDayYearTwo = day };
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request, JsonOptions));
        var hash = Hash(request.SourceSaveFile);
        var report = JsonSerializer.SerializeToElement(FriendshipTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(hash, Hash(request.SourceSaveFile));
        var xml = XDocument.Load(request.IsolatedSaveFile);
        Assert.Equal("2", xml.Root!.Element("year")!.Value);
        Assert.Equal("winter", xml.Root.Element("currentSeason")!.Value);
        Assert.Equal(day.ToString(), xml.Root.Element("dayOfMonth")!.Value);
        Assert.Equal((196 + day).ToString(), xml.Descendants("unsignedInt").Single().Value);
        Assert.False(report.GetProperty("native_criterion_outcome_verified").GetBoolean());
        Assert.False(report.GetProperty("prior_friendship_acquisition_verified").GetBoolean());
        Assert.False(report.GetProperty("formal_product_training_authorized").GetBoolean());
    }

    [Theory]
    [InlineData("day_zero")]
    [InlineData("day_negative")]
    [InlineData("day_overflow")]
    [InlineData("missing_stats")]
    [InlineData("duplicate_stat")]
    [InlineData("malformed_stat")]
    [InlineData("foreign_basis_date")]
    public void CalendarRequestCannotBypassOriginalDateOrWriteOnInvalidInputs(string mutation)
    {
        var f = Fixture(5);
        var request = f.Request with { DecisionWinterDayYearTwo = mutation switch
            { "day_zero" => 0, "day_negative" => -1, "day_overflow" => 29, _ => 21 } };
        if (mutation is "missing_stats" or "duplicate_stat" or "malformed_stat")
        {
            var xml = XDocument.Load(request.SourceSaveFile);
            var stats = xml.Root!.Element("player")!.Element("stats")!;
            if (mutation == "missing_stats") stats.Remove();
            if (mutation == "duplicate_stat") stats.Element("Values")!.Add(new XElement(stats.Element("Values")!.Elements().Single()));
            if (mutation == "malformed_stat") stats.Descendants("unsignedInt").Single().Value = "unknown";
            xml.Save(request.SourceSaveFile);
            File.Copy(request.SourceSaveFile, request.IsolatedSaveFile, overwrite: true);
        }
        if (mutation == "foreign_basis_date")
        {
            var node = JsonNode.Parse(File.ReadAllText(request.BasisSnapshotPath))!;
            node["state"]!["time"]!["day"]!["value"] = 21;
            var basis = node.Deserialize<SnapshotEnvelope>(JsonOptions)!;
            basis.StateHash = SnapshotHash.ComputeStateHash(basis.State);
            File.WriteAllText(request.BasisSnapshotPath, JsonSerializer.Serialize(basis, JsonOptions));
        }
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request, JsonOptions));
        var source = Hash(request.SourceSaveFile);
        var clone = Hash(request.IsolatedSaveFile);
        var error = Assert.Throws<InvalidDataException>(() => FriendshipTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        if (mutation == "foreign_basis_date") Assert.Contains("basis date differs", error.Message);
        Assert.Equal(source, Hash(request.SourceSaveFile));
        Assert.Equal(clone, Hash(request.IsolatedSaveFile));
        Assert.False(File.Exists(f.Output));
    }
}
