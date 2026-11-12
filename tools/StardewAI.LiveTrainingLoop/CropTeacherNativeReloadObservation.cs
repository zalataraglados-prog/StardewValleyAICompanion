using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherReloadProcessObservation(int ProcessId, string ExecutablePath, string ExecutableSha256,
    string StartedAtUtc, string ObservedAtUtc);

public sealed record CropTeacherNativeReloadObservation(string SchemaVersion, string RunId,
    string SourceSnapshotPath, string LedgerPath, string OvernightSaveBoundaryPath, CropTeacherNativeSaveBinding SaveBinding,
    SaveDirectoryFingerprint BeforeReadSave, SaveDirectoryFingerprint AfterReadSave,
    CropTeacherReloadProcessObservation Process, string LoadedSnapshotPath, string EventStreamPath, string LogSnapshotPath,
    string SourceLogPath, string SourceLogLastWriteAtUtc,
    IReadOnlyDictionary<string, string> InputArtifactHashes);

// A load witness and a resource comparison are separate requirements; neither implies the other.
public sealed class CropTeacherNativeSavedContentReadback : ICropTeacherSupportingTransition
{
    private readonly CropSeedRuntimeArtifacts inputs;
    private readonly CropTeacherSavedResourceReadback content;
    private CropTeacherNativeSavedContentReadback(string observationPath, string stepId)
    {
        inputs = new(new Dictionary<string, string>());
        Observation = inputs.Read<CropTeacherNativeReloadObservation>(observationPath);
        Require(Observation.SchemaVersion == "crop_teacher_native_reload_observation.v1" && !string.IsNullOrWhiteSpace(Observation.RunId),
            "Native reload observation has no original schema/run.");
        foreach (var (path, hash) in Observation.InputArtifactHashes)
        {
            Require(Path.IsPathFullyQualified(path), "Native reload input path is not absolute.");
            Require(inputs.Pin(path) == hash, "Native reload observation input changed.");
        }
        foreach (var path in new[] { Observation.SourceSnapshotPath, Observation.LedgerPath, Observation.OvernightSaveBoundaryPath,
                     Observation.LoadedSnapshotPath, Observation.EventStreamPath, Observation.LogSnapshotPath, Observation.Process.ExecutablePath })
            Require(Path.IsPathFullyQualified(path) && Observation.InputArtifactHashes.Keys.Any(key => Same(key, path)), "Native reload observation omitted a required absolute input.");
        var source = inputs.Read<SnapshotEnvelope>(Observation.SourceSnapshotPath);
        var loaded = inputs.Read<SnapshotEnvelope>(Observation.LoadedSnapshotPath);
        var saved = inputs.Read<JsonObject>(Observation.OvernightSaveBoundaryPath);
        var binding = CropTeacherManagedSeedOwner.NormalizeNativeSaveBinding(Observation.SaveBinding)!;
        var fingerprint = saved["after_save"]?.Deserialize<SaveDirectoryFingerprint>(CropTeacherManagedSeedOwner.JsonOptions)
            ?? throw new InvalidDataException("Native reload has no original completed save fingerprint.");
        var savedSnapshotPath = saved["after_snapshot_path"]?.GetValue<string>() ?? "";
        Require(Path.IsPathFullyQualified(savedSnapshotPath) && Observation.InputArtifactHashes.Keys.Any(key => Same(key, savedSnapshotPath)),
            "Native reload omitted the canonical saved snapshot input.");
        var savedSnapshot = inputs.Read<SnapshotEnvelope>(savedSnapshotPath);
        Require(saved["schema_version"]?.GetValue<string>() == "crop_teacher_owned_overnight_save_boundary.v1" &&
            saved["native_save_boundary_verified"]?.GetValue<bool>() == true &&
            saved["formal_product_training_authorized"]?.GetValue<bool>() == false &&
            saved["run_id"]?.GetValue<string>() == Observation.RunId && saved["save_id"]?.GetValue<string>() == source.SaveId.Value &&
            saved["player_id"]?.GetValue<string>() == source.PlayerId.Value &&
            Same(fingerprint.SlotPath, Path.Combine(binding.SaveIsolationPath, binding.SaveSlot)) &&
            fingerprint.FileCount > 0 && fingerprint.TotalBytes > 0 && fingerprint.Sha256.Length == 64 && fingerprint.Sha256.All(Uri.IsHexDigit) &&
            fingerprint == Observation.BeforeReadSave && fingerprint == Observation.AfterReadSave,
            "Native reload did not read the exact original completed save slot/bytes.");
        var process = Observation.Process;
        var started = Timestamp(process.StartedAtUtc); var observed = Timestamp(process.ObservedAtUtc);
        Require(process.ProcessId > 0 && started > Timestamp(source.RealTimestamp) && observed >= started &&
            Timestamp(loaded.RealTimestamp) >= started && Timestamp(loaded.RealTimestamp) <= observed &&
            inputs.Hashes[Path.GetFullPath(process.ExecutablePath)] == process.ExecutableSha256 &&
            Path.GetFileNameWithoutExtension(process.ExecutablePath).Equals("StardewModdingAPI", StringComparison.OrdinalIgnoreCase),
            "Native reload did not bind a newly started original game process.");
        Require(Path.IsPathFullyQualified(Observation.SourceLogPath) && Timestamp(Observation.SourceLogLastWriteAtUtc) >= started &&
            Timestamp(Observation.SourceLogLastWriteAtUtc) <= observed, "Native reload log predates its observed game process.");
        var log = File.ReadAllText(Observation.LogSnapshotPath);
        var lines = log.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        Require(lines.Any(line => line.EndsWith("Redirected Stardew save folder to " + binding.SaveIsolationPath, StringComparison.Ordinal)) &&
            lines.Any(line => line.EndsWith("Loading isolated test save slot " + binding.SaveSlot, StringComparison.Ordinal)),
            "Native reload lacks the original harness save-root/slot load observations.");
        VerifyEvents(inputs.Read<EventStreamEnvelope>(Observation.EventStreamPath), loaded, started, observed);
        CropTeacherSavedResourceReadback.VerifySnapshotContent(savedSnapshot, loaded);
        content = CropTeacherSavedResourceReadback.Open(Observation.SourceSnapshotPath, Observation.LoadedSnapshotPath,
            Observation.LedgerPath, stepId);
        foreach (var (path, hash) in content.InputHashes) Require(inputs.Pin(path) == hash, "Native reload resource inputs changed.");
        ValidateInputsUnchanged();
    }

