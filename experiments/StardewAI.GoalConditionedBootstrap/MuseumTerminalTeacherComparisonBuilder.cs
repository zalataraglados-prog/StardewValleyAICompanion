using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumTerminalTeacherComparisonBuilder
{
    public const int HorizonTicks = 600;
    public const string PolicyId = "independent_teacher.museum_new_achievement_5_within_600_ticks.v1";
    private const string DeferId = "museum:bounded_no_donation:600";

    public static MuseumTerminalTeacherComparison Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MuseumTerminalTeacherComparisonRequest>(requestPath, "museum_terminal_teacher_comparison_request.v1");
        Require(request.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative, "Museum Teacher goal differs.");
        var intakePath = RequiredFile(request.IntakePath);
        var intake = CurrentMuseumCollectionIntakeBuilder.Verify(intakePath);
        var snapshot = ReadTyped<SnapshotEnvelope>(intake.SnapshotPath, "snapshot.v1");
        ValidatePrestate(intake, snapshot);
        var ledgerPath = RequiredFile(request.StrategyLedgerPath);
        var ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, snapshot).Ledger;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, intake.ConsideredOptionIds, commitmentLedger: ledger);
        var candidates = availability.Options.SelectMany(row => row.EventCandidates).OrderBy(row => row.CandidateId, StringComparer.Ordinal).ToArray();
        Require(candidates.All(row => !string.IsNullOrWhiteSpace(row.CandidateId)) &&
            candidates.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() == candidates.Length &&
            candidates.Select(row => row.CandidateId).SequenceEqual(intake.Availability.Options.SelectMany(row => row.EventCandidates)
                .Select(row => row.CandidateId).Order(StringComparer.Ordinal)),
            "Managed Museum evaluation changed the original current donation denominator.");
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, request.GoalId)
            .ToDictionary(row => row.CandidateId, StringComparer.Ordinal);
        var missing = intake.Items.Single(row => !row.Donated).QualifiedItemId;
        var rows = candidates.Select(candidate => ranked.TryGetValue(candidate.CandidateId, out var current)
            ? EvaluateDonation(snapshot, ledger, current, missing, request.GoalId)
            : new MuseumTerminalTeacherAlternative(candidate.CandidateId, candidate.Kind, candidate.QualifiedItemId,
                false, candidate.BlockReasons.Append("original_timeline_did_not_admit_candidate").Distinct(StringComparer.Ordinal).ToArray(),
                0, candidate.EstimatedTicks, null, null)).ToList();
        var defer = EvaluateDefer(snapshot, ledger, request.GoalId);
        rows.Add(defer);
        var pairs = defer.Admitted ? rows.Where(row => row.Admitted && row.ProjectedNewAchievementCredit == 1)
            .Select(row => new CurrentCollectionTeacherPairwisePreference(row.CandidateId, DeferId,
                "projected_new_native_achievement_5_within_declared_horizon", "preferred_over_current_eligible_counterfactual")).ToArray()
            : Array.Empty<CurrentCollectionTeacherPairwisePreference>();
        var day = TotalDay(snapshot);
        var splitKey = Identity(snapshot.SaveId) + ":" + Identity(snapshot.PlayerId) + ":" + day;
        return new(requestPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath), ["intake"] = CurrentTeacherFrontierSupport.HashFile(intakePath),
            ["snapshot"] = intake.SnapshotSha256, ["knowledge"] = intake.KnowledgeSha256,
            ["requirement_inventory"] = intake.RequirementInventorySha256, ["dependencies"] = intake.DependenciesSha256,
            ["strategy_ledger"] = CurrentTeacherFrontierSupport.HashFile(ledgerPath)
        }, snapshot.StateHash, request.GoalId, Identity(snapshot.SaveId), Identity(snapshot.PlayerId), day,
            snapshot.GameTick, ledger.Revision, splitKey, PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey), rows.ToArray(), pairs);
    }

    public static MuseumTerminalTeacherComparison Verify(string comparisonPath)
    {
        var text = CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(comparisonPath));
        var stored = JsonSerializer.Deserialize<MuseumTerminalTeacherComparison>(text, JsonDefaults.Options) ??
            throw new InvalidDataException("Museum comparison is null.");
        var rebuilt = Build(stored.RequestPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(text), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Museum comparison differs from its independent current sources.");
        return rebuilt;
    }

    public static (SmallModelPlanEnvelope Plan, ActionQueueEnvelope Queue) Compile(string comparisonPath, string candidateId)
    {
        var comparison = Verify(comparisonPath);
        Require(comparison.PairwisePreferences.Any(row => row.PreferredCandidateId == candidateId),
            "Museum selected candidate has no independently verified preference pair.");
        var selected = comparison.CandidateEvaluations.Single(row => row.CandidateId == candidateId);
        Require(selected.Admitted && selected.CompiledPlan is not null && selected.CompiledQueue is not null,
            "Museum selected original compilation is unavailable.");
        return (selected.CompiledPlan!, selected.CompiledQueue!);
    }

    private static void ValidatePrestate(CurrentMuseumCollectionIntake intake, SnapshotEnvelope snapshot)
    {
        Require(intake.CurrentTransitionSourcesReadable && intake.DonatedCount == 94 && intake.MissingItemCount == 1 &&
            !intake.CollectionComplete && !intake.NativeAchievementReceived && TotalDay(snapshot) is >= 0 and < 224,
            "Museum terminal comparison needs an unearned 94-to-95 decision before Year 3.");
        var museum = Value(snapshot, "world_progress", "museum");
        Require(museum.TryGetProperty("museum_is_current_location", out var here) && here.ValueKind == JsonValueKind.True &&
            Value(snapshot, "player", "location_id").ValueEquals("ArchaeologyHouse"), "Museum terminal comparison must be inside the museum.");
        var story = Value(snapshot, "player", "story_event");
        var menu = Value(snapshot, "menus", "active_menu");
        Require(story.ValueKind == JsonValueKind.Object && ExplicitFalse(story, "active") && ExplicitFalse(story, "event_up") &&
            menu.ValueKind == JsonValueKind.Object && ExplicitFalse(menu, "is_open"), "Museum terminal comparison has active native input.");
        Require(SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.AutonomousRecoveryStartTime) is int minutes &&
            minutes >= GameClockBudgetPolicy.TicksToGameMinutes(HorizonTicks), "Museum comparison horizon exceeds the safe clock budget.");
    }

    private static bool ExplicitFalse(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.False;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
