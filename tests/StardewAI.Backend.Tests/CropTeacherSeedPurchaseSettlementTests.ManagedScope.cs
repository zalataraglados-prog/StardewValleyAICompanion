using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedScopeUsesTheSameOriginalPurchaseTransactionAndLostReplyRecovery()
    {
        using var f = new Fixture();
        using var runtime = new RuntimeFixture(f, loseReplies: true);
        var original = f.Open();
        var intent = original.AcquisitionIntent;
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.AcquireSeed, original.TotalDay, original.CommitmentId,
            "", "FarmHouse", intent.SeedQualifiedItemId, intent.ShopId, intent.MaxUnitPrice, 1, null, "owned_supply");
        var scopes = CropTeacherManagedSeedScopes.Open(f.RequestPath, f.SourcePath, f.LedgerPath, step, original.MarkerRequest.PortfolioId);
        scopes.ValidateStep(step);
        var transition = f.Transition();
        var prepared = await runtime.Prepare(transition, selectedScope: scopes.Purchase);
        runtime.ObserveAfter();
        var final = await prepared.CompleteAsync(transition);
        Assert.Equal(2, CropTeacherOwnedSeedSupply.Read(f.After, final.Ledger, new[] { Id }).PlayerQuantity(new[] { Id }));
        Assert.Equal(2, runtime.Handler.Commits);
        Assert.Equal(1, runtime.Handler.Settlements);
        Assert.Equal(3, runtime.Handler.Lookups);
        AssertOtherRootsUnchanged(f.Ledger, final.Ledger);
    }
}
