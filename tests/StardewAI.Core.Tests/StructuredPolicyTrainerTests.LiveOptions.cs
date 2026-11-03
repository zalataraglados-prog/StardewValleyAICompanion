namespace StardewAI.Core.Tests;

public sealed partial class StructuredPolicyTrainerTests
{
    [Fact]
    public void LiveOptionsRequireCheckpointWhenStructuredPolicyIsMandatory()
    {
        Assert.Throws<ArgumentException>(() =>
            LiveTrainingOptions.Parse(new[] { "--require-structured-policy" }));
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--require-structured-policy",
            "--policy-checkpoint-path", @"E:\model.json",
            "--policy-initialization-checkpoint-path", @"E:\grandpa-21.json"
        });
        Assert.True(options.RequireStructuredPolicy);
        Assert.Equal(@"E:\model.json", options.PolicyCheckpointPath);
        Assert.Equal(@"E:\grandpa-21.json", options.PolicyInitializationCheckpointPath);
    }
}
