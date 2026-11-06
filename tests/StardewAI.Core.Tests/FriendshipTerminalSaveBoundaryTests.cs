using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class FriendshipTerminalSaveBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ReturnedDeclarationsCannotMutateTheNextPreparation()
    {
        var first = Fixture(5);
        var report = FriendshipTerminalSaveBoundaryBuilder.Prepare(first.RequestPath, first.Output);
        var ids = (string[])report.GetType().GetProperty("declared_completed_local_event_ids")!.GetValue(report)!;
        ids[0] = "edited";
        var second = Fixture(5);
        var next = JsonSerializer.SerializeToElement(FriendshipTerminalSaveBoundaryBuilder.Prepare(second.RequestPath, second.Output));
        Assert.Equal("18", next.GetProperty("declared_completed_local_event_ids")[0].GetString());
        Assert.Contains(XDocument.Load(second.Request.IsolatedSaveFile).Root!.Element("player")!.Element("eventsSeen")!.Elements("int"),
            row => row.Value == "18");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void OnlyTheExactCloneGetsDeclaredPrerequisitesAndSourceRelationshipsStayIntact(int target)
    {
        var f = Fixture(target);
        var sourceHash = Hash(f.Request.SourceSaveFile);
        var before = XDocument.Load(f.Request.IsolatedSaveFile);
        var report = JsonSerializer.SerializeToElement(FriendshipTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(sourceHash, Hash(f.Request.SourceSaveFile));
        var saved = XDocument.Load(f.Request.IsolatedSaveFile);
        var player = saved.Root!.Element("player")!;
        var rows = player.Element("friendshipData")!.Elements("item").ToDictionary(
            row => row.Element("key")!.Element("string")!.Value, row => row.Element("value")!.Element("Friendship")!);
        Assert.Equal(target - 1, rows.Values.Count(row => int.Parse(row.Element("Points")!.Value) >= 1975));
        Assert.Equal("2407", rows["Abigail"].Element("Points")!.Value);
        Assert.Equal("Married", rows["Abigail"].Element("Status")!.Value);
        Assert.Equal("1955", rows["George"].Element("Points")!.Value);
        Assert.Equal("1955", rows["Evelyn"].Element("Points")!.Value);
        Assert.Equal("0", rows["Alex"].Element("Points")!.Value);
        Assert.Equal("1", rows["George"].Element("GiftsToday")!.Value);
        Assert.Equal(before.Root!.Element("player")!.Element("items")!.ToString(), player.Element("items")!.ToString());
        Assert.Equal("JoshHouse", player.Element("lastSleepLocation")!.Value);
        Assert.Equal("15", player.Element("lastSleepPoint")!.Element("X")!.Value);
        Assert.Equal(new[] { "existing", "18", "19", "21", "2119820", "56", "5837189" },
            player.Element("eventsSeen")!.Elements("int").Select(row => row.Value));
        Assert.False(report.GetProperty("prior_story_progress_verified").GetBoolean());
        Assert.False(report.GetProperty("native_criterion_outcome_verified").GetBoolean());
        Assert.False(report.GetProperty("prior_friendship_acquisition_verified").GetBoolean());
    }

    [Theory]
    [InlineData("source_target")]
    [InlineData("changed_clone")]
    [InlineData("bad_count")]
    [InlineData("undeclared")]
    [InlineData("undeclared_story")]
    [InlineData("formal")]
    [InlineData("foreign_basis")]
    [InlineData("unsafe_tile")]
    [InlineData("point_drift")]
    public void InvalidBoundaryNeverWritesTheOriginalOrClone(string change)
    {
        var f = Fixture(5);
        var request = f.Request;
        if (change == "source_target") request = request with { IsolatedSaveFile = request.SourceSaveFile };
        if (change == "changed_clone") File.AppendAllText(request.IsolatedSaveFile, " ");
        if (change == "bad_count") request = request with { TargetQualifyingCount = 6 };
        if (change == "undeclared") request = request with { DeclaredLocalPrerequisiteOnly = false };
        if (change == "undeclared_story") request = request with { DeclaredLocalStoryPrerequisites = false };
        if (change == "formal") request = request with { FormalProductTrainingAuthorized = true };
        if (change is "foreign_basis" or "unsafe_tile" or "point_drift")
        {
            var basis = JsonNode.Parse(File.ReadAllText(request.BasisSnapshotPath))!;
            if (change == "foreign_basis") basis["player_id"]!["value"] = "other";
            if (change == "unsafe_tile") basis["state"]!["locations"]!["social_route_date_evidence"]!["value"]!["locations"]![0]!["static_walkable_tile_ranges"] = new JsonArray();
            if (change == "point_drift") basis["state"]!["npcs"]!["grandpa_friendship_progress"]!["value"]!["eligible_villager_rows"]![1]!["friendship_points"] = 3;
            var snapshot = basis.Deserialize<SnapshotEnvelope>(JsonOptions)!;
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            File.WriteAllText(request.BasisSnapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        File.WriteAllText(f.RequestPath, JsonSerializer.Serialize(request, JsonOptions));
        var source = Hash(request.SourceSaveFile);
        var clone = Hash(request.IsolatedSaveFile);
        Assert.Throws<InvalidDataException>(() => FriendshipTerminalSaveBoundaryBuilder.Prepare(f.RequestPath, f.Output));
        Assert.Equal(source, Hash(request.SourceSaveFile));
        Assert.Equal(clone, Hash(request.IsolatedSaveFile));
        Assert.False(File.Exists(f.Output));
    }

    private static (FriendshipTerminalSaveBoundaryRequest Request, string RequestPath, string Output) Fixture(int target)
    {
        var directory = Path.Combine(Path.GetTempPath(), "friendship-native-boundary", Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(directory, "source");
        var isolation = Path.Combine(directory, "isolated-saves");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(Path.Combine(isolation, "slot"));
        var source = Path.Combine(sourceDir, "slot");
        var clone = Path.Combine(isolation, "slot", "slot");
        var names = new[] { "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Elliott", "Emily", "Gus", "Haley", "Harvey", "George", "Evelyn", "Lewis", "Linus" };
        var points = names.Select(name => name == "Abigail" ? 2407 : 0).ToArray();
        var friendships = new XElement("friendshipData", names.Select((name, index) => new XElement("item",
            new XElement("key", new XElement("string", name)), new XElement("value", new XElement("Friendship",
                new XElement("Points", points[index]), new XElement("TalkedToToday", true), new XElement("GiftsToday", 1),
                new XElement("Status", name == "Abigail" ? "Married" : "Friendly"))))));
        var document = new XDocument(new XElement("SaveGame", new XElement("gameVersion", "1.6.15"),
            new XElement("year", 2), new XElement("currentSeason", "winter"), new XElement("dayOfMonth", 28),
            new XElement("player", new XElement("farmName", "fixture"), new XElement("UniqueMultiplayerID", "1"),
                new XElement("items", new XElement("item", "preserved")), new XElement("eventsSeen", new XElement("int", "existing")),
                new XElement("lastSleepLocation", "Farm"),
                new XElement("lastSleepPoint", new XElement("X", 66), new XElement("Y", 19)),
                new XElement("Position", new XElement("X", 4224), new XElement("Y", 1248)),
                new XElement("sleptInTemporaryBed", new XElement("boolean", true)), friendships)));
        document.Save(source);
        File.Copy(source, clone);
        static JsonElement Field(object value) => JsonSerializer.SerializeToElement(new { status = "available", value });
        var snapshot = new SnapshotEnvelope
        {
            GameVersion = "1.6.15", GameTick = 1,
            SaveId = new() { Status = "available", Value = "fixture" }, PlayerId = new() { Status = "available", Value = "1" },
            State = new()
            {
                ["time"] = JsonSerializer.SerializeToElement(new { year = Field(2), season = Field("winter"), day = Field(28) }),
                ["npcs"] = JsonSerializer.SerializeToElement(new
                {
                    grandpa_friendship_progress = Field(new
                    {
                        threshold_points = 1975, maximum_points = 999999, romance_only = false,
                        projection_status = "complete_live_native_iteration", eligible_villager_count = names.Length, qualifying_count = 1,
                        eligible_villager_rows = names.Select((name, index) => new
                        {
                            native_iteration_index = index, npc_name = name, is_villager = true, event_actor = false,
                            friendship_row_exists = true, friendship_points = points[index], qualifies = points[index] >= 1975
                        })
                    }),
                    social_interaction = Field(names.Select(name => new
                    {
                        name, location_id = "JoshHouse", can_socialize = true,
                        expected_talk_friendship_delta_complete = true, expected_talk_friendship_delta = 20
                    }))
                }),
                ["locations"] = JsonSerializer.SerializeToElement(new
                {
                    social_route_date_evidence = Field(new { locations = new[] { new
                    {
                        location_id = "JoshHouse", static_walkable_tile_ranges = new[] { new { y = 22, start_x = 13, end_x = 18 } }
                    } } })
                })
            }
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var basis = Path.Combine(directory, "basis.json");
        File.WriteAllText(basis, JsonSerializer.Serialize(snapshot, JsonOptions));
        var request = new FriendshipTerminalSaveBoundaryRequest("friendship_terminal_save_boundary_request.v1", source, clone, isolation, basis, target, true, false, true);
        var requestPath = Path.Combine(directory, "request.json");
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request, JsonOptions));
        return (request, requestPath, Path.Combine(directory, "boundary.json"));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
