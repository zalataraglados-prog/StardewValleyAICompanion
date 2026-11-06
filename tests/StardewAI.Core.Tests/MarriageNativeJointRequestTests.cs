using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MarriageNativeJointRequestTests
{
    [Theory]
    [InlineData("run", 0)]
    [InlineData("run", 2)]
    [InlineData("run", 65)]
    [InlineData("", 3)]
    [InlineData(" ", 3)]
    [InlineData("null_steps", 3)]
    [InlineData("null_step", 3)]
    [InlineData("no_preference", 3)]
    [InlineData("blank_preference", 3)]
    [InlineData("missing_before", 3)]
    [InlineData("missing_final", 3)]
    public void IncompleteRequestsCannotOpenOrCreditNativeSources(string mode, int count)
    {
        var path = Path.Combine(Path.GetTempPath(), "StardewAI-MarriageJoint", Guid.NewGuid().ToString("N"), "request.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var steps = Enumerable.Range(0, count).Select(_ => new MarriageNativeJointStep("missing", "missing", "missing", "missing", "preference")).ToArray();
        if (mode == "null_step") steps[1] = null!;
        if (mode == "no_preference") steps[0] = steps[0] with { PreferencePath = null };
        if (mode == "blank_preference") steps[0] = steps[0] with { PreferencePath = " " };
        if (mode == "missing_before") steps[1] = steps[1] with { BeforeSnapshotPath = "" };
        var request = new MarriageNativeJointRequest("marriage_native_joint_request.v1", mode, mode == "null_steps" ? null! : steps,
            mode == "missing_final" ? "" : "missing");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        var error = Assert.Throws<InvalidDataException>(() => MarriageNativeJointReceiptBuilder.Build(path));
        Assert.Contains("complete bounded steps", error.Message);
    }
}
