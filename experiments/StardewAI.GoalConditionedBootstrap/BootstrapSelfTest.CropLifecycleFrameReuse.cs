namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCropLifecycleFrameReuse()
    {
        var source = MoneyCropProductionSnapshot();
        var pool = new MoneyRevenueCropNativeLifecycleProofBuilder.LifecycleFramePool();
        Require(ReferenceEquals(source, pool.Share(source)), "Lifecycle pool replaced its first frame.");
        Require(ReferenceEquals(source, pool.Share(CloneSnapshot(source))), "Exact lifecycle aliases were not shared.");
        var changed = CloneSnapshot(source);
        changed.State["player"] = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            money = new { status = "available", value = 1 }
        });
        Require(!ReferenceEquals(source, pool.Share(changed)), "Claimed state hash hid different frame contents.");
        var tick = CloneSnapshot(source);
        tick.GameTick++;
        Require(!ReferenceEquals(source, pool.Share(tick)), "Lifecycle pool dropped native tick identity.");
        var identity = CloneSnapshot(source);
        identity.StateHash += ".other";
        Require(!ReferenceEquals(source, pool.Share(identity)), "Lifecycle pool dropped the envelope state hash.");
        var independent = new MoneyRevenueCropNativeLifecycleProofBuilder.LifecycleFramePool();
        var nextInvocation = CloneSnapshot(source);
        Require(ReferenceEquals(nextInvocation, independent.Share(nextInvocation)), "Lifecycle frames leaked across invocations.");
        Console.WriteLine("PASS: exact lifecycle frame sharing, full envelope identity and invocation isolation.");
    }
}
