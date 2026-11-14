using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedForeignYieldHoldsTheOwnerBeforeNativeAndReturnsThroughOriginalBookkeeping(bool product)
    {
        var f = new SharedYieldFixture(product);
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        Assert.True(operation.IsSharedSourceReturn);
        Assert.Same(operation, owner.Pending);
        Assert.False(File.Exists(f.Native.ReceiptPath));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Admit(owner));
        var claim = await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        Assert.Equal(JsonSerializer.Serialize(f.Selection, JsonOptions), JsonSerializer.Serialize(claim.Selection, JsonOptions));
        Assert.Equal(f.Native.Preparation.CandidateId, claim.Transition.SelectedCandidateId);
        Assert.Equal(f.Native.Files.F.Source.SourcePath, owner.CurrentSnapshotPath);
        Assert.False(File.Exists(f.Native.ReceiptPath));
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client));
        await f.Native.Build(); // Declared synthetic native journal; the owner never invokes it.
        f.Native.Files.F.Runtime.Store.Snapshots[f.Native.After.StateHash] = f.Native.After;
        await owner.VerifySharedSourceExecutionAsync(operation);
        await RunObservedSharedReturn(f.Native, owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(f.Native.AfterPath, owner.CurrentSnapshotPath);
        var trace = await owner.ReadJointProductionTraceAsync();
        Assert.Empty(trace.CropTrace.Transitions);
        Assert.Single(trace.SharedSourceReturns);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        await reopened.RestoreHeadAsync(f.Native.Files.F.Runtime.Client, f.Native.Files.F.Files.Recovery, f.Native.Files.F.Files.Post);
        Assert.Equal(f.Native.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedForeignYieldReopenDoesNotRedispatchAnUnknownNativeOutcome(bool emitOutcome)
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        var pending = reopened.Pending!;
        Assert.True(pending.IsSharedSourceReturn);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Native.Files.F.Runtime.Client));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.RecoverPendingAsync(f.Native.Files.F.Runtime.Client,
            f.Native.Files.F.Files.Recovery, f.Native.Files.F.Files.Post));
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        if (!emitOutcome)
        {
            await Assert.ThrowsAnyAsync<IOException>(() => reopened.VerifySharedSourceExecutionAsync(pending));
            Assert.Equal(f.Native.Files.F.Source.SourcePath, reopened.CurrentSnapshotPath);
            return;
        }
        await f.Native.Build();
        f.Native.Files.F.Runtime.Store.Snapshots[f.Native.After.StateHash] = f.Native.After;
        // Finish from original journal/output files without claiming or invoking native execution again.
        await reopened.VerifySharedSourceExecutionAsync(pending);
        await RunObservedSharedReturn(f.Native, reopened, pending);
        Assert.Null(reopened.Pending);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task UnclaimedSelectedForeignYieldCanBeClaimedOnceAfterReopen()
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        await f.Admit(owner);
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        var pending = reopened.Pending!;
        var claim = await reopened.ClaimSharedSourceExecutionAsync(pending, f.Native.Files.F.Runtime.Client);
        Assert.Equal(pending.Sequence, claim.OwnerSequence);
        Assert.False(File.Exists(f.Native.ReceiptPath));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.ClaimSharedSourceExecutionAsync(pending, f.Native.Files.F.Runtime.Client));
    }

    [Fact]
    public async Task SelectedForeignYieldRecoversALostMarkerReplyWithoutAnotherNativeClaimOrMutation()
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        var failed = false;
        await using var owner = await f.Native.Files.F.Files.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == "marker-reply.json")
            {
                failed = true;
                throw new IOException("Synthetic yielded-source marker reply lost.");
            }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var operation = await f.Admit(owner);
        await owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client);
        await f.Native.Build();
        f.Native.Files.F.Runtime.Store.Snapshots[f.Native.After.StateHash] = f.Native.After;
        await Assert.ThrowsAsync<IOException>(() => RunObservedSharedReturn(f.Native, owner, operation));
        Assert.True(failed);
        await owner.DisposeAsync();
        await using var reopened = await f.Native.Files.F.Files.Open();
        await reopened.RecoverPendingAsync(f.Native.Files.F.Runtime.Client, f.Native.Files.F.Files.Recovery, f.Native.Files.F.Files.Post);
        Assert.Null(reopened.Pending);
        Assert.Equal(f.Native.AfterPath, reopened.CurrentSnapshotPath);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Commits);
        Assert.Equal(1, f.Native.Files.F.Runtime.Handler.Lookups);
    }

    [Theory]
    [InlineData("selected_source")]
    [InlineData("selected_candidate")]
    [InlineData("owned_candidate")]
    [InlineData("compiled_queue")]
    [InlineData("blocked_queue")]
    [InlineData("output_alias")]
    public async Task SelectedForeignYieldRejectsMisboundPreparationBeforeAnyNativeClaim(string change)
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        if (change == "owned_candidate") f.Selection = f.Selection with { OwnedCandidateIds = [f.Native.Preparation.CandidateId] };
        if (change is "selected_source" or "selected_candidate")
        {
            var evidence = JsonNode.Parse(File.ReadAllText(f.Selection.EvidencePath))!;
            evidence[change == "selected_source" ? "state_hash" : "selected_candidate_id"] = "different-original-identity";
            Fixture.Save(f.Selection.EvidencePath, evidence);
        }
        if (change is "compiled_queue" or "blocked_queue")
        {
            var queue = JsonNode.Parse(File.ReadAllText(f.Selection.QueuePath))!;
            if (change == "compiled_queue") queue["goal_id"] = "different-compiled-goal";
            else queue["status"] = "blocked";
            Fixture.Save(f.Selection.QueuePath, queue);
        }
        await Assert.ThrowsAnyAsync<Exception>(() => owner.AdmitSharedSourceYieldAsync(f.Selection,
            change == "output_alias" ? f.Selection.QueuePath : f.Native.ReceiptPath,
            f.Native.AfterPath, f.Native.Files.F.Files.Native));
        Assert.Null(owner.Pending);
        Assert.False(Directory.Exists(f.Native.Files.F.Files.Native));
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task SelectedForeignYieldDetectsAChangedDecisionBeforeNativeClaim()
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        await using var owner = await f.Native.Files.F.Files.Open();
        var operation = await f.Admit(owner);
        File.AppendAllText(f.Selection.DecisionPlanPath, "\n");
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ClaimSharedSourceExecutionAsync(operation, f.Native.Files.F.Runtime.Client));
        Assert.False(Directory.Exists(operation.RuntimeDirectory));
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
    }

    private sealed class SharedYieldFixture
    {
        internal SiteTillNativeFixture Native { get; }
        internal CropTeacherSharedSourceSelection Selection { get; set; }
        internal SharedYieldFixture(bool product = false)
        {
            Native = new(productTransport: product);
            var source = Native.Files.F.Source;
            var action = JsonSerializer.Deserialize<SmallModelActionEnvelope>(File.ReadAllText(Native.Files.Full), JsonOptions)!.Actions.Single();
            // Synthetic availability, but real ranker, daily compiler and action compiler. No game or policy call.
            var availability = new OptionAvailabilityEnvelope
            {
                StateHash = source.Before.StateHash,
                Options = [new OptionAvailability { OptionId = "executor.till_soil", Available = true, ReadEligible = true,
                    EventCandidates = [new EventCandidate { CandidateId = Native.Preparation.CandidateId, Kind = "till_soil_tile",
                        Available = true, LocationId = "Farm", TileX = 0, TileY = 0, EstimatedTicks = 60,
                        EnergyCost = 2, Parameters = action.Parameters }] }]
            };
            var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "daily.closed_loop");
            var plan = new DailyPlanCompiler().Compile(rows, source.Before.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
            Assert.NotEmpty(plan.Steps);
            var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
            var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(plan, ranking).Candidates);
            var queue = new ActionQueueCompiler().Compile(plan, source.Before, source.Ledger);
            Assert.Equal("pending", queue.Status);
            Assert.Single(queue.Items);
            var evidence = new JsonObject
            {
                ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = source.Before.StateHash,
                ["goal_id"] = plan.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
                ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
                ["selected_option_id"] = selected.OptionId, ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
            };
            string Save<T>(string name, T value)
            {
                var path = Path.Combine(source.RuntimePath, name); Fixture.Save(path, value); return path;
            }
            Selection = new(source.SourcePath, Save("yield-decision-plan.json", plan), Save("yield-decision-ranking.json", ranking),
                Save("yield-fresh-plan.json", plan), Native.Files.Queue, Save("yield-refresh.json", evidence), selected.QueueIndex, []);
            Fixture.Save(Native.Files.Queue, queue);
        }

        internal Task<CropTeacherManagedSeedOperation> Admit(CropTeacherManagedSeedOwner owner) =>
            owner.AdmitSharedSourceYieldAsync(Selection, Native.ReceiptPath, Native.AfterPath, Native.Files.F.Files.Native);
    }
}
