using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentMuseumCollectionIntakeBuilder
{
    public const string DirectionId = "complete_museum_collection";
    private const string SetId = "museum_collection";
    private const string CriterionId = "achievement_complete_collection";

    public static CurrentMuseumCollectionIntake Build(
        string snapshotPath, string knowledgePath, string requirementInventoryPath, string dependenciesPath)
    {
        snapshotPath = RequiredFile(snapshotPath);
        knowledgePath = RequiredFile(knowledgePath);
        requirementInventoryPath = RequiredFile(requirementInventoryPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        var criterion = knowledge.Criteria.SingleOrDefault(row => row.Id == CriterionId);
        Require(knowledge.GoalId == "grandpa.maximum_21" && knowledge.CriterionCount == 19 &&
            criterion is not null && criterion.Points == 1 && criterion.Operation == "contains" &&
            criterion.Target == "5" && criterion.StatePaths.SequenceEqual(new[] { "world_progress.achievements" }),
            "Museum authority differs from the native achievement-5 scoring rule.");
        var inventory = CurrentTeacherFrontierSupport.Read<AuthoritativeRequirementInventoryReport>(
            requirementInventoryPath, "Museum requirement inventory");
        Require(inventory.SchemaVersion == "authoritative_goal_requirement_inventory.v1" &&
            inventory.GoalId == knowledge.GoalId && inventory.GameVersion == "1.6.15" &&
            inventory.Status == "complete" && inventory.DenominatorComplete && inventory.AcquisitionRoutesComplete &&
            inventory.RequirementSets is not null && inventory.RequirementSets.All(row => row is not null),
            "Museum requirement authority is incomplete or unsupported.");
        var set = CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets!, row => row.RequirementSetId,
            SetId, "Museum requirement inventory");
        Require(set.CriterionId == CriterionId && set.TransparentStatePath == "world_progress.museum" &&
            set.DenominatorStatus == "complete" && set.AcquisitionRoutesComplete &&
            set.RequiredGroupCount == 95 && set.Groups is { Length: 95 } &&
            set.Groups.All(row => row is not null && !string.IsNullOrWhiteSpace(row.RequirementId) &&
                row.Alternatives is { Length: 1 } && row.Alternatives[0] is not null &&
                !string.IsNullOrWhiteSpace(row.Alternatives[0].ItemId)) &&
            set.Groups.Select(row => row.RequirementId).Distinct(StringComparer.Ordinal).Count() == 95 &&
            set.Groups.Select(row => row.Alternatives[0].QualifiedItemId).Distinct(StringComparer.Ordinal).Count() == 95,
            "Museum requires all 95 unique native exact-item requirements.");
        CurrentCollectionTeacherFrontierBuilder.ValidateMuseumRequirementShape(set);
        var snapshot = ReadTyped<SnapshotEnvelope>(snapshotPath, "snapshot.v1");
        ValidateSnapshot(snapshot);
        _ = Identity(snapshot.SaveId);
        _ = Identity(snapshot.PlayerId);
        Require(snapshot.GameTick >= 0, "Museum snapshot tick is negative.");
        var museum = Value(snapshot, "world_progress", "museum");
        Require(museum.ValueKind == JsonValueKind.Object, "Museum current progress is malformed.");
        // The original strict reader owns every item, count and missing-set check.
        var progress = CurrentCollectionTeacherFrontierBuilder.ReadMuseumProgressValue(museum, set);
        Require(progress.Available, "Museum per-item progress is unavailable.");
        var achievements = Value(snapshot, "world_progress", "achievements");
        Require(achievements.ValueKind == JsonValueKind.Array &&
            achievements.EnumerateArray().All(row => row.ValueKind == JsonValueKind.Number && row.TryGetInt32(out _)),
            "Museum achievement authority is unavailable or malformed.");
        var achievement = achievements.EnumerateArray().Any(row => row.GetInt32() == 5);
        Require(museum.TryGetProperty("complete_collection_achievement_received", out var received) &&
            received.ValueKind is JsonValueKind.True or JsonValueKind.False && received.GetBoolean() == achievement,
            "Museum and goal achievement facts disagree for this local actor scope.");
        var direction = ReadDirection(dependenciesPath, knowledge.GoalId);
        var options = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId)
            .PermittedOptionIds.Order(StringComparer.Ordinal).ToArray();
        Require(direction.GetProperty("nodes").EnumerateArray().Where(row => row.TryGetProperty("option_id", out _))
            .Select(row => row.GetProperty("option_id").GetString()).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).SequenceEqual(options), "Museum transition sources differ from the original catalog.");
        var items = set.Groups.OrderBy(row => row.RequirementId, StringComparer.Ordinal).Select(row =>
            new CurrentMuseumCollectionItem(row.RequirementId, row.Alternatives[0].ItemId,
                row.Alternatives[0].QualifiedItemId, progress.Requirements[row.RequirementId].Completed)).ToArray();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, options, true);
        return new(snapshotPath, CurrentTeacherFrontierSupport.HashFile(snapshotPath), snapshot.StateHash, snapshot.GameTick,
            knowledgePath, knowledge.Sha256, requirementInventoryPath, CurrentTeacherFrontierSupport.HashFile(requirementInventoryPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath), direction, items,
            progress.AggregateCompletedCount, items.Count(row => !row.Donated), items.All(row => row.Donated), achievement,
            options, availability);
    }

    public static CurrentMuseumCollectionIntake Verify(string intakePath)
    {
        var text = CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(intakePath));
        var stored = JsonSerializer.Deserialize<CurrentMuseumCollectionIntake>(text, JsonDefaults.Options) ??
            throw new InvalidDataException("Museum intake is null.");
        var rebuilt = Build(stored.SnapshotPath, stored.KnowledgePath, stored.RequirementInventoryPath, stored.DependenciesPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(text), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Museum intake differs from its complete original sources.");
        return rebuilt;
    }

    private static JsonElement ReadDirection(string path, string goalId)
    {
        using var document = JsonDocument.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(path));
        var root = document.RootElement;
        Require(root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("schema_version", out var schema) && schema.GetString() == "goal_method_dependency_expansions.v1" &&
            root.TryGetProperty("goal_id", out var goal) && goal.GetString() == goalId &&
            !root.EnumerateObject().Any(row => row.Name.EndsWith("_local_method_view", StringComparison.Ordinal)) &&
            root.TryGetProperty("directions", out var directions) && directions.ValueKind == JsonValueKind.Array,
            "Museum intake requires the original unscoped dependency graph.");
        var matches = root.GetProperty("directions").EnumerateArray().Where(row =>
            row.TryGetProperty("direction_id", out var id) && id.GetString() == DirectionId).ToArray();
        Require(matches.Length == 1 && matches[0].TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Array, "Museum original direction is missing or duplicated.");
        return matches[0].Clone();
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
