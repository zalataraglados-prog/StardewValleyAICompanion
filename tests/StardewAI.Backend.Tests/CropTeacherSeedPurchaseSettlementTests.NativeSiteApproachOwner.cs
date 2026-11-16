using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private const string NativeSiteMode = "route_site_approach_native_action_effects";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSiteApproachMainOptsInOnlyForProductAtOriginalAdmission(bool product)
    {
        var f = new OwnedRouteFixture(productExecutor: product, prepareSource: source =>
        {
            ChangeState(source.Before, "player", player =>
            { player["tile_x"]!["value"] = 8; player["tile_y"]!["value"] = 8; });
            ChangeState(source.Before, "current_location", location => location["map"]!["value"]!["id"] = "");
            source.Before.State["options"] = System.Text.Json.JsonSerializer.SerializeToElement(new { });
            source.Before.State["transport"] = System.Text.Json.JsonSerializer.SerializeToElement(new { });
        });
        using var host = f.Runtime;
        var observation = WriteValidSiteMainObservation(f);
        await using var owner = await f.Files.Open();
        using var capture = new SiteApproachCaptureTransport(f, ReadSiteSnapshot(observation));
        using var client = new HttpClient(capture);
        var step = new CropTeacherRunStep(CropTeacherRunStageKinds.PrepareSite,
            f.Source.Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
            Id, "Farm", "Farm", "", "", 0, 0, null, "site-main-fixture");
        await InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", client, f.Runtime.LiveOptions, 1,
            step, f.Inputs.Plan, f.Inputs.Request, owner);
        Assert.Equal(product, NativeSiteStart(owner.Pending!).UsesNativeActionEffects);
        Assert.Equal(product, NativeSiteRecord(owner.Pending!).ContainsKey(NativeSiteMode));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeSiteApproachOwnerPersistsCopiesAndReopensOnlyExplicitMode(bool enabled)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        await using var owner = await f.Files.Open();
        var operation = await owner.CompileAndAdmitOriginalRouteAsync(() => Task.FromResult(
            SiteOwnerCompilation(f, files) with { SiteApproachNativeActionEffects = enabled }));
        var record = NativeSiteRecord(operation);
        Assert.Equal(enabled, record.ContainsKey(NativeSiteMode));
        Assert.Equal(enabled, NativeSiteStart(operation).UsesNativeActionEffects);
        var intent = (JsonObject)typeof(CropTeacherManagedSeedOwner).GetMethod("CreateOriginalRouteIntent",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, new object[] { record })!;
        Assert.Equal(enabled, intent.ContainsKey(NativeSiteMode));
        Assert.True(JsonNode.DeepEquals(record[NativeSiteMode], intent[NativeSiteMode]));
        await owner.DisposeAsync();
        // Current live selection is deliberately opposite; recovery must use the stored mode.
        f.Runtime.LiveOptions.UseProductExecutor = !enabled;
        await using var reopened = await f.Files.Open();
        Assert.Equal(enabled, NativeSiteStart(reopened.Pending!).UsesNativeActionEffects);
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(f.Files.Native, "route-native-claim.json")));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("null")]
    [InlineData("missing_site")]
    [InlineData("control")]
    [InlineData("role")]
    [InlineData("false")]
    public async Task NativeSiteApproachOwnerReadsModeStrictlyAndKeepsExplicitFalseNeutral(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        await using var owner = await f.Files.Open();
        var operation = await AdmitSiteOwner(f, files, owner);
        var record = (JsonObject)NativeSiteRecord(operation).DeepClone();
        record[NativeSiteMode] = change switch
        {
            "string" => JsonValue.Create("true"), "number" => JsonValue.Create(1),
            "null" => null, "false" => JsonValue.Create(false), _ => JsonValue.Create(true)
        };
        if (change == "missing_site")
        {
            record.Remove("route_site_approach_observation_path");
            record.Remove("route_site_approach_commitment_id");
        }
        if (change == "control") record["route_native_control_observation"] = true;
        if (change == "role") record["route_transition"]!["transition_kind"] = "supporting_route_home";
        var open = typeof(CropTeacherManagedSeedOwner).GetMethod("OpenOriginalRouteStart",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (change == "false")
            Assert.False(((CropTeacherSupportingRoute.Preparation)open.Invoke(owner, new object[] { record, true })!).UsesNativeActionEffects);
        else
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
                open.Invoke(owner, new object[] { record, true })).InnerException);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("missing_site")]
    [InlineData("role")]
    [InlineData("control")]
    public async Task NativeSiteApproachOwnerRejectsModeOutsideOriginalSiteAdmission(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        await using var owner = await f.Files.Open();
        var compilation = SiteOwnerCompilation(f, files) with
        {
            SiteApproachNativeActionEffects = true,
            SiteApproachObservationPath = change == "missing_site" ? "" : files.Observation,
            SiteApproachCommitmentId = change == "missing_site" ? "" : Id,
            Kind = change == "role" ? "supporting_route_home" : "supporting_route_to_crop",
            NativeControlObservation = change == "control"
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalRouteAsync(() => Task.FromResult(compilation)));
        Assert.Null(owner.Pending); Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    private static JsonObject NativeSiteRecord(CropTeacherManagedSeedOperation operation) =>
        (JsonObject)typeof(CropTeacherManagedSeedOperation).GetProperty("Record", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operation)!;

    private static CropTeacherSupportingRoute.Preparation NativeSiteStart(CropTeacherManagedSeedOperation operation) =>
        (CropTeacherSupportingRoute.Preparation)typeof(CropTeacherManagedSeedOperation).GetProperty("RouteStart",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(operation)!;
}
