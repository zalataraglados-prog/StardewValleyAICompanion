using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteTillNativeRetainsOriginalProtocolAndRebindsOnlyToActualAfter()
    {
        var f = new SiteTillNativeFixture(); using var host = f.Files.F.Runtime;
        await f.Build();
        var hashes = f.Preparation.InputHashes.Keys.Concat(f.NativePaths).Distinct()
            .ToDictionary(path => path, SiteTillNativeFixture.FileHash);
        var ledger = JsonSerializer.Serialize(f.Preparation.BaseLedger, JsonOptions);
        var outcome = f.Preparation.VerifyTransition(f.Transition);
        foreach (var pair in hashes)
        {
            Assert.Equal(pair.Value, outcome.InputHashes[pair.Key]);
            Assert.Equal(pair.Value, SiteTillNativeFixture.FileHash(pair.Key));
        }
        Assert.Equal(new(0, 0), f.Preparation.Target);
        Assert.Equal(1, f.Preparation.ToolSlotIndex);
        Assert.True(outcome.Resources.PhysicalInventoryDeltaVerified);
        Assert.True(outcome.Resources.MaterialGraphAndMirrorsVerified);
        Assert.True(outcome.Resources.TillingEnergyTerrainAndStatisticVerified);
        Assert.True(outcome.Resources.UnrelatedPersistentStateVerified);
        Assert.Empty(outcome.Resources.Inventory.Slots);
        Assert.Equal(0, outcome.Resources.Inventory.Evidence.ObservedPickupUnits);
        Assert.False(outcome.NativeCropLifecycleVerified); Assert.False(outcome.FormalProductTrainingAuthorized);
        Assert.False(outcome.Resources.NativeOutcomeVerified); Assert.False(outcome.Resources.ResourceCreditAuthorized);
        Assert.False(outcome.Resources.ProducerProvenanceVerified); Assert.False(outcome.Resources.FormalProductTrainingAuthorized);
        var request = outcome.RebindRequest;
        Assert.Equal(f.After.StateHash, request.StateHash);
        Assert.NotEqual(f.Preparation.SourceStateHash, request.StateHash);
        Assert.Empty(request.MaterialClaims); Assert.Empty(request.CurrencyClaims); Assert.Empty(request.ReleaseReservationIds);
        // Original in-process ledger service proof, not a backend commit/recovery claim.
        var reply = new ReservationPortfolioLedgerService().Commit(outcome.BaseLedger, f.After, request, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var rebound = outcome.VerifyRebindReply(reply);
        Assert.Equal(f.After.StateHash, rebound.SourceStateHash);
        Assert.Equal(f.Preparation.BaseLedger.Revision + 1, rebound.Revision);
        Assert.Equal(ledger, JsonSerializer.Serialize(f.Preparation.BaseLedger, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(f.Preparation.BaseLedger.MaterialReservations, JsonOptions), JsonSerializer.Serialize(rebound.MaterialReservations, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(f.Preparation.BaseLedger.CurrencyReservations, JsonOptions), JsonSerializer.Serialize(rebound.CurrencyReservations, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(f.Preparation.BaseLedger.CropPlantingCommitments, JsonOptions), JsonSerializer.Serialize(rebound.CropPlantingCommitments, JsonOptions));
        Assert.Equal(f.After.StateHash, SnapshotHash.ComputeStateHash(f.After.State));
        outcome.ValidateInputsUnchanged();
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
        foreach (var pair in hashes) Assert.Equal(pair.Value, SiteTillNativeFixture.FileHash(pair.Key));
    }

    [Theory]
    [InlineData("result")]
    [InlineData("journal")]
    [InlineData("gzip")]
    [InlineData("input")]
    [InlineData("source")]
    [InlineData("candidate")]
    [InlineData("target")]
    public async Task SiteTillNativeRejectsTamperingOnlyAfterFullBaselinePasses(string fault)
    {
        var f = new SiteTillNativeFixture(); using var host = f.Files.F.Runtime;
        await f.Build();
        var outcome = f.Preparation.VerifyTransition(f.Transition); // Required valid baseline through every original validator.
        outcome.ValidateInputsUnchanged();
        var transition = f.Transition;
        string? changedPath = null;
        switch (fault)
        {
            case "result": Edit(f.ResultPath, node => node["primitive_verification_status"] = "failed"); break;
            case "journal": Edit(f.JournalPath, node => node["raw_request_sha256"] = new string('0', 64)); break;
            case "gzip":
                changedPath = f.JournalPath + ".gz";
                var bytes = File.ReadAllBytes(changedPath); bytes[0] ^= 0xff; File.WriteAllBytes(changedPath, bytes); break;
            case "input": Edit(f.Files.Narrow, node => node["state_hash"] = "changed"); break;
            case "source": transition = transition with { BeforeSnapshotPath = f.Files.Observation }; break;
            case "candidate": transition = transition with { SelectedCandidateId = "another-candidate" }; break;
            case "target": Edit(f.ResultPath, node => node["target_tile_x"] = 1); break;
        }
        Assert.Throws<InvalidDataException>(() => f.Preparation.VerifyTransition(transition));
        if (changedPath is not null)
        {
            Assert.Throws<InvalidDataException>(() => outcome.ValidateInputsUnchanged());
            Assert.Throws<InvalidDataException>(() => outcome.RebindRequest);
        }
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
        void Edit(string path, Action<JsonNode> change)
        {
            changedPath = path;
            var node = JsonNode.Parse(File.ReadAllText(path))!; change(node); Fixture.Save(path, node);
        }
    }
}
