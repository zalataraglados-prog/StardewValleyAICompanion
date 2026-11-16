using System.Reflection;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCapacityPrefixUsesTheRecordedOriginalRuntimeOrProductReader(bool product)
    {
        using var f = new NativeRefillProductFixture(product: product, repair: true, nativeObserved: true);
        await f.Build();
        var executor = product ? PolicyTrajectoryVersionPins.ProductExecutor : PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor;
        var transition = f.Transition(OpenNativeObservedRefill(f), executor);
        var method = typeof(MoneyRevenueCropDailyCapacityProofBuilder).GetMethod("ReadObservedPrefixQueue", BindingFlags.NonPublic | BindingFlags.Static)!;
        var native = method.Invoke(null, [transition])!;
        var actions = Assert.IsAssignableFrom<Array>(native.GetType().GetProperty("Actions")!.GetValue(native));
        Assert.Equal(3, actions.Length);
        var wrong = transition with { ExecutorVersion = product
            ? PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor : PolicyTrajectoryVersionPins.ProductExecutor };
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [wrong])).InnerException);
    }
}
