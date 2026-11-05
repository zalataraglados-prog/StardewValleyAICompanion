using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyDonationTeacherComparisonBuilder
{
    private const int HorizonTicks = 600;
    private const string PolicyId = "independent_teacher.rusty_key_threshold_progress_within_600_ticks.v1";
    private const string DeferId = "rusty_key:bounded_no_donation:600";

    public static RustyKeyDonationTeacherComparison Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<RustyKeyDonationTeacherComparisonRequest>(requestPath,
            "rusty_key_donation_teacher_comparison_request.v1");
        var scopedGoal = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == "obtain_rusty_key").EffectiveGoalId;
        Require(request.GoalId == scopedGoal || request.GoalId == GoalMethodTeacherCoverageGoalIds.Authoritative ||
            request.GoalId == GoalMethodTeacherCoverageGoalIds.AcquisitionCorpus, "Rusty Key Teacher goal is not attributable.");
        var snapshotPath = RequiredFile(request.SnapshotPath);
        var ledgerPath = RequiredFile(request.StrategyLedgerPath);
        var snapshot = ReadTyped<SnapshotEnvelope>(snapshotPath, "snapshot.v1");
        ValidatePrestate(snapshot);
        var ledger = AcquisitionStrategyLedgerReader.Read(ledgerPath, snapshot).Ledger;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "museum.donate_items" }, commitmentLedger: ledger);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, request.GoalId);
        var byId = ranked.ToDictionary(row => row.CandidateId, StringComparer.Ordinal);
        var rows = availability.Options.SelectMany(option => option.EventCandidates)
            .OrderBy(row => row.CandidateId, StringComparer.Ordinal)
            .Select(candidate => byId.TryGetValue(candidate.CandidateId, out var current)
                ? EvaluateDonation(snapshot, ledger, current, request.GoalId)
                : new RustyKeyDonationTeacherAlternative(candidate.CandidateId, candidate.Kind, candidate.QualifiedItemId,
                    false, candidate.BlockReasons.Append("original_timeline_did_not_admit_candidate")
                        .Distinct(StringComparer.Ordinal).ToArray(), 0, candidate.EstimatedTicks, null)).ToList();
        Require(rows.Count == availability.Options.Sum(option => option.EventCandidates.Length) &&
            rows.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "Rusty Key Teacher must preserve every original donation candidate, including rejected rows.");
        rows.Add(EvaluateDefer(snapshot, ledger, request.GoalId));
        var defer = rows.Single(row => row.CandidateId == DeferId);
        var pairs = defer.Admitted ? rows.Where(row => row.Admitted && row.ProjectedThresholdCredit == 1)
            .Select(row => new CurrentCollectionTeacherPairwisePreference(row.CandidateId, DeferId,
                "native_projected_threshold_credit_within_declared_horizon",
                "preferred_over_current_eligible_counterfactual")).ToArray()
            : Array.Empty<CurrentCollectionTeacherPairwisePreference>();
        var saveId = Identity(snapshot.SaveId);
        var playerId = Identity(snapshot.PlayerId);
        var day = TotalDay(snapshot);
        var splitKey = saveId + ":" + playerId + ":" + day;
        return new(requestPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["request"] = CurrentTeacherFrontierSupport.HashFile(requestPath),
            ["snapshot"] = CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            ["strategy_ledger"] = CurrentTeacherFrontierSupport.HashFile(ledgerPath)
        }, snapshot.StateHash, request.GoalId, saveId, playerId, day, snapshot.GameTick, ledger.Revision, splitKey,
            PolicyTrajectoryDatasetBuilder.PartitionFor(splitKey), rows.ToArray(), pairs);
    }

    public static RustyKeyDonationTeacherComparison Verify(string comparisonPath)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(RequiredFile(comparisonPath), "Rusty Key Teacher comparison");
        Require(raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("request_path", out var request) &&
            request.ValueKind == JsonValueKind.String, "Rusty Key Teacher request path is missing.");
        var rebuilt = Build(raw.GetProperty("request_path").GetString()!);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Rusty Key Teacher comparison drifted from independently rebuilt prestate sources.");
        return rebuilt;
    }

    private static RustyKeyDonationTeacherAlternative EvaluateDonation(SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger, PolicyEventCandidatePrediction candidate, string goalId)
    {
        var reasons = (candidate.BlockReasons ?? Array.Empty<string>()).ToList();
        if (!candidate.Available || candidate.AllowedNow == false) reasons.Add("original_candidate_not_currently_available");
        if (candidate.Kind != "donate_museum_item") reasons.Add("outside_immediate_donation_scope");
        if (candidate.EstimatedTicks is not >= 1 or > HorizonTicks) reasons.Add("outside_declared_comparison_horizon");
        if (!Parameter(candidate, "expected_donated_count_before", "59") ||
            !Parameter(candidate, "expected_donated_count_after", "60") ||
            !Parameter(candidate, "reaches_rusty_key_threshold", "true") ||
            !Parameter(candidate, "rusty_key_reward_id", "museum60") ||
            !Parameter(candidate, "rusty_key_reward_action", "MarkEventSeen Host 295672") ||
            !Parameter(candidate, "reward_projection_status", "ready")) reasons.Add("native_threshold_projection_unavailable");
        var plan = new DailyPlanCompiler().Compile(new[]
        {
            CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, PolicyId)
        }, snapshot.StateHash, goalId, maxCandidates: 1,
            availableMinutes: SnapshotGameClockBudgetReader.AvailableMinutes(snapshot, GameClockBudgetPolicy.AutonomousRecoveryStartTime));
        plan.PlanId = "rusty_key_teacher_plan." + CompilationIdentity(snapshot, ledger, candidate.CandidateId, goalId);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        reasons.AddRange(CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue));
        if (plan.Steps.Length != 1 || queue.Status != "pending" || queue.Items.Length != 1 ||
            queue.Items.Any(item => item.Status != "pending" || item.OptionId != "executor.donate_museum_item"))
            reasons.Add("original_donation_compiler_rejected");
        Stabilize(queue, snapshot, ledger, candidate.CandidateId);
        var admitted = reasons.Count == 0;
        return new(candidate.CandidateId, candidate.Kind, candidate.QualifiedItemId, admitted,
            reasons.Distinct(StringComparer.Ordinal).ToArray(), admitted ? 1 : 0, candidate.EstimatedTicks, queue);
    }

    private static bool Parameter(PolicyEventCandidatePrediction candidate, string name, string expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(candidate, name, out var value) && value == expected;

    private static void Stabilize(ActionQueueEnvelope queue, SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string candidateId) =>
        AcquisitionRouteDispatchCompilationBuilder.StabilizeQueueIdentity(queue,
            CompilationIdentity(snapshot, ledger, candidateId, queue.GoalId), "rusty_key_teacher");

    private static string CompilationIdentity(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string candidateId, string goalId) =>
        AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(new
            { snapshot.StateHash, ledger.LedgerId, ledger.Revision, candidateId, goalId, PolicyId });
}