    public static CropTeacherNativeSavedContentReadback Open(string observationPath, string stepId) => new(observationPath, stepId);
    public CropTeacherNativeReloadObservation Observation { get; }
    public bool NativeReloadVerified => true;
    public bool SemanticSavedContentVerified => true;
    public bool FormalProductTrainingAuthorized => false;
    public string AfterSnapshotPath => content.AfterSnapshotPath;
    public StrategyCommitmentLedger BaseLedger => content.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => content.RebindRequest;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply)
    { ValidateInputsUnchanged(); return content.VerifyRebindReply(reply); }
    public IReadOnlyDictionary<string, string> InputHashes => inputs.Hashes;
    public void ValidateInputsUnchanged() { inputs.Validate(); content?.ValidateInputsUnchanged(); }

    private static void VerifyEvents(EventStreamEnvelope stream, SnapshotEnvelope loaded, DateTimeOffset started, DateTimeOffset observed)
    {
        Require(stream.SchemaVersion == "event_stream.v2" && stream.ChainStatus == "ok" && stream.Events.Length > 0 &&
            stream.Count == stream.Events.Length && stream.Events[0].EventSequence == 1 && stream.Events[0].PreviousEventHash == "genesis" &&
            stream.Events[^1].EventSequence == stream.LatestEventSequence && stream.Events[^1].EventHash == stream.LatestEventHash &&
            stream.NextAfterSequence == stream.LatestEventSequence, "Native reload event stream is partial or not a fresh process chain.");
        var previous = "genesis"; long sequence = 0;
        foreach (var item in stream.Events)
        {
            var payload = JsonSerializer.SerializeToNode(item, CropTeacherManagedSeedOwner.JsonOptions)!.AsObject();
            payload.Remove("event_hash");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                SnapshotHash.Canonicalize(JsonSerializer.SerializeToElement(payload))))).ToLowerInvariant();
            Require(item.SchemaVersion == "event.v2" && item.EventSequence == ++sequence && item.PreviousEventHash == previous &&
                item.EventHash == hash && Timestamp(item.RealTimestamp) >= started && Timestamp(item.RealTimestamp) <= observed,
                "Native reload event hash/sequence/time differs from the original bridge protocol.");
            previous = hash;
        }
        var load = stream.Events.Where(item => item.EventType == "SaveLoaded" && item.Source == "SMAPI event subscription").ToArray();
        Require(load.Length == 1 && load[0].ChangedFields.Contains("identity.save_id", StringComparer.Ordinal) &&
            load[0].ChangedFields.Contains("identity.player_id", StringComparer.Ordinal), "Native reload lacks one original SaveLoaded event.");
        var published = stream.Events.Where(item => item.EventType == "SnapshotPublished" && item.Source == "TransparentStateCollector.BuildSnapshot" &&
            item.SnapshotRelation == "snapshot_published" && item.PublishedSnapshotHash == loaded.StateHash && item.GameTick == loaded.GameTick &&
            item.EventSequence > load[0].EventSequence).ToArray();
        Require(published.Length > 0 && loaded.GameTick >= load[0].GameTick &&
            Timestamp(loaded.RealTimestamp) >= Timestamp(load[0].RealTimestamp) &&
            !stream.Events.Any(item => item.EventSequence > load[0].EventSequence && item.EventSequence <= published[0].EventSequence &&
                item.EventType == "InventoryChanged"), "Native reload full readback is not an untouched post-load snapshot.");
    }

    private static DateTimeOffset Timestamp(string text) => DateTimeOffset.TryParse(text, out var value) ? value :
        throw new InvalidDataException("Native reload timestamp is unavailable.");
    private static bool Same(string left, string right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
}
