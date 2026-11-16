using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JointSelectionMaterializesOnlyOwnedMaintenanceOutsideTheActualPolicyPool(bool restrictPolicyOptions)
    {
        using var f = new OwnedWateringFixture(prepareSource: CompleteDailySelectionSurface);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 3);
        var options = f.Shared.Runtime.LiveOptions;
        if (restrictPolicyOptions) options.DailyPlanCandidateOptionIds = ["farm.care_for_pets"];
        using var handler = new JointPolicyPoolHandler(f.Shared.Runtime.Handler);
        using var http = new HttpClient(handler, disposeHandler: false);

        // Use the real endpoint and its autonomous-role filter, not an injected maintenance ranking.
        var result = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        Assert.Equal(1, handler.Ranks);
        Assert.DoesNotContain(handler.Ranking!.RankedEventCandidates, row => row.OptionId == "farm.maintain_crops");
        var selected = SelectionProperty<CropTeacherRunStep>(result, "Step")!;
        Assert.Equal(2, selected.ActionBatch!.Tiles.Length);
        var decision = SelectionProperty<object>(result, "Selection")!;
        var lease = SelectionProperty<SelectedQueueDecisionLease>(decision, "Lease")!;
        var owned = SelectionProperty<IReadOnlySet<string>>(decision, "OwnedCandidateIds")!;
        Assert.Equal(2, owned.Count);
        Assert.All(owned, id => Assert.Contains(lease.Candidates, row => row.CandidateId == id));
        var ranking = JsonNode.Parse(File.ReadAllText(SelectionProperty<string>(decision, "RankingPath")!))!.AsObject();
        var mechanical = ranking["ranked_event_candidates"]!.AsArray().OfType<JsonObject>()
            .Where(row => row["option_id"]!.GetValue<string>() == "farm.maintain_crops").ToArray();
        Assert.Equal(2, mechanical.Length);
        Assert.All(mechanical, row => Assert.Contains(row["candidate_id"]!.GetValue<string>(), owned));
        Assert.Equal(2, ranking["joint_daily_owned_materialization"]!["candidate_ids"]!.AsArray().Count);
        Assert.False(ranking["joint_daily_owned_materialization"]!["policy_model_selected"]!.GetValue<bool>());
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    private sealed class JointPolicyPoolHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        internal int Ranks { get; private set; }
        internal AvailabilityAwarePolicyPredictionEnvelope? Ranking { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var rank = request.RequestUri!.AbsolutePath.EndsWith("/baseline/rank-options", StringComparison.Ordinal);
            if (rank)
            {
                Ranks++;
                // Supply an empty baseline report, not a fake ranking or a host dataset dependency.
                var input = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!.AsObject();
                input["training_report"] = JsonSerializer.SerializeToNode(new BaselineTrainingReport(), JsonOptions);
                request.Content = new StringContent(input.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            }
            var response = await base.SendAsync(request, token);
            if (rank && response.IsSuccessStatusCode)
                Ranking = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(await response.Content.ReadAsStringAsync(token), JsonOptions);
            return response;
        }
    }
}
