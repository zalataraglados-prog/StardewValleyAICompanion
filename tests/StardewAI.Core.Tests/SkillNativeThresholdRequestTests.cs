using System.Text.Json;
using System.Reflection;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class SkillNativeThresholdRequestTests
{
    [Theory]
    [InlineData("{ \"state_hash\": \"same\", \"extra\": 1 }", true)]
    [InlineData("{\"extra\":1,\"state_hash\":\"same\"}", true)]
    [InlineData("{\"state_hash\":\"same\",\"extra\":2}", false)]
    [InlineData("{\"state_hash\":\"same\"}", false)]
    [InlineData("{\"state_hash\":\"other\",\"extra\":1}", false)]
    public void FrameBindingPreservesAllFieldsButNotJsonWhitespaceOrPropertyOrder(string nativeJson, bool matches)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SkillFrame", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var decision = Path.Combine(root, "decision.json");
        var native = Path.Combine(root, "native.json");
        File.WriteAllText(decision, "{\"state_hash\":\"same\",\"extra\":1}");
        File.WriteAllText(native, nativeJson);
        var method = typeof(SkillNativeThresholdReceiptBuilder).GetMethod("SameDecisionSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(matches, method.Invoke(null, new object[] { decision, native }));
    }

    [Theory]
    [InlineData("", 15)]
    [InlineData(" ", 25)]
    [InlineData("run", 14)]
    [InlineData("run", 26)]
    public void InvalidIdentityOrThresholdRejectsBeforeReadingNativeSources(string run, int threshold)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-SkillRequest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "request.json");
        var request = new SkillNativeThresholdRequest("skill_native_threshold_request.v1", run,
            "missing-preference", "missing-queue", "missing-execution", "missing-after", threshold);
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        var error = Assert.Throws<InvalidDataException>(() => SkillNativeThresholdReceiptBuilder.Build(path));
        Assert.Contains("exact scoring threshold", error.Message);
    }
}
