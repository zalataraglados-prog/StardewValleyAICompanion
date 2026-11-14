using System.Text.Json.Nodes;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(true, "clear")]
    [InlineData(false, "clear")]
    [InlineData(true, "access")]
    public async Task SiteClearanceNativeTrustsRealOutcomeWithoutPredictingPrivateEffects(bool product, string frontier)
    {
        var f = new SiteClearanceNativeFixture(product, frontier); using var host = f.Files.F.Runtime;
        await f.Build();
        var outcome = f.Preparation.VerifyTransition(f.Transition);
        Assert.True(outcome.NativeOutcomeVerified); Assert.Equal("native_engine", outcome.OutcomeAuthority);
        Assert.True(outcome.ObservedAfterStateRetained); Assert.False(outcome.PrivateResourceEffectsPredicted);
        Assert.False(outcome.ProducerProvenanceVerified); Assert.False(outcome.ResourceCreditAuthorized);
        Assert.False(outcome.NativeCropLifecycleVerified); Assert.False(outcome.FormalProductTrainingAuthorized);
        Assert.Contains(f.JournalPath + ".gz", outcome.InputHashes.Keys);
        Assert.Equal(f.After.StateHash, outcome.RebindRequest.StateHash);
        Assert.Empty(outcome.RebindRequest.MaterialClaims); Assert.Empty(outcome.RebindRequest.CurrencyClaims);
        var reply = new ReservationPortfolioLedgerService().Commit(outcome.BaseLedger, f.After, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        Assert.Equal(f.After.StateHash, outcome.VerifyRebindReply(reply).SourceStateHash);
        outcome.ValidateInputsUnchanged(); Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("result")]
    [InlineData("target")]
    [InlineData("authorization")]
    [InlineData("after_anchor")]
    [InlineData("journal")]
    public async Task SiteClearanceNativeStillRejectsBrokenExecutionIdentity(string fault)
    {
        var f = new SiteClearanceNativeFixture(); using var host = f.Files.F.Runtime;
        await f.Build(); var valid = f.Preparation.VerifyTransition(f.Transition);
        var transition = f.Transition;
        if (fault == "candidate") transition = transition with { SelectedCandidateId = "foreign" };
        else
        {
            var path = fault == "journal" ? f.JournalPath : f.ResultPath;
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            switch (fault)
            {
                case "result": node["primitive_verification_status"] = "failed"; break;
                case "target": node["target_tile_x"] = 99; break;
                case "authorization": node["product_authorization_status"] = "denied"; break;
                case "after_anchor": node["product_after_game_tick"] = f.After.GameTick + 1; break;
                case "journal": node["raw_request_sha256"] = new string('0', 64); break;
            }
            Fixture.Save(path, node);
            Assert.Throws<InvalidDataException>(valid.ValidateInputsUnchanged);
        }
        Assert.Throws<InvalidDataException>(() => f.Preparation.VerifyTransition(transition));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }
}
