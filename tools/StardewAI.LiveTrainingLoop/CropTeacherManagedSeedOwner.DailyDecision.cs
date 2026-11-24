using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherDailyDecisionReference(string Path, string Sha256);

internal sealed record CropTeacherManagedDailyDecision(int Iteration, int TotalDay, string GoalId,
    string SourceSnapshotPath, string PlanPath, string RankingPath, string[] OwnedCandidateIds,
    CropTeacherDailyDecisionReference Reference, IReadOnlySet<string> CompletedForeignCandidateIds)
{
    public IReadOnlyDictionary<string, JsonObject> ForeignObjectiveContinuations { get; init; } =
        new Dictionary<string, JsonObject>(StringComparer.Ordinal);
}

public sealed partial class CropTeacherManagedSeedOwner
{
    private static readonly Regex DailyDecisionName = new(@"^joint-daily-decision-(\d{8,})-([0-9a-f]{64})\.json$", RegexOptions.CultureInvariant);

    internal async Task<CropTeacherDailyDecisionReference> SaveDailyDecisionAsync(int iteration, string sourcePath,
        string planPath, string rankingPath, IReadOnlyCollection<string> ownedIds)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireDailyDecisionHead();
            if (iteration < 1 || !SamePath(sourcePath, headSnapshotPath))
                throw new InvalidDataException("Daily decision must bind the original current owner source/iteration.");
            var inputs = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
            foreach (var path in new[] { sourcePath, planPath, rankingPath }) inputs.Pin(path);
            var value = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_daily_decision.v1", ["iteration"] = iteration,
                ["total_day"] = SnapshotInt(inputs.Read<SnapshotEnvelope>(sourcePath), "time", "total_days"),
                ["run_id"] = options.RunId, ["owner_header_sha256"] = headerHash,
                ["owner_anchor_sequence"] = lastAcceptedOperation?.Sequence ?? 0, ["owner_anchor_sha256"] = lastAcceptedHash,
                ["source_snapshot_path"] = Path.GetFullPath(sourcePath), ["decision_plan_path"] = Path.GetFullPath(planPath),
                ["decision_ranking_path"] = Path.GetFullPath(rankingPath),
                ["owned_candidate_ids"] = JsonSerializer.SerializeToNode(ownedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(), JsonOptions),
                ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(inputs.Hashes, JsonOptions),
                ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
            };
            var text = value.ToJsonString(JsonOptions);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            var pathName = Path.Combine(options.EvidenceDirectory, $"joint-daily-decision-{iteration:D8}-{hash}.json");
            var existing = Directory.EnumerateFiles(options.EvidenceDirectory, $"joint-daily-decision-{iteration:D8}-*.json").ToArray();
            if (existing.Any(path => !SamePath(path, pathName)))
                throw new IOException("Daily decision iteration is already retained with another decision.");
            // Validate before publishing; this is metadata, never another owner/native operation.
            _ = ReadDailyDecisionValue(value, new(pathName, hash), ReadAcceptedPlanningCertificates().ToArray());
            inputs.Validate();
            if (!File.Exists(pathName)) await SaveAsync(pathName, value).ConfigureAwait(false);
            if (HashFile(pathName) != hash) throw new InvalidDataException("Daily decision writer changed its immutable body.");
            TrackFrontier(pathName); ValidateState();
            return new(pathName, hash);
        }
        finally { gate.Release(); }
    }

    internal async Task<CropTeacherManagedDailyDecision?> ReadDailyDecisionAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireDailyDecisionHead();
            var day = SnapshotInt(Read<SnapshotEnvelope>(headSnapshotPath), "time", "total_days");
            (int Sequence, string Path, string Hash, JsonObject Certificate)[]? history = null;
            var candidates = new List<CropTeacherManagedDailyDecision>();
            foreach (var path in Directory.EnumerateFiles(options.EvidenceDirectory, "joint-daily-decision-*.json"))
            {
                var match = DailyDecisionName.Match(Path.GetFileName(path));
                if (!match.Success) throw new InvalidDataException("Daily decision record name is invalid.");
                var reference = new CropTeacherDailyDecisionReference(path, match.Groups[2].Value);
                var metadata = ReadBoundPlanningRecord(path, reference.Sha256);
                if (Text(metadata, "owner_header_sha256") != headerHash || Text(metadata, "run_id") != options.RunId)
                    throw new InvalidDataException("Retained daily metadata belongs to another owner.");
                // Expired choices need no historical snapshot graph materialization.
                if (metadata["total_day"]?.GetValue<int>() != day) continue;
                history ??= ReadAcceptedPlanningCertificates().ToArray();
                var decision = ReadDailyDecisionReference(reference, history);
                if (decision.TotalDay == day) candidates.Add(decision);
            }
            if (candidates.GroupBy(row => row.Iteration).Any(group => group.Count() != 1))
                throw new InvalidDataException("Daily decision iteration is ambiguous.");
            var latest = candidates.OrderByDescending(row => row.Iteration).FirstOrDefault();
            ValidateState(); return latest;
        }
        finally { gate.Release(); }
    }

    private void RequireDailyDecisionHead()
    {
        ValidateState();
        if (Pending is not null || HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the original owner before reading or saving its daily decision.");
    }

    private CropTeacherManagedDailyDecision ReadDailyDecisionReference(CropTeacherDailyDecisionReference reference,
        (int Sequence, string Path, string Hash, JsonObject Certificate)[] history)
    {
        if (!Path.IsPathFullyQualified(reference.Path) || !SamePath(Path.GetDirectoryName(reference.Path)!, options.EvidenceDirectory) ||
            !DailyDecisionName.IsMatch(Path.GetFileName(reference.Path)) || HashFile(reference.Path) != reference.Sha256 ||
            DailyDecisionName.Match(Path.GetFileName(reference.Path)).Groups[2].Value != reference.Sha256)
            throw new InvalidDataException("Daily decision reference changed or escaped its original owner.");
        var decision = ReadDailyDecisionValue(Read<JsonObject>(reference.Path), reference, history);
        if (HashFile(reference.Path) != reference.Sha256) throw new InvalidDataException("Daily decision changed during readback.");
        return decision;
    }

    private CropTeacherManagedDailyDecision ReadDailyDecisionValue(JsonObject value, CropTeacherDailyDecisionReference reference,
        (int Sequence, string Path, string Hash, JsonObject Certificate)[] history)
    {
        var anchor = value["owner_anchor_sequence"]?.GetValue<int>() ?? -1;
        var anchorRecord = history.SingleOrDefault(row => row.Sequence == anchor);
        if (Text(value, "schema_version") != "crop_teacher_managed_daily_decision.v1" || Text(value, "run_id") != options.RunId ||
            Text(value, "owner_header_sha256") != headerHash || anchor < 0 || anchor > history.Length ||
            Text(value, "owner_anchor_sha256") != (anchor == 0 ? headerHash : anchorRecord.Hash) ||
            value["native_dispatch_performed"]?.GetValue<bool>() != false || value["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Daily decision is not anchored to this original accepted owner.");
        var iteration = value["iteration"]!.GetValue<int>();
        if (iteration < 1 || !Path.GetFileName(reference.Path).StartsWith($"joint-daily-decision-{iteration:D8}-", StringComparison.Ordinal))
            throw new InvalidDataException("Daily decision ordinal differs from its immutable record.");
        var sourcePath = StoredPath(value, "source_snapshot_path"); var planPath = StoredPath(value, "decision_plan_path");
        var rankingPath = StoredPath(value, "decision_ranking_path");
        if (!SamePath(sourcePath, anchor == 0 ? options.InitialSnapshotPath : StoredPath(anchorRecord.Certificate, "after_snapshot_path")))
            throw new InvalidDataException("Daily decision source is not its original accepted anchor.");
        var hashes = value["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonOptions)!;
        if (hashes.Count != 3 || hashes.Keys.Any(path => !Path.IsPathFullyQualified(path)) ||
            new[] { sourcePath, planPath, rankingPath }.Distinct(PathComparer).Count() != 3 ||
            new[] { sourcePath, planPath, rankingPath }.Any(path => !hashes.Keys.Any(key => SamePath(key, path))))
            throw new InvalidDataException("Daily decision input denominator changed.");
        var inputs = new CropSeedRuntimeArtifacts(hashes);
        var source = inputs.Read<SnapshotEnvelope>(sourcePath); var decision = inputs.Read<SmallModelPlanEnvelope>(planPath);
        var ranking = inputs.Read<JsonObject>(rankingPath); var lease = SelectedQueueDecisionLease.CreateFromPlan(decision, ranking);
        var owned = value["owned_candidate_ids"]!.Deserialize<string[]>(JsonOptions)!;
        if (decision.StateHash != source.StateHash || decision.ExecutionMode != "training_singleplayer" ||
            decision.SourceModel != "StardewAI.Core.Training.DailyPlanCompiler" || decision.PlanType != "daily_candidate_plan" ||
            source.SaveId.Value != saveId || source.PlayerId.Value != playerId ||
            value["total_day"]?.GetValue<int>() != SnapshotInt(source, "time", "total_days") ||
            owned.Any(string.IsNullOrWhiteSpace) || owned.Distinct(StringComparer.Ordinal).Count() != owned.Length || lease.Candidates.Count == 0)
            throw new InvalidDataException("Daily decision source/actor/plan/ownership is invalid.");
        var progress = ReadAcceptedDailyProgress(history, anchor, sourcePath, planPath, rankingPath, owned, lease);
        inputs.Validate();
        return new(iteration, SnapshotInt(source, "time", "total_days"), decision.GoalId, sourcePath, planPath, rankingPath, owned, reference,
            progress.Completed) { ForeignObjectiveContinuations = progress.Continuations };
    }
}
