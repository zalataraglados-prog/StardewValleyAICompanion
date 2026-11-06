using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CurrentMarriageHouseIntake(
    string SnapshotPath, string SnapshotSha256, string KnowledgePath, string KnowledgeSha256,
    string DependenciesPath, string DependenciesSha256, CurrentMarriageHouseState CurrentState,
    JsonElement OriginalDirection, string[] ConsideredOptionIds, OptionAvailabilityEnvelope Availability)
{
    public string SchemaVersion => "current_marriage_house_intake.v1";
    public string Status => CurrentState.Status;
    public string CandidateScope => "complete_original_current_transition_sources_not_future_acquisition_or_teacher_labels";
    public bool SocialCandidateLimitApplied => false;
    public bool CurrentTransitionSourcesReadable => Status == "ready_current_state_only" &&
        Availability.Options.Select(row => row.OptionId).SequenceEqual(ConsideredOptionIds) &&
        Availability.Options.All(row => row.ReadEligible);
    public bool TrainingLabelEligible => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static class CurrentMarriageHouseIntakeBuilder
{
    public const string DirectionId = "marriage_and_house_upgrade";

    public static CurrentMarriageHouseIntake Build(string snapshotPath, string knowledgePath, string dependenciesPath)
    {
        snapshotPath = RequiredFile(snapshotPath);
        knowledgePath = RequiredFile(knowledgePath);
        dependenciesPath = RequiredFile(dependenciesPath);
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        var criterion = knowledge.Criteria.SingleOrDefault(row => row.Id == "married_or_roommate_house_2");
        Require(knowledge.GoalId == "grandpa.maximum_21" && knowledge.CriterionCount == 19 && criterion is not null &&
            criterion.Points == 1 && criterion.Operation == "and" &&
            criterion.Target == "married_or_roommate=true;farmhouse_upgrade_level>=2" &&
            criterion.StatePaths.SequenceEqual(new[] { "player.married_or_roommate", "player.farmhouse_upgrade_level" }),
            "Marriage/House authority differs from the complete native joint scoring rule.");
        using var document = JsonDocument.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(dependenciesPath));
        var root = document.RootElement;
        Require(root.GetProperty("schema_version").GetString() == "goal_method_dependency_expansions.v1" &&
            root.GetProperty("goal_id").GetString() == knowledge.GoalId &&
            !root.EnumerateObject().Any(property => property.Name.EndsWith("_local_method_view", StringComparison.Ordinal)),
            "Marriage/House intake requires the original unscoped dependency graph.");
        var directions = root.GetProperty("directions").EnumerateArray()
            .Where(row => row.GetProperty("direction_id").GetString() == DirectionId).ToArray();
        Require(directions.Length == 1, "Marriage/House direction is missing or duplicated.");
        var direction = directions[0];
        var optionIds = direction.GetProperty("nodes").EnumerateArray().Where(row => row.TryGetProperty("option_id", out _))
            .Select(row => row.GetProperty("option_id").GetString()!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var direct = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId);
        Require(optionIds.SequenceEqual(direct.PermittedOptionIds.Concat(new[]
            { "social.talk_npc", "social.gift_npc", "recovery.stabilize_day" }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
            "Marriage/House dependency transition sources differ from the original catalog/support contract.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotPath, "Marriage/House snapshot");
        var state = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        // Inspection must retain every option and unavailable row; exclusive recovery/ranking is not denominator collection.
        var availability = state.Status == "ready_current_state_only"
            ? CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison().Evaluate(snapshot, optionIds, true)
            : new OptionAvailabilityEnvelope { StateHash = snapshot.StateHash };
        return new(snapshotPath, CurrentTeacherFrontierSupport.HashFile(snapshotPath), knowledgePath, knowledge.Sha256,
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath), state, direction.Clone(), optionIds, availability);
    }

    public static CurrentMarriageHouseIntake Verify(string intakePath)
    {
        var stored = JsonSerializer.Deserialize<CurrentMarriageHouseIntake>(
            CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(intakePath)), JsonDefaults.Options) ??
            throw new InvalidDataException("Marriage/House intake is null.");
        var rebuilt = Build(stored.SnapshotPath, stored.KnowledgePath, stored.DependenciesPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(intakePath)),
            JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)), "Marriage/House intake differs from its original sources.");
        return rebuilt;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
