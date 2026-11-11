using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task RefillApproachNeutralUnrequestedObservationsRemainOptionalWithOriginalJournalAndOwnerRebind()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true,
            resourceNeutral: true, observeNativeOutputs: false);
        await f.Build();
        Assert.NotEmpty(f.Journals);
        for (int index = 0; index < f.Journals.Count; index++)
        {
            var journal = JsonNode.Parse(File.ReadAllText(f.Journals[index]))!;
            string raw = await ReadNeutralRequestBlob(f.Journals[index]);
            var request = JsonSerializer.Deserialize<TrainingExecutionRequest>(raw, JsonOptions)!;
            var result = JsonNode.Parse(File.ReadAllText(f.Results[index]))!;
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            Assert.Equal(hash, journal["raw_request_sha256"]!.GetValue<string>());
            Assert.Equal(Encoding.UTF8.GetByteCount(raw), journal["request_bytes"]!.GetValue<int>());
            Assert.Equal(request.RequestNonce, journal["request_nonce"]!.GetValue<string>());
            Assert.Equal(request.BeforeStateHash, journal["source_state_hash"]!.GetValue<string>());
            Assert.Equal(request.QueueItemId, result["queue_item_id"]!.GetValue<string>());
            Assert.Equal(request.RequestNonce, result["execution_request_nonce"]!.GetValue<string>());
            Assert.Equal(hash, result["execution_request_sha256"]!.GetValue<string>());
            Assert.Null(request.ActionOriginObservationScopeId);
            Assert.False(request.ActionOriginSnapshotAnchor);
            Assert.False(request.ActionOriginToolInputs);
            Assert.False(request.ActionOriginInventoryPayload);
            Assert.False(request.ActionOriginInventoryTransients);
            Assert.False(request.ActionOriginDebrisState);
            Assert.False(request.ActionOriginInventoryGraph);
            Assert.Null(result["action_origin_observation"]);
        }
        var outcome = VerifyNeutralApproach(f);
        Assert.True(outcome.RefillApproachVerified);
        Assert.False(outcome.RefillControlVerified);
        Assert.Equal(0, outcome.AppliedTileCount);
        Assert.Empty(outcome.AppliedTiles);
        Assert.False(outcome.CompleteBatch);
        Assert.False(outcome.NativeCropLifecycleVerified);
        Assert.False(outcome.FormalProductTrainingAuthorized);
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        Assert.Equal(0, after.State["player"].GetProperty("watering_can_state").GetProperty("value").GetProperty("water_left").GetInt32());
        Assert.Equal(f.Approach.Watering.Before.State["farm"].GetProperty("crops").GetRawText(), after.State["farm"].GetProperty("crops").GetRawText());
        var shared = f.Approach.Watering.Shared;
        var reply = new ReservationPortfolioLedgerService().Commit(shared.Source.Ledger, after, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var ledger = outcome.VerifyRebindReply(reply);
        AssertOtherRootsUnchanged(shared.Source.Ledger, ledger);
        Assert.Equal(shared.Source.Ledger.CropPlantingCommitments.Single().CommitmentId, ledger.CropPlantingCommitments.Single().CommitmentId);
        outcome.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData("foreign-reservation")]
    [InlineData("foreign-marker")]
    [InlineData("rejected-reply")]
    public async Task RefillApproachNeutralOriginalRebindVerifierRejectsForeignOrWrongReply(string change)
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true, resourceNeutral: true);
        await f.Build();
        var outcome = VerifyNeutralApproach(f);
        var shared = f.Approach.Watering.Shared;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.AfterPath), JsonOptions)!;
        var request = outcome.RebindRequest;
        if (change == "foreign-marker") request.PortfolioId += ".foreign";
        var reply = new ReservationPortfolioLedgerService().Commit(shared.Source.Ledger, after, request, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        if (change == "foreign-reservation") reply.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
        if (change == "rejected-reply")
        {
            var raw = JsonSerializer.SerializeToNode(reply, JsonOptions)!;
            raw["accepted"] = false;
            reply = raw.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)!;
        }
        var error = Assert.Throws<InvalidDataException>(() => outcome.VerifyRebindReply(reply));
        Assert.Contains("rebind does not replay the original marker", error.Message);
        outcome.ValidateInputsUnchanged();
    }

    private static CropTeacherWateringMaintenance.Preparation PrepareNeutralApproach(RefillTraceFixture f)
    {
        var shared = f.Approach.Watering.Shared;
        return CropTeacherWateringMaintenance.Preparation.OpenRefillApproach(shared.Inputs.Plan.RequestPath,
            shared.Source.SourcePath, shared.Source.LedgerPath, shared.Inputs.Plan.CoveredCommitmentIds, Id,
            "refill-neutral.peer", f.Approach.ModelPath, f.Approach.QueuePath);
    }

    private static MoneyRevenueCropNativeLifecycleTransitionRequest NeutralTransition(RefillTraceFixture f,
        CropTeacherWateringMaintenance.Preparation preparation)
    {
        var shared = f.Approach.Watering.Shared;
        return new("crop_actions", preparation.TotalDay, f.Approach.QueuePath, shared.Source.SourcePath, f.ReceiptPath,
            f.AfterPath, shared.Runtime.LiveOptions.RunId, shared.Runtime.LiveOptions.PolicyTrajectoryExecutorVersion, preparation.CandidateId);
    }

    private static CropTeacherWateringMaintenance VerifyNeutralApproach(RefillTraceFixture f)
    {
        var preparation = PrepareNeutralApproach(f);
        return preparation.VerifyTransition(NeutralTransition(f, preparation));
    }

    private static async Task<string> ReadNeutralRequestBlob(string journalPath)
    {
        using var file = File.OpenRead(journalPath + ".gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
