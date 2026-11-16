using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherRuntimeInstallationTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ManagedStartUsesActualInstalledLedgerAndRejectsMissingSourceBeforeMutation(bool loseReplies, bool completeSource)
    {
        using var files = new Files();
        using var fixture = new MoneyCropTeacherFixture("474", 3);
        var farm = JsonNode.Parse(fixture.SourceSnapshot.State["farm"].GetRawText())!;
        var node = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!;
        var location = fixture.SourceSnapshot.State["player"].GetProperty("location_id").GetProperty("value").GetString()!;
        if (completeSource)
        {
            node["inventory_kind"] = "player_inventory"; node["supply_state"] = "available";
            node["owner_player_id"] = long.Parse(fixture.SourceSnapshot.PlayerId.Value!, System.Globalization.CultureInfo.InvariantCulture);
            node["actor_use_authorized"] = true; node["location_id"] = location;
            foreach (var slot in node["slots"]!.AsArray().OfType<JsonObject>()) slot["quality"] = 0;
        }
        fixture.SourceSnapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        fixture.ChangePlayer(player =>
        {
            player["location_id"]!["value"] = location;
            if (completeSource)
                foreach (var slot in player["inventory"]!["value"]!.AsArray().OfType<JsonObject>()) slot["quality"] = 0;
        });
        var layout = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(fixture.LayoutPath), JsonOptions)!;
        var layoutFarm = JsonNode.Parse(layout.State["farm"].GetRawText())!;
        layoutFarm["material_inventory_graph"] = farm["material_inventory_graph"]!.DeepClone();
        layout.State["farm"] = JsonSerializer.SerializeToElement(layoutFarm);
        var layoutPlayer = JsonNode.Parse(layout.State["player"].GetRawText())!;
        layoutPlayer["inventory"] = JsonNode.Parse(fixture.SourceSnapshot.State["player"].GetRawText())!["inventory"]!.DeepClone();
        layout.State["player"] = JsonSerializer.SerializeToElement(layoutPlayer);
        layout.StateHash = SnapshotHash.ComputeStateHash(layout.State);
        File.WriteAllText(fixture.LayoutPath, JsonSerializer.Serialize(layout, JsonOptions));
        var decision = Prepare(fixture, files);
        var original = CropTeacherRuntimeInstallation.Open(decision);
        var basePath = Path.Combine(files.Root, "managed-base-ledger.json");
        File.WriteAllText(basePath, JsonSerializer.Serialize(original.BaseLedger, JsonOptions));
        var capture = MoneyCropTeacherCaptureFixture.Save(fixture, Path.Combine(files.Root, "capture.json"));
        var manifest = MoneyRevenueCropTeacherDispatchBuilder.Build(capture, Path.Combine(files.Root, "dispatch"),
            "managed-bootstrap", "2026-11-12T02:00:00Z", baseStrategyLedgerPath: basePath);
        var plan = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlan>(File.ReadAllText(manifest.NativeRunPlanPath), JsonOptions)!;
        var request = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlanRequest>(File.ReadAllText(manifest.NativeRunPlanRequestPath), JsonOptions)!;
        var ledgerPath = Path.Combine(files.Root, "dispatch", "crop-teacher-transition-ledger.json");
        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program")!;
        var sessionType = program.GetNestedType("CropTeacherDispatchSession", BindingFlags.NonPublic)!;
        var session = Activator.CreateInstance(sessionType, new object[] { new PlanningHandoffCaptureResult(capture, manifest.SourceStateHash, 4, 3, 1),
            Path.Combine(files.Root, "dispatch", "teacher-dispatch-manifest.json"), manifest.NativeRunPlanPath, request, plan,
            ledgerPath, CropTeacherTransitionLedger.Open(ledgerPath, manifest.NativeRunPlanPath), manifest.ActionCount })!;
        var options = new LiveTrainingOptions
        {
            Root = files.Root, ManifestPath = Path.Combine(files.Root, "run.json"), RunId = "managed-start", BackendUrl = "http://test",
            UseManagedCropTeacher = true, UseDailyPlan = true, UseRuntimeTestHarnessExecutor = true, UseProductExecutor = true,
            RequireExecutorFeedback = true, EmitQueueExecutionReceipt = true, RequireNativeSaveBoundary = true,
            SkipTraining = true,
            SaveIsolationPath = files.Root, SaveSlot = "declared-save"
        };
        using var handler = new RepositoryHandler(files.Repository, fixture.SourceSnapshot)
        {
            LoseCropReply = loseReplies, LoseResourceReply = loseReplies,
            EvidenceRoot = Path.Combine(options.SnapshotDir, "crop-teacher-managed-0001", "installation")
        };
        using var client = new HttpClient(handler);
        Task<CropTeacherManagedSeedOwner> Open() => (Task<CropTeacherManagedSeedOwner>)program.GetMethod("OpenCropTeacherManagedOwnerAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { client, options, 1, session })!;
        if (!completeSource)
        {
            await Assert.ThrowsAsync<InvalidDataException>(Open);
            Assert.Equal(0, handler.Reads); Assert.Equal(0, handler.CropPosts); Assert.Equal(0, handler.ResourcePosts);
            Assert.False(Directory.Exists(Path.Combine(options.SnapshotDir, "crop-teacher-managed-0001")));
            return;
        }
        await using var owner = await Open();
        var persisted = new FileStrategyCommitmentRepository(files.Repository).Get(fixture.SourceSnapshot);
        var installed = JsonSerializer.Deserialize<StardewAI.Contracts.Strategy.StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        Assert.Equal(persisted.Revision, installed.Revision);
        Assert.Equal(fixture.SnapshotPath, owner.CurrentSnapshotPath);
        Assert.Equal(350, installed.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount);
        Assert.Single(installed.CropPlantingCommitments); Assert.Null(owner.Pending);
        Assert.Equal(1, handler.CropPosts); Assert.Equal(1, handler.ResourcePosts);
        await Assert.ThrowsAsync<IOException>(Open);
        Assert.Equal(1, handler.CropPosts); Assert.Equal(1, handler.ResourcePosts);
    }
}
