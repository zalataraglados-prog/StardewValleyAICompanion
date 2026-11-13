using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SiteTillProductUsesExplicitBoundReceiptWhileLegacyNativeReaderStillRejectsItsEndpoint()
    {
        var f = new SiteTillNativeFixture(productTransport: true); using var host = f.Files.F.Runtime;
        await f.Build();
        var type = typeof(CropTeacherSiteTilling).Assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!;
        var method = type.GetMethod("ReadQueue", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { f.Files.Queue,
            f.Preparation.SourcePath, f.ReceiptPath, f.AfterPath, f.Transition.RunId, f.Preparation.CandidateId }));
        Assert.Contains("Source is not a native training executor request", Assert.IsType<InvalidDataException>(error.InnerException).Message);
        var outcome = f.Preparation.VerifyTransition(f.Transition);
        Assert.True(outcome.Resources.MaterialGraphAndMirrorsVerified);
        Assert.False(outcome.NativeCropLifecycleVerified); Assert.False(outcome.FormalProductTrainingAuthorized);
        outcome.ValidateInputsUnchanged(); Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("product_executor_schema_version")]
    [InlineData("product_authorization_status")]
    [InlineData("product_receipt_id")]
    [InlineData("product_request_sha256")]
    [InlineData("product_request_before_state_hash")]
    [InlineData("product_before_game_tick")]
    [InlineData("product_request_state_hash_drift")]
    [InlineData("product_dispatch_guard")]
    [InlineData("product_replan_required")]
    [InlineData("product_idempotent_replay")]
    public async Task SiteTillProductRejectsUnboundTransportFieldsAfterValidBaseline(string field)
    {
        var f = new SiteTillNativeFixture(productTransport: true); using var host = f.Files.F.Runtime;
        await f.Build(); _ = f.Preparation.VerifyTransition(f.Transition);
        var raw = JsonNode.Parse(File.ReadAllText(f.ResultPath))!;
        raw[field] = field switch
        {
            "product_before_game_tick" => JsonValue.Create(f.After.GameTick + 1),
            "product_request_state_hash_drift" or "product_idempotent_replay" => JsonValue.Create(true),
            "product_replan_required" => JsonValue.Create(false),
            _ => JsonValue.Create("unbound")
        };
        Fixture.Save(f.ResultPath, raw);
        Assert.Throws<InvalidDataException>(() => f.Preparation.VerifyTransition(f.Transition));
        Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }
}
