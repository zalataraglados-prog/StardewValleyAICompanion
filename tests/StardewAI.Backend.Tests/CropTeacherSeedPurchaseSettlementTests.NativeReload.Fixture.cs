using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Synthetic process/log/event observations test the consumer, never an actual game reload.
    private sealed class NativeReloadFixture
    {
        internal SavedResourceFixture Resources { get; } = new();
        internal string Root => Path.GetDirectoryName(Resources.BeforePath)!;
        internal string ObservationPath => Path.Combine(Root, "reload-observation.json");
        internal string EventsPath => Path.Combine(Root, "events.json");
        internal string LogPath => Path.Combine(Root, "log-copy.txt");
        internal CropTeacherNativeReloadObservation Observation { get; set; }
        internal EventStreamEnvelope Events { get; }
        internal NativeReloadFixture()
        {
            Resources.Before.RealTimestamp = "2026-11-12T00:00:00Z";
            Resources.After.RealTimestamp = "2026-11-12T00:01:05Z";
            Resources.Before.GameTick = 9000; Resources.After.GameTick = 10; Resources.Save();
            var binding = new CropTeacherNativeSaveBinding(Path.Combine(Root, "declared-saves"), "DeclaredReloadSlot");
            var slot = Path.Combine(binding.SaveIsolationPath, binding.SaveSlot); Directory.CreateDirectory(slot);
            File.WriteAllText(Path.Combine(slot, "declared-save.dat"), "not a native save: fixture fingerprint only");
            var fingerprint = NativeSaveBoundaryVerifier.Capture(binding.SaveIsolationPath, binding.SaveSlot);
            var savedPath = Path.Combine(Root, "overnight-save-boundary.json");
            Fixture.Save(savedPath, new JsonObject
            {
                ["schema_version"] = "crop_teacher_owned_overnight_save_boundary.v1", ["run_id"] = "declared-reload-run",
                ["save_id"] = Resources.Before.SaveId.Value, ["player_id"] = Resources.Before.PlayerId.Value,
                ["after_snapshot_path"] = Resources.BeforePath, ["after_save"] = JsonSerializer.SerializeToNode(fingerprint, JsonOptions),
                ["native_save_boundary_verified"] = true, ["formal_product_training_authorized"] = false
            });
            var exe = Path.Combine(Root, "StardewModdingAPI.exe");
            File.WriteAllText(exe, "declared executable bytes; not a runnable game");
            File.WriteAllText(LogPath, "[Harness] Redirected Stardew save folder to " + binding.SaveIsolationPath +
                "\n[Harness] Loading isolated test save slot " + binding.SaveSlot + "\n");
            Events = new() { Events = new[]
            {
                new GameEvent { EventId = "declared-load", EventType = "SaveLoaded", EventSequence = 1, GameTick = 2,
                    RealTimestamp = "2026-11-12T00:01:01Z", Source = "SMAPI event subscription", ObservedSnapshotHash = "unavailable",
                    PreviousEventHash = "genesis", ChangedFields = new[] { "identity.save_id", "identity.player_id" } },
                new GameEvent { EventId = "declared-full", EventType = "SnapshotPublished", EventSequence = 2, GameTick = 10,
                    RealTimestamp = "2026-11-12T00:01:06Z", Source = "TransparentStateCollector.BuildSnapshot",
                    ObservedSnapshotHash = Resources.After.StateHash, PublishedSnapshotHash = Resources.After.StateHash,
                    SnapshotRelation = "snapshot_published" }
            } };
            RehashEvents();
            var map = new Dictionary<string, string>();
            foreach (var path in new[] { Resources.BeforePath, Resources.AfterPath, Resources.LedgerPath, savedPath, exe, EventsPath, LogPath })
                map[path] = SavedResourceFixture.Hash(path);
            Observation = new("crop_teacher_native_reload_observation.v1", "declared-reload-run", Resources.BeforePath,
                Resources.LedgerPath, savedPath, binding, fingerprint, fingerprint,
                new(123, exe, map[exe], "2026-11-12T00:01:00Z", "2026-11-12T00:01:07Z"), Resources.AfterPath,
                EventsPath, LogPath, Path.Combine(Root, "declared-original-log.txt"), "2026-11-12T00:01:04Z", map);
            SaveObservation();
        }
        internal CropTeacherNativeSavedContentReadback Open() => CropTeacherNativeSavedContentReadback.Open(ObservationPath, "declared.native-reload");
        internal void SaveObservation() => Fixture.Save(ObservationPath, Observation);
        internal void PinAgain(string path)
        {
            var hashes = Observation.InputArtifactHashes.ToDictionary(row => row.Key, row => row.Value);
            hashes[path] = SavedResourceFixture.Hash(path); Observation = Observation with { InputArtifactHashes = hashes };
        }
        internal void RehashEvents()
        {
            var previous = "genesis";
            foreach (var item in Events.Events)
            {
                item.PreviousEventHash = previous;
                // Original bridge protocol uses these explicit payload keys, not a serializer of the witness record.
                var payload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["schema_version"] = item.SchemaVersion, ["event_id"] = item.EventId, ["event_sequence"] = item.EventSequence,
                    ["event_type"] = item.EventType, ["game_tick"] = item.GameTick, ["real_timestamp"] = item.RealTimestamp,
                    ["source"] = item.Source, ["in_game_time"] = item.InGameTime, ["observed_snapshot_hash"] = item.ObservedSnapshotHash,
                    ["published_snapshot_hash"] = item.PublishedSnapshotHash, ["snapshot_relation"] = item.SnapshotRelation,
                    ["previous_event_hash"] = item.PreviousEventHash, ["changed_fields"] = item.ChangedFields,
                    ["before"] = item.Before, ["after"] = item.After
                }, JsonOptions);
                item.EventHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SnapshotHash.Canonicalize(payload)))).ToLowerInvariant();
                previous = item.EventHash;
            }
            Events.Count = Events.Events.Length; Events.LatestEventSequence = Events.Events[^1].EventSequence;
            Events.NextAfterSequence = Events.LatestEventSequence; Events.LatestEventHash = previous;
            Events.LatestSnapshotHash = Resources.After.StateHash;
            Fixture.Save(EventsPath, Events);
        }
    }
}
