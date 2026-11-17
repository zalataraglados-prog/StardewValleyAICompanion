using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class MoneyRevenueCropManagedProvenance
{
    public void RequireNewRunDirectory(string directory) => Require(!request.PriorEpisodeDirectories.Any(path =>
        Same(path, directory)), "A new provenance run must not overwrite a retained prior episode directory.");

    public JsonObject Build(params string[] additionalEpisodeDirectories)
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        var directories = request.PriorEpisodeDirectories.Concat(additionalEpisodeDirectories).Select(Path.GetFullPath).ToArray();
        Require(directories.Distinct(Comparer).Count() == directories.Length, "Provenance episode directories repeat.");
        var segments = new JsonArray(); var seen = new HashSet<string>(StringComparer.Ordinal);
        var heldEdges = new JsonArray();
        var ownedPauseEdges = new JsonArray();
        PlanExecutionEpisodeEnvelope? previous = null; SnapshotEnvelope? previousAfter = null; string? previousPath = null;
        GoldenWalnutManagedReadbackSourceBinding? binding = null;
        var verified = true; var rawCount = 0; var observationCount = 0;
        foreach (var directory in directories)
        {
            Require(Path.IsPathFullyQualified(directory) && Directory.Exists(directory), "Provenance episode directory is missing.");
            var paths = Directory.GetFiles(directory, "plan-execution-episode-*.json");
            if (paths.Length == 0) paths = Directory.GetFiles(directory, GoldenWalnutFreshSaveEpisodeArchive.EpisodeSearchPattern);
            var records = paths.Select(path => new EpisodeRow(Pin(path), ReadEpisode(path))).ToArray();
            Require(records.All(row => seen.Add(row.Episode.EpisodeId)), "Provenance contains duplicate episode identities.");
            var selected = records.GroupBy(row => row.ParentId, StringComparer.Ordinal).SelectMany(group =>
            {
                var items = group.Where(row => row.ItemOrdinal > 0).OrderBy(row => row.ItemOrdinal).ToArray();
                if (items.Length == 0) items = group.ToArray();
                else
                Require(items.Select(row => row.ItemOrdinal).SequenceEqual(Enumerable.Range(1, items.Length)), "Sequential episode observations have a missing item.");
                return new[] { new { Rows = items, Iteration = items[0].Iteration,
                    Tick = items.Min(row => row.Episode.BeforeGameTick), ParentId = group.Key } };
            }).OrderBy(group => group.Iteration).ThenBy(group => group.Tick).ThenBy(group => group.ParentId, StringComparer.Ordinal)
                .SelectMany(group => group.Rows).ToArray();
            foreach (var row in selected)
            {
                var episode = row.Episode;
                var before = Snapshot(row.Path, episode.BeforeSnapshotPath, episode.SourceStateHash, episode.BeforeGameTick);
                if (binding is null)
                {
                    binding = GoldenWalnutManagedReadbackSourceBindingBuilder.Build(new GoldenWalnutManagedReadbackSourceBindingRequest(
                        "golden_walnut_managed_readback_source_binding_request.v1", RunId, nativePath, row.Path, request.AcceptedSavedReadbacks));
                    Require(Same(binding.LoadedSnapshotPath, loadedPath), "Source binding reaches another original loaded head.");
                }
                else if (previous is not null && previousAfter is not null)
                {
                    try { Observe(previousPath!, previous, previousAfter, row.Path, episode, before); }
                    catch (InvalidDataException exception)
                    {
                        var edge = new JsonObject { ["previous_episode_path"] = previousPath, ["current_episode_path"] = row.Path,
                            ["previous_after_state_hash"] = previous.AfterStateHash, ["current_source_state_hash"] = episode.SourceStateHash,
                            ["previous_after_game_tick"] = previous.AfterGameTick, ["current_before_game_tick"] = episode.BeforeGameTick,
                            ["reason"] = exception.Message };
                        if (OwnedPauseObservation(previousAfter, before))
                        {
                            edge["binding_kind"] = "original_executor_owned_pause_preserved_progress";
                            ownedPauseEdges.Add(edge);
                        }
                        else { verified = false; heldEdges.Add(edge); }
                    }
                }
                if (!string.IsNullOrWhiteSpace(episode.ExecutionResultPath)) Pin(Resolve(row.Path, episode.ExecutionResultPath));
                if (!episode.AfterSnapshotFresh)
                {
                    verified = false; previous = null; previousAfter = null; previousPath = null; continue;
                }
                Require(episode.AfterGameTick >= episode.BeforeGameTick, "Episode's original clock reversed.");
                previousAfter = Snapshot(row.Path, episode.AfterSnapshotPath, episode.AfterStateHash, episode.AfterGameTick);
                previous = episode; previousPath = row.Path;
            }
            rawCount += records.Length; observationCount += selected.Length;
            segments.Add(new JsonObject
            {
                ["episode_directory"] = directory,
                ["retained_episode_payloads"] = JsonSerializer.SerializeToNode(records.OrderBy(row => row.Path, StringComparer.Ordinal)
                    .Select(row => new MoneyRevenueArtifactBinding("original_episode", row.Path, direct[row.Path])), JsonDefaults.Options),
                ["observation_episode_paths"] = JsonSerializer.SerializeToNode(selected.Select(row => row.Path), JsonDefaults.Options),
                ["unverified_episode_ids"] = JsonSerializer.SerializeToNode(selected.Where(row => !row.Episode.AfterSnapshotFresh)
                    .Select(row => row.Episode.EpisodeId), JsonDefaults.Options)
            });
        }
        retained.Validate();
        foreach (var (path, hash) in inherited.Concat(direct).DistinctBy(pair => pair.Key, Comparer))
            Require(RegularHash(path) == hash, "Retained provenance input changed during continuation.");
        foreach (var (path, hash) in snapshotLogicalHashes)
            Require(ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path) == hash, "Snapshot blob closure changed during continuation.");
        var result = (JsonObject)summary.DeepClone();
        result["readback_source_binding"] = binding is null ? null : JsonSerializer.SerializeToNode(binding, JsonDefaults.Options);
        result["segments"] = segments;
        result["held_observation_edges"] = heldEdges;
        result["owned_pause_observation_edges"] = ownedPauseEdges;
        result["retained_episode_payload_count"] = rawCount;
        result["observation_episode_count"] = observationCount;
        result["continuation_observation_linkage_verified"] = verified && binding is not null;
        result["last_observed_episode_path"] = previousPath;
        result["last_observed_snapshot_path"] = previous is null ? null : Resolve(previousPath!, previous.AfterSnapshotPath);
        result["last_observed_state_hash"] = previousAfter?.StateHash;
        result["last_observed_game_tick"] = previousAfter?.GameTick;
        result["post_closeout_native_execution_replayed"] = false;
        // The inherited closure stays in its original frozen report, not copied into every new run.
        result["input_artifact_hashes"] = JsonSerializer.SerializeToNode(direct, JsonDefaults.Options);
        return result;
    }

    private sealed class EpisodeRow
    {
        internal string Path { get; }
        internal PlanExecutionEpisodeEnvelope Episode { get; }
        internal int Iteration { get; }
        internal string ParentId { get; }
        internal int ItemOrdinal { get; }
        internal EpisodeRow(string path, PlanExecutionEpisodeEnvelope episode)
        {
            Path = path; Episode = episode;
            var iteration = Regex.Match(System.IO.Path.GetFileName(path), @"^(?:plan-execution-episode-|golden-walnut-fresh-save-episode-)(\d+)");
            Require(iteration.Success && int.TryParse(iteration.Groups[1].Value, out _), "Original episode filename has no iteration.");
            Iteration = int.Parse(iteration.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var item = Regex.Match(episode.EpisodeId, @"\.item\.(\d+)$");
            ParentId = item.Success ? episode.EpisodeId[..item.Index] : episode.EpisodeId;
            ItemOrdinal = item.Success ? int.Parse(item.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        }
    }
}
