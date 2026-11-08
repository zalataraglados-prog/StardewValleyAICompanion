using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("kind")]
    [InlineData("day")]
    [InlineData("run")]
    [InlineData("candidate")]
    [InlineData("executor")]
    [InlineData("receipt")]
    [InlineData("money")]
    [InlineData("inventory")]
    [InlineData("unknown_progress")]
    [InlineData("crop_state")]
    [InlineData("target")]
    [InlineData("actor")]
    public void SupportingRouteRejectsUnprovenNativeOrChangedPersistentBoundary(string change)
    {
        using var f = new Fixture();
        var (transition, after) = CreateSupportingRoute(f);
        transition = change switch
        {
            "kind" => transition with { TransitionKind = "crop_actions" },
            "day" => transition with { TotalDay = transition.TotalDay + 1 },
            "run" => transition with { RunId = "other-run" },
            "candidate" => transition with { SelectedCandidateId = "other-candidate" },
            "executor" => transition with { ExecutorVersion = "other-executor" },
            _ => transition
        };
        if (change is "money" or "inventory" or "unknown_progress" or "crop_state" or "actor")
        {
            if (change == "money") ChangeState(after, "player", value => value["money"]!["value"] = 0);
            if (change == "inventory") ChangeState(after, "player", value => value["inventory"]!["value"]!.AsArray().Clear());
            if (change == "unknown_progress") ChangeState(after, "world_progress", value => value["unrecognized_progress"] = true);
            if (change == "crop_state") ChangeState(after, "farm", value => value["crops"]!["value"]!.AsArray().Add(new JsonObject { ["unexpected"] = true }));
            if (change == "actor") after.PlayerId.Value = "456";
            RewriteSupportingRouteAfter(transition, after);
        }
        if (change == "target")
        {
            var run = JsonNode.Parse(File.ReadAllText(f.RequestPath))!;
            run["layout_intents"]![0]!["location_id"] = "Town";
            Fixture.Save(f.RequestPath, run);
        }
        if (change == "receipt")
        {
            var receipt = JsonNode.Parse(File.ReadAllText(transition.ExecutionReceiptPath))!;
            receipt["success"] = false;
            Fixture.Save(transition.ExecutionReceiptPath, receipt);
        }
        Assert.Throws<InvalidDataException>(() => OpenRoute(f, transition));
    }

    [Theory]
    [InlineData("queue")]
    [InlineData("receipt")]
    [InlineData("after")]
    [InlineData("source")]
    [InlineData("ledger")]
    [InlineData("request")]
    public void SupportingRouteRejectsChangedRetainedInputsBeforeAnyMarkerRequest(string change)
    {
        using var f = new Fixture();
        var (transition, _) = CreateSupportingRoute(f);
        var route = OpenRoute(f, transition);
        var path = change switch
        {
            "queue" => transition.QueuePath, "receipt" => transition.ExecutionReceiptPath,
            "after" => transition.AfterSnapshotPath, "source" => f.SourcePath,
            "ledger" => f.LedgerPath, _ => f.RequestPath
        };
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(() => route.RebindRequest);
    }

    private static void ChangeState(SnapshotEnvelope snapshot, string domain, Action<JsonNode> change)
    {
        var value = JsonNode.Parse(snapshot.State[domain].GetRawText())!;
        change(value);
        snapshot.State[domain] = JsonSerializer.SerializeToElement(value);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static void RewriteSupportingRouteAfter(StardewAI.GoalConditionedBootstrap.MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        SnapshotEnvelope after)
    {
        Fixture.Save(transition.AfterSnapshotPath, after);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        receipt.AfterStateHash = after.StateHash;
        receipt.StepResults[^1].AfterStateHash = after.StateHash;
        Fixture.Save(transition.ExecutionReceiptPath, receipt);
    }
}
