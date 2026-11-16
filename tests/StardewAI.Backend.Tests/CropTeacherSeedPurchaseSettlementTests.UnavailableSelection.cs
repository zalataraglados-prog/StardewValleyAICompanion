using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JointUnavailableSuppressionSurvivesSameDayReselectionButNotDayChange(bool dayChanged)
    {
        using var f = new OwnedWateringFixture(prepareSource: CompleteDailySelectionSurface);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 2);
        var candidates = ActualWaterSelectionCandidates(f);
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request,
            f.Basis.Before, f.Shared.Source.Ledger, CompositionRequest(f.Basis.Before, candidates));
        var unavailable = UnavailableChoice("social");
        unavailable.Selection.GetType().GetProperty("TotalDay")!.SetValue(unavailable.Selection,
            binding.OriginalNextStep.TotalDay - (dayChanged ? 1 : 0));
        var released = InvokeLive<object?>("ReleaseUnavailableCropTeacherSelection", unavailable.Selection,
            unavailable.Candidate, unavailable.Evidence, unavailable.Queue)!;
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, candidates);
        using var http = new HttpClient(handler, disposeHandler: false);
        var result = await ComposeSelection(f, owner, http, 1, f.Basis.Before, released);
        var selected = SelectionProperty<object>(result, "Selection")!;
        var expectedCount = dayChanged ? 0 : 1;
        Assert.Equal(expectedCount, SelectionProperty<IReadOnlyCollection<JsonObject>>(selected, "SuppressedForeignContinuations")!.Count);
        var ranking = JsonNode.Parse(File.ReadAllText(SelectionProperty<string>(selected, "RankingPath")!))!;
        Assert.Equal(expectedCount, ranking["released_objective_suppression"]!["suppressed_objective_count"]!.GetValue<int>());
        Assert.Empty(SelectionProperty<IReadOnlySet<string>>(selected, "CompletedForeignCandidateIds")!);
        Assert.Equal(1, handler.Ranks);
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("social")]
    [InlineData("quest")]
    [InlineData("mail")]
    public void JointUnavailableReleaseReusesOldPolicyWithoutCompletingOrMutatingTheChoice(string kind)
    {
        var f = UnavailableChoice(kind);
        var original = f.Candidate.RankedCandidate.ToJsonString();
        var cursor = f.Candidate.ObjectiveContinuation!.ToJsonString();
        var released = InvokeLive<object?>("ReleaseUnavailableCropTeacherSelection", f.Selection, f.Candidate, f.Evidence, f.Queue);
        Assert.NotNull(released);
        Assert.Empty(SelectionProperty<IReadOnlySet<string>>(released!, "CompletedForeignCandidateIds")!);
        Assert.Empty(SelectionProperty<IReadOnlyCollection<JsonObject>>(f.Selection, "SuppressedForeignContinuations")!);
        var suppressed = SelectionProperty<IReadOnlyCollection<JsonObject>>(released!, "SuppressedForeignContinuations")!;
        Assert.Single(suppressed);
        Assert.Empty(QueueReplanFilter.FilterSuppressedContinuations(new JsonArray(f.Candidate.RankedCandidate.DeepClone()), suppressed));
        Assert.Equal(original, f.Candidate.RankedCandidate.ToJsonString());
        Assert.Equal(cursor, f.Candidate.ObjectiveContinuation.ToJsonString());
        Assert.False(f.Evidence["selected_candidate_completed"]!.GetValue<bool>());
        Assert.False(f.Evidence["native_dispatch_performed"]!.GetValue<bool>());
        Assert.False(f.Evidence["shared_source_return_required"]!.GetValue<bool>());
        Assert.Equal("unavailable_until_day_change", f.Evidence["joint_candidate_disposition"]!.GetValue<string>());
        Assert.Null(InvokeLive<object?>("ReleaseUnavailableCropTeacherSelection", released!, f.Candidate, f.Evidence, f.Queue));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("executable_subset")]
    [InlineData("matched_candidate")]
    [InlineData("inactive_filter")]
    [InlineData("no_continuation")]
    [InlineData("source_bound_master")]
    [InlineData("owned_work")]
    public void JointUnavailableReleaseDoesNotSwallowOtherQueueOrOwnershipStates(string fault)
    {
        var f = UnavailableChoice("social");
        var candidate = f.Candidate;
        if (fault == "pending") f.Queue["status"] = "pending";
        if (fault == "executable_subset") f.Queue["items"] = new JsonArray(new JsonObject
            { ["status"] = "pending", ["option_id"] = "executor.wait_ticks", ["normalized_command"] = new JsonObject() });
        if (fault == "matched_candidate") f.Evidence["selected_candidate_filter"]!["selected_candidate_count"] = 1;
        if (fault == "inactive_filter") f.Evidence["selected_candidate_filter"]!["active"] = false;
        if (fault == "no_continuation") candidate = candidate with { ObjectiveContinuation = null };
        if (fault == "source_bound_master") candidate = candidate with { ObjectiveContinuation = new JsonObject
            { ["kind"] = "master_angler", ["option_id"] = "fishing.catch_fish", ["master_angler_source_key"] = "original-source" } };
        if (fault == "owned_work") ((HashSet<string>)SelectionProperty<IReadOnlySet<string>>(f.Selection, "OwnedCandidateIds")!)
            .Add(candidate.CandidateId);
        var original = f.Evidence.ToJsonString();
        Assert.Null(InvokeLive<object?>("ReleaseUnavailableCropTeacherSelection", f.Selection, candidate, f.Evidence, f.Queue));
        Assert.Equal(original, f.Evidence.ToJsonString());
    }

    [Fact]
    public async Task JointUnavailablePreparationRetainsBlockedEvidenceAndRequestsNoDispatchRefresh()
    {
        var f = UnavailableChoice("social");
        var options = new LiveTrainingOptions { Root = Path.Combine(Path.GetTempPath(), "joint-unavailable-" + Guid.NewGuid().ToString("N")),
            RunId = "declared-unavailable", UseJointDailyCalendar = true };
        Directory.CreateDirectory(options.SnapshotDir);
        using var handler = new UnavailableSelectionHandler();
        using var http = new HttpClient(handler);
        var task = InvokeLive<Task>("PrepareCropTeacherSelectedOtherGoalAsync", http, options, 1,
            new SnapshotEnvelope { StateHash = "declared-source" }, f.Selection, f.Candidate, null!);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.True(SelectionProperty<bool>(result, "ReplanWithoutDispatch"));
        Assert.Null(SelectionProperty<object>(result, "OtherGoalPreparation"));
        Assert.Empty(SelectionProperty<IReadOnlySet<string>>(SelectionProperty<object>(result, "Selection")!, "CompletedForeignCandidateIds")!);
        var evidence = JsonNode.Parse(File.ReadAllText(Path.Combine(options.SnapshotDir, "crop-joint-daily-0001-other-goal-evidence.json")))!;
        Assert.Equal("unavailable_until_day_change", evidence["joint_candidate_disposition"]!.GetValue<string>());
        Assert.False(evidence["policy_model_invoked"]!.GetValue<bool>());
        Assert.False(evidence["native_dispatch_performed"]!.GetValue<bool>());
        Assert.False(evidence["selected_candidate_completed"]!.GetValue<bool>());
        Assert.Equal(2, handler.Requests);
    }

    private static (object Selection, SelectedQueueCandidateLock Candidate, JsonObject Evidence, JsonObject Queue) UnavailableChoice(string kind)
    {
        var option = kind == "social" ? "social.talk_npc" : kind == "quest" ? "quest.advance" : "mail.process_letter";
        var continuation = new JsonObject { ["kind"] = kind, ["option_id"] = option,
            ["npc_name"] = "Abigail", ["quest_candidate_id"] = "quest:13:Quest", ["mail_id"] = "declared-mail" };
        var parameters = new JsonArray();
        foreach (var property in continuation.Where(row => row.Key is not "kind" and not "option_id"))
            foreach (var prefix in new[] { "", "continuation." })
                parameters.Add(new JsonObject { ["name"] = prefix + property.Key, ["value"] = property.Value!.DeepClone() });
        var candidate = new SelectedQueueCandidateLock(3, "declared-candidate", option, new JsonObject
            { ["candidate_id"] = "declared-candidate", ["kind"] = "route_connector_tile", ["option_id"] = option, ["parameters"] = parameters }, continuation);
        var lease = (SelectedQueueDecisionLease)typeof(SelectedQueueDecisionLease).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single().Invoke(new object[] { new[] { candidate } });
        var selectionType = typeof(LiveTrainingOptions).Assembly.GetType("Program")!.GetNestedType("CropTeacherDailySelection", BindingFlags.NonPublic)!;
        var selection = Activator.CreateInstance(selectionType, 9, "earn_money", lease, new HashSet<string>(StringComparer.Ordinal),
            "original-plan.json", "original-ranking.json")!;
        return (selection, candidate, new JsonObject { ["selected_candidate_filter"] = new JsonObject
            { ["active"] = true, ["selected_candidate_count"] = 0 } }, new JsonObject { ["status"] = "blocked", ["items"] = new JsonArray() });
    }

    private sealed class UnavailableSelectionHandler : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/v1/planner/options/availability" => "{\"state_hash\":\"declared-source\",\"options\":[]}",
                "/api/v1/planner/daily-plan/compile" => "{\"plan\":{\"state_hash\":\"declared-source\"},\"action_queue\":{\"state_hash\":\"declared-source\",\"status\":\"blocked\",\"items\":[]}}",
                _ => throw new InvalidOperationException("Unexpected native/ranking request: " + request.RequestUri.AbsolutePath)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
