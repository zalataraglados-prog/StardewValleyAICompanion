using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Fact]
    public async Task SaleStockLeasesMainKeepsTheFullOriginalQueueAndOnlyClaimsUnreservedMovedUnits()
    {
        using var f = new SelectedSaleStockFixture(transferLease: true);
        await using var owner = await f.OwnerFiles.Open(); await f.RetainDecision(owner);
        var result = await f.PrepareMain(owner);
        var other = result.GetType().GetProperty("OtherGoalPreparation")!.GetValue(result)!;
        var selection = (CropTeacherSharedSourceSelection)other.GetType().GetProperty("SourceSelection")!.GetValue(other)!;
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(selection.QueuePath), JsonOptions)!;
        Assert.Contains(queue.Items, row => row.OptionId == "executor.move_to_tile");
        var ledger = f.Transfer.Runtime.Repository.Get(f.Transfer.Files.Before);
        Assert.True(CropSaleStockTransferLeaseContract.IsBound(ledger));
        Assert.Equal(1, Assert.Single(ledger.PreparedMaterialTransfer!.MaterialRelocations).Quantity);
        Assert.Equal(2, ledger.MaterialReservations.Single(row => CropSaleStockReservationContract.TryRead(row, out _)).Quantity);
        Assert.True(CropSaleStockMaterialTransferBinding.IsBound(Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material")), ledger));
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits); Assert.Equal(0, f.Transfer.Runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SaleStockLeasesUseOriginalNativeSettlementMarkerReleaseAndRecoveryWithoutProductionCredit(bool product, bool loseMarker)
    {
        using var f = new SelectedSaleStockFixture(transferLease: true, product: product, nearChest: true);
        string? markerRoot = null; var lost = false;
        await using var owner = await f.OwnerFiles.Open(writer: async (path, json) =>
        {
            if (loseMarker && !lost && markerRoot is not null && Path.GetDirectoryName(path) == markerRoot && Path.GetFileName(path) == "marker-reply.json")
            { lost = true; throw new IOException("Declared accepted lease-release marker publication loss."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.RetainDecision(owner);
        var receipt = f.OwnerFiles.Native + "-receipt.json"; var after = f.OwnerFiles.Native + "-after.json";
        var operation = await owner.AdmitSelectedSaleStockPreparationAsync(f.Decision, receipt, after, f.OwnerFiles.Native);
        markerRoot = operation.RuntimeDirectory;
        var prepared = await owner.PrepareSharedTransferAsync(operation, f.Transfer.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Transfer.Runtime.Client,
                f.Options, diagnostic, request),
            (ledger, root) => Task.FromResult(PrepareLeaseTerminalSelection(f, ledger, root)));
        f.Transfer.PreparationReference = prepared.MaterialTransferPreparation!;
        f.Transfer.Transition = f.Transfer.NativeTemplate! with { TransitionKind = "shared_source_return",
            QueuePath = prepared.QueuePath, ExecutionReceiptPath = receipt, AfterSnapshotPath = after,
            SelectedCandidateId = f.Candidate.CandidateId, ExecutorVersion = f.Options.PolicyTrajectoryExecutorVersion };
        f.Transfer.NativeFacts = () => [new() { Path = "player.inventory.(O)330", Before = "0", After = "1" }];
        var calls = 0;
        Task<JsonObject> Execute() => InvokeLive<Task<JsonObject>>("ExecuteCropTeacherSharedOperationAsync", f.Transfer.Runtime.Client,
            f.Options, owner, operation, prepared.QueuePath, (Func<Task<JsonObject>>)(async () =>
            {
                calls++;
                await f.Transfer.Build(product: product, includePreparation: true, settle: false, alreadyPrepared: true);
                return new() { ["execution_path"] = receipt, ["after_snapshot_path"] = after };
            }));
        if (loseMarker) await Assert.ThrowsAsync<IOException>(Execute); else await Execute();
        Assert.Equal(1, calls); Assert.Equal(2, f.Transfer.Runtime.Handler.Commits); Assert.Equal(1, f.Transfer.Runtime.Handler.Settlements);
        await owner.DisposeAsync();
        await using var reopened = await f.OwnerFiles.Open();
        if (loseMarker) await reopened.RecoverPendingAsync(f.Transfer.Runtime.Client, f.OwnerFiles.Recovery, f.OwnerFiles.Post);
        else await reopened.RestoreHeadAsync(f.Transfer.Runtime.Client, f.OwnerFiles.Recovery, f.OwnerFiles.Post);
        Assert.Null(reopened.Pending); Assert.Equal(after, reopened.CurrentSnapshotPath);
        Assert.Equal(1, calls); Assert.Equal(2, f.Transfer.Runtime.Handler.Commits); Assert.Equal(1, f.Transfer.Runtime.Handler.Settlements);
        var source = Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        var transition = source.NativeTransition with { NativeSharedSource = source.SourceReference };
        Assert.NotNull(transition.NativeSharedSource!.AcceptedMarker);
        var verified = MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(transition);
        Assert.True(verified.ReleasesTransferLease); Assert.NotNull(verified.CompletedLedger);
        var leases = verified.RebindRequest.ReleaseReservationIds;
        Assert.Single(leases);
        Assert.All(leases, id => Assert.Equal(StrategyCommitmentStatuses.Cancelled,
            verified.CompletedLedger!.MaterialReservations.Single(row => row.ReservationId == id).Status));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedTransferYields(transition));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(
            transition with { NativeSharedSource = transition.NativeSharedSource with { AcceptedMarker = null } }));
        ObserveLeaseSourceAndLaterFreeConsumption(f.Transfer, transition);
        if (product && !loseMarker) AssertLeaseMarkerRejectsForeignReleaseAndFalseReadback(f.Transfer, transition);
        var marker = transition.NativeSharedSource.AcceptedMarker!;
        File.AppendAllText(marker.ReplyPath, "\n");
        Assert.Throws<InvalidDataException>(verified.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(transition));
    }

    private static void AssertLeaseMarkerRejectsForeignReleaseAndFalseReadback(SharedTransferReturnFixture f,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var reference = transition.NativeSharedSource!; var marker = reference.AcceptedMarker!;
        CropTeacherSharedSourceReturn Fresh() => CropTeacherSharedSourceReturn.OpenAfterPreparedInventoryLeaseYield(
            reference.RunRequestPath, transition.BeforeSnapshotPath, reference.LedgerPath, reference.CoveredCommitmentIds,
            transition, reference.StepId, reference.SettledMaterialTransfer!, reference.SettledTransferReferencePath!, product: true);
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenAfterPreparedProductTransferYield(
            reference.RunRequestPath, transition.BeforeSnapshotPath, reference.LedgerPath, reference.CoveredCommitmentIds,
            transition, reference.StepId, reference.SettledMaterialTransfer!, reference.SettledTransferReferencePath!));
        var root = Path.Combine(f.Files.RuntimePath, "bad-marker-projections"); Directory.CreateDirectory(root);
        var request = JsonSerializer.Deserialize<ReservationPortfolioCommitRequest>(File.ReadAllText(marker.RequestPath), JsonOptions)!;
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(marker.AfterLedgerPath), JsonOptions)!;
        request.ReleaseReservationIds = [ledger.MaterialReservations.Single(row => CropSaleStockReservationContract.TryRead(row, out _)).ReservationId];
        var json = JsonSerializer.Serialize(request, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var stem = Path.Combine(root, "portfolio-mutation-" + hash);
        File.WriteAllText(stem + "-rpc-request.json", json);
        Fixture.Save(stem + "-rpc-journal.json", new { schema_version = "stardewai.portfolio_rpc_journal.v1",
            backend_url = marker.BackendUrl, operation = "commit", request_file = Path.GetFileName(stem + "-rpc-request.json"), request_sha256 = hash });
        Assert.Throws<InvalidDataException>(() => Fresh().BindAcceptedMarker(marker with
            { RequestPath = stem + "-rpc-request.json", JournalPath = stem + "-rpc-journal.json" }));
        var reply = JsonSerializer.Deserialize<ReservationPortfolioCommitResult>(File.ReadAllText(marker.ReplyPath), JsonOptions)!;
        reply.Ledger!.MaterialReservations.Single(row => CropSaleStockReservationContract.TryRead(row, out _)).Quantity++;
        var replyPath = Path.Combine(root, "bad-reply.json"); Fixture.Save(replyPath, reply);
        Assert.Throws<InvalidDataException>(() => Fresh().BindAcceptedMarker(marker with { ReplyPath = replyPath }));
        ledger.MaterialReservations.Single(row => row.Status == StrategyCommitmentStatuses.Cancelled).Status = StrategyCommitmentStatuses.Active;
        var readback = Path.Combine(root, "bad-readback.json"); Fixture.Save(readback, ledger);
        Assert.Throws<InvalidDataException>(() => Fresh().BindAcceptedMarker(marker with { AfterLedgerPath = readback }));
        Assert.Throws<InvalidDataException>(() => Fresh().BindAcceptedMarker(marker with { AfterLedgerPath = marker.ReplyPath }));
        Assert.Throws<InvalidDataException>(() => Fresh().BindAcceptedMarker(marker with { BackendUrl = "http://wrong-backend" }));
    }

    private static CropTeacherSharedSourceSelection PrepareLeaseTerminalSelection(SelectedSaleStockFixture f,
        StrategyCommitmentLedger ledger, string root)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(f.Transfer.Files.Before,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item", Parameters = CropSaleStockMaterialTransferBinding.ToParameters(ledger) }],
            commitmentLedger: ledger);
        Assert.True(Assert.Single(availability.Options).Available, string.Join(",", availability.Options.SelectMany(row => row.BlockingReasons)));
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(rows, f.Transfer.Files.Before.StateHash, goalId: "earn_money",
            maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
        // Declared terminal protocol slice at the already reached chest; Main's separate test keeps its full queue.
        plan.Steps = plan.Steps.Where(row => row.Kind == "transfer_material").ToArray();
        var queue = new ActionQueueCompiler().Compile(plan, f.Transfer.Files.Before, ledger);
        Assert.Single(queue.Items); Assert.Equal("pending", queue.Status);
        Directory.CreateDirectory(root);
        string Save(string name, object value) { var path = Path.Combine(root, name + ".json"); Fixture.Save(path, value); return path; }
        var model = Save("lease-model", plan); var path = Save("lease-queue", queue);
        var evidence = Save("lease-refresh", new { schema_version = "selected_queue_candidate_refresh.v1", state_hash = plan.StateHash,
            goal_id = plan.GoalId, policy_model_invoked = false, selection_locked = true, selected_queue_index = f.Candidate.QueueIndex,
            selected_candidate_id = f.Candidate.CandidateId, selected_option_id = f.Candidate.OptionId, availability });
        f.Transfer.NativeTemplate = f.Transfer.Files.NativeTransition(null!, snapshot =>
        {
            var projected = ReservationPortfolioLedgerTests.ProjectPartitionAfter(f.Transfer.Files.Before, ledger.PreparedMaterialTransfer!);
            var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!.AsObject();
            farm["material_inventory_graph"] = JsonNode.Parse(projected.State["farm"].GetProperty("material_inventory_graph").GetRawText());
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
            Fixture.ApplyNativeEchoes(farm, player, snapshot.GameTick);
            snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        }, compiledQueue: queue);
        return new(f.Decision.SnapshotPath, f.Decision.PlanPath, f.Decision.RankingPath, model, path, evidence,
            f.Candidate.QueueIndex, f.Decision.OwnedCandidateIds) { DecisionReference = f.Decision.DecisionReference };
    }

    private static void ObserveLeaseSourceAndLaterFreeConsumption(SharedTransferReturnFixture f,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var account = Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount), flags, null, [f.Files.Before, "(O)745"], null)!;
        var type = typeof(CropTeacherSaleStockInventory).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropTeacherTransferredSaleStock")!;
        var stock = Activator.CreateInstance(type, flags | BindingFlags.Public, null, [Id, "declared-no-harvest", "(O)745"], null)!;
        Assert.Equal(1, type.GetMethod("ObserveShared", flags)!.Invoke(stock, [account, transition]));
        var next = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Files.After, JsonOptions), JsonOptions)!; next.GameTick++;
        MaterialInventoryGraphReader.TryRead(next, out var graph);
        graph!.InventoryNodes.Single(row => row.NodeId == "player:123").Slots = graph.InventoryNodes.Single(row => row.NodeId == "player:123")
            .Slots.Where(row => row.QualifiedItemId != "(O)330").ToArray();
        var farm = JsonNode.Parse(next.State["farm"].GetRawText())!.AsObject();
        farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph, JsonOptions);
        var player = JsonNode.Parse(next.State["player"].GetRawText())!.AsObject(); Fixture.ApplyNativeEchoes(farm, player, next.GameTick);
        next.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions); next.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        next.StateHash = SnapshotHash.ComputeStateHash(next.State);
        type.GetMethod("Observe", flags)!.Invoke(stock, [account, f.Files.After, next, false]);
        Assert.Equal(0, typeof(MoneyRevenueCropRetentionAccount).GetProperty("Quantity", flags)!.GetValue(account));
    }
}
