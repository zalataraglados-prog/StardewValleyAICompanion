using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // One immutable declared boundary; negative cases clone only its descriptor, not its full trajectory.
    private static readonly Lazy<Task<(GlobalContinuationFixture Fixture, JsonObject Boundary)>> GlobalSaveBoundaryGolden = new(async () =>
    {
        var fixture = new GlobalContinuationFixture();
        await fixture.Initialize(); await fixture.CompleteNight();
        return (fixture, await fixture.ReadBoundary());
    });

    [Theory]
    [InlineData("version")]
    [InlineData("not_ready")]
    [InlineData("not_saved")]
    [InlineData("run")]
    [InlineData("player")]
    [InlineData("day")]
    [InlineData("state")]
    [InlineData("acceptance_hash")]
    [InlineData("save_hash")]
    [InlineData("save_path_alias_with_bad_hash")]
    [InlineData("save_pin_missing")]
    [InlineData("slot")]
    public async Task ManagedGlobalSaveConsumerRejectsUnboundEvidenceWithoutDispatchOrNewSave(string fault)
    {
        var golden = await GlobalSaveBoundaryGolden.Value;
        var f = golden.Fixture;
        var boundary = golden.Boundary.DeepClone().AsObject();
        var options = new LiveTrainingOptions { RunId = f.Options.RunId, SaveIsolationPath = f.Options.SaveIsolationPath, SaveSlot = f.Options.SaveSlot };
        var commits = f.F.Shared.Runtime.Handler.Commits;
        if (fault == "version") boundary["schema_version"] = "unknown";
        if (fault == "not_ready") boundary["operational_handoff_ready"] = false;
        if (fault == "not_saved") boundary["native_save_boundary_verified"] = false;
        if (fault == "run") boundary["run_id"] = "other-run";
        if (fault == "player") boundary["player_id"] = "other-player";
        if (fault == "day") boundary["to_total_day"] = boundary["to_total_day"]!.GetValue<int>() + 1;
        if (fault == "state") boundary["after_state_hash"] = new string('0', 64);
        if (fault == "acceptance_hash") boundary["accepted_operation_sha256"] = new string('0', 64);
        var savePath = boundary["overnight_save_boundary_path"]!.GetValue<string>();
        if (fault == "save_hash") boundary["input_artifact_hashes"]![savePath] = new string('0', 64);
        if (fault == "save_path_alias_with_bad_hash")
        {
            boundary["input_artifact_hashes"]![savePath] = new string('0', 64);
            boundary["overnight_save_boundary_path"] = savePath.Replace('\\', '/');
        }
        if (fault == "save_pin_missing") boundary["input_artifact_hashes"]!.AsObject().Remove(savePath);
        if (fault == "slot") options.SaveSlot += "-other";
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<object>("ReadManagedGlobalSaveBoundary", options, "not-written.json", boundary));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(commits, f.F.Shared.Runtime.Handler.Commits);
        Assert.Null(f.Owner.Pending);
        Assert.False(boundary["terminal_settlement_verified"]!.GetValue<bool>());
        Assert.False(boundary["semantic_saved_content_verified"]!.GetValue<bool>());
        Assert.False(boundary["formal_product_training_authorized"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ManagedGlobalSaveConsumerUsesOriginalWindowsPathEquivalenceWithoutDroppingHashPins()
    {
        var golden = await GlobalSaveBoundaryGolden.Value;
        var boundary = golden.Boundary.DeepClone().AsObject();
        foreach (var name in new[] { "overnight_save_boundary_path", "accepted_snapshot_path", "accepted_operation_path" })
        {
            var path = boundary[name]!.GetValue<string>().Replace('\\', '/');
            boundary[name] = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
        }
        var result = InvokeLive<object>("ReadManagedGlobalSaveBoundary", golden.Fixture.Options, "not-written.json", boundary);
        var observation = Assert.IsType<NativeSaveBoundaryObservation>(result.GetType().GetProperty("SaveBoundary")!.GetValue(result));
        Assert.True(observation.Verified);
        Assert.Equal(golden.Boundary["after_state_hash"]!.GetValue<string>(), result.GetType().GetProperty("AfterStateHash")!.GetValue(result));
    }
}
