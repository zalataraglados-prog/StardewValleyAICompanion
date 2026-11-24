using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagedPlannerInputsConsumeAcceptedNightBeforeAndAfterOriginalNativeReadback(bool load)
    {
        using var f = new SavedReadbackOwnerFixture(prepareSource: DeclarePlannerFarmObjectView);
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner);
        if (load) await f.Run(owner, await f.Admit(owner));
        var before = f.Night.Shared.Runtime.Handler.Commits;
        var snapshot = ReadSiteSnapshot(owner.CurrentSnapshotPath);
        var missing = CropTeacherRunPlanner.Evaluate(f.Night.Shared.Inputs.Plan, f.Night.Shared.Inputs.Request, snapshot);
        Assert.Equal("crop_teacher_run_day_advanced_without_verified_save_boundary", missing.Reason);
        var actual = EvaluateManagedPlannerInputs(f.Night, owner);
        Assert.NotEqual(CropTeacherRunStageKinds.ReplanRequired, actual.Stage);
        Assert.Equal(before, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.Night.NativeNightCalls);
        Assert.Equal(load ? f.LoadedPath : f.Night.NightAfter, owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task ManagedPlannerInputsConsumeAcceptedHarvestInsteadOfReharvestingMissingCrops()
    {
        using var f = new OvernightFixture(prepareSource: source => DeclarePlannerFarmObjectView(source.Before));
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var harvest = await f.PrepareHarvest(owner);
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        var before = f.Shared.Runtime.Handler.Commits;
        var actual = EvaluateManagedPlannerInputs(f, owner);
        Assert.NotEqual(CropTeacherRunStageKinds.ReplanRequired, actual.Stage);
        Assert.Equal(before, f.Shared.Runtime.Handler.Commits);
        Assert.Equal(1, f.NativeHarvestCalls);
        Assert.Equal(f.HarvestAfter, owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task ManagedPlannerInputsDoNotInferAcceptedNightFromCurrentDayAlone()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        var snapshot = ReadSiteSnapshot(owner.CurrentSnapshotPath);
        var day = snapshot.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
        ChangeState(snapshot, "time", time => time["total_days"]!["value"] = day + 1);
        var actual = InvokeLive<CropTeacherRunStep>("EvaluateCropTeacherCurrentRunStep", f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, snapshot, new HashSet<string>(StringComparer.Ordinal), null!, owner);
        Assert.Equal("crop_teacher_run_day_advanced_without_verified_save_boundary", actual.Reason);
        Assert.Equal(0, f.NativeNightCalls);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public void ManagedPlannerInputsKeepOriginalNoOwnerDefaultUnchanged()
    {
        using var f = new OvernightFixture();
        var snapshot = ReadSiteSnapshot(f.Options.InitialSnapshotPath);
        var expected = CropTeacherRunPlanner.Evaluate(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, snapshot);
        var actual = InvokeLive<CropTeacherRunStep>("EvaluateCropTeacherCurrentRunStep", f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, snapshot, new HashSet<string>(StringComparer.Ordinal), null!, null!);
        Assert.Equal(JsonSerializer.Serialize(expected, JsonOptions), JsonSerializer.Serialize(actual, JsonOptions));
    }

    private static CropTeacherRunStep EvaluateManagedPlannerInputs(OvernightFixture f, CropTeacherManagedSeedOwner owner) =>
        InvokeLive<CropTeacherRunStep>("EvaluateCropTeacherCurrentRunStep", f.Shared.Inputs.Plan, f.Shared.Inputs.Request,
            ReadSiteSnapshot(owner.CurrentSnapshotPath), new HashSet<string>(StringComparer.Ordinal), null!, owner);

    // These sequencing cases explicitly model a farm without spawned debris.
    // An unavailable farm.objects field must not be treated as an empty view by
    // the production crop-protection path.
    private static void DeclarePlannerFarmObjectView(SnapshotEnvelope snapshot) =>
        ChangeState(snapshot, "farm", farm => farm["objects"] =
            JsonSerializer.SerializeToNode(new { status = "available", value = Array.Empty<object>() }));
}
