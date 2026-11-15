using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransferredRetentionProductSharedSourceRequiresOriginalProductWireWithoutInventingATransfer(bool productWire)
    {
        var f = new SiteTillNativeFixture(productTransport: productWire); using var host = f.Files.F.Runtime;
        await f.Build();
        // These are declared protocol fixtures, not relabelled accepted game evidence.
        var raw = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
        raw["executor_version"] = PolicyTrajectoryVersionPins.ProductExecutor;
        File.WriteAllText(f.ReceiptPath, raw.ToJsonString());
        var transition = f.Transition with { TransitionKind = "shared_source_return", ExecutorVersion = PolicyTrajectoryVersionPins.ProductExecutor };
        CropTeacherSharedSourceReturn Open() => CropTeacherSharedSourceReturn.OpenProductSource(f.Files.F.Inputs.Plan.RequestPath,
            f.Files.F.Source.SourcePath, f.Files.F.Source.LedgerPath, f.Files.F.Inputs.Plan.CoveredCommitmentIds, transition,
            "declared.product.shared.source");
        if (!productWire)
        {
            Assert.Throws<InvalidDataException>(Open); return;
        }
        var scope = Open();
        transition = transition with { NativeSharedSource = new(f.Files.F.Inputs.Plan.RequestPath, f.Files.F.Source.LedgerPath,
            f.Files.F.Inputs.Plan.CoveredCommitmentIds, "declared.product.shared.source", scope.InputHashes) };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedProductTransferYields(transition));
        var verified = MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(transition);
        Assert.Equal(f.After.StateHash, verified.AfterStateHash); Assert.Null(verified.TransferSettlement);
        var load = typeof(MoneyRevenueCropNativeLifecycleProofBuilder).GetMethod("LoadJointObservedTransition", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.NotNull(load.Invoke(null, [transition, true]));
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => load.Invoke(null, [transition, false])).InnerException);
        Assert.False(verified.CropProductionCreditAuthorized); Assert.Equal(0, f.Files.F.Runtime.Handler.Commits);
    }
}
