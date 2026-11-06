using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static IntakeFiles Fixture(int donated = 59, bool achievement = false)
    {
        var original = MuseumDonationMainlineTests.CreateDonationTeacherFixture();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(original.SnapshotPath), Json)!;
        var state = JsonSerializer.SerializeToNode(snapshot.State, Json)!;
        var world = state["world_progress"]!;
        var museum = world["museum"]!["value"]!;
        var ids = Enumerable.Range(1, 94).Append(96).Select(value => value.ToString()).ToArray();
        museum["donatable_items"] = JsonSerializer.SerializeToNode(ids.Select((id, index) => new
            { item_id = id, qualified_item_id = "(O)" + id, donated = index < donated }));
        museum["missing_item_ids"] = JsonSerializer.SerializeToNode(ids.Skip(donated));
        museum["total_donatable_items"] = 95;
        museum["donated_count"] = donated;
        museum["missing_item_count"] = 95 - donated;
        museum["collection_complete"] = donated == 95;
        museum["complete_collection_achievement_received"] = achievement;
        world["achievements"] = JsonSerializer.SerializeToNode(new
            { status = "available", value = achievement ? new[] { 5, 28 } : new[] { 28 } });
        snapshot.State = state.Deserialize<Dictionary<string, JsonElement>>(Json)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var files = new IntakeFiles(original.SnapshotPath, Path.Combine(original.Root, "knowledge.json"),
            Path.Combine(original.Root, "requirements.json"), Path.Combine(original.Root, "dependencies.json"),
            Path.Combine(original.Root, "intake.json"));
        Write(files.Snapshot, snapshot);
        var criteria = new[] { new { id = "achievement_complete_collection", points = 1,
            statePaths = new[] { "world_progress.achievements" }, operation = "contains", target = "5" } }
            .Concat(Enumerable.Range(0, 18).Select(index => new { id = "fixture" + index,
                points = index < 2 ? 2 : 1, statePaths = new[] { "fixture" }, operation = "equals", target = "true" }));
        Write(files.Knowledge, new { schema_version = "fixture", issues = Array.Empty<string>(),
            summary = new { grandpaCriterionCount = 19, grandpaMaximumScore = 21 },
            grandpa_goal = new { goalId = "grandpa.maximum_21", targetScore = 21, criteria } });
        Write(files.Inventory, new AuthoritativeRequirementInventoryReport
        {
            Status = "complete", GoalId = "grandpa.maximum_21", GameVersion = "1.6.15",
            DenominatorComplete = true, AcquisitionRoutesComplete = true,
            RequirementSets = new[] { new GoalRequirementSet
            {
                RequirementSetId = "museum_collection", CriterionId = "achievement_complete_collection",
                TransparentStatePath = "world_progress.museum", DenominatorStatus = "complete",
                AcquisitionRoutesComplete = true, RequiredGroupCount = 95, RouteCoveredGroupCount = 95,
                Groups = ids.Select(id => new GoalRequirementGroup
                {
                    RequirementId = "museum_collection:item:" + id, SelectionRule = "all_required", RequiredAlternativeCount = 1,
                    Alternatives = new[] { new GoalRequirementAlternative
                        { ItemId = id, QualifiedItemId = "(O)" + id, MatchKind = "item_id", Amount = 1 } }
                }).ToArray()
            } }
        });
        Write(files.Dependencies, new { schema_version = "goal_method_dependency_expansions.v1", goal_id = "grandpa.maximum_21",
            directions = new[] { new { direction_id = "complete_museum_collection",
                blockers = new[] { "full_new_save_obligations_remain" },
                nodes = new[] { new { id = "donate", option_id = "museum.donate_items" } },
                retained_extension = "original_dependency_metadata" } } });
        return files;
    }

    private static CurrentMuseumCollectionIntake Build(IntakeFiles f) =>
        CurrentMuseumCollectionIntakeBuilder.Build(f.Snapshot, f.Knowledge, f.Inventory, f.Dependencies);

    private static void Edit(string path, Action<JsonNode> change, bool snapshot = false, bool preserveHash = false)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        change(root);
        if (snapshot && !preserveHash)
            root["state_hash"] = SnapshotHash.ComputeStateHash(root["state"]!.Deserialize<Dictionary<string, JsonElement>>(Json)!);
        File.WriteAllText(path, root.ToJsonString(Json));
    }

    private static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    private sealed record IntakeFiles(string Snapshot, string Knowledge, string Inventory, string Dependencies, string Intake);
}
