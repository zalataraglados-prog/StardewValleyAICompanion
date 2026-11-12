using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed partial class OvernightFixture : IDisposable
    {
        internal OwnedWateringFixture Watering { get; }
        internal OwnedRouteFixture Shared => Watering.Shared;
        internal CropTeacherManagedSeedOwnerOptions Options { get; }
        internal string NativeSaveFile { get; }
        internal string NightRuntime => Shared.Files.Native + "-overnight";
        internal string HarvestRuntime => Shared.Files.Native + "-harvest";
        internal string NightQueue => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-sleep-0003-compiled-queue.json");
        internal string NightAfter => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "after-snapshot-0003.json");
        internal string HarvestQueue => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-batch-0005-compiled-queue.json");
        internal string HarvestAfter => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "after-snapshot-0005.json");
        internal int NativeNightCalls { get; private set; }
        internal int NativeHarvestCalls { get; private set; }

        internal OvernightFixture(bool noCrops = false, Action<Fixture>? prepareSource = null)
        {
            // A declared one-day catalog isolates owner sequencing; it is NOT vanilla growth evidence.
            Watering = new(prepareSource: source =>
            {
                HarvestFixture.PrepareSource(source);
                ChangeState(source.Before, "farm", farm =>
                {
                    var row = farm["crop_catalog"]!["value"]!.AsArray().Single(row => row!["seed_id"]!.GetValue<string>() == "745")!;
                    row["grow_days"] = 1;
                    foreach (var crop in farm["crops"]!["value"]!.AsArray().OfType<JsonObject>())
                    {
                        crop["phase_days"] = new JsonArray(1, 99999); crop["days_until_next_harvest_if_watered"] = 1;
                        crop["harvest_experience_skill_id"] = "farming"; crop["harvest_experience_skill_index"] = 0;
                        crop["harvest_experience_on_success_min"] = 23; crop["harvest_experience_on_success_max"] = 23;
                        crop["harvest_experience_projection_status"] = "exact_from_decompiled_native_harvest";
                        crop["harvest_experience_condition"] = "declared_fixture_projection";
                    }
                    if (noCrops) farm["crops"]!["value"]!.AsArray().Clear();
                });
                ChangeState(source.Before, "current_location", node =>
                {
                    node["crops"] = JsonNode.Parse(source.Before.State["farm"].GetRawText())!["crops"]!.DeepClone();
                    node["home_context"]!["value"] = JsonSerializer.SerializeToNode(new { home_location_id = "FarmHouse",
                        home_available = true, current_location_is_home = false, bed_tile_has_bed = true, bed_tile_x = 6, bed_tile_y = 6 });
                });
                ChangeState(source.Before, "menus", node =>
                {
                    node["menu_specific_state"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { } });
                    node["sleep_prompt_context"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { prompt_open = false } });
                });
                ChangeState(source.Before, "locations", node => node["route_graph"]!["value"]!["edges"]!.AsArray().Add(
                    JsonSerializer.SerializeToNode(new { kind = "warp", from_location = "FarmHouse", from_x = 6, from_y = 6,
                        target_location = "Farm", target_x = 5, target_y = 6, resolved = true })));
                prepareSource?.Invoke(source);
            });
            var live = Shared.Runtime.LiveOptions;
            live.UseDailyPlan = true;
            live.RequireNativeSaveBoundary = true;
            live.SaveIsolationPath = Path.Combine(Shared.Source.RuntimePath, "declared-native-saves"); live.SaveSlot = "OwnedNightFixture";
            var slot = Path.Combine(live.SaveIsolationPath, live.SaveSlot); Directory.CreateDirectory(slot);
            NativeSaveFile = Path.Combine(slot, "declared-save.dat"); File.WriteAllText(NativeSaveFile, "declared-before-native-night");
            Options = Shared.Files.Options with { NativeSaveBinding = new(live.SaveIsolationPath, live.SaveSlot) };
        }

        internal Task<CropTeacherManagedSeedOwner> Open(Func<string, string, Task>? writer = null) => Shared.Files.Open(Options, writer);

        internal async Task Home(CropTeacherManagedSeedOwner owner, bool water = true)
        {
            if (water)
            {
                var operation = await Watering.Prepare(owner);
                await owner.ExecuteOriginalWateringAsync(operation, Shared.Runtime.Client, () => Watering.EmitNative());
                await Watering.Rebind(owner, operation);
            }
            var iteration = water ? 2 : 1;
            var source = owner.CurrentSnapshotPath;
            var route = await Shared.Prepare(owner, iteration);
            await owner.ExecuteOriginalRouteAsync(route, Shared.Runtime.Client, () => Shared.EmitNative(iteration, source, snapshot =>
                ChangeState(snapshot, "current_location", node =>
                {
                    node["home_context"]!["value"]!["current_location_is_home"] = true;
                    node["crops"]!["value"] = new JsonArray();
                })));
            await Shared.Rebind(owner, route);
        }

        internal async Task<CropTeacherManagedSeedOperation> PrepareNight(CropTeacherManagedSeedOwner owner)
        {
            var before = Read(owner.CurrentSnapshotPath);
            var day = before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
            var step = new CropTeacherRunStep(CropTeacherRunStageKinds.NativeSleep, day, "", "FarmHouse", "FarmHouse", "", "", 0, 0, null, "declared_original_native_night");
            try
            {
                return await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedOvernightAsync", Shared.Runtime.Client, Shared.Runtime.LiveOptions,
                    3, step, Shared.Inputs.Plan, Shared.Inputs.Request, owner, NightRuntime);
            }
            catch (InvalidDataException error)
            {
                var path = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "crop-teacher-sleep-0003-ranking.json");
                if (!File.Exists(path)) throw;
                var ranking = JsonNode.Parse(File.ReadAllText(path))!;
                var candidates = ranking["ranked_event_candidates"]?.AsArray().Take(3).Select(row =>
                    row?["candidate_id"] + "/" + row?["available"] + "/" + row?["block_reasons"]?.ToJsonString()) ?? Array.Empty<string>();
                throw new InvalidDataException(error.Message + "; candidates=" + string.Join("|", candidates) + "; resolution=" +
                    ranking["goal_resolution"]?.ToJsonString() + "; kind_filter=" + ranking["candidate_kind_filter"]?.ToJsonString(), error);
            }
        }

        internal Task<JsonObject> EmitNight(CropTeacherManagedSeedOwner owner, bool save = true)
        {
            NativeNightCalls++;
            var before = Read(owner.CurrentSnapshotPath); var after = Copy(before); after.GameTick++;
            after.InGameTime.Value = 600;
            ChangeState(after, "time", node =>
            {
                node["total_days"]!["value"] = node["total_days"]!["value"]!.GetValue<int>() + 1;
                node["day"]!["value"] = node["day"]!["value"]!.GetValue<int>() + 1;
                node["time"]!["value"] = 600;
            });
            ChangeState(after, "farm", farm =>
            {
                foreach (var crop in farm["crops"]!["value"]!.AsArray().OfType<JsonObject>())
                {
                    crop["current_phase"] = 1; crop["day_of_current_phase"] = 0;
                    crop["ready_for_harvest"] = true; crop["days_until_next_harvest_if_watered"] = 0; crop["watered"] = false;
                }
            });
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(NightQueue), JsonOptions)!;
            var candidate = queue.Items.Single().NormalizedCommand.Parameters.Single(row => row.Name == "precondition" &&
                row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", queue, before, after,
                Shared.Files.Options.RunId, candidate, "sleep");
            receipt.StepResults.Single().AfterSnapshotPath = NightAfter;
            var path = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "execution-0003.json");
            Fixture.Save(path, receipt); Fixture.Save(NightAfter, after);
            Shared.Runtime.Store.Snapshots[after.StateHash] = after;
            if (save) CompleteSave();
            return Task.FromResult(new JsonObject { ["execution_path"] = path, ["after_snapshot_path"] = NightAfter });
        }

        internal void CompleteSave() => File.WriteAllText(NativeSaveFile, "declared-completed-native-night");
        internal Task RebindNight(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
            owner.RunOvernightAsync(operation, Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Shared.Runtime.Client, Shared.Runtime.LiveOptions, diagnostic, request), Shared.Files.Post);
        private static SnapshotEnvelope Read(string path) => JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(path), JsonOptions)!;
        private static SnapshotEnvelope Copy(SnapshotEnvelope source) => JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
        public void Dispose()
        {
            var slot = Path.GetDirectoryName(NativeSaveFile)!;
            // Retain unexpected artifacts for diagnosis without masking the original assertion.
            if (Directory.EnumerateFileSystemEntries(slot).Any(path => path != NativeSaveFile)) return;
            // Remove only this fixture's named save file and now-empty directories, never a recursive tree.
            File.Delete(NativeSaveFile);
            Directory.Delete(slot);
            Directory.Delete(Options.NativeSaveBinding!.SaveIsolationPath);
            Watering.Dispose();
        }
    }
}
