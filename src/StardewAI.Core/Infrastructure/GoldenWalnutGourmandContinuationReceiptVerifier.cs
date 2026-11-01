using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutGourmandContinuationReceipt(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("verified_transition_count")] int VerifiedTransitionCount,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons,
    [property: JsonPropertyName("source_episode_manifest_path")] string SourceEpisodeManifestPath,
    [property: JsonPropertyName("source_episode_manifest_sha256")] string SourceEpisodeManifestSha256,
    [property: JsonPropertyName("source_episode_evidence_root_sha256")] string SourceEpisodeEvidenceRootSha256,
    [property: JsonPropertyName("native_receipt_evidence_root_sha256")] string NativeReceiptEvidenceRootSha256,
    [property: JsonPropertyName("source_run_id")] string SourceRunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion)
{
    [JsonPropertyName("verified")]
    public bool Verified => Ready;

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible => false;
}

/// <summary>Offline native transition proof over one frozen episode manifest; not training admission.</summary>
public static class GoldenWalnutGourmandContinuationReceiptVerifier
{
    public const string SchemaVersion = "golden_walnut_gourmand_continuation_receipt.v1";
    private const string OptionId = "executor.advance_gourmand_request";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] NativeReasons =
    {
        "native_Gourmand_Yes_response_completed", "native_crop_event_and_return_observed",
        "native_five_walnut_debris_spawn_and_pickup_verified", "request_tracker_and_collected_nut_receipt_verified"
    };

    public static GoldenWalnutGourmandContinuationReceipt Verify(
        string episodeDirectory, string manifestPath,
        string? expectedManifestSha256 = null, string? expectedEvidenceRootSha256 = null)
    {
        if (episodeDirectory is null) throw new ArgumentNullException(nameof(episodeDirectory));
        if (manifestPath is null) throw new ArgumentNullException(nameof(manifestPath));
        var manifestFullPath = string.Empty;
        var manifestHash = string.Empty;
        var sourceRoot = string.Empty;
        var nativeRoot = string.Empty;
        var run = string.Empty;
        var save = string.Empty;
        var player = string.Empty;
        var game = string.Empty;
        var verified = 0;
        try
        {
            var directory = Path.GetFullPath(episodeDirectory);
            manifestFullPath = Path.GetFullPath(manifestPath);
            var observations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var manifestJson = Read(manifestFullPath, observations);
            manifestHash = observations[manifestFullPath];
            Require(expectedManifestSha256 is null || manifestHash == expectedManifestSha256,
                "manifest_hash_mismatch");
            var manifest = manifestJson.Deserialize<GoldenWalnutFreshSaveEpisodeManifest>()!;
            Require(manifest is not null && manifest.EpisodePaths is { Length: > 0 }, "manifest_invalid");
            run = manifest!.SourceRunId;
            game = manifest.GameVersion;
            Require(!string.IsNullOrWhiteSpace(run) && !string.IsNullOrWhiteSpace(manifest.ScheduleId),
                "manifest_invalid");
            var boundaryPath = FrozenPath(directory, manifest.InitialBoundaryPath);
            var boundaryJson = Read(boundaryPath, observations);
            Require(boundaryJson.ValueKind == JsonValueKind.Object && new[]
                {
                    "schema_version", "training_label_eligible", "run_id", "save_id", "game_version",
                    "source_state_hash", "source_game_tick", "source_total_day", "snapshot_path", "snapshot_logical_sha256"
                }.All(name => boundaryJson.TryGetProperty(name, out _)), "initial_boundary_fields_missing");
            var boundary = boundaryJson.Deserialize<GoldenWalnutFreshSaveInitialBoundary>()!;
            Require(boundary is not null, "initial_boundary_invalid");
            var initialPath = FrozenPath(directory, boundary!.SnapshotPath);
            var initialJson = Read(initialPath, observations);
            var initial = initialJson.Deserialize<SnapshotEnvelope>()!;
            Require(initial is not null, "initial_boundary_invalid");
            save = Identity(initialJson, "save_id");
            player = Identity(initialJson, "player_id");

            // Preserve the existing membership, ordering, fresh-save and passive-handoff rules.
            var rebuilt = GoldenWalnutFreshSaveEpisodeManifestBuilder.BuildFromDirectory(
                directory, manifest.ScheduleId, run);
            Require(SameJson(manifestJson, JsonSerializer.SerializeToElement(rebuilt)), "manifest_membership_or_order_drift");
            var episodes = new List<(string Path, JsonElement Json, PlanExecutionEpisodeEnvelope Episode)>();
            var sourceHashes = new List<string> { observations[boundaryPath] };
            foreach (var path in manifest.EpisodePaths)
            {
                var episodePath = FrozenPath(directory, path);
                var json = Read(episodePath, observations);
                var episode = json.Deserialize<PlanExecutionEpisodeEnvelope>()!;
                Require(episode is not null, "episode_invalid");
                episodes.Add((episodePath, json, episode!));
                var beforePath = FrozenPath(directory, episode!.BeforeSnapshotPath);
                var afterPath = FrozenPath(directory, episode.AfterSnapshotPath);
                Read(beforePath, observations);
                Read(afterPath, observations);
                sourceHashes.Add(observations[episodePath]);
                sourceHashes.Add(observations[beforePath]);
                sourceHashes.Add(observations[afterPath]);
            }
            var schedule = GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build(manifestFullPath);
            sourceRoot = schedule.SourceEpisodeEvidenceRootSha256;
            Require(schedule.SourceEpisodeManifestSha256 == manifestHash &&
                    sourceRoot == Hash(string.Join("\n", sourceHashes)) &&
                    (expectedEvidenceRootSha256 is null || sourceRoot == expectedEvidenceRootSha256),
                "source_evidence_hash_mismatch");

            var previous = ReadProgress(initialJson);
            Require(previous.Fulfilled == 0, "initial_gourmand_progress_not_zero");
            var episodeIds = new HashSet<string>(StringComparer.Ordinal);
            var executionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nativeHashes = new List<string>();
            foreach (var row in episodes)
            {
                var episode = row.Episode;
                Require(episodeIds.Add(episode.EpisodeId), "episode_duplicate");
                var beforeJson = Read(FrozenPath(directory, episode.BeforeSnapshotPath), observations);
                var afterJson = Read(FrozenPath(directory, episode.AfterSnapshotPath), observations);
                ValidateSnapshot(beforeJson, episode.SourceStateHash, episode.BeforeGameTick, save, player, game);
                ValidateSnapshot(afterJson, episode.AfterStateHash, episode.AfterGameTick, save, player, game);
                var before = ReadProgress(beforeJson);
                var after = ReadProgress(afterJson);
                Require(previous.Fulfilled == before.Fulfilled && previous.Quantities.SequenceEqual(before.Quantities),
                    "gourmand_unreceipted_handoff_progress");
                if (before.Fulfilled != after.Fulfilled || !before.Quantities.SequenceEqual(after.Quantities))
                {
                    Require(before.Fulfilled == verified && after.Fulfilled == verified + 1 && verified < 3,
                        "gourmand_transition_sequence_invalid");
                    var executionPath = FrozenPath(directory, episode.ExecutionResultPath);
                    Require(executionPaths.Add(executionPath), "native_receipt_reused");
                    var execution = Read(executionPath, observations);
                    ValidateNativeReceipt(row.Json, execution, before, after, beforeJson, afterJson);
                    nativeHashes.Add(observations[executionPath]);
                    verified++;
                }
                else if (episode.OptionId == OptionId && episode.PrimitiveVerificationReasons is not null &&
                         episode.PrimitiveVerificationReasons.Contains(NativeReasons[3], StringComparer.Ordinal))
                {
                    throw new EvidenceException("native_receipt_without_transition");
                }
                previous = after;
            }
            Require(verified == 3, "gourmand_native_transitions_incomplete");
            var terminal = Read(FrozenPath(directory, episodes.Last().Episode.AfterSnapshotPath), observations)
                .Deserialize<SnapshotEnvelope>()!;
            Require(GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(terminal).Ready,
                "gourmand_terminal_state_invalid");
            nativeRoot = Hash(string.Join("\n", nativeHashes));
            foreach (var observation in observations)
                Require(Hash(ReadLogicalText(observation.Key)) == observation.Value, "frozen_artifact_changed_during_verification");
            var finalManifest = GoldenWalnutFreshSaveEpisodeManifestBuilder.BuildFromDirectory(directory, manifest.ScheduleId, run);
            Require(SameJson(manifestJson, JsonSerializer.SerializeToElement(finalManifest)), "manifest_membership_or_order_drift");
            return Receipt(Array.Empty<string>());
        }
        catch (EvidenceException exception) { return Receipt(new[] { exception.Message }); }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
            exception is JsonException || exception is ArgumentException || exception is InvalidOperationException ||
            exception is KeyNotFoundException || exception is NotSupportedException || exception is OverflowException ||
            exception is NullReferenceException || exception is FormatException)
        {
            // Existing manifest validators throw for malformed contract members, including explicit nulls.
            return Receipt(new[] { "frozen_episode_evidence_invalid" });
        }

        GoldenWalnutGourmandContinuationReceipt Receipt(string[] blockers) => new(
            SchemaVersion, blockers.Length == 0 && verified == 3, verified, blockers, manifestFullPath,
            manifestHash, sourceRoot, nativeRoot, run, save, player, game);
    }

    public static GoldenWalnutGourmandContinuationReceipt Write(
        string outputPath, string episodeDirectory, string manifestPath)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        var receipt = Verify(episodeDirectory, manifestPath);
        var fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath))
        {
            using var existing = JsonDocument.Parse(File.ReadAllText(fullPath));
            if (Text(existing.RootElement, "schema_version") != SchemaVersion)
                throw new ArgumentException("Output must not replace a source evidence artifact.", nameof(outputPath));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(receipt, JsonOptions), new UTF8Encoding(false));
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
            else File.Move(temporary, fullPath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return receipt;
    }

    private static void ValidateNativeReceipt(JsonElement episode, JsonElement execution,
        Progress before, Progress after, JsonElement beforeJson, JsonElement afterJson)
    {
        Require(Text(episode, "option_id") == OptionId && Bool(episode, "success") &&
                Bool(episode, "after_snapshot_fresh") && Text(episode, "status") == "applied" &&
                Text(episode, "primitive_kind") == "advance_gourmand_request" &&
                Text(episode, "primitive_verification_status") == "verified" &&
                EmptyArray(episode, "block_reasons"), "native_episode_not_verified");
        Require(Text(execution, "schema_version") == "training_execution_result.v1" &&
                Text(execution, "option_id") == OptionId && Text(execution, "status") == "applied" &&
                Bool(execution, "feedback_available") && Bool(execution, "after_snapshot_fresh") &&
                Text(execution, "primitive_kind") == "advance_gourmand_request" &&
                Text(execution, "primitive_verification_status") == "verified" &&
                EmptyArray(execution, "block_reasons"), "native_execution_not_verified");
        Require(Bool(episode, "state_hash_changed") && Number64(execution, "after_game_tick") > Number64(execution, "before_game_tick") &&
                Number(execution, "actual_ticks") > 0 &&
                DateTimeOffset.TryParse(Text(execution, "started_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var started) &&
                DateTimeOffset.TryParse(Text(execution, "completed_at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var completed) &&
                completed >= started, "native_execution_freshness_invalid");
        foreach (var name in new[] { "run_id", "queue_id", "option_id", "requested_effect", "observed_effect", "primitive_verification_reasons", "changed_facts" })
            Require(SameJson(Property(episode, name), Property(execution, name)), "native_execution_episode_mismatch");
        Require(!string.IsNullOrWhiteSpace(Text(execution, "queue_id")) &&
                !string.IsNullOrWhiteSpace(Text(execution, "queue_item_id")) &&
                Text(execution, "before_state_hash") == Text(episode, "source_state_hash") &&
                Text(execution, "after_state_hash") == Text(episode, "after_state_hash") &&
                Number64(execution, "before_game_tick") == Number64(episode, "before_game_tick") &&
                Number64(execution, "after_game_tick") == Number64(episode, "after_game_tick") &&
                Text(execution, "target_location") == "IslandFarmCave", "native_execution_identity_mismatch");
        var item = Property(episode, "effective_queue_item");
        Require(Text(item, "queue_item_id") == Text(execution, "queue_item_id") && Text(item, "option_id") == OptionId,
            "native_queue_item_mismatch");
        var reasons = Property(execution, "primitive_verification_reasons");
        Require(reasons.ValueKind == JsonValueKind.Array && reasons.GetArrayLength() == NativeReasons.Length &&
                reasons.EnumerateArray().Select(value => value.GetString()).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(NativeReasons.OrderBy(value => value, StringComparer.Ordinal)), "native_completion_reasons_missing");
        Require(before.Mail && after.Mail && before.Talked && after.Talked && before.ReadyCrops > 0 &&
                before.Location == "IslandFarmCave" && after.Location == "IslandFarmCave" &&
                before.IsCurrent && after.IsCurrent && before.ProjectionStatus == "exact_native_active_request_projected" &&
                before.Host && after.Host && before.MenuClear && after.MenuClear &&
                before.Debris == 0 && after.Debris == 0 && !before.Triggered && !after.Triggered &&
                before.MutexAvailable && after.MutexAvailable && !before.MutexLocked && !after.MutexLocked &&
                before.NativeContract == after.NativeContract && before.NativeContract ==
                    "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward",
            "native_gourmand_context_invalid");
        var foundBefore = Number(Projection(beforeJson, "golden_walnut_sources"), "world_golden_walnuts_found");
        var foundAfter = Number(Projection(afterJson, "golden_walnut_sources"), "world_golden_walnuts_found");
        Require(foundBefore >= 0 && foundBefore <= 130 && foundAfter == Math.Min(130, foundBefore + 5),
            "native_walnut_found_delta_invalid");
        var key = "IslandGourmand" + after.Fulfilled;
        Require(Text(execution, "requested_effect") ==
                "gourmand_stage=complete_request;request_index_after=" + after.Fulfilled +
                ";collected_nut_key=" + key + ";walnut_debris_spawn_count=5;golden_walnuts_found_after=" + foundAfter &&
                Text(execution, "observed_effect") == "location=IslandFarmCave;menu=none;request_index=" +
                after.Fulfilled + ";talked=true;walnut_debris=0;golden_walnuts_found=" + foundAfter,
            "native_effect_mismatch");
        var facts = Property(execution, "changed_facts");
        Require(facts.ValueKind == JsonValueKind.Array && facts.GetArrayLength() == 5, "native_changed_facts_invalid");
        Fact(facts, "world_progress.island_gourmand.requests_fulfilled", before.Fulfilled.ToString(CultureInfo.InvariantCulture), after.Fulfilled.ToString(CultureInfo.InvariantCulture));
        Fact(facts, "world_progress.island_gourmand.talked_to_gourmand_received_or_pending", "True", "True");
        Fact(facts, "world_progress.island_gourmand.walnut_debris_count", "0", "0");
        Fact(facts, "world_progress.golden_walnuts.found", foundBefore.ToString(CultureInfo.InvariantCulture), foundAfter.ToString(CultureInfo.InvariantCulture));
        Fact(facts, "player.team.collected_nut_tracker[" + key + "]", "False", "True");
    }

    private static Progress ReadProgress(JsonElement snapshot)
    {
        var progress = Projection(snapshot, "island_gourmand");
        var fulfilled = Number(progress, "requests_fulfilled");
        Require(fulfilled >= 0 && fulfilled <= 3 && Number(progress, "total_requests") == 3 &&
                Text(progress, "schema_version") == "island_gourmand_progress.v1" &&
                Bool(progress, "complete") == (fulfilled == 3), "gourmand_projection_invalid");
        var activeIndex = Property(progress, "active_request_index");
        var crop = fulfilled == 3 ? string.Empty : new[] { "254", "262", "248" }[fulfilled];
        Require((fulfilled == 3 ? activeIndex.ValueKind == JsonValueKind.Null : activeIndex.GetInt32() == fulfilled) &&
                Text(progress, "required_crop_item_id") == crop &&
                Text(progress, "required_crop_qualified_item_id") == (crop.Length == 0 ? string.Empty : "(O)" + crop),
            "gourmand_active_request_invalid");
        var catalog = Projection(snapshot, "golden_walnut_sources");
        Require(Text(catalog, "schema_version") == GoldenWalnutNativeSourceCatalog.SchemaVersion &&
                Text(catalog, "projection_status") == GoldenWalnutNativeSourceCatalog.ProjectionStatus &&
                Number(catalog, "native_source_key_count") == GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount &&
                Number(catalog, "native_walnut_total") == GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
            "source_catalog_invalid");
        var rows = Property(catalog, "sources").EnumerateArray().ToArray();
        Require(rows.Length == GoldenWalnutNativeSourceCatalog.Sources.Count &&
                rows.Select(row => Text(row, "source_key")).Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "source_catalog_denominator_invalid");
        var collected = 0;
        var remaining = 0;
        foreach (var definition in GoldenWalnutNativeSourceCatalog.Sources)
        {
            var matches = rows.Where(row => Text(row, "source_key") == definition.SourceKey).ToArray();
            Require(matches.Length == 1, "source_catalog_denominator_invalid");
            var row = matches[0];
            var quantity = Number(row, "collected_quantity");
            Require(Number(row, "native_quantity") == definition.NativeQuantity && quantity >= 0 &&
                    quantity <= definition.NativeQuantity && Number(row, "remaining_quantity") == definition.NativeQuantity - quantity &&
                    Text(row, "acquisition_option_id") == definition.AcquisitionOptionId, "source_quantity_invalid");
            collected += quantity;
            remaining += Number(row, "remaining_quantity");
        }
        Require(Number(catalog, "tracked_collected_quantity") == collected &&
                Number(catalog, "tracked_remaining_quantity") == remaining, "source_totals_invalid");
        var quantities = Enumerable.Range(1, 3).Select(index => Number(
            rows.Single(row => Text(row, "source_key") == "IslandGourmand" + index), "collected_quantity")).ToArray();
        Require(quantities.Select((value, index) => value == (index < fulfilled ? 5 : 0)).All(value => value),
            "gourmand_source_quantity_mismatch");
        return new Progress(fulfilled, quantities, Bool(progress, "required_mail_satisfied"),
            Bool(progress, "talked_to_gourmand_received_or_pending"), Number(progress, "ready_matching_crop_count"),
            Text(progress, "location_id"), Bool(progress, "host_authority"), Bool(progress, "menu_clear"),
            Number(progress, "walnut_debris_count"), Bool(progress, "triggered_gourmand"),
            Bool(progress, "mutex_projection_available"), Bool(progress, "mutex_locked"), Text(progress, "native_contract"),
            Bool(progress, "is_current_location"), Text(progress, "projection_status"));
    }

    private static void ValidateSnapshot(JsonElement snapshot, string hash, long tick, string save, string player, string game)
    {
        Require(Text(snapshot, "schema_version") == "snapshot.v1" && Text(snapshot, "game_version") == game &&
                game == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion &&
                Text(snapshot, "state_hash") == hash && !string.IsNullOrWhiteSpace(hash) &&
                Number64(snapshot, "game_tick") == tick && Identity(snapshot, "save_id") == save &&
                Identity(snapshot, "player_id") == player, "snapshot_identity_mismatch");
    }

    private static void Fact(JsonElement facts, string path, string before, string after)
    {
        var matches = facts.EnumerateArray().Where(row => Text(row, "path") == path).ToArray();
        Require(matches.Length == 1 && Text(matches[0], "before") == before && Text(matches[0], "after") == after,
            "native_changed_facts_invalid");
    }

    private static JsonElement Projection(JsonElement snapshot, string key)
    {
        var field = Property(Property(Property(snapshot, "state"), "world_progress"), key);
        Require(Text(field, "status") == FieldStatus.Available, "transparent_projection_unavailable");
        return Property(field, "value");
    }

    private static string Identity(JsonElement snapshot, string key)
    {
        var field = Property(snapshot, key);
        var value = Text(field, "value");
        Require(Text(field, "status") == FieldStatus.Available && !string.IsNullOrWhiteSpace(value), "snapshot_identity_unavailable");
        return value;
    }

    private static JsonElement Read(string path, IDictionary<string, string> observations)
    {
        var text = ReadLogicalText(path);
        var hash = Hash(text);
        Require(!observations.TryGetValue(path, out var previous) || previous == hash, "frozen_artifact_changed_during_verification");
        observations[path] = hash;
        using var document = JsonDocument.Parse(text);
        RejectDuplicateProperties(document.RootElement);
        return document.RootElement.Clone();
    }

    private static string ReadLogicalText(string path) =>
        ContentAddressedJsonArtifactReader.MaterializeIfNeeded(path, File.ReadAllText(path));

    private static string FrozenPath(string directory, string path)
    {
        Require(!string.IsNullOrWhiteSpace(path), "frozen_artifact_path_missing");
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(directory, path));
        Require(string.Equals(Path.GetDirectoryName(fullPath), directory.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0 &&
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, "artifact_outside_frozen_directory");
        return fullPath;
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToArray();
            Require(properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length,
                "artifact_duplicate_json_property");
            foreach (var property in properties) RejectDuplicateProperties(property.Value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var element in value.EnumerateArray()) RejectDuplicateProperties(element);
    }

    private static bool SameJson(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        if (left.ValueKind == JsonValueKind.Object)
        {
            var properties = left.EnumerateObject().ToArray();
            return properties.Length == right.EnumerateObject().Count() && properties.All(property =>
                right.TryGetProperty(property.Name, out var value) && SameJson(property.Value, value));
        }
        if (left.ValueKind == JsonValueKind.Array)
            return left.GetArrayLength() == right.GetArrayLength() && left.EnumerateArray()
                .Zip(right.EnumerateArray(), SameJson).All(value => value);
        return left.ValueKind == JsonValueKind.String ? left.GetString() == right.GetString() : left.GetRawText() == right.GetRawText();
    }

    private static JsonElement Property(JsonElement value, string name) => value.GetProperty(name);
    private static string Text(JsonElement value, string name) => Property(value, name).GetString() ?? string.Empty;
    private static int Number(JsonElement value, string name) => Property(value, name).GetInt32();
    private static long Number64(JsonElement value, string name) => Property(value, name).GetInt64();
    private static bool Bool(JsonElement value, string name) => Property(value, name).GetBoolean();
    private static bool EmptyArray(JsonElement value, string name) => Property(value, name).ValueKind == JsonValueKind.Array && Property(value, name).GetArrayLength() == 0;
    private static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant();
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new EvidenceException(reason); }
    private sealed class EvidenceException : Exception { public EvidenceException(string reason) : base(reason) { } }
    private sealed record Progress(int Fulfilled, int[] Quantities, bool Mail, bool Talked, int ReadyCrops,
        string Location, bool Host, bool MenuClear, int Debris, bool Triggered, bool MutexAvailable, bool MutexLocked,
        string NativeContract, bool IsCurrent, string ProjectionStatus);
}
