using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class FriendshipNativeThresholdRequestTests
{
    [Theory]
    [InlineData("", 5)]
    [InlineData(" ", 10)]
    [InlineData("run", 4)]
    [InlineData("run", 9)]
    [InlineData("run", 11)]
    public void InvalidIdentityOrThresholdCannotReadOrCreditNativeSources(string run, int target)
    {
        var root = Path.Combine(Path.GetTempPath(), "StardewAI-FriendshipRequest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new FriendshipNativeThresholdRequest(
            "friendship_native_threshold_request.v1", run, "missing-preference", "missing-queue", "missing-receipt", "missing-after", target)));
        var error = Assert.Throws<InvalidDataException>(() => FriendshipNativeThresholdReceiptBuilder.Build(path));
        Assert.Contains("exact five/ten count threshold", error.Message);
    }
}
