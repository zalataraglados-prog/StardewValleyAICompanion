using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MarriageTeacherSaveBoundaryTests
{
    [Fact]
    public void DeclaredNonterminalClonePreservesTheOriginalHouseItemsLocationAndOtherRelationships()
    {
        var f = Fixture();
        var source = File.ReadAllBytes(f.Request.SourceSaveFile);
        var report = JsonSerializer.SerializeToElement(MarriageTeacherSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(source, File.ReadAllBytes(f.Request.SourceSaveFile));
        var clone = XDocument.Load(f.Request.IsolatedSaveFile);
        var player = clone.Root!.Element("player")!;
        Assert.Equal("14", clone.Root.Element("dayOfMonth")!.Value);
        Assert.Equal("210", player.Descendants("unsignedInt").Single().Value);
        Assert.Equal("3", player.Element("houseUpgradeLevel")!.Value);
        Assert.Equal("preserved", player.Element("items")!.Value);
        Assert.Equal("FarmHouse", player.Element("lastSleepLocation")!.Value);
        Assert.Equal("", player.Element("spouse")!.Value);
        var rows = player.Element("friendshipData")!.Elements("item").ToDictionary(row => row.Element("key")!.Value, row => row.Element("value")!.Element("Friendship")!);
        Assert.Equal("2420", rows["Abigail"].Element("Points")!.Value);
        Assert.Equal("Dating", rows["Abigail"].Element("Status")!.Value);
        Assert.Equal("historical", rows["Abigail"].Element("WeddingDate")!.Value);
        Assert.Equal("123", rows["George"].Element("Points")!.Value);
        var actorLocation = clone.Root.Element("locations")!.Elements().Single(location => location.Element("characters")!.Elements().Any());
        Assert.Equal("SeedShop", actorLocation.Element("name")!.Value);
        Assert.Equal("64", actorLocation.Element("characters")!.Elements().Single().Element("Position")!.Element("X")!.Value);
        Assert.False(report.GetProperty("prior_partnership_acquisition_verified").GetBoolean());
        Assert.False(report.GetProperty("prior_house_acquisition_verified").GetBoolean());
        Assert.False(report.GetProperty("native_criterion_outcome_verified").GetBoolean());
        Assert.False(report.GetProperty("formal_product_training_authorized").GetBoolean());
    }

    [Theory]
    [InlineData("undeclared")]
    [InlineData("formal")]
    [InlineData("day_zero")]
    [InlineData("day_29")]
    [InlineData("same_path")]
    [InlineData("changed_clone")]
    [InlineData("wrong_basis_date")]
    [InlineData("wrong_basis_points")]
    [InlineData("missing_stat")]
    [InlineData("duplicate_stat")]
    [InlineData("duplicate_identity")]
    [InlineData("wrong_farmer")]
    public void InvalidOrUndeclaredPreparationNeverWritesEitherInput(string mutation)
    {
        var f = Fixture();
        var request = f.Request with
        {
            DeclaredLocalPrerequisitesOnly = mutation != "undeclared",
            FormalProductTrainingAuthorized = mutation == "formal",
            DecisionWinterDayYearTwo = mutation == "day_zero" ? 0 : mutation == "day_29" ? 29 : 14,
            IsolatedSaveFile = mutation == "same_path" ? f.Request.SourceSaveFile : f.Request.IsolatedSaveFile
        };
        if (mutation.StartsWith("wrong_basis_", StringComparison.Ordinal))
        {
            var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(request.BasisSnapshotPath), Options)!;
            if (mutation == "wrong_basis_date") CurrentMarriageHouseStateTests.Mutate(snapshot, "time", node => node["day"]!["value"] = 14);
            if (mutation == "wrong_basis_points") CurrentMarriageHouseStateTests.Mutate(snapshot, "npcs", node => node["friendships"]!["value"]![0]!["points"] = 2408);
            File.WriteAllText(request.BasisSnapshotPath, JsonSerializer.Serialize(snapshot, Options));
        }
        if (mutation is "missing_stat" or "duplicate_stat" or "duplicate_identity" or "wrong_farmer")
        {
            var document = XDocument.Load(request.SourceSaveFile);
            var player = document.Root!.Element("player")!;
            if (mutation == "missing_stat") player.Element("stats")!.Remove();
            if (mutation == "duplicate_stat") player.Element("stats")!.Element("Values")!.Add(new XElement(player.Element("stats")!.Element("Values")!.Elements().Single()));
            if (mutation == "duplicate_identity") player.Element("friendshipData")!.Add(new XElement(player.Element("friendshipData")!.Elements().First()));
            if (mutation == "wrong_farmer") player.Element("UniqueMultiplayerID")!.Value = "2";
            document.Save(request.SourceSaveFile);
            File.Copy(request.SourceSaveFile, request.IsolatedSaveFile, overwrite: true);
        }
        if (mutation == "changed_clone") File.AppendAllText(request.IsolatedSaveFile, " ");
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request, Options));
        var source = File.ReadAllBytes(request.SourceSaveFile);
        var target = File.ReadAllBytes(request.IsolatedSaveFile);
        Assert.Throws<InvalidDataException>(() => MarriageTeacherSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(source, File.ReadAllBytes(request.SourceSaveFile));
        Assert.Equal(target, File.ReadAllBytes(request.IsolatedSaveFile));
        Assert.False(File.Exists(f.Output));
    }

    [Fact]
    public void NormalHomeCoordinatesComeFromTheObservedMetadataRatherThanAnExampleTile()
    {
        var f = Fixture();
        var ordinaryPath = f.Request.UnmarriedNpcBasisSnapshotPath!;
        var ordinary = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(ordinaryPath), Options)!;
        CurrentMarriageHouseStateTests.Mutate(ordinary, "npcs", node =>
        {
            var row = node["schedule_catalog"]!["value"]!["villagers"]![0]!;
            row["default_tile_x"] = 7;
            row["default_tile_y"] = 4;
        });
        File.WriteAllText(ordinaryPath, JsonSerializer.Serialize(ordinary, Options));
        MarriageTeacherSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output);
        var actor = XDocument.Load(f.Request.IsolatedSaveFile).Root!.Element("locations")!.Elements()
            .Single(location => location.Element("name")!.Value == "SeedShop").Element("characters")!.Elements().Single();
        Assert.Equal("448", actor.Element("Position")!.Element("X")!.Value);
        Assert.Equal("256", actor.Element("Position")!.Element("Y")!.Value);
        Assert.True(XNode.DeepEquals(actor.Element("Position"), new XElement("Position", actor.Element("DefaultPosition")!.Elements())));
    }

    [Theory]
    [InlineData("foreign_save")]
    [InlineData("foreign_player")]
    [InlineData("stale_hash")]
    [InlineData("married")]
    [InlineData("ambiguous_npc")]
    [InlineData("absent_map")]
    [InlineData("negative_x")]
    [InlineData("overflow_y")]
    public void ForeignOrInvalidNormalHomeMetadataCannotModifyTheClone(string mutation)
    {
        var f = Fixture();
        var path = f.Request.UnmarriedNpcBasisSnapshotPath!;
        var ordinary = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(path), Options)!;
        if (mutation == "foreign_save") ordinary.SaveId.Value = "another-save";
        if (mutation == "foreign_player") ordinary.PlayerId.Value = "another-player";
        if (mutation == "married") CurrentMarriageHouseStateTests.Mutate(ordinary, "player", node => node["married_or_roommate"]!["value"] = true);
        CurrentMarriageHouseStateTests.Mutate(ordinary, "npcs", node =>
        {
            var rows = node["schedule_catalog"]!["value"]!["villagers"]!.AsArray();
            if (mutation == "ambiguous_npc") rows.Add(rows[0]!.DeepClone());
            if (mutation == "absent_map") rows[0]!["default_map"] = "UnobservedHome";
            if (mutation == "negative_x") rows[0]!["default_tile_x"] = -1;
            if (mutation == "overflow_y") rows[0]!["default_tile_y"] = int.MaxValue;
        });
        if (mutation == "stale_hash") ordinary.StateHash = "not-the-observed-state";
        File.WriteAllText(path, JsonSerializer.Serialize(ordinary, Options));
        var source = File.ReadAllBytes(f.Request.SourceSaveFile);
        var clone = File.ReadAllBytes(f.Request.IsolatedSaveFile);
        Assert.Throws<InvalidDataException>(() => MarriageTeacherSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(source, File.ReadAllBytes(f.Request.SourceSaveFile));
        Assert.Equal(clone, File.ReadAllBytes(f.Request.IsolatedSaveFile));
        Assert.False(File.Exists(f.Output));
    }

    [Fact]
    public void NewInventoryFixtureDoesNotMoveNpcsChangeTimeOrReplaceExistingInventoryAndRunsOnlyInIsolation()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "Invoke-RuntimeFriendshipThresholdSmoke.ps1"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var text = File.ReadAllText(Path.Combine(directory!.FullName, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MarriageTeacherFixture.cs"));
        Assert.Contains("ValidateExecutionRequest(request)", text);
        Assert.Contains("STARDEWAI_SAVE_ISOLATION_PATH", text);
        Assert.Contains("STARDEWAI_TRAINING_RUN_ID", text);
        Assert.Contains("Where(row => row.item is null)", text);
        Assert.DoesNotContain("characters", text);
        Assert.DoesNotContain("Game1.timeOfDay =", text);
        Assert.DoesNotContain("friendshipData", text);
        Assert.DoesNotContain("Items.Clear", text);
    }

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static (MarriageTeacherSaveBoundaryRequest Request, string RequestPath, string Output) Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-marriage-boundary-" + Guid.NewGuid().ToString("N"));
        var saves = Path.Combine(root, "isolated-saves");
        Directory.CreateDirectory(saves);
        var source = Path.Combine(root, "source", "slot");
        var target = Path.Combine(saves, "slot", "slot");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        XElement Friendship(string name, int points, string status) => new("item", new XElement("key", new XElement("string", name)),
            new XElement("value", new XElement("Friendship", new XElement("Points", points), new XElement("Status", status),
                new XElement("GiftsToday", 1), new XElement("GiftsThisWeek", 1), new XElement("TalkedToToday", "true"),
                new XElement("ProposalRejected", "false"), new XElement("RoommateMarriage", "false"), new XElement("WeddingDate", "historical"))));
        var locations = new XElement("locations",
            new XElement("GameLocation", new XElement("name", "FarmHouse"), new XElement("characters", new XElement("Abigail",
                new XElement("name", "Abigail"), new XElement("defaultMap", "FarmHouse"),
                new XElement("DefaultPosition", new XElement("X", 2816), new XElement("Y", 1472)),
                new XElement("Position", new XElement("X", 1408), new XElement("Y", 1536))))),
            new XElement("GameLocation", new XElement("name", "SeedShop"), new XElement("characters")));
        var player = new XElement("player", new XElement("farmName", "fixture"), new XElement("UniqueMultiplayerID", "1"), new XElement("spouse", "Abigail"),
            new XElement("houseUpgradeLevel", 3), new XElement("daysUntilHouseUpgrade", -1), new XElement("items", "preserved"),
            new XElement("lastSleepLocation", "FarmHouse"), new XElement("friendshipData", Friendship("Abigail", 2407, "Married"), Friendship("George", 123, "Friendly")),
            new XElement("stats", new XElement("Values", new XElement("item", new XElement("key", new XElement("string", "daysPlayed")),
                new XElement("value", new XElement("unsignedInt", 213))))));
        new XDocument(new XElement("SaveGame", new XElement("gameVersion", "1.6.15"), new XElement("year", 2),
            new XElement("currentSeason", "winter"), new XElement("dayOfMonth", 17), player, locations)).Save(source);
        File.Copy(source, target);
        var basis = CurrentMarriageHouseStateTests.Snapshot(true, 3);
        basis.SaveId.Value = "fixture";
        basis.PlayerId.Value = "1";
        basis.State["npcs"] = JsonSerializer.SerializeToElement(new { friendships = CurrentMarriageHouseStateTests.Field(
            new[] { new { npc_name = "Abigail", status = "Married", points = 2407 } }),
            schedule_catalog = CurrentMarriageHouseStateTests.Field(new { villagers = new[] { new { npc_name = "Abigail", default_map = "FarmHouse" } } }) });
        basis.StateHash = SnapshotHash.ComputeStateHash(basis.State);
        var basisPath = Path.Combine(root, "basis.json");
        File.WriteAllText(basisPath, JsonSerializer.Serialize(basis, Options));
        var ordinary = CurrentMarriageHouseStateTests.Snapshot(false, 3, spouse: "");
        ordinary.SaveId.Value = "fixture";
        ordinary.PlayerId.Value = "1";
        ordinary.State["npcs"] = JsonSerializer.SerializeToElement(new { schedule_catalog = CurrentMarriageHouseStateTests.Field(new
            { villagers = new[] { new { npc_name = "Abigail", default_map = "SeedShop", default_tile_x = 1, default_tile_y = 9 } } }) });
        ordinary.StateHash = SnapshotHash.ComputeStateHash(ordinary.State);
        var ordinaryPath = Path.Combine(root, "ordinary.json");
        File.WriteAllText(ordinaryPath, JsonSerializer.Serialize(ordinary, Options));
        var request = new MarriageTeacherSaveBoundaryRequest("marriage_teacher_save_boundary_request.v1", source, target, saves, basisPath, 14, true, false)
            { UnmarriedNpcBasisSnapshotPath = ordinaryPath };
        var requestPath = Path.Combine(root, "request.json");
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request, Options));
        return (request, requestPath, Path.Combine(root, "boundary.json"));
    }
}
