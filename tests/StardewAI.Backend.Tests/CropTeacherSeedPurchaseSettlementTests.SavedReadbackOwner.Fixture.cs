using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class SavedReadbackOwnerFixture : IDisposable
    {
        internal OvernightFixture Night { get; } = new(prepareSource: source => ChangeState(source.Before, "time", time =>
        {
            time["year"] ??= JsonSerializer.SerializeToNode(new { status = "available", value = 2 });
            time["season"] ??= JsonSerializer.SerializeToNode(new { status = "available", value = "spring" });
        }));
        internal NativeReloadFixture Witness { get; } = new();
        internal string Runtime => Path.Combine(Night.Options.EvidenceDirectory, "saved-readback");
        internal string LoadedPath => Path.Combine(Witness.Root, "owner-loaded.json");

        internal async Task CompleteNight(CropTeacherManagedSeedOwner owner)
        {
            await Night.Home(owner);
            var night = await Night.PrepareNight(owner);
            await owner.ExecuteOriginalOvernightAsync(night, Night.Shared.Runtime.Client, () => Night.EmitNight(owner));
            await Night.RebindNight(owner, night);
            BindObservation(owner);
        }

        internal void BindObservation(CropTeacherManagedSeedOwner owner)
        {
            var loaded = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
            var sourceTime = DateTimeOffset.Parse(loaded.RealTimestamp);
            loaded.GameTick = 10; loaded.RealTimestamp = sourceTime.AddSeconds(65).ToString("O");
            ChangeState(loaded, "player", player => player["tile_x"]!["value"] = player["tile_x"]!["value"]!.GetValue<int>() + 1);
            Fixture.Save(LoadedPath, loaded);
            Night.Shared.Runtime.Store.Snapshots[loaded.StateHash] = loaded;
            var savedPath = Path.Combine(Night.NightRuntime, "overnight-save-boundary.json");
            var report = JsonNode.Parse(File.ReadAllText(savedPath))!;
            var fingerprint = report["after_save"]!.Deserialize<SaveDirectoryFingerprint>(JsonOptions)!;
            var binding = Night.Options.NativeSaveBinding!;
            File.WriteAllText(Witness.LogPath, "[Harness] Redirected Stardew save folder to " + binding.SaveIsolationPath +
                "\n[Harness] Loading isolated test save slot " + binding.SaveSlot + "\n");
            Witness.Events.Events[0].RealTimestamp = sourceTime.AddSeconds(61).ToString("O");
            Witness.Events.Events[1].RealTimestamp = sourceTime.AddSeconds(66).ToString("O");
            Witness.Events.Events[1].PublishedSnapshotHash = loaded.StateHash;
            Witness.Events.Events[1].ObservedSnapshotHash = loaded.StateHash;
            Witness.RehashEvents();
            Witness.Events.LatestSnapshotHash = loaded.StateHash;
            Fixture.Save(Witness.EventsPath, Witness.Events);
            var executable = Witness.Observation.Process.ExecutablePath;
            var map = new[] { owner.CurrentSnapshotPath, owner.CurrentLedgerPath, savedPath, LoadedPath,
                Witness.EventsPath, Witness.LogPath, executable }.Distinct().ToDictionary(path => path, SavedResourceFixture.Hash);
            Witness.Observation = Witness.Observation with
            {
                RunId = Night.Options.RunId, SourceSnapshotPath = owner.CurrentSnapshotPath, LedgerPath = owner.CurrentLedgerPath,
                OvernightSaveBoundaryPath = savedPath, SaveBinding = binding, BeforeReadSave = fingerprint, AfterReadSave = fingerprint,
                LoadedSnapshotPath = LoadedPath, InputArtifactHashes = map,
                SourceLogLastWriteAtUtc = sourceTime.AddSeconds(64).ToString("O"),
                Process = Witness.Observation.Process with
                { StartedAtUtc = sourceTime.AddSeconds(60).ToString("O"), ObservedAtUtc = sourceTime.AddSeconds(67).ToString("O") }
            };
            Witness.SaveObservation();
        }

        internal Task<CropTeacherManagedSeedOperation> Admit(CropTeacherManagedSeedOwner owner) =>
            owner.AdmitSavedContentReadbackAsync(Witness.ObservationPath, Runtime);
        internal Task Run(CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation) =>
            owner.RunSavedContentReadbackAsync(operation, Night.Shared.Runtime.Client, (diagnostic, request) =>
                InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Night.Shared.Runtime.Client,
                    Night.Shared.Runtime.LiveOptions, diagnostic, request), Night.Shared.Files.Post);
        public void Dispose() => Night.Dispose();
    }
}
