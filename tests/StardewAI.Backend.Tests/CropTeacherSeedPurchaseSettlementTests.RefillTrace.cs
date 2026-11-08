using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillTraceConsumesOriginalCompilationWireAndAllNativeStepsWithoutResourceCredit(bool repair)
    {
        using var f = new RefillTraceFixture(repair);
        await f.Build();
        var trace = f.Open();
        Assert.Equal(f.Approach.Queue.Items.Length, trace.Steps.Count);
        Assert.Equal(repair ? 1 : 0, trace.Steps.Count(step => step.OptionId == "executor.clear_obstacle"));
        Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.NativeOutcomeVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        trace.ValidateInputsUnchanged();
        File.AppendAllText(f.Journals[0] + ".gz", "changed");
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("session")]
    [InlineData("wire")]
    [InlineData("queue-order")]
    [InlineData("missing-observation")]
    [InlineData("gap")]
    [InlineData("result-status")]
    public async Task RefillTraceRejectsChangedScopeOrOriginalNativeChain(string change)
    {
        using var f = new RefillTraceFixture(repair: true);
        await f.Build();
        if (change == "queue-order")
        {
            var aggregate = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
            var rows = aggregate["step_results"]!.AsArray();
            var first = rows[0]!.DeepClone(); rows[0] = rows[1]!.DeepClone(); rows[1] = first;
            Fixture.Save(f.ReceiptPath, aggregate);
        }
        else
        {
            var path = f.Results[^1]; var result = JsonNode.Parse(File.ReadAllText(path))!;
            switch (change)
            {
                case "scope": result["action_origin_observation"]!["scope_id"] = "foreign"; break;
                case "session": result["action_origin_observation"]!["capture_session_id"] = Guid.NewGuid().ToString("N"); break;
                case "wire": result["execution_request_nonce"] = Guid.NewGuid().ToString("N"); break;
                case "missing-observation": result.AsObject().Remove("action_origin_observation"); break;
                case "gap": result["before_game_tick"] = 999999; break;
                case "result-status": result["status"] = "blocked"; break;
            }
            Fixture.Save(path, result);
        }
        Assert.Throws<InvalidDataException>(() => f.Open());
    }
}
