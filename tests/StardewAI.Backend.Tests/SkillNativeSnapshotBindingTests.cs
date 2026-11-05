using System.Reflection;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class SkillNativeSnapshotBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeManifestMustMaterializeTheCompleteOriginalDecisionFrame(bool changeExtraField)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SkillManifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var decision = Path.Combine(root, "decision.json");
        var native = Path.Combine(root, "native.json");
        const string original = "{\"schema_version\":\"snapshot.v1\",\"state_hash\":\"same\",\"extra\":1,\"state\":{\"time\":600}}";
        File.WriteAllText(decision, original);
        await ContentAddressedJsonArtifactStore.WriteAsync(native,
            changeExtraField ? original.Replace("\"extra\":1", "\"extra\":2") : original,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        Assert.NotEqual(File.ReadAllText(decision), File.ReadAllText(native));
        var method = typeof(SkillNativeThresholdReceiptBuilder).GetMethod("SameDecisionSnapshot",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(!changeExtraField, method.Invoke(null, new object[] { decision, native }));
    }
}
