namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedModeTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("no_hash")]
    [InlineData("no_path")]
    [InlineData("invalid_hash")]
    [InlineData("relative_path")]
    [InlineData("no_joint")]
    [InlineData("no_managed")]
    public void PreparedTerminalProductionRequiresExplicitManagedJointPinnedPair(string fault)
    {
        var path = Path.Combine(Path.GetTempPath(), "published-production.json");
        var args = JointArgs().Concat(new[] { "--prepared-crop-terminal-production", path,
            "--prepared-crop-terminal-production-sha256", new string('a', 64) }).ToList();
        if (fault == "no_joint") args.Remove("--joint-daily-calendar");
        if (fault == "no_managed") args.Remove("--use-managed-crop-teacher");
        if (fault == "relative_path") args[args.IndexOf("--prepared-crop-terminal-production") + 1] = "relative.json";
        if (fault == "invalid_hash") args[^1] = "invalid";
        if (fault == "no_hash") args.RemoveRange(args.IndexOf("--prepared-crop-terminal-production-sha256"), 2);
        if (fault == "no_path") args.RemoveRange(args.IndexOf("--prepared-crop-terminal-production"), 2);
        if (fault != "valid") { Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(args.ToArray())); return; }
        var options = LiveTrainingOptions.Parse(args.ToArray());
        Assert.Equal(path, options.PreparedCropTerminalProduction!.ManifestPath);
        Assert.Equal(new string('a', 64), options.PreparedCropTerminalProduction.ManifestSha256);
        Assert.Null(LiveTrainingOptions.Parse(JointArgs()).PreparedCropTerminalProduction);
    }
}
